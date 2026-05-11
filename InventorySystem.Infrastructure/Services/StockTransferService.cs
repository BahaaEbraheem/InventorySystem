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
        if (request == null || request.Items == null || !request.Items.Any())
            throw new ArgumentException("يجب أن يحتوي طلب التحويل على عنصر واحد على الأقل");

        // التحقق من التكرار باستخدام IdempotencyKey
        var exists = await _dbContext.StockTransfers
            .AnyAsync(t => t.IdempotencyKey == request.IdempotencyKey, cancellationToken);

        if (exists)
            throw new InvalidOperationException("تم إرسال نفس طلب التحويل مسبقاً");

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

            try
            {
                var transfer = new StockTransfer
                {
                    Id = Guid.NewGuid(),
                    FromWarehouseId = request.FromWarehouseId,
                    ToWarehouseId = request.ToWarehouseId,
                    TransferDate = request.TransferDate,
                    IdempotencyKey = request.IdempotencyKey, // إضافة المفتاح هنا
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                };

                foreach (var item in request.Items)
                {
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

                    var totalAvailable = sourceBatches.Sum(b => b.QuantityRemaining);
                    if (totalAvailable < item.Quantity)
                        throw new InvalidOperationException($"المخزون غير كافٍ في المستودع المصدر للمنتج {item.ProductId}");

                    var remainingToMove = item.Quantity;

                    foreach (var batch in sourceBatches)
                    {
                        if (remainingToMove <= 0) break;

                        var moveQty = Math.Min(batch.QuantityRemaining, remainingToMove);

                        // منطق الحجز
                        if (batch.QuantityAvailable < moveQty)
                            throw new InvalidOperationException("المخزون غير كافٍ بعد التحقق من الحجز");

                        batch.QuantityReserved += moveQty;
                        batch.QuantityRemaining -= moveQty;
                        batch.QuantityReserved -= moveQty;

                        remainingToMove -= moveQty;

                        // إنشاء Batch جديد في المستودع الوجهة
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

                        // إشعارات دقيقة
                        await _notificationService.NotifyStockChangedAsync(batch.ProductId, batch.WarehouseId, batch.QuantityRemaining);
                        await _notificationService.NotifyStockChangedAsync(destBatch.ProductId, destBatch.WarehouseId, destBatch.QuantityRemaining);
                    }
                }

                await _dbContext.StockTransfers.AddAsync(transfer, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new CreateStockTransferResponse { StockTransferId = transfer.Id };
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}

