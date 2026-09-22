using QosServicePlatform.Application;
using QosServicePlatform.Domain;
using Microsoft.Extensions.Configuration;

namespace QosServicePlatform.Infrastructure;

public sealed class InMemoryServiceRegistry : IServiceRegistry
{
    private readonly IReadOnlyList<ServiceEndpoint> _endpoints;

    public InMemoryServiceRegistry(IConfiguration? configuration = null)
    {
        var baseUrl = configuration?["Providers:BaseUrl"]
            ?? Environment.GetEnvironmentVariable("PROVIDER_BASE_URL")
            ?? "http://localhost:5201";
        baseUrl = baseUrl.TrimEnd('/');
        _endpoints =
        [
        Endpoint("10000000-0000-0000-0000-000000000001", ServiceCategories.Weather, "晴空天气", "weather/fast", 0.08m),
        Endpoint("10000000-0000-0000-0000-000000000002", ServiceCategories.Weather, "云图天气", "weather/balanced", 0.04m),
        SoapEndpoint("10000000-0000-0000-0000-000000000003", ServiceCategories.Weather, "经典 SOAP 天气", "soap/legacy-weather", 0.03m, baseUrl),
        Endpoint("20000000-0000-0000-0000-000000000001", ServiceCategories.Shipping, "闪送物流", "shipping/fast", 0.12m),
        Endpoint("20000000-0000-0000-0000-000000000002", ServiceCategories.Shipping, "稳达物流", "shipping/balanced", 0.06m),
        Endpoint("20000000-0000-0000-0000-000000000003", ServiceCategories.Shipping, "经济物流", "shipping/economy", 0.02m),
        Endpoint("30000000-0000-0000-0000-000000000001", ServiceCategories.Currency, "即时汇率", "currency/fast", 0.05m),
        Endpoint("30000000-0000-0000-0000-000000000002", ServiceCategories.Currency, "基础汇率", "currency/economy", 0.01m),
        Endpoint("40000000-0000-0000-0000-000000000001", ServiceCategories.Notification, "极速通知", "notification/fast", 0.04m),
        Endpoint("40000000-0000-0000-0000-000000000002", ServiceCategories.Notification, "可靠通知", "notification/reliable", 0.07m)
        ];

        _endpoints = _endpoints.Select(endpoint => endpoint.Protocol == ServiceProtocol.Rest
            ? endpoint with { Address = new Uri($"{baseUrl}{endpoint.Address.AbsolutePath}") }
            : endpoint).ToArray();
    }

    public IReadOnlyList<ServiceEndpoint> GetCandidates(string categoryId) =>
        _endpoints.Where(x => x.CategoryId.Equals(categoryId, StringComparison.OrdinalIgnoreCase)).ToArray();

    public IReadOnlyList<ServiceEndpoint> GetAll() => _endpoints;

    private static ServiceEndpoint Endpoint(string id, string category, string name, string route, decimal cost) =>
        new(Guid.Parse(id), category, name, new Uri($"http://localhost:5201/api/providers/{route}"), cost);

    private static ServiceEndpoint SoapEndpoint(string id, string category, string name, string route, decimal cost, string baseUrl) =>
        new(Guid.Parse(id), category, name, new Uri($"{baseUrl}/{route}"), cost, true,
            ServiceProtocol.Soap, new Uri($"{baseUrl}/{route}?wsdl"),
            "http://qos-demo.local/weather/QueryWeather");
}
