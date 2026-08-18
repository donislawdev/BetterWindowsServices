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
/// So the mapping from a code to a colour lives in Themes/List.xaml, in the mark styles that
/// trigger on these codes, and the colours they reach for are named in Themes/Values.xaml, where
/// `ADR-23` says every appearance value lives. These strings are the joint between the two. They are constants
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

    /// <summary>Nothing about this state needs pointing at. The start column stopped producing it
    /// on 2026-08-17, when each start type got a mark of its own - the disagreement mark still
    /// does, for an entry doing exactly what its start type says.</summary>
    public const string Ordinary = Prefix + "ordinary";

    /// <summary>
    /// The four start types, each with a mark of its own - owner's decision, 2026-08-17. Until
    /// then all four shared <see cref="Ordinary"/>, which is to say they shared no mark at all.
    ///
    /// <b>PREFIXED, UNLIKE EVERY OTHER CODE HERE, AND FOR A REASON WORTH ONE LINE.</b> The obvious
    /// name for the second of them is <c>System</c>, and a constant called that inside this class
    /// shadows the namespace of the same name for everything written after it. The compiler would
    /// not complain here today and would complain somewhere else later, which is the worst shape a
    /// name can have. The <c>shape.start.</c> prefix also says which column a code belongs to,
    /// which the older ones leave to the reader.
    /// </summary>
    public const string StartBoot = Prefix + "start.boot";

    public const string StartSystem = Prefix + "start.system";

    public const string StartAutomatic = Prefix + "start.automatic";

    public const string StartManual = Prefix + "start.manual";

    /// <summary>Switched off. A fact about the entry rather than a shade of one - `docs/11` 3.1.</summary>
    public const string Disabled = Prefix + "disabled";

    /// <summary>The file the manager would run is not there, so it cannot do anything at all.</summary>
    public const string Missing = Prefix + "missing";

    /// <summary>
    /// Set to start automatically and not running, with nothing waiting to start it.
    ///
    /// <b>Narrower than the column's own name, and that was checked rather than assumed.</b>
    /// <c>ScmEntry.Judge</c> answers false for anything whose start type is not Automatic - so a
    /// service that is switched off and running anyway is NOT this. The first draft of the tooltip
    /// beside this said it was, which would have been a sentence on screen that the code disagrees
    /// with.
    ///
    /// <b>Its own code rather than one of the two above, and its own shape family in the theme.</b>
    /// This is a PERSISTENT disagreement between two settings, not a state something is passing
    /// through, so it may share neither the vocabulary of the running state nor the amber that
    /// means "on its way somewhere". `docs/11` 3.1 asks for shape and colour and word, and the
    /// column had the word alone until 2026-08-12 - backlog 165.
    /// </summary>
    public const string Against = Prefix + "against";
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
    ///
    /// <b>AND FROM 2026-08-17 EVERY START TYPE HAS ITS OWN, owner's decision.</b> Until then Boot,
    /// System, Automatic and Manual all came back as <see cref="CellShapes.Ordinary"/>, which the
    /// theme draws as nothing - so the column's mark answered "is something wrong" and never "what
    /// is this set to".
    ///
    /// <b>The order of the two checks above is unchanged and that is the whole of how the alarm
    /// survives.</b> A missing file still wins, and it is still the only FILLED mark in this
    /// column - so an entry whose file is gone does not quietly become a purple ring saying
    /// Automatic. The four below are outlines in quiet colours, argued where they are declared.
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

        return entry.StartType.Value switch
        {
            StartType.Boot => CellShapes.StartBoot,
            StartType.System => CellShapes.StartSystem,
            StartType.Automatic => CellShapes.StartAutomatic,
            StartType.Manual => CellShapes.StartManual,
            StartType.Disabled => CellShapes.Disabled,

            // NO MARK, AND THIS LINE WAS WRONG FOR ONE BUILD - it said Unknown, and an existing
            // test caught it within the hour.
            //
            // StartType.Unknown is the zero of that enumeration, which is what a value outside the
            // five Windows documents becomes. The entry HAS an answer and the manager gave it to
            // us - we have no word of our own for it. CellShapes.Unknown means the opposite: that
            // nobody could read it. Rule 8 spends most of its weight on not letting those two
            // sentences stand in for each other, and painting a broken ring here would have said
            // "denied" about a reading that succeeded.
            //
            // So it keeps the answer the column gave every start type until today: nothing. The
            // WORD in the cell says "Unknown", which is the honest channel for a category we
            // cannot name, and inventing a fifth colour for it would claim we had named it.
            _ => CellShapes.Ordinary
        };
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

    /// <summary>
    /// A reading as text, with each of the four states saying something different.
    ///
    /// <b>The empty string is only ever used for "there is genuinely nothing"</b>, which is the
    /// one state where a blank cell tells the truth. The other two say so in words, because a
    /// person scanning a column has no other way to tell them from a value nobody has. Rule 8 of
    /// the project notes, arriving in the last place it can still be broken.
    ///
    /// <b>Moved here from EntryRow on 2026-08-11 because the columns needed it</b>, and it was
    /// private there. Eleven more columns turn a Reading into a cell now, and a second copy of
    /// this switch is a second place for one of the four states to quietly go missing.
    /// </summary>
    public static string Say<T>(Reading<T> reading, Func<T, string> show)
    {
        ArgumentNullException.ThrowIfNull(show);

        return reading.Outcome switch
        {
            ReadOutcome.Present => show(reading.Value!),
            ReadOutcome.Absent => string.Empty,
            ReadOutcome.Denied => Texts.Of("gui.cell.noAccess"),
            _ => Texts.Of("gui.cell.unknown")
        };
    }

    /// <summary>
    /// What the entry is technically. Always present - it comes out of the enumeration itself
    /// rather than out of a query that can be refused - so this takes the value rather than a
    /// reading, and says so by its signature.
    /// </summary>
    public static string EntryTypeLabel(EntryType type) => type switch
    {
        EntryType.KernelDriver => Texts.Of("gui.cell.entryType.kernelDriver"),
        EntryType.FileSystemDriver => Texts.Of("gui.cell.entryType.fileSystemDriver"),
        EntryType.OwnProcess => Texts.Of("gui.cell.entryType.ownProcess"),
        EntryType.SharedProcess => Texts.Of("gui.cell.entryType.sharedProcess"),
        _ => Texts.Of("gui.cell.unknown")
    };

    /// <summary>
    /// Whether the entry has an identity of its own.
    ///
    /// <b>"None" is not one of the words here and that is `ScmEntry` keeping its promise.</b> The
    /// manager reports NONE for an entry with no identity, which is the absence of one rather
    /// than a kind of one - so it arrives as <see cref="ReadOutcome.Absent"/> and renders as an
    /// empty cell, exactly like every other field with genuinely nothing in it.
    /// </summary>
    public static string SidTypeLabel(Reading<ServiceSidType> reading) => Say(reading, type => type switch
    {
        ServiceSidType.Unrestricted => Texts.Of("gui.cell.sidType.unrestricted"),
        ServiceSidType.Restricted => Texts.Of("gui.cell.sidType.restricted"),
        _ => Texts.Of("gui.cell.unknown")
    });

    /// <summary>
    /// How hard the system takes it when this entry fails to start AT BOOT, and nothing else.
    ///
    /// The glossary warns that "critical" invites reading as "this service is important". The
    /// words are the manager's own four and are left as they are - the place to explain what they
    /// mean is a details panel, not a cell.
    /// </summary>
    public static string ErrorControlLabel(Reading<ErrorControl> reading) => Say(reading, control => control switch
    {
        ErrorControl.Ignore => Texts.Of("gui.cell.errorControl.ignore"),
        ErrorControl.Normal => Texts.Of("gui.cell.errorControl.normal"),
        ErrorControl.Severe => Texts.Of("gui.cell.errorControl.severe"),
        ErrorControl.Critical => Texts.Of("gui.cell.errorControl.critical"),
        _ => Texts.Of("gui.cell.unknown")
    });

    /// <summary>
    /// Whether the entry is supposed to be running and is not - the single most useful derived
    /// fact in the tool, as a cell.
    ///
    /// <b>Yes and no rather than a word and a blank</b>, and the blank was the tempting one: a
    /// column where only the broken entries carry text is scannable at 810 rows. It is refused
    /// because an empty cell already means "there is genuinely nothing" everywhere else in this
    /// window, and one column where it means "no" instead is the kind of local exception that
    /// makes rule 8 unenforceable.
    ///
    /// <b>What this does NOT do, said rather than left to be noticed:</b> `docs/11` 3.1 asks for
    /// shape AND colour AND word wherever something means something, and this has the word alone
    /// while the two columns beside it carry marks. A mark here would be a fifth shape family and
    /// a row in MarkDistinctionGuards. Backlog 165.
    /// </summary>
    public static string Judgement(Reading<bool> reading) => YesOrNo(reading);

    /// <summary>
    /// A read boolean as a cell, in the four states every reading has.
    ///
    /// <b>Split out of <see cref="Judgement"/> on 2026-08-17 rather than copied, when two more
    /// columns needed the same four answers</b> - whether a service starts late, and whether the
    /// file behind it is on disk. The whole argument three paragraphs up applies to each of them
    /// unchanged: yes and no in words, never a word and a blank, because an empty cell already
    /// means "there is genuinely nothing" everywhere else in this window.
    ///
    /// <see cref="Judgement"/> stays as its own name because the comment above it is about ONE
    /// column and would be false over these two - a delayed start is not a disagreement between
    /// two settings, it is a setting.
    /// </summary>
    public static string YesOrNo(Reading<bool> reading) =>
        Say(reading, value => value ? Texts.Of("gui.cell.yes") : Texts.Of("gui.cell.no"));

    /// <summary>
    /// Which shape that judgement wears - backlog 165, and it closes the sentence three paragraphs
    /// up which admitted this column had the word alone.
    ///
    /// <b>Only a definite YES gets a mark.</b> An entry doing what its start type says is the
    /// ordinary case and marking it would put a shape beside seven hundred rows that are fine,
    /// which is the same argument the start column already makes for waiting on a trigger.
    ///
    /// <b>Anything that is not a definite yes or no is UNKNOWN, including a state this can never
    /// produce today.</b> <c>RunsAgainstItsStartType</c> yields present, denied or not-read and
    /// never absent - but a switch that answered "ordinary" to a case it does not recognise would
    /// be rendering "I could not check" as "everything is fine", which is the one thing rule 8
    /// forbids in as many words.
    /// </summary>
    public static string AgainstShape(Reading<bool> reading) => reading.Outcome switch
    {
        ReadOutcome.Present => reading.Value ? CellShapes.Against : CellShapes.Ordinary,
        _ => CellShapes.Unknown
    };

    /// <summary>
    /// A list of names as one cell - dependencies and required privileges.
    ///
    /// <b>Joined rather than counted, and that is `docs/11` rather than a preference.</b> That
    /// document opens by saying this window was built as a table when an administrator needs
    /// something to search with. A cell saying "5" answers how many, and nobody arrives at this
    /// column wanting to know how many.
    ///
    /// Names are the manager's own, unchanged - a privilege is <c>SeShutdownPrivilege</c> and a
    /// dependency keeps the leading plus that marks a load order group rather than a service.
    /// Translating either would turn an identifier into a label, which is `ADR-14` in the small.
    /// </summary>
    public static string NameList(Reading<IReadOnlyList<string>> reading) => Say(reading, Join);

    /// <summary>
    /// The conditions the manager starts or stops this entry on, as one cell.
    ///
    /// <b>Kinds rather than triggers, collapsed to distinct ones.</b> An entry with four custom
    /// triggers would otherwise read "Custom, Custom, Custom, Custom", which is four times the
    /// noise for none of the answer. Which exact device class or which exact event belongs to a
    /// details panel, and <see cref="ServiceTrigger"/> says so itself.
    ///
    /// <b>The action is deliberately not here.</b> Nearly every trigger starts something, and the
    /// start type column already says "on trigger" for the entries where that changes what the
    /// start type means. A cell listing stop triggers as though they were start ones would be
    /// wrong, and one qualifying every kind would be twice the width for a rare case.
    /// </summary>
    public static string TriggerList(Reading<IReadOnlyList<ServiceTrigger>> reading) =>
        Say(reading, triggers => Join(triggers.Select(trigger => TriggerKindLabel(trigger.Kind)).Distinct(StringComparer.Ordinal)));

    private static string TriggerKindLabel(TriggerKind kind) => kind switch
    {
        TriggerKind.DeviceArrival => Texts.Of("gui.cell.trigger.deviceArrival"),
        TriggerKind.IpAddress => Texts.Of("gui.cell.trigger.ipAddress"),
        TriggerKind.DomainJoin => Texts.Of("gui.cell.trigger.domainJoin"),
        TriggerKind.FirewallPort => Texts.Of("gui.cell.trigger.firewallPort"),
        TriggerKind.GroupPolicy => Texts.Of("gui.cell.trigger.groupPolicy"),
        TriggerKind.NetworkEndpoint => Texts.Of("gui.cell.trigger.networkEndpoint"),
        TriggerKind.Custom => Texts.Of("gui.cell.trigger.custom"),
        TriggerKind.CustomSystemStateChange => Texts.Of("gui.cell.trigger.systemState"),
        _ => Texts.Of("gui.cell.unknown")
    };

    /// <summary>
    /// The separator between one item and the next, from the language file rather than written
    /// here, because where a comma goes and whether a space follows it is a fact about a language.
    /// </summary>
    private static string Join(IEnumerable<string> items) =>
        string.Join(Texts.Of("gui.cell.list.separator"), items);
}
