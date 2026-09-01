using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Xunit;

namespace Bws.Gui.Tests;

/// <summary>
/// What a filter chip looks like, now that this product draws one itself.
///
/// <b>WHY THERE IS ANYTHING TO GUARD HERE, AND IT IS A RISK THIS SESSION CREATED ON PURPOSE.</b>
/// Until 2026-09-01 the chip was BasedOn WPF UI's ToggleButton and overrode three brushes, so every
/// state it has - hover, pressed, checked, disabled, focus - was the library's problem. It was also
/// therefore drawn as the same rounded rectangle, at the same 47 pixels, as the action button under
/// it that opens a plan to stop a service. Measured off the automation tree: chip 47 tall, button
/// 48.
///
/// <b>`docs/11` complaint 14 is that fault one control over, and its rule is what was followed:</b>
/// when the look carries the wrong MEANING, the repair is a whole visual family. So the template is
/// written out - which is the only way to reach a corner radius the library resolves from its own
/// resource - and the cost is that every state is now this product's job.
///
/// <b>THAT COST IS EXACTLY WHAT THESE GUARDS HOLD.</b> A state dropped from the template is
/// invisible in every other way: the build is green, the window draws, the chip works, and one of
/// the four ways it answers a person simply stops happening. Nothing else in this suite would say
/// so, because nothing else renders a chip under a pointer.
/// </summary>
public sealed class ChipGuards
{
    /// <summary>
    /// Every state the chip took over from the library is still drawn.
    ///
    /// <b>Asked of the TEMPLATE rather than of a rendered pixel</b>, and the boundary is worth
    /// naming: hover and pressed need a pointer, disabled needs a real control tree, and neither
    /// exists in a test process - <c>tools/gui-probe/check.ps1</c> is the instrument for those and
    /// it needs a person. What a test CAN say is that the trigger is there at all, which is the
    /// failure this whole rewrite risks.
    /// </summary>
    [Fact]
    public void A_chip_keeps_every_state_it_took_over_from_the_library()
    {
        var template = ChipTemplate();

        var single = WpfHost.On(() => template.Triggers.OfType<Trigger>()
            .Select(trigger => trigger.Property.Name)
            .ToList());

        Assert.Contains(nameof(UIElement.IsMouseOver), single);
        Assert.Contains(nameof(ToggleButton.IsChecked), single);
        Assert.Contains(nameof(UIElement.IsEnabled), single);

        // AND THE PAIR OF THEM TOGETHER, which is the state a single trigger cannot express: a chip
        // that is already on, under the pointer. Without it the loudest control in the row is the
        // one place in this window that does not answer a hand moving over it.
        var both = WpfHost.On(() => template.Triggers.OfType<MultiTrigger>()
            .Any(trigger => trigger.Conditions.Count == 2));

        Assert.True(
            both,
            "The chip template has no MultiTrigger, so a chip that is already on does not answer "
            + "the pointer at all. Owning a template means owning every state it has.");
    }

    /// <summary>
    /// A chip that is on is drawn in the colour this window uses for "this one is picked".
    ///
    /// <b>The claim the style this replaced already made, kept across the rewrite.</b> WPF UI marks
    /// checked with a fill a shade lighter than not-checked against a window that is already dark,
    /// which is `docs/11` complaint 1: the one state a row of filters exists to show, shown in the
    /// least visible difference there is.
    /// </summary>
    [Fact]
    public void A_chip_that_is_on_is_painted_in_the_colour_that_means_picked()
    {
        var template = ChipTemplate();

        var painted = WpfHost.On(() =>
        {
            _ = WpfHost.Resources;

            var on = template.Triggers.OfType<Trigger>()
                .Single(trigger => trigger.Property == ToggleButton.IsCheckedProperty);

            return on.Setters.OfType<Setter>()
                .Where(setter => setter.Property == Border.BackgroundProperty)
                .Select(setter => setter.Value)
                .Single();
        });

        Assert.Equal(Declared("SurfaceSelected"), Colour(painted));
    }

    /// <summary>
    /// The even claim, and without the one above it this pair is worth much less: a chip that is
    /// OFF is not painted in that colour either, so the guard above measures a difference rather
    /// than a constant. Backlog 128 asks for this shape by name.
    /// </summary>
    [Fact]
    public void A_chip_that_is_off_is_not_painted_in_it()
    {
        var resting = WpfHost.On(() =>
        {
            _ = WpfHost.Resources;

            return ((Border)ChipTemplate().LoadContent()).Background;
        });

        Assert.NotEqual(Declared("SurfaceSelected"), Colour(resting));
    }

    /// <summary>
    /// The chip is a pill, which is the signal that carries most of the separation from the button
    /// beside it - and the only one of the three that cannot be reached with a setter, because the
    /// library resolves its corner from a resource of its own.
    /// </summary>
    [Fact]
    public void A_chip_is_rounded_far_enough_to_read_as_a_pill_rather_than_a_button()
    {
        var corner = WpfHost.On(() =>
        {
            _ = WpfHost.Resources;

            return ((Border)ChipTemplate().LoadContent()).CornerRadius;
        });

        // Half the tallest chip this window draws, which is what makes a rounded rectangle a pill.
        // A number rather than a comparison with the action button's corner: that one belongs to
        // the library and is resolved from a resource this test has no name for.
        Assert.True(
            corner.TopLeft >= 12,
            $"The chip's corner is {corner.TopLeft}, which draws a rounded rectangle rather than a "
            + "pill - the same silhouette as the action button beside it, which is what this "
            + "family exists to stop being.");
    }

    private static ControlTemplate ChipTemplate() => WpfHost.On(() =>
    {
        var style = (Style)WpfHost.Resources["FilterChip"];

        return (ControlTemplate)style.Setters.OfType<Setter>()
            .Single(setter => setter.Property == Control.TemplateProperty)
            .Value;
    });

    /// <summary>The colour a name in the theme stands for, read the same way ContrastGuards reads one.</summary>
    private static Color Declared(string name) =>
        WpfHost.On(() => ((SolidColorBrush)WpfHost.Resources[name]).Color);

    private static Color Colour(object brush) => WpfHost.On(() => ((SolidColorBrush)brush).Color);
}
