using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Domain.Common
{
    public abstract class AuditableEntity
    {
        public Guid Id { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = "system";

        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }

        public bool IsDeleted { get; set; }
    }
}
