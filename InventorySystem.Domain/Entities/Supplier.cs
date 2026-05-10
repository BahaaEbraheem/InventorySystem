using InventorySystem.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Domain.Entities
{
    public class Supplier : AuditableEntity
    {
        public string Name { get; set; } = default!;
    }
}
