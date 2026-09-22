using System.Collections.Concurrent;
using QosServicePlatform.Application;
using QosServicePlatform.Domain;

namespace QosServicePlatform.Infrastructure;

public sealed class InMemoryQosMetricsStore : IQosMetricsStore
{
    private sealed record State(long Calls, long Successes, double AverageMs);
    private readonly ConcurrentDictionary<Guid, State> _states = new();

    public QosSnapshot GetSnapshot(ServiceEndpoint endpoint)
    {
        var baseline = Baseline(endpoint.Address.AbsolutePath);
        var state = _states.GetOrAdd(endpoint.Id, _ => new State(0, 0, baseline.ResponseMs));
        var successRate = state.Calls == 0 ? baseline.SuccessRate : (double)state.Successes / state.Calls;
        return new QosSnapshot(endpoint.Id, baseline.Availability, successRate, state.AverageMs,
            baseline.Load, DateTimeOffset.UtcNow);
    }

    public void Record(Guid endpointId, bool success, TimeSpan duration)
    {
        _states.AddOrUpdate(endpointId,
            _ => new State(1, success ? 1 : 0, duration.TotalMilliseconds),
            (_, old) => new State(old.Calls + 1, old.Successes + (success ? 1 : 0),
                (old.AverageMs * old.Calls + duration.TotalMilliseconds) / (old.Calls + 1)));
    }

    private static (double Availability, double SuccessRate, double ResponseMs, double Load) Baseline(string path)
    {
        if (path.Contains("reliable")) return (0.999, 0.995, 220, 0.30);
        if (path.Contains("legacy-weather")) return (0.985, 0.980, 260, 0.35);
        if (path.Contains("fast")) return (0.970, 0.960, 80, 0.75);
        if (path.Contains("economy")) return (0.940, 0.930, 350, 0.25);
        return (0.990, 0.985, 160, 0.45);
    }
}
