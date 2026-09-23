using Bws.Core;
using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

// THE SHAPE OF A COLUMN, AWAY FROM THE CATALOGUE OF THEM - split out 2026-08-18, backlog 176's
// sibling problem. Columns.cs stood at 462 lines and the catalogue is the half that grows: five
// more columns arrived with the second phase of `ADR-13` and would have carried it past the
// ratchet. What moved is the part that does NOT grow - one enum and one record - so the file
// that keeps growing is the one with room.

/// <summary>
/// How a cell is drawn, as far as a view model is allowed to know.
///
/// <b>A shape rather than a template name</b>, for the same reason <see cref="CellShapes"/> is a
/// code rather than a brush: `Bws.Integration.Tests` references this assembly deliberately WITHOUT
/// UseWPF, so a view model naming a WPF type would end that, and end it silently. Which template
/// each of these turns into is decided in <c>ListColumns</c>, beside the window.
/// </summary>
internal enum ColumnFace
{
    /// <summary>Words, left aligned, trimmed with an ellipsis and a tooltip.</summary>
    Text,

    /// <summary>Digits, right aligned - `docs/11` 3.3, so a column of them is comparable at a glance.</summary>
    Number,

    /// <summary>
    /// Monospaced. A launch path and a security descriptor, where alignment carries meaning and
    /// two different values must not be able to look the same - `docs/03`, part 4.
    /// </summary>
    Fixed,

    /// <summary>A mark plus the word - the running state.</summary>
    Status,

    /// <summary>A mark plus the word - the start type and what qualifies it.</summary>
    StartType,

    /// <summary>
    /// A mark plus the word - whether the entry is doing the opposite of its start type.
    ///
    /// A third shape family rather than a third use of the dot, because this is a persistent
    /// disagreement between two settings and neither of the other two columns means that.
    /// Backlog 165.
    /// </summary>
    Mismatch,

    /// <summary>
    /// Words, and a badge saying how many other entries this row stands for - `A11`.
    ///
    /// <b>A badge rather than words appended to the cell, and that distinction is load bearing.</b>
    /// This is the identity column: <c>ADR-14</c> makes the internal name what a plan is built
    /// from, the copy menu puts exactly this text on the clipboard, and sorting is asked for by the
    /// value behind it. A count glued onto the end would travel into all three and would do it
    /// silently. The badge sits beside the cell the same way the run state's dot does, and
    /// <see cref="EntryRow.StandsFor"/> carries the argument from the row's side.
    /// </summary>
    Rollup,

    /// <summary>
    /// Prose rather than a value, so the chosen row gives it more than one line - backlog 192.
    ///
    /// <b>Its own face rather than a check for one identifier at the place a column is built.</b>
    /// Every other difference between columns already travels this way, and an identifier read in
    /// <c>ListColumns</c> would be a second place that knows which column is which - the fault
    /// <c>ADR-14</c> is about, one layer up.
    ///
    /// <b>One column carries it today and the name still says what it means rather than naming
    /// that column.</b> A description is the only field on an entry that is written for a person to
    /// read instead of for a tool to match on, and if a second such field ever arrives it wants
    /// exactly this treatment. <c>CellProse</c> in <c>Themes/Cells.xaml</c> carries the measurement
    /// that decided how many lines.
    /// </summary>
    Prose
}

/// <summary>
/// One column somebody can turn on, and everything about it that is not WPF.
///
/// <b>The identifier is a frozen contract in waiting and is spelled from the glossary.</b> S6d3
/// writes the layout to disk, and what it writes is these strings - so `docs/03` part 4 decides
/// them, not this file. A column named here with an invented word is a word that ends up in
/// people's configuration files.
/// </summary>
internal sealed record Column
{
    /// <summary>What this column IS, in the glossary's own spelling. Never a translated word.</summary>
    public required string Id { get; init; }

    /// <summary>The key for its heading, which is also what the picker calls it.</summary>
    public required string LabelKey { get; init; }

    /// <summary>
    /// The name of its starting width in Themes/Values.xaml.
    ///
    /// A name rather than a number, `ADR-23` - and the word STARTING is load bearing since
    /// 2026-08-11: the theme decides where a column begins and a person dragging its edge decides
    /// where it ends up. See the note added to `ADR-23` that day.
    /// </summary>
    public required string WidthKey { get; init; }

    public required ColumnFace Face { get; init; }

    /// <summary>Whether it is on before anybody chooses anything.</summary>
    public required bool ShownAtFirst { get; init; }

    /// <summary>
    /// A scope this column starts OFF in, whatever <see cref="ShownAtFirst"/> says.
    ///
    /// <b>It carries a fact rather than a preference, which is why it is named after the scope and
    /// not after a default.</b> The process identifier is off for drivers because drivers do not
    /// have one - measured on this machine on 2026-08-19 through the command line, over 472
    /// drivers and 340 services:
    ///
    ///   processId    0 of 472 drivers carry one, against 113 of 340 services
    ///   account      3 of 472, against 312 of 340
    ///   delayedAuto  0 of 472, against 77 of 340
    ///
    /// <b>OFF rather than ABSENT, and the three accounts are the whole reason.</b> Taking the column
    /// out of the picker for drivers would make those three unreadable and say nothing about it,
    /// which is rule 8 arriving through a default. A column that starts off can be turned on by
    /// somebody who wants exactly that answer.
    ///
    /// <b>One scope rather than a set</b>, because one is what the measurement supports. A column
    /// that turns out to be empty in two scopes wants this widened by whoever measures it, not a
    /// collection standing empty for every other column in the catalogue.
    /// </summary>
    public EntryScope? OffAtFirstIn { get; init; }

    /// <summary>Whether this column is on before anybody chooses anything, in a given scope.</summary>
    public bool ShownAtFirstIn(EntryScope scope) => ShownAtFirst && OffAtFirstIn != scope;

    /// <summary>What its cell says about one entry.</summary>
    public required Func<ScmEntry, string> Reads { get; init; }

    /// <summary>
    /// What the machine holds behind what the cell says, when the cell says something else -
    /// nothing for every column whose cell shows the value as held.
    ///
    /// <b>Only the account column has one, since 2026-09-15.</b> Its cell shows "Local Service"
    /// where the manager holds <c>NT AUTHORITY\LocalService</c>, and the spelling is not a detail
    /// a person can do without: it is what <c>account:</c> matches in the box above the list and
    /// what <c>sc qc</c> prints beside it. So the cell's tooltip says it, and the details panel
    /// and a copy carry it in brackets after the shown name - <see cref="Details"/> composes that,
    /// and nothing else reads this.
    ///
    /// <b>Nothing rather than the value when the two are the same</b>, so a tooltip built on this
    /// does not repeat a cell that already tells the whole truth. The status column translates too
    /// - "Running" for a number - and deliberately has none of this: nobody types the number, and a
    /// tooltip saying "4" under "Running" would be noise.
    /// </summary>
    public Func<ScmEntry, string?>? Holds { get; init; }

    /// <summary>
    /// The family of the second phase of `ADR-13` this cell reads, when it reads one at all.
    ///
    /// <b>SHOWING A COLUMN IS A WAY OF ASKING, AND UNTIL 2026-09-05 IT WAS NOT.</b> The window
    /// worked out what to go and read from the QUERY alone - <c>Query.Needs</c>, summed over the
    /// members somebody had typed. Turning a column on told nobody: <c>ColumnBar.Changed</c> had
    /// exactly one subscriber, the one that writes the layout file. So the five columns fed by
    /// the second phase - this one, the three beside it and the signature - said "unknown" on
    /// every row, on every machine, for as long as the box above stayed empty. Reported by the
    /// owner about the memory column, and it was never about memory.
    ///
    /// <b>The prose in Themes/Values claimed the opposite and nothing could contradict it.</b> The
    /// note beside the picker's last item said turning on a signature column is what sends the
    /// window to open eight hundred files. The measurement it quotes is real and belongs to the
    /// query path. Nothing had ever measured the column path, because there was nothing there to
    /// measure.
    ///
    /// <b>Declared here rather than in a map beside the catalogue, unlike the picker's headings.</b>
    /// A heading is a fact about how a person is OFFERED all the columns, so it wants to be read
    /// at once and lives in one table. This is a fact about this cell - it is the other half of
    /// <see cref="Reads"/>, and the two are wrong together or right together. A guard checks the
    /// pair mechanically rather than trusting the declaration: a cell that says "unknown" over an
    /// entry whose first phase is complete is a cell reading the second phase, whatever it claims
    /// here.
    /// </summary>
    public ExtraRead Needs { get; init; }

    /// <summary>
    /// How the reading behind the cell went, for the columns whose "not read" is a lasting state
    /// rather than a moment - the six fed by the second phase.
    ///
    /// <b>The details panel is what asked for it, 2026-09-16.</b> A cell in a column of eight
    /// hundred says "not read" for the second between the column being turned on and the reading
    /// landing, and a person watching a column knows why. The panel about ONE entry says it six
    /// times over, for as long as nobody turns the columns on, and it has to say it differently
    /// from an answer: in the colour of a label, with a sentence under the section telling what
    /// would read them. To do that it has to know that "not read" is a state and not a word the
    /// machine holds, and <see cref="Reads"/> hands over words. So the six columns that need a
    /// family declare how their reading went, and a guard beside <see cref="Needs"/> holds the
    /// two together: a column that needs a family and cannot say whether it was read is the panel
    /// drawing a state as an answer.
    ///
    /// Nothing on the other twenty-two, and that is a cost said rather than hidden: a refusal on
    /// one of those - "no access" on the account of an entry this session may not open - is drawn
    /// as an answer in white. The word is right and rule 8 is kept - the colour is not, and the day
    /// it matters this becomes a required member. Backlog 372.
    /// </summary>
    public Func<ScmEntry, ReadOutcome>? Outcome { get; init; }

    /// <summary>
    /// The code of the mark this cell wears beside its word, for the four columns that wear one -
    /// the same codes <see cref="EntryRow"/> exposes for the list's templates, so the details
    /// panel draws a running entry in the dot the list draws it in.
    ///
    /// <b>Declared on the column since 2026-09-16, and the list does not read it yet</b> - the
    /// row still computes its three shapes by name, and the two mismatch columns share one
    /// template bound to one of them (backlog 371). The day the list reads this, that fault has
    /// nowhere left to live.
    /// </summary>
    public Func<ScmEntry, string>? Marks { get; init; }

    /// <summary>
    /// What to sort it by, when that is not simply what the cell says.
    ///
    /// <b>Only the process identifier needs it today, and it needs it badly.</b> Sorted as text,
    /// 103292 comes before 9 - which is what this window did from the day sorting was turned on
    /// until 2026-08-11, because the column bound to a string property and DataGrid sorts by the
    /// binding path.
    /// </summary>
    public Func<ScmEntry, IComparable?>? Sorts { get; init; }

    /// <summary>What two rows are compared by when this column is sorted.</summary>
    public IComparable? SortKey(ScmEntry entry) => Sorts is null ? Reads(entry) : Sorts(entry);
}
