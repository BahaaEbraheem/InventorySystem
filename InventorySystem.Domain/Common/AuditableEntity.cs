using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Domain.Common
{
    public abstract class AuditableEntity
    {
        public Guid Id { get; set; }

        public string CreatedBy { get; set; } = default!;
        public DateTime CreatedAt { get; set; }

        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }

        public bool IsDeleted { get; set; }
    }
}
