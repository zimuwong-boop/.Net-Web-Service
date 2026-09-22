using QosServicePlatform.Infrastructure;
using QosServicePlatform.Application;
using QosServicePlatform.Domain;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddRazorPages();
builder.Services.AddQosServicePlatform();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "qos-web" }));
app.MapPost("/api/composite", async (
    CompositeApiRequest request,
    ICompositeOrchestrator orchestrator,
    CancellationToken cancellationToken) =>
{
    if (request.Categories.Length is < 1 or > 4)
        return Results.BadRequest(new { error = "categories 必须包含 1 到 4 个服务类别。" });

    var tasks = request.Categories.Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(category => new ServiceTask(category, request.Payload ?? new Dictionary<string, string>()))
        .ToArray();
    try
    {
        var result = await orchestrator.ExecuteAsync(new CompositeRequest(
            Guid.NewGuid(), tasks, request.Weights ?? QosWeights.Default), cancellationToken);
        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

public sealed record CompositeApiRequest(
    string[] Categories,
    Dictionary<string, string>? Payload,
    QosWeights? Weights);

public partial class Program;
