using InventorySystem.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Domain.Entities
{
    public class PurchaseOrder : AuditableEntity
    {
        public Guid SupplierId { get; set; }
        public Supplier Supplier { get; set; } = default!;

        public DateTime PurchaseDate { get; set; }

        public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
    }
}
