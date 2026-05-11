using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.DTOs.Purchase
{
    public class PurchaseOrderItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductSku { get; set; }
        public Guid WarehouseId { get; set; }
        public string? WarehouseName { get; set; }
        public decimal UnitCost { get; set; }
        public decimal Quantity { get; set; }
        public decimal LineTotal => UnitCost * Quantity;
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }
}
