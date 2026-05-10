using InventorySystem.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Domain.Entities
{
    public class PurchaseOrderItem : AuditableEntity
    {
        public Guid PurchaseOrderId { get; set; }
        public PurchaseOrder PurchaseOrder { get; set; } = default!;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = default!;

        public Guid WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; } = default!;

        public decimal UnitCost { get; set; }
        public decimal Quantity { get; set; }
    }
}
