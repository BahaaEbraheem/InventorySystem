using InventorySystem.Application.DTOs.Transfers;
using InventorySystem.Application.Interfaces;
using InventorySystem.Domain.Entities;
using InventorySystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Infrastructure.Services;

public class StockTransferService : IStockTransferService
{
    private readonly AppDbContext _dbContext;
    private readonly INotificationService _notificationService;

    public StockTransferService(AppDbContext dbContext, INotificationService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }
    public async Task<CreateStockTransferResponse> CreateTransferAsync(CreateStockTransferRequest request, CancellationToken cancellationToken = default)
    {
        //  تحقق من التكرار باستخدام IdempotencyKey
        if (request.IdempotencyKey != Guid.Empty)
        {
            var exists = await _dbContext.StockTransfers
                .AnyAsync(t => t.IdempotencyKey == request.IdempotencyKey, cancellationToken);

            if (exists)
                throw new InvalidOperationException("تم إرسال نفس طلب التحويل مسبقاً");
        }

        //  Validation أساسي
        if (request == null)
            throw new ArgumentNullException(nameof(request), "طلب التحويل لا يمكن أن يكون فارغاً");

        if (request.Items == null || !request.Items.Any())
            throw new ArgumentException("يجب أن يحتوي طلب التحويل على عنصر واحد على الأقل");

        if (request.FromWarehouseId == request.ToWarehouseId)
            throw new ArgumentException("لا يمكن التحويل إلى نفس المستودع");


        //التعامل مع التنافس (Concurrency)
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            //ضمان أن العملية إما تنفذ بالكامل أو تُلغى بالكامل (Atomic Transaction)
            using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

            try
            {
                var transfer = new StockTransfer
                {
                    Id = Guid.NewGuid(),
                    FromWarehouseId = request.FromWarehouseId,
                    ToWarehouseId = request.ToWarehouseId,
                    TransferDate = request.TransferDate,
                    IdempotencyKey = request.IdempotencyKey,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system",
                    Items = new List<StockTransferItem>()
                };

                foreach (var item in request.Items)
                {
                    if (item.Quantity <= 0)
                        throw new ArgumentException($"الكمية للمنتج {item.ProductId} يجب أن تكون أكبر من صفر");

                    var transferItem = new StockTransferItem
                    {
                        Id = Guid.NewGuid(),
                        StockTransfer = transfer,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "system"
                    };

                    transfer.Items.Add(transferItem);

                    // جلب الدفعات من المستودع المصدر مع قفل الصفوف
                    var sourceBatches = await _dbContext.StockBatches
                        .FromSqlRaw(@"
                                SELECT * FROM StockBatches WITH (ROWLOCK, UPDLOCK)
                                WHERE ProductId = {0} AND WarehouseId = {1} AND QuantityRemaining > 0",
                            item.ProductId, request.FromWarehouseId)
                        .ToListAsync(cancellationToken);

                    sourceBatches = sourceBatches
                        .OrderBy(b => b.PurchaseDate)
                        .ThenBy(b => b.CreatedAt)
                        .ToList();
                    //منع حدوث مخزون سلبي في المستودع المصدر عن طريق التحقق من الكمية المتاحة قبل الخصم:
                    var totalAvailable = sourceBatches.Sum(b => b.QuantityRemaining);
                    if (totalAvailable < item.Quantity)
                    {
                        await _notificationService.NotifyTransferFailedAsync(item.ProductId, request.FromWarehouseId, item.Quantity);
                        throw new InvalidOperationException($"المخزون غير كافٍ في المستودع المصدر للمنتج {item.ProductId}");
                    }

                    var remainingToMove = item.Quantity;

                    foreach (var batch in sourceBatches)
                    {
                        if (remainingToMove <= 0) break;

                        var moveQty = Math.Min(batch.QuantityRemaining, remainingToMove);
                        batch.QuantityRemaining -= moveQty;
                        remainingToMove -= moveQty;

                        var destBatch = new StockBatch
                        {
                            Id = Guid.NewGuid(),
                            ProductId = batch.ProductId,
                            SupplierId = batch.SupplierId,
                            WarehouseId = request.ToWarehouseId,
                            PurchaseOrderItemId = batch.PurchaseOrderItemId,
                            QuantityReceived = moveQty,
                            QuantityRemaining = moveQty,
                            PurchaseDate = batch.PurchaseDate,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "system"
                        };

                        await _dbContext.StockBatches.AddAsync(destBatch, cancellationToken);
                        _dbContext.StockBatches.Update(batch);

                        //  إشعارات دقيقة
                        await _notificationService.NotifyStockChangedAsync(batch.ProductId, batch.WarehouseId, batch.QuantityRemaining);
                        await _notificationService.NotifyStockChangedAsync(destBatch.ProductId, destBatch.WarehouseId, destBatch.QuantityRemaining);
                    }
                }

                await _dbContext.StockTransfers.AddAsync(transfer, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                //  إشعار نجاح التحويل
                await _notificationService.NotifyTransferReceivedAsync(transfer.Id);

                return new CreateStockTransferResponse { StockTransferId = transfer.Id };
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }


    /// <summary>
    /// Retrieves full transfer details including items and warehouse info.
    /// Moved from controller to keep the API layer thin.
    /// </summary>
    public async Task<StockTransferDetailDto?> GetTransferByIdAsync(
        Guid transferId,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _dbContext.StockTransfers
            .AsNoTracking()
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Include(t => t.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(t => t.Id == transferId, cancellationToken);

        if (transfer == null)
            return null;

        return new StockTransferDetailDto
        {
            Id = transfer.Id,
            TransferDate = transfer.TransferDate,
            FromWarehouseId = transfer.FromWarehouseId,
            FromWarehouseName = transfer.FromWarehouse?.Name ?? string.Empty,
            ToWarehouseId = transfer.ToWarehouseId,
            ToWarehouseName = transfer.ToWarehouse?.Name ?? string.Empty,
            Status = transfer.Status.ToString(),
            CompletedAt = transfer.CompletedAt,
            Items = transfer.Items.Select(i => new StockTransferItemDetailDto
            {
                ProductId = i.ProductId,
                ProductName = i.Product?.Name ?? string.Empty,
                Quantity = i.Quantity
            }).ToList()
        };
    }
}

