using QosServicePlatform.Domain;

namespace QosServicePlatform.Application;

public sealed class QosScorer : IQosScorer
{
    public IReadOnlyList<ServiceScore> Rank(
        IReadOnlyList<ServiceEndpoint> endpoints,
        IReadOnlyDictionary<Guid, QosSnapshot> snapshots,
        QosWeights weights)
    {
        weights.Validate();
        var eligible = endpoints
            .Where(endpoint => endpoint.Enabled && snapshots.ContainsKey(endpoint.Id))
            .ToArray();
        if (eligible.Length == 0) return [];

        var availability = eligible.Select(x => snapshots[x.Id].Availability).ToArray();
        var success = eligible.Select(x => snapshots[x.Id].SuccessRate).ToArray();
        var response = eligible.Select(x => snapshots[x.Id].AverageResponseTimeMs).ToArray();
        var cost = eligible.Select(x => (double)x.CostPerCall).ToArray();
        var load = eligible.Select(x => snapshots[x.Id].Load).ToArray();

        var scored = eligible.Select(endpoint =>
        {
            var snapshot = snapshots[endpoint.Id];
            var parts = new ScoreBreakdown(
                NormalizeHigher(snapshot.Availability, availability) * weights.Availability,
                NormalizeHigher(snapshot.SuccessRate, success) * weights.SuccessRate,
                NormalizeLower(snapshot.AverageResponseTimeMs, response) * weights.ResponseTime,
                NormalizeLower((double)endpoint.CostPerCall, cost) * weights.Cost,
                NormalizeLower(snapshot.Load, load) * weights.Load);
            var total = parts.Availability + parts.SuccessRate + parts.ResponseTime + parts.Cost + parts.Load;
            return (endpoint, total, parts);
        })
        .OrderByDescending(x => x.total)
        .ThenBy(x => x.endpoint.Name, StringComparer.Ordinal)
        .ToArray();

        return scored.Select((x, i) => new ServiceScore(x.endpoint, x.total, x.parts, i + 1)).ToArray();
    }

    private static double NormalizeHigher(double value, IReadOnlyCollection<double> values) =>
        Normalize(value, values, false);

    private static double NormalizeLower(double value, IReadOnlyCollection<double> values) =>
        Normalize(value, values, true);

    private static double Normalize(double value, IReadOnlyCollection<double> values, bool invert)
    {
        var min = values.Min();
        var max = values.Max();
        if (Math.Abs(max - min) < 0.000001) return 1;
        var normalized = (value - min) / (max - min);
        return invert ? 1 - normalized : normalized;
    }
}

