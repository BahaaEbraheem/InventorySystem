using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.Interfaces;

public interface INotificationService
{
    Task NotifyStockChangedAsync(Guid productId, Guid warehouseId, decimal newQuantity);
    Task NotifyStockBelowThresholdAsync(Guid productId, Guid warehouseId, decimal newQuantity, decimal threshold);
    Task NotifyPurchaseOrderReceivedAsync(Guid purchaseOrderId);
    Task NotifyPurchaseOrderSubmittedAsync(Guid purchaseOrderId);

    Task NotifyPurchaseOrderCancelledAsync(Guid purchaseOrderId);
    Task NotifyTransferReceivedAsync(Guid transferId);

    Task NotifyTransferFailedAsync(Guid productId, Guid warehouseId, decimal attemptedQuantity);
}

