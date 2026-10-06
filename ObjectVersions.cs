namespace SqlMarkdownRunner;

public record ObjectVersion(string FileName, DateTime SavedAt, long Bytes);

/// <summary>
/// Every definition this app has saved, one .sql per version under
/// wwwroot/object-versions/&lt;connection&gt;/&lt;object&gt;/, so changes can be diffed later.
/// </summary>
public class ObjectVersions(IWebHostEnvironment env)
{
    public string Root { get; } = Path.Combine(env.WebRootPath, "object-versions");

    public string Save(string connection, string objectName, string definition)
    {
        var folder = Folder(connection, objectName);
        Directory.CreateDirectory(folder);

        // The baseline and the new text are written within the same second, so the stamp needs
        // milliseconds - and a fixed-width one, since the list is ordered by file name.
        var when = DateTime.Now;
        string path;
        while (File.Exists(path = Path.Combine(folder, $"{Safe(objectName)}-{when:yyyyMMdd-HHmmss-fff}.sql")))
            when = when.AddMilliseconds(1);

        File.WriteAllText(path, definition);
        return path;
    }

    /// <summary>Newest first.</summary>
    public List<ObjectVersion> List(string connection, string objectName)
    {
        var folder = Folder(connection, objectName);
        if (!Directory.Exists(folder)) return [];

        return new DirectoryInfo(folder).GetFiles("*.sql")
            .OrderByDescending(f => f.Name)
            .Select(f => new ObjectVersion(f.Name, f.LastWriteTime, f.Length))
            .ToList();
    }

    public string? Read(string connection, string objectName, string fileName)
    {
        // GetFileName strips any path the caller smuggled in, so ".." cannot walk out of Root.
        var path = Path.Combine(Folder(connection, objectName), Path.GetFileName(fileName));
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private string Folder(string connection, string objectName) =>
        Path.Combine(Root, Safe(connection), Safe(objectName));

    private static string Safe(string name) => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
}
