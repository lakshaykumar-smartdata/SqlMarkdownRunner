namespace SqlMarkdownRunner;

public record SavedRun(string FileName, DateTime SavedAt, long Bytes);

/// <summary>
/// Markdown of every run, written under wwwroot/sql-queries/&lt;connection&gt;/ when auto-save is on.
/// </summary>
public class SavedRuns(IWebHostEnvironment env)
{
    public string Root { get; } = Path.Combine(env.WebRootPath, "sql-queries");

    public string Save(string connection, string markdown)
    {
        var folder = Path.Combine(Root, Safe(connection));
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"{Safe(connection)}-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(path, markdown);
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

    private static string Safe(string name) => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
}
