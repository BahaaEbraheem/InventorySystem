using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.DTOs.Sales
{
    public class CreateSaleRequest
    {
        public Guid IdempotencyKey { get; set; } = Guid.NewGuid();
        public DateTime SaleDate { get; set; }
        public List<CreateSaleItemDto> Items { get; set; } = new();
    }
}
