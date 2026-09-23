namespace Bws.Architecture.Tests;

/// <summary>
/// A ceiling on how long a file may get, set at what the longest one is today.
///
/// Borrowed 2026-08-02 from a Go codebase, where the same idea caps function length and
/// nesting. <b>The method transfers, none of its numbers do</b> - theirs came from their tree,
/// these come from measuring this one.
///
/// <b>The point is the direction, not the number.</b> Nothing here says a file of three hundred
/// lines is a good length - it says the longest file in this product is that long and may not
/// become one line longer. Adding to the longest file means splitting it first, which is the
/// conversation this guard exists to force. Numbers may only ever go down, and lowering one is the
/// reward for doing the work.
///
/// <b>LINES OF CODE SINCE 2026-09-23, NOT LINES</b> - owner's decision, with the rest of the shape
/// guards in <see cref="CodeShapeGuards"/>. Sixty-three per cent of the lines under src/ were
/// comments and blank lines that day, and this guard's own history recorded sessions shortening a
/// fresh comment to fit under its number at least three times: ScmEntry.cs stood four lines under
/// the old ceiling with 67 lines of code in it. A ceiling on raw lines was a ceiling on explaining,
/// in a project that keeps its reasons beside its code on purpose. Comments are free now, in C#
/// and in markup alike, and <see cref="CodeShape"/> says exactly what counts.
///
/// <b>EXACT SINCE THE SAME DAY, NOT WITHIN A HUNDRED LINES.</b> The drift test here allowed a
/// hundred lines of slack, and SizeCeilings.cs recorded at least five times that a ceiling had
/// been left standing above the file it held - found each time by a mutation run in the full gate,
/// never by this test. <see cref="The_ceilings_have_not_been_left_behind_by_the_code"/> now asks
/// for equality, and says the number to set. The slack existed so that deleting a comment would
/// not demand an edit here - and deleting a comment no longer moves the number at all.
///
/// <b>Why this is worth less than the other guards, said out loud.</b> Length is a poor measure
/// of tangle: a long file of flat, well-named methods is fine and a short one can be impossible.
/// What it does measure exactly is growth, and growth is what nobody notices. The tangle is what
/// the method ceilings in <see cref="CodeShapeGuards"/> are for.
/// </summary>
public sealed class SizeRatchetGuards
{
    private static readonly ShapeAxis ShippedFiles = new(
        "shipped file length", "lines of code",
        "SizeCeilings." + nameof(SizeCeilings.LongestShippedFile), SizeCeilings.LongestShippedFile,
        "SizeCeilings." + nameof(SizeCeilings.ShippedFilesNearLongest), SizeCeilings.ShippedFilesNearLongest,
        ShapeAxis.Share(SizeCeilings.LongestShippedFile, ShapeCeilings.NearShare),
        "Split it, or move a piece of it somewhere it belongs",
        () => Lengths(CodeShape.Shipped.Files));

    private static readonly ShapeAxis MarkupFiles = new(
        "shipped markup file length", "lines of markup",
        "SizeCeilings." + nameof(SizeCeilings.LongestShippedMarkupFile), SizeCeilings.LongestShippedMarkupFile,
        "SizeCeilings." + nameof(SizeCeilings.MarkupFilesNearLongest), SizeCeilings.MarkupFilesNearLongest,
        ShapeAxis.Share(SizeCeilings.LongestShippedMarkupFile, ShapeCeilings.NearShare),
        "Markup has no seam a compiler will show you, so the split is a resource dictionary merged in - and " +
        "neither half of the theme can be loaded on its own, so check the window still draws rather than " +
        "trusting a green build",
        () => Lengths(CodeShape.Markup));

    private static readonly ShapeAxis TestFiles = new(
        "test file length", "lines of code",
        "SizeCeilings." + nameof(SizeCeilings.LongestTestFile), SizeCeilings.LongestTestFile,
        "SizeCeilings." + nameof(SizeCeilings.TestFilesNearLongest), SizeCeilings.TestFilesNearLongest,
        ShapeAxis.Share(SizeCeilings.LongestTestFile, ShapeCeilings.NearShare),
        "A test nobody can read is a test nobody checks, and this project leans on them harder than most - split it",
        () => Lengths(CodeShape.Testing.Files));

    /// <summary>The three file axes, for the margin report. Declared after them, so they exist when this is built.</summary>
    internal static readonly ShapeAxis[] Axes = [ShippedFiles, MarkupFiles, TestFiles];

    [Fact]
    public void No_shipped_file_is_longer_than_the_longest_one_was()
    {
        var verdict = ShippedFiles.OverVerdict();

        Assert.True(verdict is null, verdict);
    }

    [Fact]
    public void No_shipped_markup_file_is_longer_than_the_longest_one_was()
    {
        var verdict = MarkupFiles.OverVerdict();

        Assert.True(verdict is null, verdict);
    }

    [Fact]
    public void No_test_file_is_longer_than_the_longest_one_was()
    {
        var verdict = TestFiles.OverVerdict();

        Assert.True(verdict is null, verdict);
    }

    [Fact]
    public void Not_more_files_are_long_than_were_long()
    {
        // The second dial, and the one that catches what the first cannot: everything creeping
        // towards the ceiling at once without any single file crossing it. "Long" is 70% of the
        // ceiling since 2026-09-23 rather than a fixed five hundred lines, so the band follows its
        // ceiling down instead of going quiet the day the ceiling passes under it.
        var verdicts = new[] { ShippedFiles, MarkupFiles, TestFiles }.Select(axis => axis.CrowdVerdict()).OfType<string>().ToArray();

        Assert.True(verdicts.Length == 0, string.Join(Environment.NewLine + Environment.NewLine, verdicts));
    }

    [Fact]
    public void The_ceilings_have_not_been_left_behind_by_the_code()
    {
        // A ratchet that is never tightened is a ceiling nobody is under.
        var verdicts = new[] { ShippedFiles, MarkupFiles, TestFiles }.Select(axis => axis.UnderVerdict()).OfType<string>().ToArray();

        Assert.True(verdicts.Length == 0, string.Join(Environment.NewLine + Environment.NewLine, verdicts));
    }

    private static IEnumerable<ShapeItem> Lengths(IEnumerable<ShapeFile> files) =>
        files.Select(file => new ShapeItem(file.Name, file.Name, file.CodeLines));
}
