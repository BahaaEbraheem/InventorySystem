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

    public async Task<CreateSaleResponse> CreateSaleAsync(CreateSaleRequest request, CancellationToken cancellationToken = default)
    {
        // IsolationLevel.Serializable لتفادي الـ race conditions
        using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            SaleDate = request.SaleDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        foreach (var item in request.Items)
        {
            var saleItem = new SaleItem
            {
                Id = Guid.NewGuid(),
                Sale = sale,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };

            sale.Items.Add(saleItem);

            // 1) اجلب كل الـ StockBatches المتاحة لهذا المنتج في هذا المخزن بالترتيب (FIFO)
            var batches = await _dbContext.StockBatches
                .Where(b =>
                    b.ProductId == item.ProductId &&
                    b.WarehouseId == item.WarehouseId &&
                    b.QuantityRemaining > 0)
                .OrderBy(b => b.PurchaseDate)
                .ThenBy(b => b.CreatedAt)
                .ToListAsync(cancellationToken);

            var totalAvailable = batches.Sum(b => b.QuantityRemaining);
            if (totalAvailable < item.Quantity)
            {
                throw new InvalidOperationException($"Insufficient stock for product {item.ProductId} in warehouse {item.WarehouseId}.");
            }

            var remainingToAllocate = item.Quantity;

            foreach (var batch in batches)
            {
                if (remainingToAllocate <= 0)
                    break;

                var allocateQty = Math.Min(batch.QuantityRemaining, remainingToAllocate);

                batch.QuantityRemaining -= allocateQty;
                remainingToAllocate -= allocateQty;

                var allocation = new SaleItemBatchAllocation
                {
                    Id = Guid.NewGuid(),
                    SaleItem = saleItem,
                    StockBatch = batch,
                    Quantity = allocateQty,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                };

                saleItem.BatchAllocations.Add(allocation);
                _dbContext.SaleItemBatchAllocations.Add(allocation);
                _dbContext.StockBatches.Update(batch);

                //  إشعار بتغيير المخزون
                await _notificationService.NotifyStockChangedAsync(
                    batch.ProductId,
                    batch.WarehouseId,
                    batch.QuantityRemaining
                );

                //  إشعار بانخفاض المخزون تحت الحد
                decimal threshold = 5; // لاحقًا نقرأه من جدول Thresholds
                if (batch.QuantityRemaining < threshold)
                {
                    await _notificationService.NotifyStockBelowThresholdAsync(
                        batch.ProductId,
                        batch.WarehouseId,
                        batch.QuantityRemaining,
                        threshold
                    );
                }
            }

        }

        await _dbContext.Sales.AddAsync(sale, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CreateSaleResponse
        {
            SaleId = sale.Id
        };
    }
}
