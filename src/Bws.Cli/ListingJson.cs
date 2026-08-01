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
