using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.DTOs.Purchase
{
    public class CreatePurchaseOrderRequest
    {
        public Guid SupplierId { get; set; }
        public DateTime PurchaseDate { get; set; }
        public List<CreatePurchaseOrderItemDto> Items { get; set; } = new();
    }
}
