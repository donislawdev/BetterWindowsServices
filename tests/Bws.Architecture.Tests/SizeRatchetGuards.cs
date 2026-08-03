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
    /// The longest file in the product. <b>583 lines as of 2026-08-02, down from 807 in a
    /// single day, and it is no longer WindowsScmCatalog.cs.</b>
    ///
    /// That file was 807 lines and is now 515. Every step down was forced by this guard rather
    /// than done for tidiness:
    ///
    ///   807 -> 746   the network rule pushed it to 816, and its two record types moved into
    ///                ScmBuffers.cs
    ///   746 -> 644   reading entries in parallel pushed it to 797, and the questions asked of
    ///                a single service handle moved into ScmDetailReader.cs
    ///   644 -> 631   filling the list in one go pushed MainViewModel.cs to 662, and the
    ///                sentences the window says moved into Sentences.cs
    ///   631 -> 583   refusing to overwrite a snapshot pushed Program.cs to 651, and carrying
    ///                a plan out moved into Execution.cs
    ///
    /// <b>That is the whole argument for a ceiling, happening four times in an afternoon.</b>
    /// None of the splits was planned, none was suggested by anybody reading the files, and all
    /// four followed a seam that was already there once somebody was made to look for one.
    ///
    /// Lowering the number afterwards is not bookkeeping. Leaving it at 746 would hand back a
    /// hundred lines of room nobody argued for.
    ///
    /// <b>583 -> 568 on 2026-08-03</b>, and it happened three more times the same way while
    /// repairing what an audit found:
    ///
    ///   583 -> 583   reporting a wildcard the engine will not build pushed QueryParser.cs to
    ///                598, and working out which accepted spelling somebody meant moved into
    ///                QuerySpelling.cs
    ///   583 -> 583   moving the whole program inside its own catch, and telling a mistyped path
    ///                from a disk that went away, pushed Program.cs to 597 - so the snapshot as a
    ///                file on disk moved into SnapshotFiles.cs
    ///   583 -> 568   saying how a list becomes another list without doubling a row pushed
    ///                MainViewModel.cs to 611, and the reconciliation moved into RowList.cs,
    ///                which is where a collection that knows how to become another one belongs
    ///
    /// <b>568 -> 557 later the same day</b>, repairing the last tier of the same audit, and twice
    /// more for the same reason:
    ///
    ///   568 -> 568   saying whether query text is finished or still being typed pushed
    ///                QueryParser.cs to 604, and reading a VALUE moved into QueryValueReader.cs -
    ///                a seam the language had from the day it was written, found only because a
    ///                ceiling made somebody look
    ///   568 -> 557   refusing an option given twice pushed CommandLine.cs to 589, and reading a
    ///                single word moved into Arguments.cs
    ///
    /// The longest file is now WindowsScmCatalog.cs at 557, and it got there without being
    /// touched - every file that used to be above it came down past it. <b>The mutation entry
    /// that proves this guard can fail finds the longest file at run time</b> rather than naming
    /// one, because an entry naming a file stops proving anything the moment that file stops
    /// being longest - it came back MISSED for exactly that reason on 2026-08-02, and again on
    /// 2026-08-03.
    /// </summary>
    private const int LongestShippedFile = 557;

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

    /// <summary>
    /// Six until 2026-08-03, and it was fully used the whole time - the seventh file to pass 500
    /// lines would have reddened the build. Two splits that afternoon took it to five, so the
    /// number came down with it: leaving it at six would hand back a slot nobody argued for,
    /// which is the same reasoning as the ceiling above.
    ///
    /// The mutation entry proving this dial can fail came back MISSED the moment the count
    /// dropped, honestly - adding one long file to a tree with a spare slot changes nothing.
    /// </summary>
    private const int ShippedFilesAllowedToBeLong = 5;
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
