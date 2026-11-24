using Bws.Core.Snapshots;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// A changed recovery in the comparison table, after the review of PR #133: in words when the words tell
/// the two sides apart, as the two values when they do not, and a program no failure runs said to be not
/// used, as the details panel says it.
/// </summary>
public sealed class RecoveryDiffLinesTests
{
    private const string EmptyList =
        """{"actions":[],"command":null,"onNonCrashFailures":false,"rebootMessage":null,"resetPeriodSeconds":60}""";

    private const string OneRestart =
        """{"actions":[{"action":"RestartService","delayMilliseconds":60000}],"command":"C:\\fix.cmd","onNonCrashFailures":false,"rebootMessage":null,"resetPeriodSeconds":86400}""";

    /// <summary>
    /// The period of an empty list decides nothing, so the words leave it out and both sides would read "no
    /// recovery is set" - a difference shown as two equal sides. The two values are shown instead.
    /// </summary>
    [Fact]
    public void A_change_the_words_leave_out_shows_the_two_values()
    {
        var after = EmptyList.Replace("60}", "120}", StringComparison.Ordinal);

        var line = Assert.Single(DiffLines.Of(Changed(EmptyList, after), DiffSides.WithThisMachine));

        Assert.Equal(EmptyList, line.Before);
        Assert.Equal(after, line.After);
    }

    [Fact]
    public void A_program_no_failure_runs_reads_as_not_used_in_the_row()
    {
        var after = OneRestart.Replace("RestartService", "RunProgram", StringComparison.Ordinal);

        var line = Assert.Single(DiffLines.Of(Changed(OneRestart, after), DiffSides.WithThisMachine));

        Assert.Contains(Texts.Of("gui.recovery.line.command.unused", @"C:\fix.cmd"), line.Before, StringComparison.Ordinal);
        Assert.Contains(Texts.Of("gui.recovery.line.command", @"C:\fix.cmd"), line.After, StringComparison.Ordinal);
    }

    private static SnapshotDiff Changed(string before, string after) =>
        new(
            [], [],
            [new ChangedEntry("Spooler", "Print Spooler", [new FieldDifference("recovery", DifferenceGroup.Configuration, before, after)], [])],
            [], [], [], [],
            new ComparisonCaveats(false, false, false, false, false, false, false, false, false, []),
            new InstancesLeftOut(0, 0),
            []);
}
