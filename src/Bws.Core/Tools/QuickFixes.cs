using Bws.Core.Planning;

namespace Bws.Core.Tools;

/// <summary>
/// One quick fix: a restart of one service together with what depends on it, under a name somebody
/// with the problem would use - `docs/03` part 2.
///
/// <b>It does not build a plan, and that is the one rule worth knowing about it.</b> It turns into the
/// same ask <c>bws restart NAME --dependents</c> makes, and the ordinary <see cref="PlanBuilder"/> takes
/// it from there - the cascade, the warnings, the refusals and the way back are all the plan's. A fix
/// with a write path of its own would be the thing rule 1 of the project notes forbids.
/// </summary>
/// <param name="Name">
/// The word after <c>bws tool fix</c>. A public contract once shipped, like <see cref="Tool.Name"/>.
/// </param>
/// <param name="ServiceName">
/// The entry it restarts, by the manager's own name - `ADR-14`. Never a display name: those are
/// translated, and a fix has to mean the same thing on every Windows.
/// </param>
public sealed record QuickFix(string Name, string ServiceName)
{
    /// <summary>
    /// What this fix asks for: a restart, with the entries standing on this one taken down first and
    /// brought back after.
    ///
    /// <b>With dependants because a restart without them is refused</b> whenever something running
    /// depends on the entry - the manager will not stop it under them - and a fix that answers "no" on
    /// the machines that have the problem is no fix. The plan names every one of them before anything
    /// moves, and puts back only what it took down.
    /// </summary>
    public ServiceAction Ask => new(ActionKind.Restart, ServiceName, IncludeDependents: true);

    /// <summary>
    /// The line that carries this fix out from a terminal - <c>bws tool fix printer</c> - which the window's
    /// card shows small at its foot (owner's decision T-h), so the card teaches the command it stands for.
    /// </summary>
    public string Typed => EquivalentCommand.ForTool(ToolCatalog.Fix.Name, Name);
}

/// <summary>
/// The quick fixes this build carries - four, the owner's decision T-f of 2026-10-20.
///
/// <b>EACH ONE STANDS ON A MICROSOFT PAGE, NOT ON A FORUM ANSWER</b>, and the page is named beside it.
/// A fix tells somebody "this should help", which is a claim about the world, and rule 3 of the project
/// notes asks such a claim to carry its source. The sources were read on 2026-10-20 and the table of what
/// each page actually says - which is sometimes less than a fix would like it to say - is in
/// `docs/PROJEKT-TOOLS-20261020.md` section 4. The sentence on screen says what the page says and no more.
///
/// <b>A TABLE IN CODE RATHER THAN A DATA FILE</b>, and that was weighed: a fix is one line here and two
/// sentences in each language file (rule 13 keeps every sentence there), and a file would add a parser
/// and a schema to that and remove nothing. A guard holds each fix to having its words in both
/// interfaces and a name nobody else has.
///
/// <b>WHAT WAS LEFT OUT, SO IT IS NOT ADDED BACK BY SOMEBODY READING THE IDEAS REPORT:</b> Bluetooth
/// (no Microsoft page says to restart its service - only forum answers do), WMI (on a machine with
/// Hyper-V its dependants include the virtual machine management service) and the clock (the report
/// itself says the fix is <c>w32tm /resync</c>, which this does not do).
/// </summary>
public static class QuickFixes
{
    /// <summary>
    /// The printer does not print, or jobs are stuck in its queue.
    ///
    /// learn.microsoft.com/troubleshoot/windows-server/printing/printer-not-responding, step 7: "Restart the
    /// Print Spooler service to clear any stuck print jobs".
    /// </summary>
    public static QuickFix Printer { get; } = new("printer", "Spooler");

    /// <summary>
    /// No sound, with Windows saying the audio services are not responding.
    ///
    /// learn.microsoft.com/windows-hardware/drivers/audio/usb-audio-not-playing: "Both the Windows Audio and the
    /// Windows Audio End Point Builder services must be running for audio to work correctly." <b>The page says
    /// both must RUN, not that a restart repairs anything</b>, and the sentence on screen says exactly that.
    /// Audiosrv depends on the endpoint builder, so the restart takes it along.
    /// </summary>
    public static QuickFix Sound { get; } = new("sound", "AudioEndpointBuilder");

    /// <summary>
    /// Search does not find files, because indexing is paused.
    ///
    /// learn.microsoft.com/troubleshoot/windows-client/shell-experience/windows-search-performance-issues: with
    /// indexing paused, "To resume indexing more quickly, restart the Windows Search service (wsearch)". <b>The
    /// index of files, NOT the search box on the taskbar</b> - Microsoft's page on fixing Windows Search
    /// (solution 4) says that box is a process, SearchHost.exe, which no service restart reaches.
    /// </summary>
    public static QuickFix Indexing { get; } = new("indexing", "WSearch");

    /// <summary>
    /// The search box does not respond.
    ///
    /// learn.microsoft.com/troubleshoot/windows-client/shell-experience/fix-problems-in-windows-search, solution 1:
    /// "Sometimes, you can resolve Windows Search issues by restarting Windows Font Cache Service." Surprising,
    /// and the first thing Microsoft's own page says.
    /// </summary>
    public static QuickFix Search { get; } = new("search", "FontCache");

    /// <summary>
    /// Every fix, in the order both interfaces show them.
    /// </summary>
    public static IReadOnlyList<QuickFix> All { get; } = [Printer, Sound, Indexing, Search];

    /// <summary>
    /// The fix somebody named, or nothing. Without case, like every other word on the command line.
    /// </summary>
    public static QuickFix? Named(string name) =>
        All.FirstOrDefault(fix => string.Equals(fix.Name, name, StringComparison.OrdinalIgnoreCase));
}
