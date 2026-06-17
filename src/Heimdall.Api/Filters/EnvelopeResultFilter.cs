using Heimdall.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Heimdall.Api.Filters;

/// <summary>
/// Result filter that wraps all controller action results in the standard <see cref="ApiEnvelope{T}"/> format.
/// Pulls CorrelationId from HttpContext.Items (set by CorrelationIdMiddleware).
/// </summary>
public sealed class EnvelopeResultFilter : IAsyncResultFilter
{
    private const string CorrelationIdKey = "CorrelationId";

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var correlationId = GetCorrelationId(context.HttpContext);

        if (context.Result is ObjectResult objectResult && !IsAlreadyEnveloped(objectResult.Value))
        {
            var statusCode = objectResult.StatusCode ?? StatusCodes.Status200OK;

            if (statusCode >= 200 && statusCode < 300)
            {
                objectResult.Value = CreateSuccessEnvelope(objectResult.Value, correlationId);
            }
            else
            {
                objectResult.Value = CreateErrorEnvelope(objectResult.Value, statusCode, correlationId);
            }
        }
        else if (context.Result is StatusCodeResult statusCodeResult)
        {
            var statusCode = statusCodeResult.StatusCode;

            if (statusCode >= 200 && statusCode < 300)
            {
                context.Result = new ObjectResult(CreateSuccessEnvelope<object>(null, correlationId))
                {
                    StatusCode = statusCode
                };
            }
            else
            {
                context.Result = new ObjectResult(CreateErrorEnvelope(null, statusCode, correlationId))
                {
                    StatusCode = statusCode
                };
            }
        }

        await next();
    }

    private static string GetCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(CorrelationIdKey, out var value) && value is string correlationId)
        {
            return correlationId;
        }

        return Guid.NewGuid().ToString();
    }

    private static bool IsAlreadyEnveloped(object? value)
    {
        if (value is null) return false;

        var type = value.GetType();
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ApiEnvelope<>);
    }

    private static ApiEnvelope<object> CreateSuccessEnvelope<T>(T? data, string correlationId)
    {
        return new ApiEnvelope<object>
        {
            Success = true,
            Data = data,
            Errors = [],
            CorrelationId = correlationId
        };
    }

    private static ApiEnvelope<object> CreateErrorEnvelope(object? value, int statusCode, string correlationId)
    {
        var errors = ExtractErrors(value, statusCode);

        return new ApiEnvelope<object>
        {
            Success = false,
            Data = null,
            Errors = errors,
            CorrelationId = correlationId
        };
    }

    private static List<ApiError> ExtractErrors(object? value, int statusCode)
    {
        if (value is ProblemDetails problemDetails)
        {
            return
            [
                new ApiError
                {
                    Code = GetErrorCodeFromStatus(statusCode),
                    Message = problemDetails.Detail ?? problemDetails.Title ?? "An error occurred."
                }
            ];
        }

        if (value is string message)
        {
            return
            [
                new ApiError
                {
                    Code = GetErrorCodeFromStatus(statusCode),
                    Message = message
                }
            ];
        }

        return
        [
            new ApiError
            {
                Code = GetErrorCodeFromStatus(statusCode),
                Message = "An error occurred."
            }
        ];
    }

    private static string GetErrorCodeFromStatus(int statusCode) => statusCode switch
    {
        400 => "ValidationFailed",
        401 => "Unauthenticated",
        403 => "PermissionDenied",
        404 => "ResourceNotFound",
        409 => "ResourceConflict",
        429 => "RateLimitExceeded",
        _ => "InternalError"
    };
}
