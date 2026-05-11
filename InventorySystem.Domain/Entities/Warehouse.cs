using InventorySystem.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Domain.Entities
{
    public class Warehouse : AuditableEntity
    {
        public string Name { get; set; } = default!;
        public bool IsActive { get; set; } = true;

        public string? Location { get; set; }
    }
}
