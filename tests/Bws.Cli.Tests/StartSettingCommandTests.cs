using System.Text.Json;
using Bws.Core.Planning;

namespace Bws.Cli.Tests;

/// <summary>
/// The command line's half of 2026-09-24: the fourth word "delayed", the switch --stop, and the
/// plan document saying a late setting the way the listing does. UX-GUI-006 with backlog 231 and
/// 232, `docs/PROJEKT-TYP-STARTU-20260924.md`.
/// </summary>
public sealed class StartSettingCommandTests
{
    /// <summary>"delayed" reads as the late setting, the word start:delayed already asks about.</summary>
    [Fact]
    public void The_fourth_word_is_the_late_setting()
    {
        Assert.Equal(StartSetting.AutomaticDelayed, WriteCommands.Named("delayed"));
        Assert.Equal(StartSetting.AutomaticDelayed, WriteCommands.Named("DELAYED"));
        Assert.Null(WriteCommands.Named("delayed-auto"));
    }

    /// <summary>
    /// --stop beside anything but disabled is turned back before the manager is opened, with the
    /// word that was written in the sentence.
    /// </summary>
    [Theory]
    [InlineData("automatic")]
    [InlineData("delayed")]
    [InlineData("manual")]
    public void A_stop_beside_any_word_but_disabled_is_refused_before_anything_is_read(string word)
    {
        Assert.Equal(ExitCode.Usage, Refusals.Answer(CommandLine.Read(["start-type", "Spooler", word, "--stop"])));
    }

    /// <summary>Beside disabled it is a whole ask, and nothing is wrong with it.</summary>
    [Fact]
    public void A_stop_beside_disabled_has_nothing_wrong_with_it()
    {
        var line = CommandLine.Read(["start-type", "Spooler", "disabled", "--stop", "--dry-run"]);

        Assert.Null(Refusals.Answer(line));
        Assert.True(line.Setting.AlsoStop);
    }

    /// <summary>--stop belongs to one verb. On a plain stop it is an option that verb does not take.</summary>
    [Fact]
    public void A_stop_switch_on_another_verb_is_an_option_that_verb_does_not_take()
    {
        Assert.Equal(ExitCode.Usage, Refusals.Answer(CommandLine.Read(["stop", "Spooler", "--stop"])));
    }

    /// <summary>
    /// A late step says "Automatic" and delayedAuto true - the listing's words, the owner's decision
    /// J1 of 2026-09-24 - and a plan that stops as well says alsoStop.
    /// </summary>
    [Fact]
    public void The_plan_document_says_a_late_setting_the_way_the_listing_does()
    {
        var plan = new OperationPlan
        {
            Action = new ServiceAction(ActionKind.SetStartType, "RemoteRegistry", To: StartSetting.AutomaticDelayed),
            Steps = [new PlanStep("RemoteRegistry", "RemoteRegistry", StepOperation.SetStartType, StepReason.Requested, To: StartSetting.AutomaticDelayed)],
            Warnings = [],
            Problems = []
        };

        var document = JsonDocument.Parse(PlanJson.Render(plan)).RootElement;
        var step = document.GetProperty("steps")[0];

        Assert.Equal("Automatic", step.GetProperty("startType").GetString());
        Assert.True(step.GetProperty("delayedAuto").GetBoolean());
        Assert.False(document.GetProperty("alsoStop").GetBoolean());
    }

    /// <summary>Every other step carries null in both fields, written rather than left out.</summary>
    [Fact]
    public void A_stop_riding_on_a_setting_is_asked_for_and_its_step_carries_no_setting()
    {
        var plan = new OperationPlan
        {
            Action = new ServiceAction(ActionKind.SetStartType, "Spooler", To: StartSetting.Disabled, AlsoStop: true),
            Steps =
            [
                new PlanStep("Spooler", "Spooler", StepOperation.SetStartType, StepReason.Requested, To: StartSetting.Disabled),
                new PlanStep("Spooler", "Spooler", StepOperation.Stop, StepReason.Requested)
            ],
            Warnings = [],
            Problems = []
        };

        var document = JsonDocument.Parse(PlanJson.Render(plan)).RootElement;

        Assert.True(document.GetProperty("alsoStop").GetBoolean());
        Assert.False(document.GetProperty("steps")[0].GetProperty("delayedAuto").GetBoolean());
        Assert.Equal(JsonValueKind.Null, document.GetProperty("steps")[1].GetProperty("delayedAuto").ValueKind);
    }
}
