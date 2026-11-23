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
    /// The program, word for word, and said never to run when no item runs a program - measured 2026-11-23
    /// on a clean Windows, MSiSCSI names one beside a list of restarts.
    /// </summary>
    internal static string Command(Reading<RecoverySettings> reading) =>
        CellFaces.Say(reading, settings => Named(
            settings.List.Command, settings.List, RecoveryAction.RunProgram, "gui.recovery.command.unused"));

    /// <summary>The message before a restart of the computer, the same way as the program.</summary>
    internal static string RebootMessage(Reading<RecoverySettings> reading) =>
        CellFaces.Say(reading, settings => Named(
            settings.List.RebootMessage, settings.List, RecoveryAction.RestartComputer, "gui.recovery.message.unused"));

    /// <summary>
    /// One side of a changed recovery in a comparison, on one line. Null for a value that is not a
    /// recovery, which the caller then shows as it is - a file can hold anything.
    /// </summary>
    internal static string? Line(string? json)
    {
        if (json is null || Parsed(json) is not { } recovery || recovery.Actions is null)
        {
            return null;
        }

        string?[] parts =
        [
            Actions(recovery),
            Reset(recovery),
            recovery.Command is { } command ? Texts.Of("gui.recovery.line.command", command) : null,
            recovery.RebootMessage is { } message ? Texts.Of("gui.recovery.line.message", message) : null,
            recovery.Actions.Count > 0 && recovery.OnNonCrashFailures ? Texts.Of("gui.recovery.line.nonCrash") : null
        ];

        return string.Join(", ", parts.OfType<string>());
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

    private static string Named(string? text, RecoveryList list, RecoveryAction action, string unusedKey) =>
        text is null
            ? string.Empty
            : list.Items.Any(item => item.Action == action) ? text : Texts.Of(unusedKey, text);

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
