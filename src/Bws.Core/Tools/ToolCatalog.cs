namespace Bws.Core.Tools;

/// <summary>
/// One tool: a ready-made answer to a problem somebody has, under the word <c>bws tool</c> takes for it.
///
/// <b>Only the name, and that is deliberate rather than unfinished.</b> Tools will come in shapes that
/// share almost nothing - a reading over the whole machine, a reading about one entry, a recipe that
/// ends in a plan, a procedure with state of its own (`docs/PROJEKT-TOOLS-20261020.md` section 5). What
/// they share is a place in the catalogue and a word on the command line, so that is all this holds.
/// What a tool does is ordinary code beside it, like <see cref="QuickFixes"/> - and a property added here
/// for a tool that does not exist yet would be a promise with nothing behind it.
/// </summary>
/// <param name="Name">
/// The word after <c>bws tool</c>. <b>A public contract the day it ships</b> - people paste these lines
/// into forum answers and runbooks, so it is never renamed. An alias may be added later, one may never
/// be taken away (owner's decision T-g, 2026-10-20).
/// </param>
public sealed record Tool(string Name);

/// <summary>
/// Every tool this build has, in the order both interfaces show them.
///
/// <b>ONE LIST FOR THE WINDOW AND THE COMMAND LINE</b>, and that is the whole reason it is in the core.
/// The window's Tools tab and <c>bws tool</c> read it, and a test on each side refuses a tool the other
/// cannot show - so "every tool exists in both interfaces" (the owner's decision behind the ideas
/// report) is something that fails a build rather than something somebody has to remember.
/// </summary>
public static class ToolCatalog
{
    /// <summary>
    /// Restarting one service, and what depends on it, when it got stuck - B1 of the ideas report and
    /// the first tool, owner's decision T-b of 2026-10-20.
    /// </summary>
    public static Tool Fix { get; } = new("fix");

    /// <summary>All of them, in order.</summary>
    public static IReadOnlyList<Tool> All { get; } = [Fix];

    /// <summary>
    /// The tool somebody named, or nothing. Without case, because nothing else on the command line
    /// cares about it either.
    /// </summary>
    public static Tool? Named(string name) =>
        All.FirstOrDefault(tool => string.Equals(tool.Name, name, StringComparison.OrdinalIgnoreCase));
}
