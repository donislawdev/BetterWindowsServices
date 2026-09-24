using System.Windows.Controls;
using System.Windows.Input;

namespace Bws.Gui.Tests;

/// <summary>
/// The Help button and its menu - UX-GUI-014 and the shortcut half of UX-GUI-010, owner's decision
/// 2026-09-24. Until that day the window named Ctrl+F, Ctrl+C and Ctrl+A nowhere, said its own
/// version nowhere, and answered F1 with nothing.
///
/// <b>What is not here, said rather than left as a gap:</b> that the two pages really open in a
/// browser. Nobody should find out by opening one from a test run - ExternalLinksGuards holds the
/// rule both roads follow, and the addresses are pinned below.
/// </summary>
public sealed class HelpMenuGuards
{
    /// <summary>
    /// The menu holds the six keys, the two pages and the version, in three groups - asked of the
    /// real menu hung on the real button. The keys are asserted on the ITEM, for the reason
    /// RowMenuGuards gives: an entry saying "Ctrl+F" and a style never binding it would leave the
    /// menu looking as it did before.
    /// </summary>
    [Fact]
    public void The_help_menu_names_every_key_the_pages_and_the_version()
    {
        var window = WpfHost.Window();

        var opened = WpfHost.On(() => window.OpenHelp());
        var items = WpfHost.On(() => window.Scope.Help.ContextMenu!.Items.Cast<object>().ToList());

        Assert.True(opened, "The Help button has no menu to open.");

        var shape = WpfHost.On(() => items.Select(item => item switch
        {
            MenuItem { DataContext: RowMenuEntry entry } => $"{entry.LabelKey} {((MenuItem)item).InputGestureText}".Trim(),
            Separator => "-",
            _ => "?"
        }).ToList());

        Assert.Equal(
            [
                "gui.help.find Ctrl+F",
                "gui.help.refresh F5",
                "gui.help.details Enter",
                "gui.help.copy Ctrl+C",
                "gui.help.selectAll Ctrl+A",
                "gui.help.back Esc",
                "-",
                "gui.help.queryLanguage",
                "gui.help.projectPage",
                "-",
                "gui.help.version"
            ],
            shape);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// F1 is the help key, as it is in every Windows program - and it opens the same menu the
    /// button does rather than a second one.
    /// </summary>
    [Fact]
    public void F1_opens_the_help_menu()
    {
        Assert.Equal(Shortcut.Help, Shortcuts.For(Key.F1, ModifierKeys.None));

        var window = WpfHost.Window();

        Assert.True(WpfHost.On(() => window.Act(Shortcut.Help, out _)));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The version item carries the number this build was stamped with and nothing after a plus -
    /// the part the SDK appends for the source revision is not a version anybody reports.
    /// </summary>
    [Fact]
    public void The_version_item_says_the_stamped_number()
    {
        var number = HelpMenu.VersionNumber;

        Assert.NotEqual(Texts.Of("gui.help.versionUnknown"), number);
        Assert.DoesNotContain("+", number, StringComparison.Ordinal);
        Assert.True(char.IsDigit(number[0]), $"A version starts with a digit, and this one is \"{number}\".");

        var window = WpfHost.Window();
        var entry = WpfHost.On(() => window.Scope.Help.ContextMenu!.Items.OfType<MenuItem>()
            .Select(item => (RowMenuEntry)item.DataContext)
            .Single(one => one.LabelKey == "gui.help.version"));

        Assert.Equal(Texts.Of("gui.help.version", number), entry.Label);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// "Select every entry" does what Ctrl+A does in the grid - asked of the grid, so the item and
    /// the key are one act.
    /// </summary>
    [Fact]
    public async Task Selecting_every_entry_picks_every_row_in_the_list()
    {
        var window = await PlanFixture.Ready();

        Assert.True(WpfHost.On(() => window.SelectEverything()));

        Assert.Equal(
            WpfHost.On(() => window.Entries.Items.Count),
            WpfHost.On(() => window.Entries.SelectedItems.Count));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// An item for a key does what the key does, through the same act - asked of Esc, which with
    /// nothing open clears the search. A menu that only described the keys would pass every test
    /// above and fail this one.
    /// </summary>
    [Fact]
    public async Task A_shortcut_item_does_what_its_key_does()
    {
        var window = await PlanFixture.Ready();
        var model = WpfHost.On(() => (Bws.Gui.ViewModels.MainViewModel)window.DataContext);

        WpfHost.On(() => model.QueryText = "status:running");

        var back = WpfHost.On(() => window.Scope.Help.ContextMenu!.Items.OfType<MenuItem>()
            .Select(item => (RowMenuEntry)item.DataContext)
            .Single(entry => entry.LabelKey == "gui.help.back"));

        await WpfHost.On(() => back.Act());

        Assert.Equal(string.Empty, WpfHost.On(() => model.QueryText));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The two pages are the project's own, over https, and the button says before it is pressed
    /// that the tool itself connects to nothing - the one moment a person could feel `ADR-19` was
    /// broken is the moment a browser appears.
    /// </summary>
    [Fact]
    public void The_pages_are_the_projects_own_over_https()
    {
        Assert.Equal("https://betterwindowsservices.donislawdev.com/", ExternalLinks.ProjectPage);
        Assert.Equal("https://betterwindowsservices.donislawdev.com/query-language/", ExternalLinks.QueryLanguagePage);
        Assert.Contains("connects to nothing", Texts.Of("gui.help.hint"), StringComparison.Ordinal);
    }
}
