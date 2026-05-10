using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.DTOs.Sales
{
    public class CreateSaleRequest
    {
        public DateTime SaleDate { get; set; }
        public List<CreateSaleItemDto> Items { get; set; } = new();
    }
}
