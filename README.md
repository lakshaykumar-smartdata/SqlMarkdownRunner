# SqlMarkdownRunner

Blazor Server app (MudBlazor UI): manage MS SQL connection strings, run SQL (GO batches supported),
get the query + results back as one markdown document to copy or download into an LLM.

The connection is picked once in the header; Run SQL, History and Saved runs all follow it.

- **Connections** page → saved to `connections.json` in this folder (hand-editable, gitignored). Each connection carries its own query timeout and auto-save setting.
- **Run SQL** page → run, copy/download the markdown. The side panel lists the database's tables, views, procedures and functions; drag a name into the editor. Listings are cached per connection in `objects.json` until you hit Refresh.
- **Saved runs** page → every run auto-saved to `wwwroot/sql-queries/<connection>/`; browse, preview, copy or download. Turn it off per connection on the Connections page.
- **History** page → the last 200 runs per connection, saved to `history.json` (also gitignored). Open reloads a query into the editor.

```bash
dotnet run --project SqlMarkdownRunner.csproj
```

An UPDATE or DELETE with no WHERE clause asks for confirmation before it runs.

Self-check for the GO-splitting and WHERE-detection logic: `dotnet run -- --selftest`

Notes: credentials are never written into the markdown (server/database only).
Result sets are capped at 500 rows (`SqlRunner.MaxRowsPerResultSet`).
