using FluentValidation;
using Heimdall.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Heimdall.Api.Filters;

/// <summary>
/// Exception filter that catches <see cref="ValidationException"/> thrown by FluentValidation
/// and converts them into a 400 Bad Request response with the standard API envelope containing
/// field-level error details.
/// </summary>
public sealed class ValidationExceptionFilter : IAsyncExceptionFilter
{
    private const string CorrelationIdKey = "CorrelationId";

    public Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not ValidationException validationException)
        {
            return Task.CompletedTask;
        }

        var correlationId = GetCorrelationId(context.HttpContext);

        var errors = validationException.Errors
            .Select(failure => new ApiError
            {
                Code = "ValidationFailed",
                Message = failure.ErrorMessage,
                Field = string.IsNullOrWhiteSpace(failure.PropertyName) ? null : failure.PropertyName
            })
            .ToList();

        var envelope = new ApiEnvelope<object>
        {
            Success = false,
            Data = null,
            Errors = errors,
            CorrelationId = correlationId
        };

        context.Result = new ObjectResult(envelope)
        {
            StatusCode = StatusCodes.Status400BadRequest
        };

        context.ExceptionHandled = true;

        return Task.CompletedTask;
    }

    private static string GetCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(CorrelationIdKey, out var value) && value is string correlationId)
        {
            return correlationId;
        }

        return Guid.NewGuid().ToString();
    }
}
