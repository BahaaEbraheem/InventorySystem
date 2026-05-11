using InventorySystem.Shared.Responses;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Domain.Exceptions
{
    public class ValidationException : ApplicationException
    {
        public List<ResponseError> Errors { get; }

        public ValidationException(List<ResponseError> errors)
            : base("Validation failed", ResponseCodes.ValidationError, 400)
        {
            Errors = errors;
        }

        public ValidationException(string field, string code, string message)
            : this(new List<ResponseError> { new(field, code, message) }) { }

        // Helper لإنشاء استثناء من قاموس أخطاء
        public static ValidationException FromDictionary(Dictionary<string, string[]> errors)
        {
            var responseErrors = errors
                .SelectMany(kvp => kvp.Value.Select(msg => new ResponseError(kvp.Key, "INVALID_VALUE", msg)))
                .ToList();

            return new ValidationException(responseErrors);
        }
    }
}
