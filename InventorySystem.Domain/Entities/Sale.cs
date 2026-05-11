using InventorySystem.Domain.Common;
using System;
using System.Collections.Generic;
using System.Net.ServerSentEvents;
using System.Text;

namespace InventorySystem.Domain.Entities
{
    public class Sale : AuditableEntity
    {
        public DateTime SaleDate { get; set; }

        public Guid WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; } = default!;
        public bool IsActive { get; set; } = true;
        public Guid IdempotencyKey { get; set; }
        public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
    }
}
