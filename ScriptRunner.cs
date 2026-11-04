using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace SqlMarkdownRunner;

/// <summary>
/// Everything that wraps a run: the read-only and no-WHERE guards first, then the history entry
/// and the auto-saved markdown. The editor page and the script page both go through here, so a
/// guard tightened once covers both.
/// </summary>
public class ScriptRunner(IDialogService dialogs, HistoryStore history, SavedRuns runs)
{
    /// <param name="label">Names the auto-saved run; the subject of the SQL when nothing better.</param>
    /// <param name="checking">
    /// Raised around the EXEC check, which costs a round trip per routine — long enough that the
    /// page says what it is waiting for.
    /// </param>
    /// <returns><c>null</c> when a guard refused the script, which has already said so on screen.</returns>
    public async Task<RunResult?> RunAsync(
        DbConnectionEntry entry, string sql, string? label, bool singleTransaction,
        CancellationToken token, Action<bool>? checking = null)
    {
        if (string.IsNullOrWhiteSpace(sql)) return null;
        if (!await AllowedOnThisConnectionAsync(entry, sql, checking)) return null;
        if (!await ConfirmUnfilteredWritesAsync(sql)) return null;

        var run = await SqlRunner.RunAsMarkdownAsync(
            entry, sql, entry.TimeoutSeconds, null, token, singleTransaction);

        history.Add(entry.Name, sql);
        if (entry.AutoSaveMarkdown)
            runs.Save(entry.Name, run.Markdown, label ?? SqlRunner.SubjectOf(sql));

        return run;
    }

    /// <summary>
    /// A connection marked production refuses anything that writes, with no override. A direct
    /// INSERT/UPDATE/DELETE is caught by scanning; an EXEC is not, so what it reaches gets read.
    /// </summary>
    private async Task<bool> AllowedOnThisConnectionAsync(
        DbConnectionEntry entry, string sql, Action<bool>? checking)
    {
        if (!entry.IsProduction) return true;

        var writes = SqlRunner.WriteStatements(sql);
        if (writes.Count > 0)
        {
            await RefusedAsync(entry, $"Found: <code>{string.Join("</code>, <code>", writes)}</code>");
            return false;
        }

        if (SqlRunner.UsesDynamicSql(sql))
        {
            await RefusedAsync(entry, "This script builds SQL as it runs, so what it would do "
                                      + "cannot be known before running it.");
            return false;
        }

        if (SqlRunner.ExecutedRoutines(sql).Count == 0) return true;

        // Reading the called routines costs a round trip each, so it waits until there is an EXEC.
        checking?.Invoke(true);
        List<SqlRunner.RoutineWrite> reached;
        try
        {
            reached = await SqlRunner.ExecWritesAsync(entry, sql);
        }
        catch (Exception ex)
        {
            await RefusedAsync(entry, $"What this EXEC reaches could not be checked: {ex.Message}");
            return false;
        }
        finally
        {
            checking?.Invoke(false);
        }

        if (reached.Count == 0) return true;

        var listed = string.Join("<br>", reached.Select(r => $"<code>{r.Routine}</code> — {r.Reason}"));
        await RefusedAsync(entry, $"This EXEC reaches code that writes:<br><br>{listed}");
        return false;
    }

    private Task RefusedAsync(DbConnectionEntry entry, string detail) =>
        dialogs.ShowMessageBoxAsync(
            "This connection is read-only",
            (MarkupString)($"<strong>{entry.Name}</strong> is marked as production, so it only runs "
                           + $"queries that read.<br><br>{detail}"
                           + "<br><br>Turn Production off on the Connections page if that is wrong."),
            yesText: "OK");

    /// <summary>An UPDATE or DELETE with no WHERE hits every row, so make the user say yes first.</summary>
    private async Task<bool> ConfirmUnfilteredWritesAsync(string sql)
    {
        var risky = SqlRunner.StatementsMissingWhere(sql);
        if (risky.Count == 0) return true;

        var what = risky.Count == 1 ? "This statement has" : $"These {risky.Count} statements have";
        var message = $"{what} no WHERE clause, so every row in the target table will be affected:";
        var listed = string.Join("<br>", risky.Select(r => $"<code>{r}</code>"));

        var ok = await dialogs.ShowMessageBoxAsync(
            "Run without a WHERE clause?",
            (MarkupString)$"{message}<br><br>{listed}",
            yesText: "Run anyway", cancelText: "Cancel");

        return ok == true;
    }
}
