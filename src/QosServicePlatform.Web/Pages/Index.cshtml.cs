using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QosServicePlatform.Application;
using QosServicePlatform.Domain;

namespace QosServicePlatform.Web.Pages;

public sealed class IndexModel(
    ICompositeOrchestrator orchestrator,
    ICallHistoryStore history,
    ICircuitBreaker circuitBreaker,
    IEndpointHealthMonitor healthMonitor) : PageModel
{
    public IReadOnlyDictionary<string, string> Categories => ServiceCategories.All;

    [BindProperty] public string[] SelectedCategories { get; set; } = [ServiceCategories.Weather];
    [BindProperty] public string City { get; set; } = "上海";
    [BindProperty] public string Origin { get; set; } = "上海";
    [BindProperty] public string Destination { get; set; } = "北京";
    [BindProperty] public string Amount { get; set; } = "100";
    [BindProperty] public string Recipient { get; set; } = "demo@example.com";
    [BindProperty] public double AvailabilityWeight { get; set; } = 35;
    [BindProperty] public double SuccessWeight { get; set; } = 25;
    [BindProperty] public double ResponseWeight { get; set; } = 20;
    [BindProperty] public double CostWeight { get; set; } = 10;
    [BindProperty] public double LoadWeight { get; set; } = 10;

    public CompositeResult? Result { get; private set; }
    public IReadOnlyList<CallHistoryEntry> History => history.GetRecent(10);
    public IReadOnlyList<CircuitStatus> CircuitStatuses => circuitBreaker.GetStatuses();
    public IReadOnlyList<EndpointHealthStatus> HealthStatuses => healthMonitor.GetStatuses();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (SelectedCategories.Length is < 1 or > 4)
            ModelState.AddModelError(string.Empty, "请选择 1 到 4 个服务类别。");

        var total = AvailabilityWeight + SuccessWeight + ResponseWeight + CostWeight + LoadWeight;
        if (Math.Abs(total - 100) > 0.01)
            ModelState.AddModelError(string.Empty, $"权重之和必须为 100%，当前为 {total:0.##}%。");

        if (!ModelState.IsValid) return Page();

        var weights = new QosWeights(
            AvailabilityWeight / 100,
            SuccessWeight / 100,
            ResponseWeight / 100,
            CostWeight / 100,
            LoadWeight / 100);
        var tasks = SelectedCategories.Distinct().Select(CreateTask).ToArray();
        Result = await orchestrator.ExecuteAsync(new CompositeRequest(Guid.NewGuid(), tasks, weights), cancellationToken);
        return Page();
    }

    private ServiceTask CreateTask(string category) => category switch
    {
        ServiceCategories.Weather => new(category, new Dictionary<string, string> { ["city"] = City }),
        ServiceCategories.Shipping => new(category, new Dictionary<string, string>
        {
            ["origin"] = Origin,
            ["destination"] = Destination,
            ["weight"] = "2"
        }),
        ServiceCategories.Currency => new(category, new Dictionary<string, string>
        {
            ["from"] = "CNY",
            ["to"] = "USD",
            ["amount"] = Amount
        }),
        ServiceCategories.Notification => new(category, new Dictionary<string, string>
        {
            ["recipient"] = Recipient,
            ["message"] = "QoS 动态调用演示消息"
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "未知服务类别。")
    };
}
