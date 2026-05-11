using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Shared.Responses
{
    public class ResponseError
    {
        public string Field { get; set; } = string.Empty;  // اسم الحقل الذي به خطأ
        public string Code { get; set; } = string.Empty;   // كود الخطأ التفصيلي
        public string Message { get; set; } = string.Empty; // رسالة الخطأ للمستخدم

        public ResponseError() { }

        public ResponseError(string field, string code, string message)
        {
            Field = field;
            Code = code;
            Message = message;
        }
    }
}
