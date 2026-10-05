using System.Windows.Input;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What reads the query inside the 400 ms the search box waits after the last key - G-9 of the
/// external stability report of 2026-09-29.
///
/// <b>The box's binding is delayed on purpose</b>, so eight hundred rows are narrowed once per burst
/// of typing rather than once per character. Anything that read the model's query inside that
/// window read the one from before the last few keys: a restart carried a shorter query, Escape
/// found nothing to clear and did nothing, a chip wrote its member into the old text. Each test
/// sets the box and asks at once - the delay has not run out.
/// </summary>
public sealed class BoxCommitGuards
{
    [Fact]
    public async Task A_restart_carries_what_the_box_says_rather_than_what_the_model_heard()
    {
        var (window, _) = await Opened();

        WpfHost.On(() => window.Search.Box.Text = "spool");

        Assert.Equal("spool", WpfHost.On(() => window.HandOverNow().Query));

        WpfHost.On(window.Close);
    }

    [Fact]
    public async Task Escape_straight_after_typing_empties_the_box()
    {
        var (window, model) = await Opened();

        WpfHost.On(() =>
        {
            window.Search.Box.Text = "spool";
            model.Suggesting.Close();
        });

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Back, out _)));
        Assert.Equal(string.Empty, WpfHost.On(() => model.QueryText));
        Assert.Equal(string.Empty, WpfHost.On(() => window.Search.Box.Text));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The road a chip takes: clicking or tabbing to one takes the keyboard from the box first, and
    /// that hands the text over before the chip writes its member into it.
    /// </summary>
    [Fact]
    public async Task The_box_hands_its_text_over_when_it_loses_the_keyboard()
    {
        var (window, model) = await Opened();

        WpfHost.On(() =>
        {
            var box = window.Search.Box;

            box.Text = "spool";
            box.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, box, null)
            {
                RoutedEvent = Keyboard.LostKeyboardFocusEvent
            });
        });

        Assert.Equal("spool", WpfHost.On(() => model.QueryText));

        WpfHost.On(window.Close);
    }

    private static async Task<(MainWindow Window, MainViewModel Model)> Opened()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler"), Rows.Entry("W32Time")), new SteppedClock());

        await model.LoadAsync();

        var window = WpfHost.Window(model);

        // The box's binding attaches lazily, at a lower priority than a call from here - SearchBoxKeys
        // carries the hour that cost. Settled first, or the text set below is overwritten by it.
        WpfHost.Settled();

        return (window, model);
    }
}
