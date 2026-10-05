namespace Bws.Core.Tests;

/// <summary>
/// What the listing does after one turn of asking the manager for its entries - stability report R-5,
/// 2026-10-05, and the owner's decision that day to ask again here and nowhere else.
///
/// The loop itself calls the manager and cannot be handed a turn by a test, so the decision at its
/// foot lives in <see cref="WindowsScmCatalog.After"/>, which calls nothing. Whether the race these
/// cases stand for - an entry growing between the size and the reading - really produces a turn with
/// nothing in it is NOT CHECKED. What is pinned is what the listing does if it does.
/// </summary>
public sealed class ListingTurnTests
{
    [Fact]
    public void A_first_turn_that_hands_over_nothing_is_asked_again_rather_than_ending_the_listing_empty()
    {
        // The handle is zero before and after the first turn. Tested for "nothing left" first, this
        // turn would end the listing with no entries under a code of success - rule 8, at its widest.
        var stalled = 0;

        Assert.Equal(WindowsScmCatalog.Turn.AskAgain, WindowsScmCatalog.After(0, 0, 0, ref stalled));
        Assert.Equal(1, stalled);
    }

    [Fact]
    public void Three_turns_without_progress_are_asked_again_and_the_fourth_gives_up()
    {
        // The ceiling stays - a manager that never makes progress still ends in an exception rather
        // than in a loop that never returns, which is what backlog 304 put there.
        var stalled = 0;

        Assert.Equal(WindowsScmCatalog.Turn.AskAgain, WindowsScmCatalog.After(0, 7, 7, ref stalled));
        Assert.Equal(WindowsScmCatalog.Turn.AskAgain, WindowsScmCatalog.After(0, 7, 7, ref stalled));
        Assert.Equal(WindowsScmCatalog.Turn.AskAgain, WindowsScmCatalog.After(0, 7, 7, ref stalled));
        Assert.Equal(WindowsScmCatalog.Turn.GiveUp, WindowsScmCatalog.After(0, 7, 7, ref stalled));
    }

    [Fact]
    public void A_turn_that_makes_progress_forgets_the_stalls_before_it()
    {
        var stalled = 2;

        Assert.Equal(WindowsScmCatalog.Turn.Onwards, WindowsScmCatalog.After(5, 0, 7, ref stalled));
        Assert.Equal(0, stalled);
    }

    [Fact]
    public void A_handle_back_at_zero_after_a_turn_with_entries_is_the_end_of_the_listing()
    {
        var stalled = 0;

        Assert.Equal(WindowsScmCatalog.Turn.Done, WindowsScmCatalog.After(5, 0, 0, ref stalled));
    }
}
