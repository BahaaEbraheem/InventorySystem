using InventorySystem.Domain.Common;
using InventorySystem.Shared.Enums;
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
        public DateTime? ExpectedDeliveryDate { get; set; } 
        public DateTime? ReceivedDate { get; set; }        

        //  الحالة + ملاحظات
        public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
        public string? Notes { get; set; }

        public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();

        //  دالة مساعدة للتحقق من إمكانية الإلغاء
        public bool CanBeCancelled() =>
            Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Submitted;
    }
}
