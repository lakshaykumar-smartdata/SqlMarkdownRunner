using System.Text.Json;

namespace SqlMarkdownRunner;

public record CachedObjects(DateTime FetchedAt, List<DbObject> Objects, int Version = 0);

/// <summary>
/// Schema listings kept per connection in a JSON file, so they survive a restart and switching
/// connections costs nothing. Refresh in the UI is what re-queries the server.
/// </summary>
public class ObjectCache(IWebHostEnvironment env, ILogger<ObjectCache> log)
{
    // Bump whenever DbObject or DbColumn gains a field: an older file would otherwise
    // deserialise with those fields silently defaulted, showing wrong data as fact.
    private const int CurrentVersion = 5;

    private readonly object _lock = new();

    public string FilePath { get; } = Path.Combine(env.ContentRootPath, "objects.json");

    public CachedObjects? Get(string connection)
    {
        lock (_lock)
        {
            var cached = Read().GetValueOrDefault(connection);
            return cached?.Version == CurrentVersion ? cached : null;
        }
    }

    public void Set(string connection, List<DbObject> objects)
    {
        lock (_lock)
        {
            var all = Read();
            all[connection] = new CachedObjects(DateTime.Now, objects, CurrentVersion);
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(all));
        }
    }

    private Dictionary<string, CachedObjects> Read()
    {
        if (!File.Exists(FilePath)) return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, CachedObjects>>(File.ReadAllText(FilePath)) ?? [];
        }
        catch (JsonException ex)
        {
            // A cache written by an older shape must never break the page — it is all refetchable.
            log.LogWarning(ex, "{Path} is unreadable; objects will be fetched from the server", FilePath);
            return [];
        }
    }
}
