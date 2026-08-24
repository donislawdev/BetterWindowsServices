using System.Windows;
using System.Windows.Controls;

namespace Bws.Gui.Tests;

/// <summary>
/// Every tooltip in this window wraps - backlog 167.
///
/// <b>WHY A GUARD AT ALL FOR THREE SETTERS.</b> The fault it repairs was invisible to everything
/// this project owns: the tooltip was CORRECT, bound to the right text, and unreadable, because a
/// 700 character privilege list came out as one line across half the screen. No test can see that,
/// the layout snapshot does not capture popups, and the pixel probe needs a person. What can be
/// checked is that the three things which make it wrap are still there.
///
/// <b>The BasedOn is asserted first and it is the one that matters most.</b> Trap 4 of `docs/10`:
/// a style whose TargetType the control library also styles REPLACES theirs rather than adding to
/// it. Drop that one word and the tooltip keeps wrapping while losing its background, its border
/// and its shadow - black text on nothing, which nothing else here would notice.
/// </summary>
public sealed class ToolTipGuards
{
    [Fact]
    public void The_tooltip_style_extends_the_librarys_rather_than_replacing_it()
    {
        var ours = WpfHost.On(() => (Style)WpfHost.Resources[typeof(ToolTip)]);

        Assert.NotNull(ours.BasedOn);
    }

    [Fact]
    public void A_tooltip_is_bounded_and_its_content_wraps()
    {
        var style = WpfHost.On(() => (Style)WpfHost.Resources[typeof(ToolTip)]);

        var width = Setter(style, FrameworkElement.MaxWidthProperty);

        Assert.NotNull(width);
        Assert.True((double)width! > 0, "A tooltip with no width limit has nothing to wrap against.");

        // THE TEMPLATE IS THE HALF THAT ACTUALLY WRAPS, and a width without it looks like the width
        // being ignored: a tooltip whose content is a string measures that string on one line and
        // is then clipped. So both are asserted, and separately.
        var template = Setter(style, ContentControl.ContentTemplateProperty) as DataTemplate;

        Assert.NotNull(template);

        var wraps = WpfHost.On(() =>
        {
            var made = template!.LoadContent() as TextBlock;

            return made?.TextWrapping;
        });

        Assert.Equal(TextWrapping.Wrap, wraps);
    }

    /// <summary>
    /// A placeholder arms the tooltip and no binding fills it - both halves asserted.
    ///
    /// <b>The second half is the one worth having.</b> Putting the binding back would work, look
    /// identical and cost a live binding on every TextBlock of every realised row again - about
    /// twenty of the forty-six a row carried with twenty columns on, measured with
    /// tools/gui-probe/row-cost.ps1. Nothing on screen would say so.
    ///
    /// The placeholder has to be there AND has to be non-empty: ToolTipService ignores an element
    /// whose tooltip is null or empty, so an over-tidy cleanup of that space would silently stop
    /// every cell in the list from ever offering its text again.
    /// </summary>
    [Fact]
    public void A_cell_arms_its_tooltip_with_a_placeholder_rather_than_a_binding_on_every_cell()
    {
        var style = WpfHost.On(() => (Style)WpfHost.Resources["CellText"]);

        var tip = Setter(style, FrameworkElement.ToolTipProperty);

        Assert.NotNull(tip);

        Assert.False(
            tip is System.Windows.Data.BindingBase,
            "The tooltip is bound on every cell again, which is what CellTips replaced.");

        Assert.False(
            string.IsNullOrEmpty(tip as string),
            "The placeholder is empty, so ToolTipService will never open and never ask CellTips.");
    }

    /// <summary>
    /// A tooltip is offered for text that gave way, and refused for text that did not.
    ///
    /// <b>This is the behaviour half of `Cells.xaml`'s own rule</b> - the tooltip exists because
    /// text that gives way has to be gettable, so one over a fully visible word repeats what is
    /// already on screen and covers the row under it.
    ///
    /// The decision is asked directly rather than through the event, because ToolTipEventArgs has
    /// no public constructor and cannot be raised from a test.
    /// </summary>
    [Fact]
    public void Text_that_fits_is_offered_nothing_and_text_that_gives_way_is_offered_all_of_it()
    {
        const string Long = "A service display name far too long for the room it was given here";

        var (fits, gives) = WpfHost.On(() =>
            (CellTips.TipFor(ToolTipGuards.Laid("Stopped", 400)),
             CellTips.TipFor(ToolTipGuards.Laid(Long, 40))));

        Assert.Null(fits);
        Assert.Equal(Long, gives);
    }

    /// <summary>A cell with nothing in it offers nothing, rather than an empty tooltip.</summary>
    [Fact]
    public void An_empty_cell_is_offered_nothing()
    {
        var tip = WpfHost.On(() => CellTips.TipFor(ToolTipGuards.Laid(string.Empty, 40)));

        Assert.Null(tip);
    }

    /// <summary>A TextBlock measured and arranged into a box of a known width, as a cell is.</summary>
    private static TextBlock Laid(string text, double width)
    {
        var cell = new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis };

        cell.Measure(new Size(width, double.PositiveInfinity));
        cell.Arrange(new Rect(0, 0, width, cell.DesiredSize.Height));

        return cell;
    }

    private static object? Setter(Style style, DependencyProperty property) =>
        style.Setters.OfType<Setter>().LastOrDefault(setter => setter.Property == property)?.Value;
}
