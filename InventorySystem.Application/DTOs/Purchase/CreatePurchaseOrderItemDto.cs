using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.DTOs.Purchase
{
    public class CreatePurchaseOrderItemDto
    {
        public Guid ProductId { get; set; }
        public Guid WarehouseId { get; set; }
        public decimal UnitCost { get; set; }
        public decimal Quantity { get; set; }
    }
}
