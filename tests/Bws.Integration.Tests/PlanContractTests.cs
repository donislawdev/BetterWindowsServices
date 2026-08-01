using System.Text.Json;

namespace Bws.Integration.Tests;

/// <summary>
/// The write commands, against the real manager, on the machine this session runs on.
///
/// Every case here is chosen so that nothing can be stopped or started by running it, which
/// is not a matter of care but of arithmetic. The refusal cases end before the manager is
/// asked for anything at all. The one case that does carry a plan out picks an entry that is
/// both stopped and disabled, so there is no state for the run to change and no way for the
/// entry to acquire one between choosing it and running it. Everything that could actually
/// move a service belongs on a throwaway machine, and the plan for that is in the slice.
/// </summary>
public sealed class PlanContractTests
{
    [Fact]
    public void A_plan_that_was_only_shown_says_so_in_the_document()
    {
        // The field a change process reads to know whether a machine was touched. Absent
        // results could be read as "ran and did nothing", so the answer is said outright.
        var run = CommandLineTool.Run("stop", "Spooler", "--dry-run", "--json");
        var plan = JsonDocument.Parse(run.StandardOutput).RootElement;

        Assert.Equal(0, run.ExitCode);
        Assert.True(plan.GetProperty("dryRun").GetBoolean());
        Assert.Equal(JsonValueKind.Null, plan.GetProperty("results").ValueKind);
        Assert.Equal(JsonValueKind.Null, plan.GetProperty("completed").ValueKind);
        Assert.True(plan.GetProperty("steps").GetArrayLength() > 0);
    }

    [Fact]
    public void A_name_nobody_has_is_refused_without_the_word_dry_run()
    {
        // The refusal has to hold for the real command, not only for the preview. This is
        // the case that would quietly start being a stop if the check moved.
        var run = CommandLineTool.Run("stop", "NoSuchServiceAnywhere");

        Assert.Equal(2, run.ExitCode);
        Assert.Equal(string.Empty, run.StandardOutput.Trim());
        Assert.False(string.IsNullOrWhiteSpace(run.StandardError));
    }

    [Fact]
    public void A_driver_is_refused_without_the_word_dry_run()
    {
        var driver = CommandLineTool.Listing()
            .First(entry => CommandLineTool.Text(entry, "entryType") is "KernelDriver" or "FileSystemDriver");

        var run = CommandLineTool.Run("stop", CommandLineTool.Text(driver, "serviceName"));

        Assert.Equal(2, run.ExitCode);
        Assert.Equal(string.Empty, run.StandardOutput.Trim());
    }

    [Theory]
    [InlineData("list", "--dry-run")]
    [InlineData("list", "--dependents")]
    [InlineData("list", "--timeout", "30")]
    [InlineData("stop", "Spooler", "--query", "status:running")]

    // Starting is not the mirror of stopping. The manager brings up whatever the entry
    // needs by itself and nothing that depends on it has to move, so the plan ignores this
    // word - and a word the plan ignores must not be one the command line takes.
    [InlineData("start", "Spooler", "--dependents")]
    public void An_option_that_does_not_belong_to_the_verb_is_refused_rather_than_ignored(params string[] line)
    {
        // The whole point of the change: none of these used to be an error. They were taken,
        // dropped, and the command carried on doing something other than what the line said.
        // A runbook is read more often than it is run, and a switch that does nothing reads
        // exactly like one that works.
        var run = CommandLineTool.Run(line);

        Assert.Equal(2, run.ExitCode);
        Assert.Equal(string.Empty, run.StandardOutput.Trim());

        // And the message says where the option does work, because "not here" alone leaves
        // somebody guessing at the surface.
        Assert.Contains("It works with:", run.StandardError);
    }

    [Fact]
    public void The_diagnostic_switch_applies_to_every_command_because_every_command_reads()
    {
        // The same silence seen from the other side: --timing used to be accepted on a write
        // command and honoured only when listing. Now it applies, and reports the half E4a
        // asks for - the reading, which is the part that belongs to us.
        var run = CommandLineTool.Run("stop", "Spooler", "--dry-run", "--timing");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("Read ", run.StandardError);

        // Not the filtering line, which would be a measurement of something that never ran.
        Assert.DoesNotContain("filtered in", run.StandardError);
    }

    [Fact]
    public void A_timeout_that_is_not_a_number_of_seconds_is_a_mistake_rather_than_a_default()
    {
        var run = CommandLineTool.Run("stop", "Spooler", "--dry-run", "--timeout", "30s");

        Assert.Equal(2, run.ExitCode);
        Assert.Equal(string.Empty, run.StandardOutput.Trim());
    }

    [Fact]
    public void Carrying_out_a_plan_with_nothing_left_to_do_reports_it_as_done()
    {
        // The whole path end to end - plan, run, outcome per step, exit code - on an entry
        // where the run has nothing to do. Disabled and stopped, confirmed with sc.exe
        // immediately before and after, so the run cannot change anything and neither can
        // anybody else in the meantime.
        var target = DisabledAndStopped();

        var run = CommandLineTool.Run("stop", target, "--json");
        var document = JsonDocument.Parse(run.StandardOutput).RootElement;
        var result = document.GetProperty("results").EnumerateArray().Single();

        Assert.Equal(0, run.ExitCode);
        Assert.False(document.GetProperty("dryRun").GetBoolean());
        Assert.True(document.GetProperty("completed").GetBoolean());
        Assert.False(document.GetProperty("cancelled").GetBoolean());

        Assert.Equal("skipped", result.GetProperty("outcome").GetString());
        Assert.Equal("alreadyThere", result.GetProperty("skippedBecause").GetString());
        Assert.Equal("Stopped", result.GetProperty("status").GetString());

        // And the machine agrees, according to something that is not us.
        Assert.Contains("STOPPED", CommandLineTool.ServiceControl("query", target).StandardOutput);
    }

    [Fact]
    public void The_line_a_person_reads_is_the_plan_line_with_what_came_of_it_alongside()
    {
        // Written down as an exact prediction rather than as a shape, because the value of
        // this rendering is that somebody who read the preview recognises it. A search for
        // some substring would pass on a line the preview never had.
        var target = DisabledAndStopped();
        var run = CommandLineTool.Run("stop", target);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains($"Plan: stop {target}  (1 step)", run.StandardOutput);
        Assert.Contains($"  1. stop  {target}   (asked for)   already there, nothing to do", run.StandardOutput);
    }

    /// <summary>
    /// An entry that is stopped and cannot start, taken from this machine rather than named
    /// in advance. A name written into a test is a name that is running on somebody else's
    /// machine, and the point of choosing here is that there is nothing to break.
    /// </summary>
    private static string DisabledAndStopped()
    {
        foreach (var entry in CommandLineTool.Listing("--query", "status:stopped start:disabled !type:driver"))
        {
            var name = CommandLineTool.Text(entry, "serviceName");

            // sc.exe is the authority, not our own listing. Checking with the tool under
            // test whether the tool under test is safe to run would prove nothing.
            var stopped = CommandLineTool.ServiceControl("query", name).StandardOutput.Contains("STOPPED");
            var disabled = CommandLineTool.ServiceControl("qc", name).StandardOutput.Contains("DISABLED");

            if (stopped && disabled)
            {
                return name;
            }
        }

        throw new InvalidOperationException(
            "No disabled and stopped entry on this machine, so the one safe way to carry a " +
            "plan out here is not available. Run this on a machine that has one.");
    }
}
