using Bws.Core;
using Bws.Core.Querying;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The columns of `A8`, and the row that answers through them.
///
/// <b>Written against the CATALOGUE rather than against the window, on purpose.</b> What a column
/// is - its identifier, what its cell says, what it sorts by - has no WPF in it, so it can be
/// asked here without a desktop. What only a window can answer is whether turning one off changes
/// what is on screen, and that belongs to <c>tools/window-journey</c>, which counts the HEADINGS a
/// grid is showing rather than believing the object it just told to hide one.
/// </summary>
public sealed class ColumnGuards
{
    /// <summary>
    /// The arithmetic behind the number, kept where it can fail.
    ///
    /// `ScmEntry` carries 23 fields and one derived answer. Four are refused because the second
    /// phase of `ADR-13` reads them and the window has no second phase. That leaves twenty, and
    /// the day somebody adds a field this test says so rather than the column quietly not existing.
    ///
    /// <b>TWENTY SINCE 2026-08-17, and the two that moved it were already on screen</b> - backlog
    /// 190, on the owner's report that the window shows too few columns. The delayed start and
    /// whether the binary is on disk were folded into the start type cell as qualifiers, which is
    /// readable for one row and unsortable and unscannable for a machine. Eighteen since 2026-08-12
    /// before that, when the description arrived - backlog 171.
    ///
    /// <b>THE NUMBER LEFT THIS TEST'S NAME ON 2026-08-17 AND THAT IS THE POINT OF THE RENAME.</b>
    /// It was called Eighteen_columns_are_offered..., which had to be false before anybody could
    /// notice it needed changing - a name that has to lie first is a name that gets renamed under
    /// pressure, and this project deprecates identifiers rather than renaming them precisely
    /// because renames are where mistakes hide. The count is asserted in the body, where being
    /// wrong reddens instead of merely reading oddly.
    /// </summary>
    [Fact]
    public void Every_column_is_offered_and_each_is_named_exactly_once()
    {
        // TWENTY EIGHT SINCE 2026-09-06, and the one that moved it went the other way round from
        // the last: `dependents` was read by the PLAN and by nothing a person could see. The
        // manager has always been asked who stands on an entry before a cascade, and there was no
        // column, no field in the query language and no line in a snapshot. Owner's decision, and
        // it took the schema version with it.
        //
        // Twenty seven since 2026-08-26, and that one was a debt too: `perUserRole` was in the
        // core, in `--json`, in the snapshot and in the query language, and the window could FOLD
        // by it while having nowhere to show it.
        Assert.Equal(28, Columns.All.Count);

        var twice = Columns.All
            .GroupBy(column => column.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.True(
            twice.Count == 0,
            "Two columns share an identifier. S6d3 writes these into a file people will keep, so a "
            + "collision here is a layout that cannot say which column it means:"
            + Environment.NewLine + string.Join(Environment.NewLine, twice));

        Assert.All(Columns.All, column => Assert.False(string.IsNullOrWhiteSpace(column.Id)));
        Assert.All(Columns.All, column => Assert.False(string.IsNullOrWhiteSpace(column.WidthKey)));
    }

    /// <summary>
    /// Five before anybody chooses anything, and they are the five services.msc opens on.
    ///
    /// <b>SIX UNTIL 2026-09-02, AND THE CHANGE WAS THE OWNER'S.</b> The internal name and the
    /// process id went off and the description came on, so that somebody who has used the Windows
    /// tool meets the same five answers in the same places: what it is called, what it is for,
    /// whether it runs, when it starts, and who it runs as.
    ///
    /// <b>The list is written out rather than counted, and that is the half worth keeping.</b> A
    /// count alone goes green on any five, so a session swapping one column for another would not
    /// be noticed - and which five is the whole of the decision.
    ///
    /// Earlier: the six were the window's original set minus RAM and plus the display name, owner's
    /// decision 2026-08-11, because RAM belongs to a phase that does not exist and a column that can
    /// only say "nobody looked" is a promise the window cannot keep.
    /// </summary>
    [Fact]
    public void Five_columns_are_shown_before_anybody_chooses_anything()
    {
        Assert.Equal(5, Columns.All.Count(column => column.ShownAtFirst));

        Assert.Equal(
            ["displayName", "description", "status", "startType", "account"],
            Columns.All.Where(column => column.ShownAtFirst).Select(column => column.Id));
    }

    /// <summary>
    /// Every column answers about an ordinary entry, and none of them throws doing it.
    ///
    /// <b>The claim is that it ANSWERS, not that the answer is right.</b> Eleven of these read
    /// parts of an entry nothing in this window had ever rendered, and the first way that goes
    /// wrong is an exception inside a cell - which WPF swallows into a binding failure, leaving a
    /// blank cell and nothing anywhere saying why. Backlog 19.
    /// </summary>
    [Fact]
    public void Every_column_says_something_about_an_ordinary_entry()
    {
        var entry = Rows.Entry("Spooler");

        Assert.All(Columns.All, column => Assert.NotNull(column.Reads(entry)));
    }

    /// <summary>
    /// <b>Written 2026-09-02 after a photograph caught what a green suite had not.</b> Backlog 309.
    /// Two entries on an ordinary machine hand back an indirection nobody resolved instead of a
    /// label. The rule that replaces it was applied to <see cref="EntryRow"/>, a guard on the row
    /// went green, and the cell on screen still read <c>@todo.dll,-100;Microsoft IPv6 Pro...</c> -
    /// because <see cref="Column.Reads"/> takes an entry rather than a row, so the column is its own
    /// path to the same fact.
    ///
    /// <b>The order matters as much as the text and is asserted with it.</b> This column declares no
    /// <c>Sorts</c>, so <see cref="Column.SortKey"/> falls through to <c>Reads</c> - which means an
    /// at sign sorting before every letter put these two at the very top of the Drivers and All
    /// scopes. Measured the same day over 799 entries: positions 0 and 1.
    /// </summary>
    [Fact]
    public void The_display_name_column_shows_a_label_rather_than_an_indirection()
    {
        var column = Columns.All.Single(column => column.Id == "displayName");

        var readable = Rows.Entry("Tcpip6", "@todo.dll,-100;Microsoft IPv6 Protocol Driver");
        var bare = Rows.Entry("tcpipreg", @"@%SystemRoot%\System32\drivers\tcpipreg.sys,-10110,");

        Assert.Equal("Microsoft IPv6 Protocol Driver", column.Reads(readable));
        Assert.Equal("tcpipreg", column.Reads(bare));

        // The sort key is the same string, so nothing sorts under a punctuation mark any more.
        Assert.Equal("Microsoft IPv6 Protocol Driver", column.SortKey(readable));
        Assert.Equal("tcpipreg", column.SortKey(bare));
    }

    /// <summary>
    /// The row and the column agree, which is what makes the indexer the only path.
    ///
    /// Two ways to reach one cell would drift the first time somebody changed one of them, and
    /// nothing about a green build would notice.
    /// </summary>
    [Fact]
    public void A_row_says_through_a_column_exactly_what_that_column_reads()
    {
        var entry = Rows.Entry("Spooler");
        var row = EntryRow.Of(entry);

        Assert.All(Columns.All, column => Assert.Equal(column.Reads(entry), row[column.Id]));
    }

    /// <summary>
    /// An identifier nothing knows comes back as itself.
    ///
    /// The same answer <see cref="Bws.Gui.Texts"/> gives a key nothing declares, and for the same
    /// reason: a layout naming a column that has gone away should look wrong on screen rather than
    /// render as an empty cell, which is the one thing an empty cell must never mean. It is how a
    /// layout from a future version will fail when S6d3 can load one.
    /// </summary>
    [Fact]
    public void A_column_nothing_knows_comes_back_as_itself()
    {
        var row = EntryRow.Of(Rows.Entry("Spooler"));

        Assert.Equal("noSuchColumn", row["noSuchColumn"]);
    }

    /// <summary>
    /// A row that moved says its CELLS changed, and this asserts the notice rather than the value.
    ///
    /// <b>Asking the row what it says would prove nothing.</b> A cell is worked out at the moment
    /// it is asked, so it answers correctly whether or not anybody was ever told to ask - and what
    /// a binding needs is the telling. This is the same trap a mutation entry caught on the chips
    /// one slice earlier, where the test read <c>IsOn</c> directly and stayed green through a
    /// broken notification.
    /// </summary>
    [Fact]
    public void A_row_that_takes_a_cheap_reading_says_its_cells_changed()
    {
        var row = EntryRow.Of(Rows.Entry("Spooler"));
        var said = new List<string?>();

        row.PropertyChanged += (_, changed) => said.Add(changed.PropertyName);

        row.Absorb(
            new ScmStatus("Spooler", EntryStatus.Stopped, Reading<int>.Absent()),
            DateTimeOffset.UnixEpoch);

        Assert.Contains("Item[]", said);
    }

    /// <summary>
    /// The mark about the start type follows a CHEAP reading, not only a full one - backlog 165.
    ///
    /// <b>This is the trap that column carries.</b> Whether an entry runs against its start type is
    /// worked out from the start type AND the running state, and the running state is what the
    /// per-second reading watches. Nothing about the configuration changes when a service stops -
    /// so a mark recomputed only on a full reading would be true when the window opened and
    /// quietly false a second later, which is worse than no mark at all.
    ///
    /// It asserts the VALUE here rather than the notice, and that is the opposite of the two tests
    /// below on purpose: this is a property with a backing field, so the question is whether
    /// anything recomputed it, not whether anybody was told.
    /// </summary>
    [Fact]
    public void The_mark_about_the_start_type_follows_a_cheap_reading()
    {
        // Automatic and running, which is the ordinary case and wears no mark.
        var row = EntryRow.Of(Rows.Entry("Spooler"));

        Assert.Equal(CellShapes.Ordinary, row.AgainstShape);

        // And then it stops, with nothing waiting to start it. Same configuration, different answer.
        row.Absorb(
            new ScmStatus("Spooler", EntryStatus.Stopped, Reading<int>.Absent()),
            DateTimeOffset.UnixEpoch);

        Assert.Equal(CellShapes.Against, row.AgainstShape);
    }

    /// <summary>
    /// A PER-USER TEMPLATE WEARS NO MARK, AND THE BRANCH THAT WOULD HAVE GIVEN IT ONE SAID
    /// "I COULD NOT CHECK".
    ///
    /// <b>Backlog 235, and this is the half of it that lives in the window.</b> The reading behind
    /// this column became absent for a template on 2026-08-26, and the switch that renders it had
    /// a comment saying absent was a state it could never produce - true when written, false that
    /// day. Left alone, the default branch would have put the unknown mark beside every template:
    /// the same false alarm the change was removing, wearing a different word.
    ///
    /// <b>The cell beside the mark is asserted too</b>, because the two channels are what this
    /// column is - a mark with no word is exactly the failure this file's own header describes.
    /// Empty is right here and is not a blank standing in for a refusal: the reading is absent,
    /// and absent means the question does not apply.
    /// </summary>
    [Fact]
    public void A_per_user_template_wears_no_mark_about_a_start_type_it_cannot_disobey()
    {
        var template = EntryRow.Of(Rows.Template("CDPUserSvc"));

        Assert.Equal(CellShapes.Ordinary, template.AgainstShape);
        Assert.Equal(string.Empty, CellFaces.Judgement(Rows.Template("CDPUserSvc").RunsAgainstItsStartType));

        // And the session copy is still judged, or this passes on a change that silenced the
        // whole family - an instance that failed to come up is a real fault.
        var instance = Rows.Instance("CDPUserSvc_7b537") with
        {
            Status = EntryStatus.Stopped,
            StartType = Reading<StartType>.Present(StartType.Automatic),
            Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Absent()
        };

        Assert.Equal(CellShapes.Against, EntryRow.Of(instance).AgainstShape);
    }

    /// <summary>
    /// The per-user role says which side of the family a row is on, in words rather than a blank.
    ///
    /// <b>None is a word and that is this window's rule rather than a preference</b> - an empty
    /// cell means "there is genuinely nothing" everywhere else here, and most of the machine
    /// having a measured answer of "not part of this family" is not nothing.
    /// </summary>
    [Theory]
    [InlineData(PerUserRole.None, "Not per-user")]
    [InlineData(PerUserRole.Template, "Template")]
    [InlineData(PerUserRole.Instance, "Session copy")]
    public void The_per_user_role_reads_as_words(PerUserRole role, string expected)
    {
        Assert.Equal(expected, CellFaces.PerUserRoleLabel(role));
    }

    /// <summary>
    /// And the full reading, which says so even when nothing it compares moved.
    ///
    /// <b>This is the half that is easy to get wrong and the reason the notice is
    /// unconditional.</b> Whether the row MOVED is decided over five fields, and eleven columns
    /// read parts of the entry none of those five touch. An entry whose privileges changed and
    /// whose status did not is not a row that moved - and its cell still has to be re-read.
    /// </summary>
    [Fact]
    public void A_row_says_its_cells_changed_even_when_nothing_it_calls_movement_did()
    {
        var before = Rows.Entry("Spooler");
        var row = EntryRow.Of(before);
        var said = new List<string?>();

        row.PropertyChanged += (_, changed) => said.Add(changed.PropertyName);

        var after = before with
        {
            RequiredPrivileges = Reading<IReadOnlyList<string>>.Present(["SeShutdownPrivilege"])
        };

        var moved = row.Absorb(after, DateTimeOffset.UnixEpoch);

        Assert.False(moved, "A change of privileges is not the row moving, and should not light it up.");
        Assert.Contains("Item[]", said);
        Assert.Equal("SeShutdownPrivilege", row["requiredPrivileges"]);
    }

    /// <summary>
    /// The four read states stay apart in the new columns, which is rule 8 arriving in twelve
    /// places that never had to keep it before.
    ///
    /// <b>The description joined on 2026-08-12 and it is the one entry here that is not
    /// hypothetical.</b> Every other column in this list is refused only under a restricted token -
    /// on an elevated session they read fine. Eight entries of 809 refuse their description to an
    /// ELEVATED session, because the manager hands back an unresolved indirection, so this is the
    /// first column where "I could not read it" is a state a person will actually meet.
    ///
    /// Present says the value, absent says nothing at all because that is the one state a blank
    /// cell tells the truth about, and the other two say so in words. A column where "I was not
    /// allowed to look" renders like "there is none" is a snapshot of a machine that looks
    /// complete and is not.
    /// </summary>
    [Theory]
    [InlineData("description")]
    [InlineData("binaryPath")]
    [InlineData("binaryFile")]
    [InlineData("loadOrderGroup")]
    [InlineData("securityDescriptor")]
    [InlineData("sidType")]
    [InlineData("errorControl")]
    [InlineData("dependsOn")]
    [InlineData("triggers")]
    [InlineData("requiredPrivileges")]
    public void The_four_read_states_do_not_look_alike_in_a_new_column(string id)
    {
        var absent = EntryRow.Of(Refused(ReadOutcome.Absent))[id];
        var denied = EntryRow.Of(Refused(ReadOutcome.Denied))[id];
        var unread = EntryRow.Of(Refused(ReadOutcome.NotRead))[id];

        Assert.Equal(string.Empty, absent);
        Assert.NotEqual(string.Empty, denied);
        Assert.NotEqual(string.Empty, unread);
        Assert.NotEqual(denied, unread);
    }

    /// <summary>
    /// The one column whose four states are NOT the four states of the field behind it, because it
    /// is a judgement rather than a value - and it never says "there is none".
    ///
    /// <b>Written as its own test after the one above failed on it, and the PRODUCT was right.</b>
    /// The theory expected an absent reading to render blank, which is true of every field a
    /// person can look up. This is not one: `ScmEntry` maps an absent start type to a judgement of
    /// NotRead, because "there is no answer to whether this entry is running as configured" is a
    /// thing nobody could work out, not a thing that is genuinely absent. An empty cell here would
    /// read as "nothing wrong", which is exactly the claim nobody is entitled to make.
    ///
    /// So what is asserted is the property that does hold: three states, three different words,
    /// and none of them silence.
    /// </summary>
    [Fact]
    public void The_derived_judgement_never_renders_as_there_being_nothing()
    {
        var absent = EntryRow.Of(Refused(ReadOutcome.Absent))["runsAgainstItsStartType"];
        var denied = EntryRow.Of(Refused(ReadOutcome.Denied))["runsAgainstItsStartType"];
        var plain = EntryRow.Of(Rows.Entry("Spooler"))["runsAgainstItsStartType"];

        Assert.NotEqual(string.Empty, absent);
        Assert.NotEqual(string.Empty, denied);
        Assert.NotEqual(string.Empty, plain);
        Assert.NotEqual(absent, denied);
        Assert.NotEqual(absent, plain);
    }

    /// <summary>
    /// A list in a cell is joined rather than counted.
    ///
    /// `docs/11` opens by saying this window was built as a table when an administrator needs
    /// something to search with, and a cell saying "3" answers how many when the question is
    /// which. The names are the manager's own, including the plus that marks a load order group.
    /// </summary>
    [Fact]
    public void A_list_in_a_cell_says_the_names_rather_than_how_many_there_are()
    {
        var entry = Rows.Entry("RemoteAccess") with
        {
            DependsOn = Reading<IReadOnlyList<string>>.Present(["RpcSs", "+NetBIOSGroup"])
        };

        var cell = EntryRow.Of(entry)["dependsOn"];

        Assert.Contains("RpcSs", cell, StringComparison.Ordinal);
        Assert.Contains("+NetBIOSGroup", cell, StringComparison.Ordinal);
        Assert.DoesNotContain("2", cell, StringComparison.Ordinal);
    }

    /// <summary>
    /// One kind of trigger is said once, however many triggers of it there are.
    ///
    /// Four custom triggers reading "Custom, Custom, Custom, Custom" is four times the noise for
    /// none of the answer. Which exact device class belongs to a details panel, and
    /// <see cref="ServiceTrigger"/> says so itself.
    /// </summary>
    [Fact]
    public void A_kind_of_trigger_is_named_once_however_many_there_are()
    {
        var entry = Rows.Entry("BthAvctpSvc") with
        {
            Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Present(
            [
                new ServiceTrigger(TriggerKind.DeviceArrival, TriggerAction.Start),
                new ServiceTrigger(TriggerKind.DeviceArrival, TriggerAction.Start),
                new ServiceTrigger(TriggerKind.IpAddress, TriggerAction.Start)
            ])
        };

        var cell = EntryRow.Of(entry)["triggers"];
        var arrivals = Bws.Gui.Texts.Of("gui.cell.trigger.deviceArrival");

        Assert.Equal(
            1,
            cell.Split(arrivals, StringSplitOptions.None).Length - 1);

        Assert.Contains(Bws.Gui.Texts.Of("gui.cell.trigger.ipAddress"), cell, StringComparison.Ordinal);
    }

    [Fact]
    public void The_last_column_left_showing_may_not_be_turned_off()
    {
        var bar = new ColumnBar();

        foreach (var choice in bar.Choices.Where(choice => choice.IsShown).Skip(1).ToList())
        {
            choice.IsShown = false;
        }

        var alone = bar.Choices.Single(choice => choice.IsShown);

        Assert.False(
            alone.MayHide,
            "A list with no columns at all is 809 rows of nothing above a count line still saying "
            + "809, which is the empty rectangle docs/11 complaint 9 is about arriving by a door "
            + "nobody thought to close.");
    }

    /// <summary>
    /// Turning a second one on releases the first, and nothing about that click mentions the first.
    ///
    /// The same shape as the chips: one edit moves several controls, so everything is worked out
    /// again rather than only the thing that was touched.
    /// </summary>
    [Fact]
    public void Turning_a_second_column_on_lets_the_first_be_turned_off_again()
    {
        var bar = new ColumnBar();

        foreach (var choice in bar.Choices.Where(choice => choice.IsShown).Skip(1).ToList())
        {
            choice.IsShown = false;
        }

        var alone = bar.Choices.Single(choice => choice.IsShown);

        bar.Choices.First(choice => !choice.IsShown).IsShown = true;

        Assert.True(alone.MayHide);
    }

    /// <summary>
    /// Every column that reads a Reading, with every one of them in the same state.
    ///
    /// Built rather than listed, so a field added to the entry arrives here in whatever state this
    /// asks for instead of quietly keeping the one <see cref="Rows"/> gives it.
    /// </summary>
    private static ScmEntry Refused(ReadOutcome outcome) => Rows.Entry("Spooler") with
    {
        StartType = Read<Core.StartType>(outcome),
        Triggers = Read<IReadOnlyList<ServiceTrigger>>(outcome),
        DependsOn = Read<IReadOnlyList<string>>(outcome),
        RequiredPrivileges = Read<IReadOnlyList<string>>(outcome),
        BinaryPath = Read<string>(outcome),
        BinaryFile = Read<string>(outcome),
        LoadOrderGroup = Read<string>(outcome),
        Description = Read<string>(outcome),
        SecurityDescriptor = Read<string>(outcome),
        SidType = Read<ServiceSidType>(outcome),
        ErrorControl = Read<ErrorControl>(outcome)
    };

    private static Reading<T> Read<T>(ReadOutcome outcome) => outcome switch
    {
        ReadOutcome.Absent => Reading<T>.Absent(),

        // The number and the sentence together, because a refusal always carries both and this is
        // the state where inventing either of them would be inventing a fact about the machine.
        ReadOutcome.Denied => Reading<T>.Denied(5, "Access is denied."),
        _ => Reading<T>.NotRead()
    };
}
