using System.Text.Json;
using Bws.Core;
using Bws.Core.Snapshots;

namespace Bws.Cli;

/// <summary>
/// The machine readable shape of a listing.
///
/// Thin on purpose. The shape itself is <see cref="EntryDocument"/> in the core, shared
/// with the snapshot, because they are one contract and not two: <c>docs/02</c> names the
/// snapshot model as the source for the listing's fields as well. Two mappings would be two
/// things to keep in step, and that goes wrong quietly - a field added to one and missing
/// from the other, found out when a snapshot turns out not to hold something the listing
/// shows.
///
/// What is left here is the arrangement: an array, indented, one field per line.
/// </summary>
internal static class ListingJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,

        // Display names as themselves. The default encoder escapes everything outside
        // ASCII, so on this machine every Polish name came out as a row of ł and
        // friends - valid JSON that no person reading a terminal can check against
        // services.msc. Found while building the snapshot, where the same default made the
        // file unreadable in a diff, and fixed in both because one of them printing names
        // and the other printing escapes would be a difference with no reason behind it.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal static string Render(IReadOnlyList<ScmEntry> entries) =>
        JsonSerializer.Serialize(entries.Select(EntryDocument.From).ToList(), Options);
}
