using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;

namespace Bws.Gui.ViewModels;

/// <summary>
/// How one sample is built, measured and checked - the factory by target type, the reasons a
/// component cannot stand on its own, the sizes read from the theme, and the pixel check.
///
/// Its own file since 2026-09-16, split out of Catalogue.cs at the length ceiling. What is here
/// is true of every KIND of sample - a styled control, a template over a specimen, a view over a
/// model - which is what made it a seam rather than a cut.
/// </summary>
public static partial class Catalogue
{
    /// <summary>
    /// The three sizes the sheet is drawn to, read from the theme once and carried through the
    /// building of every sample.
    ///
    /// <b>Read from the theme rather than written here a second time.</b> The first draft of the
    /// pixel check below carried its own 420 and 320 - the same two numbers Themes/Catalogue.xaml
    /// declares as WidthCatalogueExtreme and HeightCatalogueTall - and GUI rule 12 is about exactly
    /// that pair: a dimension written twice is a dimension that will disagree with itself after
    /// the next change to either.
    /// </summary>
    /// <param name="RowCeiling">The row's height ceiling - a sample past it is marked tall.</param>
    /// <param name="TallCeiling">The taller ceiling, which also bounds how tall a sample is rendered to be looked at.</param>
    /// <param name="Widest">The widest column, which bounds how wide a sample is rendered.</param>
    /// <param name="ViewWidth">How wide a view is drawn - the narrowest window the product draws it in.</param>
    private sealed record Limits(double RowCeiling, double TallCeiling, double Widest, double ViewWidth)
    {
        internal static Limits Of(ResourceDictionary resources) => new(
            Number(resources, "HeightCatalogueSample"),
            Number(resources, "HeightCatalogueTall"),
            Number(resources, "WidthCatalogueExtreme"),
            Number(resources, "WidthCatalogueView"));

        // Infinity rather than a default of this file's own, so a theme that stops declaring a
        // limit stops limiting rather than quietly limiting to a number nobody can find.
        private static double Number(ResourceDictionary resources, string key) =>
            resources[key] is double number ? number : double.PositiveInfinity;
    }

    /// <summary>
    /// Why a component cannot stand on its own, where that is the case.
    ///
    /// <b>Named one by one rather than caught by a rule</b>, because each of these has a different
    /// reason and a reader deciding whether the omission matters needs the reason rather than the
    /// category. All four are parts of the list, which the window itself shows and
    /// <c>tools/gui-probe/screens.ps1</c> photographs.
    /// </summary>
    private static string? WhyNot(string target) => target switch
    {
        nameof(DataGrid) => "the list itself. It is on every screenshot of this window",
        nameof(DataGridRow) => "only exists inside the list, and a row on its own has no columns",
        nameof(DataGridCell) => "only exists inside a row, which only exists inside the list",
        nameof(ToolTip) => "only appears over something. Rest the pointer on a sample above",

        // A focus ring. It has no control of its own - it is drawn AROUND whichever control has
        // the keyboard, which is why this screen asks the reader to press Tab rather than
        // pretending to show one.
        "IFrameworkInputElement" => "a focus ring. Press Tab on this sheet and watch it appear",

        // A style with no target type at all. Not seen in this product on 2026-09-10, and left
        // here rather than left to throw: a style that targets nothing is a style nothing can
        // wear, and the sheet should say that rather than fall over.
        "-" => "a style with no target type, so nothing can wear it",

        _ => null
    };

    /// <summary>
    /// A control of the given type, wearing the given style.
    ///
    /// <b>A table rather than reflection</b> - see the note at the top of this file about
    /// <c>LayeringGuards</c>. Twelve entries cover fifty two styles, and a target type that is not
    /// here is a red test rather than a missing row.
    /// </summary>
    private static Sample Make(string target, Style style, string text, bool enabled, Limits limits)
    {
        // AN INLINE RATHER THAN AN ELEMENT, since 2026-09-15, when the notice line got a link in
        // it. A Hyperlink cannot stand on a sheet by itself - it lives inside text - so the sample
        // is a TextBlock wearing nothing of its own, holding the one styled link. Disabled goes on
        // the TextBlock, and the link inherits it, which is how the product disables it too.
        if (target == nameof(System.Windows.Documents.Hyperlink))
        {
            var link = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(text)) { Style = style };

            return Sample.Of(new TextBlock(link) { TextWrapping = TextWrapping.Wrap, IsEnabled = enabled });
        }

        // A RUN, since 2026-09-24, when an entry's name in the plan panel got a style of its own.
        // The same reason as the link above: it lives inside text and cannot stand alone. A run
        // has no disabled state to show, so that column says so rather than drawing it twice.
        if (target == nameof(System.Windows.Documents.Run))
        {
            return enabled
                ? Sample.Of(new TextBlock(new System.Windows.Documents.Run(text) { Style = style }) { TextWrapping = TextWrapping.Wrap })
                : Sample.None(NoSuchState);
        }

        FrameworkElement? element = target switch
        {
            nameof(TextBlock) => new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
            nameof(Button) => new Button { Content = text },
            nameof(ToggleButton) => new ToggleButton { Content = text },
            nameof(CheckBox) => new CheckBox { Content = text },
            nameof(TextBox) => new TextBox { Text = text },
            nameof(MenuItem) => new MenuItem { Header = text },
            nameof(DataGridColumnHeader) => new DataGridColumnHeader { Content = text },

            // Added 2026-09-10 because the guard asked for it, on the catalogue's own sample box.
            // That is the mechanism working on the day it was written: a style over a control type
            // this table did not know reddened the build rather than printing a row saying nothing
            // could build it.
            nameof(ContentControl) => new ContentControl { Content = text },
            nameof(Border) => new Border { Child = new TextBlock { Text = text } },

            // The list under the search box, 2026-09-15 - the guard asked, as it did for the box
            // above. Two rows of the sample text, so that the list's own scrolling and the item's
            // states are both something to look at rather than one line.
            nameof(ListBox) => new ListBox { ItemsSource = new[] { text, text } },
            nameof(ListBoxItem) => new ListBoxItem { Content = text },
            nameof(Ellipse) => new Ellipse(),
            nameof(Path) => new Path(),
            nameof(Thumb) => new Thumb(),
            nameof(ScrollBar) => new ScrollBar(),
            _ => null
        };

        if (element is null)
        {
            return Sample.None($"nothing here knows how to build a {target} to look at");
        }

        element.Style = style;

        // Asked of the element rather than of the type, because IsEnabled on something that draws
        // no disabled state is a promise this screen would be making on the style's behalf.
        if (!enabled)
        {
            if (element is not Control)
            {
                return Sample.None(NoSuchState);
            }

            element.IsEnabled = false;
        }

        return Sized(element, limits);
    }

    /// <summary>
    /// A built element, measured, checked for a pixel, and marked tall where it is - or the
    /// sentence saying why it is not on the sheet. Shared by every kind of sample: a styled
    /// control, a template over a specimen, a view over a model.
    ///
    /// MEASURED, BECAUSE AN EMPTY CELL IS A CLAIM ABOUT THE PRODUCT. Some styles describe how a
    /// shape looks and leave WHAT it is to wherever it is used - FilterDisclosure is a Path
    /// whose geometry comes from the control template around it, so on its own it is nothing at
    /// all. The first version put those on the sheet as blank cells, which reads as a component
    /// that draws nothing rather than as one that cannot stand alone.
    ///
    /// Asked of WPF rather than of the style: measuring is what the layout pass does anyway,
    /// and a setter chain walked by hand would be this file guessing at BasedOn.
    /// </summary>
    private static Sample Sized(FrameworkElement element, Limits limits)
    {
        try
        {
            element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            if (element.DesiredSize.Width <= 0 || element.DesiredSize.Height <= 0)
            {
                return Sample.None("nothing on its own - its shape or size comes from where it is used");
            }

            // A SIZE IS NOT A PICTURE, and the sheet shipped three blank cells for six days on
            // the strength of one - AgainstMark, StartMark and FilterDisclosure all measure to
            // their token size and draw nothing: two are hidden until a row's state shows them,
            // one takes its colour from the button around it. Asked of the pixels rather than of
            // the style, because the style is exactly what said "this has a size". GUI rule 10.
            if (Paints(element, limits) == false)
            {
                return Sample.None("draws nothing on its own - its colour, shape or visibility comes from where it is used");
            }

            return Sample.Of(element, tall: element.DesiredSize.Height > limits.RowCeiling);
        }
        catch (InvalidOperationException)
        {
            // A control that will not measure outside a tree is still worth showing: the sheet
            // puts it in a real window, where the layout pass will measure it properly. Swallowed
            // deliberately and narrowly - this is a question being asked early, not work.
        }

        return Sample.Of(element);
    }

    /// <summary>
    /// Whether a measured element puts a single pixel on a bitmap - true, false, or null when the
    /// question could not be asked.
    ///
    /// <b>One pixel with any opacity, not two colours.</b> A border on a panel of its own colour
    /// holding text is two colours and paints - a scrim is dozens and paints - a path with no
    /// geometry is none. Alpha separates the last from the rest without a guess about backgrounds,
    /// which a colour count would have to make.
    ///
    /// <b>Bounded, because this runs for every sample when the sheet is built.</b> The render is
    /// capped at the sheet's own extreme column and taller ceiling, so a component that measures
    /// to a screen is drawn once at that size and not at whatever it asked for.
    ///
    /// Null rather than false on a render that throws: an element that will not render outside a
    /// tree is not an element that draws nothing, and turning "could not ask" into "nothing" would
    /// hide a component behind a sentence about itself - the failure this method exists to stop.
    /// </summary>
    private static bool? Paints(FrameworkElement element, Limits limits)
    {
        var width = (int)Math.Ceiling(Math.Min(element.DesiredSize.Width, limits.Widest));
        var height = (int)Math.Ceiling(Math.Min(element.DesiredSize.Height, limits.TallCeiling));

        if (width <= 0 || height <= 0)
        {
            return false;
        }

        try
        {
            element.Arrange(new Rect(0, 0, width, height));

            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);

            bitmap.Render(element);

            var stride = width * 4;
            var pixels = new byte[stride * height];

            bitmap.CopyPixels(pixels, stride, 0);

            // The alpha byte of every pixel, which in Pbgra32 is the fourth.
            for (var at = 3; at < pixels.Length; at += 4)
            {
                if (pixels[at] != 0)
                {
                    return true;
                }
            }

            return false;
        }
        catch (InvalidOperationException)
        {
            // The two ways a render of an unrooted element says no - the same first exception
            // Sized swallows at Measure, for the same reason. Narrow rather than broad, because
            // BroadCatchGuards is right that anything else here would be a sample silently
            // turned into a sentence about itself. Anything else takes the sheet down, loudly.
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
