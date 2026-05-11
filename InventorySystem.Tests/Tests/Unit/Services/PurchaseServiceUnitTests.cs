using InventorySystem.Application.DTOs.Purchase;
using InventorySystem.Application.Interfaces;
using InventorySystem.Domain.Entities;
using InventorySystem.Infrastructure.Persistence;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Shared.Enums;
using InventorySystem.Shared.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InventorySystem.Tests.Tests.Unit.Services;

/// <summary>
/// Unit tests for PurchaseService business logic.
/// Uses InMemory database to avoid SQL Server dependency.
/// Tests state transitions, validation rules, and cancellation logic.
/// </summary>
public class PurchaseServiceUnitTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly Mock<ILogger<PurchaseService>> _loggerMock;
    private readonly PurchaseService _service;

    public PurchaseServiceUnitTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _notificationMock = new Mock<INotificationService>();
        _loggerMock = new Mock<ILogger<PurchaseService>>();
        _service = new PurchaseService(_loggerMock.Object, _dbContext, _notificationMock.Object);
    }

    // ─── SubmitPurchaseOrderAsync ─────────────────────────────────

    [Fact]
    public async Task SubmitOrder_WhenDraft_ShouldTransitionToSubmitted()
    {
        // Arrange
        var order = await CreateDraftOrderAsync();

        // Act
        var result = await _service.SubmitPurchaseOrderAsync(order.Id, default);

        // Assert
        Assert.True(result.Success);
        var updated = await _dbContext.PurchaseOrders.FindAsync(order.Id);
        Assert.Equal(PurchaseOrderStatus.Submitted, updated!.Status);
    }

    [Theory]
    [InlineData(PurchaseOrderStatus.Submitted)]
    [InlineData(PurchaseOrderStatus.PartiallyReceived)]
    [InlineData(PurchaseOrderStatus.Received)]
    [InlineData(PurchaseOrderStatus.Cancelled)]
    public async Task SubmitOrder_WhenNotDraft_ShouldReturnBusinessRuleViolation(
        PurchaseOrderStatus status)
    {
        // Arrange
        var order = await CreateDraftOrderAsync();
        order.Status = status;
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.SubmitPurchaseOrderAsync(order.Id, default);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ResponseCodes.BusinessRuleViolation, result.Code);
    }

    [Fact]
    public async Task SubmitOrder_WhenNotFound_ShouldReturnNotFound()
    {
        var result = await _service.SubmitPurchaseOrderAsync(Guid.NewGuid(), default);

        Assert.False(result.Success);
        Assert.Equal(ResponseCodes.NotFound, result.Code);
    }

    [Fact]
    public async Task SubmitOrder_WhenSuccess_ShouldFireSubmittedNotification()
    {
        var order = await CreateDraftOrderAsync();

        await _service.SubmitPurchaseOrderAsync(order.Id, default);

        _notificationMock.Verify(
            n => n.NotifyPurchaseOrderSubmittedAsync(order.Id),
            Times.Once);
    }

    // ─── CancelPurchaseOrderAsync ─────────────────────────────────

    [Theory]
    [InlineData(PurchaseOrderStatus.Draft)]
    [InlineData(PurchaseOrderStatus.Submitted)]
    public async Task CancelOrder_WhenCancellable_ShouldTransitionToCancelled(
        PurchaseOrderStatus status)
    {
        // Arrange
        var order = await CreateDraftOrderAsync();
        order.Status = status;
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.CancelPurchaseOrderAsync(order.Id, default);

        // Assert
        Assert.True(result.Success);
        var updated = await _dbContext.PurchaseOrders.FindAsync(order.Id);
        Assert.Equal(PurchaseOrderStatus.Cancelled, updated!.Status);
    }

    [Theory]
    [InlineData(PurchaseOrderStatus.PartiallyReceived)]
    [InlineData(PurchaseOrderStatus.Received)]
    [InlineData(PurchaseOrderStatus.Cancelled)]
    [InlineData(PurchaseOrderStatus.Returned)]
    public async Task CancelOrder_WhenNotCancellable_ShouldReturnBusinessRuleViolation(
        PurchaseOrderStatus status)
    {
        // Arrange
        var order = await CreateDraftOrderAsync();
        order.Status = status;
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.CancelPurchaseOrderAsync(order.Id, default);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ResponseCodes.BusinessRuleViolation, result.Code);
    }

    [Fact]
    public async Task CancelOrder_WhenNotFound_ShouldReturnNotFound()
    {
        var result = await _service.CancelPurchaseOrderAsync(Guid.NewGuid(), default);

        Assert.False(result.Success);
        Assert.Equal(ResponseCodes.NotFound, result.Code);
    }

    [Fact]
    public async Task CancelOrder_WhenSuccess_ShouldFireCancelledNotification()
    {
        var order = await CreateDraftOrderAsync();

        await _service.CancelPurchaseOrderAsync(order.Id, default);

        _notificationMock.Verify(
            n => n.NotifyPurchaseOrderCancelledAsync(order.Id),
            Times.Once);
    }

    // ─── ReceivePurchaseOrderAsync ────────────────────────────────

    [Fact]
    public async Task ReceiveOrder_WhenSubmitted_ShouldCreateStockBatches()
    {
        // Arrange
        var (order, product, warehouse) = await CreateSubmittedOrderAsync();
        var poItem = order.Items.First();

        var receivedItems = new List<ReceiveOrderItemRequest>
        {
            new() { PurchaseOrderItemId = poItem.Id, ReceivedQuantity = 50 }
        };

        // Act
        var result = await _service.ReceivePurchaseOrderAsync(order.Id, receivedItems, default);

        // Assert
        Assert.True(result.Success);
        var batches = await _dbContext.StockBatches.ToListAsync();
        Assert.Single(batches);
        Assert.Equal(50, batches[0].QuantityRemaining);
        Assert.Equal(order.SupplierId, batches[0].SupplierId);
        Assert.Equal(product.Id, batches[0].ProductId);
    }

    [Fact]
    public async Task ReceiveOrder_WhenPartial_ShouldSetPartiallyReceivedStatus()
    {
        // Arrange: order with quantity 100, receive only 60
        var (order, _, _) = await CreateSubmittedOrderAsync(orderedQty: 100);
        var receivedItems = new List<ReceiveOrderItemRequest>
        {
            new() { PurchaseOrderItemId = order.Items.First().Id, ReceivedQuantity = 60 }
        };

        // Act
        var result = await _service.ReceivePurchaseOrderAsync(order.Id, receivedItems, default);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, result.Data!.Status);
    }

    [Fact]
    public async Task ReceiveOrder_WhenFull_ShouldSetReceivedStatus()
    {
        // Arrange: order with quantity 100, receive full 100
        var (order, _, _) = await CreateSubmittedOrderAsync(orderedQty: 100);
        var receivedItems = new List<ReceiveOrderItemRequest>
        {
            new() { PurchaseOrderItemId = order.Items.First().Id, ReceivedQuantity = 100 }
        };

        // Act
        var result = await _service.ReceivePurchaseOrderAsync(order.Id, receivedItems, default);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(PurchaseOrderStatus.Received, result.Data!.Status);
    }

    [Theory]
    [InlineData(PurchaseOrderStatus.Draft)]
    [InlineData(PurchaseOrderStatus.Received)]
    [InlineData(PurchaseOrderStatus.Cancelled)]
    public async Task ReceiveOrder_WhenInvalidStatus_ShouldReturnBusinessRuleViolation(
        PurchaseOrderStatus status)
    {
        var order = await CreateDraftOrderAsync();
        order.Status = status;
        await _dbContext.SaveChangesAsync();

        var result = await _service.ReceivePurchaseOrderAsync(
            order.Id,
            new List<ReceiveOrderItemRequest>
            {
                new() { PurchaseOrderItemId = order.Items.First().Id, ReceivedQuantity = 10 }
            },
            default);

        Assert.False(result.Success);
        Assert.Equal(ResponseCodes.BusinessRuleViolation, result.Code);
    }

    // ─── CreatePurchaseOrderAsync Validation ─────────────────────

    [Fact]
    public async Task CreateOrder_WhenItemsEmpty_ShouldThrowArgumentException()
    {
        var request = new CreatePurchaseOrderRequest
        {
            SupplierId = Guid.NewGuid(),
            PurchaseDate = DateTime.UtcNow.AddDays(-1),
            Items = new List<CreatePurchaseOrderItemDto>()
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreatePurchaseOrderAsync(request, default));
    }

    [Fact]
    public async Task CreateOrder_WhenSupplierIdEmpty_ShouldThrowArgumentException()
    {
        var request = new CreatePurchaseOrderRequest
        {
            SupplierId = Guid.Empty,
            PurchaseDate = DateTime.UtcNow.AddDays(-1),
            Items = new List<CreatePurchaseOrderItemDto>
            {
                new() { ProductId = Guid.NewGuid(), WarehouseId = Guid.NewGuid(), Quantity = 10, UnitCost = 5 }
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreatePurchaseOrderAsync(request, default));
    }

    [Fact]
    public async Task CreateOrder_WhenFuturePurchaseDate_ShouldThrowArgumentException()
    {
        var request = new CreatePurchaseOrderRequest
        {
            SupplierId = Guid.NewGuid(),
            PurchaseDate = DateTime.UtcNow.AddDays(5), // future date
            Items = new List<CreatePurchaseOrderItemDto>
            {
                new() { ProductId = Guid.NewGuid(), WarehouseId = Guid.NewGuid(), Quantity = 10, UnitCost = 5 }
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreatePurchaseOrderAsync(request, default));
    }

    [Fact]
    public async Task CreateOrder_WhenNegativeUnitCost_ShouldThrowArgumentException()
    {
        var request = new CreatePurchaseOrderRequest
        {
            SupplierId = Guid.NewGuid(),
            PurchaseDate = DateTime.UtcNow.AddDays(-1),
            Items = new List<CreatePurchaseOrderItemDto>
            {
                new() { ProductId = Guid.NewGuid(), WarehouseId = Guid.NewGuid(), Quantity = 10, UnitCost = -1 }
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreatePurchaseOrderAsync(request, default));
    }

    [Fact]
    public async Task CreateOrder_WhenDuplicateProductIds_ShouldThrowInvalidOperationException()
    {
        var sharedProductId = Guid.NewGuid();
        var request = new CreatePurchaseOrderRequest
        {
            SupplierId = Guid.NewGuid(),
            PurchaseDate = DateTime.UtcNow.AddDays(-1),
            Items = new List<CreatePurchaseOrderItemDto>
            {
                new() { ProductId = sharedProductId, WarehouseId = Guid.NewGuid(), Quantity = 10, UnitCost = 5 },
                new() { ProductId = sharedProductId, WarehouseId = Guid.NewGuid(), Quantity = 5,  UnitCost = 5 }
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreatePurchaseOrderAsync(request, default));
    }

    [Fact]
    public async Task CreateOrder_WhenZeroQuantity_ShouldThrowArgumentException()
    {
        var request = new CreatePurchaseOrderRequest
        {
            SupplierId = Guid.NewGuid(),
            PurchaseDate = DateTime.UtcNow.AddDays(-1),
            Items = new List<CreatePurchaseOrderItemDto>
            {
                new() { ProductId = Guid.NewGuid(), WarehouseId = Guid.NewGuid(), Quantity = 0, UnitCost = 5 }
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreatePurchaseOrderAsync(request, default));
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private async Task<PurchaseOrder> CreateDraftOrderAsync()
    {
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Test Supplier", IsActive = true, CreatedAt = DateTime.UtcNow };
        var product = new Product { Id = Guid.NewGuid(), Name = "Test Product", IsActive = true, CategoryId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), Name = "Main Warehouse", IsActive = true, CreatedAt = DateTime.UtcNow };

        _dbContext.Suppliers.Add(supplier);
        _dbContext.Products.Add(product);
        _dbContext.Warehouses.Add(warehouse);
        await _dbContext.SaveChangesAsync();

        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            SupplierId = supplier.Id,
            Status = PurchaseOrderStatus.Draft,
            PurchaseDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            Items = new List<PurchaseOrderItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    WarehouseId = warehouse.Id,
                    Quantity = 100,
                    UnitCost = 10,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        _dbContext.PurchaseOrders.Add(order);
        await _dbContext.SaveChangesAsync();
        return order;
    }

    private async Task<(PurchaseOrder order, Product product, Warehouse warehouse)>
        CreateSubmittedOrderAsync(decimal orderedQty = 100)
    {
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Supplier", IsActive = true, CreatedAt = DateTime.UtcNow };
        var category = new ProductCategory { Id = Guid.NewGuid(), Name = "Category", CreatedAt = DateTime.UtcNow };
        var product = new Product { Id = Guid.NewGuid(), Name = "Product", IsActive = true, CategoryId = category.Id, CreatedAt = DateTime.UtcNow };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), Name = "Warehouse", IsActive = true, CreatedAt = DateTime.UtcNow };

        _dbContext.Suppliers.Add(supplier);
        _dbContext.ProductCategories.Add(category);
        _dbContext.Products.Add(product);
        _dbContext.Warehouses.Add(warehouse);
        await _dbContext.SaveChangesAsync();

        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            SupplierId = supplier.Id,
            Status = PurchaseOrderStatus.Submitted,
            PurchaseDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            Items = new List<PurchaseOrderItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    WarehouseId = warehouse.Id,
                    Quantity = orderedQty,
                    ReceivedQuantity = 0,
                    UnitCost = 10,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        _dbContext.PurchaseOrders.Add(order);
        await _dbContext.SaveChangesAsync();
        return (order, product, warehouse);
    }

    public void Dispose() => _dbContext.Dispose();
}
