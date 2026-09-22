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
/// <b>It looks at pixels since 2026-09-22, and the sentence that used to stand here - "it does
/// not look at a single pixel" - is what let the real defect through.</b> The file carried ten
/// sizes, both projects pointed at it, every test was green, and the drawing inside filled 77 per
/// cent of the frame across, 65 per cent down and 32 per cent by area. The owner's own other two
/// programs fill about half by area. He reported it as "the taskbar icon is small" and he was
/// right by about a quarter, linear. Two written sentences said otherwise - one above
/// <c>$Compositions</c> in <c>tools/icon/bean.ps1</c> and one in section 6 of
/// <c>docs/PROJEKT-IKONY-20260907.md</c> - and both were true of the bean's length along its own
/// tilted axis and false of the frame. Nothing guarded either. <c>How_large_the_drawing_reads</c>
/// below is that guard, and <c>tools/icon/ink.ps1</c> is the instrument it was calibrated with.
///
/// <b>What this still does not check, so a green run is not read as more than it is.</b> The ink
/// measurement covers the seven DIB frames, 16 through 48 - which is every size Windows asks for
/// on a machine at up to 250 per cent, including the 36 the owner looks at most - and NOT the
/// three PNG frames at 64, 96 and 256. Decoding PNG here would mean inflating and unfiltering
/// scanlines by hand, and all ten frames are scaled from one master by
/// <c>tools/icon/make-ico.ps1</c>, so a packing that got the small ones right and the large ones
/// wrong is not a failure this format produces. Run <c>tools/icon/ink.ps1</c> to see all ten.
/// Ink is also area rather than legibility - a shape can fill the frame and still be mush at 16 -
/// and that question belongs to the contact sheet and to a person. Nor does any of this prove the
/// BUILT EXECUTABLE carries the icon. Nothing checks that today: there is no probe for it, and
/// the sentence that used to name <c>tools/gui-probe/window-icon.ps1</c> named a file that has
/// never existed in this repository.
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
    /// How much of each frame the drawing actually spans, and how much of it is ink.
    ///
    /// <b>The floors are set below the measurement and above the defect, which is the only way a
    /// floor is worth anything.</b> As drawn on 2026-09-22 the worst frame measures 86.7 per cent
    /// across, 86.7 down and 48.2 by area. As it stood the day before, the BEST frame measured
    /// 81.2, 68.8 and 36.7. Every floor here sits in the gap, so redrawing the bean a little
    /// smaller stays green and redrawing it as small as it was does not.
    ///
    /// <b>And a ceiling, because the sweep that chose these numbers produced a candidate that hit
    /// 100 per cent.</b> A drawing flush against the frame has been clipped or is about to be,
    /// and it reads as a shape with its end cut off rather than as a large shape. 98 per cent
    /// leaves the one pixel a 16 by 16 frame can spare.
    /// </summary>
    [Fact]
    public void How_large_the_drawing_reads_in_every_frame_windows_draws_small()
    {
        var bytes = File.ReadAllBytes(Full());
        var count = BitConverter.ToUInt16(bytes, 4);
        var measured = 0;

        for (var i = 0; i < count; i++)
        {
            var entry = 6 + (16 * i);
            var size = bytes[entry] == 0 ? 256 : bytes[entry];
            var offset = BitConverter.ToUInt32(bytes, entry + 12);

            // A PNG frame opens with the eight byte signature. Everything else in an .ico is a
            // DIB, which opens with a 40 byte header length. Sniffing the bytes rather than
            // assuming a size threshold means this keeps working if make-ico.ps1 is pointed at a
            // different split.
            if (bytes[offset] == 0x89 && bytes[offset + 1] == 0x50)
            {
                continue;
            }

            var ink = MeasureInk(bytes, offset, size);
            measured++;

            Assert.True(
                ink.BoxWidthPercent >= 80 && ink.BoxHeightPercent >= 80,
                $"The drawing in the {size} by {size} frame spans {ink.BoxWidthPercent:N1} per cent "
                + $"of it across and {ink.BoxHeightPercent:N1} per cent down, and the floor is 80. "
                + "An icon that leaves a third of its frame empty reads smaller than everything "
                + "around it on the taskbar, and no other test here would notice. Redraw it with "
                + "tools/icon/bean.ps1 and measure with tools/icon/ink.ps1.");

            Assert.True(
                ink.InkPercent >= 42,
                $"The drawing in the {size} by {size} frame covers {ink.InkPercent:N1} per cent of "
                + "it, and the floor is 42. The two other programs this owner ships cover about "
                + "50. See tools/icon/ink.ps1.");

            Assert.True(
                ink.BoxWidthPercent <= 98 && ink.BoxHeightPercent <= 98,
                $"The drawing in the {size} by {size} frame spans {ink.BoxWidthPercent:N1} by "
                + $"{ink.BoxHeightPercent:N1} per cent of it, which is flush against the edge. "
                + "A clipped icon reads as a cut off shape rather than as a large one.");
        }

        // A file whose frames were all PNG would pass every assertion above by running none of
        // them, which is the shape of green this repository has paid for before.
        Assert.True(
            measured >= 7,
            $"Only {measured} frames were measured and there should be at least seven - 16, 20, "
            + "24, 32, 36, 40 and 48 are stored as DIBs by tools/icon/make-ico.ps1. Fewer means "
            + "the packing changed and this guard quietly stopped looking at anything.");
    }

    /// <summary>
    /// The bounding box and the ink of one 32 bit DIB frame, as percentages of the frame.
    ///
    /// <b>Two traps, both silent, and make-ico.ps1 names them at the other end of the same
    /// format.</b> The header declares a height of TWICE the image, because a one bit AND mask
    /// follows the pixels - so the row count comes from the .ico directory, not from the header.
    /// And the rows run BOTTOM UP, which does not matter to a bounding box measured in both axes
    /// but does matter to anyone reading this and expecting y to mean what it usually means.
    /// </summary>
    private static (double BoxWidthPercent, double BoxHeightPercent, double InkPercent)
        MeasureInk(byte[] bytes, uint offset, int size)
    {
        var bitCount = BitConverter.ToUInt16(bytes, (int)offset + 14);
        Assert.True(
            bitCount == 32,
            $"The {size} by {size} frame is {bitCount} bits per pixel and this reads 32. "
            + "tools/icon/make-ico.ps1 writes 32 bit DIBs, so something else packed this file.");

        var pixels = (int)offset + 40;
        int minX = size, maxX = -1, minY = size, maxY = -1, ink = 0;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                // B, G, R, A per pixel, so alpha is the fourth byte. Reading the first measures
                // blue, which on a green icon looks like a plausible answer and is not one.
                // The threshold matches tools/icon/ink.ps1 so the two instruments agree.
                if (bytes[pixels + (((y * size) + x) * 4) + 3] <= 8)
                {
                    continue;
                }

                ink++;
                if (x < minX) { minX = x; }
                if (x > maxX) { maxX = x; }
                if (y < minY) { minY = y; }
                if (y > maxY) { maxY = y; }
            }
        }

        Assert.True(maxX >= 0, $"The {size} by {size} frame has no ink in it at all.");

        return (
            100.0 * (maxX - minX + 1) / size,
            100.0 * (maxY - minY + 1) / size,
            100.0 * ink / (size * size));
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
