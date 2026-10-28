using System.Text;
using Bws.Core.Snapshots;

namespace Bws.Cli;

/// <summary>
/// What changed, written for a person.
///
/// Grouped rather than listed flat, because the two questions underneath are different ones.
/// Configuration drift is what somebody making this comparison came for. Running state is
/// what the machine happened to be doing at two moments, and on two snapshots a day apart it
/// is dozens of lines that mean nothing was wrong - printed together they bury the six lines
/// that do.
/// </summary>
internal static class DiffText
{
    internal static string Render(SnapshotDiff diff)
    {
        var text = new StringBuilder();

        // What makes the comparison less than exact goes first, not last. Somebody who reads
        // the differences before learning that one side was taken without elevation has
        // already believed something.
        Caveats(text, diff);

        // WHETHER THERE IS ANYTHING TO SHOW, which is not whether anything drifted - since
        // 2026-09-30 those are two questions. An entry that differs only in running state does not
        // count towards --exit-code any more and is still printed below, so asking Drifted here
        // would print "No differences." over it.
        if (!diff.Reported)
        {
            text.AppendLine(Texts.Of("cli.diff.same"));

            return text.ToString();
        }

        Presence(text, "cli.diff.added", diff.Added);
        Presence(text, "cli.diff.removed", diff.Removed);

        // Its own heading rather than mixed into removals, because "these are gone" and "one
        // of these files could not see them" are different sentences and only one is a fact.
        Presence(text, "cli.diff.uncertain", diff.Uncertain);

        // And its own again, since 2026-10-27 (logic report S03): these the manager says it still holds,
        // which is a fact rather than a "cannot tell" - only this account was not shown them.
        Presence(text, "cli.diff.unlisted", diff.Unlisted);

        Changed(text, "cli.diff.changed", diff.Changed);

        // Also its own heading, for the same reason one level down. These entries have
        // nothing different about them - one of the two snapshots holds no value for a field,
        // so nobody can say either way. A field refused to BOTH is not here but in the caveats. Filed under "changed" they read as drift that is not
        // there, which is how the first version of this printed "5 changed, 0 differences".
        Changed(text, "cli.diff.notFullyCompared", diff.NotFullyCompared);

        Summary(text, diff);

        return text.ToString();
    }

    /// <summary>
    /// Every line that makes the comparison less than exact, and a blank line after them when there
    /// were any.
    ///
    /// <b>Collected and then written, since 2026-09-30</b>, when three caveats and the count of
    /// per-user copies joined the four there were. One condition per line of a table rather than a
    /// flag set in eight branches - the shape a method pays for once it is long enough to be counted.
    /// </summary>
    private static void Caveats(StringBuilder text, SnapshotDiff diff)
    {
        var caveats = diff.Caveats;

        (bool Says, Func<string> Line)[] lines =
        [
            (caveats.ElevationDiffers, () => Texts.Of("cli.diff.caveat.elevation")),
            (caveats.MachineDiffers, () => Texts.Of("cli.diff.caveat.machine")),
            (caveats.OperatingSystemDiffers, () => Texts.Of("cli.diff.caveat.operatingSystem")),
            (caveats.ToolVersionDiffers, () => Texts.Of("cli.diff.caveat.tool")),
            (caveats.LanguageDiffers, () => Texts.Of("cli.diff.caveat.language")),
            (caveats.AccountDiffers, () => Texts.Of("cli.diff.caveat.account")),
            (caveats.FileVersionSourceDiffers, () => Texts.Of("cli.diff.caveat.fileVersion")),
            (caveats.TriggerDetailDiffers, () => Texts.Of("cli.diff.caveat.triggers")),
            (caveats.NotKnown.Count > 0, () => Texts.Of("cli.diff.caveat.notKnown", string.Join(", ", caveats.NotKnown))),
            (diff.NeitherRead.Count > 0, () => Texts.Of("cli.diff.caveat.neitherRead", string.Join(", ", diff.NeitherRead))),
            (diff.LeftOut.Any, () => Texts.Of("cli.diff.caveat.instancesLeftOut", diff.LeftOut.Earlier, diff.LeftOut.Later))
        ];

        var said = lines
            .Where(line => line.Says)
            .Select(line => line.Line())
            .Concat(diff.RefusedByBoth.Select(Refused))
            .ToList();

        foreach (var line in said)
        {
            text.AppendLine(line);
        }

        if (said.Count > 0)
        {
            text.AppendLine();
        }
    }

    /// <summary>
    /// A field Windows refused to both snapshots, said once with the entries it was refused on -
    /// backlog 559, owner's decision of 2026-10-14. Until then each of those entries stood under
    /// "not fully compared", so this machine compared with itself a minute later printed ten of
    /// them every time.
    /// </summary>
    private static string Refused(RefusedField field) =>
        field.Entries.Count == 1
            ? Texts.Of("cli.diff.caveat.refusedByBoth.one", field.Field, field.Entries.Count, string.Join(", ", field.Entries))
            : Texts.Of("cli.diff.caveat.refusedByBoth.many", field.Field, field.Entries.Count, string.Join(", ", field.Entries));

    private static void Presence(StringBuilder text, string heading, IReadOnlyList<EntryPresence> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        text.AppendLine(Texts.Of(heading, entries.Count));

        foreach (var entry in entries)
        {
            text.AppendLine(Texts.Of("cli.diff.entry", entry.ServiceName, entry.DisplayName));
        }

        text.AppendLine();
    }

    private static void Changed(StringBuilder text, string heading, IReadOnlyList<ChangedEntry> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        text.AppendLine(Texts.Of(heading, entries.Count));

        foreach (var entry in entries)
        {
            text.AppendLine(Texts.Of("cli.diff.entry", entry.ServiceName, entry.DisplayName));

            foreach (var line in entry.Differences.SelectMany(Lines))
            {
                text.AppendLine(line);
            }

            // Named, never counted as a change. A field one side could not read says nothing
            // about whether it moved, and leaving it out entirely would be the silence rule 8
            // forbids - the reader would take "no differences" for "nothing changed".
            foreach (var field in entry.Incomparable)
            {
                text.AppendLine(Texts.Of("cli.diff.field.incomparable", field));
            }
        }

        text.AppendLine();
    }

    private static void Summary(StringBuilder text, SnapshotDiff diff)
    {
        var configuration = diff.Changed.Sum(entry =>
            entry.Differences.Count(difference => difference.Group == DifferenceGroup.Configuration));

        var state = diff.Changed.Sum(entry =>
            entry.Differences.Count(difference => difference.Group == DifferenceGroup.RunningState));

        text.AppendLine(Texts.Of(
            "cli.diff.summary",
            diff.Added.Count,
            diff.Removed.Count,
            diff.Changed.Count,
            configuration,
            state));

        // Said in the summary too, not only above. Somebody who reads the last line and
        // stops must not come away with "nothing changed" when part of it was never looked
        // at - that is the difference between an answer and a comfortable one.
        // Entries the manager holds and did not list are seen by one side too - the snapshot's.
        var seenByOne = diff.Uncertain.Count + diff.Unlisted.Count;

        if (diff.NotFullyCompared.Count > 0 || seenByOne > 0)
        {
            // A HEADING AND A LINE PER COUNT SINCE 2026-09-01, WHERE IT USED TO BE ONE SENTENCE
            // HOLDING BOTH - backlog 207. One sentence could not carry a singular, because each
            // half counts something different and either half can be nought: "0 entries carry a
            // field one snapshot never read, and 1 entries only one of the two could see" is one
            // line saying nothing about the first count and saying it wrongly about the second.
            text.AppendLine(Texts.Of("cli.diff.summaryIncomplete"));

            if (diff.NotFullyCompared.Count > 0)
            {
                text.AppendLine(diff.NotFullyCompared.Count == 1
                    ? Texts.Of("cli.diff.incomplete.neverRead.one", diff.NotFullyCompared.Count)
                    : Texts.Of("cli.diff.incomplete.neverRead.many", diff.NotFullyCompared.Count));
            }

            if (seenByOne > 0)
            {
                text.AppendLine(seenByOne == 1
                    ? Texts.Of("cli.diff.incomplete.seenByOne.one", seenByOne)
                    : Texts.Of("cli.diff.incomplete.seenByOne.many", seenByOne));
            }
        }
    }

    /// <summary>
    /// One difference as the lines it reads in: "field: before -> after", and for the triggers one such
    /// line per trigger only one side holds, the other side "nothing" (backlog 625, owner's decision "a
    /// line per trigger", 2026-10-27). Until then the triggers were the two whole lists as JSON - about
    /// three thousand characters on one line for an entry losing one of thirteen.
    ///
    /// <b>The whole lists are still the answer when there are no trigger lines to give</b> - a field that
    /// differs only as "none" against an empty list, which no real reading writes but a file can hold.
    /// A difference reported with nothing under it would be the silence rule 8 forbids. And when a side
    /// holds a trigger that is not one to say, which the core answers with no lines at all (since the
    /// review of PR #98 - a null trigger read as "/", and a data item of nulls ended the run with code 1).
    /// </summary>
    private static IEnumerable<string> Lines(FieldDifference difference)
    {
        var key = difference.Group == DifferenceGroup.RunningState ? "cli.diff.field.state" : "cli.diff.field.configuration";

        if (difference.Triggers is not { } change || change.Removed.Count + change.Added.Count == 0)
        {
            return [Texts.Of(key, difference.Field, Value(difference.Before), Value(difference.After))];
        }

        return change.Removed.Select(trigger => Texts.Of(key, difference.Field, TriggerWords.Line(trigger), Value(null)))
            .Concat(change.Added.Select(trigger => Texts.Of(key, difference.Field, Value(null), TriggerWords.Line(trigger))));
    }

    /// <summary>
    /// A missing value said in words rather than left blank.
    ///
    /// An empty cell beside an arrow reads as though the tool ran out of room. "nothing"
    /// says which of the two it was, and a field going from a value to nothing is one of the
    /// more interesting things a diff can report.
    /// </summary>
    private static string Value(string? value) =>
        value is null ? Texts.Of("cli.diff.nothing") : value;
}
