using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// Which list the window is showing, checked without opening a window.
///
/// <b>Its own file since 2026-08-19, when the size ratchet asked and the seam was a subject</b>
/// - MainViewModelTests is about what the list shows, and this is about WHICH list. The scope
/// switch arrived that day by the owner's decision and brought six tests with it, which was
/// enough to push that file six lines past a ceiling it had been sitting under.
///
/// <b>WHAT THESE ARE REALLY FOR, said once here rather than at each of them.</b> Scope replaced
/// an arrangement where hiding drivers was a MEMBER OF THE QUERY - so the switch could not
/// disagree with the box, because it was the box. It has state of its own now, and everything
/// that can now go wrong follows from that one change: a query can contradict the position, a
/// selection can outlive the list it was made in, and a count can answer about the wrong
/// listing. Each of those has a test here.
/// </summary>
public sealed class ScopeTests
{
    [Fact]
    public async Task The_scope_switch_puts_nothing_in_the_box()
    {
        // FOUR TESTS PINNED THE OPPOSITE OF THIS UNTIL 2026-08-19, AND BOTH DECISIONS ARE THE
        // OWNER'S. They were called The_drivers_switch_writes_its_member_into_the_box,
        // The_switch_keeps_the_rest_of_the_query_when_it_writes_and_when_it_takes_back,
        // Writing_the_exclusion_by_hand_moves_the_switch and
        // The_switch_undoes_the_exclusion_wherever_somebody_put_it - and every one of them was
        // right about the arrangement it was written for. The drivers switch WAS a query member:
        // it appended !type:driver to the box, it lit up when somebody typed that by hand, and it
        // could not disagree with the text because it was the text.
        //
        // WHAT THAT ARRANGEMENT COULD NOT DO is be two lists. Scope decides which list is on
        // screen, the question is asked inside it, and type: in the box narrows rather than
        // chooses - so the switch stopped being a filter and a filter is what those four tested.
        // Named here rather than deleted quietly, because a reversed decision is worth as much as
        // the decision was.
        var model = await Loaded(Entry("Spooler"), Driver("disk"));

        model.QueryText = "status:running";
        model.Scope = EntryScope.Drivers;

        // The box is left exactly as somebody typed it. Nothing was appended, nothing taken away.
        Assert.Equal("status:running", model.QueryText);
        Assert.Equal(["disk"], model.Rows.Select(row => row.ServiceName));
    }

    [Fact]
    public async Task Each_scope_shows_its_own_half_and_all_of_it_shows_both()
    {
        var model = await Loaded(Entry("Spooler"), Driver("disk"));

        model.Scope = EntryScope.Services;

        Assert.Equal(["Spooler"], model.Rows.Select(row => row.ServiceName));

        model.Scope = EntryScope.Drivers;

        Assert.Equal(["disk"], model.Rows.Select(row => row.ServiceName));

        model.Scope = EntryScope.Everything;

        Assert.Equal(2, model.Rows.Count);
    }

    [Fact]
    public async Task A_type_member_narrows_inside_the_scope_rather_than_choosing_it()
    {
        // THE HALF OF THE CHANGE THAT IS EASY TO GET WRONG. Both kinds of driver are in the list,
        // the scope holds both because `docs/07` says type:driver means both, and asking for one
        // of them inside that scope has to leave the other out rather than being ignored.
        var model = await Loaded(Entry("Spooler"), Driver("disk"), FileSystemDriver("NTFS"));

        model.Scope = EntryScope.Drivers;

        Assert.Equal(2, model.Rows.Count);

        model.QueryText = "type:kernelDriver";

        Assert.Equal(["disk"], model.Rows.Select(row => row.ServiceName));
    }

    [Fact]
    public async Task A_query_against_the_scope_selects_nothing_and_the_box_still_says_what_was_asked()
    {
        // THE COST OF GIVING THE SWITCH A STATE, PINNED RATHER THAN LEFT TO BE DISCOVERED. While
        // the switch was text this state could not exist - the box was the only thing saying which
        // entries were wanted, so it could not contradict itself. It can now, and this is what it
        // looks like: Services on screen, type:driver in the box, and nothing selected by either.
        //
        // The window does not rewrite the query to make it agree, and that refusal is the point.
        // Editing what somebody typed to fit a control they did not touch is the window lying
        // about what was asked. What is owed is a SENTENCE saying why the list is empty, and that
        // is backlog 214 rather than something this test pretends is already done.
        var model = await Loaded(Entry("Spooler"), Driver("disk"));

        model.Scope = EntryScope.Services;
        model.QueryText = "type:driver";

        Assert.Empty(model.Rows);
        Assert.Equal("type:driver", model.QueryText);
        Assert.Equal(EntryScope.Services, model.Scope);
    }

    [Fact]
    public async Task The_count_is_against_the_scope_rather_than_against_the_machine()
    {
        // "12 of 812" while looking at a list of 472 would answer a question nobody asked. The
        // second number is the list somebody is looking at.
        var model = await Loaded(Entry("Spooler"), Stopped("BITS"), Driver("disk"));

        model.Scope = EntryScope.Services;

        Assert.Equal(Bws.Gui.Texts.Of("gui.status.read.many", 2), model.Says.Status);

        model.QueryText = "status:running";

        Assert.Equal(Bws.Gui.Texts.Of("gui.status.matched.many", 1, 2), model.Says.Status);
    }

    [Fact]
    public async Task Moving_the_scope_lets_go_of_what_was_chosen()
    {
        // A SAFETY PROPERTY RATHER THAN A TIDINESS ONE. Everything that changes a machine acts on
        // what is selected, `A7` asks for drivers to be clearly separated and warned about, and a
        // selection that survived this move would stand five services under a button on a screen
        // that says Drivers.
        var model = await Loaded(Entry("Spooler"), Driver("disk"));

        model.Chosen.Row = model.Rows.Single(row => row.ServiceName == "Spooler");

        Assert.NotNull(model.Chosen.Row);

        model.Scope = EntryScope.Drivers;

        Assert.Null(model.Chosen.Row);
    }

    /// <summary>
    /// The hint behind the empty search box names what the box searches - the list somebody is
    /// standing on. Point 8(b) of `docs/11` 2.14: it said "services and drivers" on a list from
    /// which, by its own tooltip, a search never reaches a driver.
    ///
    /// <b>The notification as well as the value</b>, because the hint is bound and a binding not
    /// told that the scope moved keeps the sentence of the list before - `docs/08` position 19.
    /// </summary>
    [Theory]
    [InlineData(EntryScope.Services, "gui.search.hint.services")]
    [InlineData(EntryScope.Drivers, "gui.search.hint.drivers")]
    [InlineData(EntryScope.Everything, "gui.search.hint.all")]
    public async Task The_hint_behind_the_box_names_the_list_the_box_searches(EntryScope scope, string key)
    {
        var model = await Loaded(Entry("Spooler"), Driver("disk"));
        var announced = new List<string>();

        model.Scope = scope == EntryScope.Everything ? EntryScope.Services : EntryScope.Everything;
        model.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);

        model.Scope = scope;

        Assert.Equal(Bws.Gui.Texts.Of(key), model.SearchHint);
        Assert.Contains(nameof(MainViewModel.SearchHint), announced);
    }

    /// <summary>
    /// Every position of the switch says how many entries its list holds - point 8(c) of
    /// `docs/11` 2.14 - and the number is the size of the LIST, untouched by the query and by
    /// which position is on. The line under the box already says what the query found.
    /// </summary>
    [Fact]
    public async Task Every_position_counts_the_list_it_stands_for_whatever_is_typed_or_chosen()
    {
        var model = await Loaded(Entry("Spooler"), Stopped("BITS"), Driver("disk"));

        model.QueryText = "status:running";
        model.Scope = EntryScope.Drivers;

        var counts = model.ScopePositions.ToDictionary(position => position.Scope, position => position.Count);

        Assert.Equal(2, counts[EntryScope.Services]);
        Assert.Equal(1, counts[EntryScope.Drivers]);
        Assert.Equal(3, counts[EntryScope.Everything]);

        model.Scope = EntryScope.Services;

        Assert.Equal(counts, model.ScopePositions.ToDictionary(position => position.Scope, position => position.Count));
    }

    /// <summary>
    /// And a reading tells the positions their numbers moved - a switch counting the machine as
    /// it was at start would be wrong after the first refresh, and `docs/08` position 19 says
    /// nothing goes red when a binding is not told.
    /// </summary>
    [Fact]
    public async Task A_reading_tells_every_position_that_its_count_may_have_moved()
    {
        var model = await Loaded(Entry("Spooler"), Driver("disk"));
        var told = new List<string>();

        foreach (var position in model.ScopePositions)
        {
            position.PropertyChanged += (_, e) => told.Add($"{position.Scope}:{e.PropertyName}");
        }

        await model.LoadAsync();

        foreach (var scope in new[] { EntryScope.Services, EntryScope.Drivers, EntryScope.Everything })
        {
            Assert.Contains($"{scope}:{nameof(ScopeChoice.Count)}", told);
        }
    }

    private static async Task<MainViewModel> Loaded(params ScmEntry[] entries)
    {
        var model = new MainViewModel(new LiveMachine(entries), new SteppedClock());

        await model.LoadAsync();

        // Opened out for the same reason MainViewModelTests does it: these tests set the scope
        // they are about, and starting from the one the window opens on would have each of them
        // assert two things at once.
        model.ClearQuery();
        model.Scope = EntryScope.Everything;

        return model;
    }

    private static ScmEntry Stopped(string name) => Rows.Stopped(name);

    private static ScmEntry Driver(string name) => Rows.Driver(name);

    private static ScmEntry FileSystemDriver(string name) => Rows.FileSystemDriver(name);

    private static ScmEntry Entry(string name) => Rows.Entry(name);
}
