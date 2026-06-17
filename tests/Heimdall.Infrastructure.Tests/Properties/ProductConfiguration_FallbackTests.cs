using FsCheck;
using FsCheck.Xunit;
using Heimdall.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace Heimdall.Infrastructure.Tests.Properties;

/// <summary>
/// Property 24: Product Name Configuration Fallback
/// Generate various configuration values (null, empty, >100 chars, valid);
/// assert correct fallback behavior.
///
/// **Validates: Requirements 28.1, 28.4**
/// </summary>
public class ProductConfiguration_FallbackTests
{
    private const string DefaultFallback = "Heimdall Access";
    private const string ConfigKey = "Platform:DisplayName";

    private static ProductConfiguration CreateWithValue(string? value)
    {
        var configData = new Dictionary<string, string?>();
        if (value != null)
        {
            configData[ConfigKey] = value;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        return new ProductConfiguration(configuration);
    }

    [Property(MaxTest = 100)]
    public bool NullConfigValue_ReturnsFallback()
    {
        // When the Platform:DisplayName key is not set (null), should return fallback
        var config = CreateWithValue(null);
        return config.GetDisplayName() == DefaultFallback;
    }

    [Property(MaxTest = 100)]
    public bool EmptyString_ReturnsFallback()
    {
        var config = CreateWithValue("");
        return config.GetDisplayName() == DefaultFallback;
    }

    [Property(MaxTest = 100)]
    public bool WhitespaceOnly_ReturnsFallback(PositiveInt spaceCountRaw)
    {
        var spaceCount = (spaceCountRaw.Get % 20) + 1;
        var whitespace = new string(' ', spaceCount);

        var config = CreateWithValue(whitespace);
        return config.GetDisplayName() == DefaultFallback;
    }

    [Property(MaxTest = 100)]
    public bool StringExceeding100Chars_ReturnsFallback(PositiveInt extraCharsRaw)
    {
        var extraChars = (extraCharsRaw.Get % 200) + 1;
        var longName = new string('A', 100 + extraChars); // Exceeds 100 char limit

        var config = CreateWithValue(longName);
        return config.GetDisplayName() == DefaultFallback;
    }

    [Property(MaxTest = 100)]
    public bool StringExactly100Chars_ReturnsConfiguredValue()
    {
        var name = new string('B', 100); // Exactly at the limit

        var config = CreateWithValue(name);
        return config.GetDisplayName() == name;
    }

    [Property(MaxTest = 100)]
    public bool ValidNonEmptyStringUnder100Chars_ReturnsConfiguredValue(NonEmptyString nameRaw)
    {
        var name = nameRaw.Get;

        // Only test strings that are valid (non-whitespace, <= 100 chars)
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return true; // Skip this case — tested separately

        var config = CreateWithValue(name);
        return config.GetDisplayName() == name;
    }

    [Property(MaxTest = 100)]
    public bool ValidShortNames_ReturnConfiguredValue(PositiveInt lengthRaw)
    {
        var length = (lengthRaw.Get % 100) + 1; // 1-100 chars
        var name = new string('X', length);

        var config = CreateWithValue(name);
        return config.GetDisplayName() == name;
    }

    [Property(MaxTest = 100)]
    public bool StringAt101Chars_ReturnsFallback()
    {
        var name = new string('C', 101); // Just over the limit

        var config = CreateWithValue(name);
        return config.GetDisplayName() == DefaultFallback;
    }

    [Property(MaxTest = 100)]
    public bool MixedWhitespaceStrings_ReturnsFallback()
    {
        // Tab characters, newlines, etc. should be treated as whitespace
        var whitespaceVariants = new[] { "\t", "\n", "\r", "\t\n\r", "  \t  " };

        foreach (var ws in whitespaceVariants)
        {
            var config = CreateWithValue(ws);
            if (config.GetDisplayName() != DefaultFallback)
                return false;
        }

        return true;
    }

    [Property(MaxTest = 100)]
    public bool FallbackValue_IsAlwaysHeimdallAccess(PositiveInt extraCharsRaw)
    {
        // Verify the fallback is specifically "Heimdall Access" (not any other default)
        var longName = new string('Z', 101 + (extraCharsRaw.Get % 200));

        var config = CreateWithValue(longName);
        var result = config.GetDisplayName();

        return result == "Heimdall Access";
    }
}
