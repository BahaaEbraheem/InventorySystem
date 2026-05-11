using InventorySystem.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.DTOs.Purchase
{
    public class ReceivePurchaseOrderResponse
    {
        public Guid PurchaseOrderId { get; set; }
        public PurchaseOrderStatus Status { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public decimal TotalReceivedQuantity { get; set; }
    }
}
