using System.IO;
using System.Windows;
using System.Windows.Controls;
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
