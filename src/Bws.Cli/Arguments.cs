using System.Globalization;

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
    /// </summary>
    internal static string? Seconds(string value, ref TimeSpan timeout)
    {
        // Invariant, not the machine's regional settings. A timeout is typed by whoever wrote
        // the runbook, and a runbook that means sixty on one machine and nothing on another
        // because of a decimal separator is exactly what rule 3 exists to stop.
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || seconds < 1)
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
            _ => CommandKind.None
        };

        return kind != CommandKind.None;
    }

    internal static bool Matches(string argument, string option) =>
        argument.Equals(option, StringComparison.OrdinalIgnoreCase);
}
