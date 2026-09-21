using System.Text.Json;

namespace SqlMarkdownRunner;

public record HistoryEntry(string Id, string Connection, DateTime RunAt, string Sql);

/// <summary>Past runs, newest first, in a JSON file next to the app — same deal as ConnectionStore.</summary>
public class HistoryStore(IWebHostEnvironment env)
{
    // ponytail: flat file trimmed to the last N runs. Move to SQLite only if this gets slow.
    public const int MaxEntries = 200;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly object _lock = new();

    public string FilePath { get; } = Path.Combine(env.ContentRootPath, "history.json");

    public List<HistoryEntry> Load()
    {
        lock (_lock)
        {
            if (!File.Exists(FilePath)) return [];
            return JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(FilePath)) ?? [];
        }
    }

    public HistoryEntry? Find(string id) => Load().FirstOrDefault(e => e.Id == id);

    public void Add(string connection, string sql)
    {
        sql = sql.Trim();
        var entries = Load();

        // Re-running the same statement moves it back to the top instead of piling up.
        entries.RemoveAll(e => e.Connection == connection && e.Sql == sql);
        entries.Insert(0, new HistoryEntry(Guid.NewGuid().ToString("N")[..8], connection, DateTime.Now, sql));

        Save(entries.Take(MaxEntries).ToList());
    }

    /// <summary>Clears one connection's history, or everything when connection is null.</summary>
    public void Clear(string? connection) =>
        Save(connection is null ? [] : Load().Where(e => e.Connection != connection).ToList());

    private void Save(List<HistoryEntry> entries)
    {
        lock (_lock) File.WriteAllText(FilePath, JsonSerializer.Serialize(entries, Json));
    }
}
