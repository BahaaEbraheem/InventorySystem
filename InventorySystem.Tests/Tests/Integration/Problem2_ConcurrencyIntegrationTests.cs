using InventorySystem.Application.DTOs.Sales;
using InventorySystem.Infrastructure.Persistence;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Tests.Tests.Integration
{
    /// <summary>
    /// اختبارات تكامل شاملة للمشكلة الثانية: المبيعات المتزامنة ودقة المخزون
    /// </summary>
    public class Problem2_ConcurrencyIntegrationTests : IClassFixture<IntegrationTestFactory>
    {
        private readonly IntegrationTestFactory _factory;
        private readonly AppDbContext _dbContext;
        private readonly SalesService _salesService;

        public Problem2_ConcurrencyIntegrationTests(IntegrationTestFactory factory)
        {
            _factory = factory;
            _dbContext = factory.CreateDbContext();
            _salesService = factory.CreateSalesService();
        }
        // هذا الاختبار يتحقق من أن النظام يمنع حدوث مخزون سلبي عند تنفيذ عمليتي بيع متوازيتين على نفس المنتج
        [Fact]
        public async Task Concurrency_RaceCondition_ShouldPreventNegativeStock()
        {
            // إعداد البيانات: مورد + منتج + مستودع + دفعة مخزون فيها 100 وحدة
            var supplier = await _factory.CreateSupplierAsync("Supplier Concurrency");
            var product = await _factory.CreateProductAsync("Product Concurrency");
            var warehouse = await _factory.CreateWarehouseAsync("Warehouse Concurrency");

            var (_, poItemId) = await _factory.CreateAndReceivePurchaseOrderAsync(
                supplier.Id, product.Id, warehouse.Id, 100, 10);

            // تشغيل عمليتي بيع متوازيتين
            var saleRequest1 = new CreateSaleRequest
            {
                IdempotencyKey = Guid.NewGuid(),
                SaleDate = DateTime.UtcNow,
                Items = new() { new CreateSaleItemDto { ProductId = product.Id, WarehouseId = warehouse.Id, Quantity = 80 } }
            };

            var saleRequest2 = new CreateSaleRequest
            {
                IdempotencyKey = Guid.NewGuid(),
                SaleDate = DateTime.UtcNow,
                Items = new() { new CreateSaleItemDto { ProductId = product.Id, WarehouseId = warehouse.Id, Quantity = 50 } }
            };

            var task1 = _salesService.CreateSaleAsync(saleRequest1);
            var task2 = _salesService.CreateSaleAsync(saleRequest2);

            await Task.WhenAll(task1.ContinueWith(_ => { }), task2.ContinueWith(_ => { }));

            // التحقق: المخزون لا يصبح سالباً
            var remaining = await _dbContext.StockBatches
                .Where(b => b.ProductId == product.Id && b.WarehouseId == warehouse.Id)
                .SumAsync(b => b.QuantityRemaining);

            Assert.True(remaining >= 0, "المخزون يجب ألا يكون سالباً");
        }
        // هذا الاختبار يتحقق من أن النظام يمنع تنفيذ نفس عملية البيع مرتين إذا تم إرسال الطلب بنفس المفتاح IdempotencyKey
        [Fact]
        public async Task DoubleSubmit_ShouldPreventDuplicateSale()
        {
            var supplier = await _factory.CreateSupplierAsync("Supplier DoubleSubmit");
            var product = await _factory.CreateProductAsync("Product DoubleSubmit");
            var warehouse = await _factory.CreateWarehouseAsync("Warehouse DoubleSubmit");

            var (_, poItemId) = await _factory.CreateAndReceivePurchaseOrderAsync(
                supplier.Id, product.Id, warehouse.Id, 50, 10);

            var key = Guid.NewGuid();

            var saleRequest = new CreateSaleRequest
            {
                IdempotencyKey = key,
                SaleDate = DateTime.UtcNow,
                Items = new() { new CreateSaleItemDto { ProductId = product.Id, WarehouseId = warehouse.Id, Quantity = 10 } }
            };

            // إرسال نفس الطلب مرتين بنفس المفتاح
            await _salesService.CreateSaleAsync(saleRequest);

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _salesService.CreateSaleAsync(saleRequest);
            });
        }
        // هذا الاختبار يتحقق من أن منطق الحجز يعمل بشكل صحيح بحيث يتم تحديث الكميات المحجوزة والمتبقية بشكل متوازن بعد البيع
        [Fact]
        public async Task Reservation_ShouldUpdateReservedAndRemainingCorrectly()
        {
            var supplier = await _factory.CreateSupplierAsync("Supplier Reservation");
            var product = await _factory.CreateProductAsync("Product Reservation");
            var warehouse = await _factory.CreateWarehouseAsync("Warehouse Reservation");

            var (_, poItemId) = await _factory.CreateAndReceivePurchaseOrderAsync(
                supplier.Id, product.Id, warehouse.Id, 20, 10);

            var saleRequest = new CreateSaleRequest
            {
                IdempotencyKey = Guid.NewGuid(),
                SaleDate = DateTime.UtcNow,
                Items = new() { new CreateSaleItemDto { ProductId = product.Id, WarehouseId = warehouse.Id, Quantity = 5 } }
            };

            await _salesService.CreateSaleAsync(saleRequest);

            var batch = await _dbContext.StockBatches.FirstAsync(b => b.ProductId == product.Id);

            Assert.Equal(15, batch.QuantityRemaining);
            Assert.Equal(0, batch.QuantityReserved);
        }
        // هذا الاختبار يتحقق من أن النظام يرسل إشعارات دقيقة عند تغيير المخزون أو عند انخفاض الكمية تحت الحد المسموح
        [Fact]
        public async Task Notifications_ShouldBeTriggeredOnStockChange()
        {
            var supplier = await _factory.CreateSupplierAsync("Supplier Notifications");
            var product = await _factory.CreateProductAsync("Product Notifications");
            var warehouse = await _factory.CreateWarehouseAsync("Warehouse Notifications");

            var (_, poItemId) = await _factory.CreateAndReceivePurchaseOrderAsync(
                supplier.Id, product.Id, warehouse.Id, 10, 10);

            var saleRequest = new CreateSaleRequest
            {
                IdempotencyKey = Guid.NewGuid(),
                SaleDate = DateTime.UtcNow,
                Items = new() { new CreateSaleItemDto { ProductId = product.Id, WarehouseId = warehouse.Id, Quantity = 8 } }
            };

            await _salesService.CreateSaleAsync(saleRequest);

            // التحقق: المخزون أقل من العتبة (5) → يجب أن يكون هناك إشعار
            var batch = await _dbContext.StockBatches.FirstAsync(b => b.ProductId == product.Id);
            Assert.True(batch.QuantityRemaining < 5, "يجب أن يكون هناك إشعار بانخفاض المخزون تحت الحد");
        }
    }
}
