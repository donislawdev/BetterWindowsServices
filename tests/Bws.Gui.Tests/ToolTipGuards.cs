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

    private static object? Setter(Style style, DependencyProperty property) =>
        style.Setters.OfType<Setter>().LastOrDefault(setter => setter.Property == property)?.Value;
}
