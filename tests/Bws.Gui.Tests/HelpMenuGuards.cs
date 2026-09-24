using System.Windows.Controls;
using System.Windows.Input;

namespace Bws.Gui.Tests;

/// <summary>
/// The Help button and its menu - UX-GUI-014, owner's decision 2026-09-24, and cut the same day to
/// the one page it offers now (HelpMenu says what went and what that costs).
///
/// <b>What is not here, said rather than left as a gap:</b> that the page really opens in a
/// browser. Nobody should find out by opening one from a test run - ExternalLinksGuards holds the
/// rule every road out follows, and the address is pinned below.
/// </summary>
public sealed class HelpMenuGuards
{
    /// <summary>
    /// The menu holds the query language page and nothing else - asked of the real menu hung on the
    /// real button, so a shortcut, the project's website or the version coming back is a red test
    /// rather than a menu that quietly grew again.
    /// </summary>
    [Fact]
    public void The_help_menu_holds_the_query_language_page_and_nothing_else()
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

        Assert.Equal(["gui.help.queryLanguage"], shape);

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
    /// The page is the project's own, over https, and the button says before it is pressed that the
    /// tool itself connects to nothing - the one moment a person could feel `ADR-19` was broken is
    /// the moment a browser appears.
    /// </summary>
    [Fact]
    public void The_page_is_the_projects_own_over_https()
    {
        Assert.Equal("https://betterwindowsservices.donislawdev.com/query-language/", ExternalLinks.QueryLanguagePage);
        Assert.Contains("connects to nothing", Texts.Of("gui.help.hint"), StringComparison.Ordinal);
    }
}
