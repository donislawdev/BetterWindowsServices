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
    /// The button at the foot of a plan is disabled exactly when the session cannot carry anything out, which is
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
    /// <b>THE OTHER HALF OF THAT TARGET IS GUARDED TOO SINCE 2026-09-02, BY THE TEST UNDER THIS
    /// ONE.</b> What stood here said it could not be: four attempts at a floor under the thumb's
    /// LENGTH came back at 19 device pixels each, and the sentence written from that - Track
    /// overwrites anything it is given - became a recorded defeat in `docs/10` trap 17. It was
    /// wrong about WHERE rather than about WHETHER, and the test below is what says so.
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

    /// <summary>
    /// The thumb is long enough to catch, both ways up, on a list the size of a real machine.
    ///
    /// <b>ARRANGED RATHER THAN INSPECTED, AND THAT IS THE WHOLE VALUE OF IT.</b> The length is not
    /// a property anybody sets: Track works it out while arranging, from the viewport against the
    /// range, and floors it at half of what <c>TryFindResource</c> hands back for the scrollbar
    /// button. So a test reading setters would have agreed with all five attempts that failed. This
    /// one builds the bar, gives it the numbers a real list gives it - 321 of range against 15 on
    /// screen, over a track of 360 - and reads what came out.
    ///
    /// <b>THE FIFTH ATTEMPT PASSED A TEST LIKE THIS ONE AND CHANGED NOTHING ON SCREEN, which is
    /// the reason the second assertion exists.</b> MinHeight on the Thumb reaches the ARRANGED
    /// size, so an earlier version of this test read 44 while a pixel scan of the real list read
    /// 13 logical. The number that decides is the resource, and tying it to the one this product
    /// declares is what stops the two drifting apart in silence.
    ///
    /// <b>Both orientations, because one style dresses both bars and the key is a different one
    /// for each.</b> A floor that reached the standing bar and nothing else would leave the
    /// sideways one exactly as it was, and the list has scrolled sideways since 2026-08-12.
    /// </summary>
    [Theory]
    [InlineData(Orientation.Vertical)]
    [InlineData(Orientation.Horizontal)]
    public void The_scrollbar_thumb_is_long_enough_to_catch(Orientation way)
    {
        var (along, least) = WpfHost.On(() =>
        {
            var bar = new ScrollBar
            {
                Style = (Style)WpfHost.Resources[typeof(ScrollBar)],
                Orientation = way,
                Minimum = 0,
                Maximum = 321,
                Value = 0,
                ViewportSize = 15
            };

            var box = way == Orientation.Vertical ? new Size(12, 360) : new Size(360, 12);

            bar.Measure(box);
            bar.Arrange(new Rect(new Point(0, 0), box));
            bar.UpdateLayout();

            var thumb = Inside<Thumb>(bar);

            return (way == Orientation.Vertical ? thumb.ActualHeight : thumb.ActualWidth,
                (double)WpfHost.Resources["LengthScrollThumbLeast"]);
        });

        // The number this replaced, measured the same way: 16.1 logical, which is the 20 device
        // pixels a scan of the real list kept answering.
        Assert.True(
            along >= least,
            $"The {way} thumb arranged {along} long against a floor of {least}. Bare, the same "
            + "bar answers 16.1 - the proportion of 15 on screen to 321 of range.");

        var key = way == Orientation.Vertical
            ? SystemParameters.VerticalScrollBarButtonHeightKey
            : SystemParameters.HorizontalScrollBarButtonWidthKey;

        var declared = WpfHost.On(() => (double)WpfHost.Resources[key]);

        Assert.True(
            Math.Floor(declared / 2) == least,
            $"Track floors the thumb at half of {key}, which this product declares as {declared}, "
            + $"so the floor it actually gets is {Math.Floor(declared / 2)} rather than the {least} "
            + "LengthScrollThumbLeast says. Those two numbers are one decision written twice.");
    }

    private static T Inside<T>(DependencyObject from) where T : DependencyObject =>
        Under<T>(from) ?? throw new InvalidOperationException(
            $"No {typeof(T).Name} under {from.GetType().Name}, so there is nothing to measure.");

    /// <summary>
    /// Depth first, and it goes on to the NEXT SIBLING when a branch has none - which the first
    /// version of this did not, so a thumb under the second child would have read as absent.
    /// </summary>
    private static T? Under<T>(DependencyObject from) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(from); i++)
        {
            var child = VisualTreeHelper.GetChild(from, i);

            if (child is T wanted)
            {
                return wanted;
            }

            if (Under<T>(child) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
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
