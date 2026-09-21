using System.Diagnostics;

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

        Console.WriteLine("self-test: OK");
    }
}
