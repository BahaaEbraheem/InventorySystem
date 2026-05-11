using InventorySystem.Shared.Responses;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Domain.Exceptions
{
    public class ConcurrencyException : ApplicationException
    {
        public ConcurrencyException(string entityName, object id)
            : base(
                $"{entityName} with ID '{id}' was modified by another user. Please refresh and retry.",
                ResponseCodes.ConcurrencyError,
                409)
        { }
    }
}
