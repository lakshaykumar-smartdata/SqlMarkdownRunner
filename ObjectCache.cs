using System.Text.Json;

namespace SqlMarkdownRunner;

public record CachedObjects(DateTime FetchedAt, List<DbObject> Objects);

/// <summary>
/// Schema listings kept per connection in a JSON file, so they survive a restart and switching
/// connections costs nothing. Refresh in the UI is what re-queries the server.
/// </summary>
public class ObjectCache(IWebHostEnvironment env)
{
    private readonly object _lock = new();

    public string FilePath { get; } = Path.Combine(env.ContentRootPath, "objects.json");

    public CachedObjects? Get(string connection)
    {
        lock (_lock) return Read().GetValueOrDefault(connection);
    }

    public void Set(string connection, List<DbObject> objects)
    {
        lock (_lock)
        {
            var all = Read();
            all[connection] = new CachedObjects(DateTime.Now, objects);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(all));
        }
    }

    private Dictionary<string, CachedObjects> Read()
    {
        if (!File.Exists(FilePath)) return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, CachedObjects>>(File.ReadAllText(FilePath)) ?? [];
        }
        catch (JsonException)
        {
            return [];   // a cache written by an older shape must never break the page
        }
    }
}
