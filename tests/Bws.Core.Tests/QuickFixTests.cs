using Bws.Core.Planning;
using Bws.Core.Tests.Fakes;
using Bws.Core.Tools;

namespace Bws.Core.Tests;

/// <summary>
/// The quick fixes (B1, `docs/PROJEKT-TOOLS-20261020.md`) are restarts with dependants and nothing more,
/// and their names keep the shape a public word has to keep.
///
/// <b>The plan itself is not tested again here</b> - a fix hands the ordinary builder the ordinary ask,
/// and the builder has its own tests. What is asserted is that the ask IS that one, and the one shape a
/// fix is meant for on a machine shaped like the owner's: the audio endpoint builder with one dependant
/// running and one stopped (measured 2026-10-20 with sc.exe: Audiosrv running, midisrv stopped).
/// </summary>
public sealed class QuickFixTests
{
    [Fact]
    public void Every_fix_asks_for_a_restart_of_its_service_with_what_depends_on_it()
    {
        foreach (var fix in QuickFixes.All)
        {
            Assert.Equal(new ServiceAction(ActionKind.Restart, fix.ServiceName, IncludeDependents: true), fix.Ask);
        }
    }

    [Fact]
    public void The_names_are_single_lower_case_words_nobody_else_has()
    {
        // The word after `bws tool fix` is a contract the day it ships (owner's decision T-g), so its shape
        // is held here rather than left to taste: one word, lower case, no two alike.
        var names = QuickFixes.All.Select(fix => fix.Name).ToList();

        Assert.All(names, name => Assert.Matches("^[a-z]+(-[a-z]+)*$", name));
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(["printer", "sound", "indexing", "search"], names);
    }

    [Fact]
    public void No_two_fixes_restart_the_same_service()
    {
        // Two names for one restart would be one fix spelled twice, and the second would have to say
        // something the first does not - which is a different fix.
        var services = QuickFixes.All.Select(fix => fix.ServiceName).ToList();

        Assert.Equal(services.Count, services.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void A_fix_and_a_tool_are_found_by_name_whatever_the_case()
    {
        Assert.Same(QuickFixes.Printer, QuickFixes.Named("PRINTER"));
        Assert.Null(QuickFixes.Named("bluetooth"));
        Assert.Same(ToolCatalog.Fix, ToolCatalog.Named("Fix"));
        Assert.Null(ToolCatalog.Named("hvci"));
    }

    [Fact]
    public void The_sound_fix_takes_down_the_dependant_that_runs_and_leaves_the_stopped_one_alone()
    {
        var catalog = new FakeScmCatalog(
            [
                Entry("AudioEndpointBuilder", EntryStatus.Running),
                Entry("Audiosrv", EntryStatus.Running),
                Entry("midisrv", EntryStatus.Stopped)
            ])
            .DependedOnBy("AudioEndpointBuilder", "Audiosrv", "midisrv");

        var plan = new PlanBuilder(catalog.ReadAll(), catalog).Build(QuickFixes.Sound.Ask);

        Assert.True(plan.IsRunnable);
        Assert.Equal(
            [
                (StepOperation.Stop, "Audiosrv"),
                (StepOperation.Stop, "AudioEndpointBuilder"),
                (StepOperation.Start, "AudioEndpointBuilder"),
                (StepOperation.Start, "Audiosrv")
            ],
            plan.Steps.Select(step => (step.Operation, step.ServiceName)));
    }

    private static ScmEntry Entry(string serviceName, EntryStatus status) =>
        Entries.Named(serviceName, serviceName) with
        {
            Status = status,
            StartType = Reading<StartType>.Present(StartType.Automatic)
        };
}
