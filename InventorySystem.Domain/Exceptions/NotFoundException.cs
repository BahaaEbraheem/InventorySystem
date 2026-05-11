using InventorySystem.Shared.Responses;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Domain.Exceptions
{
    public class NotFoundException : ApplicationException
    {
        public NotFoundException(string entityName, object id)
            : base(
                $"{entityName} with ID '{id}' was not found",
                ResponseCodes.NotFound,
                404,
                new() { { "entity", entityName }, { "id", id.ToString() } })
        { }
    }
}
