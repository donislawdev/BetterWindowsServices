using System.Diagnostics;
using Bws.Core;
using Xunit.Abstractions;

namespace Bws.Integration.Tests;

/// <summary>
/// Budgets from section 8.1 of the specification, held against the real machine.
///
/// Written 2026-08-02 after a table in `docs/04` counted how many of those six budgets had
/// anything holding them, and the answer was one. The point of that table was never the
/// numbers - it was the column saying BRAK, and this file is part of shortening it.
///
/// <b>Only the budgets a test can honestly hold end up here.</b> Two deliberately do not, and
/// saying which and why is the whole discipline:
///
/// The snapshot's five seconds has no guard, because on two processors the margin is 0.4% and
/// a test asserting it would go red on a loaded agent for reasons that are not ours - a guard
/// that reddens at random teaches everybody to ignore every other one.
///
/// The memory budget has no guard <b>here</b>, because the part of it a process can be asked
/// about includes WPF, a graphics stack and a working set Windows trims by forty megabytes
/// while nothing happens. The part this code decides is the managed heap, and that is guarded
/// in Bws.Gui.Tests where no window is needed.
/// </summary>
public sealed class PerformanceBudgetTests(ITestOutputHelper output)
{
    /// <summary>
    /// Section 8.1: expensive data for the whole machine, in the background, under ten
    /// seconds.
    ///
    /// Measured at 1100-1245 ms over 810 entries and 544 distinct files once the verification
    /// was parallelised, against 4926-8500 before it - so the budget is met eight times over
    /// and this test is not close to its own limit. That is the argument for it being here at
    /// all: unlike the snapshot's five seconds, there is no plausible loaded agent on which
    /// this becomes flaky.
    ///
    /// <b>What it would catch, and the first version of this comment got it wrong.</b> It
    /// said this catches the parallelism being removed. It does not, and the numbers to check
    /// that against were already in this repository: verification one file at a time measured
    /// 4926-8500 ms, which is under ten seconds. It also would not catch the pass asking once
    /// per entry instead of once per distinct file, because that is 810 against 544 - half as
    /// much again, not an order of magnitude.
    ///
    /// So what this holds is the budget and nothing narrower: a change that makes expensive
    /// data take longer than the specification allows. That is worth having and it is a
    /// coarse net, and saying which is the difference between a guard and a comfort. The
    /// finer claims have their own guards - one file per distinct file is checked by mutation
    /// in the second pass tests, and the parallel verdicts by an oracle.
    /// </summary>
    [Fact]
    public void Expensive_data_for_the_whole_machine_stays_inside_the_budget()
    {
        var budget = TimeSpan.FromSeconds(10);

        var catalog = new WindowsScmCatalog();
        var entries = catalog.ReadAll();

        Assert.NotEmpty(entries);

        // Warm first and measured second. The first pass of the day pays for the certificate
        // store and the disk cache, which is a real cost somebody pays once and not the thing
        // this budget is about - `docs/04` says discard the cold run rather than report it.
        _ = SecondPass.Fill(entries, new WindowsBinaryInspector());

        var before = Stopwatch.GetTimestamp();
        var verified = SecondPass.Fill(entries, new WindowsBinaryInspector());
        var elapsed = Stopwatch.GetElapsedTime(before);

        var answered = verified.Count(entry => entry.Signature.Outcome != ReadOutcome.NotRead);

        output.WriteLine(
            $"SECOND PASS {elapsed.TotalMilliseconds:F0} ms over {entries.Count} entries, " +
            $"{answered} answered, budget {budget.TotalSeconds:F0} s");

        // The other half, and without it this passes on a build that verifies nothing at all -
        // which is both the fastest possible implementation and the useless one. Two empty
        // results are equal, and a timing test with no claim about the answer is that trap
        // wearing a stopwatch.
        Assert.True(
            answered > entries.Count / 2,
            $"Only {answered} of {entries.Count} entries came back with a signature read either " +
            "way. A second pass that answers nothing is fast for the wrong reason.");

        // THE MESSAGE NAMES THE PARALLEL RUN FIRST, AND IT DID NOT UNTIL 2026-08-18. It used to open
        // with "this is not a slow machine", which sent three separate readings that day off hunting
        // for a regression in a pass nothing had touched. A clock budget measured beside four other
        // test projects does not measure the budget - it measures how much machine was left over -
        // and the sentence a red run prints is the only thing anybody reads before deciding where to
        // look. Backlog 200. What this test measures is deliberately unchanged: the budget is the
        // promise, and moving it to suit the harness would be moving the promise.
        Assert.True(
            elapsed < budget,
            $"Verifying every binary took {elapsed.TotalSeconds:F1} s over {entries.Count} entries, " +
            $"past the {budget.TotalSeconds:F0} s section 8.1 allows for expensive data. CHECK WHAT " +
            "ELSE WAS RUNNING BEFORE LOOKING AT THE CODE. Measured on 2026-08-18 on one build: 10.7 s, " +
            "11.9 s and 11.2 s inside tools/state.ps1, which runs five test projects at once, against " +
            "1262, 1422 and 1328 ms for this test on its own. Backlog 200. If it WAS on its own, then " +
            "it is a change in how the pass asks - most likely one file per entry instead of one per " +
            "distinct file, or the parallelism gone. Measured at 1100-1245 ms on 2026-08-02.");
    }
}
