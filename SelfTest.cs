using System.Diagnostics;
using System.Text.Json;
using ClosedXML.Excel;

namespace SqlMarkdownRunner;

/// <summary>`dotnet run -- --selftest` — covers the only non-trivial pure logic: GO batch splitting.</summary>
public static class SelfTest
{
    public static void Run()
    {
        string[] Split(string sql) => SqlRunner.SplitBatches(sql).ToArray();

        Debug.Assert(Split("select 1").Length == 1);
        Debug.Assert(Split("select 1\nGO\nselect 2").SequenceEqual(["select 1", "select 2"]));
        Debug.Assert(Split("select 1\r\n  go  \r\nselect 2").SequenceEqual(["select 1", "select 2"]));
        Debug.Assert(Split("select 1\nGO\n").SequenceEqual(["select 1"]));           // trailing GO
        Debug.Assert(Split("select 1\nGO 3\n").Length == 3);                          // GO <count>
        Debug.Assert(Split("select 1\nGO -- comment\nselect 2").Length == 2);
        Debug.Assert(Split("select 'GOING'").Length == 1);                            // GO must own the line
        Debug.Assert(Split("select 1\nGO\nGO\nselect 2").SequenceEqual(["select 1", "select 2"]));
        Debug.Assert(Split("   \n  \n").Length == 0);

        int Risky(string sql) => SqlRunner.StatementsMissingWhere(sql).Count;

        Debug.Assert(Risky("select * from t") == 0);
        Debug.Assert(Risky("delete from t") == 1);
        Debug.Assert(Risky("DELETE FROM t") == 1);                                 // case
        Debug.Assert(Risky("delete from t where id = 1") == 0);
        Debug.Assert(Risky("update t set a = 1") == 1);
        Debug.Assert(Risky("update t set a = 1 where id = 2") == 0);
        Debug.Assert(Risky("update t set a = 1 -- where id = 2") == 1);            // comment is not a WHERE
        Debug.Assert(Risky("delete from t /* where id=1 */") == 1);
        Debug.Assert(Risky("delete from t where note = 'where'") == 0);
        Debug.Assert(Risky("insert into t values ('delete from x')") == 0);        // literal is not a statement
        Debug.Assert(Risky("update a set x=1 where id=1; delete from b") == 1);    // only the second one
        Debug.Assert(Risky("delete from a\nupdate b set x=1 where id=1") == 1);    // only the first one
        Debug.Assert(Risky("delete from a\nGO\ndelete from b") == 2);
        Debug.Assert(SqlRunner.StatementsMissingWhere("delete   from\n  t").Single() == "delete from t");

        var noParams = SqlRunner.CallTemplate("P", "dbo.usp_Nightly", []);
        Debug.Assert(noParams == "EXEC dbo.usp_Nightly");

        var twoParams = SqlRunner.CallTemplate("P", "dbo.usp_Find", [
            new SqlRunner.Param("@Id", "int", false),
            new SqlRunner.Param("@Name", "nvarchar(50)", false)]);
        // Every line but the last needs its comma BEFORE the comment, or the next line is commented out.
        Debug.Assert(twoParams == "EXEC dbo.usp_Find\n    @Id = NULL,  -- int\n    @Name = NULL  -- nvarchar(50)");
        Debug.Assert(!twoParams.TrimEnd().EndsWith(","));

        var output = SqlRunner.CallTemplate("P", "dbo.usp_Out", [new SqlRunner.Param("@Total", "int", true)]);
        Debug.Assert(output == "EXEC dbo.usp_Out\n    @Total = NULL OUTPUT  -- int");

        Debug.Assert(SqlRunner.CallTemplate("U", "dbo.Staff", []) == "dbo.Staff");
        Debug.Assert(SqlRunner.CallTemplate("TF", "dbo.fn_Rows", [new SqlRunner.Param("@a", "int", false)])
            == "-- dbo.fn_Rows(@a int)\nSELECT * FROM dbo.fn_Rows(NULL)");

        // An existing connections.json predates the timeout and auto-save fields, so the
        // record defaults have to survive deserialisation rather than coming back as 0/false.
        var legacy = JsonSerializer.Deserialize<DbConnectionEntry>(
            """{"Name":"a","ConnectionString":"b"}""")!;
        Debug.Assert(legacy.TimeoutSeconds == 60, "timeout default lost on deserialise");
        Debug.Assert(legacy.AutoSaveMarkdown, "auto-save default lost on deserialise");

        Debug.Assert(SqlRunner.ToAlter("CREATE PROCEDURE dbo.x AS SELECT 1")
            == "ALTER PROCEDURE dbo.x AS SELECT 1");
        Debug.Assert(SqlRunner.ToAlter("ALTER PROCEDURE dbo.x AS SELECT 1")
            == "ALTER PROCEDURE dbo.x AS SELECT 1");            // nothing to swap
        Debug.Assert(SqlRunner.ToAlter("-- CREATE me later\nCREATE PROC dbo.x AS SELECT 1")
            == "-- CREATE me later\nALTER PROC dbo.x AS SELECT 1");   // the comment is left alone
        Debug.Assert(SqlRunner.ToAlter("/* CREATE */ CREATE FUNCTION dbo.f() RETURNS int AS BEGIN RETURN 1 END")
            == "/* CREATE */ ALTER FUNCTION dbo.f() RETURNS int AS BEGIN RETURN 1 END");
        Debug.Assert(SqlRunner.ToAlter("CREATE PROC dbo.x AS SELECT NCREATE").EndsWith("SELECT NCREATE"));

        Debug.Assert(SqlRunner.WriteStatements("SELECT * FROM t").Count == 0);
        Debug.Assert(SqlRunner.WriteStatements("EXEC dbo.usp_Read @id = 1").Count == 0);   // cannot be judged
        Debug.Assert(SqlRunner.WriteStatements("UPDATE t SET a = 1").Single() == "UPDATE");
        Debug.Assert(SqlRunner.WriteStatements("-- update nothing\nSELECT 1").Count == 0);
        Debug.Assert(SqlRunner.WriteStatements("SELECT QdropQ AS x".Replace("Q", "'")).Count == 0);
        Debug.Assert(SqlRunner.WriteStatements("DELETE FROM a; INSERT INTO b VALUES (1)").Count == 2);

        var legacyFlags = JsonSerializer.Deserialize<DbConnectionEntry>(
            """{"Name":"a","ConnectionString":"b"}""")!;
        Debug.Assert(!legacyFlags.IsProduction, "a connection must not become production by default");
        Debug.Assert(legacyFlags.IsActive, "an existing connection must stay visible");

        // The workbook is read back rather than just checked for a zip header.
        var sheetA = new ResultSet("Batch 1 result 1", ["id", "name"],
            [["1", "Ada"], ["2", "NULL"]]);
        var sheetB = new ResultSet("Batch 1 result 1", ["only"], [["x"]]);   // same title twice

        using var book = new XLWorkbook(new MemoryStream(ResultWorkbook.Build([sheetA, sheetB])));

        Debug.Assert(book.Worksheets.Count == 2);
        Debug.Assert(book.Worksheet(1).Name == "Batch 1 result 1");
        Debug.Assert(book.Worksheet(2).Name != book.Worksheet(1).Name, "sheet names must be unique");
        Debug.Assert(book.Worksheet(1).Cell("A1").GetString() == "id");
        Debug.Assert(book.Worksheet(1).Cell("B2").GetString() == "Ada");
        Debug.Assert(book.Worksheet(1).Cell("B3").IsEmpty(), "a NULL belongs in an empty cell");
        Debug.Assert(book.Worksheet(1).Row(1).Style.Font.Bold);

        // A DDL column holds far more than Excel allows in one cell; it is trimmed, not thrown on.
        var huge = new ResultSet("ddl", ["ddl"], [[new string('x', 40000)]]);
        using var trimmed = new XLWorkbook(new MemoryStream(ResultWorkbook.Build([huge])));
        var cell = trimmed.Worksheet(1).Cell("A2").GetString();

        Debug.Assert(cell.Length == 32_767, "a cell must not exceed what Excel accepts");
        Debug.Assert(cell.EndsWith("[truncated]"), "trimming must be visible, not silent");

        Console.WriteLine("self-test: OK");
    }
}
