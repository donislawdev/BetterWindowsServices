using CsCheck;

// System.Diagnostics, System.Globalization and System.IO are written out in full below rather than
// imported, for the reason SourceTree.cs gives: this file is meant to be linked into projects with
// different implicit using sets, and the window's test project takes System.IO out of its set. A
// using line one project needs is an unnecessary one (IDE0005, an error here) in another.
namespace Bws.Tests;

/// <summary>
/// The one door every random property in these tests goes through.
///
/// <b>Two jobs, and both are about the weekly search rather than about any one property.</b> On a
/// pull request a property draws what it asks for and nothing here changes that. On Sunday
/// tests/search/search.ps1 runs each property for minutes instead of a fraction of a second, by
/// setting CsCheck_Time, which CsCheck 4.7.0 reads whenever a test does not pass a time of its own
/// - measured 2026-10-16, one property ran 5 s with the variable at 5 and well under one without.
/// That run then has to say HOW MUCH it searched, because a green search that drew nothing looks
/// exactly like a green search that drew a million times. So every call is counted here, and the
/// count is written where the runner asked for it.
///
/// <b>The count is of calls, not of distinct inputs.</b> CsCheck calls the property again while it
/// shrinks a failure, and those calls are counted too. On a green run there is no shrinking, so
/// there the two are the same.
///
/// <b>Why one door rather than CsCheck called directly.</b> A property written past this class
/// would never report, and the weekly search would list it and then call it NOT SEARCHED - which is
/// the right verdict and a confusing way to learn the rule. SearchGuards in the architecture tests
/// holds it instead: CsCheck is sampled only here, and every test that comes through here carries
/// the trait search=weekly that the runner lists them by, apart from the few it names with a reason.
///
/// Project: docs/PROJEKT-FUZZING-20261016.md (outside the repository, like every project note).
/// </summary>
internal static class Searching
{
    /// <summary>The variable the weekly runner sets to the file it wants the count in.</summary>
    private const string ReportVariable = "BWS_SEARCH_REPORT";

    private static readonly System.Threading.Lock Writing = new();

    /// <summary>
    /// Samples <paramref name="gen"/> the way <c>Gen.Sample</c> does and returns how many times the
    /// property was called.
    /// </summary>
    internal static long Searched<T>(this Gen<T> gen, Func<T, bool> holds, Draws<T> draws)
    {
        long calls = 0;
        var watch = System.Diagnostics.Stopwatch.StartNew();

        gen.Sample(
            value =>
            {
                System.Threading.Interlocked.Increment(ref calls);

                return holds(value);
            },
            seed: draws.Seed,
            iter: draws.Iter,
            threads: draws.Threads,
            print: draws.Print);

        var counted = System.Threading.Interlocked.Read(ref calls);

        Report(counted, watch.Elapsed);

        return counted;
    }

    /// <summary>
    /// One line per sampling, appended, so that a test sampling twice is reported as two lines and
    /// the runner adds them. Nothing is written when the variable is not set, which is every run
    /// but the weekly one.
    /// </summary>
    private static void Report(long calls, TimeSpan took)
    {
        var file = Environment.GetEnvironmentVariable(ReportVariable);

        if (string.IsNullOrWhiteSpace(file))
        {
            return;
        }

        var line = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"calls={calls} seconds={took.TotalSeconds:F1}{Environment.NewLine}");

        lock (Writing)
        {
            System.IO.File.AppendAllText(file, line);
        }
    }
}

/// <summary>
/// How much one property draws on a pull request, and how it reports - one argument rather than
/// four, because the test signature ceiling is five parameters and four already counts as standing
/// near it.
/// </summary>
/// <param name="iter">Draws on an ordinary run. The weekly search replaces it with a time.</param>
internal sealed class Draws<T>(long iter)
{
    public long Iter { get; } = iter;

    /// <summary>What a failure prints instead of the value itself.</summary>
    public Func<T, string>? Print { get; init; }

    /// <summary>
    /// -1 for CsCheck's default of one thread a processor. One for a property that touches anything
    /// bound to a thread, the way the window's are.
    /// </summary>
    public int Threads { get; init; } = -1;

    /// <summary>
    /// The seed a weekly finding printed. A property replaying one is the regression test for it,
    /// and every pull request then runs the input that failed.
    /// </summary>
    public string? Seed { get; init; }
}
