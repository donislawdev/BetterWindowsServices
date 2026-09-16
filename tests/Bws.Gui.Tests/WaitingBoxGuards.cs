using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;
using static Bws.Gui.Tests.PlanFixture;

namespace Bws.Gui.Tests;

/// <summary>
/// The box of seconds as a thing on a window: what it looks like while it is wrong, and what it
/// costs the sheet to be able to say so.
///
/// <b><see cref="WaitingGuards"/> holds what the model decides. This holds the three things only a
/// laid-out window can answer</b>, and every one of them has failed silently in this project
/// before: that a colour set through a style on somebody else's template reaches a pixel
/// (`docs/11`, three slices lost to one that did not), that a binding is alive rather than written
/// (`docs/08` position 19), and that a sentence appearing under a row moves nothing (GUI rule 3).
/// </summary>
public sealed class WaitingBoxGuards
{
    /// <summary>
    /// The edge of the box wears the problem colour while the box is wrong, and does not before -
    /// counted off rendered pixels INSIDE THE BOX'S OWN RECTANGLE, because the sentence under the
    /// row wears the same colour and a count over the whole sheet would pass on the sentence alone.
    ///
    /// <b>This is the assertion that the trigger on Validation.HasError reaches the library's
    /// template.</b> WPF UI draws the box, not this product, and whether its border honours
    /// BorderBrush is a fact about its template that only a pixel can settle - trap 4 of `docs/10`
    /// is the same question asked of a different property.
    /// </summary>
    [Fact]
    public async Task The_box_wears_the_problem_colour_on_its_edge_while_it_is_wrong_and_not_before()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        var fine = RedInsideTheBox(window);

        WpfHost.On(() => panel.WaitingText = "abc");
        WpfHost.Settled();

        var wrong = RedInsideTheBox(window);

        Assert.True(
            WpfHost.On(() => Validation.GetHasError(window.PlanPanel.Footer.WaitingBox)),
            "the binding on the box never heard that the text is wrong - INotifyDataErrorInfo is "
            + "not reaching Validation.HasError, so no style can colour the edge");

        Assert.Equal(0, fine);
        Assert.True(
            wrong > 20,
            $"only {wrong} pixels of the problem colour were painted inside the box while it holds "
            + "'abc'. The style sets BorderBrush and the library's template is not drawing it - the "
            + "colour is set correctly and reaches no pixel, which is the fault this project has "
            + "paid for three times.");

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The sentence under the row, on the window, reads what the model says - and the row is
    /// reserved: the footer is exactly as tall with the sentence in it as without, so the button
    /// somebody is aiming at does not move by a pixel when a wrong number is typed.
    ///
    /// <b>Measured off a real layout pass with a floor under it</b> - `docs/10` trap 19 - because
    /// two zeros are equal too.
    /// </summary>
    [Fact]
    public async Task The_sentence_under_the_row_reads_the_problem_and_the_row_was_already_reserved_for_it()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        var (fineHeight, fineText) = FooterMeasured(window);

        WpfHost.On(() => panel.WaitingText = string.Empty);
        WpfHost.Settled();

        var (wrongHeight, wrongText) = FooterMeasured(window);

        Assert.True(fineHeight > 40, $"the footer measured {fineHeight} high, which is not a laid out footer");
        Assert.Equal(string.Empty, fineText);
        Assert.Equal(WpfHost.On(() => panel.WaitingProblem), wrongText);
        Assert.NotEqual(string.Empty, wrongText);
        Assert.Equal(fineHeight, wrongHeight);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The width of the box is the token, not a number in the view - backlog 352 - and the token
    /// reaches the control.
    /// </summary>
    [Fact]
    public async Task The_box_is_as_wide_as_the_theme_says()
    {
        var window = await Ready();

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        var width = WpfHost.On(() => window.PlanPanel.Footer.WaitingBox.Width);

        Assert.Equal((double)WpfHost.Resources["WidthWaitingBox"], width);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// A click on the box of seconds lands ON the box of seconds - asked of the visual tree with
    /// the framework's own hit test, at the centre of the box, on a laid-out sheet.
    ///
    /// <b>Found 2026-09-16 by the owner, who could not type into the box on a start or restart
    /// plan, and measured by tools/gui-probe/plan-keys.ps1: a click left the keyboard on the close
    /// button and every key after it changed nothing.</b> The progress line shares the box's grid
    /// cell, is declared after it and so lies over it, and a TextBlock takes the pointer over the
    /// whole of its rectangle whether or not it has any text - so an EMPTY sentence, stretched
    /// across the column, was catching every click meant for the box. Nothing in the markup looks
    /// wrong, the model answers every question correctly, and the box cannot be typed into.
    ///
    /// <b>The hit test rather than a focus call</b>, because a window built here is never shown and
    /// Focus() answers false about everything in one - `docs/10` trap 19 - while hit testing only
    /// needs layout. What is asserted is the geometry the click meets, which is the half that was
    /// wrong.
    /// </summary>
    [Fact]
    public async Task The_pointer_lands_on_the_box_of_seconds_and_not_on_something_lying_over_it()
    {
        var window = await Ready();

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        var (hit, box) = WpfHost.On(() =>
        {
            window.PlanPanel.Measure(new Size(1000, 800));
            window.PlanPanel.Arrange(new Rect(0, 0, 1000, 800));
            window.PlanPanel.UpdateLayout();

            var target = window.PlanPanel.Footer.WaitingBox;
            var centre = target.TranslatePoint(new Point(target.ActualWidth / 2, target.ActualHeight / 2), window.PlanPanel);
            var result = VisualTreeHelper.HitTest(window.PlanPanel, centre);

            return (Describe(result?.VisualHit), IsInside(result?.VisualHit, target));
        });

        Assert.True(
            box,
            $"a click at the centre of the box of seconds lands on {hit}, not on the box - something "
            + "is lying over it in the footer, and nothing typed there can reach it");

        WpfHost.On(window.Close);
    }

    private static bool IsInside(DependencyObject? hit, DependencyObject target)
    {
        for (var at = hit; at is not null; at = VisualTreeHelper.GetParent(at))
        {
            if (ReferenceEquals(at, target))
            {
                return true;
            }
        }

        return false;
    }

    private static string Describe(DependencyObject? hit) => hit switch
    {
        null => "nothing at all",
        FrameworkElement element when !string.IsNullOrEmpty(element.Name) => $"{element.GetType().Name} '{element.Name}'",
        _ => hit.GetType().Name
    };

    private static (double Height, string Problem) FooterMeasured(Bws.Gui.MainWindow window) =>
        WpfHost.On(() =>
        {
            window.PlanPanel.Measure(new Size(1000, 800));
            window.PlanPanel.Arrange(new Rect(0, 0, 1000, 800));
            window.PlanPanel.UpdateLayout();

            return (window.PlanPanel.Footer.DesiredSize.Height, window.PlanPanel.Footer.WaitingProblemLine.Text);
        });

    /// <summary>
    /// How many rendered pixels inside the box's rectangle are exactly MeaningRejected, the one
    /// colour that means "this is wrong" on this sheet.
    ///
    /// <b>The drawing is left behind in artifacts/gui</b>, the way the forcing sheet's is, because
    /// the owner of this project reads screens rather than code - and the wrong state of this box
    /// cannot be photographed from a probe without typing into a live window.
    /// </summary>
    /// <summary>
    /// The wrong box's edge is the style's own error template, on all four sides.
    ///
    /// <b>The owner's remark of 2026-09-16:</b> the box was red on three sides and open along the
    /// bottom. The library's TextBox template draws a second border of its own along the bottom,
    /// on top of the one the style colours, so the style's red never reached that edge. The edge
    /// is the style's error template now, drawn in the adorner layer over both - and the footer
    /// carries a layer of its own, because a window never shown in this host has none.
    ///
    /// <b>What this test can see and what it cannot, measured rather than assumed.</b> It sees
    /// that the footer has a layer, that the framework attached the adorner to the wrong box, and
    /// what the template draws. It cannot see a pixel of it: a validation adorner takes its
    /// Visibility from the adorned element's IsVisible (dotnet/wpf, TemplatedAdorner.NeedsUpdate),
    /// and IsVisible is false for everything in a window without a presentation source - four
    /// versions of this test rendered the sheet and read a collapsed 0x0 adorner out of their own
    /// failure message. The pixel half of the claim is a photograph of a shown window, and the
    /// owner's screen at 150 per cent is where the open edge was seen in the first place.
    /// </summary>
    [Fact]
    public async Task The_wrong_edge_is_the_error_template_on_all_four_sides()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        // LAID OUT BEFORE THE ERROR IS RAISED. An adorner is attached when the error fires, only
        // if a layer exists at that moment, and otherwise waits for Loaded - which never comes to
        // an element of a window that is never shown.
        WpfHost.On(() => LayOut(window.PlanPanel));
        WpfHost.On(() => panel.WaitingText = "abc");
        WpfHost.Settled();

        var (hasLayer, attached, hasTemplate) = WpfHost.On(() =>
        {
            var box = window.PlanPanel.Footer.WaitingBox;
            var layer = AdornerLayer.GetAdornerLayer(box);
            var adorners = layer?.GetAdorners(box) ?? [];

            return (layer is not null, adorners.Length > 0, Validation.GetErrorTemplate(box) is not null);
        });

        Assert.True(hasLayer, "the box has no adorner layer over it - the footer's own AdornerDecorator is gone, so the error template has nowhere to be drawn");
        Assert.True(hasTemplate, "the box has no error template - null draws nothing, which is the three-sided edge the owner photographed");
        Assert.True(attached, "the box is wrong and has a layer over it, and the framework attached no adorner to it - the error template never reached the layer");

        // The four sides, in the problem colour, around the box - read from the markup, because
        // the template refuses to be instantiated outside a control.
        Assert.Equal("BorderAllSides in MeaningRejected around AdornedElementPlaceholder", TheEdge());

        WpfHost.On(window.Close);
    }

    private static void LayOut(UIElement element)
    {
        element.Measure(new Size(1000, 800));
        element.Arrange(new Rect(0, 0, 1000, 800));
        element.UpdateLayout();
    }

    /// <summary>
    /// What the error template draws, read from the markup of the style - because the template
    /// cannot be instantiated here: AdornedElementPlaceholder refuses to exist outside a template
    /// being applied, which is one more thing four versions of this test found out by doing it.
    /// The shape every theme guard in this project reads is the shape read here.
    /// </summary>
    private static string TheEdge()
    {
        var theme = File.ReadAllText(Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "Themes", "Plan.xaml"));
        var style = theme.IndexOf("<Style x:Key=\"WaitingBox\"", StringComparison.Ordinal);
        var setter = style < 0 ? -1 : theme.IndexOf("<Setter Property=\"Validation.ErrorTemplate\">", style, StringComparison.Ordinal);
        var close = setter < 0 ? -1 : theme.IndexOf("</Setter>", setter, StringComparison.Ordinal);

        if (close < 0)
        {
            return "no error template on WaitingBox - null draws nothing, and the framework's own would draw a red not ours";
        }

        var template = theme[setter..close];
        var thickness = Regex.Match(template, @"BorderThickness=""\{StaticResource (\w+)\}""", RegexOptions.None, TimeSpan.FromSeconds(5));
        var brush = Regex.Match(template, @"BorderBrush=""\{StaticResource (\w+)\}""", RegexOptions.None, TimeSpan.FromSeconds(5));
        var wraps = template.Contains("<AdornedElementPlaceholder", StringComparison.Ordinal);

        return $"{(thickness.Success ? thickness.Groups[1].Value : "no thickness")} in {(brush.Success ? brush.Groups[1].Value : "no brush")} around {(wraps ? "AdornedElementPlaceholder" : "nothing")}";
    }

    private static int RedInsideTheBox(Bws.Gui.MainWindow window)
    {
        var (shot, left, top, right, bottom, wrong) = WpfHost.On(() =>
        {
            window.PlanPanel.Measure(new Size(1000, 800));
            window.PlanPanel.Arrange(new Rect(0, 0, 1000, 800));
            window.PlanPanel.UpdateLayout();

            var box = window.PlanPanel.Footer.WaitingBox;
            var origin = box.TranslatePoint(new Point(0, 0), window.PlanPanel);

            var drawn = new RenderTargetBitmap(1000, 800, 96, 96, PixelFormats.Pbgra32);
            drawn.Render(window.PlanPanel);

            return (
                drawn,
                (int)Math.Floor(origin.X),
                (int)Math.Floor(origin.Y),
                (int)Math.Ceiling(origin.X + box.ActualWidth),
                (int)Math.Ceiling(origin.Y + box.ActualHeight),
                Validation.GetHasError(box));
        });

        Assert.True(right > left && bottom > top, "the box has no rectangle, so it was never laid out");

        var into = Path.Combine(SourceTree.Root(), "artifacts", "gui");
        Directory.CreateDirectory(into);

        WpfHost.On(() =>
        {
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(shot));

            using var stream = File.Create(Path.Combine(into, wrong ? "plan-footer-wrong-1000x800.png" : "plan-footer-fine-1000x800.png"));
            png.Save(stream);
        });

        var pixels = new byte[1000 * 800 * 4];
        WpfHost.On(() => shot.CopyPixels(pixels, 1000 * 4, 0));

        var rejected = WpfHost.Declared("MeaningRejected");

        var red = 0;

        for (var y = Math.Max(0, top); y < Math.Min(800, bottom); y++)
        {
            for (var x = Math.Max(0, left); x < Math.Min(1000, right); x++)
            {
                var at = (y * 1000 + x) * 4;

                if (pixels[at] == rejected.B && pixels[at + 1] == rejected.G && pixels[at + 2] == rejected.R)
                {
                    red++;
                }
            }
        }

        return red;
    }
}
