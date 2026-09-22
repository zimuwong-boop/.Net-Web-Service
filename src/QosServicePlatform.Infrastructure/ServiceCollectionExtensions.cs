using Microsoft.Extensions.DependencyInjection;
using QosServicePlatform.Application;

namespace QosServicePlatform.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQosServicePlatform(this IServiceCollection services)
    {
        services.AddSingleton<IServiceRegistry, InMemoryServiceRegistry>();
        services.AddSingleton<IQosMetricsStore, InMemoryQosMetricsStore>();
        services.AddSingleton<IQosScorer, QosScorer>();
        services.AddSingleton<ICircuitBreaker, InMemoryCircuitBreaker>();
        services.AddSingleton<ICallHistoryStore, JsonCallHistoryStore>();
        services.AddSingleton<EndpointHealthMonitor>();
        services.AddSingleton<IEndpointHealthMonitor>(provider => provider.GetRequiredService<EndpointHealthMonitor>());
        services.AddHostedService(provider => provider.GetRequiredService<EndpointHealthMonitor>());
        services.AddSingleton<ICompositeOrchestrator, CompositeOrchestrator>();
        services.AddTransient<IServiceInvoker, HttpServiceInvoker>();
        services.AddHttpClient("dynamic-services", client => client.Timeout = TimeSpan.FromSeconds(2));
        services.AddHttpClient("health-checks", client => client.Timeout = TimeSpan.FromSeconds(2));
        return services;
    }
}
