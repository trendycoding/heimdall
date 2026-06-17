namespace Heimdall.Api.Models;

/// <summary>
/// Standard API response envelope wrapping all responses in a consistent format.
/// </summary>
/// <typeparam name="T">The type of the response data payload.</typeparam>
public class ApiEnvelope<T>
{
    /// <summary>
    /// Indicates whether the request was processed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The response payload. Null when the request fails.
    /// </summary>
    public T? Data { get; init; }

    /// <summary>
    /// Array of error objects. Empty when the request succeeds.
    /// </summary>
    public List<ApiError>? Errors { get; init; }

    /// <summary>
    /// Unique correlation identifier matching the ApiCallLog CorrelationId for this request.
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Creates a successful envelope with the given data payload.
    /// </summary>
    public static ApiEnvelope<T> Ok(T data, string correlationId) => new()
    {
        Success = true,
        Data = data,
        Errors = [],
        CorrelationId = correlationId
    };

    /// <summary>
    /// Creates a failure envelope with the given errors.
    /// </summary>
    public static ApiEnvelope<T> Fail(List<ApiError> errors, string correlationId) => new()
    {
        Success = false,
        Data = default,
        Errors = errors,
        CorrelationId = correlationId
    };
}

/// <summary>
/// Represents a single error in an API response.
/// </summary>
public class ApiError
{
    /// <summary>
    /// Machine-readable error code (e.g., "ValidationFailed", "TenantNotFound", "PermissionDenied", "ResourceConflict").
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable description of the error.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// The field that caused the error, if applicable.
    /// </summary>
    public string? Field { get; init; }
}
