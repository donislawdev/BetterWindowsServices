using Bws.Core.Snapshots;
using Bws.Core.Tests.Fakes;
using CsCheck;

namespace Bws.Core.Tests;

/// <summary>
/// Whole machines, generated, for the properties that compare two of them.
///
/// <b>Its own file because the size ratchet said so, and the seam it pointed at is real.</b>
/// What is here answers "what does a generated machine look like, and what do its two readings
/// say about themselves". What is in <see cref="DiffPropertyTests"/> answers "what must be true
/// of comparing two of them". Those are different questions, and the second one is the only one
/// a reader checking a claim about the engine needs to read.
///
/// <b>Generation starts from the fixture rather than from nothing.</b> A random EntryDocument is
/// mostly nulls and would prove that nulls compare equal. What is varied here is what a
/// comparison can actually see: the name, the running state, two settings, the process identifier
/// that must never count, and how each of two fields was read - present, absent, refused, or
/// never asked. Those four states are the whole of what makes the engine harder than a
/// field-by-field comparison, and they are what an example test has to write out one at a time.
/// </summary>
internal static class Machines
{
    /// <summary>
    /// Names from a small pool on purpose. A pool wide enough to make collisions rare would
    /// generate two snapshots sharing no entries at all, and every property would then be
    /// checking that two disjoint sets are disjoint.
    /// </summary>
    private static readonly string[] Names = ["Spooler", "BFE", "W32Time", "Dnscache", "Twin"];

    private static readonly string[] Accounts =
        ["LocalSystem", "NT AUTHORITY\\LocalService", "NT AUTHORITY\\NetworkService"];

    private static readonly EntryStatus[] Statuses =
        [EntryStatus.Running, EntryStatus.Stopped, EntryStatus.StartPending, EntryStatus.Paused];

    private static readonly StartType[] Starts =
        [Core.StartType.Automatic, Core.StartType.Manual, Core.StartType.Disabled];

    /// <summary>
    /// One entry, varied only where a comparison can see it.
    ///
    /// The last two dice choose how the security descriptor and the signature were read, across
    /// all four outcomes. Those two fields rather than any others because they are the ones a
    /// real machine varies: the descriptor is refused for five entries of eight hundred without
    /// elevation, and the signature is never asked for unless somebody asks.
    /// </summary>
    private static readonly Gen<ScmEntry> AnEntry =
        Gen.Select(
                Gen.Select(Gen.Int[0, 4], Gen.Int[0, 3], Gen.Int[0, 40_000]),
                Gen.Select(Gen.Int[0, 2], Gen.Int[0, 3], Gen.Int[0, 3]))
            .Select(dice =>
            {
                var (identity, setting, pid) = dice.Item1;
                var (account, descriptor, signature) = dice.Item2;

                return Entries.Any with
                {
                    ServiceName = Names[identity],
                    DisplayName = Names[identity] + " display name",
                    Status = Statuses[setting],
                    ProcessId = Reading<int>.Present(pid),
                    StartType = Reading<StartType>.Present(Starts[account]),
                    Account = Reading<string>.Present(Accounts[account]),
                    SecurityDescriptor = HowItWasRead<string>(descriptor, "O:SYG:SYD:(A;;CCLCSWLOCRRC;;;AU)"),
                    Signature = HowItWasRead(
                        signature, new BinarySignature(SignatureStatus.Trusted, 0, "Contoso Systems"))
                };
            });

    /// <summary>
    /// A whole machine. Small, because what the properties are about is how entries line up
    /// against each other, and six of them exercise every shape that a hundred would.
    /// </summary>
    internal static readonly Gen<IReadOnlyList<ScmEntry>> Any = AnEntry.List[1, 6].Select(Distinct);

    /// <summary>
    /// A reading of that machine, with metadata a test controls.
    ///
    /// The session's own would make every property about elevation pass or fail depending on how
    /// the test run was started.
    /// </summary>
    internal static Snapshot Taken(IReadOnlyList<ScmEntry> entries, bool elevated = true, long at = 0) =>
        Snapshot.Of(entries, note: null, new FakeClock()) with
        {
            Metadata = new SnapshotMetadata
            {
                SchemaVersion = Snapshot.CurrentSchemaVersion,
                Machine = "TESTBOX",
                OperatingSystem = "Microsoft Windows NT 10.0.26200.0",
                TakenAt = DateTimeOffset.UnixEpoch.AddSeconds(at),
                TakenBy = "TESTBOX\\somebody",
                Elevated = elevated,
                Note = null,
                Tool = "0.1.0"
            }
        };

    /// <summary>
    /// Five ways to hand the engine something, four of which are not a snapshot.
    ///
    /// Built by editing a well-formed one rather than generated from nothing, for the reason the
    /// snapshot reader's property gives about damaged files: an input that fails at the first
    /// character proves only that the first character is checked. These four all deserialise, all
    /// look almost right, and all used to end a comparison with a message from inside .NET.
    /// </summary>
    internal static Snapshot Spoiled(Snapshot snapshot, int how) => how switch
    {
        0 => snapshot,

        // The pair the specimen catalogue carries on purpose. Windows cannot hold both, because
        // the manager folds case when a service is created.
        1 => snapshot with
        {
            Entries =
            [
                .. snapshot.Entries,
                snapshot.Entries[0] with { ServiceName = snapshot.Entries[0].ServiceName.ToUpperInvariant() }
            ]
        },

        2 => snapshot with { Entries = [.. snapshot.Entries, null!] },
        3 => snapshot with { Entries = [.. snapshot.Entries, snapshot.Entries[0] with { ServiceName = null! }] },
        _ => snapshot with { Metadata = null! }
    };

    /// <summary>
    /// Which fields of one entry no comparison could be made on, worked out from the two sides
    /// rather than read off the answer.
    ///
    /// <b>An oracle from outside the engine, which is the only kind worth having here.</b> Asking
    /// the diff what it could not compare and then checking that it could not compare it is a
    /// test of nothing. This walks the two readings the generator produced and applies the rule
    /// in words: refused by either side, or asked by exactly one of them.
    ///
    /// The second half is what makes it an oracle rather than a copy - a field NEITHER side read
    /// is comparable, because both hold nothing and nothing equals nothing. That asymmetry is the
    /// engine's most easily broken line, and stating it here independently is what turns a
    /// mutation of it from missed into caught.
    /// </summary>
    internal static HashSet<string> Uncomparable(
        IReadOnlyList<ScmEntry> before, IReadOnlyList<ScmEntry> after, string name)
    {
        var left = Named(before, name);
        var right = Named(after, name);

        var uncomparable = left.Unreadable?.Keys.ToHashSet(StringComparer.Ordinal) ?? [];
        uncomparable.UnionWith(right.Unreadable?.Keys ?? Enumerable.Empty<string>());

        // Asked by one side and not the other. Both or neither is comparable - both because there
        // is something to compare, neither because two absences are equal.
        var oneSided = left.NotRead?.ToHashSet(StringComparer.Ordinal) ?? [];
        oneSided.SymmetricExceptWith(right.NotRead ?? Enumerable.Empty<string>());

        uncomparable.UnionWith(oneSided);

        return uncomparable;
    }

    /// <summary>How many fields these readings refused, and how many nobody asked about.</summary>
    internal static (int Refused, int Unasked) Unread(IReadOnlyList<ScmEntry> entries)
    {
        var documents = entries.Select(EntryDocument.From).ToList();

        return (documents.Sum(entry => entry.Unreadable?.Count ?? 0),
            documents.Sum(entry => entry.NotRead?.Count ?? 0));
    }

    internal static string Naming(IReadOnlyList<ScmEntry> entries) =>
        string.Join(", ", entries.Select(entry => entry.ServiceName));

    private static EntryDocument Named(IReadOnlyList<ScmEntry> entries, string name) =>
        EntryDocument.From(
            entries.First(entry => string.Equals(entry.ServiceName, name, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Keeps the first entry of each name.
    ///
    /// Without case, because that is how the engine matches its two sides and how Windows treats
    /// a service name. The generator draws from five names and lists of up to six, so collisions
    /// are the rule rather than the exception - and every one of them would otherwise be refused
    /// by the very door one property exists to test, leaving the rest checking nothing.
    /// </summary>
    private static IReadOnlyList<ScmEntry> Distinct(IReadOnlyList<ScmEntry> entries)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return [.. entries.Where(entry => seen.Add(entry.ServiceName))];
    }

    /// <summary>The four states a single field can be in, chosen by a die.</summary>
    private static Reading<T> HowItWasRead<T>(int how, T value) => how switch
    {
        0 => Reading<T>.Present(value),
        1 => Reading<T>.Absent(),
        2 => Reading<T>.NotRead(),
        _ => Reading<T>.Denied(5, "Access is denied.")
    };
}
