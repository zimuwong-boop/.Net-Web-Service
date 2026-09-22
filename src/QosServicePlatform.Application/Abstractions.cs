using QosServicePlatform.Domain;

namespace QosServicePlatform.Application;

public interface IServiceRegistry
{
    IReadOnlyList<ServiceEndpoint> GetCandidates(string categoryId);
    IReadOnlyList<ServiceEndpoint> GetAll();
}

public interface IQosMetricsStore
{
    QosSnapshot GetSnapshot(ServiceEndpoint endpoint);
    void Record(Guid endpointId, bool success, TimeSpan duration);
}

public interface IQosScorer
{
    IReadOnlyList<ServiceScore> Rank(
        IReadOnlyList<ServiceEndpoint> endpoints,
        IReadOnlyDictionary<Guid, QosSnapshot> snapshots,
        QosWeights weights);
}

public interface IServiceInvoker
{
    Task<string> InvokeAsync(ServiceEndpoint endpoint, ServiceTask task, CancellationToken cancellationToken);
}

public interface ICallHistoryStore
{
    void Add(CompositeResult result);
    IReadOnlyList<CallHistoryEntry> GetRecent(int count = 20);
}

public interface ICircuitBreaker
{
    bool CanExecute(ServiceEndpoint endpoint);
    void RecordSuccess(ServiceEndpoint endpoint);
    void RecordFailure(ServiceEndpoint endpoint);
    IReadOnlyList<CircuitStatus> GetStatuses();
}

public interface IEndpointHealthMonitor
{
    bool IsHealthy(ServiceEndpoint endpoint);
    IReadOnlyList<EndpointHealthStatus> GetStatuses();
}

public interface ICompositeOrchestrator
{
    Task<CompositeResult> ExecuteAsync(CompositeRequest request, CancellationToken cancellationToken = default);
}
