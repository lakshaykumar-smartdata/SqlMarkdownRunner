# SqlMarkdownRunner

Blazor Server app: manage MS SQL connection strings, run SQL (GO batches supported),
get the query + results back as one markdown document to copy or download into an LLM.

- **Connections** page → saved to `connections.json` in this folder (hand-editable, gitignored).
- **Run SQL** page → pick a connection, run, copy/download the markdown.

```bash
dotnet run --project SqlMarkdownRunner.csproj
```

Self-check for the GO-splitting logic: `dotnet run -- --selftest`

Notes: credentials are never written into the markdown (server/database only).
Result sets are capped at 500 rows (`SqlRunner.MaxRowsPerResultSet`).
