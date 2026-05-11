// File: tests/InventorySystem.Tests.Integration/Fixtures/IntegrationTestFactory.cs
using InventorySystem.Application.Interfaces;
using InventorySystem.Domain.Entities;
using InventorySystem.Infrastructure.Persistence;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Tests.Unit.TestHelpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.TestHost;

namespace InventorySystem.Tests.Integration.Fixtures;

public class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // في IntegrationTestFactory.cs
    private readonly string _connectionString =
        Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
        ?? "Server=DESKTOP-Q5U5ADT\\MSSQLSERVER01;Database=InventoryTestDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(_connectionString, sql =>
                {
                    sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
                });
                options.LogTo(Console.WriteLine, LogLevel.Information); // للتصحيح
            });

            services.AddScoped<INotificationService, FakeNotificationService>();
        });

        builder.UseEnvironment(Environments.Development);
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        Console.WriteLine("🗄️ Test database retained. Check: InventoryIntegrationTestDb");

        await base.DisposeAsync();
    }

    public AppDbContext CreateDbContext() =>
        Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();


    public PurchaseService CreatePurchaseService()
    {
        var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PurchaseService>>();
        return new PurchaseService(logger, dbContext, notificationService);
    }

    public async Task<Supplier> CreateSupplierAsync(string name)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "integration_test"
        };

        dbContext.Suppliers.Add(supplier);
        await dbContext.SaveChangesAsync();
        return supplier;
    }

    public async Task<Product> CreateProductAsync(string name)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var categoryId = await GetOrCreateCategoryAsync(dbContext, "Test Category");

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsActive = true,
            CategoryId = categoryId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "integration_test"
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        return product;
    }

    public async Task<Warehouse> CreateWarehouseAsync(string name)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "integration_test"
        };

        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();
        return warehouse;
    }

    private async Task<Guid> GetOrCreateCategoryAsync(AppDbContext db, string name)
    {
        var category = await db.ProductCategories.FirstOrDefaultAsync(c => c.Name == name);
        if (category == null)
        {
            category = new ProductCategory
            {
                Id = Guid.NewGuid(),
                Name = name,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "integration_test"
            };
            db.ProductCategories.Add(category);
            await db.SaveChangesAsync();
        }
        return category.Id;
    }


    // ✅ جديد: إنشاء خدمة التقارير للاختبارات
    public ReportingService CreateReportingService()
    {
        var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return new ReportingService(dbContext);
    }

    // ✅ جديد: دالة مساعدة شاملة لإنشاء + إرسال + استلام طلب شراء
    public async Task<(Guid PurchaseOrderId, Guid PurchaseOrderItemId)> CreateAndReceivePurchaseOrderAsync(
        Guid supplierId,
        Guid productId,
        Guid warehouseId,
        decimal quantity,
        decimal unitCost,
        DateTime? purchaseDate = null)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var logger = scope.ServiceProvider.GetService<ILogger<PurchaseService>>()
            ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PurchaseService>.Instance;

        var purchaseService = new PurchaseService(logger, dbContext, notificationService);

        var poRequest = new Application.DTOs.Purchase.CreatePurchaseOrderRequest
        {
            SupplierId = supplierId,
            PurchaseDate = purchaseDate ?? DateTime.UtcNow.Date,
            Items = new() { new() { ProductId = productId, WarehouseId = warehouseId, Quantity = quantity, UnitCost = unitCost } }
        };

        var poResponse = await purchaseService.CreatePurchaseOrderAsync(poRequest);
        await purchaseService.SubmitPurchaseOrderAsync(poResponse.PurchaseOrderId);

        var poItem = await dbContext.PurchaseOrderItems.FirstAsync(i => i.PurchaseOrderId == poResponse.PurchaseOrderId);
        await purchaseService.ReceivePurchaseOrderAsync(poResponse.PurchaseOrderId, new()
        {
            new() { PurchaseOrderItemId = poItem.Id, ReceivedQuantity = quantity }
        });

        return (poResponse.PurchaseOrderId, poItem.Id);
    }

  

    // ✅ جديد: إنشاء خدمة المبيعات للاختبارات
    public SalesService CreateSalesService()
    {
        var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        // استخدام NullLogger إذا لم يكن مسجلاً
        var logger = scope.ServiceProvider.GetService<ILogger<SalesService>>()
            ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SalesService>.Instance;

        return new SalesService(dbContext, notificationService);
    }

}

