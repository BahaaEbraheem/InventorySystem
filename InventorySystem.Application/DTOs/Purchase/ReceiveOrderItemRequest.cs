using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.DTOs.Purchase
{
    public class ReceiveOrderItemRequest
    {
        public Guid PurchaseOrderItemId { get; set; }
        public decimal ReceivedQuantity { get; set; } // الكمية المستلمة فعليًا
        public string? Notes { get; set; } // ملاحظات عن الاستلام (تالف، ناقص، إلخ)
    }
}
