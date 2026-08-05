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
        Assert.Equal(Shortcut.ClearQuery, Shortcuts.For(Key.Escape, ModifierKeys.None));

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
}
