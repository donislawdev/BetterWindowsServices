using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// Who breaks if an entry stops, asked about several entries at once.
///
/// <b>Written 2026-09-29 with the pass itself</b> - S-5 of the external performance report,
/// backlog 466. One after another the pass cost 155-167 ms over 797 entries on the machine it was
/// measured on, and the same calls from several threads 19-40 ms. Two things are held here, and
/// they are the two a parallel loop gets wrong without a sound: every answer landing in its own
/// entry rather than a neighbour's, and the questions really going out together rather than a loop
/// that only looks parallel.
/// </summary>
[CollectionDefinition("asking about dependents alone", DisableParallelization = true)]
public sealed class AskingAboutDependentsAlone;

/// <summary>
/// Runs with nothing else running, since the day it first ran anywhere but here.
///
/// <b>Found on the first CI run, 2026-09-29, and the lesson is the one ReadAllContractTests had
/// already paid for.</b> The second test below needs the thread pool to hand the pass a second
/// thread. On sixteen processors it always got one. On the build agent, with the rest of this
/// project's tests running beside it, it waited ten seconds and never did - the guard measured the
/// test runner and called it the product. Alone, a free thread is there to take the work.
/// </summary>
[Collection("asking about dependents alone")]
public sealed class RequiredByPassParallelTests
{
    [Fact]
    public void Asked_several_at_once_every_entry_keeps_its_own_answer_and_the_order_it_came_in()
    {
        // Enough entries for the threads to interleave, each with an answer nobody else has - so
        // an answer that landed one slot over, or an entry that moved, cannot look right by chance.
        // Every third is refused, because a refusal carried to the wrong entry is the worst of these:
        // it says "no access" about something that was read and "nobody" about something that was not.
        var entries = Enumerable.Range(0, 400)
            .Select(index => Entries.Any with { ServiceName = $"Svc{index:000}", DisplayName = $"Service {index}" })
            .ToArray();

        var catalog = new FakeScmCatalog(entries);

        for (var index = 0; index < entries.Length; index++)
        {
            if (index % 3 == 0)
            {
                catalog.RefuseDependentsFor.Add(entries[index].ServiceName);
            }
            else
            {
                catalog.DependedOnBy(entries[index].ServiceName, $"Dep{index:000}");
            }
        }

        var together = RequiredByPass.Fill(entries, catalog, degreeOfParallelism: 8);
        var alone = RequiredByPass.Fill(entries, catalog, degreeOfParallelism: 1);

        Assert.Equal(entries.Select(entry => entry.ServiceName), together.Select(entry => entry.ServiceName));

        for (var index = 0; index < entries.Length; index++)
        {
            Assert.Equal(Said(alone[index]), Said(together[index]));

            Assert.Equal(
                index % 3 == 0 ? "denied 5" : $"present Dep{index:000}",
                Said(together[index]));
        }
    }

    [Fact]
    public void The_entries_are_asked_about_at_the_same_time_rather_than_one_after_another()
    {
        // The first two questions wait for each other. One after another, the first waits for a
        // partner that only comes once it has given up - so a loop that is parallel in name only
        // fails here, twenty seconds late, rather than passing quietly.
        using var catalog = new Meeting();

        var entries = Enumerable.Range(0, 4)
            .Select(index => Entries.Any with { ServiceName = $"Svc{index}" })
            .ToArray();

        RequiredByPass.Fill(entries, catalog, degreeOfParallelism: 2);

        Assert.True(catalog.Met, "The first two questions never waited for each other - they were asked one after another.");
    }

    /// <summary>The answer an entry carries, in one string, so two of them compare in one line.</summary>
    private static string Said(ScmEntry entry) => entry.RequiredBy.Outcome switch
    {
        ReadOutcome.Present => "present " + string.Join(",", entry.RequiredBy.Value!),
        ReadOutcome.Denied => $"denied {entry.RequiredBy.ErrorCode}",
        var outcome => outcome.ToString()
    };

    /// <summary>
    /// A manager whose first two questions wait for each other, for up to twenty seconds.
    ///
    /// A deadline rather than a wait with no end, because a test that is meant to go red must not
    /// become a run that never finishes - the note at the top of tools/mutate/mutate.ps1 says why.
    /// </summary>
    private sealed class Meeting : IScmCatalog, IDisposable
    {
        private readonly CountdownEvent _both = new(2);
        private int _asked;

        /// <summary>False once the first question gave up waiting for the second.</summary>
        internal bool Met { get; private set; } = true;

        public IReadOnlyList<ScmEntry> ReadAll() => [];

        public IReadOnlyList<ScmStatus> ReadStatuses() => [];

        public Reading<IReadOnlyList<string>> ReadDependents(string serviceName)
        {
            if (Interlocked.Increment(ref _asked) <= 2)
            {
                _both.Signal();

                if (!_both.Wait(TimeSpan.FromSeconds(20)))
                {
                    Met = false;
                }
            }

            return Reading<IReadOnlyList<string>>.Absent();
        }

        public void Dispose() => _both.Dispose();
    }
}
