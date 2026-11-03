using System.Windows;
using System.Windows.Controls;
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

    /// <summary>
    /// The shield takes its room out of the button's left padding rather than widening the button by
    /// all of it - PadButtonWithMark in Spacing.xaml, so the bar that cannot wrap grows by a few units
    /// a button and not by the mark and the gap after it.
    ///
    /// <b>A guard of its own since 2026-11-03.</b> Until then the mutation registry held this to the
    /// narrowest-window guard in ActionBarGuards, which lost sight of it on 2026-10-14, when the
    /// narrowest window went from 640 to 1000 - the bar fits with the whole width added as well, and
    /// the full registry in CI said MISSED on two Saturdays running.
    ///
    /// <b>The footprint is measured off the laid-out mark</b>, its width and its margin, so the answer
    /// does not come from the padding it judges.
    /// </summary>
    [Fact]
    public void A_shield_takes_its_room_from_the_padding_rather_than_widening_its_button_by_all_of_it()
    {
        _ = WpfHost.Resources;

        var (grown, footprint) = WpfHost.On(() =>
        {
            var bar = new ActionBar { NeedsRights = false };

            bar.Picked(1, onlyDrivers: false);

            var without = WriteWidths(bar);

            bar.NeedsRights = true;

            var with = WriteWidths(bar);
            var mark = bar.FindResource("RightsMark");
            var shield = Descendants(bar).OfType<Path>().First(path => ReferenceEquals(path.Style, mark) && path.ActualWidth > 0);

            return (with.Zip(without, (wide, narrow) => wide - narrow).ToArray(), shield.ActualWidth + shield.Margin.Left + shield.Margin.Right);
        });

        // NOT VACUOUS: six buttons wear the shield, so six have to have grown at all.
        Assert.Equal(6, grown.Count(by => by > 0));
        Assert.All(grown.Where(by => by > 0), by => Assert.True(
            by < footprint - 1,
            $"A write button grew by {by} with the shield, and the shield and its gap take {footprint} - so it added its whole width "
            + "instead of taking it out of the left padding, and the bar that cannot wrap grows by that on every one of six buttons."));
    }

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

    /// <summary>The laid-out widths of the six write buttons, in the order the bar holds them.</summary>
    private static double[] WriteWidths(ActionBar bar)
    {
        bar.Measure(new Size(2000, 200));
        bar.Arrange(new Rect(0, 0, 2000, 200));
        bar.UpdateLayout();

        var write = bar.FindResource("WriteAction");

        return [.. Descendants(bar).OfType<Button>().Where(button => ReferenceEquals(button.Style, write)).Select(button => button.ActualWidth)];
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
