using Bws.Core.Planning;

namespace Bws.Cli.Tests;

/// <summary>
/// That the commands the core renders are commands THIS TOOL ACCEPTS.
///
/// <b>The one question the core cannot ask about its own output.</b> `EquivalentCommandTests` in the
/// core asserts the text, and text is not the same as validity: a switch spelled correctly onto a
/// verb that refuses it renders a line which looks exactly like a command and is rejected the moment
/// somebody pastes it. Nothing in a build says so, and neither does a screenshot of the window that
/// offered it.
///
/// <b>WHY THIS IS A BRIDGE RATHER THAN A SINGLE SOURCE, said rather than left to be discovered.</b>
/// The switch names could have lived in the core alone and been referenced from OptionSurface. They
/// do not, for one measured reason: `tools/audit/audit.ps1` reads the switch table out of
/// OptionSurface.cs by matching the literal shape of its entries against the table in `docs/02`, and
/// a reference would have left that regex matching nothing. So the literals stay where the existing
/// bridge can see them, and this guard is the second bridge. Two copies with a test between them, in
/// the same shape this project already uses for the exit codes.
///
/// <b>It found a real fault on the day it was written</b> - the core rendered --dependents onto a
/// start, which the command line refuses, and it did so for every entry of a selection carrying one
/// such flag.
/// </summary>
public sealed class EquivalentCommandBridgeGuards
{
    private static readonly CommandKind[] WriteVerbs =
        [CommandKind.Stop, CommandKind.Start, CommandKind.Restart];

    /// <summary>
    /// Every switch the core writes, on every verb it writes it with, is one this tool takes there.
    ///
    /// <b>It goes through the rendered STRING rather than through a constant, and that is the point.</b>
    /// A constant would say what the core intends. The string says what it produced, which is the
    /// thing a person pastes.
    /// </summary>
    [Fact]
    public void Every_switch_the_core_renders_is_one_this_tool_accepts_on_that_verb()
    {
        var seen = 0;

        foreach (var kind in new[] { ActionKind.Stop, ActionKind.Start, ActionKind.Restart })
        {
            // Asked for with the cascade, because that is the only way a switch appears at all today.
            var rendered = EquivalentCommand.For(new ServiceAction(kind, "Spooler", IncludeDependents: true));
            var verb = Verb(kind);

            foreach (var option in rendered.Split(' ').Where(word => word.StartsWith("--", StringComparison.Ordinal)))
            {
                seen++;

                var entry = OptionSurface.Surface.Where(one => one.Option == option).ToArray();

                Assert.True(
                    entry.Length == 1,
                    $"The core renders {option} and this tool has no such switch at all.");

                Assert.True(
                    entry[0].Verbs.Contains(verb),
                    $"The core renders '{rendered}' and this tool refuses {option} on {verb}. " +
                    "That is a line which reads as a command and fails when it is pasted.");
            }
        }

        // A BRIDGE THAT FINDS NOTHING MUST GO RED RATHER THAN GREEN, which is the fault this project
        // has now paid for four times - most recently in audit.ps1 on 2026-08-18, where a check
        // looked for a shape in one file, a seam moved it, the loop never ran once and the script
        // reported no problems. Nothing above executes if the core stops rendering switches, and
        // "no switches rendered" is indistinguishable from "every switch is valid".
        Assert.True(seen > 0, "No switch was rendered by the core at all, so nothing above was checked.");
    }

    /// <summary>
    /// And every verb the core writes is a verb this tool has.
    ///
    /// Cheap, and it covers the half the switch check cannot: a renamed verb would render a whole
    /// command that does not exist, switches and all.
    /// </summary>
    [Fact]
    public void Every_verb_the_core_renders_is_one_this_tool_has()
    {
        foreach (var kind in new[] { ActionKind.Stop, ActionKind.Start, ActionKind.Restart })
        {
            var word = EquivalentCommand.For(new ServiceAction(kind, "Spooler")).Split(' ')[1];

            Assert.True(
                Enum.TryParse<CommandKind>(word, ignoreCase: true, out var parsed) && WriteVerbs.Contains(parsed),
                $"The core renders the verb '{word}' and this tool has no write command by that name.");
        }
    }

    private static CommandKind Verb(ActionKind kind) => kind switch
    {
        ActionKind.Stop => CommandKind.Stop,
        ActionKind.Start => CommandKind.Start,
        _ => CommandKind.Restart
    };
}
