namespace Bws.Architecture.Tests;

/// <summary>
/// A ceiling on how long a file may get, set at what the longest one is today.
///
/// Borrowed 2026-08-02 from the owner's Go projects, where the same idea caps function length
/// and nesting. <b>The method transfers, none of its numbers do</b> - theirs came from their
/// tree, these come from measuring this one.
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
/// change to blame. This was the weakest of four mechanisms proposed on the day, and the owner
/// took it anyway, on the grounds that not having spaghetti is cheaper than removing it.
///
/// False alarm estimate: zero on the day, by construction - the ceilings are today's numbers.
/// Every failure from here on is something that grew.
/// </summary>
public sealed class SizeRatchetGuards
{
    /// <summary>
    /// The longest file in the product. <b>644 lines as of 2026-08-02, down from 807 through
    /// 746 in a single day, and it is no longer WindowsScmCatalog.cs.</b>
    ///
    /// That file was 807 lines and is now 515, and the two steps down were both forced by this
    /// guard rather than done for tidiness:
    ///
    ///   807 -> 746   the network rule pushed it to 816, and its two record types moved into
    ///                ScmBuffers.cs
    ///   746 -> 644   reading entries in parallel pushed it to 797, and the questions asked of
    ///                a single service handle moved into ScmDetailReader.cs
    ///   644 -> 631   filling the list in one go pushed MainViewModel.cs to 662, and the
    ///                sentences the window says moved into Sentences.cs
    ///
    /// <b>That is the whole argument for a ceiling, happening three times in an afternoon.</b>
    /// None of the splits was planned, none was suggested by anybody reading the files, and all
    /// three followed a seam that was already there once somebody was made to look for one. The
    /// longest file is now Program.cs, which has never been asked the same question.
    ///
    /// Lowering the number afterwards is not bookkeeping. Leaving it at 746 would hand back a
    /// hundred lines of room nobody argued for.
    /// </summary>
    private const int LongestShippedFile = 631;

    /// <summary>
    /// The longest test file, measured 2026-08-02: MainViewModelTests.cs at 756 lines.
    ///
    /// Held to the same rule as the product, deliberately. A test file nobody can read is a
    /// test file nobody checks, and this project leans on tests harder than most because the
    /// owner does not read the code.
    /// </summary>
    private const int LongestTestFile = 756;

    /// <summary>
    /// How many files may be long at all, where long is <see cref="Long"/>.
    ///
    /// The second dial, and the one that catches the failure the first cannot: everything
    /// creeping towards the ceiling at once without any single file crossing it.
    /// </summary>
    private const int Long = 500;

    private const int ShippedFilesAllowedToBeLong = 6;
    private const int TestFilesAllowedToBeLong = 2;

    [Fact]
    public void No_shipped_file_is_longer_than_the_longest_one_was()
    {
        var offenders = TooLong(Sources.Shipped(), LongestShippedFile);

        Assert.True(
            offenders.Length == 0,
            $"A file grew past {LongestShippedFile} lines, which is where the longest one stood " +
            "when this ceiling was set. Split it, or move a piece of it somewhere it belongs - " +
            "and then lower the number, because it may only ever go down:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void No_test_file_is_longer_than_the_longest_one_was()
    {
        var offenders = TooLong(Sources.Testing(), LongestTestFile);

        Assert.True(
            offenders.Length == 0,
            $"A test file grew past {LongestTestFile} lines. A test nobody can read is a test " +
            "nobody checks, and this project leans on them harder than most:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Not_more_files_are_long_than_were_long()
    {
        var shipped = LongOnes(Sources.Shipped());
        var testing = LongOnes(Sources.Testing());

        Assert.True(
            shipped.Length <= ShippedFilesAllowedToBeLong,
            $"{shipped.Length} shipped files are over {Long} lines, against " +
            $"{ShippedFilesAllowedToBeLong} when this was set. Nothing crossed the ceiling - " +
            "everything moved towards it, which is the shape nobody notices:" +
            Environment.NewLine + string.Join(Environment.NewLine, shipped));

        Assert.True(
            testing.Length <= TestFilesAllowedToBeLong,
            $"{testing.Length} test files are over {Long} lines, against " +
            $"{TestFilesAllowedToBeLong} when this was set:" +
            Environment.NewLine + string.Join(Environment.NewLine, testing));
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
            LongestShippedFile - longestShipped <= Slack,
            $"The longest shipped file is now {longestShipped} lines and the ceiling is still " +
            $"{LongestShippedFile}. Lower it - a ratchet only means something while it is close " +
            "to what it is holding.");

        Assert.True(
            LongestTestFile - longestTest <= Slack,
            $"The longest test file is now {longestTest} lines and the ceiling is still " +
            $"{LongestTestFile}. Lower it.");
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
            .Where(file => file.Lines > Long)
            .OrderByDescending(file => file.Lines)
            .Select(file => $"{file.Name}  {file.Lines} lines")];

    private static int Longest(IEnumerable<string> files) =>
        files.Select(file => File.ReadAllLines(file).Length).DefaultIfEmpty(0).Max();
}
