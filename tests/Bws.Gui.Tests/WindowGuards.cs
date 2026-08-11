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

    /// <summary>
    /// The jump a letter makes, asked of the window rather than of the rule behind it.
    ///
    /// <b><see cref="MainWindow.JumpTo"/> was made internal so that it could be checked, and then
    /// was not</b> - found on 2026-08-11 while repairing backlog 161, with the whole method
    /// uncovered. <c>TypeToFindTests</c> holds the rule about WHICH row a letter picks;
    /// what is here is the half only a window has: the grid's own order, the selection actually
    /// moving, and the answer that decides whether the key press is passed on.
    ///
    /// <b>The rows are handed to the grid rather than loaded through the model, on purpose.</b>
    /// The window builds its own view model and that model reads this machine's service control
    /// manager - a unit test that did so would be measuring the machine. Setting ItemsSource is
    /// also the honest shape for the claim: backlog 150 established that the jump has to follow
    /// what is ON SCREEN rather than the model's order, because a sorted column makes those two
    /// different sequences.
    /// </summary>
    [Fact]
    public void A_letter_moves_the_selection_to_the_next_row_that_starts_with_it()
    {
        var window = WpfHost.Window();

        var moved = WpfHost.On(() =>
        {
            window.Entries.ItemsSource = new[]
            {
                EntryRow.Of(Rows.Entry("Appinfo")),
                EntryRow.Of(Rows.Entry("Spooler")),
                EntryRow.Of(Rows.Entry("Winmgmt"))
            };

            return window.JumpTo('s');
        });

        Assert.True(moved);

        var landed = WpfHost.On(() => (window.Entries.SelectedItem as EntryRow)?.ServiceName);

        Assert.Equal("Spooler", landed);
    }

    /// <summary>
    /// The two ways a jump does nothing, and both have to say so.
    ///
    /// <b>A press marked handled by something that did nothing is a press that silently stops
    /// working for whatever needed it next</b> - which is the sentence the method's own comment
    /// gives as the reason it returns a bool at all. So the claim is the answer, not the
    /// selection: a letter nothing starts with, and no letter at all.
    /// </summary>
    [Fact]
    public void A_letter_nothing_starts_with_is_handed_back()
    {
        var window = WpfHost.Window();

        var answers = WpfHost.On(() =>
        {
            window.Entries.ItemsSource = new[] { EntryRow.Of(Rows.Entry("Spooler")) };

            return (Nothing: window.JumpTo('q'), NoLetter: window.JumpTo(null));
        });

        Assert.False(answers.Nothing);
        Assert.False(answers.NoLetter);
    }

    /// <summary>
    /// F5 reads everything again, and reports that it did - so the key is not passed on to the
    /// grid, which has its own plans for it.
    ///
    /// This is the one shortcut whose work is a real reading, so it is the one that costs a
    /// moment. It reads THIS machine, deliberately: the window builds its own view model and the
    /// point of the test is that the window's own path works, not that a fake can be refreshed.
    /// </summary>
    [Fact]
    public async Task F5_reads_everything_again_and_says_it_did()
    {
        var window = WpfHost.Window();

        Assert.True(await WpfHost.On(() => window.Act(Shortcut.Refresh)));
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
