using System.Windows.Input;
using Bws.Gui;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What a key press means, and what the two that change something actually do.
///
/// <b>The window had one shortcut until 2026-08-05 and it was F5.</b> `docs/11` 9.1 is blunt
/// about why that is a fault rather than a gap: WCAG 2.1.1 asks that everything reachable by
/// mouse be reachable by keyboard, and the tool this one is compared with - <c>services.msc</c> -
/// has never needed a mouse for any of it. Backlog 59.
///
/// These tests exist because the mapping was split away from the handler. A key handler in code
/// behind can only be checked by somebody pressing keys, which is not a guard - so what a press
/// MEANS lives in <see cref="Shortcuts"/> and the doing is one line each.
/// </summary>
public sealed class KeyboardTests
{
    // One fact each rather than a table, because the expected value is an internal type and a
    // public test method cannot take one as a parameter.
    [Fact]
    public void F5_refreshes_the_way_it_does_everywhere_in_windows() =>
        Assert.Equal(Shortcut.Refresh, Shortcuts.For(Key.F5, ModifierKeys.None));

    [Fact]
    public void Control_F_goes_to_the_search_the_way_it_does_everywhere_in_windows() =>
        Assert.Equal(Shortcut.FocusQuery, Shortcuts.For(Key.F, ModifierKeys.Control));

    [Fact]
    public void Escape_backs_out_of_what_was_typed_the_way_it_does_everywhere_in_windows() =>
        Assert.Equal(Shortcut.Back, Shortcuts.For(Key.Escape, ModifierKeys.None));

    /// <summary>
    /// Enter asks for everything about the chosen entry - `docs/11` 9.1, backlog 59.
    ///
    /// <b>What this does NOT say is where the press came from.</b> Enter belongs to the list rather
    /// than to the window, and that half is asked in <see cref="MainWindow"/> because where the
    /// keyboard is happens to be the one piece of state only a window can answer.
    /// </summary>
    [Fact]
    public void Enter_asks_for_the_details_of_the_chosen_entry() =>
        Assert.Equal(Shortcut.OpenDetails, Shortcuts.For(Key.Enter, ModifierKeys.None));

    /// <summary>
    /// A key with a modifier nobody asked for belongs to whatever claims it.
    ///
    /// Not a detail: a program that swallows every combination merely CONTAINING Control breaks
    /// shortcuts it has never heard of, and does it silently, on somebody else's machine.
    /// </summary>
    [Theory]
    [InlineData(Key.F, ModifierKeys.None)]
    [InlineData(Key.F, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.F, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(Key.F5, ModifierKeys.Control)]
    [InlineData(Key.Escape, ModifierKeys.Shift)]
    [InlineData(Key.A, ModifierKeys.None)]
    public void Anything_else_is_left_alone(Key key, ModifierKeys modifiers) =>
        Assert.Equal(Shortcut.None, Shortcuts.For(key, modifiers));

    [Fact]
    public async Task Escape_empties_the_query_and_the_list_comes_back()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler"), Rows.Driver("beep")), new SteppedClock());

        await model.LoadAsync();

        model.QueryText = "name:spooler";

        Assert.Single(model.Rows);
        Assert.True(model.ClearQuery());
        Assert.Equal(string.Empty, model.QueryText);
        Assert.Equal(2, model.Rows.Count);
    }

    /// <summary>
    /// An empty box says so instead of swallowing the press.
    ///
    /// Escape is the one key every window in Windows already has an opinion about, and the
    /// window takes it before anything else gets a chance. Reporting that nothing happened is
    /// what keeps that from becoming a key that stops working.
    /// </summary>
    [Fact]
    public async Task Escape_on_an_empty_query_does_nothing_and_admits_it()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        await model.LoadAsync();

        // The window opens with kernel drivers hidden, which is a query like any other - so the
        // first clear has something to do and the second is the one this test is about.
        Assert.True(model.ClearQuery());

        Assert.False(model.ClearQuery());
    }

    /// <summary>
    /// Clearing the box shows drivers again, because the exclusion has never lived anywhere else.
    ///
    /// The switch reads the query rather than holding a state of its own - `docs/07`, owner's
    /// decision - so this is that promise being kept rather than a side effect to be tidied up.
    /// </summary>
    [Fact]
    public async Task Clearing_the_query_brings_the_drivers_switch_back_with_it()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler"), Rows.Driver("beep")), new SteppedClock());

        await model.LoadAsync();

        model.ShowDrivers = false;

        Assert.False(model.ShowDrivers);
        Assert.Single(model.Rows);

        model.ClearQuery();

        Assert.True(model.ShowDrivers);
        Assert.Equal(2, model.Rows.Count);
    }

    /// <summary>
    /// Escape closes the panel first and empties the query only when there is no panel.
    ///
    /// <b>The order is the decision, and it is the whole reason Escape has a branch at all.</b> One
    /// press doing both at once takes somebody's query away while they were reaching for the panel,
    /// and a query is much the more expensive of the two to type again. Decided in `docs/04` at
    /// Paczka 1 rather than here.
    ///
    /// Driven through the window rather than through the two objects, because the ORDER is the
    /// thing being claimed and it lives in the window - asking the panel and the query separately
    /// would pass on a window that had them the wrong way round.
    /// </summary>
    [Fact]
    public async Task Escape_closes_the_panel_before_it_touches_the_query()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        // ON THE INTERFACE THREAD, because this model is the window's DataContext - setting a
        // property on it from the test thread raises PropertyChanged into live bindings, which only
        // fails when the machine is busy enough for the two to overlap.
        WpfHost.On(() => model.QueryText = "name:spooler");
        WpfHost.On(() => model.Chosen.Row = EntryRow.Of(Rows.Entry("Spooler")));

        Assert.True(WpfHost.On(() => model.Chosen.Show()));

        Assert.True(await WpfHost.On(() => window.Act(Shortcut.Back)));
        Assert.False(model.Chosen.Showing);
        Assert.Equal("name:spooler", model.QueryText);

        Assert.True(await WpfHost.On(() => window.Act(Shortcut.Back)));
        Assert.Equal(string.Empty, model.QueryText);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// Enter with no row chosen is handed back rather than swallowed.
    ///
    /// The window takes Enter in preview, before the list gets a look at it, so a press claimed by
    /// something that opened nothing is a press that silently stops working for whatever needed it
    /// next.
    /// </summary>
    [Fact]
    public async Task Enter_with_no_row_chosen_is_handed_back()
    {
        var window = WpfHost.Window();

        Assert.False(await WpfHost.On(() => window.Act(Shortcut.OpenDetails)));

        WpfHost.On(window.Close);
    }
}
