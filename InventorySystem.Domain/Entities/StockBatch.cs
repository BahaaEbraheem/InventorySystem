using InventorySystem.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Domain.Entities
{
    public class StockBatch : AuditableEntity
    {
        public Guid ProductId { get; set; }
        public Guid SupplierId { get; set; }
        public Guid WarehouseId { get; set; }

        public Guid PurchaseOrderItemId { get; set; }
        public PurchaseOrderItem PurchaseOrderItem { get; set; } = default!;

        public decimal QuantityReceived { get; set; }
        public decimal QuantityRemaining { get; set; }

        public DateTime PurchaseDate { get; set; }
    }
}
