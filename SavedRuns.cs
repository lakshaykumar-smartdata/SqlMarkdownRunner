using System.Text.RegularExpressions;

namespace SqlMarkdownRunner;

public record SavedRun(string FileName, DateTime SavedAt, long Bytes);

/// <summary>
/// Markdown of every run, written under wwwroot/sql-queries/&lt;connection&gt;/ when auto-save is on.
/// </summary>
public partial class SavedRuns(IWebHostEnvironment env)
{
    /// <summary>Nothing else deletes these, so the oldest are trimmed as new ones arrive.</summary>
    public const int KeepPerConnection = 200;

    public string Root { get; } = Path.Combine(env.WebRootPath, "sql-queries");

    public string Save(string connection, string markdown, string? label = null)
    {
        var folder = Path.Combine(Root, Safe(connection));
        Directory.CreateDirectory(folder);

        // Timestamp before label, so the name still sorts by date and the label is free to be
        // anything. Sorting by name with the label first would group by subject instead.
        var subject = string.IsNullOrWhiteSpace(label) ? "" : $"-{Safe(label)}";
        var path = Path.Combine(folder, $"{Safe(connection)}-{DateTime.Now:yyyyMMdd-HHmmss}{subject}.md");
        File.WriteAllText(path, markdown);

        Trim(folder, KeepPerConnection);

        return path;
    }

    [GeneratedRegex(@"-\d{8}-\d{6}-(?<label>.+)\.md$")]
    private static partial Regex StampedName();

    /// <summary>
    /// The subject out of a saved-run file name, or empty for one written before names carried it.
    /// The list already shows the date and the connection, so the subject is the only part worth
    /// the width.
    /// </summary>
    public static string LabelOf(string fileName)
    {
        var m = StampedName().Match(fileName);
        return m.Success ? m.Groups["label"].Value : "";
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

    /// <summary>Oldest first by the clock, not by name — retention must not depend on how a file
    /// happens to be called.</summary>
    private static void Trim(string folder, int keep)
    {
        foreach (var old in new DirectoryInfo(folder).GetFiles("*.md")
                     .OrderByDescending(f => f.LastWriteTime).Skip(keep))
            old.Delete();
    }

    private static string Safe(string name) => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
}
