using Heimdall.Infrastructure.Logging;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using NSubstitute;
using Xunit;

namespace Heimdall.Infrastructure.Tests.Logging;

public class SensitiveDataTelemetryProcessorTests
{
    private readonly ITelemetryProcessor _nextProcessor;
    private readonly SensitiveDataTelemetryProcessor _processor;

    public SensitiveDataTelemetryProcessorTests()
    {
        _nextProcessor = Substitute.For<ITelemetryProcessor>();
        _processor = new SensitiveDataTelemetryProcessor(_nextProcessor);
    }

    [Theory]
    [InlineData("secret")]
    [InlineData("password")]
    [InlineData("token")]
    [InlineData("apikey")]
    [InlineData("api_key")]
    [InlineData("connectionstring")]
    [InlineData("bearer")]
    [InlineData("authorization")]
    [InlineData("client_secret")]
    [InlineData("access_token")]
    [InlineData("refresh_token")]
    public void Process_RedactsPropertiesWithSensitiveKeys(string sensitiveKey)
    {
        // Arrange
        var trace = new TraceTelemetry("Test message");
        trace.Properties[sensitiveKey] = "super-secret-value-12345";
        trace.Properties["safe_property"] = "visible-value";

        // Act
        _processor.Process(trace);

        // Assert
        Assert.Equal("[REDACTED]", trace.Properties[sensitiveKey]);
        Assert.Equal("visible-value", trace.Properties["safe_property"]);
        _nextProcessor.Received(1).Process(trace);
    }

    [Theory]
    [InlineData("secret=mysecretvalue123", "secret=[REDACTED]")]
    [InlineData("password:hunter2", "password=[REDACTED]")]
    [InlineData("token=eyJhbGciOiJSUzI1NiJ9.test", "token=[REDACTED]")]
    [InlineData("api_key=sk-abc123def456", "api_key=[REDACTED]")]
    [InlineData("bearer=xyz-token-value", "bearer=[REDACTED]")]
    public void Process_RedactsSensitiveValuesInTraceMessages(string message, string expected)
    {
        // Arrange
        var trace = new TraceTelemetry(message);

        // Act
        _processor.Process(trace);

        // Assert
        Assert.Equal(expected, trace.Message);
        _nextProcessor.Received(1).Process(trace);
    }

    [Fact]
    public void Process_PreservesNonSensitiveMessages()
    {
        // Arrange
        var trace = new TraceTelemetry("User logged in from 192.168.1.1");

        // Act
        _processor.Process(trace);

        // Assert
        Assert.Equal("User logged in from 192.168.1.1", trace.Message);
        _nextProcessor.Received(1).Process(trace);
    }

    [Fact]
    public void Process_RedactsSensitiveValuesInExceptionMessages()
    {
        // Arrange
        var exception = new ExceptionTelemetry(new InvalidOperationException("Connection failed"))
        {
            Message = "Failed with credential=abc123 during auth"
        };

        // Act
        _processor.Process(exception);

        // Assert
        Assert.Equal("Failed with credential=[REDACTED] during auth", exception.Message);
        _nextProcessor.Received(1).Process(exception);
    }

    [Fact]
    public void Process_CaseInsensitiveKeyMatching()
    {
        // Arrange
        var trace = new TraceTelemetry("test");
        trace.Properties["Authorization"] = "Bearer eyJhbGciOi...";
        trace.Properties["API_KEY"] = "sk-test-key";

        // Act
        _processor.Process(trace);

        // Assert
        Assert.Equal("[REDACTED]", trace.Properties["Authorization"]);
        Assert.Equal("[REDACTED]", trace.Properties["API_KEY"]);
    }

    [Fact]
    public void Process_AlwaysCallsNextProcessor()
    {
        // Arrange
        var trace = new TraceTelemetry("Simple message");

        // Act
        _processor.Process(trace);

        // Assert
        _nextProcessor.Received(1).Process(trace);
    }
}
