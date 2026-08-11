using Bws.Core;

namespace Bws.Gui.ViewModels;

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
    StartType
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
internal static class Columns
{
    /// <summary>Every column, in the order they are offered.</summary>
    internal static IReadOnlyList<Column> All { get; } =
    [
        new Column
        {
            Id = "serviceName",
            LabelKey = "gui.column.name",
            WidthKey = "ColumnName",
            Face = ColumnFace.Text,
            ShownAtFirst = true,
            Reads = entry => entry.ServiceName
        },
        new Column
        {
            Id = "displayName",
            LabelKey = "gui.column.displayName",
            WidthKey = "ColumnDisplayName",
            Face = ColumnFace.Text,
            ShownAtFirst = true,
            Reads = entry => entry.DisplayName
        },
        new Column
        {
            Id = "status",
            LabelKey = "gui.column.status",
            WidthKey = "ColumnStatus",
            Face = ColumnFace.Status,
            ShownAtFirst = true,
            Reads = entry => CellFaces.StatusLabel(entry.Status)
        },
        new Column
        {
            Id = "startType",
            LabelKey = "gui.column.startType",
            WidthKey = "ColumnStart",
            Face = ColumnFace.StartType,
            ShownAtFirst = true,
            Reads = entry => CellFaces.StartLabel(entry, StartQualifiers.Of(entry))
        },
        new Column
        {
            Id = "account",
            LabelKey = "gui.column.account",
            WidthKey = "ColumnAccount",
            Face = ColumnFace.Text,
            ShownAtFirst = true,
            Reads = entry => CellFaces.Say(entry.Account, value => value)
        },
        new Column
        {
            Id = "processId",
            LabelKey = "gui.column.processId",
            WidthKey = "ColumnProcessId",
            Face = ColumnFace.Number,
            ShownAtFirst = true,
            Reads = entry => CellFaces.Say(entry.ProcessId, Number),

            // The one column whose cell and whose order are different questions. Everything else
            // sorts by what it says, which is what somebody looking at it would expect.
            Sorts = entry => entry.ProcessId.IsPresent ? entry.ProcessId.Value : null
        },

        // WHAT AN ENTRY IS. Free to read - all of these arrive in the buffers the listing already
        // fills - and each answers a question somebody arrives at an unknown server with.
        new Column
        {
            Id = "entryType",
            LabelKey = "gui.column.entryType",
            WidthKey = "ColumnEntryType",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Reads = entry => CellFaces.EntryTypeLabel(entry.EntryType)
        },
        new Column
        {
            // The sharpest question this list can be asked of a machine nobody knows: what is set
            // to run and is not. `ScmEntry` calls it the single most useful derived fact in the
            // tool, and until now it was reachable only through the query language.
            Id = "runsAgainstItsStartType",
            LabelKey = "gui.column.runsAgainstItsStartType",
            WidthKey = "ColumnAgainstStartType",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Reads = entry => CellFaces.Judgement(entry.RunsAgainstItsStartType)
        },
        new Column
        {
            Id = "binaryPath",
            LabelKey = "gui.column.binaryPath",
            WidthKey = "ColumnBinaryPath",
            Face = ColumnFace.Fixed,
            ShownAtFirst = false,
            Reads = entry => CellFaces.Say(entry.BinaryPath, value => value)
        },
        new Column
        {
            // The file the command actually runs, which is a different answer from the command -
            // arguments come off, \SystemRoot\ resolves, and an unquoted path with spaces is
            // genuinely ambiguous. Two columns because the glossary has two names.
            Id = "binaryFile",
            LabelKey = "gui.column.binaryFile",
            WidthKey = "ColumnBinaryFile",
            Face = ColumnFace.Fixed,
            ShownAtFirst = false,
            Reads = entry => CellFaces.Say(entry.BinaryFile, value => value)
        },
        new Column
        {
            Id = "loadOrderGroup",
            LabelKey = "gui.column.loadOrderGroup",
            WidthKey = "ColumnLoadOrderGroup",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Reads = entry => CellFaces.Say(entry.LoadOrderGroup, value => value)
        },
        new Column
        {
            Id = "errorControl",
            LabelKey = "gui.column.errorControl",
            WidthKey = "ColumnErrorControl",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Reads = entry => CellFaces.ErrorControlLabel(entry.ErrorControl)
        },
        new Column
        {
            Id = "sidType",
            LabelKey = "gui.column.sidType",
            WidthKey = "ColumnSidType",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Reads = entry => CellFaces.SidTypeLabel(entry.SidType)
        },

        // THE THREE LISTS. Joined rather than counted - the reasoning is at CellFaces.NameList.
        new Column
        {
            Id = "dependsOn",
            LabelKey = "gui.column.dependsOn",
            WidthKey = "ColumnDependsOn",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Reads = entry => CellFaces.NameList(entry.DependsOn)
        },
        new Column
        {
            Id = "triggers",
            LabelKey = "gui.column.triggers",
            WidthKey = "ColumnTriggers",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Reads = entry => CellFaces.TriggerList(entry.Triggers)
        },
        new Column
        {
            // Declaring none is the PERMISSIVE case, not the careful one - glossary pitfall P11.
            // Nothing in a cell can say that, which is why the heading is what it asks for rather
            // than a verdict.
            Id = "requiredPrivileges",
            LabelKey = "gui.column.requiredPrivileges",
            WidthKey = "ColumnPrivileges",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Reads = entry => CellFaces.NameList(entry.RequiredPrivileges)
        },
        new Column
        {
            // The TEXT form, which glossary pitfall P9 says belongs in a snapshot while a person
            // is shown the decoded list. The decoded list does not exist and gets its own name the
            // day a details panel can show it - so what is offered here is the faithful form, in a
            // monospaced cell, trimmed with the whole of it in the tooltip.
            Id = "securityDescriptor",
            LabelKey = "gui.column.securityDescriptor",
            WidthKey = "ColumnDescriptor",
            Face = ColumnFace.Fixed,
            ShownAtFirst = false,
            Reads = entry => CellFaces.Say(entry.SecurityDescriptor, value => value)
        }
    ];

    /// <summary>The column with this identifier, or nothing.</summary>
    internal static Column? Of(string id) => Index.GetValueOrDefault(id);

    /// <summary>
    /// Two rows compared by one column, for whatever is sorting the list.
    ///
    /// <b>Here rather than beside the window, and that is the same split this product makes for
    /// every other decision a handler would otherwise swallow.</b> A shortcut's MEANING lives in
    /// <see cref="Shortcuts"/> and the key press lives in the window; which row a letter jumps to
    /// lives in <see cref="RowList"/> and the scrolling lives in the window. An order is a
    /// decision, so it lives where it can be asked without a desktop - what stays beside the grid
    /// is subscribing to an event and handing this over.
    /// </summary>
    internal static System.Collections.IComparer OrderedBy(Column column, bool ascending) =>
        new CellOrder(column, ascending);

    /// <summary>
    /// <b>Nothing sorts as less than something, on purpose.</b> A row with no value in this column
    /// compares below every row that has one, so reversing the direction moves the whole group
    /// from the top to the bottom. That is exactly what the empty string already does in every
    /// column that says nothing with one, and a column behaving differently would be a rule
    /// nobody could learn by using the list.
    /// </summary>
    private sealed class CellOrder(Column column, bool ascending) : System.Collections.IComparer
    {
        public int Compare(object? left, object? right)
        {
            var order = Order(Key(left), Key(right));

            return ascending ? order : -order;
        }

        private static int Order(IComparable? left, IComparable? right) => (left, right) switch
        {
            (null, null) => 0,
            (null, _) => -1,
            (_, null) => 1,
            _ => left.CompareTo(right)
        };

        private IComparable? Key(object? row) =>
            row is EntryRow entry ? column.SortKey(entry.Entry) : null;
    }

    private static readonly Dictionary<string, Column> Index =
        All.ToDictionary(column => column.Id, StringComparer.Ordinal);

    private static string Number(int value) =>
        value.ToString(System.Globalization.CultureInfo.CurrentCulture);
}
