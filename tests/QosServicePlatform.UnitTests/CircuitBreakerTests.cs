using QosServicePlatform.Domain;
using QosServicePlatform.Infrastructure;

namespace QosServicePlatform.UnitTests;

public sealed class CircuitBreakerTests
{
    [Fact]
    public void RecordFailure_AfterTwoFailures_OpensCircuit()
    {
        var endpoint = new ServiceEndpoint(Guid.NewGuid(), ServiceCategories.Weather, "test",
            new Uri("http://localhost/test"), 0.01m);
        var breaker = new InMemoryCircuitBreaker(new Registry(endpoint));

        breaker.RecordFailure(endpoint);
        Assert.True(breaker.CanExecute(endpoint));
        breaker.RecordFailure(endpoint);

        Assert.False(breaker.CanExecute(endpoint));
        Assert.True(breaker.GetStatuses().Single().IsOpen);
    }

    [Fact]
    public void RecordSuccess_ResetsFailureCount()
    {
        var endpoint = new ServiceEndpoint(Guid.NewGuid(), ServiceCategories.Weather, "test",
            new Uri("http://localhost/test"), 0.01m);
        var breaker = new InMemoryCircuitBreaker(new Registry(endpoint));
        breaker.RecordFailure(endpoint);
        breaker.RecordSuccess(endpoint);
        Assert.True(breaker.CanExecute(endpoint));
        Assert.Equal(0, breaker.GetStatuses().Single().ConsecutiveFailures);
    }

    private sealed class Registry(ServiceEndpoint endpoint) : QosServicePlatform.Application.IServiceRegistry
    {
        public IReadOnlyList<ServiceEndpoint> GetCandidates(string categoryId) => [endpoint];
        public IReadOnlyList<ServiceEndpoint> GetAll() => [endpoint];
    }
}
