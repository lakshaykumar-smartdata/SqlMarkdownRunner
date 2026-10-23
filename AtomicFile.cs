namespace SqlMarkdownRunner;

/// <summary>
/// Writes that survive being interrupted. File.WriteAllText truncates the file before it writes,
/// so a process killed part-way through leaves half a file behind — for connections.json that is
/// every stored credential, encrypted and unrecoverable. Writing beside the target and swapping it
/// in means the real file is only ever whole, and the one it replaced is kept as .bak.
/// </summary>
public static class AtomicFile
{
    public const string BackupSuffix = ".bak";

    public static string BackupOf(string path) => path + BackupSuffix;

    public static void WriteAllText(string path, string contents)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, contents);

        // Replace needs something to replace, and the first write has nothing there yet.
        if (File.Exists(path)) File.Replace(temp, path, BackupOf(path));
        else File.Move(temp, path);
    }
}
