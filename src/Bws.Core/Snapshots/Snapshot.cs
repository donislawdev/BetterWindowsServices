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
    /// Windows down to the monthly update, as in "10.0.26200.9550". Null when it is not known.
    ///
    /// <b>Beside <see cref="OperatingSystem"/> rather than instead of it, schema five, 2026-09-30.</b>
    /// That field ends in ".0" where the update belongs, so two snapshots either side of a monthly
    /// update read as taken on the same Windows - stability report D-5. Changing what the old field
    /// says would be the quiet kind of break its row in docs/02 warns about, so the full number is
    /// a field of its own.
    ///
    /// <b>Not required, and that is what lets a version four file still be read.</b> Such a file
    /// has no such field, and it arrives as null - "not known", which a comparison says rather than
    /// treating as "the same". <see cref="SystemFacts.OperatingSystemVersion"/> gives null as well
    /// when the machine will not say, for the same reason.
    /// </summary>
    public string? OperatingSystemVersion { get; init; }

    /// <summary>
    /// The language the service manager names things in, as in "pl-PL". Null when it is not known.
    ///
    /// <b>The system's language, not the session's - measured, and then the owner's decision of
    /// 2026-09-30.</b> <see cref="SystemFacts.NamesLanguage"/> carries the measurement. Two snapshots
    /// whose managers name things in different languages differ on every translated display name and
    /// description without anybody having changed anything, so a comparison of two such files leaves
    /// those two fields out and says so.
    ///
    /// Not required, for the reason the field above gives.
    /// </summary>
    public string? NamesLanguage { get; init; }

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

            // Asked of Session rather than answered here, since 2026-08-05. The window has to
            // say the same thing to the person looking at a list, and a second implementation
            // of "am I elevated" would be a second reader of one fact.
            Elevated = Session.IsElevated(),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            Tool = CoreAssembly.Version,
            OperatingSystemVersion = SystemFacts.OperatingSystemVersion(),
            NamesLanguage = SystemFacts.NamesLanguage()
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
    /// <b>Two since 2026-08-25, when `perUserRole` joined the entry document.</b>
    ///
    /// <b>The sentence that stood here until that day was false, and it was the kind of false
    /// that decides things.</b> It said adding a field is not a break, because "a reader that
    /// does not know it ignores it, and a reader that expects it finds it missing in an older
    /// file". Neither half holds in this build. Every member of <see cref="EntryDocument"/> is
    /// `required`, so a file missing one does not deserialise at all - and SnapshotJson.TryRead
    /// refuses any version that is not exactly this number before it looks at a single field,
    /// which is the behaviour that comment described as impossible.
    ///
    /// <b>So an added field IS a break here, and the number is what makes the refusal legible.</b>
    /// Without the bump an older snapshot fails as a JSON error about a missing member, which
    /// tells its owner nothing. With it, the file is refused by name: it uses version 1 and this
    /// build reads version 2.
    ///
    /// <b>Why not the softer route.</b> Making the field nullable would let old files load and
    /// would put `null` on one side of every comparison against a fresh snapshot - a difference
    /// reported on 46 entries of 798 that nothing on the machine actually changed. A diff that
    /// invents changes is worse than a file that says it is too old, because the first is the
    /// promise this product is for.
    ///
    /// <b>TWO TO THREE ON 2026-08-26, AND THIS ONE CHANGES NO FIELD - it changes what four of them
    /// SAY.</b> Half the enumerated values were written through a lower-casing helper and half
    /// through ToString, so one document held <c>OwnProcess</c> beside <c>unrestricted</c>. They
    /// all go through ToString now, and <see cref="EntryDocument"/> carries the measurement that
    /// chose the direction.
    ///
    /// <b>A value changing is as breaking as a field changing, and it is the quieter of the two.</b>
    /// A missing field fails a deserialiser. A value that reads <c>Unrestricted</c> where a script
    /// expected <c>unrestricted</c> fails nothing at all - the comparison is simply false, the
    /// branch is not taken, and a report comes out saying nothing is wrong. So it takes the number,
    /// for the same reason the added field did: with it the file is refused by name, and its owner
    /// is told which version they have.
    ///
    /// <b>Comparing two files of the SAME version is unaffected either way</b>, because both sides
    /// were written by one build. What this protects is the reader comparing an old file with a new
    /// one, and the script keyed on a value rather than on a field.
    ///
    /// <b>THREE TO FOUR ON 2026-09-06, and it is an added field again: Dependents.</b> Who breaks
    /// if an entry stops joins what a snapshot holds - the owner's decision, taken with the whole
    /// price on the table. It belongs here for the reason memory does not: it is configuration
    /// rather than a measurement, so it reads the same twice in a row and moves only when somebody
    /// changes the machine, which is the one thing this file is for.
    ///
    /// <b>The refusal is the point of the bump, exactly as it was for two.</b> Every member of
    /// EntryDocument is required, so a version three file does not deserialise at all - and with
    /// the number moved it is refused by name rather than as a JSON error about a missing member,
    /// which tells its owner nothing.
    ///
    /// <b>What it costs somebody holding old snapshots, said rather than left to be met.</b> A
    /// file written before today cannot be compared against one written after it. That is the
    /// same trade taken on 2026-08-25 and again on 08-26, and it is taken for the same reason: a
    /// diff that loads both and invents differences is worse than a file that says it is too old.
    ///
    /// <b>FOUR TO FIVE ON 2026-09-30, AND THIS IS THE FIRST BUMP THAT KEEPS READING THE VERSION
    /// BEFORE IT</b> - stability report D-5, owner's decision. Two fields joined the METADATA, not the
    /// entries: the Windows version down to the monthly update, and the language the manager names
    /// things in. Neither is required, so a version four file deserialises with both as null, and
    /// null is an honest "not known" there - nothing in an entry changed, so no comparison of a four
    /// against a five can invent a difference out of the format. <see cref="OldestSchemaVersionRead"/>
    /// says how far back this build reads.
    ///
    /// <b>The number moves anyway, and for the reader going the other way.</b> A build of 0.3.0 reads
    /// only four, and the serialiser skips members it does not know - so without the bump an older
    /// build would read a five as a four, drop both fields without a word and compare as though
    /// nothing were missing. With it that build refuses the file by name.
    ///
    /// <b>FIVE TO SIX ON 2026-10-06, AND NOTHING IN THE SHAPE CHANGED - THE MEANING OF ONE FIELD DID.</b>
    /// Package SB, the owner's decision that day: <c>fileVersion</c> is the version the file carries in
    /// its own resource, where until then it could come from a language file beside the binary - on the
    /// machine this was written on, 382 of 801 entries said a version no file carries, and Explorer said
    /// otherwise. The bump is what lets a comparison tell the two apart: a six against an older file does
    /// not compare file versions at all and says so once (<see cref="ComparisonCaveats.FileVersionSourceDiffers"/>),
    /// instead of reporting hundreds of changes on a machine nobody touched. And an older build, which
    /// cannot know any of this, refuses a six by name rather than comparing two different questions.
    /// Four and five are still read - nothing in them is missing, only differently sourced.
    ///
    /// <b>SIX TO SEVEN ON 2026-10-27, AND A TRIGGER IS WHOLE</b> - logic report C01, the owner's decision
    /// to do it in that series rather than leave it in the backlog. Each trigger carries its subtype and its
    /// data items beside the kind and the action (<see cref="TriggerDocument"/>), where until then a file
    /// could not tell one RPC interface from another. Four to six are still read: a trigger in them has
    /// neither member and reads with both as null. A seven against an older file compares triggers by kind
    /// and action alone and says so once (<see cref="ComparisonCaveats.TriggerDetailDiffers"/>) - without
    /// that, every entry with a trigger would differ by format alone, 117 of 780 on the machine this was
    /// written on. And an older build refuses a seven by name rather than dropping what it cannot read.
    /// </summary>
    public const int CurrentSchemaVersion = 7;

    /// <summary>
    /// The oldest schema this build still reads. Four since 2026-09-30 - see the version five
    /// paragraph above for why that one bump, unlike every earlier one, could keep reading its
    /// predecessor.
    /// </summary>
    public const int OldestSchemaVersionRead = 4;

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

    /// <summary>
    /// Whether the two parts one of these is made of are both there.
    ///
    /// Asked rather than trusted from the type. This is a positional record, so its two parts
    /// are constructor parameters, and <c>required</c> does not reach those - a document saying
    /// <c>"metadata": null</c> deserialises perfectly well and then fails on the first line that
    /// reads it.
    /// </summary>
    internal bool MissingParts(out string? failure)
    {
        if (Metadata is null || Entries is null)
        {
            failure = "The parts a snapshot is made of are missing.";

            return true;
        }

        failure = null;

        return false;
    }

    /// <summary>
    /// Whether the entries are something a comparison can be run against.
    ///
    /// Four questions, and all four are asked of the document rather than trusted from the
    /// type. A required property does not reach the elements of a list, it does not reach a
    /// property spelled out with null after it, and nothing anywhere says a document holds each
    /// service once. The fourth - a null among the names of fields nobody read - joined on
    /// 2026-09-30, and it is the first question about the inside of an entry rather than its identity.
    ///
    /// <b>Names are compared without case, and that is a decision about what a snapshot is</b>
    /// rather than a detail of any one caller - owner's decision, 2026-08-03. Windows cannot hold
    /// two services whose names differ only in case, because the manager compares them that way
    /// when one is created, so a document carrying both describes no machine that exists.
    ///
    /// The specimen catalogue in the tests carries <c>Twin</c> and <c>TWIN</c> on purpose, to pin
    /// the tie-break that keeps two such names in a settled order when they are written. That is
    /// a fact about writing, and it is why a snapshot of the whole catalogue is deliberately not
    /// something that can be read back or compared.
    ///
    /// <b>Here rather than in the reader that first needed it, and that move is the point.</b>
    /// Until 2026-08-04 this lived inside <see cref="SnapshotJson"/>, and the comparison engine
    /// was safe only because every document it saw had come through that reader - a dependency
    /// recorded in a comment and in nothing else. The engine now asks the same question of
    /// whatever it is handed. One rule, one place, two callers: the shape this project already
    /// paid for once, when four copies of the same directory walk disagreed about their anchor.
    /// </summary>
    internal bool BrokenEntries(out string? failure)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < Entries.Count; index++)
        {
            var entry = Entries[index];

            if (entry is null)
            {
                // The position rather than a name, because there is no name to give - which is
                // the whole of what is wrong with it.
                failure = $"Entry {index + 1} is empty.";

                return true;
            }

            // `required` does not mean present. It makes the compiler insist on a value where
            // one is written in code, and says nothing about a document that spells the property
            // and puts null after it - which deserialises without complaint and then fails
            // wherever the name is used as identity.
            if (entry.ServiceName is null)
            {
                failure = $"Entry {index + 1} has no service name.";

                return true;
            }

            // EMPTY IS NOT A NAME EITHER, since 2026-10-27 - logic report S06. The manager holds no
            // entry without a name, so the document describes no machine, and a comparison would match
            // it against nothing and report it as an entry that came or went.
            if (entry.ServiceName.Length == 0)
            {
                failure = $"Entry {index + 1} has an empty service name.";

                return true;
            }

            // THE ELEMENTS OF ONE LIST, since 2026-09-30 - stability report D-3, measured: a copy of a
            // real snapshot with `"notRead": [null]` in one entry ended `bws snapshot diff` with
            // "Object reference not set to an instance of an object." and code 1. The comparison
            // turns each name on this list into the field it speaks about, and a null name is not
            // one. It is the only list whose elements are used that way - a null inside dependsOn
            // or triggers is compared as "nothing", which is an answer.
            if (entry.NotRead?.Any(field => field is null) == true)
            {
                failure = $"Entry {index + 1} ('{entry.ServiceName}') has an empty name in its list of fields nobody read.";

                return true;
            }

            if ((Contradiction(entry) ?? TriggerGap(entry, Metadata.SchemaVersion)) is { } said)
            {
                failure = $"Entry {index + 1} ('{entry.ServiceName}') {said}";

                return true;
            }

            if (!seen.Add(entry.ServiceName))
            {
                failure = $"More than one entry is called '{entry.ServiceName}'.";

                return true;
            }
        }

        failure = null;

        return false;
    }

    /// <summary>
    /// What one entry says about a field that cannot all be true, or nothing - since 2026-10-27, logic
    /// report S06, owner's decision "refuse contradictions".
    ///
    /// <b>Two shapes, and this build writes neither</b> (EntryDocument.From puts a field in one place by
    /// the outcome of its reading): a field named both as refused and as never asked about, and a field
    /// holding a value while either list says it was not read. Until that day both were read, and the
    /// comparison believed the list - a value beside "not read" was left out of the comparison, so a
    /// startup type that changed was reported as one nobody could compare.
    ///
    /// <b>Asked of the entry as the comparison sees it</b> (<see cref="SnapshotJson.Document"/>), so a
    /// field the comparison drops - memory - is not held against a list naming it. Unknown names and
    /// unknown values are left alone, as the decision said: a later build may write a field this one
    /// has never heard of.
    /// </summary>
    private static string? Contradiction(EntryDocument entry)
    {
        // Lists rather than sets, so the field named is the first one the file names - the same file gives
        // the same sentence every time. Both are a handful of names.
        var notRead = (entry.NotRead ?? []).Select(EntryDocument.Camel).ToList();
        var refused = (entry.Unreadable?.Keys ?? Enumerable.Empty<string>()).Select(EntryDocument.Camel).ToList();

        if (notRead.Count == 0 && refused.Count == 0)
        {
            return null;
        }

        if (notRead.FirstOrDefault(refused.Contains) is { } both)
        {
            return $"names '{both}' both as refused and as never asked about.";
        }

        var document = SnapshotJson.Document(entry);

        return notRead.Concat(refused).FirstOrDefault(field => document[field] is not null) is { } held
            ? $"holds a value for '{held}' and also says it was not read."
            : null;
    }

    /// <summary>
    /// Which trigger of a schema seven entry is not whole, or nothing - since 2026-10-27, the review of
    /// PR #97.
    ///
    /// <b>A seven always writes a data list for every trigger</b>, empty for the kinds that carry none,
    /// and every item in it with a type and at least one value (<see cref="TriggerData"/>). A trigger
    /// without them is a file that does not hold what its schema promises - and two such files would
    /// compare as equal on exactly what neither of them holds, which is the false "the same" a snapshot
    /// is for avoiding. A rule of schema seven only: an older file never wrote the data, and its triggers
    /// read with it as null. A trigger that is null as a whole is left alone, as it was before seven.
    /// </summary>
    private static string? TriggerGap(EntryDocument entry, int schema)
    {
        if (schema < ComparisonCaveats.WholeTriggersSince || entry.Triggers is null)
        {
            return null;
        }

        var gap = entry.Triggers.ToList().FindIndex(trigger => trigger is not null && !Whole(trigger));

        return gap < 0
            ? null
            : $"has trigger {gap + 1} without its whole data list, which a version {schema} snapshot always holds.";
    }

    /// <summary>A data list, and every item in it whole - see <see cref="TriggerDataDocument.IsWhole"/>.</summary>
    private static bool Whole(TriggerDocument trigger) => trigger.Data is { } data && data.All(TriggerDataDocument.IsWhole);
}
