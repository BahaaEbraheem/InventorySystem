using InventorySystem.Application.Interfaces;

public class FakeNotificationService : INotificationService
{
    public List<StockChangeNotification> StockChanges { get; } = new();
    public List<Guid> PurchaseOrdersReceived { get; } = new();
    public List<Guid> TransfersReceived { get; } = new();
    public List<Guid> PurchaseOrdersCancelled { get; } = new();
    public List<Guid> PurchaseOrdersSubmitted { get; } = new();

    // ✅ جديد: قائمة لمتابعة التحويلات الفاشلة
    public List<TransferFailedNotification> TransfersFailed { get; } = new();

    public Task NotifyStockChangedAsync(Guid productId, Guid warehouseId, decimal newQuantity)
    {
        StockChanges.Add(new(productId, warehouseId, newQuantity));
        return Task.CompletedTask;
    }

    public Task NotifyStockBelowThresholdAsync(Guid productId, Guid warehouseId, decimal current, decimal threshold)
    {
        StockChanges.Add(new(productId, warehouseId, current, threshold));
        return Task.CompletedTask;
    }

    public Task NotifyPurchaseOrderReceivedAsync(Guid purchaseOrderId)
    {
        PurchaseOrdersReceived.Add(purchaseOrderId);
        return Task.CompletedTask;
    }

    public Task NotifyPurchaseOrderSubmittedAsync(Guid purchaseOrderId)
    {
        PurchaseOrdersSubmitted.Add(purchaseOrderId);
        return Task.CompletedTask;
    }

    public Task NotifyTransferReceivedAsync(Guid transferId)
    {
        TransfersReceived.Add(transferId);
        return Task.CompletedTask;
    }

    public Task NotifyPurchaseOrderCancelledAsync(Guid purchaseOrderId)
    {
        PurchaseOrdersCancelled.Add(purchaseOrderId);
        return Task.CompletedTask;
    }

    // ✅ جديد: تنفيذ إشعار فشل التحويل
    public Task NotifyTransferFailedAsync(Guid productId, Guid warehouseId, decimal attemptedQuantity)
    {
        TransfersFailed.Add(new(productId, warehouseId, attemptedQuantity));
        return Task.CompletedTask;
    }
}

// Record بسيط للإشعارات (للتحقق في الاختبارات)
public record StockChangeNotification(Guid ProductId, Guid WarehouseId, decimal NewQuantity, decimal? Threshold = null);

// ✅ جديد: Record للتحويلات الفاشلة
public record TransferFailedNotification(Guid ProductId, Guid WarehouseId, decimal AttemptedQuantity);
