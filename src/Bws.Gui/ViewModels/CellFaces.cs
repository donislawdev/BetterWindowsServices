using Bws.Core;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The vocabulary of shapes a cell can wear.
///
/// <b>A code, not a colour.</b> The view model may not name a brush: `Bws.Integration.Tests`
/// references this assembly deliberately WITHOUT UseWPF, so the fact that no view model knows
/// what WPF is gets proved by that project compiling at all. A brush here would end that, and
/// end it silently.
///
/// So the mapping from a code to a colour lives in Themes/Theme.xaml, where `ADR-23` says every
/// appearance value lives, and these strings are the joint between the two. They are constants
/// rather than an enum because the other end of the joint is a XAML DataTrigger, which compares
/// against text.
/// </summary>
public static class CellShapes
{
    /// <summary>
    /// What makes a code a code.
    ///
    /// <b>The prefix is not decoration and it was put there by a test.</b> Without it the code
    /// for a status nobody could read was the string "unknown" and so was the word for it, and
    /// a guard asking whether any code equals any word had to be told to look the other way for
    /// that one case. A code that can never collide with a translated word proves the same
    /// thing by construction instead, and it says what it is at the other end of the joint -
    /// a DataTrigger reading Value="shape.running" is plainly not matching English.
    /// </summary>
    private const string Prefix = "shape.";

    public const string Running = Prefix + "running";
    public const string Stopped = Prefix + "stopped";

    /// <summary>On the way somewhere. Four of the manager's states, and none of them lasts.</summary>
    public const string Transit = Prefix + "transit";

    /// <summary>Not running and not stopped either. A state somebody put it in on purpose.</summary>
    public const string Paused = Prefix + "paused";

    /// <summary>Nobody was able to find out. Never the same as "there is none" - rule 8.</summary>
    public const string Unknown = Prefix + "unknown";

    /// <summary>Nothing about this start type needs pointing at.</summary>
    public const string Ordinary = Prefix + "ordinary";

    /// <summary>Switched off. A fact about the entry rather than a shade of one - `docs/11` 3.1.</summary>
    public const string Disabled = Prefix + "disabled";

    /// <summary>The file the manager would run is not there, so it cannot do anything at all.</summary>
    public const string Missing = Prefix + "missing";
}

/// <summary>
/// What a cell that means something says, and what shape it says it in.
///
/// <b>Words and shape are two channels on purpose and neither is optional.</b> Colour alone
/// fails WCAG 2.2 SC 1.4.1, and the word alone is what this window shipped with - 810 identical
/// greys where the most important column for an administrator was the least scannable thing on
/// the screen.
/// </summary>
internal static class CellFaces
{
    public static string StatusShape(EntryStatus status) => status switch
    {
        EntryStatus.Running => CellShapes.Running,
        EntryStatus.Stopped => CellShapes.Stopped,
        EntryStatus.Paused => CellShapes.Paused,

        EntryStatus.StartPending
            or EntryStatus.StopPending
            or EntryStatus.ContinuePending
            or EntryStatus.PausePending => CellShapes.Transit,

        _ => CellShapes.Unknown
    };

    /// <summary>
    /// The word beside the shape, in sentence case rather than the manager's own spelling.
    ///
    /// Keys written out one by one rather than built from the value's name. A key assembled at
    /// run time cannot be searched for, and a missing one would show as itself in a cell with
    /// nothing anywhere saying why - `docs/11` 3.7 for the case, rule 13 for why no word here
    /// is a literal.
    /// </summary>
    public static string StatusLabel(EntryStatus status) => status switch
    {
        EntryStatus.Running => Texts.Of("gui.cell.status.running"),
        EntryStatus.Stopped => Texts.Of("gui.cell.status.stopped"),
        EntryStatus.Paused => Texts.Of("gui.cell.status.paused"),
        EntryStatus.StartPending => Texts.Of("gui.cell.status.startPending"),
        EntryStatus.StopPending => Texts.Of("gui.cell.status.stopPending"),
        EntryStatus.ContinuePending => Texts.Of("gui.cell.status.continuePending"),
        EntryStatus.PausePending => Texts.Of("gui.cell.status.pausePending"),
        _ => Texts.Of("gui.cell.unknown")
    };

    /// <summary>
    /// Which of the start type's facts is worth a shape.
    ///
    /// A missing file wins over everything, because it is the one that says the manager cannot
    /// carry out the setting at all. Being switched off comes next, because it is a decision
    /// somebody made rather than a fault. Waiting on a trigger gets no shape and is said in
    /// words only - it is information, not a problem, and giving it a colour would put a mark
    /// beside entries that are working exactly as intended.
    /// </summary>
    public static string StartShape(ScmEntry entry, StartQualifiers qualifies)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (qualifies.FileMissing)
        {
            return CellShapes.Missing;
        }

        if (!entry.StartType.IsPresent)
        {
            return CellShapes.Unknown;
        }

        return entry.StartType.Value == StartType.Disabled ? CellShapes.Disabled : CellShapes.Ordinary;
    }

    /// <summary>
    /// The start type with everything that changes what it means, in the same order and with
    /// the same meaning as the command line prints it - `StartQualifiers` decides which apply,
    /// and only the wording is ours.
    /// </summary>
    public static string StartLabel(ScmEntry entry, StartQualifiers qualifies)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var cell = entry.StartType.Outcome switch
        {
            ReadOutcome.Present => TypeLabel(entry.StartType.Value),
            ReadOutcome.Absent => string.Empty,
            ReadOutcome.Denied => Texts.Of("gui.cell.noAccess"),
            _ => Texts.Of("gui.cell.unknown")
        };

        if (qualifies.Delayed)
        {
            cell = Texts.Of("gui.cell.start.delayed", cell);
        }
        else if (qualifies.DelayUnknown)
        {
            cell = Texts.Of("gui.cell.start.delayUnknown", cell);
        }

        if (qualifies.OnTrigger)
        {
            cell = Texts.Of("gui.cell.start.trigger", cell);
        }

        return qualifies.FileMissing ? Texts.Of("gui.cell.start.fileMissing", cell) : cell;
    }

    private static string TypeLabel(StartType type) => type switch
    {
        StartType.Boot => Texts.Of("gui.cell.start.boot"),
        StartType.System => Texts.Of("gui.cell.start.system"),
        StartType.Automatic => Texts.Of("gui.cell.start.automatic"),
        StartType.Manual => Texts.Of("gui.cell.start.manual"),
        StartType.Disabled => Texts.Of("gui.cell.start.disabled"),
        _ => Texts.Of("gui.cell.unknown")
    };
}
