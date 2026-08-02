using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// Keeps the places that can do two things at once down to a list somebody wrote on purpose.
///
/// Borrowed 2026-08-02 from the owner's Go projects, where the same guard names the files
/// allowed to hold a goroutine or a channel. <b>The method transfers, none of its decisions
/// do</b> - the constructs below are C#'s, and the list is ours.
///
/// The argument for containing it at the source rather than testing for its symptoms: a race
/// is the one defect class that does not reproduce on demand, so a test for one is a test that
/// passes on the days it is wrong. What can be checked exactly is <b>where the code is even
/// able to have one</b>, and today that is three files out of forty two.
///
/// <b>What this is not.</b> It does not make the listed files safe - `SecondPass` earns its
/// place with an oracle test and a probe, and the view model with a reentrancy test. It makes
/// the fourth file a decision instead of an accident.
///
/// False alarm estimate, as 04-PLAN-PRAC requires before building a guard: measured over the
/// whole product at the moment it was written, three files match and all three are listed. So
/// zero. Every hit from here on is a real question until somebody shows otherwise.
/// </summary>
public sealed class ConcurrencyGuards
{
    /// <summary>
    /// Where doing two things at once is allowed, and why, one line each.
    ///
    /// Adding a file here is the deliberate act, and the reason belongs beside it rather than
    /// in a commit message nobody will find again.
    /// </summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.Ordinal)
    {
        ["SecondPass.cs"] =
            "ADR-22. Verifying signatures one file at a time measured 4926-8500 ms over 810 " +
            "entries and 544 files, and several at once measures 1100-1245. The degree follows " +
            "the processor count, the work set is fixed before it starts, and the order of the " +
            "result is restored afterwards - all three of those are tested.",

        ["WindowsBinaryInspector.cs"] =
            "The publisher cache the pass above reads from several threads at once. A plain " +
            "dictionary here would be the quiet kind of race: right on most runs.",

        ["MainViewModel.cs"] =
            "Reading the manager off the interface thread, because a full reading takes about " +
            "half a second and a window that stops answering for half a second looks broken. " +
            "Only the reading runs out there - nothing it returns touches anything on screen " +
            "until it is back."
    };

    /// <summary>
    /// Constructs that mean work is happening somewhere else, or that state is being shared
    /// with somewhere else.
    ///
    /// Deliberately not <c>async</c> and <c>await</c>. Those are how a single thread waits
    /// without blocking, which is the opposite of the problem - and forbidding them would
    /// make the guard fire on the ordinary shape of a window.
    /// </summary>
    private static readonly Regex Concurrency = new(
        @"\bTask\.Run\b|\bParallel\.|\bnew\s+Thread\b|\block\s*\(|\bInterlocked\.|\bMonitor\.|" +
        @"\bVolatile\.|\bThreadPool\.|\bConcurrent(Dictionary|Bag|Queue|Stack)\b|" +
        @"\bSemaphoreSlim\b|\bManualResetEvent|\bAutoResetEvent\b|\bMutex\b|\bBarrier\b",
        RegexOptions.Compiled,
        Sources.Ceiling);

    [Fact]
    public void Only_the_places_that_argued_for_it_can_do_two_things_at_once()
    {
        var found = Matching();
        var strangers = found.Where(file => !Allowed.ContainsKey(file)).ToArray();

        Assert.True(
            strangers.Length == 0,
            "Concurrency appeared somewhere that has not argued for it. A race is the one " +
            "defect that does not reproduce on demand, so where it becomes possible is a " +
            "decision rather than a detail. Either this file belongs in the list with its " +
            "reason and its own test, or the work belongs on the thread that asked for it:" +
            Environment.NewLine + string.Join(Environment.NewLine, strangers));
    }

    [Fact]
    public void The_list_does_not_name_places_that_stopped_doing_it()
    {
        // The other direction, and the one that lets a list rot into a wish. An entry naming a
        // file that no longer does anything concurrently reads as an argument still being made.
        var found = Matching();
        var gone = Allowed.Keys.Where(file => !found.Contains(file)).ToArray();

        Assert.True(
            gone.Length == 0,
            "These files are listed as doing two things at once and no longer do. Remove them, " +
            "so the list keeps meaning what it says:" +
            Environment.NewLine + string.Join(Environment.NewLine, gone));
    }

    private static HashSet<string> Matching()
    {
        var files = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Sources.Shipped())
        {
            if (Concurrency.IsMatch(File.ReadAllText(file)))
            {
                files.Add(Path.GetFileName(file));
            }
        }

        return files;
    }
}
