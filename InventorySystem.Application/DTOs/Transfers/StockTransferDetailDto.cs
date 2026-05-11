using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.DTOs.Transfers
{
    public class StockTransferDetailDto
    {
        public Guid Id { get; set; }
        public DateTime TransferDate { get; set; }
        public Guid FromWarehouseId { get; set; }
        public string FromWarehouseName { get; set; } = default!;
        public Guid ToWarehouseId { get; set; }
        public string ToWarehouseName { get; set; } = default!;
        public string Status { get; set; } = default!;
        public DateTime? CompletedAt { get; set; }
        public List<StockTransferItemDetailDto> Items { get; set; } = new();
    }

    public class StockTransferItemDetailDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = default!;
        public decimal Quantity { get; set; }
    }
}
