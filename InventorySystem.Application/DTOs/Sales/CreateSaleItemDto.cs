using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.DTOs.Sales
{
    public class CreateSaleItemDto
    {
        public Guid ProductId { get; set; }
        public decimal Quantity { get; set; }
        public Guid WarehouseId { get; set; }
    }
}
