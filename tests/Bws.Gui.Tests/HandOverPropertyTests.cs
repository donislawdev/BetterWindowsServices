using System.Buffers.Text;
using System.Text;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;
using CsCheck;

namespace Bws.Gui.Tests;

/// <summary>
/// What a window without administrator rights hands to the one it starts with them - step 3b of the
/// fuzzing project, point C5 (docs/PROJEKT-FUZZING-20261016.md, outside the repository).
///
/// <b>Two promises, from the two ends of the argument.</b> The writing end promises that what it writes
/// is always something the reading end accepts, and that what does not fit is left behind whole and
/// said - HandOver.Encode says so in as many words, and until 2026-10-05 it was untrue for a long query.
/// The reading end promises that anything it takes is something this window could itself have
/// written, because the argument reaches a process with administrator rights and is read as hostile.
///
/// <b>The shapes of an open plan are written once, here, as a set</b> - not as a second copy of the
/// checker's conditions. The first property draws only from it, the second holds whatever came through
/// damage to it, so a checker that let through a shape the window never writes turns this red.
/// </summary>
public sealed class HandOverPropertyTests
{
    /// <summary>
    /// Every plan the window can have open when somebody restarts: none, a start, a stop or a restart
    /// (forced or not, with or without the dependants first), or a startup setting - with the stop riding
    /// on it only beside Disabled, which is the one offer MainWindow.Menu makes.
    /// </summary>
    private static readonly HashSet<(ActionKind? Asked, StartSetting? To, bool AlsoStop, bool Dependents)> WriterShapes =
    [
        (null, null, false, false),
        (ActionKind.Start, null, false, false),
        .. new[] { ActionKind.Stop, ActionKind.Restart, ActionKind.ForceStop, ActionKind.ForceRestart }
            .SelectMany(kind => new[] { ((ActionKind?)kind, (StartSetting?)null, false, false), (kind, null, false, true) }),
        .. Enum.GetValues<StartSetting>().Select(setting => ((ActionKind?)ActionKind.SetStartType, (StartSetting?)setting, false, false)),
        (ActionKind.SetStartType, StartSetting.Disabled, true, false)
    ];

    private static readonly Gen<string> Letter = Gen.Frequency(
        (6, Gen.Char[' ', '~'].Select(character => character.ToString())),
        (2, Gen.Char[(char)0x00A0, (char)0xD7FF].Select(character => character.ToString())),
        (1, Gen.Int[0x10000, 0x10FFFF].Select(char.ConvertFromUtf32)));

    /// <summary>A manager's name as the window reads one: never empty, never a control character, up to 256.</summary>
    private static readonly Gen<string> Name = Gen.Frequency(
        (8, Letter.Array[1, 40].Select(string.Concat)),
        (1, Gen.Char['a', 'z'].Array[200, 256].Select(letters => new string(letters))));

    /// <summary>
    /// What was in the box: short, around the 4096 the reading end allows, or Polish letters that cost
    /// six characters each once escaped. No lone surrogate - that is backlog 590, decided, and it would
    /// cross as U+FFFD rather than as itself.
    /// </summary>
    private static readonly Gen<string> Query = Gen.Frequency(
        (6, Gen.Frequency((8, Letter), (1, Gen.OneOfConst("\t", "\n", "\""))).Array[0, 30].Select(string.Concat)),
        (1, Gen.Char['a', 'z'].Array[4000, 4200].Select(letters => new string(letters))),
        (1, Gen.Char[(char)0x0104, (char)0x017C].Array[500, 1600].Select(letters => new string(letters))));

    private static readonly Gen<HandOver> Handed = Gen.Select(
        Gen.OneOfConst(Enum.GetValues<EntryScope>()),
        Query,
        Gen.Frequency((6, Name.Array[0, 8]), (1, Name.Array[20, 80])),
        Gen.OneOfConst([.. WriterShapes]),
        (scope, query, picked, plan) => new HandOver
        {
            Scope = scope,
            Query = query,
            Picked = picked,
            Asked = plan.Asked,
            To = plan.To,
            AlsoStop = plan.AlsoStop,
            Dependents = plan.Dependents
        });

    [Fact]
    [Trait("search", "weekly")]
    public void What_a_window_hands_over_comes_back_whole_or_says_what_was_left()
    {
        Handed.Searched(
            handed => Came(handed) is { } wrong ? throw new InvalidOperationException(wrong) : true,
            new(2_000) { Print = Described });
    }

    [Fact]
    [Trait("search", "weekly")]
    public void A_damaged_hand_over_is_taken_or_refused_whole_and_taken_only_in_a_shape_the_window_writes()
    {
        var argument = new HandOver
        {
            Scope = EntryScope.Drivers,
            Query = "status:running name:a*",
            Picked = ["Spooler", "AMD Crash Defender Service"],
            Asked = ActionKind.SetStartType,
            To = StartSetting.Disabled,
            AlsoStop = true
        }.Encode();

        var json = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(argument));

        // Both layers: damage to the JSON inside, encoded again, reaches the checker - damage to the
        // argument itself mostly reaches the decoder, which has to refuse it just as quietly.
        Gen.Select(Gen.Bool, Damaged.In(json), Damaged.In(argument))
            .Searched(
                drawn =>
                {
                    var (inside, toJson, toArgument) = drawn;
                    var text = inside
                        ? Base64Url.EncodeToString(Encoding.UTF8.GetBytes(toJson.To(json, '}')))
                        : toArgument.To(argument, '}');

                    var (carried, refused) = HandOver.Read([HandOver.Argument, text]);

                    if (refused == (carried is not null))
                    {
                        throw new InvalidOperationException("Taken and refused at once, or neither.");
                    }

                    if (carried is not null && Impossible(carried) is { } wrong)
                    {
                        throw new InvalidOperationException(wrong);
                    }

                    return true;
                },
                new(20_000) { Print = drawn => drawn.Item1 ? $"inside, {drawn.Item2}" : $"the argument, {drawn.Item3}" });
    }

    /// <summary>What did not come back as it went, or nothing.</summary>
    private static string? Came(HandOver handed)
    {
        var argument = handed.Encode();
        var (carried, refused) = HandOver.Read([HandOver.Argument, argument]);

        if (refused || carried is null)
        {
            return $"The reading end refused what the writing end wrote, {argument.Length} characters of it.";
        }

        return new[] { QueryCame(handed, carried), PicksCame(handed, carried), RoomLeft(handed, carried) }
            .FirstOrDefault(said => said is not null)
            ?? (carried.Scope == handed.Scope ? null : $"The scope came back as {carried.Scope}.");
    }

    private static string? QueryCame(HandOver handed, HandOver carried) =>
        (carried.QueryLeftBehind ? carried.Query.Length == 0 : carried.Query == handed.Query)
            ? null
            : $"The query came back {carried.Query.Length} long, left behind {carried.QueryLeftBehind}.";

    /// <summary>The picks whole and the plan over them, or neither and said - and a plan over nothing picked is no plan.</summary>
    private static string? PicksCame(HandOver handed, HandOver carried)
    {
        var kept = !carried.PickedLeftBehind && handed.Picked.Count > 0;
        var plan = kept ? Shape(handed) : (null, null, false, false);
        IReadOnlyList<string> picks = carried.PickedLeftBehind ? [] : handed.Picked;

        return carried.Picked.SequenceEqual(picks, StringComparer.Ordinal) && Shape(carried) == plan
            ? null
            : $"Came back {carried.Picked.Count} picked and {Shape(carried)}, left behind {carried.PickedLeftBehind}.";
    }

    /// <summary>A short query and a few short picks always fit, so nothing of them is ever left behind.</summary>
    private static string? RoomLeft(HandOver handed, HandOver carried) =>
        handed.Query.Length <= 200 && handed.Picked.Count <= 5 && handed.Picked.All(name => name.Length <= 80)
        && (carried.QueryLeftBehind || carried.PickedLeftBehind)
            ? "Something was left behind with room to spare."
            : null;

    /// <summary>What the window could not have written, or nothing - three questions asked apart.</summary>
    private static string? Impossible(HandOver carried) =>
        new[] { PlanImpossible(carried), NamesImpossible(carried), QueryImpossible(carried) }
            .FirstOrDefault(said => said is not null);

    /// <summary>A plan in a shape the window never has open, or any plan over nothing picked.</summary>
    private static string? PlanImpossible(HandOver carried) =>
        WriterShapes.Contains(Shape(carried)) && (carried.Picked.Count > 0 || Shape(carried) == (null, null, false, false))
            ? null
            : $"Taken with a plan the window never has open: {Shape(carried)} over {carried.Picked.Count} picked.";

    private static string? NamesImpossible(HandOver carried) =>
        carried.Picked.All(name => name.Length is > 0 and <= 256 && !name.Any(char.IsControl))
            ? null
            : "Taken with a name the manager never gives.";

    private static string? QueryImpossible(HandOver carried) =>
        carried.Query.Length <= 4096 && !(carried.QueryLeftBehind && carried.Query.Length > 0)
            ? null
            : $"Taken with a query of {carried.Query.Length}, left behind {carried.QueryLeftBehind}.";

    private static (ActionKind? Asked, StartSetting? To, bool AlsoStop, bool Dependents) Shape(HandOver handOver) =>
        (handOver.Asked, handOver.To, handOver.AlsoStop, handOver.Dependents);

    private static string Described(HandOver handed) =>
        $"{handed.Scope}, a query of {handed.Query.Length}, {handed.Picked.Count} picked, plan {Shape(handed)}";
}
