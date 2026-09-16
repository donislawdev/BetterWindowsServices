using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Bws.Core.Planning;
using static Bws.Gui.Tests.PlanFixture;

namespace Bws.Gui.Tests;

/// <summary>
/// The BODY of the plan sheet as a thing under a pointer: where its buttons end and where its
/// wheel goes. Two of the owner's four remarks of the evening of 2026-09-16, and a file of their
/// own because PlanViewGuards stood at 598 lines with them in it, against a ratchet that allows
/// two test files over 500 and already had two.
///
/// The fixture is the shared one: two rows picked, so a stop plan prints two commands, which is
/// what "Copy all" needs to exist and what the wheel needs to have somewhere to go.
/// </summary>
public sealed class PlanBodyGuards
{
    /// <summary>
    /// "Copy all" ends where the "Copy" of every line under it ends. The owner's remark of
    /// 2026-09-16: it stood further right by exactly a command box - margin, edge and padding -
    /// because a line's button sits inside one and the heading's sat in none. The heading's now
    /// stands in the same box unpainted, and this asks for the two right edges rather than for
    /// the tokens, so a box that changes shape keeps them together or fails here.
    /// </summary>
    [Fact]
    public async Task Copy_all_ends_where_the_copy_buttons_of_the_lines_end()
    {
        var window = await Ready();

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        var (all, line) = WpfHost.On(() =>
        {
            window.PlanPanel.Measure(new Size(1000, 800));
            window.PlanPanel.Arrange(new Rect(0, 0, 1000, 800));
            window.PlanPanel.UpdateLayout();

            var heading = window.PlanPanel.CopyAllButton;
            var first = Descendants(window.PlanPanel.CommandList)
                .OfType<Button>()
                .First(button => AutomationProperties.GetAutomationId(button) == "planCopyCommand");

            return (
                heading.TranslatePoint(new Point(heading.ActualWidth, 0), window.PlanPanel).X,
                first.TranslatePoint(new Point(first.ActualWidth, 0), window.PlanPanel).X);
        });

        Assert.True(all > 100 && line > 100, $"the buttons end at {all} and {line}, which is not a laid out sheet");
        Assert.True(
            Math.Abs(all - line) < 0.5,
            $"\"Copy all\" ends at {all} and the first line's \"Copy\" at {line} - the heading's button is not in the box the lines' buttons are in");

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The wheel over a command scrolls the SHEET. The owner's remark of 2026-09-16 over twenty
    /// commands: "the scroll stops working" - a command's own viewer scrolls sideways and marked
    /// every wheel event handled all the same, so with boxes under the pointer the sheet never
    /// heard it. The sheet is laid out too short to hold the plan, and the wheel is raised on the
    /// text of a command rather than on the sheet, which is where a pointer over a box puts it.
    /// </summary>
    [Fact]
    public async Task The_wheel_over_a_command_scrolls_the_sheet_and_not_the_command()
    {
        var window = await Ready();

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        var (scrollable, before, after) = WpfHost.On(() =>
        {
            // Short on purpose: the body has to have somewhere to scroll to.
            window.PlanPanel.Measure(new Size(1000, 260));
            window.PlanPanel.Arrange(new Rect(0, 0, 1000, 260));
            window.PlanPanel.UpdateLayout();

            var body = window.PlanPanel.Body;
            var text = Descendants(window.PlanPanel.CommandList).OfType<TextBlock>().First();
            var was = body.VerticalOffset;

            // AS THE INPUT SYSTEM DELIVERS IT: the preview tunnels first, and the bubbling event
            // follows only if nobody handled the preview. Raising the bubbling one alone would
            // hand it to the command's viewer before the sheet ever saw it, fixed or not.
            var preview = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
            {
                RoutedEvent = UIElement.PreviewMouseWheelEvent,
                Source = text
            };
            text.RaiseEvent(preview);

            if (!preview.Handled)
            {
                text.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = text
                });
            }

            window.PlanPanel.UpdateLayout();

            return (body.ScrollableHeight, was, body.VerticalOffset);
        });

        Assert.True(scrollable > 0, "the sheet's body has nowhere to scroll at this height, so the wheel proves nothing here");
        Assert.Equal(0, before);
        Assert.True(after > 0, "the wheel over a command moved the sheet by nothing - the command's own viewer kept it");

        WpfHost.On(window.Close);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;

            foreach (var under in Descendants(child))
            {
                yield return under;
            }
        }
    }
}
