using InventorySystem.Application.Interfaces;

public class FakeNotificationService : INotificationService
{
    public Task NotifyStockChangedAsync(Guid productId, Guid warehouseId, decimal newQuantity)
        => Task.CompletedTask;

    public Task NotifyStockBelowThresholdAsync(Guid productId, Guid warehouseId, decimal newQuantity, decimal threshold)
        => Task.CompletedTask;
}

