namespace Heimdall.Domain.Models;

public sealed class TokenValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyDictionary<string, string> Claims { get; init; } = new Dictionary<string, string>();
    public string? Error { get; init; }
}
