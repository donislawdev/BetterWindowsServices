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

        // Both kinds are on screen, so what comes back after Escape is the whole of what the scope
        // holds rather than half of it - the window opens on services, and this test is about the
        // question rather than about which list is showing.
        model.Scope = EntryScope.Everything;
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

        // THE BOX OPENS EMPTY SINCE 2026-08-19, so this is true from the very first press. It used
        // to open carrying !type:driver, and the first clear had that to do - a step this test had
        // to take before it could ask its own question.
        Assert.False(model.ClearQuery());

        // And again after something really was typed and taken back, which is the state a person
        // arrives in rather than the one the window starts in.
        model.QueryText = "spool";

        Assert.True(model.ClearQuery());
        Assert.False(model.ClearQuery());
    }

    /// <summary>
    /// Clearing the box leaves the window on the list it was on.
    ///
    /// <b>THIS ASSERTS THE REVERSE OF WHAT IT DID UNTIL 2026-08-19, and both are owner's decisions.
    /// It was called Clearing_the_query_brings_the_drivers_switch_back_with_it</b>, and it was
    /// keeping a real promise: while hiding drivers was a member of the query, emptying the query
    /// had to give them back, or the box and the list would have said two different things.
    ///
    /// Scope is a state beside the query now, so Escape empties the QUESTION and nothing else -
    /// which is what a person pressing it means. Throwing away which list they were looking at as
    /// well would be one key press doing two things, and the second one silently.
    /// </summary>
    [Fact]
    public async Task Clearing_the_query_leaves_the_scope_where_it_was()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler"), Rows.Driver("beep")), new SteppedClock());

        await model.LoadAsync();

        model.Scope = EntryScope.Drivers;
        model.QueryText = "beep";

        Assert.Single(model.Rows);

        Assert.True(model.ClearQuery());

        Assert.Equal(EntryScope.Drivers, model.Scope);
        Assert.Equal(["beep"], model.Rows.Select(row => row.ServiceName));
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
    public void Escape_closes_the_panel_before_it_touches_the_query()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        // ON THE INTERFACE THREAD, because this model is the window's DataContext - setting a
        // property on it from the test thread raises PropertyChanged into live bindings, which only
        // fails when the machine is busy enough for the two to overlap.
        WpfHost.On(() => model.QueryText = "name:spooler");
        WpfHost.On(() => model.Chosen.Row = EntryRow.Of(Rows.Entry("Spooler")));

        Assert.True(WpfHost.On(() => model.Chosen.Show()));

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
        Assert.False(model.Chosen.Showing);
        Assert.Equal("name:spooler", model.QueryText);

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
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
    public void Enter_with_no_row_chosen_is_handed_back()
    {
        var window = WpfHost.Window();

        Assert.False(WpfHost.On(() => window.Act(Shortcut.OpenDetails, out _)));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The one shortcut that waits for the machine claims its key BEFORE it starts waiting -
    /// backlog 302.
    ///
    /// <b>A routed event is over the moment its handler gives control back, and an async method
    /// gives control back at its first await.</b> So <c>e.Handled = await Act(...)</c> wrote the
    /// answer into an argument WPF had already finished reading, and F5 reached the query box as
    /// well as this window. Every other branch got away with the same line, because awaiting a
    /// task that has already finished never yields - which is what made this a fault of shape
    /// rather than of behaviour, and what would have handed it to the next asynchronous branch
    /// anybody wrote.
    ///
    /// <b>WHAT THIS DOES NOT EXECUTE, said rather than left to be assumed: the routed event
    /// itself.</b> Building a real <see cref="KeyEventArgs"/> needs a presentation source, which
    /// needs a window on somebody's screen, and these tests deliberately show none. What is
    /// checked is the one thing the fault was made of - that the answer is in hand before the work
    /// is - and the signature is what now makes the other order impossible to write.
    /// </summary>
    [Fact]
    public async Task Refresh_claims_its_key_before_the_reading_it_starts_has_finished()
    {
        var window = WpfHost.Window();
        var work = Task.CompletedTask;

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Refresh, out work)));

        await work;

        WpfHost.On(window.Close);
    }
}
