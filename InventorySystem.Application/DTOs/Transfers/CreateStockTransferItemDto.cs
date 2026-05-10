using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.DTOs.Transfers
{
    public class CreateStockTransferItemDto
    {
        public Guid ProductId { get; set; }
        public decimal Quantity { get; set; }
    }
}
