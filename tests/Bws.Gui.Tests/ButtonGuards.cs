using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Xunit;

namespace Bws.Gui.Tests;

/// <summary>
/// The button that matters most on a screen, and the bar somebody drags.
///
/// <b>BOTH ARE HERE BECAUSE THIS PRODUCT NOW DRAWS THEM ITSELF, and both took that on for a
/// measured reason rather than for a look.</b> The primary action wore the accent as a local value
/// until 2026-09-01 and went DARK under the pointer, because a value on a Button loses to a setter
/// inside the template the library draws it with. The scrollbar had no style at all, and the thumb
/// it drew measured five by thirteen device pixels against the 24 by 24 WCAG 2.2 SC 2.5.8 asks.
///
/// <b>Owning a template means owning every state, and a state dropped from one is invisible in
/// every other way</b> - green build, window draws, control works, and one of the ways it answers
/// a person stops happening. That is what these hold.
/// </summary>
public sealed class ButtonGuards
{
    /// <summary>
    /// The primary action keeps the three states it took over from the library.
    ///
    /// <b>Asked of the template rather than of a pixel</b>, and the boundary is named: hover and
    /// pressed need a pointer, which a test process does not have. What a test can say is that the
    /// trigger exists at all, which is the failure owning a template risks.
    /// </summary>
    [Fact]
    public void The_primary_action_keeps_every_state_it_took_over_from_the_library()
    {
        var names = WpfHost.On(() => PrimaryTemplate().Triggers.OfType<Trigger>()
            .Select(trigger => trigger.Property.Name)
            .ToList());

        Assert.Contains(nameof(UIElement.IsMouseOver), names);
        Assert.Contains(nameof(ButtonBase.IsPressed), names);
        Assert.Contains(nameof(UIElement.IsEnabled), names);
    }

    /// <summary>
    /// It is drawn in the one colour this window keeps for "press this".
    ///
    /// <b>The whole palette study of 2026-09-01 comes down to this assertion.</b> Before it, the
    /// single saturated colour in the window did three jobs - the picked row, the chip that is on,
    /// and the button off the first screen - so a button meant nothing. A second, brighter blue was
    /// measured and rejected at a ratio of 1.02 against the selection colour, which is the same
    /// lightness. What is left is one colour used once, and this says which.
    /// </summary>
    [Fact]
    public void The_primary_action_wears_the_colour_that_means_press_this()
    {
        var painted = WpfHost.On(() =>
        {
            _ = WpfHost.Resources;

            return ((Border)PrimaryTemplate().LoadContent()).Background;
        });

        Assert.Equal(Declared("SurfacePrimaryAction"), Colour(painted));
    }

    /// <summary>
    /// When it cannot be pressed it gives up the fill entirely rather than dimming it.
    ///
    /// <b>This is the state that matters most in this window and it is not a style preference.</b>
    /// "Carry this out" is disabled exactly when the session cannot carry anything out, which is
    /// the moment somebody most needs telling - and an accent merely a shade darker reads as a
    /// button still waiting to be pressed.
    /// </summary>
    [Fact]
    public void The_primary_action_stops_looking_pressable_when_it_cannot_be_pressed()
    {
        var (fill, ink) = WpfHost.On(() =>
        {
            _ = WpfHost.Resources;

            var off = PrimaryTemplate().Triggers.OfType<Trigger>()
                .Single(trigger => trigger.Property == UIElement.IsEnabledProperty);

            var background = off.Setters.OfType<Setter>()
                .Single(setter => setter.Property == Border.BackgroundProperty).Value;

            var foreground = off.Setters.OfType<Setter>()
                .Single(setter => setter.Property == Control.ForegroundProperty).Value;

            return (background, foreground);
        });

        Assert.Equal(Colors.Transparent, Colour(fill));
        Assert.Equal(Declared("TextSubdued"), Colour(ink));
    }

    /// <summary>
    /// The scrollbar is wide enough to aim at.
    ///
    /// <b>THE OTHER HALF OF THAT TARGET IS NOT GUARDED AND SAYING SO IS THE POINT.</b> Four measured
    /// attempts at a floor under the thumb's LENGTH all came back at the same 19 device pixels,
    /// because Track computes the length while arranging and overwrites anything it is given -
    /// `docs/10` trap 17 and backlog 275. So this asserts the one dimension this product can
    /// actually hold, and does not pretend to the other.
    /// </summary>
    [Fact]
    public void The_scrollbar_is_wide_enough_to_aim_at()
    {
        var (wide, least) = WpfHost.On(() =>
        {
            _ = WpfHost.Resources;

            var style = (Style)WpfHost.Resources[typeof(ScrollBar)];

            var width = style.Setters.OfType<Setter>()
                .Single(setter => setter.Property == FrameworkElement.WidthProperty).Value;

            return ((double)width, (double)WpfHost.Resources["WidthScrollBar"]);
        });

        Assert.Equal(least, wide);

        // The number this replaced, read off a capture of the real window: the library's thumb was
        // five device pixels across, which is about three logical.
        Assert.True(
            wide >= 12,
            $"The scrollbar is {wide} wide. The one this replaced measured about three logical "
            + "pixels across, and WCAG 2.2 SC 2.5.8 asks 24 of a target.");
    }

    private static ControlTemplate PrimaryTemplate() => WpfHost.On(() =>
    {
        var style = (Style)WpfHost.Resources["PrimaryAction"];

        return (ControlTemplate)style.Setters.OfType<Setter>()
            .Single(setter => setter.Property == Control.TemplateProperty)
            .Value;
    });

    private static Color Declared(string name) =>
        WpfHost.On(() => ((SolidColorBrush)WpfHost.Resources[name]).Color);

    private static Color Colour(object brush) => WpfHost.On(() => ((SolidColorBrush)brush).Color);
}
