using System.Text.Json;
using System.Text.Json.Serialization;
using Bws.Core;

namespace Bws.Cli;

/// <summary>
/// The machine readable shape of a listing. This is a PUBLIC CONTRACT: people will
/// build scripts and monitoring on these field names, so renaming one is a breaking
/// change and not a tidy-up. Names come from the binding column of the glossary.
///
/// The interesting part is how an unreadable field is expressed. A value that could
/// not be read must not look like a value that is simply not there: the first is a
/// fact about our permissions, the second is a fact about the service, and a snapshot
/// that confuses them reports hundreds of changes that never happened.
///
/// So the shape is: the field itself carries the value or null, and every field that
/// was refused is named in "unreadable" together with the reason. A consumer that does
/// not care sees null. A consumer that does care can tell the two cases apart.
/// </summary>
/// <summary>
/// Why one field could not be read: the system's number and the system's sentence.
///
/// Both, because they are for different readers and only one of them survives a change of
/// machine. Measured on 2026-08-01: the same refusal comes back in English on one install
/// and in Polish on another, because Windows answers in the language of the machine. The
/// number is what a script may key on, the sentence is what a person reads.
/// </summary>
internal sealed record UnreadableJson(int ErrorCode, string Message);

/// <summary>One condition that starts or stops the entry by itself.</summary>
internal sealed record TriggerJson(string Kind, string Action);

/// <summary>
/// What the system concluded about a file's signature.
///
/// The number travels with the name for the same reason a refusal carries its error code:
/// the naming is ours and can gain a value later, while the verification result is the
/// system's and does not move. A reader meeting "unknown" still has something to look up.
/// </summary>
internal sealed record SignatureJson(string Status, int ResultCode, string? Publisher);

internal sealed record EntryJson
{
    public required string ServiceName { get; init; }
    public required string DisplayName { get; init; }
    public required string EntryType { get; init; }
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
    public required IReadOnlyList<TriggerJson>? Triggers { get; init; }

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
    /// Null when nobody asked, which is the ordinary case: a listing does not verify
    /// signatures unless it is told to, because doing so costs about three seconds. Told
    /// apart from "there is no signature" by "notRead", the same as every other field here.
    /// </summary>
    public required SignatureJson? Signature { get; init; }

    /// <summary>The version the file claims for itself. Null when it carries no version resource.</summary>
    public required string? FileVersion { get; init; }

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

    /// <summary>Field name to refusal, for everything that was refused. Omitted when empty.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, UnreadableJson>? Unreadable { get; init; }

    /// <summary>
    /// Fields nobody asked for on this run. Omitted when empty.
    ///
    /// The fourth state, and the one ADR-13 exists for. Without it "no triggers" and
    /// "nobody looked for triggers" are the same null, and those two say opposite things
    /// about a stopped service: one is broken, the other is waiting.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? NotRead { get; init; }

    internal static EntryJson From(ScmEntry entry)
    {
        var unreadable = new Dictionary<string, UnreadableJson>(StringComparer.Ordinal);
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
        Note(unreadable, notRead, nameof(entry.RequiredPrivileges), entry.RequiredPrivileges);
        Note(unreadable, notRead, nameof(entry.SidType), entry.SidType);
        Note(unreadable, notRead, nameof(entry.SecurityDescriptor), entry.SecurityDescriptor);

        return new EntryJson
        {
            ServiceName = entry.ServiceName,
            DisplayName = entry.DisplayName,
            EntryType = entry.EntryType.ToString(),
            Status = entry.Status.ToString(),
            ProcessId = entry.ProcessId.IsPresent ? entry.ProcessId.Value : null,
            StartType = entry.StartType.IsPresent ? entry.StartType.Value.ToString() : null,
            DelayedAuto = entry.DelayedAuto.IsPresent ? entry.DelayedAuto.Value : null,
            Account = entry.Account.IsPresent ? entry.Account.Value : null,
            DependsOn = entry.DependsOn.IsPresent ? entry.DependsOn.Value : null,

            Triggers = entry.Triggers.IsPresent
                ? [.. entry.Triggers.Value!.Select(trigger =>
                    new TriggerJson(Camel(trigger.Kind.ToString()), Camel(trigger.Action.ToString())))]
                : null,

            BinaryPath = entry.BinaryPath.IsPresent ? entry.BinaryPath.Value : null,
            BinaryFile = entry.BinaryFile.IsPresent ? entry.BinaryFile.Value : null,
            BinaryOnDisk = entry.BinaryOnDisk.IsPresent ? entry.BinaryOnDisk.Value : null,

            Signature = entry.Signature.IsPresent
                ? new SignatureJson(
                    Camel(entry.Signature.Value!.Status.ToString()),
                    entry.Signature.Value.ResultCode,
                    entry.Signature.Value.Publisher)
                : null,

            FileVersion = entry.FileVersion.IsPresent ? entry.FileVersion.Value : null,

            RequiredPrivileges = entry.RequiredPrivileges.IsPresent ? entry.RequiredPrivileges.Value : null,
            SidType = entry.SidType.IsPresent ? Camel(entry.SidType.Value.ToString()) : null,
            SecurityDescriptor = entry.SecurityDescriptor.IsPresent ? entry.SecurityDescriptor.Value : null,

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
        Dictionary<string, UnreadableJson> unreadable, List<string> notRead, string field, Reading<T> reading)
    {
        if (reading.Outcome == ReadOutcome.Denied)
        {
            unreadable[Camel(field)] = new UnreadableJson(reading.ErrorCode, reading.Reason!);
        }

        if (reading.Outcome == ReadOutcome.NotRead)
        {
            notRead.Add(Camel(field));
        }
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}

internal static class ListingJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    internal static string Render(IReadOnlyList<ScmEntry> entries) =>
        JsonSerializer.Serialize(entries.Select(EntryJson.From).ToList(), Options);
}
