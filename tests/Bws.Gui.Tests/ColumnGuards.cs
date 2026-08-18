using Bws.Core;
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
        Assert.Equal(20, Columns.All.Count);

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
    /// Six before anybody chooses anything, and eleven waiting.
    ///
    /// The six are the ones the window had, minus RAM and plus the display name - owner's
    /// decision, 2026-08-11, because RAM belongs to the phase that does not exist and a column
    /// that can only say "nobody looked" is a promise the window cannot keep.
    /// </summary>
    [Fact]
    public void Six_columns_are_shown_before_anybody_chooses_anything()
    {
        Assert.Equal(6, Columns.All.Count(column => column.ShownAtFirst));

        Assert.Equal(
            ["serviceName", "displayName", "status", "startType", "account", "processId"],
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

    /// <summary>
    /// The process identifier is ordered as a NUMBER, and until 2026-08-11 it was not.
    ///
    /// <b>A defect found while rewriting the sort rather than a feature.</b> The column binds to
    /// text and DataGrid sorts by the binding path, so from the day sorting was turned on -
    /// 2026-08-10, backlog 150 - 103292 came before 9. Nothing could see it: the sort worked, the
    /// numbers moved, and only reading the column tells you the order is alphabetical.
    /// </summary>
    [Fact]
    public void The_process_id_is_ordered_as_a_number_rather_than_as_text()
    {
        var column = Columns.Of("processId")!;

        var small = Rows.Entry("Small") with { ProcessId = Reading<int>.Present(9) };
        var large = Rows.Entry("Large") with { ProcessId = Reading<int>.Present(103292) };

        Assert.True(
            column.SortKey(small)!.CompareTo(column.SortKey(large)) < 0,
            "9 has to come before 103292. Compared as text it does not, which is what this column did.");

        // And the text really is the other way round, so the claim above is measuring a difference
        // rather than agreeing with itself.
        Assert.True(
            string.CompareOrdinal(column.Reads(small), column.Reads(large)) > 0,
            "The cells no longer sort the wrong way as text, so this guard is proving nothing.");
    }

    /// <summary>An entry with no process sorts as nothing rather than as zero, which is a value.</summary>
    [Fact]
    public void An_entry_with_no_process_has_no_number_to_order_by()
    {
        var column = Columns.Of("processId")!;

        Assert.Null(column.SortKey(Rows.Stopped("Spooler")));
    }

    /// <summary>
    /// The order a column puts two rows in, asked of the comparison rather than of a grid.
    ///
    /// Ascending and descending are both claimed, because a comparer that returns the same answer
    /// whichever way round it was asked sorts perfectly and reverses nothing.
    /// </summary>
    [Fact]
    public void A_column_orders_two_rows_by_what_it_says_and_reverses_when_asked()
    {
        var column = Columns.Of("serviceName")!;

        var first = EntryRow.Of(Rows.Entry("Appinfo"));
        var last = EntryRow.Of(Rows.Entry("Winmgmt"));

        Assert.True(Columns.OrderedBy(column, ascending: true).Compare(first, last) < 0);
        Assert.True(Columns.OrderedBy(column, ascending: false).Compare(first, last) > 0);
    }

    /// <summary>
    /// A row with nothing in this column sorts below every row that has something, and reversing
    /// moves the whole group rather than scattering it.
    ///
    /// The same thing the empty string already does in every column that says nothing with one -
    /// a column where "no value" behaved differently would be a rule nobody could learn by using
    /// the list.
    /// </summary>
    [Fact]
    public void A_row_with_nothing_in_a_column_sorts_below_one_that_has_something()
    {
        var column = Columns.Of("processId")!;

        var running = EntryRow.Of(Rows.Entry("Spooler"));
        var stopped = EntryRow.Of(Rows.Stopped("Dhcp"));

        Assert.True(Columns.OrderedBy(column, ascending: true).Compare(stopped, running) < 0);
        Assert.True(Columns.OrderedBy(column, ascending: true).Compare(stopped, stopped) == 0);
        Assert.True(Columns.OrderedBy(column, ascending: false).Compare(stopped, running) > 0);
    }

    /// <summary>Anything that is not a row has nothing to compare, and says so rather than throwing.</summary>
    [Fact]
    public void Something_that_is_not_a_row_has_no_order()
    {
        var order = Columns.OrderedBy(Columns.Of("serviceName")!, ascending: true);

        Assert.Equal(0, order.Compare(null, null));
        Assert.True(order.Compare(null, EntryRow.Of(Rows.Entry("Spooler"))) < 0);
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
