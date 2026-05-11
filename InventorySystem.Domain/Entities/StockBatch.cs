using InventorySystem.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace InventorySystem.Domain.Entities
{
    // يمثل شحنة محددة من مورد محدد
    public class StockBatch : AuditableEntity
    {
        public Guid ProductId { get; set; }
        public Guid SupplierId { get; set; }
        public Supplier Supplier { get; set; } = default!;
        public Guid WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; } = default!;
        public Guid PurchaseOrderItemId { get; set; }
        public PurchaseOrderItem PurchaseOrderItem { get; set; } = default!;
        public bool IsActive { get; set; } = true;

        //  الكمية المطلوبة من أمر الشراء (المرجعية)
        public decimal OrderedQuantity { get; set; }

        //  الكمية المستلمة فعليًا (قد تكون < OrderedQuantity)
        public decimal QuantityReceived { get; set; }

        // الكمية المتبقية للبيع (بعد الخصم والمبيعات)
        public decimal QuantityRemaining { get; set; }

        //  الكمية المحجوزة لمبيعات قيد المعالجة
        public decimal QuantityReserved { get; set; }

        //  الكمية المتاحة فعليًا للبيع
        public decimal QuantityAvailable => QuantityRemaining - QuantityReserved;

        public DateTime PurchaseDate { get; set; }
        public DateTime? ReceivedDate { get; set; } //  تاريخ الاستلام الفعلي

        //  هل اكتمل استلام هذه الشحنة؟
        public bool IsFullyReceived => QuantityReceived >= OrderedQuantity;

        [Timestamp]
        public byte[] RowVersion { get; set; }
    }
}
