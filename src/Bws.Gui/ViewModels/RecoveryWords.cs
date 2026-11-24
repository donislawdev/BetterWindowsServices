using System.Globalization;
using System.Text.Json;
using Bws.Core;
using Bws.Core.Snapshots;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What Windows does when a service fails, in the window's words - the five cells of the "When it fails"
/// columns, which the details panel shows as well, and one side of a changed recovery in a comparison.
///
/// <b>Made from <see cref="RecoveryDocument"/>, the form the JSON and the snapshot hold</b>, so a recovery
/// reads the same in a live entry and in a snapshot (backlog 555, 2026-11-23). The command line has its own
/// copy in Bws.Cli, because the two interfaces keep their own words and their own files of them.
///
/// <b>The two units moved here from the plan's words that day</b>, so the plan and the panel say "1 h" and
/// "120 s" the same way - the owner's decision of 2026-10-07.
/// </summary>
internal static class RecoveryWords
{
    private static readonly JsonSerializerOptions Read = new(JsonSerializerDefaults.Web);

    /// <summary>The list on one line: "1: restart after 120 s, 2: restart after 300 s, 3+: no action".</summary>
    internal static string Actions(Reading<RecoverySettings> reading) =>
        CellFaces.Say(reading, settings => Actions(RecoveryDocument.Of(settings)));

    /// <summary>When the count starts from 1 again - empty for a list with no items, where it decides nothing.</summary>
    internal static string Reset(Reading<RecoverySettings> reading) =>
        CellFaces.Say(reading, settings => Reset(RecoveryDocument.Of(settings)) ?? string.Empty);

    /// <summary>Whether a stop with an error counts as a failure too - empty for a list with no items.</summary>
    internal static string OnNonCrashFailures(Reading<RecoverySettings> reading) =>
        CellFaces.Say(reading, settings => settings.List.Items.Count == 0
            ? string.Empty
            : Texts.Of(settings.OnNonCrashFailures ? "gui.cell.yes" : "gui.cell.no"));

    /// <summary>
    /// The program, word for word, and said to be unused when no item runs a program - measured 2026-11-23
    /// on a clean Windows, MSiSCSI names one beside a list of restarts.
    ///
    /// <b>"Not used" comes first and the command after it</b>, because that is what decides anything for the
    /// person reading. With the command first, the cell read as a program this service runs.
    ///
    /// <b>No quotes around the command</b>, unlike the message: a command line carries quotes of its own, and
    /// one more pair around <c>"C:\Tools\a.exe" /p</c> would read as two quoted halves.
    /// </summary>
    internal static string Command(Reading<RecoverySettings> reading) =>
        CellFaces.Say(reading, settings => settings.List.Command is not { } command
            ? string.Empty
            : Uses(settings.List, RecoveryAction.RunProgram)
                ? command
                : Texts.Of("gui.recovery.command.unused", command));

    /// <summary>
    /// The message before a restart of the computer, the same way as the program - and IN QUOTES, used or not.
    ///
    /// <b>The quotes are what keep it from reading as the window's own words.</b> It is prose somebody else
    /// wrote: on a clean Windows Server 2025, MSiSCSI holds "See Note 3 below", and the panel showed it bare -
    /// an instruction that seemed to come from this tool and pointed at nothing (2026-11-24, the owner).
    /// </summary>
    internal static string RebootMessage(Reading<RecoverySettings> reading) =>
        CellFaces.Say(reading, settings => settings.List.RebootMessage is not { } message
            ? string.Empty
            : Uses(settings.List, RecoveryAction.RestartComputer)
                ? Texts.Of("gui.recovery.message.used", message)
                : Texts.Of("gui.recovery.message.unused", message));

    /// <summary>
    /// One side of a changed recovery in a comparison, on one line. Null for a value that is not a
    /// recovery, which the caller then shows as it is - a file can hold anything.
    /// </summary>
    internal static string? Line(string? json)
    {
        if (json is null || Parsed(json) is not { } recovery || recovery.Actions is null || recovery.Actions.Any(action => action is null))
        {
            return null;
        }

        string?[] parts =
        [
            Actions(recovery),
            Reset(recovery),
            recovery.Command is not { } command ? null
                : Uses(recovery, nameof(RecoveryAction.RunProgram)) ? Texts.Of("gui.recovery.line.command", command)
                : Texts.Of("gui.recovery.line.command.unused", command),
            recovery.RebootMessage is not { } message ? null
                : Uses(recovery, nameof(RecoveryAction.RestartComputer)) ? Texts.Of("gui.recovery.line.message", message)
                : Texts.Of("gui.recovery.line.message.unused", message),
            recovery.Actions.Count > 0 && recovery.OnNonCrashFailures ? Texts.Of("gui.recovery.line.nonCrash") : null
        ];

        return string.Join(", ", parts.OfType<string>());
    }

    /// <summary>
    /// Both sides of a changed recovery in words when the words tell them apart, and neither when they do
    /// not - the caller then shows the two values as they are. The words leave out what decides nothing,
    /// such as the period of an empty list, so two values the comparison found different can read the same,
    /// and a difference must never be shown as two equal sides.
    /// </summary>
    internal static (string? Before, string? After) Sides(string? before, string? after)
    {
        var (worded, otherWorded) = (Line(before), Line(after));

        return worded == otherWorded ? (null, null) : (worded, otherWorded);
    }

    /// <summary>A reset period in the largest unit that divides it evenly - 30 s, 15 min, 1 h, 24 h - the owner's decision of 2026-10-07.</summary>
    internal static string Period(TimeSpan period) => (long)period.TotalSeconds switch
    {
        var seconds when seconds % 3600 == 0 => Texts.Of("gui.plan.recovery.hours", seconds / 3600),
        var seconds when seconds % 60 == 0 => Texts.Of("gui.plan.recovery.minutes", seconds / 60),
        var seconds => Texts.Of("gui.plan.recovery.seconds", seconds)
    };

    /// <summary>A delay, always in seconds with one decimal place.</summary>
    internal static string Seconds(TimeSpan delay) =>
        Texts.Of("gui.plan.recovery.seconds", delay.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture));

    private static string Actions(RecoveryDocument recovery)
    {
        var actions = recovery.Actions;

        if (actions.Count == 0)
        {
            return Texts.Of("gui.recovery.none");
        }

        if (actions.Count == 1)
        {
            return Texts.Of("gui.recovery.every", Action(actions[0]));
        }

        return string.Join(
            ", ",
            actions.Select((action, index) => index == actions.Count - 1
                ? Texts.Of("gui.recovery.failureAndLater", index + 1, Action(action))
                : Texts.Of("gui.recovery.failure", index + 1, Action(action))));
    }

    private static string? Reset(RecoveryDocument recovery) =>
        recovery.Actions.Count == 0
            ? null
            : recovery.ResetPeriodSeconds switch
            {
                RecoveryDocument.NeverReset => Texts.Of("gui.recovery.reset.atBoot"),

                // Measured on the throwaway machine on 2026-10-07: zero makes every failure the first.
                0 => Texts.Of("gui.recovery.reset.atOnce"),
                var seconds => Texts.Of("gui.recovery.reset.after", Period(TimeSpan.FromSeconds(seconds)))
            };

    private static string Action(RecoveryActionDocument action)
    {
        var after = Seconds(TimeSpan.FromMilliseconds(action.DelayMilliseconds));

        return action.Action switch
        {
            nameof(RecoveryAction.Nothing) => Texts.Of("gui.recovery.action.nothing"),
            nameof(RecoveryAction.RestartService) => Texts.Of("gui.recovery.action.restartService", after),
            nameof(RecoveryAction.RunProgram) => Texts.Of("gui.recovery.action.runProgram", after),
            nameof(RecoveryAction.RestartComputer) => Texts.Of("gui.recovery.action.restartComputer", after),

            // Unnamed, and anything a later build may write - named as unknown, never as the likeliest.
            _ => Texts.Of("gui.recovery.action.unnamed", after)
        };
    }

    /// <summary>
    /// Whether some item of the list does this - the question behind "not used". Each key stays written
    /// inside its own Texts.Of at the caller, where TextKeyGuards can see it: a key handed to a helper as an
    /// argument is one nothing reading the source finds (2026-11-24, three keys went unseen that way).
    /// </summary>
    private static bool Uses(RecoveryList list, RecoveryAction action) => list.Items.Any(item => item.Action == action);

    /// <summary>The same question of a recovery as a comparison holds it, with the kind in its written word.</summary>
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
