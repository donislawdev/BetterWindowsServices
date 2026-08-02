using System.Diagnostics;
using Bws.Core;
using Bws.Core.Snapshots;
using Xunit.Abstractions;

namespace Bws.Integration.Tests;

/// <summary>
/// Reading every entry several at a time, checked against reading them one at a time.
///
/// Written 2026-08-02 with the change it guards. A listing spent about 440 ms of its 455-475
/// opening a handle per entry and waiting - the same loop without those handles measures
/// 13-22 ms - so the work went wide. Afterwards: 108-122 ms, and `bws list` end to end went
/// from 569-595 ms to 231-260.
///
/// <b>Speed is the reason for the change and not the thing being guarded here.</b> A faster
/// listing that answered differently would be worthless, and there are two ways this could
/// answer differently: the order could shift, or a shared handle could produce a different
/// reading under load. The first is held by construction - each entry writes into the slot it
/// was enumerated into - and the second is what this file exists for.
///
/// <b>The oracle is the sequential path, and that is a real oracle rather than two runs of the
/// same code.</b> This project already learned the difference the expensive way: a guard
/// comparing two runs of one implementation sees nothing but non-determinism, and stayed green
/// while every answer was pinned to the wrong file. Degree one and degree sixteen are
/// different code paths through this method, so a disagreement between them is a fact rather
/// than weather.
///
/// <b>What it cannot see, said rather than left out:</b> a fault that both paths share. The
/// listing being right at all is held elsewhere, by ListingContractTests comparing against
/// sc.exe entry by entry.
/// </summary>
[CollectionDefinition("one reading at a time", DisableParallelization = true)]
public sealed class ReadingAlone;

/// <summary>
/// Runs with nothing else running, and that is not fussiness.
///
/// The timing half of this class compares one processor against sixteen. Every other
/// integration test in this project verifies signatures, which takes every processor it can
/// get - so run alongside them, the parallel reading has nothing to be parallel on and the
/// two figures converge. The first version measured that and reported it as the parallelism
/// being gone, which is a guard measuring the test runner and calling it the product.
/// </summary>
[Collection("one reading at a time")]
public sealed class ReadAllContractTests(ITestOutputHelper output)
{

    [Fact]
    public void Reading_several_at_a_time_answers_exactly_what_reading_one_at_a_time_answers()
    {
        var catalog = new WindowsScmCatalog();

        var oneAtATime = catalog.ReadAll(degreeOfParallelism: 1);
        var severalAtOnce = catalog.ReadAll(WindowsScmCatalog.DefaultDegreeOfParallelism);

        Assert.NotEmpty(oneAtATime);
        Assert.Equal(oneAtATime.Count, severalAtOnce.Count);

        // Rendered rather than compared field by field in code, and that is the stronger
        // choice here: the document is the whole entry, every field of it, in a shape the
        // snapshot already has to keep stable. A hand-written comparison would list the
        // fields somebody remembered, and would go on passing when a new one arrived.
        var compared = 0;
        var moved = 0;

        for (var index = 0; index < oneAtATime.Count; index++)
        {
            var sequential = oneAtATime[index];
            var parallel = severalAtOnce[index];

            // Order first, because everything below is meaningless if the two lists are not
            // talking about the same entry.
            Assert.Equal(sequential.ServiceName, parallel.ServiceName);

            var left = System.Text.Json.JsonSerializer.Serialize(EntryDocument.From(sequential));
            var right = System.Text.Json.JsonSerializer.Serialize(EntryDocument.From(parallel));

            if (left == right)
            {
                compared++;
                continue;
            }

            // Running state moves on its own - measured on this machine, services start and
            // stop by themselves within minutes - so a difference in what an entry is DOING
            // between two readings taken seconds apart is the machine, not the threads. A
            // difference in how it is SET UP is not.
            if (sequential with { Status = parallel.Status, ProcessId = parallel.ProcessId } == parallel)
            {
                moved++;
                continue;
            }

            Assert.Fail(
                $"{sequential.ServiceName} reads differently depending on how many entries are " +
                $"described at once, in something other than its running state.{Environment.NewLine}" +
                $"one at a time: {left}{Environment.NewLine}several at once: {right}");
        }

        output.WriteLine(
            $"COMPARED {compared} entries identical, {moved} differing only in running state, " +
            $"of {oneAtATime.Count}");

        // The claim that stops this passing on two empty answers, which are always equal.
        Assert.True(
            compared > oneAtATime.Count / 2,
            $"Only {compared} of {oneAtATime.Count} entries compared identical. Too few for this " +
            "to be saying anything about threads.");
    }

    [Fact]
    public void Reading_several_at_a_time_is_faster_than_reading_one_at_a_time()
    {
        // The reason for the change, held as well as the correctness. Without it, somebody
        // could quietly set the degree back to one and every other test in this project would
        // stay green while a listing took four times as long.
        //
        // Interleaved rather than blocked, because the two variants must meet the same minute
        // of the machine's life - `docs/04` has that rule and it was paid for at S5a1.
        var catalog = new WindowsScmCatalog();
        var single = new List<double>();
        var many = new List<double>();

        for (var run = 0; run < 4; run++)
        {
            var before = Stopwatch.GetTimestamp();
            var one = catalog.ReadAll(degreeOfParallelism: 1);
            var oneTook = Stopwatch.GetElapsedTime(before).TotalMilliseconds;

            before = Stopwatch.GetTimestamp();
            var wide = catalog.ReadAll(WindowsScmCatalog.DefaultDegreeOfParallelism);
            var wideTook = Stopwatch.GetElapsedTime(before).TotalMilliseconds;

            Assert.Equal(one.Count, wide.Count);

            // The first pass of each pays for the manager's own caches. Kept out of the
            // figures rather than folded in, as the measuring rule asks.
            if (run == 0)
            {
                continue;
            }

            single.Add(oneTook);
            many.Add(wideTook);
        }

        output.WriteLine(
            $"ONE AT A TIME {single.Min():F0}-{single.Max():F0} ms, " +
            $"SEVERAL AT ONCE {many.Min():F0}-{many.Max():F0} ms");

        // Twice as fast, not "the ranges do not touch". The measured difference is nearer six
        // times - 466-470 ms against 72-84 on a quiet machine - so half is a long way from the
        // truth on purpose. This runs on whatever hardware somebody has, and a guard that
        // asserts the exact win measured on one machine is a guard that reddens on every other
        // one. What it holds is that going wide still buys something large, which is the claim
        // the change was made on.
        Assert.True(
            many.Max() * 2 < single.Min(),
            $"Describing entries several at a time measured {many.Min():F0}-{many.Max():F0} ms " +
            $"against {single.Min():F0}-{single.Max():F0} ms one at a time - less than twice as " +
            "fast, where six times was measured. Either the parallelism is gone, or the cost it " +
            "was hiding has moved somewhere else and this guard needs rewriting rather than " +
            "relaxing.");
    }
}
