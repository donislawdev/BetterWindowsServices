using Bws.Core.Planning;

namespace Bws.Cli.Tests;

/// <summary>
/// <c>bws pause</c> and <c>bws resume</c> - spec C1, backlog 555 part (b), the owner's decisions of 2026-11-25:
/// the two verbs read, what they refuse, and every sentence their plans can carry.
/// </summary>
public sealed class PauseCommandTests
{
    [Theory]
    [InlineData("pause", ActionKind.Pause)]
    [InlineData("RESUME", ActionKind.Resume)]
    public void Each_verb_is_read_as_a_write_of_one_named_entry(string verb, ActionKind asked)
    {
        var read = CommandLine.Read([verb, "Spooler", "--dry-run", "--timeout", "5"]);

        Assert.True(read.NothingWrong);
        Assert.True(read.IsWrite);
        Assert.Equal("Spooler", read.ServiceName);
        Assert.Equal(asked, WriteCommands.AskedFor(read.Kind));
    }

    [Fact]
    public void Taking_dependants_along_is_refused_because_a_pause_takes_nothing_along()
    {
        Assert.Equal(ExitCode.Usage, Refusals.Answer(CommandLine.Read(["pause", "Spooler", "--dependents"])));
    }

    [Fact]
    public void A_stopped_entry_is_refused_in_words_that_name_the_start_rather_than_doing_it()
    {
        Assert.Equal(
            "Spooler is not running, so there is nothing to pause.",
            PlanText.Describe(new PlanProblem(PlanProblemKind.NotRunning, "Spooler", [])));

        Assert.Equal(
            "Spooler is not running, so there is nothing to resume. Resuming does not start a stopped service - to start it: bws start Spooler",
            PlanText.Describe(new PlanProblem(PlanProblemKind.StoppedNotPaused, "Spooler", [])));

        Assert.Contains(
            "does not accept pause requests",
            PlanText.Describe(new PlanProblem(PlanProblemKind.PauseNotAccepted, "Spooler", [])),
            StringComparison.Ordinal);
    }

    [Fact]
    public void What_keeps_running_is_named_in_the_singular_and_the_plural()
    {
        Assert.Equal(
            "SessionEnv depends on LanmanWorkstation. Pausing LanmanWorkstation does not pause or stop it, so it keeps running.",
            PlanText.Describe(new PlanWarning(PlanWarningKind.DependentsKeepRunning, "LanmanWorkstation", ["SessionEnv"])));

        Assert.Equal(
            "These depend on LanmanWorkstation: SessionEnv, Netlogon. Pausing LanmanWorkstation does not pause or stop them, so they keep running.",
            PlanText.Describe(new PlanWarning(PlanWarningKind.DependentsKeepRunning, "LanmanWorkstation", ["SessionEnv", "Netlogon"])));
    }

    [Fact]
    public void A_list_of_dependants_that_could_not_be_read_is_said_rather_than_left_out()
    {
        Assert.Equal(
            "Could not read what depends on LanmanWorkstation, so this plan cannot name what keeps running while it is paused. "
            + "Pausing LanmanWorkstation pauses and stops nothing else.",
            PlanText.Describe(new PlanWarning(PlanWarningKind.DependentsUnreadable, "LanmanWorkstation", [])));
    }

    [Fact]
    public void A_start_of_a_paused_entry_points_at_this_tools_own_resume()
    {
        Assert.EndsWith(
            "To resume it instead: bws resume Spooler",
            PlanText.Describe(new PlanWarning(PlanWarningKind.PausedCannotStart, "Spooler", [])),
            StringComparison.Ordinal);
    }
}
