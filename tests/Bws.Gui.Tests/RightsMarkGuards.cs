using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The shield on the write buttons of a session without administrator rights - UX-GUI-004 (b) of
/// the audit of 2026-09-23.
///
/// <b>What was wrong:</b> a window started from the Start menu has no rights, and everything in it
/// looked ready to act - every button live, every plan built in full - and the one sentence about
/// rights waited under the plan, beside a grey button. The shield says it before the plan, the way
/// Windows marks a button that will ask for rights.
///
/// <b>Laid out rather than read off the markup</b> - GUI rule 10: a Visibility set in a style can be
/// silently beaten, and a mark that is Visible with no width is no mark. So the bar is measured and
/// arranged, and the question is which shields took room.
/// </summary>
public sealed class RightsMarkGuards
{
    [Fact]
    public void The_six_write_buttons_wear_a_shield_while_the_session_cannot_carry_their_plans_out()
    {
        _ = WpfHost.Resources;

        var (shields, tip) = WpfHost.On(() =>
        {
            var bar = new ActionBar { NeedsRights = true };

            bar.Picked(1, onlyDrivers: false);

            return (Shown(bar), bar.Stop.ToolTip as string);
        });

        // Six, not nine: Refresh, Export and Overview change nothing, so they wear nothing.
        Assert.Equal(6, shields);
        Assert.Equal(Texts.Of("gui.action.stop.hint") + " " + Texts.Of("gui.action.needsRights"), tip);
    }

    [Fact]
    public void A_session_with_rights_draws_no_shield_and_says_nothing_about_rights()
    {
        _ = WpfHost.Resources;

        var (shields, tip) = WpfHost.On(() =>
        {
            var bar = new ActionBar { NeedsRights = false };

            bar.Picked(1, onlyDrivers: false);

            return (Shown(bar), bar.Stop.ToolTip as string);
        });

        Assert.Equal(0, shields);
        Assert.Equal(Texts.Of("gui.action.stop.hint"), tip);    }

    /// <summary>The window tells the bar from the session's rights - both answers, so neither is a default.</summary>
    [Fact]
    public void The_window_tells_the_bar_whether_this_session_has_rights()
    {
        _ = WpfHost.Resources;

        foreach (var elevated in new[] { false, true })
        {
            var window = WpfHost.On(() => new MainWindow(
                WpfHost.Nowhere(),
                new MainViewModel { Says = new Says { Elevated = elevated } }));

            Assert.Equal(!elevated, WpfHost.On(() => window.Actions.NeedsRights));

            WpfHost.On(window.Close);
        }
    }

    /// <summary>How many shields in the bar took room once it was laid out.</summary>
    private static int Shown(ActionBar bar)
    {
        bar.Measure(new Size(2000, 200));
        bar.Arrange(new Rect(0, 0, 2000, 200));
        bar.UpdateLayout();

        var mark = bar.FindResource("RightsMark");

        return Descendants(bar).OfType<Path>().Count(path =>
            ReferenceEquals(path.Style, mark) && path.Visibility == Visibility.Visible && path.ActualWidth > 0);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject from)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(from); i++)
        {
            var child = VisualTreeHelper.GetChild(from, i);

            yield return child;

            foreach (var below in Descendants(child))
            {
                yield return below;
            }
        }
    }
}
