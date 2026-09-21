using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace SqlMarkdownRunner;

public partial class SqlRunner
{
    // ponytail: line-level GO split; a literal "GO" alone on a line inside a string/block comment
    // would split wrongly. Swap for a real T-SQL tokenizer only if that ever bites.
    [GeneratedRegex(@"^[\t ]*GO[\t ]*(?<count>\d+)?[\t ]*(--.*)?\r?$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex GoSeparator();

    public const int MaxRowsPerResultSet = 500;

    public static IEnumerable<string> SplitBatches(string sql)
    {
        var last = 0;
        foreach (Match m in GoSeparator().Matches(sql))
        {
            var batch = sql[last..m.Index];
            var repeat = m.Groups["count"].Success ? int.Parse(m.Groups["count"].Value) : 1;
            if (!string.IsNullOrWhiteSpace(batch))
                for (var i = 0; i < repeat; i++) yield return batch.Trim();
            last = m.Index + m.Length;
        }
        var tail = sql[last..];
        if (!string.IsNullOrWhiteSpace(tail)) yield return tail.Trim();
    }

    /// <summary>Runs the script and returns the whole session as one markdown document.</summary>
    public static async Task<string> RunAsMarkdownAsync(
        DbConnectionEntry entry, string sql, int commandTimeoutSeconds, CancellationToken ct = default)
    {
        var md = new StringBuilder();
        var sw = Stopwatch.StartNew();

        md.AppendLine("# SQL run");
        md.AppendLine();
        md.AppendLine($"- **Connection:** {entry.Name} — `{Describe(entry.ConnectionString)}`");
        md.AppendLine($"- **Executed (local):** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        md.AppendLine();

        await using var conn = new SqlConnection(entry.ConnectionString);
        var batches = SplitBatches(sql).ToList();
        try
        {
            await conn.OpenAsync(ct);
            for (var i = 0; i < batches.Count; i++)
            {
                md.AppendLine($"## Batch {i + 1} of {batches.Count}");
                md.AppendLine();
                md.AppendLine("```sql");
                md.AppendLine(batches[i]);
                md.AppendLine("```");
                md.AppendLine();
                await AppendBatchResultAsync(conn, batches[i], commandTimeoutSeconds, md, ct);
                md.AppendLine();
            }
        }
        catch (Exception ex)
        {
            md.AppendLine($"> **Error:** {ex.Message.Replace("\n", " ")}");
            md.AppendLine();
        }

        md.AppendLine($"_Total time: {sw.ElapsedMilliseconds} ms_");
        return md.ToString();
    }

    private static async Task AppendBatchResultAsync(
        SqlConnection conn, string batch, int timeout, StringBuilder md, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(batch, conn) { CommandTimeout = timeout };
        var messages = new List<string>();
        void OnInfo(object s, SqlInfoMessageEventArgs e) => messages.Add(e.Message);
        conn.InfoMessage += OnInfo;
        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var set = 0;
            do
            {
                if (reader.FieldCount > 0)
                    await AppendTableAsync(reader, md, ++set, ct);
            } while (await reader.NextResultAsync(ct));

            await reader.CloseAsync();
            if (set == 0)
                md.AppendLine($"**{Math.Max(reader.RecordsAffected, 0)} row(s) affected.**");
        }
        catch (SqlException ex)
        {
            md.AppendLine($"> **Error {ex.Number}** (line {ex.LineNumber}): {ex.Message.Replace("\n", " ")}");
        }
        finally
        {
            conn.InfoMessage -= OnInfo;
        }

        foreach (var m in messages) md.AppendLine($"> {m.Replace("\n", " ")}");
    }

    private static async Task AppendTableAsync(SqlDataReader reader, StringBuilder md, int set, CancellationToken ct)
    {
        // SQL Server returns "" for expression columns (e.g. select count(*)); an empty
        // markdown header renders as a broken table, so give it a usable name.
        var cols = Enumerable.Range(0, reader.FieldCount)
            .Select(i => string.IsNullOrWhiteSpace(reader.GetName(i)) ? $"column{i + 1}" : reader.GetName(i))
            .ToArray();
        var rows = new List<string[]>();
        var truncated = false;
        while (await reader.ReadAsync(ct))
        {
            if (rows.Count == MaxRowsPerResultSet) { truncated = true; break; }
            var row = new string[cols.Length];
            for (var c = 0; c < cols.Length; c++)
                row[c] = Cell(reader.IsDBNull(c) ? null : reader.GetValue(c));
            rows.Add(row);
        }

        md.AppendLine($"**Result set {set}** — {rows.Count}{(truncated ? "+" : "")} row(s)");
        md.AppendLine();
        md.AppendLine("| " + string.Join(" | ", cols.Select(Escape)) + " |");
        md.AppendLine("| " + string.Join(" | ", cols.Select(_ => "---")) + " |");
        foreach (var row in rows) md.AppendLine("| " + string.Join(" | ", row) + " |");
        // ponytail: hard row cap keeps the markdown paste-able; raise MaxRowsPerResultSet if needed.
        if (truncated) md.AppendLine($"\n_Output truncated at {MaxRowsPerResultSet} rows._");
    }

    private static string Cell(object? value) => value switch
    {
        null => "NULL",
        byte[] b => $"0x{Convert.ToHexString(b)}",
        DateTime d => Escape(d.ToString("yyyy-MM-dd HH:mm:ss.fff")),
        _ => Escape(value.ToString() ?? "")
    };

    private static string Escape(string s) =>
        s.Replace("|", @"\|").Replace("\r\n", "<br>").Replace("\n", "<br>").Replace("\r", "<br>");

    /// <summary>Server/database only — never echo credentials into a document meant for pasting.</summary>
    public static string Describe(string connectionString)
    {
        try
        {
            var b = new SqlConnectionStringBuilder(connectionString);
            return $"{b.DataSource} / {b.InitialCatalog}";
        }
        catch
        {
            return "(unparsable connection string)";
        }
    }
}
