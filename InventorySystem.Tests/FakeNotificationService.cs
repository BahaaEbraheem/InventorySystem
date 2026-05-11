// File: tests/InventorySystem.Tests.Unit/TestHelpers/FakeNotificationService.cs
using InventorySystem.Application.Interfaces;

namespace InventorySystem.Tests.Unit.TestHelpers;

public class FakeNotificationService : INotificationService
{
    public List<StockChangeNotification> StockChanges { get; } = new();
    public List<Guid> PurchaseOrdersReceived { get; } = new();
    public List<Guid> TransfersReceived { get; } = new();
    public List<Guid> PurchaseOrdersCancelled { get; } = new(); 

    // قائمة جديدة لمتابعة الإرسال
    public List<Guid> PurchaseOrdersSubmitted { get; } = new();

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

    // ✅ الإصلاح: إضافة إلى القائمة الصحيحة
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


}

// Record بسيط للإشعارات (للتحقق في الاختبارات)
public record StockChangeNotification(Guid ProductId, Guid WarehouseId, decimal NewQuantity, decimal? Threshold = null);