using System.Diagnostics.Metrics;
using Heimdall.Infrastructure.Logging;
using Xunit;

namespace Heimdall.Infrastructure.Tests.Logging;

public class HeimdallMetricsServiceTests : IDisposable
{
    private readonly IMeterFactory _meterFactory;
    private readonly HeimdallMetricsService _metrics;
    private readonly MeterListener _listener;
    private readonly List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)> _recordings = new();

    public HeimdallMetricsServiceTests()
    {
        _meterFactory = new TestMeterFactory();
        _metrics = new HeimdallMetricsService(_meterFactory);

        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == HeimdallMetricsService.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
        {
            _recordings.Add((instrument.Name, value, tags.ToArray()));
        });
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) =>
        {
            _recordings.Add((instrument.Name, value, tags.ToArray()));
        });
        _listener.Start();
    }

    [Fact]
    public void RecordPermissionCheck_IncrementsTotal()
    {
        // Act
        _metrics.RecordPermissionCheck(allowed: true, durationMs: 15.0);
        _listener.RecordObservableInstruments();

        // Assert
        Assert.Contains(_recordings, r => r.Name == "heimdall.permission_checks.total");
    }

    [Fact]
    public void RecordPermissionCheck_WhenDenied_IncrementsDeniedCounter()
    {
        // Act
        _metrics.RecordPermissionCheck(allowed: false, durationMs: 25.0);
        _listener.RecordObservableInstruments();

        // Assert
        Assert.Contains(_recordings, r => r.Name == "heimdall.permission_checks.denied");
    }

    [Fact]
    public void RecordPermissionCheck_RecordsDuration()
    {
        // Act
        _metrics.RecordPermissionCheck(allowed: true, durationMs: 42.5);
        _listener.RecordObservableInstruments();

        // Assert
        Assert.Contains(_recordings, r =>
            r.Name == "heimdall.permission_checks.duration" && (double)r.Value == 42.5);
    }

    [Fact]
    public void RecordCacheAccess_Hit_IncrementsCacheHits()
    {
        // Act
        _metrics.RecordCacheAccess(hit: true);
        _listener.RecordObservableInstruments();

        // Assert
        Assert.Contains(_recordings, r => r.Name == "heimdall.cache.hits");
        Assert.DoesNotContain(_recordings, r => r.Name == "heimdall.cache.misses");
    }

    [Fact]
    public void RecordCacheAccess_Miss_IncrementsCacheMisses()
    {
        // Act
        _metrics.RecordCacheAccess(hit: false);
        _listener.RecordObservableInstruments();

        // Assert
        Assert.Contains(_recordings, r => r.Name == "heimdall.cache.misses");
        Assert.DoesNotContain(_recordings, r => r.Name == "heimdall.cache.hits");
    }

    [Fact]
    public void RecordRequestDuration_RecordsWithTags()
    {
        // Act
        _metrics.RecordRequestDuration("/api/tenants", "GET", 200, 55.2);
        _listener.RecordObservableInstruments();

        // Assert
        var recording = _recordings.FirstOrDefault(r => r.Name == "heimdall.http.request_duration");
        Assert.NotEqual(default, recording);
        Assert.Equal(55.2, (double)recording.Value);
        Assert.Contains(recording.Tags, t => t.Key == "endpoint" && (string?)t.Value == "/api/tenants");
        Assert.Contains(recording.Tags, t => t.Key == "method" && (string?)t.Value == "GET");
        Assert.Contains(recording.Tags, t => t.Key == "status_code" && (int?)t.Value == 200);
    }

    [Fact]
    public void RecordDependencyDuration_RecordsWithTags()
    {
        // Act
        _metrics.RecordDependencyDuration("SQL", "HeimdallDb", true, 12.3);
        _listener.RecordObservableInstruments();

        // Assert
        var recording = _recordings.FirstOrDefault(r => r.Name == "heimdall.dependencies.duration");
        Assert.NotEqual(default, recording);
        Assert.Equal(12.3, (double)recording.Value);
        Assert.Contains(recording.Tags, t => t.Key == "dependency_type" && (string?)t.Value == "SQL");
        Assert.Contains(recording.Tags, t => t.Key == "target" && (string?)t.Value == "HeimdallDb");
        Assert.Contains(recording.Tags, t => t.Key == "success" && (bool?)t.Value == true);
    }

    [Fact]
    public void RecordUnhandledException_RecordsWithTags()
    {
        // Act
        _metrics.RecordUnhandledException("NullReferenceException", "GET /api/users");
        _listener.RecordObservableInstruments();

        // Assert
        var recording = _recordings.FirstOrDefault(r => r.Name == "heimdall.exceptions.unhandled");
        Assert.NotEqual(default, recording);
        Assert.Contains(recording.Tags, t => t.Key == "exception_type" && (string?)t.Value == "NullReferenceException");
        Assert.Contains(recording.Tags, t => t.Key == "source" && (string?)t.Value == "GET /api/users");
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = new();

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options.Name, options.Version);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (var meter in _meters)
                meter.Dispose();
            _meters.Clear();
        }
    }
}
