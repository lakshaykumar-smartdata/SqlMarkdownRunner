using ClosedXML.Excel;

namespace SqlMarkdownRunner;

/// <summary>Result sets as an .xlsx, one worksheet each.</summary>
public static class ResultWorkbook
{
    public static byte[] Build(IReadOnlyList<ResultSet> sets)
    {
        using var workbook = new XLWorkbook();

        foreach (var set in sets)
        {
            var sheet = workbook.Worksheets.Add(SheetName(workbook, set.Title));

            for (var c = 0; c < set.Columns.Count; c++)
                sheet.Cell(1, c + 1).Value = set.Columns[c];

            for (var r = 0; r < set.Rows.Count; r++)
            for (var c = 0; c < set.Columns.Count; c++)
            {
                var text = set.Rows[r][c];
                // NULL is written by the runner as the literal word; an empty cell says it better.
                if (text != "NULL") sheet.Cell(r + 2, c + 1).SetValue(text);
            }

            sheet.Row(1).Style.Font.Bold = true;
            sheet.SheetView.FreezeRows(1);
            if (set.Columns.Count > 0 && set.Rows.Count > 0)
                sheet.Range(1, 1, set.Rows.Count + 1, set.Columns.Count).SetAutoFilter();
            sheet.Columns().AdjustToContents(1, 50);
        }

        if (!workbook.Worksheets.Any()) workbook.Worksheets.Add("No results");

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Excel caps sheet names at 31 characters and will not take a duplicate.</summary>
    private static string SheetName(XLWorkbook workbook, string title)
    {
        var name = new string(title.Where(c => !"[]:*?/\\".Contains(c)).ToArray());
        if (name.Length > 31) name = name[..31];

        var unique = name;
        for (var n = 2; workbook.Worksheets.Any(w => w.Name == unique); n++)
            unique = $"{name[..Math.Min(name.Length, 28)]}-{n}";

        return unique;
    }
}
