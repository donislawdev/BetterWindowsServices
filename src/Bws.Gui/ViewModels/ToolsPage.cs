using Bws.Core.Tools;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What the Tools tab holds: a section per tool, and in it the cards somebody can press - the window half
/// of <see cref="ToolCatalog"/>, `docs/PROJEKT-TOOLS-20261020.md`.
///
/// <b>Knows nothing about WPF</b>, so a test can ask what the page says and which cards can be pressed
/// without opening a window. Whether the page is ON SCREEN is not here but in the bar of tabs
/// (<see cref="Tabs"/>), because putting it there or away moves the rest of the window.
///
/// <b>A section per tool in the catalogue, built by an arm per tool</b>, and an arm that is missing
/// refuses rather than drawing a blank section - the window's half of "every tool in both interfaces",
/// held by a test walking the catalogue. The command line holds the other half the same way.
/// </summary>
public sealed class ToolsPage
{
    private readonly Func<bool> _everRead;
    private readonly Func<bool> _failed;
    private readonly Func<string, bool> _has;

    /// <param name="everRead">Whether any reading of the machine has arrived.</param>
    /// <param name="failed">Whether the last reading failed outright.</param>
    /// <param name="has">Whether the reading holds an entry of this name.</param>
    /// <param name="needsRights">
    /// Whether this window runs without administrator rights, which every fix needs at its foot - asked
    /// rather than handed over, for the reason Readings gives about Says: a test replaces it after this runs.
    /// </param>
    internal ToolsPage(Func<bool> everRead, Func<bool> failed, Func<string, bool> has, Func<bool> needsRights)
    {
        _everRead = everRead;
        _failed = failed;
        _has = has;
        Sections = [.. ToolCatalog.All.Select(tool => SectionFor(tool, WhyNot, needsRights))];
    }

    /// <summary>The sections, in the catalogue's order.</summary>
    public IReadOnlyList<ToolSection> Sections { get; }

    /// <summary>
    /// Asks every card again whether it can be pressed - after a reading, because what decides it is
    /// whether the machine has the entry, and a reading is when that can change.
    /// </summary>
    internal void Rethink()
    {
        foreach (var card in Sections.SelectMany(section => section.Cards))
        {
            card.Rethink();
        }
    }

    /// <summary>
    /// Why a card for this entry cannot be pressed, or nothing when it can.
    ///
    /// <b>Before the first reading, because the plan looks entries up in what was read</b> - MainViewModel.PlanAsync
    /// hands it every row the window holds - so a card pressed during the first second would be told the
    /// machine has no such service. <b>And when the reading has no such entry</b>, which is a real machine: a
    /// Server core installation without the audio services, a virtual machine without a font cache.
    ///
    /// <b>A first reading that FAILED is not "waiting"</b> - it is a window with nothing to plan against until a
    /// reading succeeds, and the card says that rather than promising a reading that is not coming.
    /// </summary>
    private string? WhyNot(string serviceName)
    {
        if (!_everRead())
        {
            return _failed() ? Texts.Of("gui.tools.unavailable.unread") : Texts.Of("gui.tools.unavailable.reading");
        }

        return _has(serviceName) ? null : Texts.Of("gui.tools.unavailable.missing", serviceName);
    }

    private static ToolSection SectionFor(Tool tool, Func<string, string?> unavailable, Func<bool> needsRights) =>
        tool == ToolCatalog.Fix
            ? new ToolSection(
                Texts.Of("gui.tools.fix.heading"),
                Texts.Of("gui.tools.fix.what"),
                [.. QuickFixes.All.Select(fix => new ToolCard(fix, unavailable, needsRights))])
            : throw new ArgumentOutOfRangeException(nameof(tool), tool.Name, "No section is drawn for this tool.");
}

/// <summary>
/// One tool on the page: its heading, what it is for, and its cards. Public because WPF cannot see an
/// internal type from outside this assembly - the reason <see cref="PlanLine"/> gives.
/// </summary>
public sealed class ToolSection(string heading, string what, IReadOnlyList<ToolCard> cards)
{
    public string Heading { get; } = heading;

    public string What { get; } = what;

    public IReadOnlyList<ToolCard> Cards { get; } = cards;
}

/// <summary>
/// One quick fix as a card: the problem as its title, what pressing it does, the line that does the same
/// from a terminal, and whether it can be pressed now.
///
/// <b>Pressing it opens the ordinary plan sheet and nothing else</b> - the same sheet the list's Restart
/// button opens, with the dependants already asked for and a sentence saying where it came from. What
/// changes the machine is still the one button at the foot of that sheet (rule 1 of the project notes).
/// </summary>
public sealed class ToolCard : Observable
{
    private readonly Func<string, string?> _unavailable;
    private readonly Func<bool> _needsRights;

    internal ToolCard(QuickFix fix, Func<string, string?> unavailable, Func<bool> needsRights)
    {
        Fix = fix;
        _unavailable = unavailable;
        _needsRights = needsRights;
        Title = TitleOf(fix);
    }

    /// <summary>The fix behind the card.</summary>
    internal QuickFix Fix { get; }

    /// <summary>The problem, as somebody who has it would say it.</summary>
    public string Title { get; }

    /// <summary>What pressing it does, and - where Microsoft's page says less than a fix would like - only what the page says.</summary>
    public string What => WhatOf(Fix);

    /// <summary>The same thing from a terminal, small at the card's foot (owner's decision T-h).</summary>
    public string Command => Fix.Typed;

    /// <summary>
    /// What pressing it will and will not do to the machine, as the ideas report asked every card to say:
    /// it changes something, the plan comes first, and - in a window without the rights - that carrying it
    /// out needs an administrator. Said before the press rather than discovered on the sheet.
    /// </summary>
    public string Effect => _needsRights() ? Texts.Of("gui.tools.fix.effect.unelevated") : Texts.Of("gui.tools.fix.effect");

    /// <summary>Whether it can be pressed now. When it cannot, <see cref="Hint"/> says why.</summary>
    public bool Available => _unavailable(Fix.ServiceName) is null;

    /// <summary>
    /// What the card says to somebody pointing at it: what will open, or why nothing can. Shown on a grey
    /// card too - ShowOnDisabled, `docs/10` trap 14 - because a reason that only exists while the card is
    /// live is a reason nobody reads.
    /// </summary>
    public string Hint => _unavailable(Fix.ServiceName) ?? Texts.Of("gui.tools.fix.hint", Fix.ServiceName);

    /// <summary>
    /// What the card asks for: a restart of its service with the dependants, opened on a sheet of its own
    /// with a sentence saying it came from this card - the sheet has no other way to know.
    /// </summary>
    internal NamedAsk Ask => new(Fix.Ask.Kind, Fix.ServiceName, string.Empty, Texts.Of("gui.tools.fix.because", Title), Fix.Ask.IncludeDependents);

    internal void Rethink()
    {
        Raise(nameof(Available));
        Raise(nameof(Hint));
    }

    /// <summary>
    /// An arm per fix with its key written out - a key chosen into a variable is invisible to the guard
    /// over text keys, the reason PlanWords gives - and a fix without words here refuses rather than
    /// drawing a card with nothing on it.
    /// </summary>
    private static string TitleOf(QuickFix fix) =>
        fix == QuickFixes.Printer ? Texts.Of("gui.tools.fix.printer")
        : fix == QuickFixes.Sound ? Texts.Of("gui.tools.fix.sound")
        : fix == QuickFixes.Indexing ? Texts.Of("gui.tools.fix.indexing")
        : fix == QuickFixes.Search ? Texts.Of("gui.tools.fix.search")
        : throw new ArgumentOutOfRangeException(nameof(fix), fix.Name, "No title is written for this fix.");

    private static string WhatOf(QuickFix fix) =>
        fix == QuickFixes.Printer ? Texts.Of("gui.tools.fix.printer.what")
        : fix == QuickFixes.Sound ? Texts.Of("gui.tools.fix.sound.what")
        : fix == QuickFixes.Indexing ? Texts.Of("gui.tools.fix.indexing.what")
        : fix == QuickFixes.Search ? Texts.Of("gui.tools.fix.search.what")
        : throw new ArgumentOutOfRangeException(nameof(fix), fix.Name, "No sentence is written for this fix.");
}
