using Bws.Core.Tools;

namespace Bws.Cli;

/// <summary>
/// The two lists under <c>bws tool</c> - the tools, and the quick fixes - as text for a person.
///
/// <b>Answered without the machine</b>, like help and the licence: both lists come out of the build
/// itself, so neither waits for a reading of eight hundred entries it would not look at (Immediate).
///
/// <b>Text only.</b> The switch table says why there is no JSON of these yet, at --json.
/// </summary>
internal static class ToolText
{
    /// <summary>The tools, one line each - the same list, in the same order, the window's Tools tab shows.</summary>
    internal static string Tools() =>
        Lines(Texts.Of("cli.tool.heading"), ToolCatalog.All.Select(tool => (tool.Name, Said(tool))), Texts.Of("cli.tool.next"));

    /// <summary>
    /// The quick fixes, one line each: the word to type, the service it restarts and the problem it is for.
    ///
    /// <b>The service is on the line</b>, because an administrator deciding whether to run one wants to know
    /// what it touches before reading what it is for - and it is the name `bws restart` would take.
    /// </summary>
    internal static string Fixes()
    {
        var widest = QuickFixes.All.Max(fix => fix.ServiceName.Length);

        return Lines(
            Texts.Of("cli.fix.heading"),
            QuickFixes.All.Select(fix => (fix.Name, fix.ServiceName.PadRight(widest) + "   " + Said(fix))),
            Texts.Of("cli.fix.next"));
    }

    /// <summary>A heading, a blank line, the rows with their first column lined up, a blank line, the way on.</summary>
    private static string Lines(string heading, IEnumerable<(string Word, string Said)> rows, string next)
    {
        var listed = rows.ToList();
        var widest = listed.Max(row => row.Word.Length);

        return string.Join(
            Environment.NewLine,
            [heading, string.Empty, .. listed.Select(row => "  " + row.Word.PadRight(widest) + "   " + row.Said), string.Empty, next]);
    }

    /// <summary>
    /// What a tool is for, in one line. An arm per tool, and the default refuses: a tool in the catalogue
    /// without a line here would print nothing beside its name - ToolCommandTests walks the catalogue.
    /// </summary>
    private static string Said(Tool tool) =>
        tool == ToolCatalog.Fix
            ? Texts.Of("cli.tool.fix")
            : throw new ArgumentOutOfRangeException(nameof(tool), tool.Name, "No line is written for this tool.");

    /// <summary>What a fix is for, as the problem somebody has. An arm per fix, refusing the rest, as above.</summary>
    private static string Said(QuickFix fix) =>
        fix == QuickFixes.Printer ? Texts.Of("cli.fix.printer")
        : fix == QuickFixes.Sound ? Texts.Of("cli.fix.sound")
        : fix == QuickFixes.Indexing ? Texts.Of("cli.fix.indexing")
        : fix == QuickFixes.Search ? Texts.Of("cli.fix.search")
        : throw new ArgumentOutOfRangeException(nameof(fix), fix.Name, "No line is written for this fix.");
}
