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
        Caveats(text, diff.Caveats, diff.NeitherRead);

        if (!diff.Any && diff.Uncertain.Count == 0 && diff.NotFullyCompared.Count == 0)
        {
            text.AppendLine(Texts.Of("cli.diff.same"));

            return text.ToString();
        }

        Presence(text, "cli.diff.added", diff.Added);
        Presence(text, "cli.diff.removed", diff.Removed);

        // Its own heading rather than mixed into removals, because "these are gone" and "one
        // of these files could not see them" are different sentences and only one is a fact.
        Presence(text, "cli.diff.uncertain", diff.Uncertain);

        Changed(text, "cli.diff.changed", diff.Changed);

        // Also its own heading, for the same reason one level down. These entries have
        // nothing different about them - one of the two snapshots simply never read a field,
        // so nobody can say either way. Filed under "changed" they read as drift that is not
        // there, which is how the first version of this printed "5 changed, 0 differences".
        Changed(text, "cli.diff.notFullyCompared", diff.NotFullyCompared);

        Summary(text, diff);

        return text.ToString();
    }

    private static void Caveats(StringBuilder text, ComparisonCaveats caveats, IReadOnlyList<string> neitherRead)
    {
        var said = false;

        if (caveats.ElevationDiffers)
        {
            text.AppendLine(Texts.Of("cli.diff.caveat.elevation"));
            said = true;
        }

        if (caveats.MachineDiffers)
        {
            text.AppendLine(Texts.Of("cli.diff.caveat.machine"));
            said = true;
        }

        if (caveats.OperatingSystemDiffers)
        {
            text.AppendLine(Texts.Of("cli.diff.caveat.operatingSystem"));
            said = true;
        }

        if (caveats.ToolVersionDiffers)
        {
            text.AppendLine(Texts.Of("cli.diff.caveat.tool"));
            said = true;
        }

        if (neitherRead.Count > 0)
        {
            text.AppendLine(Texts.Of("cli.diff.caveat.neitherRead", string.Join(", ", neitherRead)));
            said = true;
        }

        if (said)
        {
            text.AppendLine();
        }
    }

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

            foreach (var difference in entry.Differences)
            {
                text.AppendLine(Texts.Of(
                    difference.Group == DifferenceGroup.RunningState
                        ? "cli.diff.field.state"
                        : "cli.diff.field.configuration",
                    difference.Field,
                    Value(difference.Before),
                    Value(difference.After)));
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
        if (diff.NotFullyCompared.Count > 0 || diff.Uncertain.Count > 0)
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

            if (diff.Uncertain.Count > 0)
            {
                text.AppendLine(diff.Uncertain.Count == 1
                    ? Texts.Of("cli.diff.incomplete.seenByOne.one", diff.Uncertain.Count)
                    : Texts.Of("cli.diff.incomplete.seenByOne.many", diff.Uncertain.Count));
            }
        }
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
