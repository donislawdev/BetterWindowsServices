using Bws.Core.Querying;

namespace Bws.Core.Tests;

/// <summary>
/// Properties of the query language rather than examples of it.
///
/// ADR-10 names this parser as one of the three places where property tests pay for
/// themselves here, and gives the two properties: the parser never fails in a way it did
/// not plan for, and the order somebody typed their members in does not change the
/// answer. Examples cannot cover either, because both are statements about every input
/// rather than about a chosen one.
///
/// Generation is deliberately built from the characters that mean something - quotes,
/// backslashes, commas, colons, exclamation marks, slashes, stars - because random
/// letters would exercise the boring path and prove nothing about the interesting one.
/// </summary>
#pragma warning disable CA1031
// Catching everything is the assertion here, not a lapse. The property being checked is
// that no exception of any kind escapes, so narrowing the catch would narrow the claim to
// the failures somebody already thought of - which are exactly the ones that do not need
// a property test.
public sealed class QueryPropertyTests
{
    private const string Interesting = "\"\\,:!*?/=<>- abnames tatus";

    [Fact]
    public void Reading_a_query_never_fails_in_a_way_it_did_not_plan_for()
    {
        // The contract is total: any text at all comes back either as something that can
        // filter or as a list of problems. An escaping mistake that throws would reach a
        // person as a stack trace from typing into a search box.
        var random = new Random(Seed: 20260801);

        for (var attempt = 0; attempt < 20_000; attempt++)
        {
            var text = RandomQuery(random);

            QueryParseResult parsed;

            try
            {
                parsed = QueryParser.Parse(text);
            }
            catch (Exception failure)
            {
                Assert.Fail($"Parsing '{text}' threw {failure.GetType().Name}: {failure.Message}");
                return;
            }

            Assert.True(
                parsed.IsValid ^ (parsed.Problems.Count > 0),
                $"Parsing '{text}' produced neither a usable query nor a problem.");
        }
    }

    [Fact]
    public void A_query_that_reads_can_always_judge_an_entry_without_failing()
    {
        // Parsing and evaluating fail differently. A pattern that compiles and then trips
        // over a particular value would surface only on the machine that has that value.
        var random = new Random(Seed: 20260802);
        var entries = Sample();

        for (var attempt = 0; attempt < 20_000; attempt++)
        {
            var text = RandomQuery(random);
            var parsed = QueryParser.Parse(text);

            if (!parsed.IsValid)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                try
                {
                    _ = parsed.Query!.Match(entry);
                }
                catch (Exception failure)
                {
                    Assert.Fail($"Running '{text}' against {entry.ServiceName} threw {failure.GetType().Name}.");
                    return;
                }
            }
        }
    }

    [Fact]
    public void The_order_members_were_typed_in_never_changes_the_answer()
    {
        // Breaking this shows up as results that depend on the order somebody clicked
        // filters in, which is the kind of difference nobody would think to look for.
        string[] members =
        [
            "status:running",
            "type:ownProcess",
            "!name:dnscache",
            "spooler",
            "pid:>100",
            "start:auto",
            "account:any"
        ];

        var entries = Sample();
        var random = new Random(Seed: 20260803);

        var expected = Answer(members, entries);

        for (var attempt = 0; attempt < 500; attempt++)
        {
            var shuffled = Shuffle(members, random);

            Assert.Equal(expected, Answer(shuffled, entries));
        }
    }

    [Fact]
    public void Repeating_a_field_never_narrows_the_answer()
    {
        // The other half of the folding rule. Adding a second value for a field somebody
        // already asked about widens the answer, never shrinks it, which is what makes
        // ticking a second box in the interface do what a person expects.
        var entries = Sample();

        var one = new HashSet<string>(Names(["status:running"], entries), StringComparer.Ordinal);
        var two = new HashSet<string>(Names(["status:running", "status:stopped"], entries), StringComparer.Ordinal);

        Assert.Subset(two, one);
        Assert.True(two.Count > one.Count, "The sample has to contain a stopped entry for this to prove anything.");
    }

    private static string Answer(IReadOnlyList<string> members, IReadOnlyList<ScmEntry> entries) =>
        string.Join(",", Names(members, entries));

    private static string[] Names(IReadOnlyList<string> members, IReadOnlyList<ScmEntry> entries) =>
        [.. QueryParserTests.Valid(string.Join(' ', members)).Filter(entries).Entries.Select(entry => entry.ServiceName)];

    private static string[] Shuffle(string[] members, Random random)
    {
        var shuffled = members.ToArray();

        for (var index = shuffled.Length - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (shuffled[index], shuffled[swap]) = (shuffled[swap], shuffled[index]);
        }

        return shuffled;
    }

    private static string RandomQuery(Random random)
    {
        var length = random.Next(0, 24);
        var text = new char[length];

        for (var index = 0; index < length; index++)
        {
            text[index] = Interesting[random.Next(Interesting.Length)];
        }

        return new string(text);
    }

    /// <summary>
    /// Entries that differ in the ways the language can ask about, including the two that
    /// carry no value and the one that could not be read. A sample where every entry looks
    /// the same would let all of these pass while checking nothing.
    /// </summary>
    private static ScmEntry[] Sample() =>
    [
        Entries.Any,
        Entries.Named("Dnscache", "DNS Client") with { Status = EntryStatus.Stopped },
        Entries.Named("amdkmdag", "AMD display driver") with
        {
            EntryType = EntryType.KernelDriver,
            StartType = Reading<StartType>.Present(Core.StartType.Manual),
            DelayedAuto = Reading<bool>.Absent(),
            ProcessId = Reading<int>.Absent()
        },
        Entries.Named("BITS", "Background Intelligent Transfer Service") with
        {
            DelayedAuto = Reading<bool>.Present(true)
        },
        Entries.Named("McmSvc", "Managed connections") with
        {
            Account = Reading<string>.Present(@"NT SERVICE\McmSvc")
        },
        Entries.Named("Locked", "Nothing readable here") with
        {
            StartType = Reading<StartType>.Denied(Entries.AccessDenied, "access denied"),
            DelayedAuto = Reading<bool>.Denied(Entries.AccessDenied, "access denied"),
            Account = Reading<string>.Denied(Entries.AccessDenied, "access denied"),
            ProcessId = Reading<int>.Denied(Entries.AccessDenied, "access denied")
        },
        Entries.Named("Empty", "No account at all") with
        {
            Account = Reading<string>.Absent(),
            ProcessId = Reading<int>.Absent()
        }
    ];
}
#pragma warning restore CA1031
