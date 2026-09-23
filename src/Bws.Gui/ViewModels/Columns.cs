using Bws.Core;
using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The columns of `A8` - twenty-eight on 2026-09-23. Count <see cref="All"/> rather than this line.
///
/// <b>It began as seventeen, and the arithmetic of that day is kept because it says what earns a
/// column.</b> On 2026-08-11 `ScmEntry` carried 22 fields plus one derived. Four were left out
/// because only the second phase of `ADR-13` reads them - signature, file version, binary hash and
/// memory - and the window had no second phase, so a column would have said "nobody looked" on every
/// row. Two more were qualifiers the start column already carried - the delayed flag and whether the
/// file is on disk. 23 - 4 - 2 was seventeen, owner's decision.
///
/// <b>What changed since, and the entries below carry their dates.</b> The two qualifiers got
/// columns of their own on 2026-08-17, the four second phase fields came in once the window had that
/// phase, and five more arrived with later slices - 17 + 2 + 4 + 5 is the twenty-eight above. This
/// paragraph stated the seventeen as the present until the review of the pull request that moved it
/// here, 2026-09-23.
///
/// <b>Which are on before anybody chooses is each column's own</b> <see cref="Column.ShownAtFirstIn"/>,
/// per list, and not a number written here.
///
/// <b>The order is the order they are offered in</b>, which is what somebody reads down the picker
/// and what the grid uses before anybody drags anything - with the three lists and the two that are
/// mostly for an audit at the end.
///
/// <b>This comment stood at the end of Column.cs, attached to nothing, from the day the size
/// ratchet moved this table out of that file until 2026-09-23</b>, when the documentation file
/// made the compiler read it and it refused a comment on nothing.
/// </summary>
internal static partial class Columns
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

            // OFF AT THE START SINCE 2026-09-02, ON THE OWNER'S DECISION, so the first screen is the
            // one somebody already knows from services.msc - display name, description, state,
            // start type and account, and never the internal name.
            //
            // WHAT IT COSTS WAS PUT TO HIM AND HE TOOK IT: the rollup badge lives in this cell, so
            // out of the box nothing says that 23 per-user copies were folded away. The status line
            // under the list still says it, and the column is one click away in Columns.
            //
            // `ADR-14` IS NOT BENT BY THIS. The internal name stays the identity everywhere it
            // matters - in the data, in a copy, in a plan and in every command this window prints.
            // What changed is which columns a stranger meets first.
            ShownAtFirst = false,
            Reads = entry => entry.ServiceName
        },
        new Column
        {
            Id = "displayName",
            LabelKey = "gui.column.displayName",
            WidthKey = "ColumnDisplayName",
            Face = ColumnFace.Text,
            ShownAtFirst = true,

            // THROUGH THE RULE HERE AS WELL AS IN EntryRow, AND THE REASON IS WORTH A LINE BECAUSE
            // A GREEN TEST SAID OTHERWISE. Reads takes an ScmEntry, not a row, so this column and
            // EntryRow.DisplayName are two separate paths to the same fact - a guard on the row
            // passed while the cell on screen still showed @todo.dll,-100. A photograph caught it.
            // Sorts is null here, so SortKey falls through to Reads, which means this line decides
            // the ORDER as well as the text: an at sign sorts before every letter, and these two
            // entries held the first two rows of the Drivers and All scopes until it was written.
            Reads = entry => ServiceDisplayName.Of(entry.DisplayName, entry.ServiceName)
        },
        new Column
        {
            // THE SECOND COLUMN OF services.msc, and the only one that answers "what even is this".
            //
            // ON AT THE START SINCE 2026-09-02, REVERSING WHAT STOOD HERE, on the owner's decision.
            // The sentence it replaces argued that prose in a narrow column shows a fragment and an
            // ellipsis, which is true and was the wrong thing to weigh: a stranger opening this
            // window meets three hundred names and no way to tell what any of them are for, and a
            // fragment of an answer beats none. Whoever disagrees turns it off in Columns.
            //
            // IT COSTS NOTHING EXTRA TO READ, and that was measured rather than assumed. Descriptions
            // come back on the same handle as the rest of the configuration - 212-223 ms over 819
            // entries, recorded beside the call in WindowsScmCatalog - so this column was always
            // paid for and never shown. What it costs to DRAW is not measured yet.
            Id = "description",
            LabelKey = "gui.column.description",
            WidthKey = "ColumnDescription",

            // PROSE SINCE 2026-09-01, WHICH IS WHAT LETS THE CHOSEN ROW SHOW MORE THAN A FRAGMENT -
            // backlog 192, owner's decision. The comment above this is the reason it was needed:
            // one line and an ellipsis over a field that runs to 1251 characters on this machine.
            Face = ColumnFace.Prose,
            ShownAtFirst = true,
            Reads = entry => CellFaces.Say(entry.Description, value => value)
        },
        new Column
        {
            Id = "status",
            LabelKey = "gui.column.status",
            WidthKey = "ColumnStatus",
            Face = ColumnFace.Status,
            ShownAtFirst = true,
            Reads = entry => CellFaces.StatusLabel(entry.Status),
            Marks = entry => CellFaces.StatusShape(entry.Status)
        },
        new Column
        {
            Id = "startType",
            LabelKey = "gui.column.startType",
            WidthKey = "ColumnStart",
            Face = ColumnFace.StartType,
            ShownAtFirst = true,
            Reads = entry => CellFaces.StartLabel(entry, StartQualifiers.Of(entry)),
            Marks = entry => CellFaces.StartShape(entry, StartQualifiers.Of(entry))
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

            // THE NAME A PERSON KNOWS, WITH THE SPELLING THE MANAGER HOLDS BEHIND IT, since
            // 2026-09-15. "NT AUTHORITY\Loca..." was what this column showed for a third of the
            // services on the owner's machine - the part that gave way was the part that told the
            // two service accounts apart. SystemAccounts carries the measurement and the reason
            // the spelling still travels with the cell rather than being replaced.
            Reads = entry => SystemAccounts.Shown(entry.Account),
            Holds = entry => SystemAccounts.Held(entry.Account)
        },
        new Column
        {
            Id = "processId",
            LabelKey = "gui.column.processId",
            WidthKey = "ColumnProcessId",
            Face = ColumnFace.Number,

            // OFF AT THE START SINCE 2026-09-02, ON THE OWNER'S DECISION, for the same reason as the
            // internal name: services.msc does not show it, and a number nobody asked for is width
            // taken from the description beside it. It is also empty for every stopped entry, which
            // on this machine is most of them - so the default view spent a column on blanks.
            ShownAtFirst = false,

            // NOT ONE DRIVER OF 472 HAS ONE - measured, and it is a fact about what a driver IS
            // rather than about this machine. A column that is empty in every row of a list is a
            // column spending width on nothing. It stays here now that the column is off at the
            // start everywhere: this says it must not come back for drivers even if that changes.
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
            // THE DEBT THE TWO `A11` SLICES LEFT BEHIND, PAID 2026-08-26. The field was in the
            // core, in `--json`, in the snapshot and in the query language, and the window could
            // FOLD by it while having nowhere to show it - which reads as the list hiding
            // something rather than as a column nobody got round to.
            //
            // Beside the entry type on purpose: a template that shares a process is a
            // SharedProcess and that is true, so these two answer different questions about the
            // same entry and are read together or not at all.
            Id = "perUserRole",
            LabelKey = "gui.column.perUserRole",
            WidthKey = "ColumnPerUserRole",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Reads = entry => CellFaces.PerUserRoleLabel(entry.PerUserRole)
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
            Reads = entry => CellFaces.Judgement(entry.RunsAgainstItsStartType),
            Marks = entry => CellFaces.AgainstShape(entry.RunsAgainstItsStartType)
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
            Needs = ExtraRead.Signatures,
            Reads = entry => CellFaces.SignatureLabel(entry.Signature),
            Outcome = entry => entry.Signature.Outcome
        },
        new Column
        {
            Id = "publisher",
            LabelKey = "gui.column.publisher",
            WidthKey = "ColumnPublisher",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Needs = ExtraRead.Signatures,
            Reads = entry => CellFaces.PublisherLabel(entry.Signature),
            Outcome = entry => entry.Signature.Outcome
        },
        new Column
        {
            Id = "fileVersion",
            LabelKey = "gui.column.fileVersion",
            WidthKey = "ColumnFileVersion",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Needs = ExtraRead.Signatures,
            Reads = entry => CellFaces.Say(entry.FileVersion, value => value),
            Outcome = entry => entry.FileVersion.Outcome
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

            // The same family as the signature and the version beside it, because one pass fills
            // all three - SecondPass reads the file once and writes the three answers it found.
            Needs = ExtraRead.Signatures,
            Reads = entry => CellFaces.Say(entry.BinaryHash, value => value),
            Outcome = entry => entry.BinaryHash.Outcome
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

            // ITS OWN FAMILY, AND THAT IS WHAT MAKES THIS COLUMN AFFORDABLE. Asking for memory
            // used to drag the signature verification along with it, because one method filled
            // both - so a column measured at under a millisecond over 110 processes would have
            // cost the seven and a half seconds the file reading costs over 810 entries. Split on
            // 2026-09-05, at Readings.FillAsync.
            Needs = ExtraRead.Memory,
            Reads = entry => CellFaces.MemoryLabel(entry.Memory),
            Outcome = entry => entry.Memory.Outcome,

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
            Reads = entry => CellFaces.Judgement(entry.RunsWhileDisabled),
            Marks = entry => CellFaces.AgainstShape(entry.RunsWhileDisabled)
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
            // WHAT BREAKS IF THIS STOPS - the other direction of the column below, 2026-09-06.
            //
            // ITS OWN COLUMN RATHER THAN A WIDER VERSION OF THAT ONE, and the reason is the same
            // one that keeps the two mismatch columns apart: they answer opposite questions and a
            // person arrives with one of them. "What does this need" is asked before starting
            // something. "What needs this" is asked before stopping something, and it is the only
            // one of the two that can talk somebody out of an action.
            //
            // IT IS THE ONLY COLUMN IN THIS CATALOGUE THAT COSTS A CALL PER ENTRY - 236-259 ms
            // over 313 services, measured 2026-09-05, against 423-500 ms for the whole listing. So
            // it declares a family and is read when somebody turns it on, exactly as the four
            // signature columns and the memory column are.
            Id = "requiredBy",
            LabelKey = "gui.column.requiredBy",
            WidthKey = "ColumnRequiredBy",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Needs = ExtraRead.RequiredBy,
            Reads = entry => CellFaces.Say(entry.RequiredBy, value => string.Join(", ", value)),
            Outcome = entry => entry.RequiredBy.Outcome
        },
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
    /// <see cref="Shortcuts"/> and the key press lives in the window. An order is a decision, so it
    /// lives where it can be asked without a desktop - what stays beside the grid is subscribing to
    /// an event and handing this over.
    ///
    /// The comparison itself is <see cref="CellOrder"/>, in its own file since 2026-08-31: the
    /// keeping of keys it now does needed more lines than this file had left under the ratchet, and
    /// the seam was already there - this file is the CATALOGUE of columns, that one is how two rows
    /// compare by one of them.
    /// </summary>
    internal static System.Collections.IComparer OrderedBy(Column column, bool ascending) =>
        new CellOrder(column, ascending);

    private static readonly Dictionary<string, Column> Index =
        All.ToDictionary(column => column.Id, StringComparer.Ordinal);

    private static string Number(int value) =>
        value.ToString(System.Globalization.CultureInfo.CurrentCulture);
}
