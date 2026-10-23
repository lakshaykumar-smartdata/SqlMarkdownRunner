using System.Text.Json;

namespace SqlMarkdownRunner;

/// <summary>
/// The connection every page works against. The header picks it; Run SQL, History and Saved runs
/// all follow it. The choice is written to selection.json, so a refresh or a new tab keeps it.
/// </summary>
public class Session(ConnectionStore store, IWebHostEnvironment env, ILogger<Session> log)
{
    private readonly string _file = Path.Combine(env.ContentRootPath, "selection.json");

    private List<DbConnectionEntry> _connections = store.Load();
    private string? _selected = Read(Path.Combine(env.ContentRootPath, "selection.json"), log);

    public event Action? Changed;

    /// <summary>Only the active ones; the Connections page is where a deactivated one comes back.</summary>
    public IReadOnlyList<DbConnectionEntry> Connections =>
        _connections.Where(c => c.IsActive).ToList();

    /// <summary>Falls back to the first connection, so a page always has something to run against.</summary>
    public DbConnectionEntry? Current =>
        Connections.FirstOrDefault(c => c.Name == _selected) ?? Connections.FirstOrDefault();

    public string? Name
    {
        get => Current?.Name;
        set
        {
            _selected = value;
            Write(value);
            Changed?.Invoke();
        }
    }

    /// <summary>Call after the connections list is edited so the header picks the change up.</summary>
    public void Reload()
    {
        _connections = store.Load();
        Changed?.Invoke();
    }

    private static string? Read(string file, ILogger log)
    {
        try
        {
            return File.Exists(file) ? JsonSerializer.Deserialize<string>(File.ReadAllText(file)) : null;
        }
        catch (Exception ex)
        {
            // An unreadable preference just means "no preference" — but say so, rather than
            // leaving someone wondering why their connection keeps resetting.
            log.LogWarning(ex, "{Path} could not be read; falling back to no saved connection", file);
            return null;
        }
    }

    private void Write(string? name)
    {
        try
        {
            File.WriteAllText(_file, JsonSerializer.Serialize(name));
        }
        catch (IOException)
        {
            // two tabs switching at once is harmless; the next switch wins
        }
    }
}
