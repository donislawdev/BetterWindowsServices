using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The window itself, built rather than reasoned about.
///
/// <b>This is the guard the two worst faults in `docs/10` went past.</b> Merging WPF UI's
/// dictionaries beside a <c>ThemeMode</c> ends the process at startup, and removing the
/// <c>ThemeMode</c> without giving the window a background leaves the list invisible while the
/// count line still reports every entry. Both were found by a person running the program. Both
/// are markup resolving - or failing to - at the moment a window is constructed, which is what
/// the first test here does.
///
/// It cannot say what the window LOOKS like. RowStateGuards renders the list for that, and
/// <c>tools/gui-probe/check.ps1</c> is still the only thing that sees a real screen.
/// </summary>
public sealed class WindowGuards
{
    /// <summary>
    /// The markup resolves and the window is built.
    ///
    /// Every <c>StaticResource</c> in <c>MainWindow.xaml</c> is resolved while the file is read,
    /// so a name that stopped existing - a style renamed here, a key dropped by an update to the
    /// control library - throws right here rather than on somebody's machine.
    /// </summary>
    [Fact]
    public void The_window_is_built_and_every_name_its_markup_uses_resolves()
    {
        var window = WpfHost.Window();

        Assert.NotNull(window);
    }

    /// <summary>
    /// Ctrl+F reports that it did something, so the press is not passed on to a control that
    /// would type an f into the query box.
    /// </summary>
    [Fact]
    public async Task Control_F_is_taken_by_the_window()
    {
        var window = WpfHost.Window();

        Assert.True(await WpfHost.On(() => window.Act(Shortcut.FocusQuery)));
    }

    /// <summary>
    /// AND THE ONE THAT MATTERS MORE: Escape on an empty box is handed back.
    ///
    /// The window takes these keys in preview, before anything else in it gets a look, so a
    /// shortcut that claims a press it did nothing with is a key that silently stops working for
    /// whatever needed it next. Escape is the one every dialog in Windows already has plans for.
    /// </summary>
    [Fact]
    public async Task Escape_with_nothing_to_clear_is_handed_back()
    {
        var window = WpfHost.Window();

        Assert.False(await WpfHost.On(() => window.Act(Shortcut.ClearQuery)));
    }

    [Fact]
    public async Task A_key_the_window_has_no_use_for_is_passed_on()
    {
        var window = WpfHost.Window();

        Assert.False(await WpfHost.On(() => window.Act(Shortcut.None)));
    }

    /// <summary>
    /// What a copy would put on the clipboard is decided where it can be checked.
    ///
    /// The clipboard call itself needs a window and cannot be tested - which is the reason the
    /// decision was moved out of it rather than left beside it.
    /// </summary>
    [Fact]
    public async Task With_no_row_chosen_there_is_nothing_to_copy()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        await model.LoadAsync();

        Assert.Null(model.SelectedServiceName);
        Assert.Null(model.SelectedDisplayName);
    }

    [Fact]
    public async Task The_chosen_row_is_what_a_copy_would_take()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler", "Print Spooler")), new SteppedClock());

        await model.LoadAsync();

        model.Selected = model.Rows[0];

        Assert.Equal("Spooler", model.SelectedServiceName);
        Assert.Equal("Print Spooler", model.SelectedDisplayName);
    }
}
