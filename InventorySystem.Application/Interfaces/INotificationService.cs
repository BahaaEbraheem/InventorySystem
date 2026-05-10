using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.Interfaces;

public interface INotificationService
{
    Task NotifyStockChangedAsync(Guid productId, Guid warehouseId, decimal newQuantity);
    Task NotifyStockBelowThresholdAsync(Guid productId, Guid warehouseId, decimal newQuantity, decimal threshold);
}

