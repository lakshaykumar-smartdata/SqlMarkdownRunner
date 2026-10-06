using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SqlMarkdownRunner;

public record DbConnectionEntry(
    string Name,
    string ConnectionString,
    int TimeoutSeconds = 60,
    bool AutoSaveMarkdown = true,
    bool IsProduction = false,
    bool IsActive = true);

/// <summary>
/// Connections in a JSON file next to the app, encrypted with DPAPI because it holds production
/// passwords. Only the Windows account that wrote the file can read it back.
/// </summary>
public class ConnectionStore(IWebHostEnvironment env)
{
    private const string Marker = "DPAPI1:";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly object _lock = new();

    public string FilePath { get; } = Path.Combine(env.ContentRootPath, "connections.json");

    /// <summary>
    /// True when the file exists but could not be decrypted or parsed. Saving is refused while it
    /// is set, so a file written by another account is never silently replaced with an empty list.
    /// </summary>
    public bool Unreadable { get; private set; }

    public List<DbConnectionEntry> Load()
    {
        lock (_lock)
        {
            if (!File.Exists(FilePath))
            {
                Unreadable = false;
                return [];
            }

            var text = File.ReadAllText(FilePath);
            var wasPlainText = !text.StartsWith(Marker, StringComparison.Ordinal);

            try
            {
                var json = wasPlainText ? text : Decrypt(text[Marker.Length..]);
                var entries = JsonSerializer.Deserialize<List<DbConnectionEntry>>(json) ?? [];

                Unreadable = false;
                if (wasPlainText) Write(entries);   // migrate the old plaintext file in place
                return entries;
            }
            catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
            {
                Unreadable = true;
                return [];
            }
        }
    }

    public void Save(List<DbConnectionEntry> entries)
    {
        lock (_lock)
        {
            if (Unreadable)
                throw new InvalidOperationException(
                    $"{FilePath} could not be read, so it will not be overwritten. " +
                    "It was most likely encrypted by a different Windows account.");

            Write(entries);
        }
    }

    private void Write(List<DbConnectionEntry> entries) =>
        File.WriteAllText(FilePath, Marker + Encrypt(JsonSerializer.Serialize(entries, Json)));

    private static string Encrypt(string json) =>
        Convert.ToBase64String(ProtectedData.Protect(
            Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser));

    private static string Decrypt(string payload) =>
        Encoding.UTF8.GetString(ProtectedData.Unprotect(
            Convert.FromBase64String(payload), null, DataProtectionScope.CurrentUser));
}
