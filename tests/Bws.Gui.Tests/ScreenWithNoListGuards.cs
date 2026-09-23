// Explicit, because UseWPF swaps the implicit using set and takes what this needs out of it.
using System.Windows;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What the window does while the machine overview has the middle of it and the list does not.
///
/// <b>A file of its own since 2026-09-01, and the seam is a subject rather than a line count.</b>
/// <see cref="OverviewGuards"/> holds what `G` promises about the numbers - which questions they
/// ask, that every one parses, that clicking one lands where it was counted. This holds what
/// happens to the REST of the window when that screen is up, which is a different question and
/// arrived with backlog 263.
///
/// The move was forced by <c>SizeRatchetGuards</c>, which refused a third test file over five
/// hundred lines - and it landed on the right seam because the two subjects were already there.
/// A ratchet that only ever gets loosened is a number somebody edits instead of a rule.
/// </summary>
public sealed class ScreenWithNoListGuards
{
    /// <summary>
    /// The list's own controls come off the screen while the list is not on it, and come back.
    ///
    /// <b>Backlog 263, owner's decision 2026-09-01.</b> Four rows sat over a screen that opens with
    /// "This machine, before you ask it anything" - a scope switch, a search box, four rows of
    /// filter chips and an action bar whose verbs are greyed because there is nothing to pick. Every
    /// one of them is a way of asking.
    ///
    /// <b>Both directions, because chrome that never comes back is the worse fault of the two.</b>
    /// A window that lost its search box for good would be unusable, and a test asserting only the
    /// hiding would pass on it.
    /// </summary>
    [Fact]
    public void The_lists_own_controls_are_not_on_the_screen_that_has_no_list()
    {
        _ = WpfHost.Resources;

        var model = new MainViewModel { Says = new Says { Elevated = true } };
        var window = WpfHost.On(() => new MainWindow(WpfHost.Nowhere(), model));

        WpfHost.Settled();

        try
        {
            WpfHost.On(() => model.ShowingOverview = true);
            WpfHost.Settled();

            Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Scope.Visibility));
            Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Search.Visibility));
            Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Filters.Visibility));
            Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Actions.Visibility));

            // AND THE ONE THAT STAYS, which is the line between them. The status row carries the
            // sentence about running without administrator rights - backlog 16 put it first because
            // it is a fact about the whole list, and before anybody has asked for anything is
            // exactly when it matters most.
            Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Status.Visibility));

            WpfHost.On(() => model.ShowingOverview = false);
            WpfHost.Settled();

            Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Scope.Visibility));
            Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Search.Visibility));
            Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Filters.Visibility));
            Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Actions.Visibility));
        }
        finally
        {
            WpfHost.On(window.Close);
        }
    }

    /// <summary>
    /// The screen says what the reading is doing, now that the line which used to say it is gone.
    ///
    /// <b>Backlog 263, and this is the debt the row above creates rather than a separate wish.</b>
    /// The count line beside the search box carried "Reading the service control manager..." and
    /// the failure message, and the empty state that would otherwise carry it is deliberately
    /// collapsed while this screen has the middle of the window. Without a line of its own, a first
    /// run that could not read the manager would say nothing at all - rule 8 in the first place
    /// anybody looks.
    /// </summary>
    [Fact]
    public async Task The_screen_with_no_list_still_says_that_the_reading_is_out()
    {
        var says = new Says { Elevated = true };

        // Before anything has been read, which is the state the window is in when it opens on this
        // screen: LoadAsync has not run and the constructor has already asked for the numbers.
        says.AboutTheList(
            firstLook: true, failed: false, shown: 0, everything: 0, inScope: 0,
            scope: EntryScope.Services, askedElsewhere: false);

        Assert.Equal(Bws.Gui.Texts.Of("gui.empty.loading"), says.ReadingMessage);
        Assert.False(says.ReadingFailed);

        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock())
        {
            Says = says
        };

        await model.LoadAsync();

        // AND THE EVEN CLAIM: once the reading is in, the screen stops apologising. A line about
        // reading that outlived the read would teach people to ignore it.
        Assert.Equal(string.Empty, model.Says.ReadingMessage);
    }

    /// <summary>
    /// A query that matched nothing is NOT said on this screen, and that is the point of the member.
    ///
    /// <b>The subject is what decides, not the screen.</b> A reading that failed is worth saying
    /// anywhere. "Nothing matched" is worth saying only where the query and its result are both
    /// visible - on the machine overview it would be a sentence about a list nobody can see, which
    /// is the fault this screen was repaired for, arriving from the other direction.
    /// </summary>
    [Fact]
    public void A_query_that_matched_nothing_is_not_the_screens_business()
    {
        var says = new Says { Elevated = true };

        says.AboutTheList(
            firstLook: false, failed: false, shown: 0, everything: 810, inScope: 810,
            scope: EntryScope.Services, askedElsewhere: false);

        Assert.NotEqual(string.Empty, says.ListMessage);
        Assert.Equal(string.Empty, says.ReadingMessage);
    }

    /// <summary>
    /// Zero is a claim, and this screen made it before it had read anything.
    ///
    /// <b>Backlog 263.</b> The window opens on this screen in its constructor and the first reading
    /// is still out for 749-822 ms measured - over which every line counted an empty listing and
    /// said nothing was running. It is the same distinction this project keeps apart everywhere
    /// else: a machine with no services legitimately shows zero, a machine nobody has read yet
    /// must not.
    /// </summary>
    [Fact]
    public void Nothing_read_yet_is_not_the_same_as_nothing_there()
    {
        var notYet = ViewModels.Overview.Of([], counted: false);
        var empty = ViewModels.Overview.Of([], counted: true);

        Assert.All(notYet, line => Assert.Equal(Bws.Gui.Texts.Of("gui.overview.notCounted"), line.CountText));
        Assert.All(empty, line => Assert.Equal("0", line.CountText));

        // The number itself is carried either way, because the line's other job is to be clickable
        // and that never depended on having something to show.
        Assert.All(notYet, line => Assert.Equal(0, line.Count));
    }
}
