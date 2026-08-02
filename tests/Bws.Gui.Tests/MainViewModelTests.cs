using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What the window will show, checked without opening one.
///
/// The list is the whole of these three slices, and the interesting decisions in it are not
/// about markup: which entries appear, what a cell says when the value behind it is one of the
/// four read states, what happens to the list when the query is half typed or wrong, and what
/// a refresh does to the row somebody is pointing at.
///
/// A screenshot is still the other half of the check, and it is a person's job. This half is
/// the one that can fail on its own at three in the morning - and it is the only half that can
/// look at a service stopping between two readings, which a real machine will not do on cue.
/// </summary>
public sealed class MainViewModelTests
{
    [Fact]
    public async Task Every_entry_the_manager_hands_over_becomes_a_row()
    {
        // Nothing filtered, nothing collapsed, nothing dropped, because nothing was asked for.
        // A list that quietly showed fewer entries than the machine has would be the silence
        // rule 8 forbids, in the one place a person would believe it.
        var model = await Loaded(Entry("Spooler"), Entry("BFE"), Entry("Winmgmt"));

        Assert.Equal(["Spooler", "BFE", "Winmgmt"], model.Rows.Select(row => row.ServiceName));
    }

    [Fact]
    public async Task The_line_under_the_list_says_how_many_there_are()
    {
        var model = await Loaded(Entry("Spooler"));

        Assert.Contains("1", model.Status, StringComparison.Ordinal);
        Assert.False(model.Incomplete);
        Assert.Equal(string.Empty, model.Notice);
        Assert.Equal(string.Empty, model.Problem);
    }

    [Fact]
    public async Task A_manager_that_will_not_open_is_reported_instead_of_taking_the_window_down()
    {
        // The window has to survive it and say what happened. A dialog nobody can act on, or
        // a crash, would both be worse than a sentence under an empty list.
        var machine = new LiveMachine(Entry("Spooler")) { FailNext = new InvalidOperationException("no manager here") };
        var model = new MainViewModel(machine, new SteppedClock());

        await model.LoadAsync();

        Assert.Empty(model.Rows);
        Assert.True(model.Incomplete);
        Assert.Contains("no manager here", model.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_four_read_states_do_not_look_alike_in_a_cell()
    {
        // The whole reason this project has four states, arriving at the last place it could
        // be undone. A value nobody could read must not render like a value nobody has - one
        // of those says the machine is set up that way, the other says we do not know.
        var model = await Loaded(
            Entry("Present") with { Account = Reading<string>.Present("LocalSystem") },
            Entry("Absent") with { Account = Reading<string>.Absent() },
            Entry("Denied") with { Account = Reading<string>.Denied(5, "Access is denied.") },
            Entry("NotRead") with { Account = Reading<string>.NotRead() });

        var cells = model.Rows.ToDictionary(row => row.ServiceName, row => row.Account, StringComparer.Ordinal);

        Assert.Equal("LocalSystem", cells["Present"]);
        Assert.Equal(string.Empty, cells["Absent"]);

        // The two that are not values say so in words, and they do not say the same words.
        Assert.NotEqual(string.Empty, cells["Denied"]);
        Assert.NotEqual(string.Empty, cells["NotRead"]);
        Assert.NotEqual(cells["Denied"], cells["NotRead"]);
    }

    [Fact]
    public async Task Typing_a_query_narrows_the_list_and_the_count_says_out_of_how_many()
    {
        var model = await Loaded(Entry("Spooler"), Stopped("BITS"), Entry("Winmgmt"));

        model.QueryText = "status:running";

        Assert.Equal(["Spooler", "Winmgmt"], model.Rows.Select(row => row.ServiceName));

        // Both numbers, because "2 entries" on a filtered list reads as a machine with two
        // services on it.
        Assert.Contains("2", model.Status, StringComparison.Ordinal);
        Assert.Contains("3", model.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Emptying_the_box_brings_everything_back()
    {
        var model = await Loaded(Entry("Spooler"), Stopped("BITS"));

        model.QueryText = "status:running";
        model.QueryText = string.Empty;

        Assert.Equal(2, model.Rows.Count);
    }

    [Fact]
    public async Task A_query_with_a_mistake_leaves_the_list_alone_and_says_what_is_wrong()
    {
        // The sentence "a query with a mistake filters nothing" means the opposite of what it
        // means in a terminal. There it prints nothing, because a terminal writes into pipes.
        // Here the list a person was looking at stays on screen and the mistake is reported
        // beside it - docs/07, "nie filtruje niczego znaczy co innego w oknie i w terminalu".
        var model = await Loaded(Entry("Spooler"), Stopped("BITS"));

        model.QueryText = "status:running";
        model.QueryText = "status:runing";

        Assert.Single(model.Rows);
        Assert.Equal("Spooler", model.Rows[0].ServiceName);

        // And the complaint carries the way out, not merely the fact of a mistake.
        Assert.Contains("running", model.Problem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_half_typed_member_is_not_a_mistake()
    {
        // Validation runs on every keystroke, so it sees "sta", then "status", then "status:".
        // None of those is somebody's error and a box that reddens through most of the typing
        // is a box people learn to ignore.
        var model = await Loaded(Entry("Spooler"), Stopped("BITS"));

        foreach (var halfway in new[] { "sta", "status", "status:" })
        {
            model.QueryText = halfway;

            Assert.Equal(string.Empty, model.Problem);
        }

        // The last one selects everything, because a member with nothing after the colon is
        // dropped rather than answered.
        Assert.Equal(2, model.Rows.Count);
    }

    [Fact]
    public async Task The_drivers_switch_writes_its_member_into_the_box()
    {
        var model = await Loaded(Entry("Spooler"), Driver("disk"));

        Assert.True(model.ShowDrivers);
        Assert.Equal(2, model.Rows.Count);

        model.ShowDrivers = false;

        // Visible, in the box, in the language. This is A5's promise arriving early: what was
        // clicked is what can be read, and what can be read can be pasted into a terminal.
        Assert.Equal("!type:driver", model.QueryText);
        Assert.Equal(["Spooler"], model.Rows.Select(row => row.ServiceName));

        model.ShowDrivers = true;

        Assert.Equal(string.Empty, model.QueryText);
        Assert.Equal(2, model.Rows.Count);
    }

    [Fact]
    public async Task The_switch_keeps_the_rest_of_the_query_when_it_writes_and_when_it_takes_back()
    {
        var model = await Loaded(Entry("Spooler"), Driver("disk"));

        model.QueryText = "status:running";
        model.ShowDrivers = false;

        Assert.Equal("status:running !type:driver", model.QueryText);

        model.ShowDrivers = true;

        Assert.Equal("status:running", model.QueryText);
    }

    [Fact]
    public async Task Writing_the_exclusion_by_hand_moves_the_switch()
    {
        // The trip in the other direction, which is what stops the switch and the box from
        // telling a person two different things.
        var model = await Loaded(Entry("Spooler"), Driver("disk"));

        model.QueryText = "!TYPE:Driver";

        Assert.False(model.ShowDrivers);
    }

    [Fact]
    public async Task The_switch_will_not_undo_what_it_could_not_have_written()
    {
        // A known limit, pinned so it stays known. The switch appends at the end and takes
        // back from the end, because cutting a member out of the middle of text that may hold
        // quotes is the scanner's job, and a second scanner in the window would drift from the
        // real one in silence. Somebody who put the exclusion first keeps it, and the switch
        // says so by going back to where it was instead of pretending.
        var model = await Loaded(Entry("Spooler"), Driver("disk"));

        model.QueryText = "!type:driver status:running";

        Assert.False(model.ShowDrivers);

        model.ShowDrivers = true;

        Assert.Equal("!type:driver status:running", model.QueryText);
        Assert.False(model.ShowDrivers);
    }

    [Fact]
    public async Task With_the_regex_switch_on_a_bare_word_is_an_expression()
    {
        var model = await Loaded(Entry("Spooler"), Entry("SpoolerAgent"), Entry("Winmgmt"));

        model.QueryText = "^spooler$";

        // Off, that is a search for a fragment nobody has, because the characters are part of
        // the text being looked for.
        Assert.Empty(model.Rows);

        model.BareWordsAreExpressions = true;

        Assert.Equal(["Spooler"], model.Rows.Select(row => row.ServiceName));
    }

    [Fact]
    public async Task The_regex_switch_leaves_members_with_a_field_alone()
    {
        // Only the search half changes. Somebody who learned that name:spool* is a wildcard
        // does not have to find out that a switch elsewhere silently made it something else.
        var model = await Loaded(Entry("Spooler"), Entry("Winmgmt"));

        model.BareWordsAreExpressions = true;
        model.QueryText = "name:spool*";

        Assert.Equal(["Spooler"], model.Rows.Select(row => row.ServiceName));
    }

    [Fact]
    public async Task An_expression_that_will_not_compile_is_reported_and_the_list_stays()
    {
        var model = await Loaded(Entry("Spooler"), Entry("Winmgmt"));

        model.BareWordsAreExpressions = true;
        model.QueryText = "spooler";

        Assert.Single(model.Rows);

        model.QueryText = "spooler(";

        Assert.Single(model.Rows);
        Assert.NotEqual(string.Empty, model.Problem);
    }

    [Fact]
    public async Task Asking_about_signatures_says_nobody_read_them_rather_than_that_they_were_refused()
    {
        // The window reads what a listing reads. The command line answers this question by
        // going and verifying every binary, which costs seconds - a price a search box cannot
        // pay on every keystroke, so here the honest answer is that nobody looked.
        //
        // And it must not be phrased as a refusal. "The machine would not let us" and "nobody
        // asked" are the two states this project spends most of its rules keeping apart, and
        // they arrive from the query engine as one number.
        var model = await Loaded(Entry("Spooler"), Entry("Winmgmt"));

        model.QueryText = "signed:no";

        Assert.Empty(model.Rows);
        Assert.Equal(string.Empty, model.Problem);
        Assert.Contains(Bws.Gui.Texts.Of("gui.query.unreadSignatures"), model.Notice, StringComparison.Ordinal);
        Assert.DoesNotContain(
            Bws.Gui.Texts.Of("gui.status.partial", 2), model.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_result_judged_on_something_the_machine_refused_says_so()
    {
        // The other case, where the sentence about a refusal is the true one: nothing expensive
        // was asked about, and the field really was denied.
        var model = await Loaded(
            Entry("Spooler"),
            Entry("Denied") with { Account = Reading<string>.Denied(5, "Access is denied.") });

        model.QueryText = "account:LocalSystem";

        Assert.Equal(["Spooler"], model.Rows.Select(row => row.ServiceName));
        Assert.NotEqual(string.Empty, model.Notice);
    }

    // ---------------------------------------------------------------------------------
    // S6c: the list lives
    // ---------------------------------------------------------------------------------

    [Fact]
    public async Task A_service_stopped_from_outside_shows_up_without_anybody_pressing_anything()
    {
        var machine = new LiveMachine(Entry("Spooler"), Entry("BITS"));
        var model = await Loaded(machine);

        machine.Stop("Spooler");
        await model.RefreshAsync();

        Assert.Equal("Stopped", model.Rows.Single(row => row.ServiceName == "Spooler").Status);
    }

    [Fact]
    public async Task The_row_is_the_same_object_afterwards_and_that_is_what_saves_the_selection()
    {
        // The whole mechanism behind "it does not jump under your cursor". A window binds to
        // these objects, and a selection is a reference to one of them - swap in new ones every
        // second and the selection goes with them, along with the scroll position.
        var machine = new LiveMachine(Entry("Spooler"), Entry("BITS"));
        var model = await Loaded(machine);

        var before = model.Rows.Single(row => row.ServiceName == "Spooler");

        machine.Stop("Spooler");
        await model.RefreshAsync();

        Assert.Same(before, model.Rows.Single(row => row.ServiceName == "Spooler"));
        Assert.Equal("Stopped", before.Status);
    }

    [Fact]
    public async Task A_refresh_asks_the_cheap_question_and_not_the_expensive_one()
    {
        // The reason this can run once a second at all. Measured on the real machine: 13-22 ms
        // for the cheap reading against 423-500 ms for a full one, over 810 entries.
        var machine = new LiveMachine(Entry("Spooler"), Entry("BITS"));
        var model = await Loaded(machine);

        var fullReadsAfterLoading = machine.FullReads;

        machine.Stop("Spooler");
        await model.RefreshAsync();
        await model.RefreshAsync();

        Assert.Equal(2, machine.StatusReads);
        Assert.Equal(fullReadsAfterLoading, machine.FullReads);
    }

    [Fact]
    public async Task A_row_that_moved_is_marked_and_stops_being_marked_a_few_seconds_later()
    {
        // A10 rule three: a change has to be visible rather than stealthy. Somebody looking at
        // another part of the screen has to be able to see that something happened.
        var machine = new LiveMachine(Entry("Spooler"), Entry("BITS"));
        var clock = new SteppedClock();
        var model = new MainViewModel(machine, clock);

        await model.LoadAsync();

        // A first reading marks nothing. Everything is new, and a list that opened with every
        // row lit would be telling somebody that their whole machine just changed.
        Assert.All(model.Rows, row => Assert.False(row.RecentlyChanged));

        machine.Stop("Spooler");
        await model.RefreshAsync();

        Assert.True(model.Rows.Single(row => row.ServiceName == "Spooler").RecentlyChanged);
        Assert.False(model.Rows.Single(row => row.ServiceName == "BITS").RecentlyChanged);

        clock.Advance(MainViewModel.HighlightFor + TimeSpan.FromSeconds(1));
        model.FadeHighlights();

        Assert.All(model.Rows, row => Assert.False(row.RecentlyChanged));
    }

    [Fact]
    public async Task A_service_that_stops_leaves_a_list_of_running_ones()
    {
        // The refresh has to run the query again, or the list keeps showing an entry that no
        // longer answers it.
        var machine = new LiveMachine(Entry("Spooler"), Entry("BITS"));
        var model = await Loaded(machine);

        model.QueryText = "status:running";

        Assert.Equal(2, model.Rows.Count);

        machine.Stop("Spooler");
        await model.RefreshAsync();

        Assert.Equal(["BITS"], model.Rows.Select(row => row.ServiceName));
    }

    [Fact]
    public async Task A_service_installed_from_outside_appears_with_all_of_its_columns()
    {
        // The cheap reading knows a name arrived and nothing else about it - everything else a
        // row shows is configuration. So a name nobody knows costs a full reading, which is
        // rare enough to be worth half a second and better than a row with two columns saying
        // "unknown" for no reason anybody could work out.
        var machine = new LiveMachine(Entry("Spooler"));
        var model = await Loaded(machine);

        var fullReadsBefore = machine.FullReads;
        var stayed = model.Rows.Single(row => row.ServiceName == "Spooler");

        machine.Install(Entry("Fresh") with { Account = Reading<string>.Present("NT AUTHORITY\\LocalService") });
        await model.RefreshAsync();

        Assert.Equal(fullReadsBefore + 1, machine.FullReads);

        var arrived = model.Rows.Single(row => row.ServiceName == "Fresh");

        Assert.Equal("NT AUTHORITY\\LocalService", arrived.Account);
        Assert.Equal("Automatic", arrived.StartType);

        // And the rows that were already there are the same objects afterwards. A full reading
        // happens on F5 too, and one that built every row again would drop the selection every
        // time somebody asked for fresh data - which is the habit A10 exists to break, arriving
        // through the one door a person opened deliberately.
        Assert.Same(stayed, model.Rows.Single(row => row.ServiceName == "Spooler"));
    }

    [Fact]
    public async Task A_service_removed_from_outside_leaves_the_list()
    {
        var machine = new LiveMachine(Entry("Spooler"), Entry("Doomed"));
        var model = await Loaded(machine);

        machine.Remove("Doomed");
        await model.RefreshAsync();

        Assert.Equal(["Spooler"], model.Rows.Select(row => row.ServiceName));
    }

    [Fact]
    public async Task While_somebody_is_using_the_list_nothing_joins_or_leaves_it()
    {
        // A10 rule two, and the reason it is a rule: a row appearing above the one somebody is
        // aiming at moves their target while they are reaching for it.
        var machine = new LiveMachine(Entry("Spooler"), Entry("BITS"));
        var model = await Loaded(machine);

        model.QueryText = "status:running";
        model.Interacting = true;

        machine.Stop("Spooler");
        await model.RefreshAsync();

        // Still on screen, and visibly stopped. The cell moved, the list did not.
        Assert.Equal(2, model.Rows.Count);
        Assert.Equal("Stopped", model.Rows.Single(row => row.ServiceName == "Spooler").Status);

        // And it says so, because a list quietly disagreeing with its own query is the silence
        // rule 8 forbids, arriving from the one direction where it looks like politeness.
        Assert.Contains(Bws.Gui.Texts.Of("gui.status.holding"), model.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_list_settles_as_soon_as_the_interaction_ends()
    {
        var machine = new LiveMachine(Entry("Spooler"), Entry("BITS"));
        var model = await Loaded(machine);

        model.QueryText = "status:running";
        model.Interacting = true;

        machine.Stop("Spooler");
        await model.RefreshAsync();

        model.Interacting = false;

        Assert.Equal(["BITS"], model.Rows.Select(row => row.ServiceName));
        Assert.DoesNotContain(Bws.Gui.Texts.Of("gui.status.holding"), model.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refresh_that_fails_is_reported_and_the_window_carries_on()
    {
        // This runs unattended once a second, so an exception nobody caught would take the
        // window down while its owner was somewhere else entirely.
        var machine = new LiveMachine(Entry("Spooler"), Entry("BITS"));
        var model = await Loaded(machine);

        machine.FailNext = new InvalidOperationException("the manager went away");
        await model.RefreshAsync();

        Assert.True(model.Incomplete);
        Assert.Contains("the manager went away", model.Status, StringComparison.Ordinal);

        // And the next tick recovers rather than staying broken.
        machine.Stop("Spooler");
        await model.RefreshAsync();

        Assert.False(model.Incomplete);
        Assert.Equal("Stopped", model.Rows.Single(row => row.ServiceName == "Spooler").Status);
    }

    [Fact]
    public async Task A_refresh_that_finds_nothing_moving_marks_nothing()
    {
        var machine = new LiveMachine(Entry("Spooler"), Entry("BITS"));
        var model = await Loaded(machine);

        await model.RefreshAsync();

        Assert.All(model.Rows, row => Assert.False(row.RecentlyChanged));
        Assert.Equal(string.Empty, model.Notice);
    }

    [Fact]
    public async Task Pressing_F5_reads_everything_again_including_what_the_cheap_reading_cannot_see()
    {
        // Configuration does not move by itself, so the cheap reading does not watch it. A
        // person who changed a start type in services.msc and wants to see it has F5, and
        // A10 says so in as many words: admins trust it more than they trust the automatic.
        var machine = new LiveMachine(Entry("Spooler"));
        var model = await Loaded(machine);

        machine.Rename("Spooler", "Print Spooler, renamed");
        await model.RefreshAsync();

        Assert.Equal("Spooler display name", model.Rows[0].DisplayName);

        await model.LoadAsync();

        Assert.Equal("Print Spooler, renamed", model.Rows[0].DisplayName);
    }

    [Fact]
    public void Not_one_string_a_person_reads_is_written_into_the_code()
    {
        // Rule 13, checked rather than trusted. Every one of these comes from the language
        // file, so a key that is missing shows up as the key itself - which is ugly on screen
        // and impossible to miss, unlike a sentence quietly hard-coded in English.
        foreach (var key in new[]
                 {
                     "gui.window.title", "gui.column.name", "gui.column.displayName",
                     "gui.column.status", "gui.column.startType", "gui.column.account",
                     "gui.column.processId", "gui.status.reading", "gui.status.read",
                     "gui.status.matched", "gui.status.failed", "gui.status.partial",
                     "gui.status.tooCostly", "gui.status.holding", "gui.cell.unknown",
                     "gui.cell.noAccess", "gui.search.hint", "gui.search.expressions",
                     "gui.search.expressionsHint", "gui.search.showDrivers",
                     "gui.search.showDriversHint", "gui.query.unreadSignatures",
                     "gui.query.unreadMemory", "gui.query.unknownField", "gui.query.unknownValue",
                     "gui.query.unknownValueNearest", "gui.query.badPattern",
                     "gui.query.unclosedQuote", "gui.query.badNumber", "gui.query.badSize"
                 })
        {
            Assert.NotEqual(key, Bws.Gui.Texts.Of(key));
        }
    }

    private static async Task<MainViewModel> Loaded(params ScmEntry[] entries) =>
        await Loaded(new LiveMachine(entries));

    private static async Task<MainViewModel> Loaded(LiveMachine machine)
    {
        var model = new MainViewModel(machine, new SteppedClock());

        await model.LoadAsync();

        return model;
    }

    private static ScmEntry Stopped(string name) => Entry(name) with
    {
        Status = EntryStatus.Stopped,
        ProcessId = Reading<int>.Absent()
    };

    private static ScmEntry Driver(string name) => Entry(name) with
    {
        EntryType = EntryType.KernelDriver
    };

    private static ScmEntry Entry(string name) => new()
    {
        ServiceName = name,
        DisplayName = name + " display name",
        EntryType = EntryType.OwnProcess,
        Status = EntryStatus.Running,
        ProcessId = Reading<int>.Present(1234),
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Absent(),
        Account = Reading<string>.Present("LocalSystem"),
        DependsOn = Reading<IReadOnlyList<string>>.Absent(),
        Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Absent(),
        BinaryPath = Reading<string>.Absent(),
        BinaryFile = Reading<string>.Absent(),
        BinaryOnDisk = Reading<bool>.Absent(),
        Signature = Reading<BinarySignature>.NotRead(),
        FileVersion = Reading<string>.NotRead(),
        BinaryHash = Reading<string>.NotRead(),
        RequiredPrivileges = Reading<IReadOnlyList<string>>.Absent(),
        SidType = Reading<ServiceSidType>.Absent(),
        SecurityDescriptor = Reading<string>.Absent(),
        ErrorControl = Reading<ErrorControl>.Absent(),
        LoadOrderGroup = Reading<string>.Absent(),
        Memory = Reading<ProcessMemory>.NotRead()
    };
}
