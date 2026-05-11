using InventorySystem.Application.DTOs.Purchase;
using InventorySystem.Application.Interfaces;
using InventorySystem.Domain.Entities;
using InventorySystem.Domain.Exceptions;
using InventorySystem.Infrastructure.Persistence;
using InventorySystem.Shared.Enums;
using InventorySystem.Shared.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventorySystem.Infrastructure.Services;

public class PurchaseService : IPurchaseService
{
    private readonly AppDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PurchaseService> _logger;
    public PurchaseService(ILogger<PurchaseService> logger,AppDbContext dbContext, INotificationService notificationService)
    {
        _logger = logger;
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

            Status = purchaseOrder.Status,

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

    public async Task<BaseResponse<PurchaseOrderDto>> SubmitPurchaseOrderAsync(
    Guid purchaseOrderId,
    CancellationToken cancellationToken = default)
    {
        try
        {
            //  جلب الطلب مع العناصر للتحقق
            var purchaseOrder = await _dbContext.PurchaseOrders
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == purchaseOrderId, cancellationToken);

            if (purchaseOrder == null)
                return BaseResponse<PurchaseOrderDto>.NotFound(
                    $"Purchase order with id '{purchaseOrderId}' not found");

            //  قاعدة العمل: الإرسال مسموح فقط من حالة المسودة
            if (purchaseOrder.Status != PurchaseOrderStatus.Draft)
                return BaseResponse<PurchaseOrderDto>.ErrorResponse(
                    ResponseCodes.BusinessRuleViolation,
                    $"Cannot submit order in status '{purchaseOrder.Status}'. Only 'Draft' orders can be submitted.");

            //  منع إرسال طلب فارغ
            if (!purchaseOrder.Items.Any())
                return BaseResponse<PurchaseOrderDto>.ErrorResponse(
                    ResponseCodes.ValidationError,
                    "Cannot submit an empty purchase order. Add at least one item.");

            //  التحقق من أن جميع العناصر صالحة (منتج/مستودع نشط)
            var invalidItems = new List<string>();
            foreach (var item in purchaseOrder.Items)
            {
                var productActive = await _dbContext.Products
                    .AnyAsync(p => p.Id == item.ProductId && p.IsActive, cancellationToken);
                if (!productActive)
                    invalidItems.Add($"Product {item.ProductId} is inactive");

                var warehouseActive = await _dbContext.Warehouses
                    .AnyAsync(w => w.Id == item.WarehouseId && w.IsActive, cancellationToken);
                if (!warehouseActive)
                    invalidItems.Add($"Warehouse {item.WarehouseId} is inactive");
            }

            if (invalidItems.Any())
                return BaseResponse<PurchaseOrderDto>.ErrorResponse(
                    ResponseCodes.ValidationError,
                    "Order contains invalid items",
                    invalidItems.Select(f => new ResponseError("Items", "INVALID_ITEM", f)).ToList());

            //  تغيير الحالة وتحديث الطوابع الزمنية
            purchaseOrder.Status = PurchaseOrderStatus.Submitted;
            purchaseOrder.ModifiedAt = DateTime.UtcNow;
            purchaseOrder.ModifiedBy = "system"; // ⚠️ استبدل بـ User.Identity.Name في الإنتاج

            await _dbContext.SaveChangesAsync(cancellationToken);

            //  إشعار: تم إرسال الطلب للمورد
            await _notificationService.NotifyPurchaseOrderSubmittedAsync(purchaseOrder.Id);

            //  إرجاع النتيجة
            var dto = await GetPurchaseOrderByIdAsync(purchaseOrder.Id, cancellationToken);
            return BaseResponse<PurchaseOrderDto>.SuccessResponse(
                dto!,
                $"Purchase order submitted successfully. Status: {PurchaseOrderStatus.Submitted}");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency conflict while submitting purchase order {OrderId}", purchaseOrderId);
            return BaseResponse<PurchaseOrderDto>.ErrorResponse(
                ResponseCodes.ConcurrencyError,
                "The order was modified by another user. Please refresh and try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while submitting purchase order {OrderId}", purchaseOrderId);
            return BaseResponse<PurchaseOrderDto>.InternalError("An unexpected error occurred while submitting the order.");
        }
    }



    public async Task<BaseResponse<ReceivePurchaseOrderResponse>> ReceivePurchaseOrderAsync(
        Guid purchaseOrderId,
        List<ReceiveOrderItemRequest> receivedItems,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var purchaseOrder = await _dbContext.PurchaseOrders
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == purchaseOrderId, cancellationToken);

            if (purchaseOrder == null)
                return BaseResponse<ReceivePurchaseOrderResponse>.NotFound(
                    $"Purchase order with id '{purchaseOrderId}' not found");

            // ✅ التحقق من الحالة المسموح بها
            if (purchaseOrder.Status != PurchaseOrderStatus.Submitted &&
                purchaseOrder.Status != PurchaseOrderStatus.PartiallyReceived)
            {
                return BaseResponse<ReceivePurchaseOrderResponse>.ErrorResponse(
                    ResponseCodes.BusinessRuleViolation,
                    $"Cannot receive order in status '{purchaseOrder.Status}'. Expected 'Submitted' or 'PartiallyReceived'.");
            }

            var totalReceived = 0m;

            foreach (var received in receivedItems)
            {
                var poItem = purchaseOrder.Items.FirstOrDefault(i => i.Id == received.PurchaseOrderItemId);
                if (poItem == null) continue;

                // ✅ إنشاء StockBatch
                var stockBatch = new StockBatch
                {
                    Id = Guid.NewGuid(),
                    ProductId = poItem.ProductId,
                    SupplierId = purchaseOrder.SupplierId,
                    WarehouseId = poItem.WarehouseId,
                    PurchaseOrderItemId = poItem.Id,  // ✅ استخدم الـ Id فقط
                    OrderedQuantity = poItem.Quantity,
                    QuantityReceived = received.ReceivedQuantity,
                    QuantityRemaining = received.ReceivedQuantity,
                    QuantityReserved = 0,
                    PurchaseDate = purchaseOrder.PurchaseDate,
                    ReceivedDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system",
                    RowVersion = new byte[8]  // ✅ قيمة افتراضية
                };

                await _dbContext.StockBatches.AddAsync(stockBatch, cancellationToken);
                poItem.ReceivedQuantity += received.ReceivedQuantity;
                totalReceived += received.ReceivedQuantity;
            }

            // ✅ تحديث حالة الطلب
            var allReceived = purchaseOrder.Items.All(i => i.ReceivedQuantity >= i.Quantity);
            var someReceived = purchaseOrder.Items.Any(i => i.ReceivedQuantity > 0);

            purchaseOrder.Status = allReceived
                ? PurchaseOrderStatus.Received
                : (someReceived ? PurchaseOrderStatus.PartiallyReceived : purchaseOrder.Status);

            if (allReceived)
                purchaseOrder.ReceivedDate = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _notificationService.NotifyPurchaseOrderReceivedAsync(purchaseOrderId);

            return BaseResponse<ReceivePurchaseOrderResponse>.SuccessResponse(
                new ReceivePurchaseOrderResponse
                {
                    PurchaseOrderId = purchaseOrder.Id,
                    Status = purchaseOrder.Status,
                    ReceivedAt = purchaseOrder.ReceivedDate,
                    TotalReceivedQuantity = totalReceived
                },
                $"Purchase order received successfully. Status: {purchaseOrder.Status}");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency conflict while receiving purchase order {OrderId}", purchaseOrderId);
            return BaseResponse<ReceivePurchaseOrderResponse>.ErrorResponse(
                ResponseCodes.ConcurrencyError,
                "The order was modified by another user. Please refresh and try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while receiving purchase order {OrderId}", purchaseOrderId);
            return BaseResponse<ReceivePurchaseOrderResponse>.InternalError(
                "An unexpected error occurred while receiving the order.");
        }
    }
    public async Task<BaseResponse<PurchaseOrderDto>> CancelPurchaseOrderAsync(
    Guid purchaseOrderId,
    CancellationToken cancellationToken = default)
    {
        try
        {
            var purchaseOrder = await _dbContext.PurchaseOrders
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == purchaseOrderId, cancellationToken);

            if (purchaseOrder == null)
                return BaseResponse<PurchaseOrderDto>.NotFound(
                    $"Purchase order with id '{purchaseOrderId}' not found");

            // ✅ قاعدة العمل: الإلغاء مسموح فقط من الحالات المسموح بها
            if (!purchaseOrder.CanBeCancelled())
            {
                return BaseResponse<PurchaseOrderDto>.ErrorResponse(
                    ResponseCodes.BusinessRuleViolation,
                    $"Cannot cancel order in status '{purchaseOrder.Status}'. " +
                    $"Only 'Draft' or 'Submitted' orders can be cancelled.");
            }

            // ✅ تحديث الحالة
            purchaseOrder.Status = PurchaseOrderStatus.Cancelled;
            purchaseOrder.ModifiedAt = DateTime.UtcNow;
            purchaseOrder.ModifiedBy = "system"; // ⚠️ استبدل بـ User.Identity.Name في الإنتاج

            // ⚠️ ملاحظة: إذا كان هناك حجز مخزون مرتبط بهذا الطلب، يجب إلغاؤه هنا
            // مثال: تحديث أي StockBatch مرتبط بـ QuantityReserved = 0
            // (يمكن إضافته لاحقًا حسب متطلبات العمل)

            await _dbContext.SaveChangesAsync(cancellationToken);

            // ✅ إشعار: تم إلغاء الطلب
            await _notificationService.NotifyPurchaseOrderCancelledAsync(purchaseOrder.Id);

            // ✅ إرجاع النتيجة
            var dto = await GetPurchaseOrderByIdAsync(purchaseOrder.Id, cancellationToken);
            return BaseResponse<PurchaseOrderDto>.SuccessResponse(
                dto!,
                $"Purchase order cancelled successfully. Status: {PurchaseOrderStatus.Cancelled}");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency conflict while cancelling purchase order {OrderId}", purchaseOrderId);
            return BaseResponse<PurchaseOrderDto>.ErrorResponse(
                ResponseCodes.ConcurrencyError,
                "The order was modified by another user. Please refresh and try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while cancelling purchase order {OrderId}", purchaseOrderId);
            return BaseResponse<PurchaseOrderDto>.InternalError(
                "An unexpected error occurred while cancelling the order.");
        }
    }

}



