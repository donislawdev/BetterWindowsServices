using System.Globalization;
using Bws.Core;
using Bws.Core.History;

namespace Bws.Cli;

/// <summary>
/// The human readable shape of the change history: what the log can speak for, said on the error
/// channel, and one line per change on the data channel, newest first.
///
/// <b>The sentences go to the error channel because they are about the answer rather than part of
/// it</b> - the contract in docs/02 keeps the data channel for data, so a pipe gets rows and a person
/// at a terminal still reads the reach of the log before them, because it is printed first.
/// </summary>
internal static class HistoryText
{
    /// <summary>
    /// The sentences that qualify the history, in the order they are read.
    /// </summary>
    /// <param name="history">The whole history, for what the log can speak for.</param>
    /// <param name="shown">The changes about to be shown - all of them, or one entry's.</param>
    /// <param name="serviceName">The name asked about, or null for the whole machine.</param>
    internal static IReadOnlyList<string> Admissions(ChangeHistory history, IReadOnlyList<ServiceChange> shown, string? serviceName)
    {
        var lines = new List<string> { Reach(history.Log) };

        if (history.Log.Overwritten)
        {
            lines.Add(Texts.Of("cli.history.log.overwritten"));
        }

        if (history.Log.Full)
        {
            lines.Add(Texts.Of("cli.history.log.full"));
        }

        // An archived log keeps its past in other files whatever its size is now, so this is said every
        // time rather than at the limit - H02, 2026-10-27.
        if (history.Log.Mode == LogMode.AutoBackup)
        {
            lines.Add(Texts.Of("cli.history.log.autoBackup"));
        }

        if (serviceName is null && history.HasInstallations)
        {
            lines.Add(Texts.Of("cli.history.installedUnderDisplayName"));
        }

        if (serviceName is not null)
        {
            // Said whenever the log holds any, rather than counted against the name. The count would
            // claim to know which installation belongs to this entry, and that is exactly what the
            // event does not say.
            if (history.HasInstallations)
            {
                lines.Add(Texts.Of("cli.history.installationsNotByName"));
            }

            if (history.Unnamed > 0)
            {
                lines.Add(history.Unnamed == 1
                    ? Texts.Of("cli.history.unnamed.one", history.Unnamed)
                    : Texts.Of("cli.history.unnamed.many", history.Unnamed));
            }
        }

        if (shown.Count == 0)
        {
            lines.Add(serviceName is null
                ? Texts.Of("cli.history.nothing")
                : Texts.Of("cli.history.nothingFor", serviceName));
        }
        else
        {
            lines.Add(Texts.Of("cli.history.windowsWords"));
        }

        return lines;
    }

    /// <summary>One line per change, newest first, under a header - or nothing at all for no changes.</summary>
    internal static string Render(IReadOnlyList<ServiceChange> changes)
    {
        if (changes.Count == 0)
        {
            return string.Empty;
        }

        var rows = new List<string[]>(changes.Count + 1)
        {
            new[]
            {
                Texts.Of("cli.column.when"),
                Texts.Of("cli.column.by"),
                Texts.Of("cli.column.entry"),
                Texts.Of("cli.column.change")
            }
        };

        foreach (var change in changes)
        {
            // Every cell through Printable, before the widths are counted - the values were written
            // by whoever installed a service, and a line break in one turns one row into two.
            rows.Add(
            [
                Printable.Of(When(change.At)),
                Printable.Of(Who(change.By)),
                Printable.Of(Entry(change)),
                Printable.Of(What(change))
            ]);
        }

        return ListingTable.Layout(rows);
    }

    /// <summary>
    /// How far back the log reaches, as the first sentence - and only that.
    ///
    /// <b>"And has lost nothing since" went on 2026-10-27</b> (logic report H02): it was said whenever the
    /// log was neither at its limit nor full, and neither says the log was written to without a gap - a
    /// stopped log service, a log that stood full until somebody made it larger, an archived one.
    /// </summary>
    private static string Reach(LogCoverage log)
    {
        if (log.ReachesBack is not { } oldest)
        {
            return Texts.Of("cli.history.log.empty");
        }

        if (log.Cleared is { } cleared)
        {
            return cleared.By is null
                ? Texts.Of("cli.history.log.clearedBySomebody", When(oldest))
                : Texts.Of("cli.history.log.cleared", When(oldest), Who(cleared.By));
        }

        return Texts.Of("cli.history.log.reaches", When(oldest));
    }

    /// <summary>
    /// The time on this machine's clock, to the second - two changes a person makes in one minute
    /// are two lines, and with minutes only they would look like one change printed twice.
    /// </summary>
    private static string When(DateTimeOffset? at) =>
        at is { } time
            ? time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : Texts.Of("cli.history.notRecorded");

    /// <summary>The name when this machine could give one, and the SID itself otherwise.</summary>
    private static string Who(Actor? by) => by?.Name ?? by?.Sid ?? Texts.Of("cli.history.notRecorded");

    /// <summary>
    /// The entry by its service name - or its display name IN QUOTES when that is all the event has,
    /// which is every installation. The quotes are what tells the two apart on the screen, and a
    /// sentence above the table says what they mean.
    /// </summary>
    private static string Entry(ServiceChange change) =>
        change.ServiceName
        ?? (change.DisplayName is { } shown ? Texts.Of("cli.history.displayNameOnly", shown) : Texts.Of("cli.history.notRecorded"));

    private static string What(ServiceChange change) => change.Kind switch
    {
        ChangeKind.StartTypeChanged => Texts.Of(
            "cli.history.startTypeChanged",
            Word(change.Words.StartTypeBefore),
            Word(change.Words.StartTypeAfter)),

        ChangeKind.Installed => Installed(change),

        _ => throw new ArgumentOutOfRangeException(
            nameof(change), change.Kind, "A kind of change this text does not know how to say.")
    };

    /// <summary>
    /// The launch command last, because it is the long one, and what kind of entry it is, how it
    /// starts and the account it runs as in brackets before it - only the ones the event had.
    /// </summary>
    private static string Installed(ServiceChange change)
    {
        string[] details = [.. new[] { change.Words.EntryType, change.Words.StartType, change.Account }
            .Where(detail => detail is not null)
            .Select(detail => detail!)];

        var path = Word(change.BinaryPath);

        return details.Length == 0
            ? Texts.Of("cli.history.installed", path)
            : Texts.Of("cli.history.installedWith", string.Join(", ", details), path);
    }

    private static string Word(string? word) => word ?? Texts.Of("cli.history.notRecorded");
}
