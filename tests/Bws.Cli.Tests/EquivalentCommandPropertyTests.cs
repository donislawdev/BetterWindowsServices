using Bws.Core.Planning;
using CsCheck;

namespace Bws.Cli.Tests;

/// <summary>
/// The line a plan hands somebody to paste, typed back into this tool, is the same ask - for any
/// name the service manager lets an entry have.
///
/// EquivalentCommandBridgeGuards asks this of eight names somebody thought of (stability report
/// W-12). A name is chosen by whoever wrote the installer, and the manager refuses only two
/// characters in it - the slash and the backslash - so the names worth asking about are exactly
/// the ones nobody thinks of.
///
/// <b>The ask is compared by rendering it again.</b> The words are read by the reader that reads a
/// real command line, turned into an ask the way Program.cs turns them, and written out once more:
/// the same line means every switch and the value survived as well as the name. And the line has to
/// pass every refusal this tool makes before it opens the manager, because a line refused on paste is
/// the failure this exists to catch.
/// </summary>
public sealed class EquivalentCommandPropertyTests
{
    [Fact]
    [Trait("search", "weekly")]
    public void Any_name_typed_the_way_the_plan_writes_it_reaches_this_tool_through_PowerShell_as_the_same_ask()
    {
        Asks.Searched(
            action => !EquivalentCommand.Renders(action) || SameAsk(action, new PowerShellWords().Of(EquivalentCommand.For(action))),
            new(20_000) { Print = Said });
    }

    [Fact]
    [Trait("search", "weekly")]
    public void Any_name_the_plan_writes_for_cmd_too_reaches_this_tool_through_the_Windows_parser_as_the_same_ask()
    {
        Asks.Searched(
            action => !EquivalentCommand.Renders(action)
                || EquivalentCommand.Typed(action.ServiceName).StartsWith('\'')
                || SameAsk(action, WindowsArguments.Of(EquivalentCommand.For(action))),
            new(20_000) { Print = Said });
    }

    /// <summary>
    /// Characters the manager allows in a name, weighted towards the ones a name is usually made
    /// of. Printable only, because each name is a key under Services and a key name is printable.
    /// </summary>
    private static readonly Gen<char> NameCharacter = Gen.Frequency(
        (12, Gen.OneOfConst([.. "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-"])),
        (3, Gen.Const(' ')),
        (3, Gen.OneOfConst([.. "$`%\"'&|<>^!()[]{}@#;,=+~?*"])),
        (2, Gen.OneOfConst((char)0x2018, (char)0x2019, (char)0x201A, (char)0x201B, (char)0x201C, (char)0x201D, (char)0x201E)),
        (2, Gen.OneOfConst((char)0x0105, (char)0x00DF, (char)0x03A9, (char)0x4E2D, (char)0x00E9, (char)0x00A0, (char)0x2013)));

    private static readonly Gen<string> Names = Gen.Frequency(
        (10, Gen.String[NameCharacter, 1, 24]),
        (2, Gen.String[NameCharacter, 0, 12].Select(name => "-" + name)),
        (1, Gen.String[NameCharacter, 200, 256]));

    /// <summary>
    /// Every ask the plan can be asked to render, with two rules taken from the product rather than
    /// left to chance - each one an ask this tool refuses everywhere, so nobody can make it, and the
    /// first two runs of this property drew one each and saw it refused, which is the refusal working.
    ///
    /// Stopping as well rides only on a start type of disabled: the command line refuses --stop on any
    /// other type (Refusals, cli.stopNeedsDisabled) and the window offers it on disabled alone
    /// (MainWindow.Menu.cs). And an ending forced straight away never takes the dependants with it: the
    /// command line refuses --force beside --dependents (Refusals.AboutTheForcing), the core refuses the
    /// same shape (ForcedStop), and the window never asks for the forcing at all.
    /// </summary>
    private static readonly Gen<ServiceAction> Asks =
        from kind in Gen.OneOfConst(Enum.GetValues<ActionKind>())
        from name in Names
        from dependents in Gen.Bool
        from to in Gen.OneOfConst(Enum.GetValues<StartSetting>())
        from immediate in Gen.Bool
        from alsoStop in Gen.Bool
        select new ServiceAction(
            kind,
            name,
            dependents && !(immediate && kind is ActionKind.ForceStop or ActionKind.ForceRestart),
            kind == ActionKind.SetStartType ? to : null,
            immediate,
            alsoStop && kind == ActionKind.SetStartType && to == StartSetting.Disabled);

    private static bool SameAsk(ServiceAction action, IReadOnlyList<string> words)
    {
        var line = EquivalentCommand.For(action);
        var read = CommandLine.Read([.. words.Skip(1)]);

        return words.Count > 0
            && words[0] == "bws"
            && read.ServiceName == action.ServiceName
            && read.NothingWrong
            && Refusals.Answer(read) is null
            && EquivalentCommand.For(TypedBack(read)) == line;
    }

    /// <summary>
    /// The ask this tool builds out of what it read - Program.cs, where the plan is built, in the same
    /// words. Copied rather than called, because that is a top level statement nothing can call.
    /// </summary>
    private static ServiceAction TypedBack(CommandLine read) =>
        new(read.Action,
            read.ServiceName,
            read.Dependents,
            WriteCommands.NeedsAStartType(read.Kind) ? WriteCommands.Named(read.Setting.Word) : null,
            read.Force,
            read.Setting.AlsoStop);

    private static string Said(ServiceAction action) =>
        $"{action} rendered <{(EquivalentCommand.Renders(action) ? EquivalentCommand.For(action) : "nothing")}> - name in code points: "
        + string.Join(' ', action.ServiceName.Select(character => ((int)character).ToString("X4", System.Globalization.CultureInfo.InvariantCulture)));
}
