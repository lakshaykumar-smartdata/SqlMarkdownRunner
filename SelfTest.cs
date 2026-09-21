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

        Console.WriteLine("self-test: OK");
    }
}
