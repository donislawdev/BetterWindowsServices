using Bws.Core;
using Bws.Core.Querying;
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

        Assert.Contains("1", model.Says.Status, StringComparison.Ordinal);
        Assert.False(model.Says.Incomplete);
        Assert.Equal(string.Empty, model.Says.Notice);
        Assert.Equal(string.Empty, model.Says.Problem);
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
        Assert.True(model.Says.Incomplete);
        Assert.Contains("no manager here", model.Says.Status, StringComparison.Ordinal);
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

        var cells = model.Rows.ToDictionary(row => row.ServiceName, row => row["account"], StringComparer.Ordinal);

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
        Assert.Contains("2", model.Says.Status, StringComparison.Ordinal);
        Assert.Contains("3", model.Says.Status, StringComparison.Ordinal);
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
        Assert.Contains("running", model.Says.Problem, StringComparison.OrdinalIgnoreCase);
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

            Assert.Equal(string.Empty, model.Says.Problem);
        }

        // The last one selects everything, because a member with nothing after the colon is
        // dropped rather than answered.
        Assert.Equal(2, model.Rows.Count);
    }

    [Fact]
    public async Task With_the_regex_switch_on_a_bare_word_is_an_expression()
    {
        var model = await Loaded(Entry("Spooler"), Entry("SpoolerAgent"), Entry("Winmgmt"));

        model.QueryText = "^spooler$";

        // Off, that is a search for a fragment nobody has, because the characters are part of
        // the text being looked for.
        Assert.Empty(model.Rows);

        model.QueryText = "/^spooler$/";

        Assert.Equal(["Spooler"], model.Rows.Select(row => row.ServiceName));
    }

    [Fact]
    public async Task The_regex_switch_leaves_members_with_a_field_alone()
    {
        // Only the search half changes. Somebody who learned that name:spool* is a wildcard
        // does not have to find out that a switch elsewhere silently made it something else.
        var model = await Loaded(Entry("Spooler"), Entry("Winmgmt"));

        // The marks belong to the search half only, so this member is unaffected either way.
        model.QueryText = "name:spool*";

        Assert.Equal(["Spooler"], model.Rows.Select(row => row.ServiceName));
    }

    [Fact]
    public async Task An_expression_that_will_not_compile_is_reported_and_the_list_stays()
    {
        var model = await Loaded(Entry("Spooler"), Entry("Winmgmt"));

        model.QueryText = "/spooler/";

        Assert.Single(model.Rows);

        model.QueryText = "/spooler(/";

        Assert.Single(model.Rows);
        Assert.NotEqual(string.Empty, model.Says.Problem);
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
        Assert.Equal(string.Empty, model.Says.Problem);
        Assert.Contains(Bws.Gui.Texts.Of("gui.query.unreadSignatures"), model.Says.Notice, StringComparison.Ordinal);
        Assert.DoesNotContain(
            Bws.Gui.Texts.Of("gui.status.partial", 2), model.Says.Notice, StringComparison.Ordinal);
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
        Assert.NotEqual(string.Empty, model.Says.Notice);
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

        Assert.Equal("Stopped", model.Rows.Single(row => row.ServiceName == "Spooler")["status"]);
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
        Assert.Equal("Stopped", before["status"]);
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

        Assert.Equal("NT AUTHORITY\\LocalService", arrived["account"]);
        Assert.Equal("Automatic", arrived["startType"]);

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
        Assert.Equal("Stopped", model.Rows.Single(row => row.ServiceName == "Spooler")["status"]);

        // And it says so, because a list quietly disagreeing with its own query is the silence
        // rule 8 forbids, arriving from the one direction where it looks like politeness.
        Assert.Contains(Bws.Gui.Texts.Of("gui.status.holding"), model.Says.Notice, StringComparison.Ordinal);
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
        Assert.DoesNotContain(Bws.Gui.Texts.Of("gui.status.holding"), model.Says.Notice, StringComparison.Ordinal);
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

        Assert.True(model.Says.Incomplete);
        Assert.Contains("the manager went away", model.Says.Status, StringComparison.Ordinal);

        // And the next tick recovers rather than staying broken.
        machine.Stop("Spooler");
        await model.RefreshAsync();

        Assert.False(model.Says.Incomplete);
        Assert.Equal("Stopped", model.Rows.Single(row => row.ServiceName == "Spooler")["status"]);
    }

    [Fact]
    public async Task A_refresh_that_finds_nothing_moving_marks_nothing()
    {
        var machine = new LiveMachine(Entry("Spooler"), Entry("BITS"));
        var model = await Loaded(machine);

        await model.RefreshAsync();

        Assert.All(model.Rows, row => Assert.False(row.RecentlyChanged));
        Assert.Equal(string.Empty, model.Says.Notice);
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

    // ---------------------------------------------------------------------------------
    // Reentrancy: what the window can do to itself while it is waiting for the machine
    // ---------------------------------------------------------------------------------
    //
    // Not thread safety. Every continuation here comes back to the interface thread, so two
    // fields are never written at once - which is exactly why this class of hole is invisible
    // by inspection. What can go wrong is ordering: a reading started earlier finishing later
    // and leaving an older answer on screen, or a call re-entering while its own state is
    // half rebuilt. The real threading in this project lives in the core's second pass and is
    // guarded there.

    [Fact]
    public async Task Two_readings_are_never_out_at_once()
    {
        // Found by reading the code rather than by a failure, and the fix is worth less than
        // the test: before it, pressing F5 twice sent two full readings, and the one that
        // FINISHED later won rather than the one that LOOKED later. So the list could settle
        // on the older of two answers and say nothing about it.
        var machine = new LiveMachine(Entry("Spooler"), Entry("BITS"));
        var model = await Loaded(machine);

        var reads = machine.FullReads;

        machine.HoldReadings();

        var first = model.LoadAsync();

        try
        {
            // Never a bare await on the second call, and that is the whole shape of this test.
            // Without the guard it starts its own reading, blocks on the same gate as the
            // first, and **the test hangs instead of failing** - which this project has already
            // paid for once, when picking the wrong regex engine hung the suite rather than
            // reddening it. A hang tells nobody anything.
            await Returned(model.LoadAsync(), "a second full reading");

            // A tick arriving in the same window is turned away by the same guard, because both
            // kinds of reading rebuild the same state.
            await Returned(model.RefreshAsync(), "a tick");
        }
        finally
        {
            machine.ReleaseReadings();
        }

        await first;

        Assert.Equal(reads + 1, machine.FullReads);
        Assert.Equal(0, machine.StatusReads);
    }

    /// <summary>
    /// Waits for a call that is supposed to come straight back, and fails rather than waits if
    /// it does not.
    /// </summary>
    private static async Task Returned(Task call, string what)
    {
        var settled = await Task.WhenAny(call, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);

        Assert.True(
            ReferenceEquals(settled, call),
            $"{what} did not come back while another was still out, so it started one of its own. " +
            "The guard against overlapping readings is missing.");

        await call.ConfigureAwait(false);
    }

    [Fact]
    public async Task The_list_always_matches_the_query_however_the_operations_are_interleaved()
    {
        // An invariant rather than a scenario, and that is the point: nobody can enumerate the
        // orders in which a person types, points, presses F5 and lets a tick land. What can be
        // stated is what must be true after every one of them.
        var machine = new LiveMachine(Entry("Spooler"), Stopped("BITS"), Driver("disk"), Entry("Winmgmt"));
        var model = await Loaded(machine);

        var steps = new List<(string Name, Func<Task> Do)>
        {
            ("type a query", () => { model.QueryText = "status:running"; return Task.CompletedTask; }),
            ("type a broken query", () => { model.QueryText = "status:runing"; return Task.CompletedTask; }),
            ("type nothing", () => { model.QueryText = string.Empty; return Task.CompletedTask; }),
            ("go to services", () => { model.Scope = EntryScope.Services; return Task.CompletedTask; }),
            ("go to drivers", () => { model.Scope = EntryScope.Drivers; return Task.CompletedTask; }),
            ("go to everything", () => { model.Scope = EntryScope.Everything; return Task.CompletedTask; }),
            ("type an expression", () => { model.QueryText = "/^s/"; return Task.CompletedTask; }),
            ("type plain text again", () => { model.QueryText = "spool"; return Task.CompletedTask; }),
            ("start using the list", () => { model.Interacting = true; return Task.CompletedTask; }),
            ("stop using the list", () => { model.Interacting = false; return Task.CompletedTask; }),
            ("a service stops", () => { machine.Stop("Spooler"); return Task.CompletedTask; }),
            ("a service starts", () => { machine.Start("BITS", 4321); return Task.CompletedTask; }),
            ("a tick lands", model.RefreshAsync),
            ("somebody presses F5", model.LoadAsync)
        };

        // Every ordered pair, which is where reentrancy hides - one operation landing inside
        // the state another left behind.
        foreach (var first in steps)
        {
            foreach (var second in steps)
            {
                await first.Do();
                await second.Do();

                Invariants(model, $"after '{first.Name}' then '{second.Name}'");
            }
        }
    }

    /// <summary>
    /// What has to be true of the window no matter what just happened to it.
    ///
    /// Judged against what the model has read, not against the machine as it is now. The two
    /// differ on purpose between one tick and the next, and holding the window to the second
    /// would be a test demanding clairvoyance.
    /// </summary>
    private static void Invariants(MainViewModel model, string after)
    {
        // No row twice. A reconciliation that inserted without removing would show one service
        // in two places, and a person would believe it.
        Assert.True(
            model.Rows.Distinct().Count() == model.Rows.Count,
            $"A row appears more than once {after}.");

        Assert.True(
            model.Rows.Select(row => row.ServiceName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == model.Rows.Count,
            $"Two rows carry the same service name {after}.");

        // The line under the list always says something. "Reading" counts, emptiness does not.
        Assert.False(string.IsNullOrWhiteSpace(model.Says.Status), $"The line under the list is empty {after}.");

        var parsed = QueryParser.Parse(model.QueryText);

        // A query that does not read leaves the list where it was, so there is nothing to
        // hold it to - and the window has to have said so.
        if (!parsed.IsValid)
        {
            Assert.False(string.IsNullOrWhiteSpace(model.Says.Problem), $"A broken query is not reported {after}.");

            return;
        }

        Assert.Equal(string.Empty, model.Says.Problem);

        // Suspended on purpose while somebody is leaning on the list, and the window says so
        // in words. That is the one place this invariant is allowed to lapse, and it may not
        // lapse quietly.
        if (model.Interacting)
        {
            return;
        }

        Assert.True(
            model.Rows.All(row => parsed.Query!.Match(row.Entry).Matched),
            $"A row on screen does not satisfy the query {after}.");
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
                     "gui.cell.noAccess", "gui.search.hint",
                     "gui.filter.group.state", "gui.filter.group.start", "gui.filter.group.about",
                     "gui.filter.hint.adds", "gui.filter.hint.narrows", "gui.query.unreadSignatures",
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

    /// <summary>
    /// A window that has read a machine, with the box emptied and the scope opened out.
    ///
    /// <b>Emptied since 2026-08-13, when the window started opening with kernel drivers hidden</b> -
    /// owner's decision. Every test reached through here is about the list, the reading or a query
    /// it sets itself, and none of them is about what the window opens with. That one claim is
    /// asserted on its own, so a change to the decision goes red in one place rather than in thirty
    /// about something else.
    ///
    /// <b>AND THE SCOPE IS OPENED OUT SINCE 2026-08-19, WHICH IS THE SAME SENTENCE ABOUT A DIFFERENT
    /// MECHANISM.</b> Hiding drivers used to be text, so emptying the box was enough to neutralise
    /// it. It is a scope now, and a helper that emptied only the box would leave every test through
    /// here quietly looking at services alone - a driver handed to <c>Loaded</c> would vanish, and
    /// the test would fail somewhere far from the reason.
    /// </summary>
    private static async Task<MainViewModel> Loaded(LiveMachine machine)
    {
        var model = new MainViewModel(machine, new SteppedClock());

        await model.LoadAsync();

        model.ClearQuery();
        model.Scope = EntryScope.Everything;

        return model;
    }

    // The three factories moved to Rows.cs on 2026-08-05, when the ratchet asked for a seam and
    // this was the honest one - CellFaceTests had arrived carrying a second copy of the same
    // twenty lines. Forwarders rather than call-site changes, because renaming thirty call sites
    // would be a large diff for no reading gain.
    private static ScmEntry Stopped(string name) => Rows.Stopped(name);

    private static ScmEntry Driver(string name) => Rows.Driver(name);

    private static ScmEntry FileSystemDriver(string name) => Rows.FileSystemDriver(name);

    private static ScmEntry Entry(string name) => Rows.Entry(name);
}
