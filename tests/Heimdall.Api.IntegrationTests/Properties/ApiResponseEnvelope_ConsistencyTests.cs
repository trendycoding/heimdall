using FsCheck;
using FsCheck.Xunit;
using Heimdall.Api.Filters;
using Heimdall.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Heimdall.Api.IntegrationTests.Properties;

/// <summary>
/// Property 19: API Response Envelope Consistency
/// Generate various API responses (success/failure); assert envelope always contains
/// success, data, errors, correlationId with correct structure.
///
/// **Validates: Requirements 21.1, 21.2, 21.3, 21.4**
/// </summary>
public class ApiResponseEnvelope_ConsistencyTests
{
    private static readonly EnvelopeResultFilter Filter = new();

    /// <summary>
    /// Helper to create a ResultExecutingContext with an ObjectResult.
    /// </summary>
    private static ResultExecutingContext CreateObjectResultContext(object? value, int statusCode, string? correlationId = null)
    {
        var httpContext = new DefaultHttpContext();
        if (correlationId != null)
        {
            httpContext.Items["CorrelationId"] = correlationId;
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var objectResult = new ObjectResult(value) { StatusCode = statusCode };

        return new ResultExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            objectResult,
            controller: new object());
    }

    /// <summary>
    /// Helper to create a ResultExecutingContext with a StatusCodeResult.
    /// </summary>
    private static ResultExecutingContext CreateStatusCodeResultContext(int statusCode, string? correlationId = null)
    {
        var httpContext = new DefaultHttpContext();
        if (correlationId != null)
        {
            httpContext.Items["CorrelationId"] = correlationId;
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var statusCodeResult = new StatusCodeResult(statusCode);

        return new ResultExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            statusCodeResult,
            controller: new object());
    }

    /// <summary>
    /// Executes the filter and returns the resulting envelope object.
    /// </summary>
    private static async Task<ApiEnvelope<object>?> ExecuteFilterAsync(ResultExecutingContext context)
    {
        ApiEnvelope<object>? envelope = null;

        await Filter.OnResultExecutionAsync(context, () =>
        {
            // Extract the envelope from the result after filter execution
            if (context.Result is ObjectResult objResult && objResult.Value is ApiEnvelope<object> env)
            {
                envelope = env;
            }

            var executedContext = new ResultExecutedContext(
                context,
                new List<IFilterMetadata>(),
                context.Result,
                controller: new object());

            return Task.FromResult(executedContext);
        });

        return envelope;
    }

    [Property(MaxTest = 100)]
    public bool SuccessResponse_AlwaysContainsAllEnvelopeFields(NonEmptyString dataContent, byte statusCodeOffset)
    {
        // Generate success status codes: 200-299
        var statusCode = 200 + (statusCodeOffset % 100); // Range [200, 299]
        if (statusCode >= 300) statusCode = 200;

        return RunSuccessEnvelopeTestAsync(dataContent.Get, statusCode).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunSuccessEnvelopeTestAsync(string data, int statusCode)
    {
        var correlationId = Guid.NewGuid().ToString();
        var context = CreateObjectResultContext(data, statusCode, correlationId);

        var envelope = await ExecuteFilterAsync(context);

        if (envelope is null) return false;

        // Requirement 21.1: success (boolean) must be present and true
        if (envelope.Success != true) return false;

        // Requirement 21.1: data must contain the response payload
        if (envelope.Data is null) return false;
        if ((string)envelope.Data != data) return false;

        // Requirement 21.1: errors must be an empty array (not null)
        if (envelope.Errors is null) return false;
        if (envelope.Errors.Count != 0) return false;

        // Requirement 21.1: correlationId must be a non-empty string
        if (string.IsNullOrEmpty(envelope.CorrelationId)) return false;
        if (envelope.CorrelationId != correlationId) return false;

        return true;
    }

    [Property(MaxTest = 100)]
    public bool FailureResponse_AlwaysContainsAllEnvelopeFields(byte statusCodeOffset)
    {
        // Generate failure status codes: 400, 401, 403, 404, 409, 429, 500
        int[] errorCodes = [400, 401, 403, 404, 409, 429, 500];
        var statusCode = errorCodes[statusCodeOffset % errorCodes.Length];

        return RunFailureEnvelopeTestAsync(statusCode).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunFailureEnvelopeTestAsync(int statusCode)
    {
        var correlationId = Guid.NewGuid().ToString();
        var context = CreateObjectResultContext("Some error", statusCode, correlationId);

        var envelope = await ExecuteFilterAsync(context);

        if (envelope is null) return false;

        // Requirement 21.3: success must be false
        if (envelope.Success != false) return false;

        // Requirement 21.3: data must be null on failure
        if (envelope.Data != null) return false;

        // Requirement 21.3: errors must contain at least one error object
        if (envelope.Errors is null) return false;
        if (envelope.Errors.Count < 1) return false;

        // Each error must have a Code and Message
        foreach (var error in envelope.Errors)
        {
            if (string.IsNullOrEmpty(error.Code)) return false;
            if (string.IsNullOrEmpty(error.Message)) return false;
        }

        // Requirement 21.1: correlationId must be present
        if (string.IsNullOrEmpty(envelope.CorrelationId)) return false;
        if (envelope.CorrelationId != correlationId) return false;

        return true;
    }

    [Property(MaxTest = 100)]
    public bool StatusCodeResult_SuccessAlwaysWrappedInEnvelope(byte statusCodeOffset)
    {
        // Generate success status codes: 200-204
        int[] successCodes = [200, 201, 202, 203, 204];
        var statusCode = successCodes[statusCodeOffset % successCodes.Length];

        return RunStatusCodeResultSuccessTestAsync(statusCode).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunStatusCodeResultSuccessTestAsync(int statusCode)
    {
        var correlationId = Guid.NewGuid().ToString();
        var context = CreateStatusCodeResultContext(statusCode, correlationId);

        await Filter.OnResultExecutionAsync(context, () =>
        {
            var executedContext = new ResultExecutedContext(
                context,
                new List<IFilterMetadata>(),
                context.Result,
                controller: new object());
            return Task.FromResult(executedContext);
        });

        // The result should now be an ObjectResult containing an envelope
        if (context.Result is not ObjectResult objResult) return false;
        if (objResult.Value is not ApiEnvelope<object> envelope) return false;

        // Requirement 21.2: success must be true
        if (envelope.Success != true) return false;

        // Requirement 21.1: errors must be an empty array
        if (envelope.Errors is null) return false;
        if (envelope.Errors.Count != 0) return false;

        // Requirement 21.1: correlationId must be present
        if (string.IsNullOrEmpty(envelope.CorrelationId)) return false;
        if (envelope.CorrelationId != correlationId) return false;

        return true;
    }

    [Property(MaxTest = 100)]
    public bool StatusCodeResult_FailureAlwaysWrappedInEnvelope(byte statusCodeOffset)
    {
        // Generate failure status codes
        int[] errorCodes = [400, 401, 403, 404, 409, 429, 500];
        var statusCode = errorCodes[statusCodeOffset % errorCodes.Length];

        return RunStatusCodeResultFailureTestAsync(statusCode).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunStatusCodeResultFailureTestAsync(int statusCode)
    {
        var correlationId = Guid.NewGuid().ToString();
        var context = CreateStatusCodeResultContext(statusCode, correlationId);

        await Filter.OnResultExecutionAsync(context, () =>
        {
            var executedContext = new ResultExecutedContext(
                context,
                new List<IFilterMetadata>(),
                context.Result,
                controller: new object());
            return Task.FromResult(executedContext);
        });

        // The result should now be an ObjectResult containing an envelope
        if (context.Result is not ObjectResult objResult) return false;
        if (objResult.Value is not ApiEnvelope<object> envelope) return false;

        // Requirement 21.3: success must be false
        if (envelope.Success != false) return false;

        // Requirement 21.3: data must be null
        if (envelope.Data != null) return false;

        // Requirement 21.3: errors must have at least one error
        if (envelope.Errors is null) return false;
        if (envelope.Errors.Count < 1) return false;

        // Each error must have Code and Message
        foreach (var error in envelope.Errors)
        {
            if (string.IsNullOrEmpty(error.Code)) return false;
            if (string.IsNullOrEmpty(error.Message)) return false;
        }

        // Requirement 21.1: correlationId must be present
        if (string.IsNullOrEmpty(envelope.CorrelationId)) return false;
        if (envelope.CorrelationId != correlationId) return false;

        return true;
    }

    [Property(MaxTest = 100)]
    public bool CorrelationId_AlwaysPresentEvenWithoutHttpContextItem(NonEmptyString dataContent)
    {
        // When no CorrelationId is in HttpContext, the filter should generate one
        return RunMissingCorrelationIdTestAsync(dataContent.Get).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunMissingCorrelationIdTestAsync(string data)
    {
        // No correlationId set in HttpContext.Items
        var context = CreateObjectResultContext(data, 200, correlationId: null);

        var envelope = await ExecuteFilterAsync(context);

        if (envelope is null) return false;

        // Even without a CorrelationId in HttpContext, the envelope must still contain one
        if (string.IsNullOrEmpty(envelope.CorrelationId)) return false;

        // The generated correlation ID should be a valid GUID format
        return Guid.TryParse(envelope.CorrelationId, out _);
    }

    [Property(MaxTest = 100)]
    public bool FailureResponse_ErrorCodeMapsToStatusCode(byte statusCodeOffset)
    {
        // Verify the error code matches the expected machine-readable code per Requirement 21.3
        int[] errorCodes = [400, 401, 403, 404, 409, 429, 500];
        var statusCode = errorCodes[statusCodeOffset % errorCodes.Length];

        return RunErrorCodeMappingTestAsync(statusCode).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunErrorCodeMappingTestAsync(int statusCode)
    {
        var correlationId = Guid.NewGuid().ToString();
        var context = CreateObjectResultContext("Error detail", statusCode, correlationId);

        var envelope = await ExecuteFilterAsync(context);

        if (envelope is null) return false;
        if (envelope.Errors is null || envelope.Errors.Count == 0) return false;

        var expectedCode = statusCode switch
        {
            400 => "ValidationFailed",
            401 => "Unauthenticated",
            403 => "PermissionDenied",
            404 => "ResourceNotFound",
            409 => "ResourceConflict",
            429 => "RateLimitExceeded",
            _ => "InternalError"
        };

        return envelope.Errors[0].Code == expectedCode;
    }

    [Property(MaxTest = 100)]
    public bool SuccessResponse_NullDataPayload_StillConsistentEnvelope(byte statusCodeOffset)
    {
        // Test with null data payload - envelope structure must remain consistent
        int[] successCodes = [200, 201, 202, 204];
        var statusCode = successCodes[statusCodeOffset % successCodes.Length];

        return RunNullDataSuccessTestAsync(statusCode).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunNullDataSuccessTestAsync(int statusCode)
    {
        var correlationId = Guid.NewGuid().ToString();
        var context = CreateObjectResultContext(null, statusCode, correlationId);

        var envelope = await ExecuteFilterAsync(context);

        if (envelope is null) return false;

        // Requirement 21.1: success must be true for success codes
        if (envelope.Success != true) return false;

        // data can be null for success responses (e.g., 204 No Content)
        // but the field must still be present in the envelope (it's nullable by design)

        // Requirement 21.1: errors must be empty array
        if (envelope.Errors is null) return false;
        if (envelope.Errors.Count != 0) return false;

        // Requirement 21.1: correlationId must be present
        if (string.IsNullOrEmpty(envelope.CorrelationId)) return false;
        if (envelope.CorrelationId != correlationId) return false;

        return true;
    }

    [Property(MaxTest = 100)]
    public bool ProblemDetails_FailureWrappedCorrectly(NonEmptyString title, NonEmptyString detail, byte statusCodeOffset)
    {
        // Test with ProblemDetails object (common in ASP.NET Core error responses)
        int[] errorCodes = [400, 401, 403, 404, 409, 500];
        var statusCode = errorCodes[statusCodeOffset % errorCodes.Length];

        return RunProblemDetailsTestAsync(title.Get, detail.Get, statusCode).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunProblemDetailsTestAsync(string title, string detail, int statusCode)
    {
        var correlationId = Guid.NewGuid().ToString();
        var problemDetails = new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = statusCode
        };

        var context = CreateObjectResultContext(problemDetails, statusCode, correlationId);

        var envelope = await ExecuteFilterAsync(context);

        if (envelope is null) return false;

        // Requirement 21.3: success must be false
        if (envelope.Success != false) return false;

        // Requirement 21.3: data must be null
        if (envelope.Data != null) return false;

        // Requirement 21.3: errors must contain at least one error with Code and Message
        if (envelope.Errors is null) return false;
        if (envelope.Errors.Count < 1) return false;

        var error = envelope.Errors[0];
        if (string.IsNullOrEmpty(error.Code)) return false;
        if (string.IsNullOrEmpty(error.Message)) return false;

        // The message should contain the ProblemDetails detail (preferred) or title
        if (error.Message != detail && error.Message != title) return false;

        // Requirement 21.1: correlationId must be present
        if (string.IsNullOrEmpty(envelope.CorrelationId)) return false;
        if (envelope.CorrelationId != correlationId) return false;

        return true;
    }
}
