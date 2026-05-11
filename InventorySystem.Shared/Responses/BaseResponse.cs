// File: InventorySystem.Shared/Responses/BaseResponse.cs
using System;
using System.Collections.Generic;

namespace InventorySystem.Shared.Responses;

public class BaseResponse<T>
{
    public bool Success { get; set; }
    public string Code { get; set; } = ResponseCodes.Success;
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<ResponseError> Errors { get; set; } = new();
    public ResponseMeta? Meta { get; set; }

    // ✅ Success factory
    public static BaseResponse<T> SuccessResponse(
        T data,
        string message,
        ResponseMeta? meta = null)
    {
        return new BaseResponse<T>
        {
            Success = true,
            Code = ResponseCodes.Success,
            Message = message,
            Data = data,
            Errors = new List<ResponseError>(),
            Meta = meta
        };
    }

    // ✅ Error factory
    public static BaseResponse<T?> ErrorResponse(
        string code,
        string message,
        List<ResponseError>? errors = null,
        ResponseMeta? meta = null)
    {
        return new BaseResponse<T?>
        {
            Success = false,
            Code = code,
            Message = message,
            Data = default,
            Errors = errors ?? new List<ResponseError>(),
            Meta = meta
        };
    }

    // ✅ Convenience methods
    public static BaseResponse<T?> NotFound(string message, ResponseMeta? meta = null) =>
        ErrorResponse(ResponseCodes.NotFound, message, meta: meta);

    public static BaseResponse<T?> Unauthorized(string message = "Authentication required", ResponseMeta? meta = null) =>
        ErrorResponse(ResponseCodes.Unauthorized, message, meta: meta);

    public static BaseResponse<T?> InternalError(string message, ResponseMeta? meta = null) =>
        ErrorResponse(ResponseCodes.InternalServerError, message, meta: meta);
}