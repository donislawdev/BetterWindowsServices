using System.Windows;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The empty state as a thing on a window, rather than as a view model.
///
/// <b><see cref="EmptyStateTests"/> holds what the window KNOWS. This holds the one question that
/// class cannot ask: whether any of it reaches the window.</b> The two are deliberately apart, the
/// same split <see cref="DetailsViewGuards"/> makes beside <see cref="ChosenGuards"/>.
///
/// <b>Written the day the panel moved into its own file, 2026-08-18, and the move is exactly why it
/// is needed.</b> While the markup lived in MainWindow.xaml it bound to Says.ListMessage through the
/// window's own DataContext. It now binds to ListMessage through a DataContext of its own, and a
/// binding that resolves to nothing is silent twice over: nothing in the build says so - backlog 19 -
/// and nothing on a screenshot does either, because a dead binding here paints an EMPTY RECTANGLE in
/// the middle of the window. That rectangle is complaint 9 of the eleven in `docs/11`, which is the
/// complaint this panel was built to answer, so the failure mode of the move is the regression of
/// the feature.
/// </summary>
public sealed class EmptyStateViewGuards
{
    /// <summary>
    /// The sentence a person would read is the sentence the window holds, and the line under it is
    /// away when there is nothing to offer.
    ///
    /// <b>Both triggers are exercised, in opposite directions, and that is the point of asking at
    /// this moment.</b> A window nobody has read a machine into is in the loading state - there is a
    /// sentence and there is no way out of waiting - so the panel's own trigger must NOT have fired
    /// and the way-out line's must have. A single direction would pass on a panel whose triggers
    /// never fire at all.
    /// </summary>
    [Fact]
    public void What_it_says_on_the_window_is_what_the_window_holds()
    {
        var window = WpfHost.Window();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        // SETTLED BEFORE EVERY READ. A trigger hanging off a binding is worked out by the dispatcher
        // after the code that built the window, so reading straight away gets the value from before -
        // and only when the machine is busy enough to make the gap visible. WpfHost.Settled carries
        // the rest of that argument.
        WpfHost.Settled();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.ListEmptyState.Visibility));

        // Not merely non-empty: the same string. A panel showing its own idea of what is wrong would
        // satisfy "there are words there" and be a second sentence to keep in step with the first.
        Assert.Equal(
            WpfHost.On(() => model.Says.ListMessage),
            WpfHost.On(() => window.ListEmptyState.Sentence.Text));

        Assert.NotEqual(string.Empty, WpfHost.On(() => window.ListEmptyState.Sentence.Text));

        // There is no way out of waiting, and a way out that does nothing is worse than none.
        Assert.Equal(
            Visibility.Collapsed,
            WpfHost.On(() => window.ListEmptyState.WayOut.Visibility));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// It gets out of the way the moment there are rows, which is the half that decides whether the
    /// list is usable at all.
    ///
    /// <b>The panel covers the middle of the grid, so a trigger that never fires is not a cosmetic
    /// fault - it is a window that shows a sentence over eight hundred rows for as long as it is
    /// open.</b>
    ///
    /// <b>The reading happens BEFORE the model is handed to the window, and the order is the whole
    /// reason this is safe.</b> A model that is already a window's DataContext raises its changes
    /// into live bindings, so reading it from the test thread would be a cross thread touch of the
    /// interface - which is what <see cref="DetailsViewGuards"/> works around by going through the
    /// dispatcher for every set. There is nothing to work around if the reading is over before the
    /// bindings exist.
    /// </summary>
    [Fact]
    public async Task It_is_off_the_window_as_soon_as_there_are_rows()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        await model.LoadAsync();

        // Established without a window first, so a failure below is about the markup rather than
        // about what the model decided. Two red tests saying different things beat one saying both.
        Assert.Equal(string.Empty, model.Says.ListMessage);

        var window = WpfHost.Window();

        WpfHost.On(() => window.DataContext = model);
        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.ListEmptyState.Visibility));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// It takes neither the keyboard nor the mouse, and the third of these has no precedent in this
    /// project.
    ///
    /// <b>A UserControl is a ContentControl, and a ContentControl arrives Focusable and a tab
    /// stop</b> - measured on this product on 2026-08-13, `docs/10` trap 8. That much the row of
    /// filters and the details panel have already paid for, and both carry the same two assertions.
    ///
    /// <b>IsHitTestVisible is the new one and it is the dangerous one, because it is not a default
    /// being restored - it is a property the StackPanel this replaced set by hand.</b> A UserControl
    /// arrives hit testable, and this one sits OVER the middle of the grid rather than instead of
    /// it. Leave it out and the rows underneath stop taking a click at exactly the moment somebody
    /// is trying to get back to them - with the panel invisible, so there is nothing on the screen
    /// to suggest where the clicks are going.
    /// </summary>
    [Fact]
    public void It_takes_neither_the_keyboard_nor_the_mouse()
    {
        var window = WpfHost.Window();

        Assert.False(
            WpfHost.On(() => window.ListEmptyState.Focusable),
            "The empty state takes focus itself, so Tab stops on something that does nothing.");

        Assert.False(
            WpfHost.On(() => window.ListEmptyState.IsTabStop),
            "The empty state is a tab stop of its own, in front of the list it sits over.");

        Assert.False(
            WpfHost.On(() => window.ListEmptyState.IsHitTestVisible),
            "The empty state takes clicks, so it swallows them on their way to the rows beneath it.");

        WpfHost.On(window.Close);
    }
}
