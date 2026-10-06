namespace SqlMarkdownRunner;

public record SavedRun(string FileName, DateTime SavedAt, long Bytes);

/// <summary>
/// Markdown of every run, written under wwwroot/sql-queries/&lt;connection&gt;/ when auto-save is on.
/// </summary>
public class SavedRuns(IWebHostEnvironment env)
{
    /// <summary>Nothing else deletes these, so the oldest are trimmed as new ones arrive.</summary>
    public const int KeepPerConnection = 200;

    public string Root { get; } = Path.Combine(env.WebRootPath, "sql-queries");

    public string Save(string connection, string markdown, string? label = null)
    {
        var folder = Path.Combine(Root, Safe(connection));
        Directory.CreateDirectory(folder);

        var stem = label is null ? Safe(connection) : $"{Safe(connection)}-{Safe(label)}";
        var path = Path.Combine(folder, $"{stem}-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(path, markdown);

        foreach (var old in new DirectoryInfo(folder).GetFiles("*.md")
                     .OrderByDescending(f => f.Name).Skip(KeepPerConnection))
            old.Delete();

        return path;
    }

    public List<string> Connections() =>
        Directory.Exists(Root)
            ? Directory.GetDirectories(Root).Select(Path.GetFileName).OfType<string>().Order().ToList()
            : [];

    public List<SavedRun> List(string connection)
    {
        var folder = Path.Combine(Root, Safe(connection));
        if (!Directory.Exists(folder)) return [];

        return new DirectoryInfo(folder).GetFiles("*.md")
            .OrderByDescending(f => f.LastWriteTime)
            .Select(f => new SavedRun(f.Name, f.LastWriteTime, f.Length))
            .ToList();
    }

    /// <summary>Null when the file is gone, or when the name tries to escape its folder.</summary>
    public string? Read(string connection, string fileName)
    {
        // GetFileName strips any path the caller smuggled in, so ".." cannot walk out of Root.
        var path = Path.Combine(Root, Safe(connection), Path.GetFileName(fileName));
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public void Delete(string connection, string fileName)
    {
        var path = Path.Combine(Root, Safe(connection), Path.GetFileName(fileName));
        if (File.Exists(path)) File.Delete(path);
    }

    private static void Trim(string folder, int keep)
    {
        foreach (var old in new DirectoryInfo(folder).GetFiles("*.md")
                     .OrderByDescending(f => f.Name).Skip(keep))
            old.Delete();
    }

    private static string Safe(string name) => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
}
