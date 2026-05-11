using InventorySystem.Application.DTOs.Purchase;
using InventorySystem.Shared.Responses;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.Interfaces
{
    public interface IPurchaseService
    {
        Task<CreatePurchaseOrderResponse> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default);
        Task<PurchaseOrderDto?> GetPurchaseOrderByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<BaseResponse<PurchaseOrderDto>> SubmitPurchaseOrderAsync(Guid purchaseOrderId,CancellationToken cancellationToken = default);

        Task<BaseResponse<ReceivePurchaseOrderResponse>> ReceivePurchaseOrderAsync(
            Guid purchaseOrderId,
            List<ReceiveOrderItemRequest> receivedItems,
            CancellationToken cancellationToken = default);

        Task<BaseResponse<PurchaseOrderDto>> CancelPurchaseOrderAsync(
    Guid purchaseOrderId,
    CancellationToken cancellationToken = default);
    }
}
