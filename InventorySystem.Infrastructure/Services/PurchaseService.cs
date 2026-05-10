using InventorySystem.Application.DTOs.Purchase;
using InventorySystem.Application.Interfaces;
using InventorySystem.Domain.Entities;
using InventorySystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Infrastructure.Services;

public class PurchaseService : IPurchaseService
{
    private readonly AppDbContext _dbContext;

    public PurchaseService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CreatePurchaseOrderResponse> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        // ممكن تضيف هنا validation (وجود Supplier, Products, Warehouses)

        var purchaseOrder = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            SupplierId = request.SupplierId,
            PurchaseDate = request.PurchaseDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system" // لاحقًا تربطها بالـ User
        };

        foreach (var item in request.Items)
        {
            var poItem = new PurchaseOrderItem
            {
                Id = Guid.NewGuid(),
                PurchaseOrder = purchaseOrder,
                ProductId = item.ProductId,
                WarehouseId = item.WarehouseId,
                UnitCost = item.UnitCost,
                Quantity = item.Quantity,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };

            purchaseOrder.Items.Add(poItem);

            var stockBatch = new StockBatch
            {
                Id = Guid.NewGuid(),
                ProductId = item.ProductId,
                SupplierId = request.SupplierId,
                WarehouseId = item.WarehouseId,
                PurchaseOrderItem = poItem,
                QuantityReceived = item.Quantity,
                QuantityRemaining = item.Quantity,
                PurchaseDate = request.PurchaseDate,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };

            await _dbContext.StockBatches.AddAsync(stockBatch, cancellationToken);
        }

        await _dbContext.PurchaseOrders.AddAsync(purchaseOrder, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreatePurchaseOrderResponse
        {
            PurchaseOrderId = purchaseOrder.Id
        };
    }
}
