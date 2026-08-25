namespace Bws.Architecture.Tests;

/// <summary>
/// A ceiling on how long a file may get, set at what the longest one is today.
///
/// Borrowed 2026-08-02 from a Go codebase, where the same idea caps function length and
/// nesting. <b>The method transfers, none of its numbers do</b> - theirs came from their tree,
/// these come from measuring this one.
///
/// <b>The point is the direction, not the number.</b> Nothing here says 807 lines is a good
/// length - it says the longest file in this product is 807 lines and is not allowed to become
/// 808. Adding to the longest file means splitting it first, which is the conversation this
/// guard exists to force. Numbers below may only ever go down, exactly like the coverage
/// threshold `ADR-10` describes, and lowering one is the reward for doing the work.
///
/// <b>Why this is worth less than the other guards, said out loud.</b> Line count is a poor
/// measure of tangle: a long file of flat, well-named methods is fine and a short one can be
/// impossible. What it does measure exactly is <b>growth</b>, and growth is what nobody
/// notices - a file gains thirty lines a slice and is unreadable a year later with no single
/// change to blame. This was the weakest of four mechanisms proposed on the day and was taken
/// anyway, on the grounds that not having spaghetti is cheaper than removing it.
///
/// False alarm estimate: zero on the day, by construction - the ceilings are today's numbers.
/// Every failure from here on is something that grew.
/// </summary>
public sealed class SizeRatchetGuards
{
    [Fact]
    public void No_shipped_file_is_longer_than_the_longest_one_was()
    {
        var offenders = TooLong(Sources.Shipped(), SizeCeilings.LongestShippedFile);

        Assert.True(
            offenders.Length == 0,
            $"A file grew past {SizeCeilings.LongestShippedFile} lines, which is where the longest one stood " +
            "when this ceiling was set. Split it, or move a piece of it somewhere it belongs - " +
            "and then lower the number, because it may only ever go down:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void No_shipped_markup_file_is_longer_than_the_longest_one_was()
    {
        var offenders = TooLong(Sources.ShippedMarkup(), SizeCeilings.LongestShippedMarkupFile);

        Assert.True(
            offenders.Length == 0,
            $"A markup file grew past {SizeCeilings.LongestShippedMarkupFile} lines, which is where the longest " +
            "one stood when this ceiling was set. Markup has no seam a compiler will show you, so " +
            "the split is a resource dictionary merged in - and neither half of the theme can be " +
            "loaded on its own, so check the window still draws rather than trusting a green build:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void No_test_file_is_longer_than_the_longest_one_was()
    {
        var offenders = TooLong(Sources.Testing(), SizeCeilings.LongestTestFile);

        Assert.True(
            offenders.Length == 0,
            $"A test file grew past {SizeCeilings.LongestTestFile} lines. A test nobody can read is a test " +
            "nobody checks, and this project leans on them harder than most:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Not_more_files_are_long_than_were_long()
    {
        var shipped = LongOnes(Sources.Shipped());
        var testing = LongOnes(Sources.Testing());

        Assert.True(
            shipped.Length <= SizeCeilings.ShippedFilesAllowedToBeLong,
            $"{shipped.Length} shipped files are over {SizeCeilings.Long} lines, against " +
            $"{SizeCeilings.ShippedFilesAllowedToBeLong} when this was set. Nothing crossed the ceiling - " +
            "everything moved towards it, which is the shape nobody notices:" +
            Environment.NewLine + string.Join(Environment.NewLine, shipped));

        Assert.True(
            testing.Length <= SizeCeilings.TestFilesAllowedToBeLong,
            $"{testing.Length} test files are over {SizeCeilings.Long} lines, against " +
            $"{SizeCeilings.TestFilesAllowedToBeLong} when this was set:" +
            Environment.NewLine + string.Join(Environment.NewLine, testing));

        var markup = LongOnes(Sources.ShippedMarkup());

        Assert.True(
            markup.Length <= SizeCeilings.ShippedMarkupFilesAllowedToBeLong,
            $"{markup.Length} markup files are over {SizeCeilings.Long} lines, against " +
            $"{SizeCeilings.ShippedMarkupFilesAllowedToBeLong} when this was set. There are three markup files " +
            "in this product, so this is the appearance surface spreading rather than one file " +
            "growing:" +
            Environment.NewLine + string.Join(Environment.NewLine, markup));
    }

    [Fact]
    public void The_ceilings_have_not_been_left_behind_by_the_code()
    {
        // A ratchet that is never tightened is a ceiling nobody is under. If the longest file
        // has shrunk well below the number, the number is no longer measuring anything, and
        // this says so rather than passing quietly.
        //
        // Slack rather than exactness, because a ceiling that has to be edited on every commit
        // that deletes a comment would teach everybody to edit it without thinking.
        const int Slack = 100;

        var longestShipped = Longest(Sources.Shipped());
        var longestTest = Longest(Sources.Testing());

        Assert.True(
            SizeCeilings.LongestShippedFile - longestShipped <= Slack,
            $"The longest shipped file is now {longestShipped} lines and the ceiling is still " +
            $"{SizeCeilings.LongestShippedFile}. Lower it - a ratchet only means something while it is close " +
            "to what it is holding.");

        Assert.True(
            SizeCeilings.LongestTestFile - longestTest <= Slack,
            $"The longest test file is now {longestTest} lines and the ceiling is still " +
            $"{SizeCeilings.LongestTestFile}. Lower it.");

        var longestMarkup = Longest(Sources.ShippedMarkup());

        Assert.True(
            SizeCeilings.LongestShippedMarkupFile - longestMarkup <= Slack,
            $"The longest markup file is now {longestMarkup} lines and the ceiling is still " +
            $"{SizeCeilings.LongestShippedMarkupFile}. Lower it - and here it matters more than above, because " +
            "this ceiling was set at a number nobody would choose on purpose.");
    }

    private static string[] TooLong(IEnumerable<string> files, int ceiling) =>
        [.. files
            .Select(file => (Name: Path.GetFileName(file), Lines: File.ReadAllLines(file).Length))
            .Where(file => file.Lines > ceiling)
            .OrderByDescending(file => file.Lines)
            .Select(file => $"{file.Name}  {file.Lines} lines")];

    private static string[] LongOnes(IEnumerable<string> files) =>
        [.. files
            .Select(file => (Name: Path.GetFileName(file), Lines: File.ReadAllLines(file).Length))
            .Where(file => file.Lines > SizeCeilings.Long)
            .OrderByDescending(file => file.Lines)
            .Select(file => $"{file.Name}  {file.Lines} lines")];

    private static int Longest(IEnumerable<string> files) =>
        files.Select(file => File.ReadAllLines(file).Length).DefaultIfEmpty(0).Max();
}
