namespace SqlMarkdownRunner;

/// <summary>
/// The connection every page works against, held for the life of one browser circuit. The header
/// picks it; Run SQL, History and Saved runs all follow it.
/// </summary>
public class Session(ConnectionStore store)
{
    private List<DbConnectionEntry> _connections = store.Load();
    private string? _selected;

    public event Action? Changed;

    public IReadOnlyList<DbConnectionEntry> Connections => _connections;

    /// <summary>Falls back to the first connection, so a page always has something to run against.</summary>
    public DbConnectionEntry? Current =>
        _connections.FirstOrDefault(c => c.Name == _selected) ?? _connections.FirstOrDefault();

    public string? Name
    {
        get => Current?.Name;
        set
        {
            _selected = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Call after the connections list is edited so the header picks the change up.</summary>
    public void Reload()
    {
        _connections = store.Load();
        Changed?.Invoke();
    }
}
