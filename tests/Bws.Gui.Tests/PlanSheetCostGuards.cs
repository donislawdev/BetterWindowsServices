using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;
using static Bws.Gui.Tests.PlanFixture;

namespace Bws.Gui.Tests;

/// <summary>
/// What the plan sheet costs to draw and to build - W6 of the performance series, 2026-09-29,
/// report items G-6 and G-7. The shadow lies under the sheet rather than on it, and past twenty
/// commands a block stands in one field rather than a box per line.
///
/// <b>Its own file because both are about cost and neither about what the sheet says</b>, and
/// because PlanViewGuards stands past the length the ratchet calls long.
/// </summary>
public sealed class PlanSheetCostGuards
{
    /// <summary>
    /// <b>THE SHEET WEARS NO EFFECT, AND ITS SHADOW LIES UNDER IT IN THE SAME RECTANGLE.</b> An
    /// effect on the sheet made WPF blur the sheet again on every caret blink and every tick of a
    /// run - a third to a half of a core where WPF draws in software, which is Remote Desktop - and
    /// turned ClearType off for all its text. Asked of the laid out panel rather than of the
    /// markup: the shadow must end up where the sheet is, at the size the sheet is.
    /// </summary>
    [Fact]
    public async Task The_sheet_wears_no_effect_and_its_shadow_lies_under_it_in_the_same_rectangle()
    {
        var window = await Ready();

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        var (effect, shadowed, hit, under, same) = WpfHost.On(() =>
        {
            LaidOut(window, 800);

            var sheet = window.PlanPanel.TheSheet;
            var cell = (Panel)sheet.Parent;
            var shadow = cell.Children.OfType<Border>().First();

            return (
                sheet.Effect,
                shadow.Effect is DropShadowEffect,
                shadow.IsHitTestVisible,
                cell.Children.IndexOf(shadow) < cell.Children.IndexOf(sheet),
                shadow.RenderSize == sheet.RenderSize
                    && shadow.TranslatePoint(default, cell) == sheet.TranslatePoint(default, cell));
        });

        Assert.Null(effect);
        Assert.True(shadowed, "nothing in the sheet's cell casts a shadow - the sheet stands flat on the window");
        Assert.False(hit, "the shadow takes the pointer - it is never what somebody pressed");
        Assert.True(under, "the shadow is declared after the sheet, so it is drawn over it");
        Assert.True(same, "the shadow does not have the sheet's rectangle, so it can part from the sheet");

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// <b>PAST TWENTY COMMANDS THE SHEET HOLDS THEM IN ONE FIELD AND BUILDS NO BOX PER LINE.</b> A
    /// stop over a whole scope printed 334 lines as 5013 of the sheet's 7185 elements and stood the
    /// window still for half a second. At twenty the list stays - the threshold is a screenful, not
    /// a preference - and at twenty one the field takes every line, can only be read, and "Copy
    /// all" carries exactly what it shows.
    /// </summary>
    [Fact]
    public async Task Past_twenty_commands_the_sheet_holds_them_in_one_field_and_builds_no_box_per_line()
    {
        var window = await Ready();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        foreach (var count in new[] { CommandBlock.ListedUpTo, CommandBlock.ListedUpTo + 1 })
        {
            Assert.True(WpfHost.On(() => model.Planned.Show(Over(count))));
            WpfHost.Settled();

            var (boxes, field, lines, readOnly, carried) = WpfHost.On(() =>
            {
                LaidOut(window, 800);

                var panel = window.PlanPanel;

                return (
                    panel.CommandList.Items.Count,
                    ((FrameworkElement)panel.CommandField.Parent).Visibility,
                    panel.CommandLines,
                    panel.CommandField.IsReadOnly,
                    panel.CopyAllButton.Tag as string);
            });

            var atOnce = count > CommandBlock.ListedUpTo;

            Assert.Equal(atOnce ? 0 : count, boxes);
            Assert.Equal(atOnce ? Visibility.Visible : Visibility.Collapsed, field);
            Assert.Equal(WpfHost.On(() => model.Planned.Commands), lines);
            Assert.True(readOnly);
            Assert.Equal(string.Join(Environment.NewLine, lines), carried);
        }

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The wheel over the field scrolls the SHEET. A text box keeps a scroller of its own, and a
    /// scroller marks the wheel handled whether or not it has anywhere to go - the owner's remark of
    /// 2026-09-16 over the line boxes, which the body answers by taking the wheel in the tunnel.
    /// Raised as the input system raises it: the preview first, the bubbling one only if nobody took
    /// the preview.
    /// </summary>
    [Fact]
    public async Task The_wheel_over_the_field_of_commands_scrolls_the_sheet()
    {
        var window = await Ready();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        Assert.True(WpfHost.On(() => model.Planned.Show(Over(CommandBlock.ListedUpTo + 5))));
        WpfHost.Settled();

        var (scrollable, after) = WpfHost.On(() =>
        {
            LaidOut(window, 260);

            var body = window.PlanPanel.Body;
            var field = window.PlanPanel.CommandField;
            var preview = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
            {
                RoutedEvent = UIElement.PreviewMouseWheelEvent,
                Source = field
            };

            field.RaiseEvent(preview);

            if (!preview.Handled)
            {
                field.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = field
                });
            }

            window.PlanPanel.UpdateLayout();

            return (body.ScrollableHeight, body.VerticalOffset);
        });

        Assert.True(scrollable > 0, "the sheet's body has nowhere to scroll at this height, so the wheel proves nothing here");
        Assert.True(after > 0, "the wheel over the field moved the sheet by nothing - the field's own scroller kept it");

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// <b>BOTH BOXES THAT STAND IN SOMEBODY ELSE'S FRAME FIND THEIR CHROME ANSWERED TRANSPARENT.</b>
    /// The brushes live in ChromelessBox's own resources since 2026-09-29, and the search box and
    /// the field of commands reach them through BasedOn. Asked of the built boxes in the running
    /// theme, the way the library's template asks: a box that stopped finding them would draw a
    /// second fill and a second line inside its frame.
    /// </summary>
    [Fact]
    public async Task Both_boxes_in_a_frame_of_their_own_find_the_library_chrome_answered_transparent()
    {
        var window = await Ready();

        var found = WpfHost.On(() =>
            new[] { window.Search.QueryBox, window.PlanPanel.CommandField, window.PlanPanel.WayBackField }
                .SelectMany(box => new[] { "TextControlBackground", "ControlStrokeColorDefaultBrush", "TextControlFocusedBorderBrush" }
                    .Select(key => (box.Name, key, brush: box.TryFindResource(key) as SolidColorBrush)))
                .Where(one => one.brush?.Color != Colors.Transparent)
                .Select(one => $"{one.Name} finds {one.key} as {one.brush?.Color.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "nothing"}")
                .ToList());

        Assert.True(found.Count == 0, string.Join(Environment.NewLine, found));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The edge the field's recess takes while the keyboard is in it clears 3:1 against the recess
    /// inside it and the sheet outside it - WCAG 2.2 SC 1.4.11. Whether the edge LIGHTS is a live
    /// window's question, because a window built for a test never holds the keyboard.
    /// </summary>
    [Fact]
    public void The_focus_edge_of_the_field_of_commands_clears_its_ratio_on_both_sides()
    {
        var edge = WpfHost.Declared("FocusLine");

        foreach (var side in new[] { "SurfaceCommandBox", "SurfacePanel" })
        {
            var ratio = ContrastGuards.Contrast(edge, WpfHost.Declared(side));

            Assert.True(ratio >= 3.0, $"FocusLine against {side} measures {ratio:F2}, and an edge that shows focus needs 3.0");
        }
    }

    // -- fixtures --------------------------------------------------------------------------

    /// <summary>The plan panel laid out as a window of 1000 by the height given would lay it out.</summary>
    private static void LaidOut(MainWindow window, double height)
    {
        window.PlanPanel.Measure(new Size(1000, height));
        window.PlanPanel.Arrange(new Rect(0, 0, 1000, height));
        window.PlanPanel.UpdateLayout();
    }

    /// <summary>A stop over as many made up entries as asked, one step and one command each.</summary>
    private static BulkPlan Over(int count)
    {
        string[] names = [.. Enumerable.Range(1, count).Select(n => $"Entry{n}")];

        return new BulkPlan
        {
            Action = new BulkAction(ActionKind.Stop, names),
            Plans =
            [
                .. names.Select(name => new OperationPlan
                {
                    Action = new ServiceAction(ActionKind.Stop, name),
                    Steps = [new PlanStep(name, name, StepOperation.Stop, StepReason.Requested)],
                    Warnings = [],
                    Problems = []
                })
            ],
            Problems = []
        };
    }
}
