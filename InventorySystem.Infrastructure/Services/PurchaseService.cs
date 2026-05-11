using InventorySystem.Application.DTOs.Purchase;
using InventorySystem.Application.Interfaces;
using InventorySystem.Domain.Entities;
using InventorySystem.Domain.Exceptions;
using InventorySystem.Infrastructure.Persistence;
using InventorySystem.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Infrastructure.Services;

public class PurchaseService : IPurchaseService
{
    private readonly AppDbContext _dbContext;
    private readonly INotificationService _notificationService;
    public PurchaseService(AppDbContext dbContext, INotificationService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    public async Task<CreatePurchaseOrderResponse> CreatePurchaseOrderAsync(
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate request
        await ValidateRequestAsync(request, cancellationToken);

        // Business logic
        var purchaseOrder = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            SupplierId = request.SupplierId,
            PurchaseDate = request.PurchaseDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
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
        }

        await _dbContext.PurchaseOrders.AddAsync(purchaseOrder, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreatePurchaseOrderResponse
        {
            PurchaseOrderId = purchaseOrder.Id
        };
    }

    // ===== SEPARATE VALIDATION METHOD =====
    private async Task ValidateRequestAsync(
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request), "Purchase order request cannot be null");

        if (request.SupplierId == Guid.Empty)
            throw new ArgumentException("Supplier ID is required", nameof(request.SupplierId));

        if (request.PurchaseDate == default)
            throw new ArgumentException("Purchase date is required", nameof(request.PurchaseDate));

        if (request.PurchaseDate > DateTime.UtcNow.Date)
            throw new ArgumentException("Purchase date cannot be in the future", nameof(request.PurchaseDate));

        if (request.Items == null || request.Items.Count == 0)
            throw new ArgumentException("At least one order item is required", nameof(request.Items));

        var seenProducts = new HashSet<Guid>();

        for (int i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            var itemPrefix = $"{nameof(request.Items)}[{i}]";

            if (item == null)
                throw new ArgumentNullException($"{itemPrefix}", $"Item at index {i} cannot be null");

            if (item.ProductId == Guid.Empty)
                throw new ArgumentException($"Product ID is required", $"{itemPrefix}.{nameof(item.ProductId)}");

            if (item.WarehouseId == Guid.Empty)
                throw new ArgumentException($"Warehouse ID is required", $"{itemPrefix}.{nameof(item.WarehouseId)}");

            if (item.Quantity <= 0)
                throw new ArgumentException($"Quantity must be positive", $"{itemPrefix}.{nameof(item.Quantity)}");

            if (item.UnitCost < 0)
                throw new ArgumentException($"Unit cost cannot be negative", $"{itemPrefix}.{nameof(item.UnitCost)}");

            if (!seenProducts.Add(item.ProductId))
                throw new InvalidOperationException($"Duplicate product ID found: {item.ProductId}");
        }

        // Optional: Database existence checks
        await ValidateEntitiesExistAsync(request, cancellationToken);
    }

    // ===== OPTIONAL: DATABASE EXISTENCE VALIDATION =====
    private async Task ValidateEntitiesExistAsync(
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        // Validate Supplier exists and is active
        var supplierExists = await _dbContext.Suppliers
            .AnyAsync(s => s.Id == request.SupplierId && s.IsActive, cancellationToken);

        if (!supplierExists)
            throw new ArgumentException($"Supplier {request.SupplierId} not found or inactive", nameof(request.SupplierId));

        // Validate Products and Warehouses
        var productIds = request.Items.Select(i => i.ProductId).Distinct();
        var warehouseIds = request.Items.Select(i => i.WarehouseId).Distinct();

        var validProducts = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var validWarehouses = await _dbContext.Warehouses
            .Where(w => warehouseIds.Contains(w.Id) && w.IsActive)
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);

        foreach (var item in request.Items)
        {
            if (!validProducts.Contains(item.ProductId))
                throw new ArgumentException($"Product {item.ProductId} not found or inactive");

            if (!validWarehouses.Contains(item.WarehouseId))
                throw new ArgumentException($"Warehouse {item.WarehouseId} not found or inactive");
        }
    }





    public async Task<PurchaseOrderDto?> GetPurchaseOrderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var purchaseOrder = await _dbContext.PurchaseOrders.AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Items)
                .ThenInclude(i => i.Product)
            .Include(p => p.Items)
                .ThenInclude(i => i.Warehouse)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (purchaseOrder == null)
            return null;

        return new PurchaseOrderDto
        {
            Id = purchaseOrder.Id,
            SupplierId = purchaseOrder.SupplierId,
            SupplierName = purchaseOrder.Supplier?.Name,
            PurchaseDate = purchaseOrder.PurchaseDate,
            CreatedAt = purchaseOrder.CreatedAt,
            CreatedBy = purchaseOrder.CreatedBy,
            TotalAmount = purchaseOrder.Items.Sum(i => i.UnitCost * i.Quantity),
            Items = purchaseOrder.Items.Select(item => new PurchaseOrderItemDto
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductName = item.Product?.Name,
                ProductSku = item.Product?.Sku,
                WarehouseId = item.WarehouseId,
                WarehouseName = item.Warehouse?.Name,
                UnitCost = item.UnitCost,
                Quantity = item.Quantity,
                CreatedAt = item.CreatedAt,
                CreatedBy = item.CreatedBy
            }).ToList()
        };
    }




    // ✅ دالة جديدة: تُستدعى عند وصول الشحنة فعليًا من المورد
    public async Task<ReceivePurchaseOrderResponse> ReceivePurchaseOrderAsync(
        Guid purchaseOrderId,
        List<ReceiveOrderItemRequest> receivedItems, // الكمية المستلمة فعليًا لكل صنف
        CancellationToken cancellationToken = default)
    {
        var purchaseOrder = await _dbContext.PurchaseOrders
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == purchaseOrderId, cancellationToken);

        if (purchaseOrder == null)
            throw new NotFoundException(nameof(PurchaseOrder), purchaseOrderId);

        if (purchaseOrder.Status != PurchaseOrderStatus.Submitted)
            throw new InvalidOperationException($"Cannot receive order in status {purchaseOrder.Status}");

        foreach (var received in receivedItems)
        {
            var poItem = purchaseOrder.Items.FirstOrDefault(i => i.Id == received.PurchaseOrderItemId);
            if (poItem == null) continue;

            // ✅ إنشاء/تحديث StockBatch للكمية المستلمة
            var stockBatch = new StockBatch
            {
                Id = Guid.NewGuid(),
                ProductId = poItem.ProductId,
                SupplierId = purchaseOrder.SupplierId,
                WarehouseId = poItem.WarehouseId,
                PurchaseOrderItem = poItem,
                OrderedQuantity = poItem.Quantity,
                QuantityReceived = received.ReceivedQuantity, // ✅ الكمية الفعلية
                QuantityRemaining = received.ReceivedQuantity, // ✅ متاحة للبيع فورًا
                QuantityReserved = 0,
                PurchaseDate = purchaseOrder.PurchaseDate,
                ReceivedDate = DateTime.UtcNow, // ✅ تاريخ الاستلام الفعلي
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };

            await _dbContext.StockBatches.AddAsync(stockBatch, cancellationToken);

            // ✅ تحديث حالة عنصر أمر الشراء
            poItem.ReceivedQuantity += received.ReceivedQuantity;
        }

        // ✅ تحديث حالة أمر الشراء الكلي
        var allReceived = purchaseOrder.Items.All(i => i.ReceivedQuantity >= i.Quantity);
        var someReceived = purchaseOrder.Items.Any(i => i.ReceivedQuantity > 0);

        purchaseOrder.Status = allReceived
            ? PurchaseOrderStatus.Received
            : (someReceived ? PurchaseOrderStatus.PartiallyReceived : purchaseOrder.Status);

        if (allReceived)
            purchaseOrder.ReceivedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // ✅ إشعار: "تم استلام طلب الشراء"
        await _notificationService.NotifyPurchaseOrderReceivedAsync(purchaseOrderId);

        return new ReceivePurchaseOrderResponse
        {
            PurchaseOrderId = purchaseOrder.Id,
            Status = purchaseOrder.Status,
            ReceivedAt = purchaseOrder.ReceivedDate
        };
    }


}



