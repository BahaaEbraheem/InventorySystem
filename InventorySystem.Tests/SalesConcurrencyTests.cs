using InventorySystem.Application.DTOs.Sales;
using InventorySystem.Domain.Entities;
using InventorySystem.Infrastructure.Persistence;
using InventorySystem.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Tests
{
    public class SalesConcurrencyTests
    {
        [Fact]
        public async Task TwoConcurrentSales_OnSameProduct_ShouldNotOversell()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "SalesConcurrencyTest")
                .Options;

            using var dbContext = new AppDbContext(options);

            // Seed: Batch بكمية 10
            var productId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();
            var supplierId = Guid.NewGuid();

            dbContext.Products.Add(new Product { Id = productId, Name = "Test Product", CreatedAt = DateTime.UtcNow, CreatedBy = "test" });
            dbContext.Warehouses.Add(new Warehouse { Id = warehouseId, Name = "Main", CreatedAt = DateTime.UtcNow, CreatedBy = "test" });
            dbContext.Suppliers.Add(new Supplier { Id = supplierId, Name = "Supp", CreatedAt = DateTime.UtcNow, CreatedBy = "test" });

            var poItem = new PurchaseOrderItem
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                WarehouseId = warehouseId,
                UnitCost = 10,
                Quantity = 10,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test"
            };

            dbContext.PurchaseOrderItems.Add(poItem);

            dbContext.StockBatches.Add(new StockBatch
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                SupplierId = supplierId,
                WarehouseId = warehouseId,
                PurchaseOrderItemId = poItem.Id,
                QuantityReceived = 10,
                QuantityRemaining = 10,
                PurchaseDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test"
            });

            await dbContext.SaveChangesAsync();

            var salesService = new SalesService(dbContext, new FakeNotificationService());

            var saleRequest1 = new CreateSaleRequest
            {
                SaleDate = DateTime.UtcNow,
                Items = new()
            {
                new CreateSaleItemDto { ProductId = productId, WarehouseId = warehouseId, Quantity = 7 }
            }
            };

            var saleRequest2 = new CreateSaleRequest
            {
                SaleDate = DateTime.UtcNow,
                Items = new()
            {
                new CreateSaleItemDto { ProductId = productId, WarehouseId = warehouseId, Quantity = 7 }
            }
            };

            var task1 = Task.Run(() => salesService.CreateSaleAsync(saleRequest1));
            var task2 = Task.Run(() => salesService.CreateSaleAsync(saleRequest2));

            var tasks = new[] { task1, task2 };

            await Task.WhenAll(tasks);

            // واحدة منهم يجب أن تفشل (Exception) أو يتم رفضها
            var successCount = tasks.Count(t => t.Status == TaskStatus.RanToCompletion);
            Assert.Equal(1, successCount);

            var batch = await dbContext.StockBatches.FirstAsync();
            Assert.True(batch.QuantityRemaining >= 0);
        }
    }
}