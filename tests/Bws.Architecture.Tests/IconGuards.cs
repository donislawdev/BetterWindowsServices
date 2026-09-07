// Explicit for the same reason SourceTree.cs says so at the top of itself: this project reads
// files off disk, and the implicit using set is not something to depend on across projects.
using System.IO;

namespace Bws.Architecture.Tests;

/// <summary>
/// The one icon this product ships, and the sizes inside it.
///
/// <b>Why this needs a guard at all, when a missing file already fails the build.</b> It does -
/// MSBuild refuses an <c>ApplicationIcon</c> that points at nothing. What it does not refuse is
/// an icon file that is still there and has quietly lost most of its contents. Regenerate the
/// file with the five sizes Microsoft publishes as a minimum instead of the ten that were chosen,
/// and every build stays green, every test stays green, and the only symptom is that the taskbar
/// looks slightly soft on a machine running at 150 per cent - which nobody will trace back here.
///
/// <b>36 is the size this exists to protect.</b> It is in nobody's published minimum. It is here
/// because the machine this was measured on runs at 150 per cent scaling, where Windows asks for
/// 24 in the title bar, 36 on the taskbar and 48 for a Start pin - and 36 is therefore the size
/// the owner looks at most. It is exactly the entry a well meaning cleanup would drop first.
///
/// <b>What this does not check, so a green run is not read as more than it is.</b> It does not
/// look at a single pixel. An .ico full of the right number of blank squares passes every
/// assertion here. What the pixels look like is a question for a person and for
/// <c>tools/icon/bean.ps1</c>, and the answer of the day it was drawn is in
/// <c>artifacts/icon/</c>. Nor does it prove the built executable carries the icon -
/// <c>tools/gui-probe/window-icon.ps1</c> asks the running window that, and cannot be a unit
/// test because it needs a window.
/// </summary>
public sealed class IconGuards
{
    /// <summary>
    /// One file for both shipped programs, above them rather than inside either, because two
    /// copies of an icon is two icons that will differ and nothing would report it.
    /// </summary>
    private const string IconPath = "src/bws.ico";

    /// <summary>
    /// Every size the icon is meant to carry. The first five are Microsoft's published floor;
    /// 20, 36, 40, 64 and 96 are the exact pixel sizes Windows asks for at 125, 150, 250 and
    /// 400 per cent scaling, so that Windows never has to resize anything itself.
    /// </summary>
    private static readonly int[] Expected = [16, 20, 24, 32, 36, 40, 48, 64, 96, 256];

    private static string Full() => Path.Combine(SourceTree.Root(), IconPath);

    [Fact]
    public void The_product_icon_is_in_the_repository()
    {
        Assert.True(
            File.Exists(Full()),
            $"There is no icon at '{Full()}'. Both shipped projects point ApplicationIcon at it, "
            + "so this is not decoration - draw it with tools/icon/bean.ps1 and pack it with "
            + "tools/icon/make-ico.ps1.");
    }

    [Fact]
    public void The_product_icon_carries_every_size_windows_asks_for()
    {
        var sizes = Sizes(File.ReadAllBytes(Full()));

        foreach (var wanted in Expected)
        {
            Assert.True(
                sizes.Contains(wanted),
                $"The icon has no {wanted} by {wanted} image. It carries {string.Join(", ", sizes)}. "
                + "Windows asks for an exact size and resizes a larger one when it cannot find it, "
                + "so a missing entry is a blurred icon rather than a broken one - which is why "
                + "nothing else would have told you.");
        }
    }

    [Fact]
    public void Every_image_in_the_product_icon_lies_inside_the_file()
    {
        var bytes = File.ReadAllBytes(Full());
        var count = BitConverter.ToUInt16(bytes, 4);

        for (var i = 0; i < count; i++)
        {
            var entry = 6 + (16 * i);
            var length = BitConverter.ToUInt32(bytes, entry + 8);
            var offset = BitConverter.ToUInt32(bytes, entry + 12);

            // An .ico directory names an offset and a length for each image, and nothing in the
            // format stops those pointing past the end. A file like that loads as far as the
            // directory and then hands whatever follows to a decoder.
            Assert.True(
                offset + length <= (ulong)bytes.LongLength,
                $"Image {i} of the icon claims {length} bytes at offset {offset}, and the file is "
                + $"only {bytes.LongLength} bytes long.");
        }
    }

    [Fact]
    public void Both_shipped_programs_point_at_that_one_icon()
    {
        foreach (var project in new[] { "Bws.Cli", "Bws.Gui" })
        {
            var file = Path.Combine(SourceTree.Root(), "src", project, project + ".csproj");
            var text = File.ReadAllText(file);

            Assert.Contains("<ApplicationIcon>", text, StringComparison.Ordinal);

            // The path is relative to the project, so from src/<project>/ the shared file is one
            // directory up. Asserting on the file NAME rather than the whole element keeps this
            // from breaking on whitespace while still catching a second, private copy.
            Assert.Contains(
                @"..\bws.ico",
                text,
                StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The sizes an .ico says it contains, read out of its directory.
    ///
    /// Width and height are single BYTES in that directory, so 256 does not fit and is written
    /// as zero - a convention, not a corruption, and the reason no .ico can hold anything larger.
    /// Reading it as literal zero is how a 256 entry goes missing without anybody noticing.
    /// </summary>
    private static List<int> Sizes(byte[] bytes)
    {
        Assert.True(bytes.Length > 6, "The icon file is too short to hold even a directory.");
        Assert.Equal(1, BitConverter.ToUInt16(bytes, 2));

        var count = BitConverter.ToUInt16(bytes, 4);
        var sizes = new List<int>(count);

        for (var i = 0; i < count; i++)
        {
            var width = bytes[6 + (16 * i)];
            sizes.Add(width == 0 ? 256 : width);
        }

        return sizes;
    }
}
