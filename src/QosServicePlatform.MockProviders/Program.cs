using System.Collections.Concurrent;
using System.Xml.Linq;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var profiles = new Dictionary<string, ProviderProfile>(StringComparer.OrdinalIgnoreCase)
{
    ["fast"] = new(80, 0.04),
    ["balanced"] = new(160, 0.015),
    ["economy"] = new(350, 0.07),
    ["reliable"] = new(220, 0.005)
};
var forcedFailures = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "mock-providers" }));

app.MapGet("/soap/legacy-weather", (HttpRequest request) =>
{
    if (!request.Query.ContainsKey("wsdl")) return Results.BadRequest("请使用 ?wsdl 获取服务描述。");
    var endpoint = $"{request.Scheme}://{request.Host}/soap/legacy-weather";
    return Results.Text(CreateWeatherWsdl(endpoint), "text/xml; charset=utf-8");
});

app.MapPost("/soap/legacy-weather", async (HttpRequest request, CancellationToken cancellationToken) =>
{
    await Task.Delay(260, cancellationToken);
    if (Random.Shared.NextDouble() < 0.02)
        return Results.Text(CreateSoapFault("模拟 SOAP 服务临时故障。"), "text/xml; charset=utf-8", statusCode: 500);

    using var reader = new StreamReader(request.Body);
    var xml = XDocument.Parse(await reader.ReadToEndAsync(cancellationToken));
    var city = xml.Descendants().FirstOrDefault(x => x.Name.LocalName == "city")?.Value ?? "上海";
    return Results.Text(CreateSoapWeatherResponse(city), "text/xml; charset=utf-8");
});

app.MapPost("/api/providers/{category}/{provider}", async (
    string category,
    string provider,
    Dictionary<string, string> payload,
    CancellationToken cancellationToken) =>
{
    if (!KnownCategories.Contains(category) || !profiles.TryGetValue(provider, out var profile))
        return Results.NotFound(new { error = "未知的服务类别或提供者。" });

    await Task.Delay(profile.DelayMs, cancellationToken);
    var key = $"{category}/{provider}";
    if (forcedFailures.GetValueOrDefault(key) || Random.Shared.NextDouble() < profile.FailureRate)
        return Results.Problem("模拟提供者发生故障。", statusCode: 503);

    return Results.Ok(CreateResponse(category, provider, payload));
});

app.MapPost("/api/admin/failures/{category}/{provider}", (
    string category,
    string provider,
    FailureCommand command) =>
{
    forcedFailures[$"{category}/{provider}"] = command.Enabled;
    return Results.Ok(new { category, provider, command.Enabled });
});

app.Run();

static object CreateResponse(string category, string provider, Dictionary<string, string> payload) => category switch
{
    "weather" => new
    {
        provider,
        city = Value(payload, "city", "上海"),
        temperature = 23,
        condition = "晴间多云"
    },
    "shipping" => new
    {
        provider,
        origin = Value(payload, "origin", "上海"),
        destination = Value(payload, "destination", "北京"),
        price = provider == "fast" ? 38 : provider == "economy" ? 16 : 24,
        estimatedDays = provider == "fast" ? 1 : provider == "economy" ? 4 : 2
    },
    "currency" => new
    {
        provider,
        from = Value(payload, "from", "CNY"),
        to = Value(payload, "to", "USD"),
        amount = DecimalValue(payload, "amount", 100),
        result = Math.Round(DecimalValue(payload, "amount", 100) * 0.14m, 2)
    },
    "notification" => new
    {
        provider,
        recipient = Value(payload, "recipient", "demo@example.com"),
        accepted = true,
        messageId = Guid.NewGuid()
    },
    _ => new { provider }
};

static string Value(Dictionary<string, string> payload, string key, string fallback) =>
    payload.GetValueOrDefault(key) ?? fallback;

static decimal DecimalValue(Dictionary<string, string> payload, string key, decimal fallback) =>
    decimal.TryParse(payload.GetValueOrDefault(key), out var value) ? value : fallback;

static string CreateWeatherWsdl(string endpoint) => $$"""
<?xml version="1.0" encoding="utf-8"?>
<definitions xmlns="http://schemas.xmlsoap.org/wsdl/"
 xmlns:soap="http://schemas.xmlsoap.org/wsdl/soap/"
 xmlns:tns="http://qos-demo.local/weather"
 xmlns:xsd="http://www.w3.org/2001/XMLSchema"
 targetNamespace="http://qos-demo.local/weather">
  <types><xsd:schema targetNamespace="http://qos-demo.local/weather">
    <xsd:element name="QueryWeather"><xsd:complexType><xsd:sequence><xsd:element name="city" type="xsd:string" /></xsd:sequence></xsd:complexType></xsd:element>
    <xsd:element name="QueryWeatherResponse"><xsd:complexType><xsd:sequence><xsd:element name="QueryWeatherResult" type="xsd:string" /></xsd:sequence></xsd:complexType></xsd:element>
  </xsd:schema></types>
  <message name="QueryWeatherInput"><part name="parameters" element="tns:QueryWeather" /></message>
  <message name="QueryWeatherOutput"><part name="parameters" element="tns:QueryWeatherResponse" /></message>
  <portType name="LegacyWeatherPortType"><operation name="QueryWeather"><input message="tns:QueryWeatherInput" /><output message="tns:QueryWeatherOutput" /></operation></portType>
  <binding name="LegacyWeatherSoapBinding" type="tns:LegacyWeatherPortType"><soap:binding transport="http://schemas.xmlsoap.org/soap/http" /><operation name="QueryWeather"><soap:operation soapAction="http://qos-demo.local/weather/QueryWeather" /><input><soap:body use="literal" /></input><output><soap:body use="literal" /></output></operation></binding>
  <service name="LegacyWeatherService"><port name="LegacyWeatherSoap" binding="tns:LegacyWeatherSoapBinding"><soap:address location="{{endpoint}}" /></port></service>
</definitions>
""";

static string CreateSoapWeatherResponse(string city)
{
    XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
    XNamespace weather = "http://qos-demo.local/weather";
    return new XDocument(new XElement(soap + "Envelope",
        new XAttribute(XNamespace.Xmlns + "soap", soap),
        new XElement(soap + "Body",
            new XElement(weather + "QueryWeatherResponse",
                new XElement(weather + "QueryWeatherResult", $"{city}: 22°C, 晴，来源 Legacy SOAP")))))
        .ToString(SaveOptions.DisableFormatting);
}

static string CreateSoapFault(string message)
{
    XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
    return new XDocument(new XElement(soap + "Envelope",
        new XAttribute(XNamespace.Xmlns + "soap", soap),
        new XElement(soap + "Body", new XElement(soap + "Fault",
            new XElement("faultcode", "soap:Server"), new XElement("faultstring", message)))))
        .ToString(SaveOptions.DisableFormatting);
}

static class KnownCategories
{
    private static readonly HashSet<string> Values = ["weather", "shipping", "currency", "notification"];
    public static bool Contains(string value) => Values.Contains(value);
}

sealed record ProviderProfile(int DelayMs, double FailureRate);
sealed record FailureCommand(bool Enabled);

public partial class Program;
