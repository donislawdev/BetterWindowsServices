using Bws.Core;

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

/// <summary>
/// The seventeen columns of `A8`, and why exactly these.
///
/// <b>The count is arithmetic rather than taste, and it closes exactly.</b> `ScmEntry` carries 22
/// fields plus one derived. Four are refused because the second phase of `ADR-13` reads them and
/// the window has no second phase - signature, file version, binary hash and memory - so a column
/// for any of them would write "nobody looked" 809 times, which is a promise the window cannot
/// keep. Backlog 21 brings them back the day that phase exists. Two more are absent because they
/// are already on screen inside another cell: the delayed flag and whether the file is on disk are
/// both qualifiers the start type carries, exactly as the command line prints them. 23 - 4 - 2 is
/// seventeen. Owner's decision, 2026-08-11.
///
/// <b>Six are on at the start and that is also a decision rather than the status quo.</b> `A8`
/// names Name, Status, Start, Account, PID and RAM - and RAM belongs to the phase that does not
/// exist, so the display name keeps its place instead. It carries the text a person recognises,
/// translated on this machine, which no other column does.
///
/// <b>The order is the order they are offered in</b>, which is what somebody reads down the picker
/// and what the grid uses before anybody drags anything: the six that are on, then the cheap facts
/// about what an entry IS, then the three lists, then the two that are mostly for an audit.
/// </summary>
