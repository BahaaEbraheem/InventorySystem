using InventorySystem.Api.Hubs;
using InventorySystem.Application.Interfaces;
using InventorySystem.Domain.Entities;
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

    public async Task NotifyPurchaseOrderSubmittedAsync(Guid purchaseOrderId)
    {
        await _hubContext.Clients.All
           .SendAsync("PurchaseOrderSubmitted", new
           {
               purchaseOrderId,
               timestamp = DateTime.UtcNow
           });
    }

    public async Task NotifyPurchaseOrderCancelledAsync(Guid purchaseOrderId)
    {
        await _hubContext.Clients.All
      .SendAsync("PurchaseOrderCancelled", new
      {
          purchaseOrderId,
          timestamp = DateTime.UtcNow
      });
    }

    public async Task NotifyTransferReceivedAsync(Guid transferId)
    {
        await _hubContext.Clients.All
      .SendAsync("TransferReceived", new
      {
          transferId,
          timestamp = DateTime.UtcNow
      });
    }
}
