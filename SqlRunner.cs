using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace SqlMarkdownRunner;

public record DbColumn(string Name, string Type);

public record DbObject(string Kind, string Name, string DragText, List<DbColumn> Columns);

public partial class SqlRunner
{
    // ponytail: line-level GO split; a literal "GO" alone on a line inside a string/block comment
    // would split wrongly. Swap for a real T-SQL tokenizer only if that ever bites.
    [GeneratedRegex(@"^[\t ]*GO[\t ]*(?<count>\d+)?[\t ]*(--.*)?\r?$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex GoSeparator();

    public const int MaxRowsPerResultSet = 500;

    // Blanked out before scanning so a "where" inside a comment or a string literal doesn't count.
    [GeneratedRegex(@"--[^\r\n]*|/\*.*?\*/|N?'(?:[^']|'')*'", RegexOptions.Singleline)]
    private static partial Regex CommentsAndLiterals();

    [GeneratedRegex(@"\b(update|delete)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Mutations();

    [GeneratedRegex(@"\b(select|insert|update|delete|merge|create|alter|drop|truncate|print|declare|exec|execute|commit|rollback|use|go|begin|if|while)\b|;", RegexOptions.IgnoreCase)]
    private static partial Regex StatementBoundary();

    [GeneratedRegex(@"\bwhere\b", RegexOptions.IgnoreCase)]
    private static partial Regex Where();

    /// <summary>
    /// UPDATE/DELETE statements in the script that carry no WHERE clause, summarised for a prompt.
    /// </summary>
    public static List<string> StatementsMissingWhere(string sql)
    {
        // Same length, so offsets still line up with the original text for the summary below.
        var clean = CommentsAndLiterals().Replace(sql, m => new string(' ', m.Length));

        var found = new List<string>();
        foreach (Match m in Mutations().Matches(clean))
        {
            // ponytail: the statement ends at the next statement keyword, so a subquery in a SET
            // clause cuts it short and costs one extra confirmation. Erring towards asking.
            var next = StatementBoundary().Match(clean, m.Index + m.Length);
            var end = next.Success ? next.Index : clean.Length;

            if (!Where().IsMatch(clean[m.Index..end]))
                found.Add(Summarise(sql[m.Index..end]));
        }
        return found;
    }

    private static string Summarise(string statement)
    {
        var oneLine = string.Join(' ', statement.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return oneLine.Length <= 120 ? oneLine : oneLine[..120] + "…";
    }

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

            // Cumulative across the batch, and -1 when nothing was modified. Report it even when
            // the batch also returned rows, otherwise a write loop that ends in a SELECT looks idle.
            var affected = reader.RecordsAffected;
            if (set == 0 || affected > 0)
            {
                md.AppendLine();   // a line butted against the table rows breaks the table
                md.AppendLine($"**{Math.Max(affected, 0):N0} row(s) affected.**");
            }
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

    /// <summary>Tables, views, procedures and functions in the database, for the object panel.</summary>
    public static async Task<List<DbObject>> ListObjectsAsync(DbConnectionEntry entry, CancellationToken ct = default)
    {
        const string sql = """
            SELECT o.object_id, o.type, s.name AS schema_name, o.name
            FROM sys.objects o
            JOIN sys.schemas s ON s.schema_id = o.schema_id
            WHERE o.type IN ('U', 'V', 'P', 'FN', 'IF', 'TF') AND o.is_ms_shipped = 0
            ORDER BY s.name, o.name;

            SELECT p.object_id, p.name, t.name AS type_name, p.max_length, p.precision, p.scale, p.is_output
            FROM sys.parameters p
            JOIN sys.types t ON t.user_type_id = p.user_type_id
            WHERE p.parameter_id > 0
            ORDER BY p.object_id, p.parameter_id;

            SELECT c.object_id, c.name, t.name AS type_name, c.max_length, c.precision, c.scale
            FROM sys.columns c
            JOIN sys.objects o ON o.object_id = c.object_id
            JOIN sys.types t ON t.user_type_id = c.user_type_id
            WHERE o.type IN ('U', 'V') AND o.is_ms_shipped = 0
            ORDER BY c.object_id, c.column_id;
            """;

        await using var conn = new SqlConnection(entry.ConnectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var rows = new List<(int Id, string Type, string Name)>();
        while (await reader.ReadAsync(ct))
            rows.Add((reader.GetInt32(0), reader.GetString(1).TrimEnd(), $"{reader.GetString(2)}.{reader.GetString(3)}"));

        var parameters = new Dictionary<int, List<Param>>();
        await reader.NextResultAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetInt32(0);
            if (!parameters.TryGetValue(id, out var list)) parameters[id] = list = [];
            list.Add(new Param(
                reader.GetString(1),
                FormatType(reader.GetString(2), reader.GetInt16(3), reader.GetByte(4), reader.GetByte(5)),
                reader.GetBoolean(6)));
        }

        var columns = new Dictionary<int, List<DbColumn>>();
        await reader.NextResultAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetInt32(0);
            if (!columns.TryGetValue(id, out var list)) columns[id] = list = [];
            list.Add(new DbColumn(
                reader.GetString(1),
                FormatType(reader.GetString(2), reader.GetInt16(3), reader.GetByte(4), reader.GetByte(5))));
        }

        return rows
            .Select(r => new DbObject(
                Kind(r.Type),
                r.Name,
                CallTemplate(r.Type, r.Name, parameters.GetValueOrDefault(r.Id, [])),
                columns.GetValueOrDefault(r.Id, [])))
            .ToList();
    }

    /// <summary>What dropping the name into the editor writes: a runnable call, parameters included.</summary>
    internal static string CallTemplate(string type, string name, List<Param> parameters)
    {
        if (type is "U" or "V") return name;

        var signature = string.Join(", ", parameters.Select(p => $"{p.Name} {p.Type}"));

        if (type is "FN" or "IF" or "TF")
        {
            var call = type == "FN" ? $"SELECT {name}(" : $"SELECT * FROM {name}(";
            var args = string.Join(", ", parameters.Select(_ => "NULL"));
            return $"-- {name}({signature})\n{call}{args})";
        }

        if (parameters.Count == 0) return $"EXEC {name}";

        // One parameter per line, NULL so it runs as-is once the values are filled in. The comma
        // has to sit before the type comment, or the next line ends up commented out.
        var assignments = parameters.Select((p, i) =>
            $"    {p.Name} = NULL{(p.IsOutput ? " OUTPUT" : "")}{(i < parameters.Count - 1 ? "," : "")}  -- {p.Type}");

        return $"EXEC {name}\n{string.Join("\n", assignments)}";
    }

    private static string FormatType(string type, short maxLength, byte precision, byte scale) => type switch
    {
        "varchar" or "char" or "varbinary" or "binary" => $"{type}({(maxLength == -1 ? "max" : maxLength.ToString())})",
        "nvarchar" or "nchar" => $"{type}({(maxLength == -1 ? "max" : (maxLength / 2).ToString())})",
        "decimal" or "numeric" => $"{type}({precision},{scale})",
        _ => type
    };

    internal sealed record Param(string Name, string Type, bool IsOutput);

    private static string Kind(string type) => type switch
    {
        "U" => "Tables",
        "V" => "Views",
        "P" => "Stored procedures",
        _ => "Functions"
    };

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
