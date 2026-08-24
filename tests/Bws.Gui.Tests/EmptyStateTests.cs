using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What the middle of the window says when it has no rows.
///
/// <b>Complaint 9 of the eleven in `docs/11`, and the reason it is a complaint rather than a
/// nicety:</b> an empty rectangle does not say whether nothing matched or something broke. The
/// count line under the list did carry the words, and a line of small grey text below the grid
/// is read after the grid, if at all.
///
/// <b>Five states rather than the four that document asks for.</b> A machine handing over no
/// entries is not a query that matched nothing, and telling somebody to clear a query they never
/// typed would be the window being confidently wrong. It is rare, it is legal, and it costs one
/// sentence to get right.
/// </summary>
public sealed class EmptyStateTests
{
    [Fact]
    public async Task A_list_with_rows_in_it_says_nothing_at_all()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        await model.LoadAsync();

        Assert.Equal(ListFace.Rows, model.Says.Face);
        Assert.Equal(string.Empty, model.Says.ListMessage);
        Assert.Equal(string.Empty, model.Says.ListWayOut);
    }

    /// <summary>
    /// The state a window is in for the first three quarters of a second, measured at 749-822 ms
    /// from launch to a row on screen. Owner's choice, 2026-08-05: a sentence, not an animation.
    /// </summary>
    [Fact]
    public void Before_anything_has_been_read_it_says_it_is_reading()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        Assert.Equal(ListFace.Loading, model.Says.Face);
        Assert.NotEqual(string.Empty, model.Says.ListMessage);

        // Nothing to offer. There is no way out of waiting, and a way out that does nothing is
        // worse than none.
        Assert.Equal(string.Empty, model.Says.ListWayOut);
    }

    /// <summary>
    /// A LIST THAT IS EMPTY ON THIS MACHINE IS NOT A QUERY THAT MATCHED NOTHING.
    ///
    /// <b>A state the scope switch created on 2026-08-19, and the old answer is a lie in it
    /// twice.</b> On a machine with no drivers, standing on the Drivers list with an EMPTY box, the
    /// window used to say "nothing matches what you asked for" - blaming somebody for a query they
    /// never wrote - and then offer to press Escape to clear a box that is already clear.
    ///
    /// The way out is a different LIST, because no question typed into this one will help.
    /// </summary>
    [Fact]
    public async Task A_list_with_nothing_in_it_says_that_rather_than_blaming_the_query()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        await model.LoadAsync();

        model.Scope = EntryScope.Drivers;

        Assert.Empty(model.Rows);
        Assert.Equal(string.Empty, model.QueryText);
        Assert.Equal(ListFace.ScopeEmpty, model.Says.Face);

        // Not the sentence about a query, and not the one about the manager handing over nothing -
        // the manager handed over a service, it is just not on this list.
        Assert.Equal(Bws.Gui.Texts.Of("gui.empty.noDrivers"), model.Says.ListMessage);
        Assert.Equal(Bws.Gui.Texts.Of("gui.empty.otherListWayOut"), model.Says.ListWayOut);
    }

    /// <summary>
    /// BOTH HALVES WORKING AND EXCLUDING EACH OTHER, which is backlog 214 answered.
    ///
    /// <b>The one state that could not exist while the drivers switch WAS the query.</b> Scope has a
    /// state of its own since 2026-08-19, so Services on screen with <c>type:driver</c> in the box
    /// is reachable, and both controls are doing exactly what they were told.
    ///
    /// <b>The window names both sides and moves neither.</b> Quietly switching the scope, or
    /// rewriting the box, would be it deciding what somebody meant - and "nothing matched" would
    /// send them to edit a query that is not the problem.
    /// </summary>
    [Fact]
    public async Task A_query_asking_for_the_other_list_says_which_list_it_is_asking_for()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler"), Rows.Driver("disk")), new SteppedClock());

        await model.LoadAsync();

        model.QueryText = "type:driver";

        Assert.Empty(model.Rows);
        Assert.Equal(ListFace.AskedElsewhere, model.Says.Face);
        Assert.Equal(Bws.Gui.Texts.Of("gui.empty.askedForDrivers"), model.Says.ListMessage);

        // And the mirror, so the sentence is not one direction with the other left to chance.
        model.Scope = EntryScope.Drivers;
        model.QueryText = "!type:driver";

        Assert.Empty(model.Rows);
        Assert.Equal(ListFace.AskedElsewhere, model.Says.Face);
        Assert.Equal(Bws.Gui.Texts.Of("gui.empty.askedForServices"), model.Says.ListMessage);
    }

    /// <summary>
    /// THE WAY OUT STOPPED PROMISING SOMETHING IT CANNOT DO, and this is a regression I shipped.
    ///
    /// It read "Press Esc to empty the box and see everything again" and that was true for six days:
    /// hiding drivers lived in the box, so emptying the box gave the machine back. The scope switch
    /// moved it out on 2026-08-19 and left the sentence behind - Escape empties the question and
    /// leaves the list exactly where it was.
    ///
    /// <b>Nothing caught it.</b> TextKeyGuards asks whether a declared string reaches a screen, not
    /// whether it is true, and there is no guard in this project that could have. Backlog 215 is
    /// the same shape from the other end.
    /// </summary>
    [Fact]
    public async Task The_way_out_of_an_empty_list_does_not_promise_the_whole_machine_back()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler"), Rows.Driver("disk")), new SteppedClock());

        await model.LoadAsync();

        model.QueryText = "name:NoSuchServiceAnywhere";

        // On a named list the way out offers both real doors: empty the box, or widen the list.
        Assert.Equal(ListFace.NothingMatched, model.Says.Face);
        Assert.Equal(Bws.Gui.Texts.Of("gui.empty.nothingMatchedHere"), model.Says.ListMessage);
        Assert.Equal(Bws.Gui.Texts.Of("gui.empty.nothingMatchedHereWayOut"), model.Says.ListWayOut);

        model.Scope = EntryScope.Everything;

        // And on the whole machine there is no wider list to offer, so it offers only the box.
        Assert.Equal(ListFace.NothingMatched, model.Says.Face);
        Assert.Equal(Bws.Gui.Texts.Of("gui.empty.nothingMatched"), model.Says.ListMessage);
        Assert.Equal(Bws.Gui.Texts.Of("gui.empty.nothingMatchedWayOut"), model.Says.ListWayOut);

        Assert.DoesNotContain("everything again", model.Says.ListWayOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_query_that_matches_nothing_says_so_and_offers_the_way_back()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        await model.LoadAsync();

        model.QueryText = "name:NoSuchServiceAnywhere";

        Assert.Empty(model.Rows);
        Assert.Equal(ListFace.NothingMatched, model.Says.Face);
        Assert.NotEqual(string.Empty, model.Says.ListMessage);
        Assert.NotEqual(string.Empty, model.Says.ListWayOut);
    }

    /// <summary>
    /// AND IT SURVIVES THE READING ENDING, which is the fault this had for ten minutes while it
    /// was being written.
    ///
    /// The face was worked out at the end of a reading while the busy flag was still raised, so a
    /// query matching nothing after F5 came out as "reading the manager" and stayed there -
    /// nothing ran again once the reading finished. Every path that lowers the flag now says so.
    /// </summary>
    [Fact]
    public async Task A_query_that_matches_nothing_still_says_so_after_a_refresh()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        await model.LoadAsync();

        model.QueryText = "name:NoSuchServiceAnywhere";

        await model.LoadAsync();

        Assert.Equal(ListFace.NothingMatched, model.Says.Face);
    }

    /// <summary>
    /// A machine with nothing on it is not a query with nothing to show, and the difference is
    /// the whole reason this has five states. Offering to empty an empty box would be advice
    /// that cannot help.
    /// </summary>
    [Fact]
    public async Task A_machine_that_hands_over_nothing_is_not_a_query_that_matched_nothing()
    {
        var model = new MainViewModel(new LiveMachine(), new SteppedClock());

        await model.LoadAsync();

        Assert.Equal(ListFace.NothingToShow, model.Says.Face);
        Assert.NotEqual(string.Empty, model.Says.ListMessage);
        Assert.Equal(string.Empty, model.Says.ListWayOut);
    }

    /// <summary>
    /// A reading that failed is a fact about the machine, and it is asked about first. A window
    /// answering "nothing matched" after the manager refused to open would be blaming the person
    /// for the machine.
    /// </summary>
    [Fact]
    public async Task A_reading_that_failed_says_so_rather_than_blaming_the_query()
    {
        var machine = new LiveMachine(Rows.Entry("Spooler"))
        {
            FailNext = new InvalidOperationException("the manager would not open")
        };

        var model = new MainViewModel(machine, new SteppedClock());

        await model.LoadAsync();

        Assert.Equal(ListFace.Failed, model.Says.Face);
        Assert.NotEqual(string.Empty, model.Says.ListWayOut);
    }

    /// <summary>
    /// Complaint 7: you cannot see THAT it is filtering. The count line wears this as a weight,
    /// which is seen without being read.
    /// </summary>
    [Fact]
    public async Task The_window_knows_when_a_query_is_holding_entries_back()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler"), Rows.Entry("BITS")), new SteppedClock());

        await model.LoadAsync();

        Assert.False(model.Says.Narrowed);

        model.QueryText = "name:spooler";

        Assert.True(model.Says.Narrowed);

        model.ClearQuery();

        Assert.False(model.Says.Narrowed);
    }

    /// <summary>
    /// A REFRESH DOES NOT TAKE THE EMPTY STATE OFF THE SCREEN - owner's report, 2026-08-13.
    ///
    /// <b>What it looked like: the sentence saying nothing matched flickered once a second.</b>
    /// `A10` reads the machine every second, each reading raised and lowered the reading flag, and
    /// a list with no rows on screen answered "reading the manager" for as long as the flag was up.
    /// So the answer was replaced by a progress message and put back, over and over, while somebody
    /// was reading it.
    ///
    /// <b>The claim is the NOTIFICATION rather than the value, and that is the difference between
    /// this test and one that would have passed all along.</b> The message ends where it started
    /// either way - what a person sees is the two changes in between, and only a listener can see
    /// those. This project has been caught four times by a value that is right everywhere except
    /// where somebody is looking.
    /// </summary>
    [Fact]
    public async Task A_refresh_does_not_take_the_empty_state_off_the_screen()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        await model.LoadAsync();

        model.QueryText = "name:nothingiscallledthis";

        var before = model.Says.ListMessage;

        Assert.NotEqual(string.Empty, before);

        var announced = new List<string>();

        model.Says.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(Says.ListMessage))
            {
                announced.Add(model.Says.ListMessage);
            }
        };

        await model.RefreshAsync();

        Assert.Equal(before, model.Says.ListMessage);

        Assert.Empty(announced);
    }

    /// <summary>
    /// AND ON THE MACHINE THAT HANDS OVER NOTHING, which is the face the 2026-08-13 fix left out.
    ///
    /// <b>Backlog 196. The fix above asked `everything == 0` to mean "nothing has arrived yet", and
    /// on this machine that is true forever</b> - so the sentence saying the manager handed over no
    /// entries was replaced by "reading the manager" and put back, once a second, exactly the
    /// complaint the fix was answering. Measured before the change: the listener saw two
    /// announcements per tick.
    ///
    /// <b>A separate test rather than a case inside the one above, because they fail for different
    /// reasons.</b> That one is about a query narrowing a list to nothing and this one is about a
    /// machine with nothing on it - the two faces this product refuses to collapse, and a shared
    /// test would let either of them go quiet while the other kept it green.
    ///
    /// The claim is the NOTIFICATION rather than the value, for the reason written above it.
    /// </summary>
    [Fact]
    public async Task A_refresh_does_not_take_the_empty_state_off_the_screen_on_an_empty_machine()
    {
        var model = new MainViewModel(new LiveMachine(), new SteppedClock());

        await model.LoadAsync();

        var before = model.Says.ListMessage;

        Assert.Equal(ListFace.NothingToShow, model.Says.Face);
        Assert.NotEqual(string.Empty, before);

        var announced = new List<string>();

        model.Says.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(Says.ListMessage))
            {
                announced.Add(model.Says.ListMessage);
            }
        };

        await model.RefreshAsync();

        Assert.Equal(before, model.Says.ListMessage);

        Assert.Empty(announced);
    }
}
