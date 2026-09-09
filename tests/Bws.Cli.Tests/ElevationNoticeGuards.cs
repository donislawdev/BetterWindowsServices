using Bws.Cli;

namespace Bws.Cli.Tests;

/// <summary>
/// Whether the terminal says out loud that it was not run as an administrator.
///
/// <b>IT DID NOT SAY IT AT ALL UNTIL 2026-09-09, AND THE WINDOW HAD SAID IT SINCE IT LEARNED TO
/// PLAN.</b> Found by the pre-release audit asking why two interfaces answered one question
/// differently. <c>Session.IsElevated</c> carries the measurement that makes it worth a sentence -
/// 807 entries against 810 on one machine, and five more whose descriptor was refused - and the
/// half that matters is not the number: an entry the manager never enumerated leaves nothing
/// behind for this tool to count, so <b>this is the one admission that cannot be worked out from
/// the reading</b>. Every other line on that channel counts something it was handed.
///
/// <b>Why there is a method to test at all.</b> The decision used to be a condition inside
/// <c>Report</c> reaching for the static, and a guard written against that shape would have been
/// green on an elevated session, green on an unelevated one, and green with the condition
/// inverted - it would have asserted the machine rather than the rule. Backlog 341 describes the
/// same trap in <c>BinaryPathResolver</c> and is still open for it. Handing the answer in is the
/// pattern <c>Mishaps.Told</c> already uses in the window for exactly this reason.
///
/// <b>Asked of every command kind there is rather than of a list typed out here</b>, so a verb
/// added next year joins these assertions without anybody remembering to add it. That is the whole
/// reason these are facts rather than theories: <c>CommandKind</c> is internal, a public theory
/// cannot take one as a parameter, and the way round it turned out to be the better guard.
/// </summary>
public sealed class ElevationNoticeGuards
{
    /// <summary>
    /// Every command but the one with its own sentence, and the one exception is named here rather
    /// than assumed - so adding a second exception has to be a decision somebody writes down.
    /// </summary>
    [Fact]
    public void An_unelevated_run_says_so_on_every_command_but_the_one_that_says_it_better()
    {
        var silent = Enum.GetValues<CommandKind>()
            .Where(kind => !Execution.AdmitsNotElevated(elevated: false, kind))
            .ToList();

        Assert.Equal([CommandKind.SnapshotCreate], silent);
    }

    /// <summary>
    /// <b>The half that stops this being a line printed on every run.</b> An elevated session was
    /// handed everything the machine has, so there is nothing to admit to - and a warning that is
    /// always on screen is one nobody reads by the second week.
    /// </summary>
    [Fact]
    public void An_elevated_run_says_nothing_on_any_command()
    {
        var speaking = Enum.GetValues<CommandKind>()
            .Where(kind => Execution.AdmitsNotElevated(elevated: true, kind))
            .ToList();

        Assert.Empty(speaking);
    }

    /// <summary>
    /// <c>snapshot create</c> already prints <c>cli.snapshot.notElevated</c>, which says this and
    /// then says what it costs when the file is compared later. Two paragraphs about one fact drift
    /// apart at the first edit, so the more specific one is the one that survives.
    ///
    /// Its own assertion as well as the set above, because the set would still pass if the
    /// exception moved to a different command and nothing else changed.
    /// </summary>
    [Fact]
    public void The_one_command_that_says_it_better_is_left_to_say_it() =>
        Assert.False(Execution.AdmitsNotElevated(elevated: false, CommandKind.SnapshotCreate));
}
