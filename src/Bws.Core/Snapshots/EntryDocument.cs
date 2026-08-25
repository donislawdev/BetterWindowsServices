using System.Text.Json.Serialization;

namespace Bws.Core.Snapshots;

/// <summary>
/// Why one field could not be read: the system's number and the system's sentence.
///
/// Both, because they are for different readers and only one of them survives a change of
/// machine. Measured on 2026-08-01: the same refusal comes back in English on one install
/// and in Polish on another, because Windows answers in the language of the machine. The
/// number is what a script may key on, the sentence is what a person reads.
/// </summary>
public sealed record UnreadableDocument(int ErrorCode, string Message);

/// <summary>One condition that starts or stops the entry by itself.</summary>
public sealed record TriggerDocument(string Kind, string Action);

/// <summary>
/// What the system concluded about a file's signature.
///
/// The number travels with the name for the same reason a refusal carries its error code:
/// the naming is ours and can gain a value later, while the verification result is the
/// system's and does not move. A reader meeting "unknown" still has something to look up.
/// </summary>
public sealed record SignatureDocument(string Status, int ResultCode, string? Publisher);

/// <summary>
/// What one process is using, in bytes, and how many entries have to share that answer.
///
/// Bytes rather than megabytes, because this half of the output is for programs and a
/// program that wants megabytes can divide. Rounding here would be this layer deciding how
/// precise somebody else's question is allowed to be.
///
/// <paramref name="SharedBy"/> travels with the numbers rather than being left for a reader
/// to work out from the process ids. Five services in one process each reporting the same
/// 36 MB is correct and adds up to five times the truth, and the only thing standing
/// between those two readings is this field.
/// </summary>
public sealed record MemoryDocument(long WorkingSet, long Commit, int SharedBy);

/// <summary>
/// One entry, in the shape both the machine readable listing and the snapshot use.
///
/// One shape rather than two, and that is what <c>docs/02</c> already says: the snapshot
/// model is named there as the source for the listing columns as well. Two mappings of the
/// same fields would be two things to keep in step, and the way that fails is silent - a
/// field added to one, missing from the other, noticed when a snapshot turns out not to
/// hold something the listing shows.
///
/// This is a PUBLIC CONTRACT twice over. People build scripts on these names and they keep
/// snapshots for months and compare them against new ones, so renaming a field is a
/// breaking change needing a schema version, not a tidy-up. Names come from the binding
/// column of the glossary.
///
/// The interesting part is how an unreadable field is expressed. A value that could not be
/// read must not look like a value that is simply not there: the first is a fact about our
/// permissions, the second is a fact about the service, and a snapshot that confuses them
/// reports hundreds of changes that never happened. So the field itself carries the value
/// or null, and everything refused is named in "unreadable" with the reason. A consumer
/// that does not care sees null. One that does can tell the two apart.
/// </summary>
public sealed record EntryDocument
{
    public required string ServiceName { get; init; }
    public required string DisplayName { get; init; }

    /// <summary>
    /// What the entry says it is for. Added 2026-08-12, and adding a field is not a break -
    /// see <see cref="Snapshot.CurrentSchemaVersion"/>, which stays at one for exactly this case.
    ///
    /// <b>Null covers two different answers and the third tells them apart</b>, like every field
    /// here: 384 entries of 819 genuinely have none, and ten have one the manager could not turn
    /// into words. The second kind is named in "unreadable" and the first is not.
    ///
    /// <b>It can carry a newline, which is new for this format</b> - two of 819 do. JSON escapes
    /// it, so nothing downstream has to change, but anything printing a snapshot field on one
    /// line of a terminal now has a case it did not have before.
    /// </summary>
    public required string? Description { get; init; }

    public required string EntryType { get; init; }

    /// <summary>
    /// Which side of the per-user family the entry is on: None, Template or Instance.
    ///
    /// <b>Not folded into <see cref="EntryType"/>, on the same grounds as DelayedAuto below.</b>
    /// A per-user template that shares a process still reports SharedProcess there, because it
    /// is one, and anything already reading that field keeps working. This answers a different
    /// question and gets its own name.
    ///
    /// <b>Never null, and that is a claim about where it comes from.</b> The value is read from
    /// the type bits that arrive with every enumerated entry, on the call that produced the
    /// entry at all - so there is no machine state where it is refused and none where nobody
    /// asked. It is the only field here outside identity with no story about being missing.
    /// </summary>
    public required string PerUserRole { get; init; }

    public required string Status { get; init; }
    public required int? ProcessId { get; init; }
    public required string? StartType { get; init; }

    /// <summary>
    /// Added rather than folded into <see cref="StartType"/>, so the value of that field
    /// stays what it always was. A delayed entry still reports "Automatic", because it is
    /// automatic, and anything already reading this output keeps working.
    ///
    /// Null on everything that is not automatic, where the idea does not apply, and on
    /// anything the flag could not be read for. Those two are told apart by "unreadable",
    /// same as every other field here.
    /// </summary>
    public required bool? DelayedAuto { get; init; }

    public required string? Account { get; init; }

    /// <summary>
    /// What the entry declares it needs, exactly as the manager returns it, including the
    /// leading plus that marks a load order group. Null when it declares nothing, which is
    /// the ordinary case for well over a third of the listing.
    /// </summary>
    public required IReadOnlyList<string>? DependsOn { get; init; }

    /// <summary>
    /// What starts or stops this entry by itself. Null when it has none, which is the
    /// ordinary case, and null as well when nobody asked - told apart by "notRead".
    /// </summary>
    public required IReadOnlyList<TriggerDocument>? Triggers { get; init; }

    /// <summary>
    /// The launch command whole, exactly as the manager returns it, arguments included.
    /// Null on the entries that name nothing, all of which are drivers the manager has a
    /// default for.
    /// </summary>
    public required string? BinaryPath { get; init; }

    /// <summary>
    /// Which file that command runs, resolved to an absolute path.
    ///
    /// Alongside the raw value rather than instead of it. The raw one is what a snapshot has
    /// to keep and a diff has to compare, and this one is what makes
    /// <see cref="BinaryOnDisk"/> checkable by a person - being told a file is missing
    /// without being told which file is not a report anybody can act on.
    /// </summary>
    public required string? BinaryFile { get; init; }

    /// <summary>Whether that file is there. Null when the entry names no file at all.</summary>
    public required bool? BinaryOnDisk { get; init; }

    /// <summary>
    /// What the system thinks of the signature on that file.
    ///
    /// Null when nobody asked, which is the ordinary case for a listing: signatures are not
    /// verified unless asked for, because doing so costs several seconds. A snapshot always
    /// has them, because a snapshot is taken deliberately and being comparable matters more
    /// there than being quick.
    /// </summary>
    public required SignatureDocument? Signature { get; init; }

    /// <summary>The version the file claims for itself. Null when it carries no version resource.</summary>
    public required string? FileVersion { get; init; }

    /// <summary>
    /// SHA-256 of that file, lower case hexadecimal.
    ///
    /// What answers "is this the same file" when a signature cannot: a binary swapped for
    /// another one signed by the same publisher keeps its signature and changes this.
    /// </summary>
    public required string? BinaryHash { get; init; }

    /// <summary>
    /// What the entry asks the manager to leave in its token, by name and in the manager's
    /// own casing. Null when it declares nothing - which is the permissive case, not the
    /// careful one: an entry naming no privileges keeps everything its account has.
    /// </summary>
    public required IReadOnlyList<string>? RequiredPrivileges { get; init; }

    /// <summary>
    /// Whether the entry has an identity of its own: "unrestricted", "restricted" or null.
    ///
    /// Null covers two different things, told apart the same way as everywhere else here:
    /// an entry with no identity of its own is simply null, and an entry nobody could read
    /// is named in "unreadable". Windows calls the first case NONE, and it is expressed as
    /// null rather than as a third value because it is the absence of a SID rather than a
    /// kind of one.
    /// </summary>
    public required string? SidType { get; init; }

    /// <summary>
    /// Who may do what to this entry, in the text form the system reads and writes, with
    /// owner, group and permissions.
    ///
    /// The audit list is not in it. Reading that needs a privilege an elevated session does
    /// not have enabled, and asking for it fails the whole read - so this differs from
    /// <c>sc sdshow</c> in both directions: it carries owner and group, which sc does not
    /// show, and not the audit list, which sc does.
    /// </summary>
    public required string? SecurityDescriptor { get; init; }

    /// <summary>How hard the system takes a failure to start during boot.</summary>
    public required string? ErrorControl { get; init; }

    /// <summary>The load order group this entry belongs to. Null for most entries.</summary>
    public required string? LoadOrderGroup { get; init; }

    /// <summary>
    /// What the process behind this entry is using. Null when the entry is not running,
    /// which is the ordinary case, and null as well when nobody asked - told apart by
    /// "notRead".
    ///
    /// Never written into a snapshot. It is the one thing here that is a measurement rather
    /// than a description of how the machine is set up, so it is different a second later
    /// and would be nothing but noise in a document meant to be compared with another one.
    /// See <see cref="SnapshotJson"/>, which drops it.
    ///
    /// The only field here that is not required, and that follows from the sentence above
    /// rather than being a convenience: a snapshot genuinely does not carry it, so a
    /// document type demanding it could not read one of its own files back. Found by a test
    /// that wrote a snapshot and read it again, which is worth saying because nothing about
    /// writing one shows it.
    /// </summary>
    public MemoryDocument? Memory { get; init; }

    /// <summary>Field name to refusal, for everything that was refused. Omitted when empty.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, UnreadableDocument>? Unreadable { get; init; }

    /// <summary>
    /// Fields nobody asked for on this run. Omitted when empty.
    ///
    /// The fourth state, and the one ADR-13 exists for. Without it "no triggers" and
    /// "nobody looked for triggers" are the same null, and those two say opposite things
    /// about a stopped service: one is broken, the other is waiting.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? NotRead { get; init; }

    public static EntryDocument From(ScmEntry entry)
    {
        var unreadable = new Dictionary<string, UnreadableDocument>(StringComparer.Ordinal);
        var notRead = new List<string>();

        Note(unreadable, notRead, nameof(entry.ProcessId), entry.ProcessId);
        Note(unreadable, notRead, nameof(entry.StartType), entry.StartType);
        Note(unreadable, notRead, nameof(entry.DelayedAuto), entry.DelayedAuto);
        Note(unreadable, notRead, nameof(entry.Account), entry.Account);
        Note(unreadable, notRead, nameof(entry.DependsOn), entry.DependsOn);
        Note(unreadable, notRead, nameof(entry.Triggers), entry.Triggers);
        Note(unreadable, notRead, nameof(entry.BinaryPath), entry.BinaryPath);
        Note(unreadable, notRead, nameof(entry.BinaryFile), entry.BinaryFile);
        Note(unreadable, notRead, nameof(entry.BinaryOnDisk), entry.BinaryOnDisk);
        Note(unreadable, notRead, nameof(entry.Signature), entry.Signature);
        Note(unreadable, notRead, nameof(entry.FileVersion), entry.FileVersion);
        Note(unreadable, notRead, nameof(entry.BinaryHash), entry.BinaryHash);
        Note(unreadable, notRead, nameof(entry.RequiredPrivileges), entry.RequiredPrivileges);
        Note(unreadable, notRead, nameof(entry.SidType), entry.SidType);
        Note(unreadable, notRead, nameof(entry.SecurityDescriptor), entry.SecurityDescriptor);
        Note(unreadable, notRead, nameof(entry.ErrorControl), entry.ErrorControl);
        Note(unreadable, notRead, nameof(entry.LoadOrderGroup), entry.LoadOrderGroup);
        Note(unreadable, notRead, nameof(entry.Description), entry.Description);
        Note(unreadable, notRead, nameof(entry.Memory), entry.Memory);

        return new EntryDocument
        {
            ServiceName = entry.ServiceName,
            DisplayName = entry.DisplayName,
            Description = entry.Description.IsPresent ? entry.Description.Value : null,
            EntryType = entry.EntryType.ToString(),
            PerUserRole = entry.PerUserRole.ToString(),
            Status = entry.Status.ToString(),
            ProcessId = entry.ProcessId.IsPresent ? entry.ProcessId.Value : null,
            StartType = entry.StartType.IsPresent ? entry.StartType.Value.ToString() : null,
            DelayedAuto = entry.DelayedAuto.IsPresent ? entry.DelayedAuto.Value : null,
            Account = entry.Account.IsPresent ? entry.Account.Value : null,
            DependsOn = entry.DependsOn.IsPresent ? entry.DependsOn.Value : null,

            Triggers = entry.Triggers.IsPresent
                ? [.. entry.Triggers.Value!.Select(trigger =>
                    new TriggerDocument(Camel(trigger.Kind.ToString()), Camel(trigger.Action.ToString())))]
                : null,

            BinaryPath = entry.BinaryPath.IsPresent ? entry.BinaryPath.Value : null,
            BinaryFile = entry.BinaryFile.IsPresent ? entry.BinaryFile.Value : null,
            BinaryOnDisk = entry.BinaryOnDisk.IsPresent ? entry.BinaryOnDisk.Value : null,

            Signature = entry.Signature.IsPresent
                ? new SignatureDocument(
                    Camel(entry.Signature.Value!.Status.ToString()),
                    entry.Signature.Value.ResultCode,
                    entry.Signature.Value.Publisher)
                : null,

            FileVersion = entry.FileVersion.IsPresent ? entry.FileVersion.Value : null,
            BinaryHash = entry.BinaryHash.IsPresent ? entry.BinaryHash.Value : null,

            RequiredPrivileges = entry.RequiredPrivileges.IsPresent ? entry.RequiredPrivileges.Value : null,
            SidType = entry.SidType.IsPresent ? Camel(entry.SidType.Value.ToString()) : null,
            SecurityDescriptor = entry.SecurityDescriptor.IsPresent ? entry.SecurityDescriptor.Value : null,

            ErrorControl = entry.ErrorControl.IsPresent ? Camel(entry.ErrorControl.Value.ToString()) : null,
            LoadOrderGroup = entry.LoadOrderGroup.IsPresent ? entry.LoadOrderGroup.Value : null,

            Memory = entry.Memory.IsPresent
                ? new MemoryDocument(
                    entry.Memory.Value!.WorkingSet,
                    entry.Memory.Value.Commit,
                    entry.Memory.Value.SharedBy)
                : null,

            Unreadable = unreadable.Count == 0 ? null : unreadable,
            NotRead = notRead.Count == 0 ? null : notRead
        };
    }

    /// <summary>
    /// Takes the whole reading rather than its pieces. The pieces used to be handed over
    /// one by one, which needed a fallback for a reason that cannot be missing, and a
    /// fallback for something impossible is a sentence nobody ever reads and nobody ever
    /// checks.
    /// </summary>
    private static void Note<T>(
        Dictionary<string, UnreadableDocument> unreadable, List<string> notRead, string field, Reading<T> reading)
    {
        if (reading.Outcome == ReadOutcome.Denied)
        {
            unreadable[Camel(field)] = new UnreadableDocument(reading.ErrorCode, reading.Reason!);
        }

        if (reading.Outcome == ReadOutcome.NotRead)
        {
            notRead.Add(Camel(field));
        }
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
