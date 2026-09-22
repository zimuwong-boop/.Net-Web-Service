using System.Text.Json;
using QosServicePlatform.Application;
using QosServicePlatform.Domain;

namespace QosServicePlatform.Infrastructure;

public sealed class JsonCallHistoryStore : ICallHistoryStore
{
    private readonly Lock _lock = new();
    private readonly string _path = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "call-history.json");
    private List<CallHistoryEntry>? _entries;

    public void Add(CompositeResult result)
    {
        lock (_lock)
        {
            var entries = Load();
            entries.Insert(0, new CallHistoryEntry(result.CorrelationId, DateTimeOffset.Now,
                result.Results.Count, result.Results.Count(x => x.Success), result.Elapsed.TotalMilliseconds,
                result.Results.Select(x => x.CategoryId).ToArray()));
            if (entries.Count > 100) entries.RemoveRange(100, entries.Count - 100);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    public IReadOnlyList<CallHistoryEntry> GetRecent(int count = 20)
    {
        lock (_lock) return Load().Take(Math.Clamp(count, 1, 100)).ToArray();
    }

    private List<CallHistoryEntry> Load()
    {
        if (_entries is not null) return _entries;
        if (!File.Exists(_path)) return _entries = [];
        try
        {
            return _entries = JsonSerializer.Deserialize<List<CallHistoryEntry>>(File.ReadAllText(_path)) ?? [];
        }
        catch (JsonException)
        {
            return _entries = [];
        }
    }
}
