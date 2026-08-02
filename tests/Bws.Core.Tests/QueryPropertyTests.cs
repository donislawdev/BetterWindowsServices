using Bws.Core.Querying;

namespace Bws.Core.Tests;

/// <summary>
/// Properties of the query language that are about ORDER rather than about input.
///
/// `ADR-10` names this parser as one of the three places where property tests pay for
/// themselves, and gives two properties. The first - the parser never fails in a way it did
/// not plan for - moved to <see cref="ParserPropertyTests"/> on 2026-08-02, where a library
/// shrinks a failure to the smallest text that causes it. These stayed, because shrinking has
/// nothing to offer them: what varies here is the order of a fixed set of members, and the
/// smallest counterexample is already two of them.
///
/// Both are invariants nobody would find by looking. Breaking either shows up as results that
/// depend on the order somebody clicked filters in, which reads as the tool being unreliable
/// rather than as a bug with a location.
/// </summary>
public sealed class QueryPropertyTests
{
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
