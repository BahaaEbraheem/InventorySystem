using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Shared.Enums
{
    public enum PurchaseOrderStatus
    {
        Draft = 1,              // مسودة - لم يُرسل بعد
        Submitted = 2,          // مُرسل للمورد - في انتظار الاستلام
        PartiallyReceived = 3,  // تم استلام جزء
        Received = 4,           // تم الاستلام الكامل ✅
        Cancelled = 5,          // ملغى
        Returned = 6            // مُرتجع للمورد
    }
}
