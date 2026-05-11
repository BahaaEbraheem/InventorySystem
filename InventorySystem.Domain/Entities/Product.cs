using InventorySystem.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Domain.Entities
{
    public class Product : AuditableEntity
    {
        public string Name { get; set; } = default!;
        public string? Sku { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid CategoryId { get; set; }
        public ProductCategory Category { get; set; } = default!;
    }
}
