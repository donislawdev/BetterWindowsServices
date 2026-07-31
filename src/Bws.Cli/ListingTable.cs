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
    /// <summary>
    /// Shown where a value was refused. Deliberately not blank, and deliberately not a
    /// dash either, because both read as "nothing here" at a glance.
    /// </summary>
    private const string Refused = "(no access)";

    private const string Nothing = "";

    internal static string Render(IReadOnlyList<ScmEntry> entries)
    {
        var rows = new List<string[]>(entries.Count + 1);
        rows.Add(["NAME", "DISPLAY NAME", "TYPE", "STATUS", "START", "ACCOUNT", "PID"]);

        foreach (var entry in entries)
        {
            rows.Add(
            [
                entry.ServiceName,
                entry.DisplayName,
                entry.EntryType.ToString(),
                entry.Status.ToString(),
                Cell(entry.StartType, value => value.ToString()),
                Cell(entry.Account, value => value),
                Cell(entry.ProcessId, value => value.ToString())
            ]);
        }

        return Layout(rows);
    }

    private static string Cell<T>(Reading<T> reading, Func<T, string> show) => reading.Outcome switch
    {
        ReadOutcome.Present => show(reading.Value!),
        ReadOutcome.Denied => Refused,
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
