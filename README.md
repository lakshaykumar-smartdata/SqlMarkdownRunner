# SqlMarkdownRunner

Blazor Server app (MudBlazor UI): manage MS SQL connection strings, run SQL (GO batches supported),
get the query + results back as one markdown document to copy or download into an LLM.

The connection is picked once in the header; Run SQL, History and Saved runs all follow it.

- **Connections** page → saved to `connections.json` in this folder, encrypted with DPAPI for your Windows account (gitignored). Each connection carries its own query timeout, auto-save setting, a production flag (refuses anything that writes) and an active flag (hides it from the picker).
- **Run SQL** page → run, copy/download the markdown, or export the result sets as .xlsx. The side panel lists the database's tables, views, procedures and functions; drag a name into the editor. Expand a table for its columns; the eye beside a procedure or function opens its definition on its own page, where it can be copied, edited, or executed with its parameters filled in on a form. Every save keeps a .sql version under `wwwroot/object-versions/<connection>/<object>/`, and History diffs any two of them. Saving runs ALTER against the live database and backs the previous definition up to Saved runs first. Listings are cached per connection in `objects.json` until you hit Refresh.
- **Saved runs** page → every run auto-saved to `wwwroot/sql-queries/<connection>/`; browse, preview, copy or download. Turn it off per connection on the Connections page.
- **History** page → the last 200 runs per connection, saved to `history.json` (also gitignored). Open reloads a query into the editor.

```bash
dotnet run --project SqlMarkdownRunner.csproj
```

An UPDATE or DELETE with no WHERE clause asks for confirmation before it runs.

Ctrl+Enter runs the editor; Stop cancels a running query.

Self-check for the GO-splitting and WHERE-detection logic: `dotnet run -- --selftest`

Notes: credentials are never written into the markdown (server/database only).
Result sets are capped at 500 rows (`SqlRunner.MaxRowsPerResultSet`).
