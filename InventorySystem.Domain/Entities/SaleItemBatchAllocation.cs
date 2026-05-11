using InventorySystem.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Domain.Entities
{
    // ربط عملية البيع بالدفعة التي أتت منها الكمية
    //  جسر التتبع    
    public class SaleItemBatchAllocation : AuditableEntity
    {
        public Guid SaleItemId { get; set; }
        public SaleItem SaleItem { get; set; } = default!;

        public Guid StockBatchId { get; set; }
        public StockBatch StockBatch { get; set; } = default!;

        public decimal Quantity { get; set; }    // الكمية المأخوذة من هذه الدفعة
    }
}
