using InventorySystem.Application.DTOs.Sales;
using InventorySystem.Application.Interfaces;
using InventorySystem.Domain.Entities;
using InventorySystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Transactions;

namespace InventorySystem.Infrastructure.Services;

public class SalesService : ISalesService
{
    private readonly AppDbContext _dbContext;
    private readonly INotificationService _notificationService;

    public SalesService(AppDbContext dbContext, INotificationService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }
    public async Task<CreateSaleResponse> CreateSaleAsync(
    CreateSaleRequest request,
    CancellationToken cancellationToken = default)
    {
        //  Problem 2: Concurrent Sales and Stock Accuracy
        // التحقق من صحة الطلب + منع الضغط مرتين باستخدام IdempotencyKey
        if (request == null || request.Items == null || !request.Items.Any())
            throw new ArgumentException("يجب أن يحتوي الطلب على عنصر واحد على الأقل");

        if (request.SaleDate == default)
            throw new ArgumentException("تاريخ البيع مطلوب");

        var exists = await _dbContext.Sales
            .AnyAsync(s => s.IdempotencyKey == request.IdempotencyKey, cancellationToken);

        if (exists)
            throw new InvalidOperationException("تم إرسال نفس الطلب مسبقاً");

        //  Problem 2: ضمان الذرية والتعامل مع التنافس
        // استخدام ExecutionStrategy + Serializable Transaction
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);

            try
            {
                //  Problem 2: التحقق من أن المستودع موجود ونشط
                var warehouseId = request.Items.First().WarehouseId;
                var warehouse = await _dbContext.Warehouses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(w => w.Id == warehouseId && w.IsActive, cancellationToken);

                if (warehouse == null)
                    throw new InvalidOperationException("المستودع غير موجود أو غير نشط");

                //  Problem 1: Tracking the Source of What They Sell
                // إنشاء عملية البيع وربطها بالـ SaleItem ثم بالـ StockBatch
                var sale = new Sale
                {
                    Id = Guid.NewGuid(),
                    WarehouseId = warehouse.Id,
                    SaleDate = request.SaleDate,
                    IdempotencyKey = request.IdempotencyKey,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                };

                await _dbContext.Sales.AddAsync(sale, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                foreach (var item in request.Items)
                {
                    var saleItem = new SaleItem
                    {
                        Id = Guid.NewGuid(),
                        SaleId = sale.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "system"
                    };

                    await _dbContext.SaleItems.AddAsync(saleItem, cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    //  Problem 2: منع المخزون السلبي + التعامل مع التنافس
                    // جلب الدفعات مع قفل الصفوف (ROWLOCK, UPDLOCK)
                    var batches = await _dbContext.StockBatches
                         .FromSqlRaw(@"
                    SELECT * FROM StockBatches WITH (ROWLOCK, UPDLOCK)
                    WHERE ProductId = {0} AND WarehouseId = {1} AND QuantityRemaining > 0",
                             item.ProductId, item.WarehouseId)
                         .ToListAsync(cancellationToken);

                    batches = batches
                        .OrderBy(b => b.ReceivedDate)
                        .ThenBy(b => b.CreatedAt)
                        .ThenBy(b => b.Id)
                        .ToList();

                    var totalAvailable = batches.Sum(b => b.QuantityRemaining);
                    if (totalAvailable < item.Quantity)
                        throw new InvalidOperationException("المخزون غير كافٍ");

                    var remainingToAllocate = item.Quantity;

                    foreach (var batch in batches)
                    {
                        if (remainingToAllocate <= 0) break;

                        var allocateQty = Math.Min(batch.QuantityRemaining, remainingToAllocate);

                        if (batch.QuantityAvailable < allocateQty)
                            throw new InvalidOperationException("المخزون غير كافٍ بعد التحقق من الحجز");

                        batch.QuantityReserved += allocateQty;
                        batch.QuantityRemaining -= allocateQty;
                        batch.QuantityReserved -= allocateQty;

                        remainingToAllocate -= allocateQty;

                        //  Problem 1: Tracking Source
                        // إنشاء SaleItemBatchAllocation يربط البيع بالدفعة الأصلية (المورد/الشراء)
                        var allocation = new SaleItemBatchAllocation
                        {
                            Id = Guid.NewGuid(),
                            SaleItemId = saleItem.Id,
                            StockBatchId = batch.Id,
                            Quantity = allocateQty,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "system"
                        };

                        await _dbContext.SaleItemBatchAllocations.AddAsync(allocation, cancellationToken);

                        // Problem 4: Real-Time Visibility
                        // إشعارات عند تغيير المخزون أو انخفاضه تحت الحد
                        await _notificationService.NotifyStockChangedAsync(
                            batch.ProductId, batch.WarehouseId, batch.QuantityRemaining);

                        const decimal threshold = 5;
                        if (batch.QuantityRemaining < threshold)
                        {
                            await _notificationService.NotifyStockBelowThresholdAsync(
                                batch.ProductId, batch.WarehouseId, batch.QuantityRemaining, threshold);
                        }
                    }
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new CreateSaleResponse { SaleId = sale.Id };
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }



}
