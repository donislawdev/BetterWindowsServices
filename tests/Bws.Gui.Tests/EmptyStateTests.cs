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
}
