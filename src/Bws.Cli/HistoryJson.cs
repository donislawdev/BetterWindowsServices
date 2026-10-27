using System.Text.Json;
using Bws.Core.History;

namespace Bws.Cli;

/// <summary>
/// The machine readable shape of the change history - a public contract from the day it is printed,
/// with every name from the glossary (docs/03, part 4) and the decisions J1-a to J1-d of 2026-10-15.
///
/// <b>Every field in every change, null written out.</b> A start type change has no launch command and
/// an installation has no "before", and a reader that has to tell "not this kind" from "field missing"
/// is a reader that will one day guess - the same reasoning that writes startType as null on the plan
/// steps that do not set one.
///
/// <b>The log first, because it qualifies everything after it.</b> A script deciding whether "no
/// changes" means no changes needs reachesBack, overwritten and full before it reads the array.
/// </summary>
internal static class HistoryJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,

        // The escaping AsciiJson argues for, so a display name in Polish survives the console on its
        // way into a file or a pipe.
        Encoder = AsciiJson.Encoder
    };

    internal static string Render(LogCoverage log, IReadOnlyList<ServiceChange> changes)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(changes);

        var document = new HistoryDocument(
            new LogDocument(log.Name, log.ReachesBack, log.Overwritten, log.Full, Cleared(log.Cleared), log.Mode.ToString()),
            [.. changes.Select(Change)]);

        return AsciiJson.Serialize(document, Options);
    }

    private static ClearedDocument? Cleared(Clearing? cleared) =>
        cleared is null ? null : new ClearedDocument(cleared.At, By(cleared.By));

    private static ActorDocument? By(Actor? by) => by is null ? null : new ActorDocument(by.Sid, by.Name);

    private static ChangeDocument Change(ServiceChange change) => new(
        change.At,

        // Spelled as the enumeration spells it, rule 5 of docs/03 - MachineDocumentValueGuards holds
        // every member to that.
        change.Kind.ToString(),
        change.ServiceName,
        change.DisplayName,
        By(change.By),
        change.BinaryPath,
        change.Account,
        new WordsDocument(
            change.Words.StartTypeBefore, change.Words.StartTypeAfter, change.Words.EntryType, change.Words.StartType));

    // The order of the members below IS the order of the keys in the document.
    private sealed record HistoryDocument(LogDocument Log, IReadOnlyList<ChangeDocument> Changes);

    /// <param name="Mode">
    /// What Windows does at the size limit, spelled as <see cref="LogMode"/> spells it - added 2026-10-27
    /// (logic report H02) and LAST, so the document only grew at its end.
    /// </param>
    private sealed record LogDocument(
        string Name, DateTimeOffset? ReachesBack, bool Overwritten, bool Full, ClearedDocument? Cleared, string Mode);

    private sealed record ClearedDocument(DateTimeOffset? At, ActorDocument? By);

    private sealed record ActorDocument(string Sid, string? Name);

    private sealed record ChangeDocument(
        DateTimeOffset? At,
        string Kind,
        string? ServiceName,
        string? DisplayName,
        ActorDocument? By,
        string? BinaryPath,
        string? Account,
        WordsDocument WindowsWords);

    private sealed record WordsDocument(string? StartTypeBefore, string? StartTypeAfter, string? EntryType, string? StartType);
}
