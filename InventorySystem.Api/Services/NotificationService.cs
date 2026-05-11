using InventorySystem.Application.Interfaces;
using InventorySystem.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace InventorySystem.Api.Services;

public class NotificationService : INotificationService
{
    private readonly IHubContext<StockHub> _hubContext;

    public NotificationService(IHubContext<StockHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyStockChangedAsync(Guid productId, Guid warehouseId, decimal newQuantity)
    {
        return _hubContext.Clients.All.SendAsync("StockChanged", new
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Quantity = newQuantity
        });
    }

    public Task NotifyStockBelowThresholdAsync(Guid productId, Guid warehouseId, decimal newQuantity, decimal threshold)
    {
        return _hubContext.Clients.All.SendAsync("StockBelowThreshold", new
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Quantity = newQuantity,
            Threshold = threshold
        });
    }

    public async Task NotifyPurchaseOrderReceivedAsync(Guid purchaseOrderId)
    {
        await _hubContext.Clients.All
            .SendAsync("PurchaseOrderReceived", new
            {
                purchaseOrderId,
                timestamp = DateTime.UtcNow
            });
    }
}
