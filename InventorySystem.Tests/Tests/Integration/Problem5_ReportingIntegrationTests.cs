using InventorySystem.Application.DTOs.Reporting;
using InventorySystem.Infrastructure.Persistence;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Tests.Integration.Fixtures;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Tests.Tests.Integration
{
    /// <summary>
    /// اختبارات تكامل شاملة للمشكلة الخامسة: التقارير مع الفلاتر المختلفة
    /// </summary>
    public class Problem5_ReportingIntegrationTests : IClassFixture<IntegrationTestFactory>
    {
        private readonly IntegrationTestFactory _factory;
        private readonly AppDbContext _dbContext;
        private readonly ReportingService _reportingService;

        public Problem5_ReportingIntegrationTests(IntegrationTestFactory factory)
        {
            _factory = factory;
            _dbContext = factory.CreateDbContext();
            _reportingService = factory.CreateReportingService();
        }

        [Fact]
        // هذا الاختبار يتحقق من أن التقرير يُرجع نتائج صحيحة عند الفلترة حسب المورد
        public async Task Report_FilterBySupplier_ShouldReturnCorrectResults()
        {
            var supplierA = await _factory.CreateSupplierAsync("Supplier A");
            var supplierB = await _factory.CreateSupplierAsync("Supplier B");
            var product = await _factory.CreateProductAsync("Laptop");
            var warehouse = await _factory.CreateWarehouseAsync("Main WH");

            // إنشاء بيانات: دفعة من المورد A ودفعة من المورد B
            var (_, poItemA) = await _factory.CreateAndReceivePurchaseOrderAsync(supplierA.Id, product.Id, warehouse.Id, 20, 100);
            var (_, poItemB) = await _factory.CreateAndReceivePurchaseOrderAsync(supplierB.Id, product.Id, warehouse.Id, 10, 90);

            // بيع 20 وحدة → تُخصم كلها من المورد A
            await _factory.CreateSaleAsync(product.Id, warehouse.Id, 20);
            // بيع 10 وحدات → تُخصم كلها من المورد B
            await _factory.CreateSaleAsync(product.Id, warehouse.Id, 10);

            var filterA = new SalesReportFilter { SupplierId = supplierA.Id };
            var reportA = await _reportingService.GetSalesReportAsync(filterA);
            Assert.Equal(20, reportA.Sum(r => r.QuantitySold)); // المورد A باع 20

            var filterB = new SalesReportFilter { SupplierId = supplierB.Id };
            var reportB = await _reportingService.GetSalesReportAsync(filterB);
            Assert.Equal(10, reportB.Sum(r => r.QuantitySold)); // المورد B باع 10

        }

        [Fact]
        // هذا الاختبار يتحقق من أن التقرير يُرجع نتائج صحيحة عند الفلترة حسب الفترة الزمنية
        public async Task Report_FilterByDateRange_ShouldReturnCorrectResults()
        {
            var supplier = await _factory.CreateSupplierAsync("Supplier DateRange");
            var product = await _factory.CreateProductAsync("Keyboard");
            var warehouse = await _factory.CreateWarehouseAsync("WH DateRange");

            var (_, poItem) = await _factory.CreateAndReceivePurchaseOrderAsync(supplier.Id, product.Id, warehouse.Id, 30, 50);

            // بيع في تاريخين مختلفين
            await _factory.CreateSaleAsync(product.Id, warehouse.Id, 10, DateTime.UtcNow.AddDays(-10));
            await _factory.CreateSaleAsync(product.Id, warehouse.Id, 5, DateTime.UtcNow.AddDays(-2));

            var filter = new SalesReportFilter
            {
                FromDate = DateTime.UtcNow.AddDays(-7),
                ToDate = DateTime.UtcNow
            };

            var report = await _reportingService.GetSalesReportAsync(filter);

            // يجب أن يحتوي التقرير فقط على المبيعات ضمن الفترة الزمنية (آخر 7 أيام)
            Assert.All(report, r => Assert.InRange(r.FirstSaleDate, filter.FromDate.Value, filter.ToDate.Value));
        }

        [Fact]
        // هذا الاختبار يتحقق من أن التقرير يُرجع نتائج صحيحة عند الفلترة حسب التصنيف
        public async Task Report_FilterByCategory_ShouldReturnCorrectResults()
        {
            var supplier = await _factory.CreateSupplierAsync("Supplier Category");
            var categoryElectronics = await _factory.CreateCategoryAsync("Electronics");
            var categoryFurniture = await _factory.CreateCategoryAsync("Furniture");

            var productLaptop = await _factory.CreateProductAsync("Laptop", categoryElectronics.Id);
            var productChair = await _factory.CreateProductAsync("Chair", categoryFurniture.Id);

            var warehouse = await _factory.CreateWarehouseAsync("WH Category");

            var (_, poItemLaptop) = await _factory.CreateAndReceivePurchaseOrderAsync(supplier.Id, productLaptop.Id, warehouse.Id, 10, 100);
            var (_, poItemChair) = await _factory.CreateAndReceivePurchaseOrderAsync(supplier.Id, productChair.Id, warehouse.Id, 5, 50);

            await _factory.CreateSaleAsync(productLaptop.Id, warehouse.Id, 5);
            await _factory.CreateSaleAsync(productChair.Id, warehouse.Id, 2);

            var filter = new SalesReportFilter { ProductCategoryId = categoryElectronics.Id };
            var report = await _reportingService.GetSalesReportAsync(filter);

            // يجب أن يحتوي التقرير فقط على منتجات من فئة Electronics
            Assert.All(report, r => Assert.Equal(categoryElectronics.Id, productLaptop.CategoryId));
            Assert.Equal(5, report.Sum(r => r.QuantitySold));
        }

        [Fact]
        // اختبار الأداء: التقرير يجب أن يعمل بكفاءة مع بيانات كبيرة
        public async Task Report_WithLargeData_ShouldPerformEfficiently()
        {
            var supplier = await _factory.CreateSupplierAsync("Bulk Supplier");
            var product = await _factory.CreateProductAsync("Bulk Product");
            var warehouse = await _factory.CreateWarehouseAsync("Bulk WH");

            // إنشاء بيانات كبيرة: 50 دفعة + 100 عملية بيع
            for (int i = 0; i < 50; i++)
            {
                var (_, poItem) = await _factory.CreateAndReceivePurchaseOrderAsync(supplier.Id, product.Id, warehouse.Id, 20, 10 + i);
                await _factory.CreateSaleAsync(product.Id, warehouse.Id, 5);
            }

            var filter = new SalesReportFilter { SupplierId = supplier.Id };
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var report = await _reportingService.GetSalesReportAsync(filter);

            stopwatch.Stop();

            Assert.NotEmpty(report);
            Assert.True(stopwatch.ElapsedMilliseconds < 500, $"Report took {stopwatch.ElapsedMilliseconds}ms, expected < 500ms");
        }
    }
}
