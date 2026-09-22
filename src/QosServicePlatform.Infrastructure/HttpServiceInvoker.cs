using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using System.Collections.Concurrent;
using QosServicePlatform.Application;
using QosServicePlatform.Domain;

namespace QosServicePlatform.Infrastructure;

public sealed class HttpServiceInvoker(IHttpClientFactory clientFactory) : IServiceInvoker
{
    private readonly ConcurrentDictionary<Uri, bool> _validatedWsdls = new();

    public async Task<string> InvokeAsync(ServiceEndpoint endpoint, ServiceTask task, CancellationToken cancellationToken)
    {
        if (endpoint.Protocol == ServiceProtocol.Soap)
            return await InvokeSoapAsync(endpoint, task, cancellationToken);

        var client = clientFactory.CreateClient("dynamic-services");
        using var response = await client.PostAsJsonAsync(endpoint.Address, task.Payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<string> InvokeSoapAsync(
        ServiceEndpoint endpoint,
        ServiceTask task,
        CancellationToken cancellationToken)
    {
        if (endpoint.WsdlAddress is null || string.IsNullOrWhiteSpace(endpoint.SoapAction))
            throw new InvalidOperationException("SOAP 端点缺少 WSDL 或 SOAP Action 配置。");

        var client = clientFactory.CreateClient("dynamic-services");
        if (!_validatedWsdls.ContainsKey(endpoint.WsdlAddress))
        {
            var wsdl = await client.GetStringAsync(endpoint.WsdlAddress, cancellationToken);
            var document = XDocument.Parse(wsdl);
            XNamespace definitions = "http://schemas.xmlsoap.org/wsdl/";
            if (!document.Descendants(definitions + "operation").Any(x => (string?)x.Attribute("name") == "QueryWeather"))
                throw new InvalidOperationException("WSDL 中不存在 QueryWeather 操作。");
            _validatedWsdls[endpoint.WsdlAddress] = true;
        }

        XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
        XNamespace weather = "http://qos-demo.local/weather";
        var envelope = new XDocument(
            new XElement(soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soap", soap),
                new XElement(soap + "Body",
                    new XElement(weather + "QueryWeather",
                        new XElement(weather + "city", task.Payload.GetValueOrDefault("city") ?? "上海")))));
        using var content = new StringContent(envelope.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", $"\"{endpoint.SoapAction}\"");
        using var response = await client.PostAsync(endpoint.Address, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var responseXml = XDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var fault = responseXml.Descendants(soap + "Fault").FirstOrDefault();
        if (fault is not null) throw new HttpRequestException(fault.Value);
        var result = responseXml.Descendants().FirstOrDefault(x => x.Name.LocalName == "QueryWeatherResult")?.Value
            ?? throw new HttpRequestException("SOAP 响应缺少 QueryWeatherResult。");
        return JsonSerializer.Serialize(new { protocol = "SOAP 1.1", wsdl = endpoint.WsdlAddress, result },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
