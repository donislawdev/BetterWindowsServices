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
    public required string? Account { get; init; }

    /// <summary>Field name to reason, for everything that was refused. Omitted when empty.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Unreadable { get; init; }

    internal static EntryJson From(ScmEntry entry)
    {
        var unreadable = new Dictionary<string, string>(StringComparer.Ordinal);

        Note(unreadable, nameof(entry.ProcessId), entry.ProcessId.Outcome, entry.ProcessId.Reason);
        Note(unreadable, nameof(entry.StartType), entry.StartType.Outcome, entry.StartType.Reason);
        Note(unreadable, nameof(entry.Account), entry.Account.Outcome, entry.Account.Reason);

        return new EntryJson
        {
            ServiceName = entry.ServiceName,
            DisplayName = entry.DisplayName,
            EntryType = entry.EntryType.ToString(),
            Status = entry.Status.ToString(),
            ProcessId = entry.ProcessId.IsPresent ? entry.ProcessId.Value : null,
            StartType = entry.StartType.IsPresent ? entry.StartType.Value.ToString() : null,
            Account = entry.Account.IsPresent ? entry.Account.Value : null,
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
