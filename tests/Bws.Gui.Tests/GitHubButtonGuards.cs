using System.Windows;
using System.Windows.Automation;

namespace Bws.Gui.Tests;

/// <summary>
/// The GitHub button, left of Donate since 2026-10-14 - owner's decision.
///
/// <b>Its own file rather than more of ExternalLinksGuards</b>, which these two tests pushed into the
/// crowd of test files standing close to the size ceiling on the day they were written. The rules
/// that matter most about the button - a constant address, the two ways to the browser, nothing
/// outside the links file choosing one - are that class's and hold for this button unchanged, so
/// what is here is only what is new: which address, and where the button stands.
///
/// <b>What is NOT here.</b> That a press asks for this page and not another - LinkPressGuards
/// presses it with a hand-over that opens nothing, since 2026-10-14 (backlog 568). Whether the
/// browser then really opens the page is answered by pressing the button, the same answer
/// ExternalLinksGuards gives for Donate.
/// </summary>
public sealed class GitHubButtonGuards
{
    [Fact]
    public void The_repository_address_is_the_one_the_GitHub_button_promises()
    {
        // Pinned rather than derived, for the reason the support address is: the tooltip names the
        // page before anything is pressed, so a typo here is a promise broken on screen.
        Assert.Equal("https://github.com/donislawdev/BetterWindowsServices", ExternalLinks.RepositoryPage);
        Assert.StartsWith("https://", ExternalLinks.RepositoryPage, StringComparison.Ordinal);

        // Host and path, without the scheme - the way the Donate tooltip names its page.
        var page = new Uri(ExternalLinks.RepositoryPage);
        Assert.Contains(page.Host + page.AbsolutePath, Texts.Of("gui.repository.hint"), StringComparison.Ordinal);
    }

    [Fact]
    public void GitHub_stands_one_gap_left_of_Donate_and_clear_of_the_positions_in_the_narrowest_window()
    {
        var window = WpfHost.Window();
        var gap = ((Thickness)WpfHost.Resources["MarginBetweenControls"]).Right;

        var (id, name, hint, width, githubLeft, githubRight, donateLeft, positionsRight) = WpfHost.On(() =>
        {
            window.WindowStyle = WindowStyle.None;
            window.ShowInTaskbar = false;
            window.Left = -4000;

            // THE NARROWEST WINDOW, from the same token the window's MinWidth reads - a third button
            // on the right end of the row is room taken from the positions on its left, and this is
            // where that runs out first (GUI rule 21).
            window.Width = (double)WpfHost.Resources["WidthWindowLeast"];
            window.Show();
            window.UpdateLayout();

            var github = window.Scope.GitHub;
            var positions = Identified(window.Scope, "scopeSwitch")
                ?? throw new InvalidOperationException("the row has no element named scopeSwitch");

            double Left(FrameworkElement element) => element.TransformToAncestor(window).Transform(new Point(0, 0)).X;

            return (
                AutomationProperties.GetAutomationId(github),
                AutomationProperties.GetName(github),
                github.ToolTip as string,
                github.ActualWidth,
                Left(github),
                Left(github) + github.ActualWidth,
                Left(window.Scope.Donate),
                Left(positions) + positions.ActualWidth);
        });

        WpfHost.On(window.Close);

        Assert.Equal("github", id);
        Assert.Equal(Texts.Of("gui.repository.button"), name);
        Assert.Equal(Texts.Of("gui.repository.hint"), hint);

        // Drawn, not only declared.
        Assert.True(width > 0, "the GitHub button takes no width, so nobody sees it");

        // One gap to Donate, the gap Donate keeps to Help - GUI rule 19, placed by the row's rule
        // rather than by eye.
        Assert.Equal(gap, donateLeft - githubRight, precision: 1);

        // And clear of the positions with at least the same gap left over at the narrowest width.
        Assert.True(
            githubLeft - positionsRight >= gap,
            $"at the narrowest window the GitHub button starts {githubLeft - positionsRight:0.#} px after the positions end, less than one gap");
    }

    private static FrameworkElement? Identified(DependencyObject root, string id)
    {
        if (root is FrameworkElement element && AutomationProperties.GetAutomationId(element) == id)
        {
            return element;
        }

        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (Identified(child, id) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
