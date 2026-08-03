using System.Security.Principal;

namespace Bws.Core.Snapshots;

/// <summary>
/// What is known about the snapshot itself rather than about any service in it.
///
/// Every field here exists to answer a question a diff will ask months later, when the
/// person comparing two files has forgotten both. `D1` of the specification lists them and
/// the last one is the reason the list is not a formality.
/// </summary>
public sealed record SnapshotMetadata
{
    /// <summary>
    /// The version of the shape below.
    ///
    /// A snapshot is kept and compared against newer ones, so this has to travel with the
    /// file rather than being assumed from whatever build is reading it. A reader meeting a
    /// version it does not know must say so instead of guessing at the fields.
    /// </summary>
    public required int SchemaVersion { get; init; }

    /// <summary>Which machine this was taken on. The first thing a diff of two files needs.</summary>
    public required string Machine { get; init; }

    /// <summary>The operating system as it describes itself, for comparing across builds.</summary>
    public required string OperatingSystem { get; init; }

    /// <summary>
    /// When, in UTC, written in the round-trip format that sorts and parses without
    /// ambiguity.
    ///
    /// UTC and not local time, because these files travel: a snapshot from a server in
    /// another timezone compared against one taken here has to be orderable, and two local
    /// times are not. It costs the reader a conversion and saves them a whole class of
    /// mistake about which of two files came first.
    /// </summary>
    public required DateTimeOffset TakenAt { get; init; }

    /// <summary>Who ran it, as the account name Windows reports.</summary>
    public required string TakenBy { get; init; }

    /// <summary>
    /// Whether the session had administrator rights.
    ///
    /// **The field this list exists for.** Measured on 2026-08-01: without elevation the
    /// manager enumerates 807 entries where an elevated session sees 810, and the security
    /// descriptor is refused for five more. A snapshot taken without elevation and one taken
    /// with it are therefore two different documents, and a diff that does not know which is
    /// which will report deletions nobody performed.
    ///
    /// Asked through the built-in role, which compares the well-known identifier. Never by
    /// the name of the group: that comparison reads "Administrators" and answers no on a
    /// machine where the group is called something else, which is how a set of measurements
    /// in this project came to be recorded under the wrong heading.
    /// </summary>
    public required bool Elevated { get; init; }

    /// <summary>What the person taking it wanted their future self to know. Null when they said nothing.</summary>
    public required string? Note { get; init; }

    /// <summary>Which build wrote it, so that a difference in output can be traced to a change in us.</summary>
    public required string Tool { get; init; }

    /// <summary>
    /// Everything about the machine and the session, filled in from the machine itself.
    /// </summary>
    public static SnapshotMetadata Of(string? note, IClock clock)
    {
        // Read once and kept. A clock asked twice gives two answers, which is obvious
        // written out like this and was not obvious in the expression that rounded it: that
        // one took the ticks from one reading and the remainder from another, so the
        // rounding silently did nothing at all. The file went out with seven decimal places
        // and the mistake showed up only because two snapshots were compared.
        var now = clock.Now;

        return new SnapshotMetadata
        {
            SchemaVersion = Snapshot.CurrentSchemaVersion,
            Machine = Environment.MachineName,
            OperatingSystem = Environment.OSVersion.VersionString,

            // To the second. A snapshot of a machine is not an instant, it is the few
            // hundred milliseconds it took to read one, so seven decimal places would claim
            // precision the measurement does not have - and make the one line that
            // legitimately differs between two snapshots harder to read than it need be.
            TakenAt = new DateTimeOffset(now.Ticks - (now.Ticks % TimeSpan.TicksPerSecond), now.Offset),

            TakenBy = AccountName(),
            Elevated = IsElevated(),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            Tool = CoreAssembly.Version
        };
    }

    /// <remarks>
    /// Released, and it was not until 2026-08-03 - an identity holds a native token, and this one
    /// was taken and dropped while the method two lines below took its own and released it
    /// properly. One snapshot leaks one handle, which is nothing and is also the kind of thing
    /// that is only ever nothing until the code moves somewhere it runs repeatedly.
    /// </remarks>
    private static string AccountName()
    {
        using var identity = WindowsIdentity.GetCurrent();

        return identity.Name;
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();

        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}

/// <summary>
/// The state of every entry on a machine at one moment, plus what is known about that
/// moment.
///
/// Deliberately a plain record over documents rather than over <see cref="ScmEntry"/>. What
/// is written is what will be read back years later, and a snapshot that serialised the
/// live model would change shape every time the model did, silently.
/// </summary>
public sealed record Snapshot(SnapshotMetadata Metadata, IReadOnlyList<EntryDocument> Entries)
{
    /// <summary>
    /// The schema this build writes.
    ///
    /// One, and it stays one until a field changes meaning or disappears. Adding a field is
    /// not a break - a reader that does not know it ignores it, and a reader that expects it
    /// finds it missing in an older file, which is exactly what an older file means.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Freezes a listing.
    ///
    /// Entries are sorted by name, ignoring case and then breaking ties by the exact
    /// spelling. Sorting is what makes two files of the same machine diff to nothing in
    /// <c>git diff</c>, which is the whole promise of `ADR-6` - the manager's own order is
    /// not in any contract and has been observed to move. The tie-break matters because
    /// service names are compared without case: without it, two entries differing only in
    /// case could swap places between runs and show up as a change.
    /// </summary>
    public static Snapshot Of(IReadOnlyList<ScmEntry> entries, string? note, IClock clock) => new(
        SnapshotMetadata.Of(note, clock),
        [
            .. entries
                .OrderBy(entry => entry.ServiceName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.ServiceName, StringComparer.Ordinal)
                .Select(EntryDocument.From)
        ]);
}
