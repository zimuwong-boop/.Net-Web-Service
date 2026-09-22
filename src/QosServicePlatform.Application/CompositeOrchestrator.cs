using System.Diagnostics;
using QosServicePlatform.Domain;

namespace QosServicePlatform.Application;

public sealed class CompositeOrchestrator(
    IServiceRegistry registry,
    IQosMetricsStore metrics,
    IQosScorer scorer,
    IServiceInvoker invoker,
    ICircuitBreaker circuitBreaker,
    IEndpointHealthMonitor healthMonitor,
    ICallHistoryStore history) : ICompositeOrchestrator
{
    public async Task<CompositeResult> ExecuteAsync(
        CompositeRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Weights.Validate();
        if (request.Tasks.Count is < 1 or > 4)
            throw new ArgumentException("一次请求必须包含 1 到 4 个服务类别。", nameof(request));
        if (request.Tasks.Select(x => x.CategoryId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Tasks.Count)
            throw new ArgumentException("一次请求中不能重复选择相同服务类别。", nameof(request));

        var timer = Stopwatch.StartNew();
        var tasks = request.Tasks.Select(task => ExecuteOneAsync(task, request.Weights, cancellationToken));
        var results = await Task.WhenAll(tasks);
        timer.Stop();
        var result = new CompositeResult(request.CorrelationId, results, timer.Elapsed);
        history.Add(result);
        return result;
    }

    private async Task<ServiceCallResult> ExecuteOneAsync(
        ServiceTask task,
        QosWeights weights,
        CancellationToken cancellationToken)
    {
        var endpoints = registry.GetCandidates(task.CategoryId)
            .Where(circuitBreaker.CanExecute)
            .Where(healthMonitor.IsHealthy)
            .ToArray();
        var snapshots = endpoints.ToDictionary(endpoint => endpoint.Id, metrics.GetSnapshot);
        var ranking = scorer.Rank(endpoints, snapshots, weights);
        var attempts = new List<Attempt>();

        foreach (var candidate in ranking)
        {
            var timer = Stopwatch.StartNew();
            try
            {
                var data = await invoker.InvokeAsync(candidate.Endpoint, task, cancellationToken);
                timer.Stop();
                metrics.Record(candidate.Endpoint.Id, true, timer.Elapsed);
                circuitBreaker.RecordSuccess(candidate.Endpoint);
                attempts.Add(new Attempt(candidate.Endpoint.Id, candidate.Endpoint.Name, candidate.Rank, timer.Elapsed, "成功", null));
                return new ServiceCallResult(task.CategoryId, candidate.Endpoint.Id, candidate.Endpoint.Name, true, data, ranking, attempts, null);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
            {
                timer.Stop();
                metrics.Record(candidate.Endpoint.Id, false, timer.Elapsed);
                circuitBreaker.RecordFailure(candidate.Endpoint);
                attempts.Add(new Attempt(candidate.Endpoint.Id, candidate.Endpoint.Name, candidate.Rank, timer.Elapsed, "失败", ex.GetType().Name));
            }
        }

        return new ServiceCallResult(task.CategoryId, null, null, false, null, ranking, attempts, "所有候选服务均不可用。");
    }
}
