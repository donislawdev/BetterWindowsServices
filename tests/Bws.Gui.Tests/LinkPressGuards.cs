using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What a press on Donate, GitHub or the help menu's page asks for - backlog 568, owner's decision
/// of 2026-10-14.
///
/// <b>Pressed for real, opened for nobody.</b> The window is built with a hand-over that writes
/// down which page it was asked for and opens nothing (MainWindow.PagesOpenedBy says why that is
/// production code), so the press goes through the button's own click and the window's own
/// OpenPage, the road a person's press takes.
///
/// <b>What is NOT here.</b> The step from a page's name to its address, which is a switch of three
/// arms in ExternalLinks.OpenAsync whose every arm opens a browser - answered by reading it. The
/// addresses themselves are pinned in ExternalLinksGuards and GitHubButtonGuards.
/// </summary>
public sealed class LinkPressGuards
{
    [Fact]
    public void Each_press_asks_for_its_own_page_and_no_other()
    {
        var asked = new List<ExternalLinks.Destination>();
        var window = WpfHost.Window(destination =>
        {
            asked.Add(destination);
            return Task.FromResult<string?>(null);
        });
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        WpfHost.On(() => window.Scope.GitHub.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));
        WpfHost.On(() => window.Scope.Donate.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));
        WpfHost.On(() => TheHelpItem(window).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)));

        Assert.Equal(
            [ExternalLinks.Destination.Repository, ExternalLinks.Destination.Support, ExternalLinks.Destination.QueryLanguage],
            asked);

        // Handed over, so nothing to say.
        Assert.Equal(string.Empty, WpfHost.On(() => model.Says.Problem));
    }

    [Fact]
    public void A_page_that_did_not_open_says_why_in_the_status_line()
    {
        const string trouble = "the shell said no to this page";

        var window = WpfHost.Window(_ => Task.FromResult<string?>(trouble));
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        WpfHost.On(() => window.Scope.GitHub.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));

        Assert.Contains(trouble, WpfHost.On(() => model.Says.Problem), StringComparison.Ordinal);
    }

    /// <summary>The help menu's one item - the menu is built in the window's constructor.</summary>
    private static MenuItem TheHelpItem(MainWindow window) =>
        window.Scope.Help.ContextMenu?.Items.OfType<MenuItem>().Single()
            ?? throw new InvalidOperationException("the Help button has no menu");
}
