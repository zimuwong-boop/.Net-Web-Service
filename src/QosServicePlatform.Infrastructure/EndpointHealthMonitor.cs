using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using QosServicePlatform.Application;
using QosServicePlatform.Domain;

namespace QosServicePlatform.Infrastructure;

public sealed class EndpointHealthMonitor(
    IServiceRegistry registry,
    IHttpClientFactory clientFactory) : BackgroundService, IEndpointHealthMonitor
{
    private readonly ConcurrentDictionary<Guid, EndpointHealthStatus> _statuses = new();

    public bool IsHealthy(ServiceEndpoint endpoint) =>
        !_statuses.TryGetValue(endpoint.Id, out var status) || status.IsHealthy;

    public IReadOnlyList<EndpointHealthStatus> GetStatuses() =>
        registry.GetAll().Select(endpoint => _statuses.GetValueOrDefault(endpoint.Id)
            ?? new EndpointHealthStatus(endpoint.Id, endpoint.Name, true, DateTimeOffset.MinValue)).ToArray();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckAllAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task CheckAllAsync(CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient("health-checks");
        foreach (var group in registry.GetAll().GroupBy(x => x.Address.GetLeftPart(UriPartial.Authority)))
        {
            var healthy = false;
            try
            {
                using var response = await client.GetAsync($"{group.Key}/health", cancellationToken);
                healthy = response.IsSuccessStatusCode;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }

            var checkedAt = DateTimeOffset.Now;
            foreach (var endpoint in group)
                _statuses[endpoint.Id] = new EndpointHealthStatus(endpoint.Id, endpoint.Name, healthy, checkedAt);
        }
    }
}
