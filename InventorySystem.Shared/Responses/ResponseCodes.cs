namespace InventorySystem.Shared.Responses;

// File: Application/Common/ResponseCodes.cs
public static class ResponseCodes
{
    public const string Success = "SUCCESS";
    public const string ValidationError = "VALIDATION_ERROR";
    public const string NotFound = "NOT_FOUND";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string Conflict = "CONFLICT";
    public const string ConcurrencyError = "CONCURRENCY_ERROR";
    public const string InsufficientStock = "INSUFFICIENT_STOCK";
    public const string BusinessRuleViolation = "BUSINESS_RULE_VIOLATION";
    public const string InternalServerError = "INTERNAL_SERVER_ERROR";
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
}
