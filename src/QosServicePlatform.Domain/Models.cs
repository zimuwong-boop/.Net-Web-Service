namespace QosServicePlatform.Domain;

public enum ServiceProtocol
{
    Rest,
    Soap
}

public static class ServiceCategories
{
    public const string Weather = "weather";
    public const string Shipping = "shipping";
    public const string Currency = "currency";
    public const string Notification = "notification";

    public static readonly IReadOnlyDictionary<string, string> All =
        new Dictionary<string, string>
        {
            [Weather] = "天气查询",
            [Shipping] = "物流报价",
            [Currency] = "汇率换算",
            [Notification] = "消息通知"
        };
}

public sealed record ServiceEndpoint(
    Guid Id,
    string CategoryId,
    string Name,
    Uri Address,
    decimal CostPerCall,
    bool Enabled = true,
    ServiceProtocol Protocol = ServiceProtocol.Rest,
    Uri? WsdlAddress = null,
    string? SoapAction = null);

public sealed record QosSnapshot(
    Guid EndpointId,
    double Availability,
    double SuccessRate,
    double AverageResponseTimeMs,
    double Load,
    DateTimeOffset CapturedAt);

public sealed record QosWeights(
    double Availability = 0.35,
    double SuccessRate = 0.25,
    double ResponseTime = 0.20,
    double Cost = 0.10,
    double Load = 0.10)
{
    public static QosWeights Default { get; } = new();

    public void Validate()
    {
        var values = new[] { Availability, SuccessRate, ResponseTime, Cost, Load };
        if (values.Any(value => value < 0 || value > 1))
            throw new ArgumentOutOfRangeException(nameof(QosWeights), "每项权重必须介于 0 和 1 之间。");
        if (Math.Abs(values.Sum() - 1) > 0.0001)
            throw new ArgumentException("QoS 权重之和必须等于 1。", nameof(QosWeights));
    }
}

public sealed record ScoreBreakdown(
    double Availability,
    double SuccessRate,
    double ResponseTime,
    double Cost,
    double Load);

public sealed record ServiceScore(
    ServiceEndpoint Endpoint,
    double Total,
    ScoreBreakdown Breakdown,
    int Rank);

public sealed record ServiceTask(string CategoryId, IReadOnlyDictionary<string, string> Payload);

public sealed record CompositeRequest(
    Guid CorrelationId,
    IReadOnlyList<ServiceTask> Tasks,
    QosWeights Weights);

public sealed record Attempt(
    Guid EndpointId,
    string EndpointName,
    int Rank,
    TimeSpan Duration,
    string Outcome,
    string? ErrorCode);

public sealed record ServiceCallResult(
    string CategoryId,
    Guid? EndpointId,
    string? EndpointName,
    bool Success,
    string? Data,
    IReadOnlyList<ServiceScore> Ranking,
    IReadOnlyList<Attempt> Attempts,
    string? Error);

public sealed record CompositeResult(
    Guid CorrelationId,
    IReadOnlyList<ServiceCallResult> Results,
    TimeSpan Elapsed);

public sealed record CallHistoryEntry(
    Guid CorrelationId,
    DateTimeOffset CompletedAt,
    int ServiceCount,
    int SuccessCount,
    double ElapsedMilliseconds,
    IReadOnlyList<string> Categories);

public sealed record CircuitStatus(
    Guid EndpointId,
    string EndpointName,
    int ConsecutiveFailures,
    DateTimeOffset? OpenUntil,
    bool IsOpen);

public sealed record EndpointHealthStatus(
    Guid EndpointId,
    string EndpointName,
    bool IsHealthy,
    DateTimeOffset CheckedAt);
