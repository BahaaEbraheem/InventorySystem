using InventorySystem.Domain.Entities;
using InventorySystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Infrastructure.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        // لو الداتا موجودة لا تعمل Seed
        if (await db.Products.AnyAsync())
            return;

        // Warehouses
        var warehouse1 = new Warehouse { Id = Guid.NewGuid(), Name = "Main Warehouse",IsActive=true, CreatedAt = DateTime.UtcNow, CreatedBy = "seed" };
        var warehouse2 = new Warehouse { Id = Guid.NewGuid(), Name = "Branch Warehouse", IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "seed" };

        // Suppliers
        var supplier1 = new Supplier { Id = Guid.NewGuid(), Name = "Supplier A", IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "seed" };
        var supplier2 = new Supplier { Id = Guid.NewGuid(), Name = "Supplier B", IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "seed" };

        // Categories
        var category1 = new ProductCategory { Id = Guid.NewGuid(), Name = "Electronics", CreatedAt = DateTime.UtcNow, CreatedBy = "seed" };
        var category2 = new ProductCategory { Id = Guid.NewGuid(), Name = "Food", CreatedAt = DateTime.UtcNow, CreatedBy = "seed" };

        // Products
        var product1 = new Product { Id = Guid.NewGuid(), Name = "Laptop", IsActive = true, CategoryId = category1.Id, CreatedAt = DateTime.UtcNow, CreatedBy = "seed" };
        var product2 = new Product { Id = Guid.NewGuid(), Name = "Chocolate", IsActive = true, CategoryId = category2.Id, CreatedAt = DateTime.UtcNow, CreatedBy = "seed" };

        await db.Warehouses.AddRangeAsync(warehouse1, warehouse2);
        await db.Suppliers.AddRangeAsync(supplier1, supplier2);
        await db.ProductCategories.AddRangeAsync(category1, category2);
        await db.Products.AddRangeAsync(product1, product2);

        // Purchase Order + StockBatch
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            SupplierId = supplier1.Id,
            PurchaseDate = DateTime.UtcNow.AddDays(-10),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var poItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = po.Id,
            ProductId = product1.Id,
            WarehouseId = warehouse1.Id,
            UnitCost = 500,
            Quantity = 20,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var batch = new StockBatch
        {
            Id = Guid.NewGuid(),
            ProductId = product1.Id,
            SupplierId = supplier1.Id,
            WarehouseId = warehouse1.Id,
            PurchaseOrderItemId = poItem.Id,
            QuantityReceived = 20,
            QuantityRemaining = 20,
            PurchaseDate = po.PurchaseDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        await db.PurchaseOrders.AddAsync(po);
        await db.PurchaseOrderItems.AddAsync(poItem);
        await db.StockBatches.AddAsync(batch);

        await db.SaveChangesAsync();
    }
}
