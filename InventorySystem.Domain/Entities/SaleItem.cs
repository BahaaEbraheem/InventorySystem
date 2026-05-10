using InventorySystem.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Domain.Entities
{
    public class SaleItem : AuditableEntity
    {
        public Guid SaleId { get; set; }
        public Sale Sale { get; set; } = default!;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = default!;

        public decimal Quantity { get; set; }

        public ICollection<SaleItemBatchAllocation> BatchAllocations { get; set; } = new List<SaleItemBatchAllocation>();
    }
}
