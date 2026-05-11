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
        // ✅ الحصول على استراتيجية التنفيذ لدعم إعادة المحاولة
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);

            try
            {
                // ✅ 1. التحقق من أن المستودع موجود ونشط (مهم لـ FK)
                var warehouse = await _dbContext.Warehouses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(w => w.Id == request.Items.First().WarehouseId && w.IsActive, cancellationToken);

                if (warehouse == null)
                    throw new InvalidOperationException($"Warehouse not found or inactive");

                // ✅ 2. إنشاء عملية البيع
                var sale = new Sale
                {
                    Id = Guid.NewGuid(),
                    WarehouseId = warehouse.Id,  // ✅ تعيين الـ FK صراحةً
                    SaleDate = request.SaleDate,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                };

                // ✅ 3. إضافة البيع للـ Context أولاً (لضمان توليد الـ Id)
                await _dbContext.Sales.AddAsync(sale, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);  // ✅ حفظ لضمان وجود الـ Sale

                foreach (var item in request.Items)
                {
                    // ✅ 4. إنشاء عنصر البيع مع تعيين الـ FKs صراحةً
                    var saleItem = new SaleItem
                    {
                        Id = Guid.NewGuid(),
                        SaleId = sale.Id,           // ✅ تعيين الـ FK صراحةً
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "system"
                    };

                    await _dbContext.SaleItems.AddAsync(saleItem, cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);  // ✅ حفظ لضمان وجود الـ SaleItem





                    var batches = await _dbContext.StockBatches
                     .Where(b =>
                         b.ProductId == item.ProductId &&
                         b.WarehouseId == item.WarehouseId &&
                         b.QuantityRemaining > 0)
                     .OrderBy(b => b.ReceivedDate)   // ✅ الأقدم استلامًا أولًا
                     .ThenBy(b => b.CreatedAt)
                     .ThenBy(b => b.Id)
                     .ToListAsync(cancellationToken);




                    var totalAvailable = batches.Sum(b => b.QuantityRemaining);
                    if (totalAvailable < item.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"Insufficient stock for product {item.ProductId} in warehouse {item.WarehouseId}. " +
                            $"Available: {totalAvailable}, Requested: {item.Quantity}");
                    }

                    var remainingToAllocate = item.Quantity;

                    foreach (var batch in batches)
                    {
                        if (remainingToAllocate <= 0) break;

                        var allocateQty = Math.Min(batch.QuantityRemaining, remainingToAllocate);
                        batch.QuantityRemaining -= allocateQty;
                        remainingToAllocate -= allocateQty;

                        // ✅ 6. إنشاء التخصيص مع تعيين الـ FKs صراحةً
                        var allocation = new SaleItemBatchAllocation
                        {
                            Id = Guid.NewGuid(),
                            SaleItemId = saleItem.Id,   // ✅ تعيين الـ FK صراحةً
                            StockBatchId = batch.Id,    // ✅ تعيين الـ FK صراحةً
                            Quantity = allocateQty,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "system"
                        };

                        await _dbContext.SaleItemBatchAllocations.AddAsync(allocation, cancellationToken);

                        // ✅ إشعار بتغيير المخزون
                        await _notificationService.NotifyStockChangedAsync(
                            batch.ProductId, batch.WarehouseId, batch.QuantityRemaining);

                        // ✅ إشعار بانخفاض المخزون تحت الحد
                        const decimal threshold = 5;
                        if (batch.QuantityRemaining < threshold)
                        {
                            await _notificationService.NotifyStockBelowThresholdAsync(
                                batch.ProductId, batch.WarehouseId, batch.QuantityRemaining, threshold);
                        }
                    }
                }

                // ✅ 7. حفظ كل التغييرات المتبقية
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
