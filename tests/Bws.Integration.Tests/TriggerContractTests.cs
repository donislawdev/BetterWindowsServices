using System.Text.Json;

namespace Bws.Integration.Tests;

/// <summary>
/// What the manager actually says about triggers, checked against sc.exe.
///
/// Triggers are the one thing in the details panel that services.msc does not show at all,
/// so there is no window to compare against - sc qtriggerinfo is the only second opinion
/// available, and checklist point 2 says a reading without one is a reading nobody has
/// checked.
///
/// Read-only. Asking what starts a service changes nothing.
/// </summary>
public sealed class TriggerContractTests
{
    [Fact]
    public void Our_count_of_triggers_agrees_with_sc_entry_by_entry()
    {
        // Several entries rather than one, and taken from the machine rather than named
        // here: a name written into a test is a name that does not exist on somebody else's
        // install. Six is enough to catch a reading that stops at the first trigger, which
        // is the mistake this shape of call invites.
        var sampled = 0;

        foreach (var entry in WithTriggers().Take(6))
        {
            var name = CommandLineTool.Text(entry, "serviceName");
            var ours = entry.GetProperty("triggers").GetArrayLength();

            Assert.Equal(TriggersAccordingToServiceControl(name), ours);
            sampled++;
        }

        // A test that sampled nothing would pass while checking nothing, which is the one
        // way a green run means least.
        Assert.Equal(6, sampled);
    }

    [Fact]
    public void An_entry_we_say_has_none_has_none_according_to_sc()
    {
        // The other direction, and the one that would hide a reading that quietly returned
        // an empty answer for everybody.
        var sampled = 0;

        foreach (var entry in CommandLineTool.Listing("--query", "trigger:none !type:driver").Take(6))
        {
            Assert.Equal(0, TriggersAccordingToServiceControl(CommandLineTool.Text(entry, "serviceName")));
            sampled++;
        }

        Assert.Equal(6, sampled);
    }

    [Fact]
    public void Entries_that_start_on_a_trigger_are_a_real_part_of_the_machine()
    {
        // Not a number pinned to one install, but the shape of the claim S4 rests on: this
        // is ordinary configuration and not a curiosity. Measured on the machine this was
        // written on, 122 entries of 810 carry at least one - a count reached independently
        // from the registry in 05-PRZYPADKI-BRZEGOWE and matched exactly by this reading.
        var withTriggers = WithTriggers().Count();

        Assert.True(
            withTriggers > 20,
            $"Only {withTriggers} entries reported a trigger, which is the shape of a reading " +
            "that answers for a handful of services and quietly gives up on the rest.");
    }

    private static IEnumerable<JsonElement> WithTriggers() =>
        CommandLineTool.Listing("--query", "trigger:any");

    /// <summary>
    /// How many triggers sc.exe sees. Every one it prints is introduced by the action it
    /// takes, so the actions are what there is to count - the type line underneath is
    /// missing for at least one kind sc.exe cannot name either.
    /// </summary>
    private static int TriggersAccordingToServiceControl(string serviceName) =>
        CommandLineTool.ServiceControl("qtriggerinfo", serviceName).StandardOutput
            .Split('\n')
            .Count(line => line.Contains("START SERVICE", StringComparison.Ordinal)
                || line.Contains("STOP SERVICE", StringComparison.Ordinal));
}
