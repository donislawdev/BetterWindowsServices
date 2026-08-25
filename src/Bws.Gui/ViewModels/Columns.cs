using Bws.Core;

namespace Bws.Gui.ViewModels;

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

            // The one column that can carry a rollup badge, because it is the one `A11` names in
            // its own example - "CDPUserSvc - 4 instancje". The cell itself is still the internal
            // name and nothing else, which is what keeps copying, sorting and plan building
            // reading the identity rather than a sentence about it.
            Face = ColumnFace.Rollup,
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
            // THE SECOND COLUMN OF services.msc, and the only one that answers "what even is
            // this". Off at the start despite being second there, and that is a decision rather
            // than an oversight: it is prose, so at any width a person can spare it shows a
            // fragment and an ellipsis, and this window's six default columns are the ones that
            // answer a question in a glance. Somebody who wants it turns it on and widens it.
            Id = "description",
            LabelKey = "gui.column.description",
            WidthKey = "ColumnDescription",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Reads = entry => CellFaces.Say(entry.Description, value => value)
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

            // Three drivers out of 472 carry one on this machine, against 312 services out of 340.
            // Off rather than gone, because those three are a real answer somebody may be looking
            // for - the argument in full is at Column.OffAtFirstIn.
            OffAtFirstIn = EntryScope.Drivers,

            Reads = entry => CellFaces.Say(entry.Account, value => value)
        },
        new Column
        {
            Id = "processId",
            LabelKey = "gui.column.processId",
            WidthKey = "ColumnProcessId",
            Face = ColumnFace.Number,
            ShownAtFirst = true,

            // NOT ONE DRIVER OF 472 HAS ONE - measured, and it is a fact about what a driver IS
            // rather than about this machine. A column that is empty in every row of a list is a
            // column spending width on nothing.
            OffAtFirstIn = EntryScope.Drivers,

            Reads = entry => CellFaces.Say(entry.ProcessId, Number),

            // The one column whose cell and whose order are different questions. Everything else
            // sorts by what it says, which is what somebody looking at it would expect.
            Sorts = entry => entry.ProcessId.IsPresent ? entry.ProcessId.Value : null
        },

        // THE TWO FIELDS THAT WERE READ, SHOWN, AND HAD NO COLUMN - added 2026-08-17 on the
        // owner's report that the window shows too few columns. Backlog 190 counted it: ScmEntry
        // carries twenty-three fields and there were eighteen columns, and of the five without one,
        // four are second phase (backlog 21) and these two were folded into the start column as
        // qualifiers - "Automatic (delayed)" and "file missing".
        //
        // WHY A QUALIFIER IS NOT A COLUMN, which is the whole reason these are worth the lines. A
        // word inside another cell cannot be sorted on, cannot be scanned down, and cannot be
        // turned off by somebody who does not care about it. The start column keeps saying both
        // things, because that is what somebody reading one row wants - these are for somebody
        // asking the question of the whole machine.
        //
        // NEITHER COSTS A READING. Both arrive in the buffers the listing already fills, which is
        // what made them the two that could be added at all - everything else needs either the
        // second phase or a change to ScmEntry, and that is a frozen contract rather than a slice.
        new Column
        {
            Id = "delayedAuto",
            LabelKey = "gui.column.delayedAuto",
            WidthKey = "ColumnDelayedAuto",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Reads = entry => CellFaces.YesOrNo(entry.DelayedAuto)
        },
        new Column
        {
            // NOT the same question as binaryFile, and the pair is easy to read as one. That column
            // says WHICH file the command resolves to, which is an answer even when nothing is
            // there. This says whether that file EXISTS - so a row can carry a path, a resolved
            // file, and No.
            Id = "binaryOnDisk",
            LabelKey = "gui.column.binaryOnDisk",
            WidthKey = "ColumnBinaryOnDisk",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Reads = entry => CellFaces.YesOrNo(entry.BinaryOnDisk)
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
            Face = ColumnFace.Mismatch,
            ShownAtFirst = false,
            Reads = entry => CellFaces.Judgement(entry.RunsAgainstItsStartType)
        },
        // THE SECOND PHASE OF `ADR-13`, arriving 2026-08-18 - backlog 21. Until this slice the
        // window read none of these and said so, which was honest and left the fields invisible.
        //
        // FIVE RATHER THAN THE FOUR THE BACKLOG ESTIMATED, and the difference is that the
        // signature carries two facts: what the system thinks of it, and who signed it. The
        // dictionary has separate names for them, so they are separate columns.
        //
        // ALL OFF AT THE START, and that is not the usual caution about width. Turning one on is
        // what makes the window go and read them, and the reading measured 8.86-9.42 s of processor
        // against 1.39-1.42 s without it, over about 810 entries. A column shown by default would
        // spend that on every F5 for everybody.
        new Column
        {
            Id = "signature",
            LabelKey = "gui.column.signature",
            WidthKey = "ColumnSignature",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Reads = entry => CellFaces.SignatureLabel(entry.Signature)
        },
        new Column
        {
            Id = "publisher",
            LabelKey = "gui.column.publisher",
            WidthKey = "ColumnPublisher",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Reads = entry => CellFaces.PublisherLabel(entry.Signature)
        },
        new Column
        {
            Id = "fileVersion",
            LabelKey = "gui.column.fileVersion",
            WidthKey = "ColumnFileVersion",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Reads = entry => CellFaces.Say(entry.FileVersion, value => value)
        },
        new Column
        {
            // Monospaced, and this is the column the rule was written for: sixty-four hexadecimal
            // characters where two different values must not be able to look the same - `docs/03`,
            // part 4. Proportional digits make that exactly what they do.
            Id = "binaryHash",
            LabelKey = "gui.column.binaryHash",
            WidthKey = "ColumnBinaryHash",
            Face = ColumnFace.Fixed,
            ShownAtFirst = false,
            Reads = entry => CellFaces.Say(entry.BinaryHash, value => value)
        },
        new Column
        {
            // Right aligned, because it is the one column in this window somebody scans down
            // looking for the big one - `docs/11` 3.3.
            Id = "memory",
            LabelKey = "gui.column.memory",
            WidthKey = "ColumnMemory",
            Face = ColumnFace.Number,
            ShownAtFirst = false,
            Reads = entry => CellFaces.MemoryLabel(entry.Memory),

            // By the number rather than by the words, for the same reason the process id sorts
            // that way: "9.9 MB" sorts above "10.0 MB" as text, and a column nobody can order is
            // most of the reason to have this one at all.
            Sorts = entry => entry.Memory.IsPresent ? entry.Memory.Value!.WorkingSet : null
        },
        new Column
        {
            // THE OTHER DIRECTION, and services.msc does not name it either - backlog 170. Two of
            // its columns say opposite things side by side and nothing joins them up. Its own
            // column rather than a wider version of the one above, for the reason the fact itself
            // is separate: the remedies are opposite, so a single mark saying "something is wrong
            // here" would send somebody to start a service they may need to stop.
            Id = "runsWhileDisabled",
            LabelKey = "gui.column.runsWhileDisabled",
            WidthKey = "ColumnRunsWhileDisabled",
            Face = ColumnFace.Mismatch,
            ShownAtFirst = false,
            Reads = entry => CellFaces.Judgement(entry.RunsWhileDisabled)
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

    /// <summary>
    /// Which heading in the picker each column sits under.
    ///
    /// <b>One map rather than a field on every declaration, and that is not a saving.</b> A
    /// grouping is only useful if somebody can see the whole of it at once and ask whether it makes
    /// sense - spread across the entries it becomes one local decision each, and nobody notices
    /// when a group is left with one item in it.
    ///
    /// <b>THIS MAP IS WHY TWO NEW COLUMNS COST SIX RED TESTS ON 2026-08-17 AND NOT ONE, and that is
    /// the design working.</b> Adding a column to the catalogue without adding it here left it
    /// under no heading at all - and the picker, the details panel and the button's menu all went
    /// red about it separately, because each of them is built from this map rather than from a
    /// default. The sentence below promised exactly that outcome before it happened.
    ///
    /// <b>The groups are about what a person came looking for, not about where the data comes
    /// from.</b> The first is what the list shows without being asked. The rest are three questions
    /// somebody arrives with: who is this running as and is it behaving, what does it run, and the
    /// details you go looking for once you already suspect something.
    ///
    /// A column missing here is a failed test rather than a default, because a default would put it
    /// quietly under whichever heading was least wrong.
    /// </summary>
    private static readonly Dictionary<string, string> Groups = new(StringComparer.Ordinal)
    {
        ["serviceName"] = Basics,
        ["displayName"] = Basics,
        ["description"] = Basics,
        ["status"] = Basics,
        ["startType"] = Basics,

        // Beside the start type rather than under About, because it is a QUALIFIER on that column
        // and not a separate fact about the entry - the start cell says "Automatic (delayed)" from
        // the same field. Somebody who turns this on is refining what the start column already
        // told them, which is what Basics is.
        ["delayedAuto"] = Basics,

        ["account"] = Basics,
        ["processId"] = Basics,

        // Beside the process id in meaning, and the grouping follows meaning rather than cost -
        // this is the only column here that is a MEASUREMENT of a running process rather than a
        // setting, and the process id is the other half of that pair.
        ["memory"] = Basics,

        ["entryType"] = About,
        ["runsAgainstItsStartType"] = About,
        ["runsWhileDisabled"] = About,
        ["sidType"] = About,

        ["binaryPath"] = Binary,
        ["binaryFile"] = Binary,

        // Under Binary rather than under a heading of their own, because every one of them is a
        // fact about the FILE the entry runs rather than about the entry - which is exactly what
        // this heading means. Backlog 21.
        ["signature"] = Binary,
        ["publisher"] = Binary,
        ["fileVersion"] = Binary,
        ["binaryHash"] = Binary,

        // The third one this heading has wanted since it was drawn. "What does it run" is answered
        // by a command, by the file that command resolves to, and by whether that file is there -
        // and the last of the three was the one folded into another column as the words
        // "file missing".
        ["binaryOnDisk"] = Binary,

        ["loadOrderGroup"] = Advanced,
        ["errorControl"] = Advanced,
        ["dependsOn"] = Advanced,
        ["triggers"] = Advanced,
        ["requiredPrivileges"] = Advanced,
        ["securityDescriptor"] = Advanced
    };

    internal const string Basics = "gui.columns.group.basics";
    internal const string About = "gui.columns.group.about";
    internal const string Binary = "gui.columns.group.binary";
    internal const string Advanced = "gui.columns.group.advanced";

    /// <summary>The heading a column sits under, or null when nobody gave it one.</summary>
    internal static string? GroupOf(string id) => Groups.GetValueOrDefault(id);

    private static readonly Dictionary<string, Column> Index =
        All.ToDictionary(column => column.Id, StringComparer.Ordinal);

    private static string Number(int value) =>
        value.ToString(System.Globalization.CultureInfo.CurrentCulture);
}
