// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Bws.Gui.Tests;

/// <summary>
/// One part of a window laid out at a fixed size and drawn off screen, the way the screen draws it,
/// kept as its pixels - for a guard that has to settle a question only a pixel can settle.
///
/// <b>Three copies of this stood in three guards until the review of PR #11</b> - WaitingBoxGuards,
/// ForcedStopLayoutGuards and AnswerLineGuards, each laying out, rendering, saving a picture and
/// counting one colour by hand. The third copy put a test method among those standing near the
/// ceiling of length, the shape guard's crowd went from eleven to twelve, and CI went red on it.
/// GUI rule 2 says the same thing in its own words: the third time is a component.
///
/// <b>Exact colours, the way all three copies counted them.</b> A pixel blended with its neighbour
/// on a smoothed edge is not counted, so a caller asks for more than a handful of pixels rather
/// than for a number it measured once.
/// </summary>
internal sealed class Drawn
{
    private readonly FrameworkElement _host;
    private readonly RenderTargetBitmap _bitmap;
    private readonly byte[] _pixels;

    private Drawn(FrameworkElement host, RenderTargetBitmap bitmap, byte[] pixels, int width, int height)
    {
        _host = host;
        _bitmap = bitmap;
        _pixels = pixels;
        Width = width;
        Height = height;
    }

    // Kept as plain numbers rather than read off the bitmap, which belongs to the interface thread
    // and throws when this thread asks it for its size.
    private int Width { get; }

    private int Height { get; }

    /// <summary>
    /// Lays the element out at the size given and draws it at 96 DPI, so one pixel is one unit and
    /// a rectangle from <see cref="Around"/> can be read straight off the picture.
    /// </summary>
    internal static Drawn Of(FrameworkElement host, int width, int height)
    {
        var bitmap = WpfHost.On(() =>
        {
            host.Measure(new Size(width, height));
            host.Arrange(new Rect(0, 0, width, height));
            host.UpdateLayout();

            var drawn = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);

            drawn.Render(host);

            return drawn;
        });

        var pixels = new byte[width * height * 4];

        WpfHost.On(() => bitmap.CopyPixels(pixels, width * 4, 0));

        return new Drawn(host, bitmap, pixels, width, height);
    }

    /// <summary>
    /// Where a part of the drawn element stands on the picture, rounded outwards to whole pixels so
    /// an edge drawn on a fraction is still inside. Empty when the part was never laid out.
    /// </summary>
    internal Int32Rect Around(FrameworkElement part) => WpfHost.On(() =>
    {
        var origin = part.TranslatePoint(new Point(0, 0), _host);
        var left = (int)Math.Floor(origin.X);
        var top = (int)Math.Floor(origin.Y);

        return new Int32Rect(
            left,
            top,
            Math.Max(0, (int)Math.Ceiling(origin.X + part.ActualWidth) - left),
            Math.Max(0, (int)Math.Ceiling(origin.Y + part.ActualHeight) - top));
    });

    /// <summary>How many pixels of the whole picture are exactly this colour.</summary>
    internal int Count(Color colour) => Count(colour, new Int32Rect(0, 0, Width, Height));

    /// <summary>
    /// How many pixels inside the rectangle are exactly this colour - clipped to the picture, so a
    /// part hanging over its edge is counted where it can be seen and nowhere else.
    /// </summary>
    internal int Count(Color colour, Int32Rect within)
    {
        var found = 0;

        for (var y = Math.Max(0, within.Y); y < Math.Min(Height, within.Y + within.Height); y++)
        {
            for (var x = Math.Max(0, within.X); x < Math.Min(Width, within.X + within.Width); x++)
            {
                var at = (y * Width + x) * 4;

                if (_pixels[at] == colour.B && _pixels[at + 1] == colour.G && _pixels[at + 2] == colour.R)
                {
                    found++;
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Leaves the picture in artifacts/gui under the name given and answers where, so a red guard
    /// comes with something to look at rather than only a number.
    /// </summary>
    internal string Save(string name)
    {
        var into = Path.Combine(SourceTree.Root(), "artifacts", "gui");

        Directory.CreateDirectory(into);

        var file = Path.Combine(into, name);

        WpfHost.On(() =>
        {
            var png = new PngBitmapEncoder();

            png.Frames.Add(BitmapFrame.Create(_bitmap));

            using var stream = File.Create(file);

            png.Save(stream);
        });

        return file;
    }
}
