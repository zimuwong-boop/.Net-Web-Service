using QosServicePlatform.Domain;
using QosServicePlatform.Infrastructure;

namespace QosServicePlatform.IntegrationTests;

public sealed class RegistryTests
{
    [Theory]
    [InlineData(ServiceCategories.Weather, 3)]
    [InlineData(ServiceCategories.Shipping, 3)]
    [InlineData(ServiceCategories.Currency, 2)]
    [InlineData(ServiceCategories.Notification, 2)]
    public void GetCandidates_ReturnsConfiguredProviders(string category, int expected)
    {
        var registry = new InMemoryServiceRegistry();
        var candidates = registry.GetCandidates(category);
        Assert.Equal(expected, candidates.Count);
        Assert.All(candidates, candidate => Assert.Equal(category, candidate.CategoryId));
    }
}
