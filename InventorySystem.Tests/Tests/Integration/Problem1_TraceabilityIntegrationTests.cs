// File: tests/InventorySystem.Tests.Integration/Problem1_TraceabilityIntegrationTests.cs
using InventorySystem.Application.DTOs.Purchase;
using InventorySystem.Application.DTOs.Reporting;
using InventorySystem.Application.DTOs.Sales;
using InventorySystem.Domain.Entities;
using InventorySystem.Infrastructure.Persistence;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Shared.Enums;
using InventorySystem.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InventorySystem.Tests.Integration;

/// <summary>
/// اختبارات تكامل شاملة للمشكلة الأولى: تتبع مصدر المبيعات
/// </summary>
public class Problem1_TraceabilityIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    private readonly AppDbContext _dbContext;
    private readonly PurchaseService _purchaseService;
    private readonly SalesService _salesService;
    private readonly ReportingService _reportingService;

    public Problem1_TraceabilityIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _dbContext = factory.CreateDbContext();
        _purchaseService = factory.CreatePurchaseService();
        _salesService = factory.CreateSalesService();
        _reportingService = factory.CreateReportingService();
    }

    #region  السيناريو الكامل: شراء من موردين متعددين → بيع → تقارير

    [Fact]
    public async Task Problem1_FullScenario_TrackSalesBySupplier_AndRemainingStockFromShipment()
    {
        // ========== 📦 المرحلة 1: إعداد البيانات ==========
        var supplierA = await _factory.CreateSupplierAsync("Supplier A - March Shipment");
        var supplierB = await _factory.CreateSupplierAsync("Supplier B - April Shipment");
        var product = await _factory.CreateProductAsync("Wireless Mouse");
        var warehouse = await _factory.CreateWarehouseAsync("Main Warehouse");

        // ========== 🛒 المرحلة 2: إنشاء واستلام طلبات شراء من موردين مختلفين ==========

        // 📦 الشحنة الأولى: من المورد أ في مارس (100 وحدة)
        var poRequestA = new CreatePurchaseOrderRequest
        {
            SupplierId = supplierA.Id,
            PurchaseDate = new DateTime(2024, 03, 15), // 📅 مارس
            Items = new() { new CreatePurchaseOrderItemDto
            {
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                Quantity = 100,
                UnitCost = 25.50m
            }}
        };
        var poResponseA = await _purchaseService.CreatePurchaseOrderAsync(poRequestA);
        await _purchaseService.SubmitPurchaseOrderAsync(poResponseA.PurchaseOrderId);

        var poItemA = await _dbContext.PurchaseOrderItems.FirstAsync(i => i.PurchaseOrderId == poResponseA.PurchaseOrderId);
        await _purchaseService.ReceivePurchaseOrderAsync(poResponseA.PurchaseOrderId, new()
        {
            new() { PurchaseOrderItemId = poItemA.Id, ReceivedQuantity = 100 } // استلام كامل
        });

        // 📦 الشحنة الثانية: من المورد ب في أبريل (50 وحدة)
        var poRequestB = new CreatePurchaseOrderRequest
        {
            SupplierId = supplierB.Id,
            PurchaseDate = new DateTime(2024, 04, 10), // 📅 أبريل
            Items = new() { new CreatePurchaseOrderItemDto
            {
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                Quantity = 50,
                UnitCost = 23.00m // سعر مختلف
            }}
        };
        var poResponseB = await _purchaseService.CreatePurchaseOrderAsync(poRequestB);
        await _purchaseService.SubmitPurchaseOrderAsync(poResponseB.PurchaseOrderId);

        var poItemB = await _dbContext.PurchaseOrderItems.FirstAsync(i => i.PurchaseOrderId == poResponseB.PurchaseOrderId);
        await _purchaseService.ReceivePurchaseOrderAsync(poResponseB.PurchaseOrderId, new()
        {
            new() { PurchaseOrderItemId = poItemB.Id, ReceivedQuantity = 50 }
        });

        // ========== ✅ التحقق: تم إنشاء دفعات مخزون مرتبطة بالموردين ==========
        var batchFromA = await _dbContext.StockBatches.FirstAsync(b => b.SupplierId == supplierA.Id);
        var batchFromB = await _dbContext.StockBatches.FirstAsync(b => b.SupplierId == supplierB.Id);

        Assert.Equal(100, batchFromA.QuantityRemaining);
        Assert.Equal(50, batchFromB.QuantityRemaining);
        Assert.Equal(new DateTime(2024, 03, 15), batchFromA.PurchaseDate.Date);
        Assert.Equal(new DateTime(2024, 04, 10), batchFromB.PurchaseDate.Date);

        // ========== 🛍️ المرحلة 3: إجراء مبيعات (تطبق مبدأ FIFO) ==========

        // 🛒 بيع 60 وحدة → يجب أن تُخصم من الشحنة الأقدم (المورد أ - مارس)
        var saleRequest1 = new CreateSaleRequest
        {
            SaleDate = new DateTime(2024, 04, 20),
            Items = new() { new CreateSaleItemDto
            {
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                Quantity = 60
            }}
        };
        await _salesService.CreateSaleAsync(saleRequest1);

        // 🛒 بيع 30 وحدة → 40 متبقية من المورد أ + 50 من المورد ب = 90
        // وفق FIFO: تُخصم 40 من المورد أ (تُنهيه) + 10 من المورد ب
        var saleRequest2 = new CreateSaleRequest
        {
            SaleDate = new DateTime(2024, 04, 25),
            Items = new() { new CreateSaleItemDto
            {
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                Quantity = 30
            }}
        };
        await _salesService.CreateSaleAsync(saleRequest2);

        // 🛒 بيع 15 وحدة → 40 متبقية من المورد ب
        var saleRequest3 = new CreateSaleRequest
        {
            SaleDate = new DateTime(2024, 05, 01),
            Items = new() { new CreateSaleItemDto
            {
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                Quantity = 15
            }}
        };
        await _salesService.CreateSaleAsync(saleRequest3);

        // ========== 📊 المرحلة 4: التحقق من التتبع عبر التقارير ==========

        // ✅ السؤال 1: "كم بعنا من المنتج الذي جاء من المورد أ؟"
        var reportFilterBySupplierA = new SalesReportFilter
        {
            ProductCategoryId = product.CategoryId,
            SupplierId = supplierA.Id
        };
        var reportBySupplierA = await _reportingService.GetSalesReportAsync(reportFilterBySupplierA);

        var itemFromA = reportBySupplierA.Single();
        Assert.Equal(supplierA.Id, itemFromA.SupplierId);
        Assert.Equal(100m, itemFromA.QuantitySold); // ✅ 60 + 40 = 100 (كل ما جاء من المورد أ تم بيعه)

        // ✅ السؤال 2: "كم بعنا من المنتج الذي جاء من المورد ب؟"
        var reportFilterBySupplierB = new SalesReportFilter { SupplierId = supplierB.Id };
        var reportBySupplierB = await _reportingService.GetSalesReportAsync(reportFilterBySupplierB);

        var itemFromB = reportBySupplierB.Single();
        Assert.Equal(supplierB.Id, itemFromB.SupplierId);
        Assert.Equal(5m, itemFromB.QuantitySold);
        // ✅ السؤال 3: "كم تبقى من شحنة مارس (المورد أ)؟"
        var remainingFromMarchShipment = await _reportingService.GetRemainingStockFromShipmentAsync(poItemA.Id);
        Assert.Equal(0m, remainingFromMarchShipment.TotalRemaining); // ✅ نُفدت بالكامل

        // ✅ السؤال 4: "كم تبقى من شحنة أبريل (المورد ب)؟"
        var remainingFromAprilShipment = await _reportingService.GetRemainingStockFromShipmentAsync(poItemB.Id);

        // ✅ التصحيح: 45 وحدة متبقية (وليس 25)
        // 50 (أصلي) - 5 (مباعة) = 45 ✅
        Assert.Equal(45m, remainingFromAprilShipment.TotalRemaining);

        // ✅ التحقق النهائي: إجمالي الرصيد في المستودع = 45 وحدة (كلها من المورد ب)
        var totalRemainingInWarehouse = await _dbContext.StockBatches
            .Where(b => b.ProductId == product.Id && b.WarehouseId == warehouse.Id)
            .SumAsync(b => b.QuantityRemaining);

        // ✅ التصحيح: 45 (وليس 25)
        Assert.Equal(45m, totalRemainingInWarehouse);

        // ✅ التحقق من روابط التخصيص (SaleItemBatchAllocation)
        var allocations = await _dbContext.SaleItemBatchAllocations
            .Include(a => a.StockBatch)
            .Where(a => a.StockBatch.ProductId == product.Id)
            .ToListAsync();

        // يجب أن يكون هناك تخصيصات من كلتا الدفعتين
        var allocationsFromA = allocations.Count(a => a.StockBatch.SupplierId == supplierA.Id);
        var allocationsFromB = allocations.Count(a => a.StockBatch.SupplierId == supplierB.Id);

        Assert.True(allocationsFromA > 0, "يجب وجود تخصيصات من المورد أ");
        Assert.True(allocationsFromB > 0, "يجب وجود تخصيصات من المورد ب");

        // ✅ التحقق من أن الكميات المخصصة تطابق المبيعات
        // ✅ التحقق من أن الكميات المخصصة تطابق المبيعات
        var totalAllocatedFromA = allocations.Where(a => a.StockBatch.SupplierId == supplierA.Id).Sum(a => a.Quantity);
        var totalAllocatedFromB = allocations.Where(a => a.StockBatch.SupplierId == supplierB.Id).Sum(a => a.Quantity);

        Assert.Equal(100m, totalAllocatedFromA); // ✅ كل ما جاء من أ تم بيعه
        Assert.Equal(5m, totalAllocatedFromB);   // ✅ التصحيح: 5 وحدات من ب (وليس 25)
    }

    #endregion

    #region  اختبار جزئي: استلام على دفعات + تتبع كل دفعة

    [Fact]
    public async Task Problem1_PartialReceipt_TracksEachBatchSeparately()
    {
        // Arrange
        var supplier = await _factory.CreateSupplierAsync("Partial Receipt Supplier");
        var product = await _factory.CreateProductAsync("Partial Receipt Product");
        var warehouse = await _factory.CreateWarehouseAsync("Partial Receipt WH");

        // إنشاء طلب شراء بـ 100 وحدة
        var poRequest = new CreatePurchaseOrderRequest
        {
            SupplierId = supplier.Id,
            PurchaseDate = DateTime.UtcNow.Date,
            Items = new() { new CreatePurchaseOrderItemDto
        {
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Quantity = 100,
            UnitCost = 30
        }}
        };
        var poResponse = await _purchaseService.CreatePurchaseOrderAsync(poRequest);
        await _purchaseService.SubmitPurchaseOrderAsync(poResponse.PurchaseOrderId);
        var poItem = await _dbContext.PurchaseOrderItems.FirstAsync(i => i.PurchaseOrderId == poResponse.PurchaseOrderId);

        // 📦 استلام جزئي أول: 40 وحدة
        await _purchaseService.ReceivePurchaseOrderAsync(poResponse.PurchaseOrderId, new()
    {
        new() { PurchaseOrderItemId = poItem.Id, ReceivedQuantity = 40 }
    });

        // 📦 استلام جزئي ثاني: 35 وحدة
        await _purchaseService.ReceivePurchaseOrderAsync(poResponse.PurchaseOrderId, new()
    {
        new() { PurchaseOrderItemId = poItem.Id, ReceivedQuantity = 35 }
    });

        // 📦 استلام جزئي ثالث: 25 وحدة
        await _purchaseService.ReceivePurchaseOrderAsync(poResponse.PurchaseOrderId, new()
    {
        new() { PurchaseOrderItemId = poItem.Id, ReceivedQuantity = 25 }
    });

        //  اجلب الدفعات بترتيب FIFO باستخدام ReceivedDate
        var batches = await _dbContext.StockBatches
            .Where(b => b.PurchaseOrderItemId == poItem.Id)
            .OrderBy(b => b.ReceivedDate)
            .ToListAsync();

        var batch1 = batches[0];
        var batch2 = batches[1];
        var batch3 = batches[2];

        Assert.Equal(3, batches.Count);
        Assert.All(batches, b =>
        {
            Assert.Equal(supplier.Id, b.SupplierId);
            Assert.Equal(product.Id, b.ProductId);
            Assert.Equal(poItem.Id, b.PurchaseOrderItemId);
        });

        // بيع 50 وحدة → وفق FIFO: تُخصم 40 من batch1 + 10 من batch2
        var saleRequest = new CreateSaleRequest
        {
            SaleDate = DateTime.UtcNow,
            Items = new() { new CreateSaleItemDto
        {
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Quantity = 50
        }}
        };
        await _salesService.CreateSaleAsync(saleRequest);

        //  التحقق من التخصيصات
        var allocations = await _dbContext.SaleItemBatchAllocations
            .Include(a => a.StockBatch)
            .Where(a => a.StockBatch.PurchaseOrderItemId == poItem.Id)
            .ToListAsync();

        var allocatedFromBatch1 = allocations.FirstOrDefault(a => a.StockBatchId == batch1.Id)?.Quantity ?? 0;
        var allocatedFromBatch2 = allocations.FirstOrDefault(a => a.StockBatchId == batch2.Id)?.Quantity ?? 0;

        Assert.Equal(40m, allocatedFromBatch1); // ✅ كل الـ 40 من الدفعة الأولى
        Assert.Equal(10m, allocatedFromBatch2); // ✅ 10 من الدفعة الثانية
        var batch1Remaining = await _dbContext.StockBatches
            .AsNoTracking() 
            .Where(b => b.Id == batch1.Id)
            .Select(b => b.QuantityRemaining)
            .FirstAsync();
        Assert.Equal(0m, batch1Remaining);
        var batch2Remaining = await _dbContext.StockBatches
    .AsNoTracking()
    .Where(b => b.Id == batch2.Id)
    .Select(b => b.QuantityRemaining)
    .FirstAsync();

        Assert.Equal(25m, batch2Remaining);
        var batch3Remaining = await _dbContext.StockBatches
            .AsNoTracking()
            .Where(b => b.Id == batch3.Id)
            .Select(b => b.QuantityRemaining)
            .FirstAsync();

        Assert.Equal(25m, batch3Remaining);

    }

    #endregion

    #region  اختبار الأداء: تقرير بمؤشرات فعالة

    [Fact]
    public async Task Problem1_Report_WithLargeData_PerformsEfficiently()
    {
        // Arrange: إنشاء بيانات كبيرة لمحاكاة نمو القاعدة
        var supplier = await _factory.CreateSupplierAsync("Bulk Supplier");
        var product = await _factory.CreateProductAsync("Bulk Product");
        var warehouse = await _factory.CreateWarehouseAsync("Bulk WH");

        // إنشاء 10 دفعات مخزون (محاكاة لطلبات شراء متعددة)
        for (int i = 0; i < 10; i++)
        {
            var poRequest = new CreatePurchaseOrderRequest
            {
                SupplierId = supplier.Id,
                //  التصحيح: استخدام .Date لتجنب خطأ "في المستقبل"
                PurchaseDate = DateTime.UtcNow.AddDays(-i * 10).Date,
                Items = new() { new CreatePurchaseOrderItemDto
            {
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                Quantity = 50,
                UnitCost = 20 + i
            }}
            };
            var poResponse = await _purchaseService.CreatePurchaseOrderAsync(poRequest);
            await _purchaseService.SubmitPurchaseOrderAsync(poResponse.PurchaseOrderId);
            var poItem = await _dbContext.PurchaseOrderItems.FirstAsync(p => p.PurchaseOrderId == poResponse.PurchaseOrderId);
            await _purchaseService.ReceivePurchaseOrderAsync(poResponse.PurchaseOrderId, new()
        {
            new() { PurchaseOrderItemId = poItem.Id, ReceivedQuantity = 50 }
        });
        }

        // إجراء 20 عملية بيع صغيرة
        for (int i = 0; i < 20; i++)
        {
            var saleRequest = new CreateSaleRequest
            {
                SaleDate = DateTime.UtcNow,
                Items = new() { new CreateSaleItemDto
            {
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                Quantity = 5
            }}
            };
            await _salesService.CreateSaleAsync(saleRequest);
        }

        // Act: تشغيل التقرير مع فلتر المورد
        var filter = new SalesReportFilter { SupplierId = supplier.Id };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var report = await _reportingService.GetSalesReportAsync(filter);

        stopwatch.Stop();

        // Assert
        Assert.NotEmpty(report);
        Assert.Equal(supplier.Id, report.First().SupplierId);

        Assert.True(stopwatch.ElapsedMilliseconds < 500,
            $"Report took {stopwatch.ElapsedMilliseconds}ms, expected < 500ms");
    }

    #endregion
}