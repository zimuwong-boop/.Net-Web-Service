using System.Collections.Concurrent;
using QosServicePlatform.Application;
using QosServicePlatform.Domain;

namespace QosServicePlatform.Infrastructure;

public sealed class InMemoryCircuitBreaker(IServiceRegistry registry) : ICircuitBreaker
{
    private sealed record State(int Failures, DateTimeOffset? OpenUntil);
    private readonly ConcurrentDictionary<Guid, State> _states = new();
    private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(20);

    public bool CanExecute(ServiceEndpoint endpoint)
    {
        if (!_states.TryGetValue(endpoint.Id, out var state) || state.OpenUntil is null) return true;
        if (state.OpenUntil > DateTimeOffset.UtcNow) return false;
        _states[endpoint.Id] = state with { OpenUntil = null };
        return true;
    }

    public void RecordSuccess(ServiceEndpoint endpoint) => _states[endpoint.Id] = new State(0, null);

    public void RecordFailure(ServiceEndpoint endpoint)
    {
        _states.AddOrUpdate(endpoint.Id,
            _ => new State(1, null),
            (_, old) =>
            {
                var failures = old.Failures + 1;
                return new State(failures, failures >= 2 ? DateTimeOffset.UtcNow.Add(BreakDuration) : null);
            });
    }

    public IReadOnlyList<CircuitStatus> GetStatuses()
    {
        var endpoints = registry.GetAll().ToDictionary(x => x.Id);
        return _states.Select(pair =>
        {
            var state = pair.Value;
            var isOpen = state.OpenUntil > DateTimeOffset.UtcNow;
            return new CircuitStatus(pair.Key, endpoints.GetValueOrDefault(pair.Key)?.Name ?? pair.Key.ToString(),
                state.Failures, state.OpenUntil, isOpen);
        }).OrderByDescending(x => x.IsOpen).ThenBy(x => x.EndpointName).ToArray();
    }
}

