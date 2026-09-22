# SqlMarkdownRunner

Blazor Server app: manage MS SQL connection strings, run SQL (GO batches supported),
get the query + results back as one markdown document to copy or download into an LLM.

- **Connections** page → saved to `connections.json` in this folder (hand-editable, gitignored).
- **Run SQL** page → pick a connection, run, copy/download the markdown. The side panel lists the database's tables, views, procedures and functions; drag a name into the editor. Listings are cached per connection in `objects.json` until you hit Refresh.
- **History** page → the last 200 runs per connection, saved to `history.json` (also gitignored). Open reloads a query into the editor.

```bash
dotnet run --project SqlMarkdownRunner.csproj
```

An UPDATE or DELETE with no WHERE clause asks for confirmation before it runs.

Self-check for the GO-splitting and WHERE-detection logic: `dotnet run -- --selftest`

Notes: credentials are never written into the markdown (server/database only).
Result sets are capped at 500 rows (`SqlRunner.MaxRowsPerResultSet`).
