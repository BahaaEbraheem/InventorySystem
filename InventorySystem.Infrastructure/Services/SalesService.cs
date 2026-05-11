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
        // التحقق من صحة الطلب
        if (request == null || request.Items == null || !request.Items.Any())
            throw new ArgumentException("يجب أن يحتوي الطلب على عنصر واحد على الأقل");

        if (request.SaleDate == default)
            throw new ArgumentException("تاريخ البيع مطلوب");

        // منع الضغط مرتين باستخدام IdempotencyKey
        var exists = await _dbContext.Sales
            .AnyAsync(s => s.IdempotencyKey == request.IdempotencyKey, cancellationToken);

        if (exists)
            throw new InvalidOperationException("تم إرسال نفس الطلب مسبقاً");

        // استراتيجية التنفيذ لدعم إعادة المحاولة
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);

            try
            {
                // التحقق من أن المستودع موجود ونشط
                var warehouseId = request.Items.First().WarehouseId;
                var warehouse = await _dbContext.Warehouses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(w => w.Id == warehouseId && w.IsActive, cancellationToken);

                if (warehouse == null)
                    throw new InvalidOperationException("المستودع غير موجود أو غير نشط");

                // إنشاء عملية البيع
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
                    // إنشاء عنصر البيع
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

                    // جلب الدفعات مع قفل الصفوف
                    var batches = await _dbContext.StockBatches
                         .FromSqlRaw(@"
                        SELECT * FROM StockBatches WITH (ROWLOCK, UPDLOCK)
                        WHERE ProductId = {0} AND WarehouseId = {1} AND QuantityRemaining > 0",
                             item.ProductId, item.WarehouseId)
                         .ToListAsync(cancellationToken);

                    // الترتيب يتم هنا باستخدام LINQ
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

                        // منطق الحجز
                        if (batch.QuantityAvailable < allocateQty)
                            throw new InvalidOperationException("المخزون غير كافٍ بعد التحقق من الحجز");

                        batch.QuantityReserved += allocateQty;
                        batch.QuantityRemaining -= allocateQty;
                        batch.QuantityReserved -= allocateQty;

                        remainingToAllocate -= allocateQty;

                        // إنشاء التخصيص
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

                        // إشعار بتغيير المخزون
                        await _notificationService.NotifyStockChangedAsync(
                            batch.ProductId, batch.WarehouseId, batch.QuantityRemaining);

                        // إشعار بانخفاض المخزون تحت الحد
                        const decimal threshold = 5;
                        if (batch.QuantityRemaining < threshold)
                        {
                            await _notificationService.NotifyStockBelowThresholdAsync(
                                batch.ProductId, batch.WarehouseId, batch.QuantityRemaining, threshold);
                        }
                    }
                }

                // حفظ التغييرات
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
