using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.DTOs.Reporting
{
    public class WarehouseStockSummary
    {
        public Guid WarehouseId { get; set; }
        public decimal Quantity { get; set; }
    }
}
