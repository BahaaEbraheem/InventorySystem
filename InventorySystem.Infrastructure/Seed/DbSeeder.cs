// File: src/InventorySystem.Infrastructure/Seed/DbSeeder.cs
using InventorySystem.Domain.Entities;
using InventorySystem.Infrastructure.Persistence;
using InventorySystem.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Infrastructure.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        // Skip if data already exists
        if (await db.Products.AnyAsync())
            return;

        // 1. Create Categories first (required by Product)
        var electronics = new ProductCategory
        {
            Id = Guid.NewGuid(),
            Name = "Electronics",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
            IsActive = true
        };

        var food = new ProductCategory
        {
            Id = Guid.NewGuid(),
            Name = "Food",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
            IsActive = true
        };

        await db.ProductCategories.AddRangeAsync(electronics, food);
        await db.SaveChangesAsync();

        // 2. Create Warehouses & Suppliers
        var mainWarehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Name = "Main Warehouse",
            IsActive = true,
            Location = "Riyadh",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var branchWarehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Name = "Branch Warehouse",
            IsActive = true,
            Location = "Jeddah",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var supplierA = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = "Supplier A",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var supplierB = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = "Supplier B",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        await db.Warehouses.AddRangeAsync(mainWarehouse, branchWarehouse);
        await db.Suppliers.AddRangeAsync(supplierA, supplierB);
        await db.SaveChangesAsync();

        // 3. Create Products (now that Categories exist)
        var laptop = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Laptop Dell",
            Sku = "LAP-001",
            IsActive = true,
            CategoryId = electronics.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var chocolate = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Chocolate Bar",
            Sku = "CHOC-001",
            IsActive = true,
            CategoryId = food.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        await db.Products.AddRangeAsync(laptop, chocolate);
        await db.SaveChangesAsync();

        // 4. Create Purchase Order + Item + StockBatch (in correct order)
        var purchaseDate = DateTime.UtcNow.AddDays(-10);

        var purchaseOrder = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierA.Id,
            PurchaseDate = purchaseDate,
            Status = PurchaseOrderStatus.Received,  // جاهز للاستخدام
            ReceivedDate = DateTime.UtcNow.AddDays(-9),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var poItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = purchaseOrder.Id,
            ProductId = laptop.Id,
            WarehouseId = mainWarehouse.Id,
            UnitCost = 800,
            Quantity = 50,
            ReceivedQuantity = 50,  // تم الاستلام بالكامل
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        // StockBatch: يمثل الشحنة الفعلية المستلمة
        var stockBatch = new StockBatch
        {
            Id = Guid.NewGuid(),
            ProductId = laptop.Id,
            SupplierId = supplierA.Id,  // ✅ تتبع المورد
            WarehouseId = mainWarehouse.Id,
            PurchaseOrderItemId = poItem.Id,  // ✅ الربط مع أمر الشراء
            OrderedQuantity = 50,
            QuantityReceived = 50,
            QuantityRemaining = 50,  // ✅ متاح للبيع
            QuantityReserved = 0,
            PurchaseDate = purchaseDate,
            ReceivedDate = DateTime.UtcNow.AddDays(-9),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        await db.PurchaseOrders.AddAsync(purchaseOrder);
        await db.PurchaseOrderItems.AddAsync(poItem);
        await db.StockBatches.AddAsync(stockBatch);
        await db.SaveChangesAsync();

        // 5. Optional: Create a sample Sale to demonstrate tracking
        // (يمكن تفعيلها عند الحاجة للاختبار)
        /*
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            WarehouseId = mainWarehouse.Id,
            SaleDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var saleItem = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            ProductId = laptop.Id,
            Quantity = 10,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var allocation = new SaleItemBatchAllocation
        {
            Id = Guid.NewGuid(),
            SaleItemId = saleItem.Id,
            StockBatchId = stockBatch.Id,  // ✅ ربط البيع بالدفعة
            Quantity = 10,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        // تحديث رصيد الدفعة
        stockBatch.QuantityRemaining = 40;

        await db.Sales.AddAsync(sale);
        await db.SaleItems.AddAsync(saleItem);
        await db.SaleItemBatchAllocations.AddAsync(allocation);
        await db.SaveChangesAsync();
        */
    }
}