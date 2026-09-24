using Bws.Core;
using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// One line of the details panel: what the field is called, and what this entry says in it.
///
/// <b>Public because the panel binds to it</b>, like <see cref="ColumnChoice"/> and for the same
/// reason - WPF's binding cannot see an internal property from outside this assembly, and finding
/// that out costs a blank panel with nothing in the build to say so.
/// </summary>
public sealed class DetailLine
{
    private readonly string _labelKey;

    internal DetailLine(
        string labelKey, string value, string wears, ReadOutcome outcome, string mark = "", string? reading = null)
    {
        _labelKey = labelKey;
        Value = value;
        Wears = wears;
        Outcome = outcome;

        // A word carries a state that a blank cannot. In a column of eight hundred a blank cell
        // means "genuinely nothing" and reads that way - beside a label on a panel about ONE entry
        // it reads as "did not load" - backlog 367, found by the component catalogue the first day
        // it drew this panel over a driver. `docs/06` part 3: never a blank standing in for an
        // answer.
        //
        // AND WHILE THE PANEL READS THE FIELD, THAT - since 2026-09-24. Value stays the cell's own
        // words ("not read"), so the guard holding a line to its cell keeps holding, and only what
        // the panel draws says the reading is out.
        Shown = reading ?? (value.Length == 0 ? Texts.Of("gui.details.none") : value);
        Missing = value.Length == 0 || outcome != ReadOutcome.Present;

        StatusShape = wears == "status" ? mark : string.Empty;
        StartShape = wears == "start" ? mark : string.Empty;
        AgainstShape = wears == "mismatch" ? mark : string.Empty;
    }

    /// <summary>What the field is called, in the language of whoever is reading it.</summary>
    public string Label => Texts.Of(_labelKey);

    /// <summary>
    /// What this entry says in it: the words the column's own cell uses, followed - for the one
    /// column that translates what it shows - by what the machine holds, in brackets.
    ///
    /// <b>Read through the same catalogue the grid reads</b>, so a field cannot say one thing in a
    /// cell and another in the panel. Two paths to one answer drift the first time either is
    /// touched, and nothing in a green build would notice. <see cref="Details"/> composes the
    /// bracketed half, and says why.
    ///
    /// <b>The cell's words exactly, blank included</b> - what the panel DRAWS is <see cref="Shown"/>.
    /// Kept apart so the guard holding this line to its cell keeps holding, and so a copy and the
    /// panel share one word for nothing without either inventing it.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// What the panel draws and a copy pastes: <see cref="Value"/>, or the word for nothing when
    /// the value is blank.
    /// </summary>
    public string Shown { get; }

    /// <summary>
    /// Which face the line wears, as a code the view's triggers compare against: <c>text</c>,
    /// <c>number</c>, <c>fixed</c>, <c>prose</c>, <c>status</c>, <c>start</c> or <c>mismatch</c>.
    ///
    /// <b>A code rather than the enum</b>, like the shape codes beside it: a trigger compares a
    /// string, the enum is internal to this assembly, and a translated word would stop matching
    /// the day a second language file appeared. The fixed face is the same decision
    /// <see cref="ColumnFace.Fixed"/> makes in the list and for the same reason out of `docs/03`
    /// part 4 - prose is the one field written for a person, drawn under its label at full width
    /// rather than in a column beside it.
    /// </summary>
    public string Wears { get; }

    /// <summary>
    /// How the reading behind the value went, for the columns that can say - the six fed by the
    /// second phase, through <see cref="Column.Outcome"/>. <c>Present</c> for every other column,
    /// which is a stated limit rather than a fact about them: see that member.
    /// </summary>
    public ReadOutcome Outcome { get; }

    /// <summary>
    /// Whether what is drawn is a state of the reading rather than something the machine holds -
    /// nothing, not read, no access. Drawn in the colour of the label so it cannot pass for an
    /// answer.
    /// </summary>
    public bool Missing { get; }

    /// <summary>
    /// The code of the mark this line wears, under the name the list's own mark styles bind to -
    /// <see cref="EntryRow.StatusShape"/> and its two siblings - so the panel draws the dot the
    /// list draws, out of the same style, and cannot drift a colour from it.
    ///
    /// <b>Three names for one mark, two of them always empty</b>, and that is the cheaper of two
    /// shapes: a row carries three marks in three cells and must name them apart, and rewriting
    /// three theme styles to a shared name for one more client would touch every cell template
    /// in the list. A line wears at most one, so the other two say nothing and the styles they
    /// feed stay collapsed.
    /// </summary>
    public string StatusShape { get; }

    /// <inheritdoc cref="StatusShape"/>
    public string StartShape { get; }

    /// <inheritdoc cref="StatusShape"/>
    public string AgainstShape { get; }
}

/// <summary>
/// One headed group of lines in the details panel.
///
/// <b>The groups are the picker's groups</b>, decided once in <see cref="Columns"/>, so a person
/// who has looked for a field in the column list finds it in the same place here. A second
/// grouping would be a second thing to keep in step, and `docs/11` complaint 1 is about making
/// somebody hunt.
/// </summary>
public sealed class DetailSection
{
    private readonly string _headingKey;
    private readonly string _noteKey;

    internal DetailSection(string headingKey, IReadOnlyList<DetailLine> lines, string noteKey = "")
    {
        _headingKey = headingKey;
        _noteKey = noteKey;
        Lines = lines;
    }

    public string Heading => Texts.Of(_headingKey);

    /// <summary>A sentence about the whole group, or nothing - which is the ordinary case.</summary>
    public string Note => _noteKey.Length == 0 ? string.Empty : Texts.Of(_noteKey);

    public IReadOnlyList<DetailLine> Lines { get; }
}

/// <summary>
/// Everything this window knows about one entry, arranged for a person rather than for a grid.
///
/// <b>It is the column catalogue applied to one row, and that is the whole design.</b> The columns
/// are already label-and-value pairs derived from an entry - twenty-eight of them, twenty-six once
/// the panel's head has taken the two names - with translated headings and cells that answer the
/// four read states. So the panel is those pairs, all of them, whether or not somebody turned the
/// column on. Building a second list of fields here would be a second place to add a field to, and
/// the two would disagree the first time only one was touched.
///
/// <b>Nothing HERE asks the manager anything - and since 2026-09-24 the panel as a whole does.</b>
/// Every value comes out of the entry the row holds. What changed is who fills that entry: the
/// panel reads the three expensive families for its own entry when it opens (UX-GUI-005), through
/// <see cref="Readings"/> and into the row, so this class still only arranges what is there - and
/// is told which families are being read right now, so those lines can say so.
///
/// <b>THE SECTION THAT MADE THIS PANEL HONEST HAS BEEN DELETED, 2026-08-18, AND THAT IS THE POINT
/// OF THE CHANGE RATHER THAN A LOSS.</b> Until backlog 21 the last section named four fields this
/// window deliberately did not read - signature, file version, hash and memory - because saying so
/// once in a panel about ONE entry is right where writing "nobody looked" into eight hundred cells
/// is not. The window reads them now, so they are columns like everything else and the panel gets
/// them the same way it gets the rest. A section still apologising for them would be the panel
/// being confidently wrong, which is the same fault the section existed to prevent, pointing the
/// other way.
/// </summary>
internal static class Details
{
    /// <summary>Everything the window can say about one entry, in the picker's own order.</summary>
    internal static IReadOnlyList<DetailSection> Of(ScmEntry entry) => Build(entry, identity: true);

    /// <summary>
    /// What the panel shows: everything, less the two names its head already carries.
    ///
    /// <b>Apart from <see cref="Of"/> since 2026-09-16, and it is a view of the same list rather than
    /// a second one.</b> The panel's head shows the display name and the internal name, and until
    /// that day the first section repeated both a line below it - the one entry drawn twice on 380
    /// points. A copy keeps them, because a copy has no head.
    /// </summary>
    /// <param name="entry">The entry the panel shows.</param>
    /// <param name="reading">The families the panel is reading for it right now, whose lines say so.</param>
    internal static IReadOnlyList<DetailSection> Shown(ScmEntry entry, ExtraRead reading = ExtraRead.None) =>
        Build(entry, identity: false, reading);

    /// <summary>
    /// The groups in the order the panel shows them. <b>What it runs second since 2026-09-24</b> -
    /// owner's decision at UX-GUI-012: the file and its signature are what an administrator opens
    /// the panel for, and they stood third, under the fields describing what kind of entry it is.
    /// The groups themselves are still the picker's, only their order is the panel's.
    /// </summary>
    private static readonly string[] Order = [Columns.Basics, Columns.Binary, Columns.About, Columns.Advanced];

    private static IReadOnlyList<DetailSection> Build(ScmEntry entry, bool identity, ExtraRead reading = ExtraRead.None)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var sections = new List<DetailSection>();

        foreach (var heading in Order)
        {
            var columns = Columns.All
                .Where(column => Columns.GroupOf(column.Id) == heading)
                .Where(column => identity || !Identity.Contains(column.Id))
                .ToList();

            // ONE SENTENCE UNDER A SECTION WHOSE SIGNATURE LINES STAY UNREAD BECAUSE THE FILE IS ON
            // ANOTHER MACHINE. Until 2026-09-24 the note said "the window reads a field when its
            // column is turned on" under any unread line - false twice over once the panel read for
            // itself: the panel had already tried, and turning the column on reads the same way and
            // skips the same file. What is left unread after the panel's own reading is left unread
            // for this reason, so this is the sentence, and it is said only when it is true.
            var note = OnAnotherMachine(entry)
                && columns.Any(column => column.Needs.HasFlag(ExtraRead.Signatures)
                    && column.Outcome?.Invoke(entry) == ReadOutcome.NotRead)
                ? NetworkNote
                : string.Empty;

            sections.Add(new DetailSection(heading, [.. columns.Select(column => Line(column, entry, reading))], note));
        }

        return sections;
    }

    /// <summary>
    /// Whether the entry's file is on another machine, which the window does not reach for - the
    /// inspector skips such a file (<c>NetworkPaths.Skip</c>, MainViewModel) and so does the path
    /// resolver, so the resolved file and the raw path are both asked.
    /// </summary>
    private static bool OnAnotherMachine(ScmEntry entry) =>
        NetworkPath.LeavesThisMachine(entry.BinaryFile.Value) || NetworkPath.LeavesThisMachine(entry.BinaryPath.Value);

    /// <summary>
    /// The sentence under a section whose signature lines stay unread for that reason. A constant
    /// rather than a literal at the place it is used, because that is the shape TextKeyGuards reads a
    /// key in - a key it cannot find in the source is a key it reports as said nowhere.
    /// </summary>
    private const string NetworkNote = "gui.details.notRead.network";

    /// <summary>What a line says while the panel is reading its field. A constant for the same reason as the note.</summary>
    private const string ReadingNow = "gui.details.reading";

    /// <summary>The two columns that are the entry's identity, and that the panel's head shows.</summary>
    private static readonly HashSet<string> Identity = new(StringComparer.Ordinal)
    {
        "serviceName", "displayName"
    };

    private static DetailLine Line(Column column, ScmEntry entry, ExtraRead reading)
    {
        var outcome = column.Outcome?.Invoke(entry) ?? ReadOutcome.Present;

        return new(
            column.LabelKey,
            Said(column, entry),
            Wears(column.Face),
            outcome,
            column.Marks?.Invoke(entry) ?? string.Empty,

            // ONLY A LINE STILL UNREAD, and only for a family being read now. A line that already
            // has its answer keeps it while the panel reads the others.
            outcome == ReadOutcome.NotRead && (column.Needs & reading) != ExtraRead.None ? Texts.Of(ReadingNow) : null);
    }

    /// <summary>The face as the code the view's triggers read - see <see cref="DetailLine.Wears"/>.</summary>
    private static string Wears(ColumnFace face) => face switch
    {
        ColumnFace.Number => "number",
        ColumnFace.Fixed => "fixed",
        ColumnFace.Prose => "prose",
        ColumnFace.Status => "status",
        ColumnFace.StartType => "start",
        ColumnFace.Mismatch => "mismatch",
        _ => "text"
    };

    /// <summary>
    /// What one line says: the cell's own words, and after them - in brackets - what the machine
    /// holds when the cell translated it.
    ///
    /// <b>Both, rather than the spelling alone, since 2026-09-15.</b> This panel and a copy are
    /// where somebody goes for the exact value: the account as <c>account:</c> matches it and as
    /// <c>sc qc</c> prints it. A panel showing only "Local Service" would send them to the cell's
    /// tooltip for that, and a panel showing only the spelling would disagree with the cell above
    /// it - the two-catalogues fault the summary of this class refuses. Composed here, in the one
    /// place the fields are decided, so the panel and the clipboard cannot drift.
    ///
    /// The shape of the brackets is the language file's, not this file's - a translation may
    /// bracket differently.
    /// </summary>
    private static string Said(Column column, ScmEntry entry)
    {
        var shown = column.Reads(entry);

        return column.Holds?.Invoke(entry) is { } held
            ? Texts.Of("gui.details.held", shown, held)
            : shown;
    }

    /// <summary>
    /// One entry as text somebody can paste.
    ///
    /// <b>Here rather than in <see cref="Chosen"/>, because the selection can hold more than one
    /// entry and the panel only ever shows one.</b> Both roads have to say the same thing about the
    /// same entry - a copy that disagreed with the panel would be two catalogues - so the text is
    /// built once, in the class that already decides what the fields are.
    ///
    /// <b>The catalogue rather than the row on screen, so a copy does not depend on which columns
    /// happen to be turned on.</b> Somebody copying an entry into a ticket wants what there is to
    /// know, not what they had room for, and the columns that are off by default are exactly the
    /// ones too long to have kept.
    ///
    /// <b>A label, a tab and a value per line.</b> Tab because a spreadsheet and a ticket both take
    /// it, and one field per line because several of these values run to hundreds of characters.
    /// The section headings stay, on their own lines, because they are how the picker groups the
    /// same fields - somebody who copies this recognises the shape of it. The word for nothing is
    /// the panel's word, since 2026-09-16 - "Account" followed by a tab and nothing was a line a
    /// ticket reader had to guess at.
    /// </summary>
    internal static string AsText(ScmEntry entry) => string.Join(
        Environment.NewLine,
        Of(entry).SelectMany(section => section.Lines
            .Select(line => line.Label + "\t" + line.Shown)
            .Prepend(section.Heading)));

    /// <summary>
    /// Several entries as text, in the order they were handed over.
    ///
    /// <b>A blank line between them and nothing else, which is a decision rather than a default.</b>
    /// Each entry is already several dozen lines of label and value, so a separator that is itself a
    /// line of text would read as another field. A blank line is what every destination this text
    /// goes to - a ticket, a mail, a spreadsheet cell - already treats as a break.
    ///
    /// <b>The order is the caller's, and for the window that is the order on screen</b> rather than
    /// the order somebody clicked in. A copy of five entries is read top to bottom against the list
    /// it came from, and reordering it would make that comparison fail for no reason.
    /// </summary>
    internal static string AsText(IEnumerable<ScmEntry> entries) => string.Join(
        Environment.NewLine + Environment.NewLine,
        entries.Select(AsText));
}
