using System.Globalization;
using Bws.Core;
using Bws.Core.History;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What the window says about the change history - the sentences about what the System log can speak
/// for, and the words of one change - in one place for the view of the whole machine and for the
/// section in the panel about one entry (J2, 2026-10-20).
///
/// <b>THE COMMAND LINE'S SENTENCES, IN THE WINDOW'S OWN LANGUAGE FILE.</b> Bws.Cli.HistoryText decides
/// the same things for <c>bws history</c>, and the window cannot reference the command line (it
/// references the core alone, a rule the layering guard holds). So the decisions are made again here
/// in the same order with the same reasons, and the words differ only where the window has no
/// command to point at.
///
/// <b>TIMES IN THE COMMAND LINE'S SHAPE, TO THE SECOND</b> - the same string <c>bws history</c> prints,
/// so a person holding the window beside a terminal reads one time, not two. To the second because two
/// changes in one minute would otherwise read as one change shown twice, and in one shape that sorts
/// as text, because a table column sorts what it shows.
///
/// <b>Every value from the log through <see cref="Printable"/></b>: a display name is written by
/// whoever installed a service, and a control character or a change of writing direction in one would
/// reach the screen as it came (`docs/09`, C-4).
/// </summary>
internal static class HistoryWords
{
    /// <summary>The time in the shape <c>bws history</c> prints it, or the word for not recorded.</summary>
    internal static string When(DateTimeOffset? at) =>
        at is { } time
            ? time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : Texts.Of("gui.history.notRecorded");

    /// <summary>The account's name when this machine could give one, and the SID itself otherwise.</summary>
    internal static string Who(Actor? by) => Printable.Of(by?.Name ?? by?.Sid ?? Texts.Of("gui.history.notRecorded"));

    /// <summary>
    /// The entry by its internal name - or its display name IN QUOTES when that is all the event has,
    /// which is every installation (J1-b). A sentence over the table says what the quotes mean.
    /// </summary>
    internal static string Entry(ServiceChange change) =>
        Printable.Of(change.ServiceName
            ?? (change.DisplayName is { } shown ? Texts.Of("gui.history.displayNameOnly", shown) : Texts.Of("gui.history.notRecorded")));

    /// <summary>What changed, in the words Windows wrote at the time (J1-d).</summary>
    internal static string What(ServiceChange change) => Printable.Of(change.Kind switch
    {
        ChangeKind.StartTypeChanged => Texts.Of(
            "gui.history.startTypeChanged", Word(change.Words.StartTypeBefore), Word(change.Words.StartTypeAfter)),

        ChangeKind.Installed => Installed(change),

        _ => throw new ArgumentOutOfRangeException(nameof(change), change.Kind, "A kind of change the window does not know how to say.")
    });

    /// <summary>
    /// The sentences that qualify a history, in the order they are read - how far back the log
    /// reaches first, because a week with no changes in it reads exactly like a log that started
    /// yesterday unless somebody says which it is.
    /// </summary>
    /// <param name="history">The whole history, for what the log can speak for.</param>
    /// <param name="shown">The changes the sentences stand over - all of them, or one entry's.</param>
    /// <param name="oneEntry">True under the panel about one entry, false over the whole machine.</param>
    internal static IReadOnlyList<string> Admissions(ChangeHistory history, IReadOnlyList<ServiceChange> shown, bool oneEntry)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(shown);

        var lines = new List<string> { Reach(history.Log) };

        if (history.Log.Overwritten)
        {
            lines.Add(Texts.Of("gui.history.log.overwritten"));
        }

        if (history.Log.Full)
        {
            lines.Add(Texts.Of("gui.history.log.full"));
        }

        if (!oneEntry && history.HasInstallations)
        {
            lines.Add(Texts.Of("gui.history.installedUnderDisplayName"));
        }

        if (oneEntry)
        {
            // Said whenever the log holds any, rather than counted against the entry - a count would
            // claim to know which installation is this entry's, which is what the event does not say.
            if (history.HasInstallations)
            {
                lines.Add(Texts.Of("gui.history.installationsNotByName"));
            }

            if (history.Unnamed > 0)
            {
                lines.Add(history.Unnamed == 1
                    ? Texts.Of("gui.history.unnamed.one", history.Unnamed)
                    : Texts.Of("gui.history.unnamed.many", history.Unnamed));
            }
        }

        // Each key whole at its own call - TextKeyGuards finds a key by the call it stands in.
        lines.Add(shown.Count > 0 ? Texts.Of("gui.history.windowsWords")
            : oneEntry ? Texts.Of("gui.history.nothingFor")
            : Texts.Of("gui.history.nothing"));

        return lines;
    }

    /// <summary>
    /// How far back the log reaches. "Has lost nothing since" only when nothing says otherwise - the
    /// command line's rule, and the reason it is a sentence of its own.
    /// </summary>
    private static string Reach(LogCoverage log)
    {
        if (log.ReachesBack is not { } oldest)
        {
            return Texts.Of("gui.history.log.empty");
        }

        if (log.Cleared is { } cleared)
        {
            return cleared.By is null
                ? Texts.Of("gui.history.log.clearedBySomebody", When(oldest))
                : Texts.Of("gui.history.log.cleared", When(oldest), Who(cleared.By));
        }

        return log.Overwritten || log.Full
            ? Texts.Of("gui.history.log.reaches", When(oldest))
            : Texts.Of("gui.history.log.since", When(oldest));
    }

    /// <summary>
    /// The launch command last, because it is the long one, and what kind of entry it is, how it starts
    /// and the account it runs as in brackets before it - only the ones the event had.
    /// </summary>
    private static string Installed(ServiceChange change)
    {
        string[] details = [.. new[] { change.Words.EntryType, change.Words.StartType, change.Account }
            .Where(detail => detail is not null)
            .Select(detail => detail!)];

        var path = Word(change.BinaryPath);

        return details.Length == 0
            ? Texts.Of("gui.history.installed", path)
            : Texts.Of("gui.history.installedWith", string.Join(Texts.Of("gui.cell.list.separator"), details), path);
    }

    private static string Word(string? word) => word ?? Texts.Of("gui.history.notRecorded");
}
