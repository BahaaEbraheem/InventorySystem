using InventorySystem.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Domain.Entities
{
    public class StockTransferItem : AuditableEntity
    {
        public Guid StockTransferId { get; set; }
        public StockTransfer StockTransfer { get; set; } = default!;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = default!;

        public decimal Quantity { get; set; }
    }
}
