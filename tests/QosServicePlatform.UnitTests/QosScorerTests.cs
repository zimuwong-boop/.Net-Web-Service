using QosServicePlatform.Application;
using QosServicePlatform.Domain;

namespace QosServicePlatform.UnitTests;

public sealed class QosScorerTests
{
    [Fact]
    public void Rank_WhenSpeedDominates_SelectsFastestEndpoint()
    {
        var fast = Endpoint("fast", 0.10m);
        var slow = Endpoint("slow", 0.01m);
        var snapshots = new Dictionary<Guid, QosSnapshot>
        {
            [fast.Id] = Snapshot(fast.Id, 50),
            [slow.Id] = Snapshot(slow.Id, 500)
        };
        var weights = new QosWeights(0, 0, 1, 0, 0);

        var ranking = new QosScorer().Rank([slow, fast], snapshots, weights);

        Assert.Equal(fast.Id, ranking[0].Endpoint.Id);
        Assert.Equal(1, ranking[0].Rank);
    }

    [Fact]
    public void Rank_WhenCostDominates_SelectsCheapestEndpoint()
    {
        var expensive = Endpoint("expensive", 0.20m);
        var cheap = Endpoint("cheap", 0.01m);
        var snapshots = new Dictionary<Guid, QosSnapshot>
        {
            [expensive.Id] = Snapshot(expensive.Id, 50),
            [cheap.Id] = Snapshot(cheap.Id, 500)
        };

        var ranking = new QosScorer().Rank(
            [expensive, cheap], snapshots, new QosWeights(0, 0, 0, 1, 0));

        Assert.Equal(cheap.Id, ranking[0].Endpoint.Id);
    }

    [Fact]
    public void Validate_WhenWeightsDoNotTotalOne_Throws()
    {
        var weights = new QosWeights(0.5, 0.5, 0.5, 0, 0);
        Assert.Throws<ArgumentException>(weights.Validate);
    }

    private static ServiceEndpoint Endpoint(string name, decimal cost) =>
        new(Guid.NewGuid(), ServiceCategories.Weather, name, new Uri($"http://localhost/{name}"), cost);

    private static QosSnapshot Snapshot(Guid id, double milliseconds) =>
        new(id, 0.99, 0.99, milliseconds, 0.5, DateTimeOffset.UtcNow);
}

