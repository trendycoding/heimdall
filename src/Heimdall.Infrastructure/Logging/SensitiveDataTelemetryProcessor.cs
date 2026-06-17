using System.Text.RegularExpressions;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Heimdall.Infrastructure.Logging;

/// <summary>
/// Telemetry processor that redacts sensitive values (secrets, keys, tokens, passwords)
/// from all telemetry output to prevent accidental exposure in logs.
/// </summary>
public partial class SensitiveDataTelemetryProcessor : ITelemetryProcessor
{
    private readonly ITelemetryProcessor _next;

    // Patterns that indicate sensitive data in property keys (case-insensitive)
    private static readonly string[] SensitiveKeyPatterns =
    [
        "secret",
        "password",
        "passwd",
        "token",
        "apikey",
        "api_key",
        "api-key",
        "connectionstring",
        "connection_string",
        "connection-string",
        "credential",
        "private_key",
        "private-key",
        "privatekey",
        "client_secret",
        "client-secret",
        "clientsecret",
        "access_token",
        "access-token",
        "accesstoken",
        "refresh_token",
        "refresh-token",
        "refreshtoken",
        "bearer",
        "authorization"
    ];

    // Regex to detect inline secrets in message text (e.g., key=value patterns)
    [GeneratedRegex(
        @"(?i)(secret|password|passwd|token|apikey|api[_-]?key|credential|private[_-]?key|client[_-]?secret|access[_-]?token|refresh[_-]?token|bearer|authorization)\s*[=:]\s*[""']?([^\s""',;}{)]+)",
        RegexOptions.Compiled)]
    private static partial Regex SensitiveValuePattern();

    private const string RedactedValue = "[REDACTED]";

    public SensitiveDataTelemetryProcessor(ITelemetryProcessor next)
    {
        _next = next;
    }

    public void Process(ITelemetry item)
    {
        RedactProperties(item);
        RedactMessageContent(item);

        _next.Process(item);
    }

    private static void RedactProperties(ITelemetry item)
    {
        if (item is not ISupportProperties propertied)
            return;

        var keysToRedact = new List<string>();

        foreach (var kvp in propertied.Properties)
        {
            if (IsSensitiveKey(kvp.Key))
            {
                keysToRedact.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRedact)
        {
            propertied.Properties[key] = RedactedValue;
        }
    }

    private static void RedactMessageContent(ITelemetry item)
    {
        if (item is TraceTelemetry trace && !string.IsNullOrEmpty(trace.Message))
        {
            trace.Message = RedactSensitiveValues(trace.Message);
        }
        else if (item is ExceptionTelemetry exception && !string.IsNullOrEmpty(exception.Message))
        {
            exception.Message = RedactSensitiveValues(exception.Message);
        }
        else if (item is RequestTelemetry request)
        {
            if (!string.IsNullOrEmpty(request.Url?.Query))
            {
                var redactedUrl = RedactSensitiveValues(request.Url.ToString());
                if (Uri.TryCreate(redactedUrl, UriKind.Absolute, out var uri))
                {
                    request.Url = uri;
                }
            }
        }
    }

    private static string RedactSensitiveValues(string input)
    {
        return SensitiveValuePattern().Replace(input, match =>
            $"{match.Groups[1].Value}={RedactedValue}");
    }

    private static bool IsSensitiveKey(string key)
    {
        var lowerKey = key.ToLowerInvariant();
        foreach (var pattern in SensitiveKeyPatterns)
        {
            if (lowerKey.Contains(pattern, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
