using Markdig;
using Microsoft.AspNetCore.Components;

namespace SqlMarkdownRunner;

/// <summary>Shared so the run output and a saved file render identically.</summary>
public static class MarkdownView
{
    // Tables need the pipe-table extension; DisableHtml keeps any markup that came out of the
    // database from executing in the browser.
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UsePipeTables().DisableHtml().Build();

    public static MarkupString Render(string markdown) =>
        // DisableHtml escapes everything, so put back only the <br> we emit for multi-line cell
        // values. A cell whose text is literally "<br>" renders as a break.
        (MarkupString)Markdown.ToHtml(markdown, Pipeline).Replace("&lt;br&gt;", "<br>");
}
