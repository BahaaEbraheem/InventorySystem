using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Shared.Enums
{
    public enum StockTransferStatus
    {
        Pending = 1,        // قيد الانتظار
        Picked = 2,         // تم السحب من المصدر
        InTransit = 3,      // قيد النقل
        Received = 4,       // ✅ تم الاستلام في الوجهة
        Cancelled = 5,      // ملغى
        Failed = 6          // فشل (لإعادة المحاولة)
    }
}
