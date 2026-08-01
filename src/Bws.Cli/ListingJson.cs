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

    /// <summary>Field name to reason, for everything that was refused. Omitted when empty.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Unreadable { get; init; }

    internal static EntryJson From(ScmEntry entry)
    {
        var unreadable = new Dictionary<string, string>(StringComparer.Ordinal);

        Note(unreadable, nameof(entry.ProcessId), entry.ProcessId.Outcome, entry.ProcessId.Reason);
        Note(unreadable, nameof(entry.StartType), entry.StartType.Outcome, entry.StartType.Reason);
        Note(unreadable, nameof(entry.DelayedAuto), entry.DelayedAuto.Outcome, entry.DelayedAuto.Reason);
        Note(unreadable, nameof(entry.Account), entry.Account.Outcome, entry.Account.Reason);
        Note(unreadable, nameof(entry.DependsOn), entry.DependsOn.Outcome, entry.DependsOn.Reason);

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
            Unreadable = unreadable.Count == 0 ? null : unreadable
        };
    }

    private static void Note(
        Dictionary<string, string> unreadable, string field, ReadOutcome outcome, string? reason)
    {
        if (outcome == ReadOutcome.Denied)
        {
            unreadable[Camel(field)] = reason ?? "unreadable";
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
