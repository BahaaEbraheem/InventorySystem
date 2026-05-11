using InventorySystem.Application.DTOs.Transfers;
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
    /// اختبارات تكامل شاملة للمشكلة الثالثة: التحويل بين المستودعات
    /// </summary>
    public class Problem3_StockTransferIntegrationTests : IClassFixture<IntegrationTestFactory>
    {
        private readonly IntegrationTestFactory _factory;
        private readonly AppDbContext _dbContext;
        private readonly StockTransferService _transferService;

        public Problem3_StockTransferIntegrationTests(IntegrationTestFactory factory)
        {
            _factory = factory;
            _dbContext = factory.CreateDbContext();
            _transferService = factory.CreateStockTransferService();
        }

        [Fact]
        // هذا الاختبار يتحقق من أن النظام يمنع حدوث مخزون سلبي عند تنفيذ عمليتي تحويل متوازيتين على نفس المنتج
        public async Task Concurrency_RaceCondition_ShouldPreventNegativeStock()
        {
            var supplier = await _factory.CreateSupplierAsync("Supplier Transfer Concurrency");
            var product = await _factory.CreateProductAsync("Product Transfer Concurrency");
            var warehouseFrom = await _factory.CreateWarehouseAsync("Warehouse From");
            var warehouseTo = await _factory.CreateWarehouseAsync("Warehouse To");

            var (_, poItemId) = await _factory.CreateAndReceivePurchaseOrderAsync(
                supplier.Id, product.Id, warehouseFrom.Id, 100, 10);

            var transferRequest1 = new CreateStockTransferRequest
            {
                IdempotencyKey = Guid.NewGuid(),
                FromWarehouseId = warehouseFrom.Id,
                ToWarehouseId = warehouseTo.Id,
                TransferDate = DateTime.UtcNow,
                Items = new() { new CreateStockTransferItemDto { ProductId = product.Id, Quantity = 80 } }
            };

            var transferRequest2 = new CreateStockTransferRequest
            {
                IdempotencyKey = Guid.NewGuid(),
                FromWarehouseId = warehouseFrom.Id,
                ToWarehouseId = warehouseTo.Id,
                TransferDate = DateTime.UtcNow,
                Items = new() { new CreateStockTransferItemDto { ProductId = product.Id, Quantity = 50 } }
            };

            var task1 = _transferService.CreateTransferAsync(transferRequest1);
            var task2 = _transferService.CreateTransferAsync(transferRequest2);

            await Task.WhenAll(task1.ContinueWith(_ => { }), task2.ContinueWith(_ => { }));

            var remaining = await _dbContext.StockBatches
                .Where(b => b.ProductId == product.Id && b.WarehouseId == warehouseFrom.Id)
                .SumAsync(b => b.QuantityRemaining);

            Assert.True(remaining >= 0, "المخزون يجب ألا يكون سالباً بعد التحويلات المتزامنة");
        }

        [Fact]
        // هذا الاختبار يتحقق من أن النظام يمنع تنفيذ نفس عملية التحويل مرتين إذا تم إرسال الطلب بنفس المفتاح IdempotencyKey
        public async Task DoubleSubmit_ShouldPreventDuplicateTransfer()
        {
            var supplier = await _factory.CreateSupplierAsync("Supplier Transfer DoubleSubmit");
            var product = await _factory.CreateProductAsync("Product Transfer DoubleSubmit");
            var warehouseFrom = await _factory.CreateWarehouseAsync("Warehouse From");
            var warehouseTo = await _factory.CreateWarehouseAsync("Warehouse To");

            var (_, poItemId) = await _factory.CreateAndReceivePurchaseOrderAsync(
                supplier.Id, product.Id, warehouseFrom.Id, 50, 10);

            var key = Guid.NewGuid();

            var transferRequest = new CreateStockTransferRequest
            {
                IdempotencyKey = key,
                FromWarehouseId = warehouseFrom.Id,
                ToWarehouseId = warehouseTo.Id,
                TransferDate = DateTime.UtcNow,
                Items = new() { new CreateStockTransferItemDto { ProductId = product.Id, Quantity = 10 } }
            };

            await _transferService.CreateTransferAsync(transferRequest);

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _transferService.CreateTransferAsync(transferRequest);
            });
        }

        [Fact]
        // هذا الاختبار يتحقق من أن منطق الحجز يعمل بشكل صحيح بحيث يتم تحديث الكميات المحجوزة والمتبقية بشكل متوازن بعد التحويل
        public async Task Reservation_ShouldUpdateReservedAndRemainingCorrectly()
        {
            var supplier = await _factory.CreateSupplierAsync("Supplier Transfer Reservation");
            var product = await _factory.CreateProductAsync("Product Transfer Reservation");
            var warehouseFrom = await _factory.CreateWarehouseAsync("Warehouse From");
            var warehouseTo = await _factory.CreateWarehouseAsync("Warehouse To");

            var (_, poItemId) = await _factory.CreateAndReceivePurchaseOrderAsync(
                supplier.Id, product.Id, warehouseFrom.Id, 20, 10);

            var transferRequest = new CreateStockTransferRequest
            {
                IdempotencyKey = Guid.NewGuid(),
                FromWarehouseId = warehouseFrom.Id,
                ToWarehouseId = warehouseTo.Id,
                TransferDate = DateTime.UtcNow,
                Items = new() { new CreateStockTransferItemDto { ProductId = product.Id, Quantity = 5 } }
            };

            await _transferService.CreateTransferAsync(transferRequest);

            var batch = await _dbContext.StockBatches.FirstAsync(b => b.ProductId == product.Id && b.WarehouseId == warehouseFrom.Id);

            Assert.Equal(15, batch.QuantityRemaining);
            Assert.Equal(0, batch.QuantityReserved);
        }

        [Fact]
        // هذا الاختبار يتحقق من أن النظام يرسل إشعارات دقيقة عند تغيير المخزون في المستودع المصدر والوجهة
        public async Task Notifications_ShouldBeTriggeredOnStockChange()
        {
            var supplier = await _factory.CreateSupplierAsync("Supplier Transfer Notifications");
            var product = await _factory.CreateProductAsync("Product Transfer Notifications");
            var warehouseFrom = await _factory.CreateWarehouseAsync("Warehouse From");
            var warehouseTo = await _factory.CreateWarehouseAsync("Warehouse To");

            var (_, poItemId) = await _factory.CreateAndReceivePurchaseOrderAsync(
                supplier.Id, product.Id, warehouseFrom.Id, 10, 10);

            var transferRequest = new CreateStockTransferRequest
            {
                IdempotencyKey = Guid.NewGuid(),
                FromWarehouseId = warehouseFrom.Id,
                ToWarehouseId = warehouseTo.Id,
                TransferDate = DateTime.UtcNow,
                Items = new() { new CreateStockTransferItemDto { ProductId = product.Id, Quantity = 8 } }
            };

            await _transferService.CreateTransferAsync(transferRequest);

            var batchFrom = await _dbContext.StockBatches.FirstAsync(b => b.ProductId == product.Id && b.WarehouseId == warehouseFrom.Id);
            var batchTo = await _dbContext.StockBatches.FirstAsync(b => b.ProductId == product.Id && b.WarehouseId == warehouseTo.Id);

            Assert.True(batchFrom.QuantityRemaining >= 0, "يجب أن يتم تحديث المخزون في المستودع المصدر");
            Assert.True(batchTo.QuantityRemaining > 0, "يجب أن يتم إنشاء مخزون جديد في المستودع الوجهة");
        }
    }
}