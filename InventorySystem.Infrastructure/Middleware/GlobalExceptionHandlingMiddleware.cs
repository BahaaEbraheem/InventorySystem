// File: Infrastructure/Middleware/GlobalExceptionHandlingMiddleware.cs
using Azure;
using InventorySystem.Domain.Exceptions;
using InventorySystem.Shared.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using ApplicationException = InventorySystem.Domain.Exceptions.ApplicationException;
using ResponseError = InventorySystem.Shared.Responses.ResponseError;
using ValidationException = InventorySystem.Domain.Exceptions.ValidationException;

namespace InventorySystem.Infrastructure.Middleware;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // تسجيل الخطأ دائماً
        _logger.LogError(exception,
            "Unhandled exception | Path: {Path} | Method: {Method} | TraceId: {TraceId}",
            context.Request.Path,
            context.Request.Method,
            context.TraceIdentifier);

        // إعداد الاستجابة
        context.Response.ContentType = "application/json";

        var traceId = context.TraceIdentifier;
        var meta = new ResponseMeta(traceId, _env.EnvironmentName);

        BaseResponse<object> response;

        switch (exception)
        {
            // ✅ Custom application exceptions (most specific first)
            case ValidationException validationEx:
                response = BaseResponse<object>.ErrorResponse(
                    validationEx.ErrorCode,
                    validationEx.Message,
                    validationEx.Errors,
                    meta);
                context.Response.StatusCode = validationEx.StatusCode;
                break;

            case NotFoundException notFoundEx:
                response = BaseResponse<object>.NotFound(notFoundEx.Message, meta);
                context.Response.StatusCode = notFoundEx.StatusCode;
                break;

            case InsufficientStockException stockEx:
                response = BaseResponse<object>.ErrorResponse(
                    stockEx.ErrorCode,
                    stockEx.Message,
                    new List<ResponseError> { new("stock", stockEx.ErrorCode, stockEx.Message) },
                    meta);
                context.Response.StatusCode = stockEx.StatusCode;
                break;

            case ConcurrencyException concurrencyEx:
                response = BaseResponse<object>.ErrorResponse(
                    concurrencyEx.ErrorCode,
                    concurrencyEx.Message,
                    new List<ResponseError> { new("concurrency", concurrencyEx.ErrorCode, concurrencyEx.Message) },
                    meta);
                context.Response.StatusCode = concurrencyEx.StatusCode;
                break;

            case ApplicationException appEx:
                response = BaseResponse<object>.ErrorResponse(
                    appEx.ErrorCode,
                    _env.IsDevelopment() ? appEx.Message : "Operation failed",
                    new List<ResponseError>
                    {
                new("business", appEx.ErrorCode, _env.IsDevelopment() ? appEx.Message : "Business rule violation")
                    },
                    meta);
                context.Response.StatusCode = appEx.StatusCode;
                break;

            // ✅ Standard .NET exceptions - ORDER MATTERS! (Specific → General)

            // 1️⃣ ArgumentNullException (inherits from ArgumentException) - MUST BE FIRST
            case ArgumentNullException nullEx:
                response = BaseResponse<object>.ErrorResponse(
                    ResponseCodes.ValidationError,
                    nullEx.Message,
                    new List<ResponseError>
                    {
                new(nullEx.ParamName ?? "parameter", ResponseCodes.ValidationError, "Required parameter is missing")
                    },
                    meta);
                context.Response.StatusCode = 400;
                break;

            // 2️⃣ ArgumentException (more general) - AFTER ArgumentNullException
            case ArgumentException argEx:
                response = BaseResponse<object>.ErrorResponse(
                    ResponseCodes.ValidationError,
                    argEx.Message,
                    new List<ResponseError>
                    {
                new(argEx.ParamName ?? "parameter", ResponseCodes.ValidationError,
                    _env.IsDevelopment() ? argEx.Message : "Invalid request parameter")
                    },
                    meta);
                context.Response.StatusCode = 400;
                break;

            // 3️⃣ InvalidOperationException (unrelated hierarchy)
            case InvalidOperationException invalidOpEx:
                response = BaseResponse<object>.ErrorResponse(
                    ResponseCodes.Conflict,
                    invalidOpEx.Message,
                    new List<ResponseError>
                    {
                new("operation", ResponseCodes.Conflict,
                    _env.IsDevelopment() ? invalidOpEx.Message : "Operation cannot be completed")
                    },
                    meta);
                context.Response.StatusCode = 409;
                break;

            // ❌ Default: Catch-all for unexpected errors (MUST BE LAST)
            default:
                response = BaseResponse<object>.InternalError(
                    _env.IsDevelopment() ? exception.Message : "An unexpected error occurred. Please contact support.",
                    meta);
                context.Response.StatusCode = 500;

                if (_env.IsDevelopment())
                {
                    response.Errors.Add(new ResponseError(
                        "exception",
                        exception.GetType().Name,
                        exception.ToString()));
                }
                break;
        }

        // تحديث البيئة في الـ Meta إذا لزم
        if (!response.Success && response.Meta != null)
        {
            response.Meta.Environment = _env.EnvironmentName;
        }

        // كتابة الاستجابة
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _env.IsDevelopment()
        };

        var json = JsonSerializer.Serialize(response, options);
        await context.Response.WriteAsync(json);
    }
}