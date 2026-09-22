using System.Collections.Concurrent;
using QosServicePlatform.Application;
using QosServicePlatform.Domain;

namespace QosServicePlatform.UnitTests;

public sealed class CompositeOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenBestFails_FallsBackToSecondCandidate()
    {
        var first = Endpoint("first", 0.01m);
        var second = Endpoint("second", 0.02m);
        var registry = new FakeRegistry([first, second]);
        var metrics = new FakeMetrics();
        var invoker = new FakeInvoker(first.Id);
        var sut = new CompositeOrchestrator(registry, metrics, new QosScorer(), invoker,
            new FakeCircuitBreaker(), new FakeHealthMonitor(), new FakeHistory());

        var result = await sut.ExecuteAsync(new CompositeRequest(
            Guid.NewGuid(),
            [new ServiceTask(ServiceCategories.Weather, new Dictionary<string, string>())],
            new QosWeights(0, 0, 0, 1, 0)));

        Assert.True(result.Results[0].Success);
        Assert.Equal(second.Id, result.Results[0].EndpointId);
        Assert.Equal(2, result.Results[0].Attempts.Count);
        Assert.Equal(2, metrics.Records.Count);
    }

    private static ServiceEndpoint Endpoint(string name, decimal cost) =>
        new(Guid.NewGuid(), ServiceCategories.Weather, name, new Uri($"http://localhost/{name}"), cost);

    private sealed class FakeRegistry(IReadOnlyList<ServiceEndpoint> endpoints) : IServiceRegistry
    {
        public IReadOnlyList<ServiceEndpoint> GetCandidates(string categoryId) => endpoints;
        public IReadOnlyList<ServiceEndpoint> GetAll() => endpoints;
    }

    private sealed class FakeMetrics : IQosMetricsStore
    {
        public ConcurrentBag<(Guid Id, bool Success)> Records { get; } = [];
        public QosSnapshot GetSnapshot(ServiceEndpoint endpoint) =>
            new(endpoint.Id, 0.99, 0.99, 100, 0.5, DateTimeOffset.UtcNow);
        public void Record(Guid endpointId, bool success, TimeSpan duration) => Records.Add((endpointId, success));
    }

    private sealed class FakeInvoker(Guid failingId) : IServiceInvoker
    {
        public Task<string> InvokeAsync(ServiceEndpoint endpoint, ServiceTask task, CancellationToken cancellationToken) =>
            endpoint.Id == failingId
                ? throw new HttpRequestException("planned failure")
                : Task.FromResult("ok");
    }

    private sealed class FakeCircuitBreaker : ICircuitBreaker
    {
        public bool CanExecute(ServiceEndpoint endpoint) => true;
        public void RecordSuccess(ServiceEndpoint endpoint) { }
        public void RecordFailure(ServiceEndpoint endpoint) { }
        public IReadOnlyList<CircuitStatus> GetStatuses() => [];
    }

    private sealed class FakeHistory : ICallHistoryStore
    {
        public void Add(CompositeResult result) { }
        public IReadOnlyList<CallHistoryEntry> GetRecent(int count = 20) => [];
    }

    private sealed class FakeHealthMonitor : IEndpointHealthMonitor
    {
        public bool IsHealthy(ServiceEndpoint endpoint) => true;
        public IReadOnlyList<EndpointHealthStatus> GetStatuses() => [];
    }
}
