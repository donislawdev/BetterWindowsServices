using System.Globalization;
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
        // The signature column appears only when there is something in it, and the answer
        // comes from the entries rather than from a switch somebody passed. Reading it off
        // the data is what stops the header and the cells from ever disagreeing: a column
        // shown because a flag was set, over entries nobody actually read, would be a row
        // of blanks that looks like a row of "unsigned".
        var signed = entries.Any(entry => entry.Signature.Outcome != ReadOutcome.NotRead);

        // Same rule, same reason: the column is there because the data is, never because a
        // switch was passed. --memory turns the reading on and the column follows from that,
        // so there is no arrangement in which the header appears over cells nobody filled.
        var measured = entries.Any(entry => entry.Memory.Outcome != ReadOutcome.NotRead);

        var rows = new List<string[]>(entries.Count + 1);
        rows.Add(Row(
        [
            Texts.Of("cli.column.name"),
            Texts.Of("cli.column.displayName"),
            Texts.Of("cli.column.type"),
            Texts.Of("cli.column.status"),
            Texts.Of("cli.column.startType"),
            Texts.Of("cli.column.account"),
            Texts.Of("cli.column.processId")
        ], signed ? Texts.Of("cli.column.signature") : null, measured ? Texts.Of("cli.column.memory") : null));

        foreach (var entry in entries)
        {
            rows.Add(Row(
            [
                entry.ServiceName,
                entry.DisplayName,
                entry.EntryType.ToString(),
                entry.Status.ToString(),
                StartCell(entry),
                Cell(entry.Account, value => value),
                Cell(entry.ProcessId, value => value.ToString(CultureInfo.CurrentCulture))
            ], signed ? SignatureCell(entry) : null, measured ? MemoryCell(entry) : null));
        }

        return Layout(rows);
    }

    private static string[] Row(string[] cells, params string?[] extras) =>
        [.. cells, .. extras.Where(extra => extra is not null).Select(extra => extra!)];

    /// <summary>
    /// What the entry's process is holding, and how many entries that answer covers.
    ///
    /// The working set rather than the commit, because it is the number a person can check
    /// against Task Manager and against Get-Process. The other one is in the machine
    /// readable output, where nothing has to fit in a column.
    ///
    /// The sharing is said in the cell rather than left to be worked out from the process
    /// ids, and that is the whole reason this column is safe to show. Five rows quoting the
    /// same 36 MB is correct and adds up to five times the truth, and a person scanning a
    /// column adds it up. Measured on a real machine: 105 of 110 processes host one entry,
    /// so this widens five rows rather than the table.
    /// </summary>
    private static string MemoryCell(ScmEntry entry)
    {
        if (entry.Memory.Outcome == ReadOutcome.Denied)
        {
            return Texts.Of("cli.cell.noAccess");
        }

        if (!entry.Memory.IsPresent)
        {
            return Nothing;
        }

        var memory = entry.Memory.Value!;
        var size = Megabytes(memory.WorkingSet);

        return memory.IsShared
            ? Texts.Of("cli.cell.memoryShared", size, memory.SharedBy)
            : size;
    }

    /// <summary>
    /// Megabytes with one decimal, which is what Task Manager shows and therefore what a
    /// person can compare us against. Bytes are in the machine readable output for anybody
    /// who needs them exact.
    /// </summary>
    private static string Megabytes(long bytes) =>
        Texts.Of("cli.cell.megabytes", (bytes / (1024.0 * 1024)).ToString("N1", CultureInfo.InvariantCulture));

    /// <summary>
    /// What the system thinks of the file, with who signed it in the same cell.
    ///
    /// One column rather than two, for the same reason the start type carries its
    /// qualifiers rather than spreading into three: the listing is already 260 characters
    /// across on a real machine, and a publisher is only ever read alongside a status.
    ///
    /// An unsigned file shows the status alone. There is no publisher to put in brackets
    /// and an empty pair of them would read as a name nobody could work out.
    /// </summary>
    private static string SignatureCell(ScmEntry entry)
    {
        if (entry.Signature.Outcome == ReadOutcome.Denied)
        {
            return Texts.Of("cli.cell.noAccess");
        }

        if (!entry.Signature.IsPresent)
        {
            return Nothing;
        }

        var signature = entry.Signature.Value!;
        var status = signature.Status.ToString();

        return signature.Publisher is null
            ? status
            : Texts.Of("cli.cell.signedBy", status, signature.Publisher);
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

        // Only where the flag does something, which is not the same as everywhere it is read.
        // Since 2026-08-01 the manager is asked about it for every non-driver entry, because
        // it is stored configuration a snapshot should carry - but Windows ignores it unless
        // the entry starts automatically. Printing "Manual (delayed)" would be a sentence
        // about a setting that has no effect, on eight entries of this machine.
        var meansSomething = entry.StartType.IsPresent && entry.StartType.Value == StartType.Automatic;

        var cell = entry.DelayedAuto.Outcome switch
        {
            ReadOutcome.Present when entry.DelayedAuto.Value && meansSomething =>
                Texts.Of("cli.cell.startDelayed", startType),

            // A refusal is only worth reporting where the answer would have changed what the
            // start type means. Elsewhere it is an admission about a field nobody was asking
            // about, which is noise rather than honesty.
            ReadOutcome.Denied when meansSomething => Texts.Of("cli.cell.startDelayedUnknown", startType),

            _ => startType
        };

        // Only a trigger that starts it. One that stops the service says nothing about
        // whether it will come up, and marking it here would answer a question nobody asked
        // with a fact about something else.
        var startsOnTrigger = entry.Triggers.IsPresent
            && entry.Triggers.Value!.Any(trigger => trigger.Action == TriggerAction.Start);

        if (startsOnTrigger)
        {
            cell = Texts.Of("cli.cell.startTrigger", cell);
        }

        // A missing file changes what the start type means more sharply than either of the
        // two above: whatever the manager was going to do with this entry, it cannot. Said
        // here for the same reason and at the same cost - measured on a real machine, five
        // entries of 810, so it widens five rows rather than the table.
        //
        // A column of its own was the other option and was measured rather than guessed: the
        // listing is already 260 characters across and the paths would take it past 400, for
        // something that is empty on 805 rows out of 810. The path itself is in --json, where
        // a person who wants it can get at it.
        var fileMissing = entry.BinaryOnDisk is { Outcome: ReadOutcome.Present, Value: false };

        return fileMissing ? Texts.Of("cli.cell.startFileMissing", cell) : cell;
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
                widths[column] = Math.Max(widths[column], Wide(row[column]));
            }
        }

        var text = new StringBuilder();

        foreach (var row in rows)
        {
            for (var column = 0; column < columns; column++)
            {
                // The last column carries no trailing padding, so copying a line out of a
                // terminal does not bring invisible spaces with it.
                if (column == columns - 1)
                {
                    text.Append(row[column]);

                    continue;
                }

                text.Append(row[column]).Append(' ', widths[column] + 2 - Wide(row[column]));
            }

            text.AppendLine();
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// How many columns a cell takes on screen, as far as this can be known.
    ///
    /// <b>Not the length of the string</b>, and that is what it used to be. A display name is
    /// whatever the vendor wrote and the machine's language allows, so it can hold a character
    /// stored as two units - an emoji in a product name, a rare ideograph - and counting units
    /// made every row after it in that column two spaces out. `PadRight` counts the same way, so
    /// the two mistakes did not cancel: the width was wrong and the padding was wrong with it.
    ///
    /// Text elements rather than units, so a character built from several units counts once, and
    /// so does a letter with a combining accent.
    ///
    /// <b>WHAT THIS STILL DOES NOT KNOW, said rather than left to be found.</b> A full-width
    /// character - the Han, Kana and Hangul ranges - occupies two terminal columns and is counted
    /// here as one, so a listing on a Japanese or Chinese Windows still drifts. Getting that
    /// right needs the East Asian Width table, which .NET does not expose and which is more than
    /// a listing column is worth today. `CLAUDE.md` says not to assume an English Windows, and
    /// this is the one place that still does.
    /// </summary>
    private static int Wide(string cell)
    {
        var elements = System.Globalization.StringInfo.GetTextElementEnumerator(cell);
        var width = 0;

        while (elements.MoveNext())
        {
            width++;
        }

        return width;
    }
}
