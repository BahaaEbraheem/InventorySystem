using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Domain.Exceptions
{
    public abstract class ApplicationException : Exception
    {
        public string ErrorCode { get; }
        public int StatusCode { get; }
        public Dictionary<string, string> Metadata { get; }

        protected ApplicationException(
            string message,
            string errorCode,
            int statusCode,
            Dictionary<string, string>? metadata = null)
            : base(message)
        {
            ErrorCode = errorCode;
            StatusCode = statusCode;
            Metadata = metadata ?? new();
        }
    }
}
