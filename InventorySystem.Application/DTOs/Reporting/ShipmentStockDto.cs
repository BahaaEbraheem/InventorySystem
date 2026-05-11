using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.DTOs.Reporting
{
    public class ShipmentStockDto
    {
        public Guid PurchaseOrderItemId { get; set; }
        public decimal TotalRemaining { get; set; }
        public decimal TotalReserved { get; set; }
        public List<WarehouseStockSummary> ByWarehouse { get; set; } = new();
    }
}
