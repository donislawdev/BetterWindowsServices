using System.Globalization;
using System.Text.Json;
using Bws.Core;
using Bws.Core.Snapshots;

namespace Bws.Cli;

/// <summary>
/// What Windows does when a service fails, in words - for <c>bws show</c>, for a comparison of two
/// snapshots, and for the plan that ends a process.
///
/// <b>Made from <see cref="RecoveryDocument"/>, the form the JSON and the snapshot hold</b>, for the reason
/// <see cref="TriggerWords"/> gives: a recovery reads the same in a live entry and in a snapshot when both
/// are worded from one form (backlog 555, 2026-11-23).
///
/// <b>The two units moved here from the plan's own text that day</b>, so the plan and <c>bws show</c> say
/// "1 h" and "120 s" the same way - the owner's decision of 2026-10-07 for the period, and "after N s" for
/// a delay.
/// </summary>
internal static class RecoveryWords
{
    private static readonly JsonSerializerOptions Read = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The list as the lines <c>bws show</c> prints: "Failure 1: restart the service after 120 s", and the
    /// last item as "Failure 3 and later", because the manager repeats it past the end of the list
    /// (<c>SERVICE_FAILURE_ACTIONSW</c>). One item is "Every failure". No items is one line saying so.
    /// </summary>
    internal static string Actions(RecoveryDocument recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);

        var actions = recovery.Actions;

        if (actions.Count == 0)
        {
            return Texts.Of("cli.recovery.none");
        }

        if (actions.Count == 1)
        {
            return Texts.Of("cli.recovery.every", Action(actions[0]));
        }

        return string.Join(
            '\n',
            actions.Select((action, index) => index == actions.Count - 1
                ? Texts.Of("cli.recovery.failureAndLater", index + 1, Action(action))
                : Texts.Of("cli.recovery.failure", index + 1, Action(action))));
    }

    /// <summary>
    /// When the count starts from 1 again. Null for a list with no items, where the period decides nothing
    /// and a driver would otherwise print "at once" about a recovery it does not have.
    /// </summary>
    internal static string? Reset(RecoveryDocument recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);

        return recovery.Actions.Count == 0
            ? null
            : recovery.ResetPeriodSeconds switch
            {
                RecoveryDocument.NeverReset => Texts.Of("cli.recovery.reset.atBoot"),

                // Measured on the throwaway machine on 2026-10-07: zero makes every failure the first.
                0 => Texts.Of("cli.recovery.reset.atOnce"),
                var seconds => Texts.Of("cli.recovery.reset.after", Period(TimeSpan.FromSeconds(seconds)))
            };
    }

    /// <summary>
    /// The program, word for word - and when no item runs a program, "not used" first and the command after
    /// it. Measured 2026-11-23 on a clean Windows: MSiSCSI names one beside a list of restarts. The window's
    /// copy of this class says why the order and why no quotes: a command line carries quotes of its own.
    /// </summary>
    internal static string? Command(RecoveryDocument recovery) =>
        Named(recovery, recovery.Command, nameof(RecoveryAction.RunProgram), null, "cli.recovery.command.unused");

    /// <summary>
    /// The message before a restart of the computer, the same way as the program - and in quotes, used or
    /// not, because it is prose somebody else wrote. MSiSCSI on a clean Windows Server 2025 holds "See Note 3
    /// below", which printed bare reads as this tool pointing at a note that is not there.
    /// </summary>
    internal static string? RebootMessage(RecoveryDocument recovery) =>
        Named(
            recovery, recovery.RebootMessage, nameof(RecoveryAction.RestartComputer), "cli.recovery.message.used",
            "cli.recovery.message.unused");

    /// <summary>Whether a stop with an error counts as a failure too. Null with no items, as for the period.</summary>
    internal static string? OnNonCrashFailures(RecoveryDocument recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);

        return recovery.Actions.Count == 0
            ? null
            : Texts.Of(recovery.OnNonCrashFailures ? "cli.show.yes" : "cli.show.no");
    }

    /// <summary>
    /// One side of a changed recovery in a comparison, on one line: the list, then the period, the program
    /// and the message when there are any. Null for a value that is not a recovery, which the caller then
    /// prints as it is - a file can hold anything, and a difference must never be printed as nothing.
    /// </summary>
    internal static string? Line(string? json)
    {
        if (json is null || Parsed(json) is not { } recovery || recovery.Actions is null || recovery.Actions.Any(action => action is null))
        {
            return null;
        }

        string?[] parts =
        [
            Actions(recovery).Replace('\n', ' '),
            Reset(recovery),
            recovery.Command is not { } command ? null
                : Uses(recovery, nameof(RecoveryAction.RunProgram)) ? Texts.Of("cli.recovery.line.command", command)
                : Texts.Of("cli.recovery.line.command.unused", command),
            recovery.RebootMessage is not { } message ? null
                : Uses(recovery, nameof(RecoveryAction.RestartComputer)) ? Texts.Of("cli.recovery.line.message", message)
                : Texts.Of("cli.recovery.line.message.unused", message),
            recovery.Actions.Count > 0 && recovery.OnNonCrashFailures ? Texts.Of("cli.recovery.line.nonCrash") : null
        ];

        return string.Join(", ", parts.OfType<string>());
    }

    /// <summary>
    /// Both sides of a changed recovery in words when the words tell them apart, and neither when they do
    /// not - the caller then prints the two values as they are. The words leave out what decides nothing,
    /// such as the period of an empty list, so two values the comparison found different can read the same,
    /// and a difference must never be printed as two equal lines.
    /// </summary>
    internal static (string? Before, string? After) Sides(string? before, string? after)
    {
        var (worded, otherWorded) = (Line(before), Line(after));

        return worded == otherWorded ? (null, null) : (worded, otherWorded);
    }

    /// <summary>
    /// A reset period in the largest unit that divides it evenly - 30 s, 15 min, 1 h, 24 h, 390 min - the
    /// owner's decision of 2026-10-07. Periods on the owner's machine run from 30 s to 86 400 000 s, so
    /// seconds alone would read "86400 s" for the commonest one.
    /// </summary>
    internal static string Period(TimeSpan period) => (long)period.TotalSeconds switch
    {
        var seconds when seconds % 3600 == 0 => Texts.Of("cli.plan.recovery.hours", seconds / 3600),
        var seconds when seconds % 60 == 0 => Texts.Of("cli.plan.recovery.minutes", seconds / 60),
        var seconds => Texts.Of("cli.run.took.seconds", seconds)
    };

    /// <summary>
    /// Always in seconds, with one decimal place - the owner's decision said "after N s", and 100 ms reads
    /// as "0.1 s" rather than switching units inside one list.
    /// </summary>
    internal static string Seconds(TimeSpan delay) =>
        Texts.Of("cli.run.took.seconds", delay.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture));

    private static string Action(RecoveryActionDocument action)
    {
        var after = Seconds(TimeSpan.FromMilliseconds(action.DelayMilliseconds));

        return action.Action switch
        {
            nameof(RecoveryAction.Nothing) => Texts.Of("cli.recovery.action.nothing"),
            nameof(RecoveryAction.RestartService) => Texts.Of("cli.recovery.action.restartService", after),
            nameof(RecoveryAction.RunProgram) => Texts.Of("cli.recovery.action.runProgram", after),
            nameof(RecoveryAction.RestartComputer) => Texts.Of("cli.recovery.action.restartComputer", after),

            // Unnamed, and anything a later build may write - named as unknown, never as the likeliest.
            _ => Texts.Of("cli.recovery.action.unnamed", after)
        };
    }

    /// <param name="usedKey">How the text is said when an item uses it - null for word for word.</param>
    private static string? Named(RecoveryDocument recovery, string? text, string action, string? usedKey, string unusedKey)
    {
        if (text is null)
        {
            return null;
        }

        if (!Uses(recovery, action))
        {
            return Texts.Of(unusedKey, text);
        }

        return usedKey is null ? text : Texts.Of(usedKey, text);
    }

    /// <summary>Whether some item of the list does this - the question behind "not used".</summary>
    private static bool Uses(RecoveryDocument recovery, string action) => recovery.Actions.Any(item => item.Action == action);

    private static RecoveryDocument? Parsed(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<RecoveryDocument>(json, Read);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
