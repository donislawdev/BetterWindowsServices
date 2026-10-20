using Bws.Core.Planning;
using Bws.Core.Tools;

namespace Bws.Cli;

/// <summary>
/// Reading one argument at a time: whether a word is a verb, whether an option is about to be
/// left without its value, and how a number of seconds is spelled.
///
/// <b>Moved out of CommandLine on 2026-08-03 because the size ratchet said so.</b> Refusing an
/// option given twice pushed that file twenty one lines past a ceiling that may only ever go
/// down, and the ceiling exists so that adding to the longest file starts with looking for a
/// seam.
///
/// The seam is between the surface and the words. What stays behind is what this tool accepts -
/// a record of every switch, with the reasoning for each one beside it - and what is here is how
/// a single word is read. Nothing here knows which verb is running or what any option means.
/// </summary>
internal static class Arguments
{
    /// <summary>
    /// Whether an option that needs a value is going to be left without one.
    ///
    /// Two ways that happens and they used to be one: nothing follows it at all, or what follows
    /// is another switch. The second was taken as the value until 2026-08-03, so
    /// <c>bws list --query --json</c> searched for the text "--json", matched nothing, and ended
    /// with an empty table and code 0 - with the switch somebody actually typed silently gone.
    ///
    /// A switch that quietly does nothing is what the belonging table exists to end. This is the
    /// same fault from the other direction, and it was the louder of the two.
    /// </summary>
    internal static bool NeedsValue(string[] arguments, int index) =>
        index + 1 >= arguments.Length || OptionSurface.IsOption(arguments[index + 1]);

    /// <summary>
    /// Reads a number of seconds, or says what it got instead.
    ///
    /// Nothing below a second, and nothing at all rather than a default quietly standing in.
    /// Somebody who writes --timeout 30s meant thirty seconds, and giving them sixty because
    /// their spelling was not understood is the kind of quiet substitution that turns up in
    /// a runbook months later.
    ///
    /// <b>THE RULE ITSELF LIVES IN THE CORE SINCE 2026-09-15</b> - <see cref="StepCeiling.Seconds"/>,
    /// invariant culture, digits only, at least one - because the window asks a person for the
    /// same number in a box, and two readers of one rule agree only for as long as nobody edits
    /// one of them. What stays here is the terminal's half: which text to hand back as the mistake.
    /// </summary>
    internal static string? Seconds(string value, ref TimeSpan timeout)
    {
        if (StepCeiling.Seconds(value) is not { } seconds)
        {
            return value;
        }

        timeout = TimeSpan.FromSeconds(seconds);
        return null;
    }

    internal static bool TryVerb(string argument, out CommandKind kind)
    {
        kind = argument.ToLowerInvariant() switch
        {
            "list" => CommandKind.List,
            "show" => CommandKind.Show,
            "stop" => CommandKind.Stop,
            "start" => CommandKind.Start,
            "restart" => CommandKind.Restart,

            // Matched here rather than anywhere earlier, and the order of the arms above does not
            // decide it - these are whole words. "start-type" is not a prefix question: a switch
            // expression over strings compares the whole of one, so there is no path where this
            // word is read as "start" with something left over.
            "start-type" => CommandKind.SetStartType,

            // The word taskkill says it replaces, and the word PowerShell aliases Stop-Process
            // to. Somebody reaching for this has typed it before somewhere else.
            "kill" => CommandKind.Kill,

            // The change history. The word is the owner's - see CommandKind.History.
            "history" => CommandKind.History,

            // The tools, every one under this word - owner's decision T-c. The word after it is read
            // by TryTool below rather than here, because it names a tool rather than a command.
            "tool" => CommandKind.Tool,

            // The word every other tool puts this under, and the spelling SPDX uses for the
            // field. This repository writes the noun the British way in its own prose, and the
            // command follows the tools rather than the prose - see CommandKind.License.
            "license" => CommandKind.License,
            _ => CommandKind.None
        };

        return kind != CommandKind.None;
    }

    /// <summary>
    /// Which command the word after <c>tool</c> stands for, or nothing when no tool is called that.
    ///
    /// <b>The catalogue decides which tools exist, and this decides what each is on the command line</b> -
    /// the same split the window makes between the catalogue and the section it draws for each tool. An
    /// arm per tool rather than a rule, and the default refuses: a tool added to the catalogue and not
    /// here is caught by a test walking the catalogue (ToolCommandTests), not discovered by a person.
    /// </summary>
    internal static bool TryTool(string argument, out CommandKind kind)
    {
        kind = ToolCatalog.Named(argument) switch
        {
            { } tool when tool == ToolCatalog.Fix => CommandKind.Fixes,
            _ => CommandKind.None
        };

        return kind != CommandKind.None;
    }

    internal static bool Matches(string argument, string option) =>
        argument.Equals(option, StringComparison.OrdinalIgnoreCase);
}
