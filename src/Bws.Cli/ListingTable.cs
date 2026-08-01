using System.Text;
using Bws.Core;

namespace Bws.Cli;

/// <summary>
/// The human readable shape of a listing.
///
/// One rule drives every choice here: a cell must never let "there is nothing" and
/// "I was not allowed to look" render the same way. An empty cell means the first.
/// Refusal says so out loud.
/// </summary>
internal static class ListingTable
{
    private const string Nothing = "";

    internal static string Render(IReadOnlyList<ScmEntry> entries)
    {
        var rows = new List<string[]>(entries.Count + 1);
        rows.Add([
            Texts.Of("cli.column.name"),
            Texts.Of("cli.column.displayName"),
            Texts.Of("cli.column.type"),
            Texts.Of("cli.column.status"),
            Texts.Of("cli.column.startType"),
            Texts.Of("cli.column.account"),
            Texts.Of("cli.column.processId")
        ]);

        foreach (var entry in entries)
        {
            rows.Add(
            [
                entry.ServiceName,
                entry.DisplayName,
                entry.EntryType.ToString(),
                entry.Status.ToString(),
                StartCell(entry),
                Cell(entry.Account, value => value),
                Cell(entry.ProcessId, value => value.ToString())
            ]);
        }

        return Layout(rows);
    }

    /// <summary>
    /// The start type, with what qualifies it said out loud next to it rather than in
    /// columns of their own. services.msc puts the delay in the same place, and two whole
    /// columns that are empty for most entries would cost more width than they earn.
    ///
    /// An automatic entry whose delay flag was refused says so. Printing a plain
    /// "Automatic" there would be a claim that it starts at boot, which is precisely what
    /// nobody managed to find out.
    ///
    /// A trigger is marked here for the same reason the delay is: it changes what the start
    /// type means. Measured on a real machine on 2026-08-01, four of the ten entries that
    /// looked like "automatic and not running" were waiting to be asked for rather than
    /// broken, and nothing on the screen said so.
    /// </summary>
    private static string StartCell(ScmEntry entry)
    {
        var startType = Cell(entry.StartType, value => value.ToString());

        var cell = entry.DelayedAuto.Outcome switch
        {
            ReadOutcome.Present when entry.DelayedAuto.Value => Texts.Of("cli.cell.startDelayed", startType),
            ReadOutcome.Denied => Texts.Of("cli.cell.startDelayedUnknown", startType),
            _ => startType
        };

        // Only a trigger that starts it. One that stops the service says nothing about
        // whether it will come up, and marking it here would answer a question nobody asked
        // with a fact about something else.
        var startsOnTrigger = entry.Triggers.IsPresent
            && entry.Triggers.Value!.Any(trigger => trigger.Action == TriggerAction.Start);

        return startsOnTrigger ? Texts.Of("cli.cell.startTrigger", cell) : cell;
    }

    private static string Cell<T>(Reading<T> reading, Func<T, string> show) => reading.Outcome switch
    {
        ReadOutcome.Present => show(reading.Value!),

        // Deliberately not blank and deliberately not a dash. Both of those read as
        // "nothing here", which is the one meaning this cell must never carry.
        ReadOutcome.Denied => Texts.Of("cli.cell.noAccess"),

        _ => Nothing
    };

    private static string Layout(List<string[]> rows)
    {
        var columns = rows[0].Length;
        var widths = new int[columns];

        foreach (var row in rows)
        {
            for (var column = 0; column < columns; column++)
            {
                widths[column] = Math.Max(widths[column], row[column].Length);
            }
        }

        var text = new StringBuilder();

        foreach (var row in rows)
        {
            for (var column = 0; column < columns; column++)
            {
                // The last column carries no trailing padding, so copying a line out of a
                // terminal does not bring invisible spaces with it.
                text.Append(column == columns - 1
                    ? row[column]
                    : row[column].PadRight(widths[column] + 2));
            }

            text.AppendLine();
        }

        return text.ToString().TrimEnd();
    }
}
