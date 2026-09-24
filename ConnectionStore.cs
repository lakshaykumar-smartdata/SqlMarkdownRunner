using System.Text.Json;

namespace SqlMarkdownRunner;

public record DbConnectionEntry(
    string Name,
    string ConnectionString,
    int TimeoutSeconds = 60,
    bool AutoSaveMarkdown = true);

/// <summary>Connections persisted as a plain JSON file next to the app, hand-editable.</summary>
public class ConnectionStore(IWebHostEnvironment env)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly object _lock = new();

    public string FilePath { get; } = Path.Combine(env.ContentRootPath, "connections.json");

    public List<DbConnectionEntry> Load()
    {
        lock (_lock)
        {
            if (!File.Exists(FilePath)) return [];
            return JsonSerializer.Deserialize<List<DbConnectionEntry>>(File.ReadAllText(FilePath)) ?? [];
        }
    }

    public void Save(List<DbConnectionEntry> entries)
    {
        lock (_lock) File.WriteAllText(FilePath, JsonSerializer.Serialize(entries, Json));
    }
}
