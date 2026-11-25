using Bws.Core;
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
        [CommandKind.Stop, CommandKind.Start, CommandKind.Restart, CommandKind.SetStartType,
            CommandKind.SetRecovery, CommandKind.Pause, CommandKind.Resume, CommandKind.Kill];

    /// <summary>
    /// A recovery change naming every part, with the values that are hardest to carry through a shell: a
    /// program with its own quotes, a dollar and <c>%1%</c>, a message removed, and every item kind.
    /// </summary>
    private static readonly RecoveryChange EveryPart = new(
        RecoveryParts.Actions | RecoveryParts.ResetPeriod | RecoveryParts.Command
            | RecoveryParts.RebootMessage | RecoveryParts.OnNonCrashFailures,
        new RecoverySettings(
            new RecoveryList(
                [
                    new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromMinutes(2)),
                    new RecoveryItem(RecoveryAction.RunProgram, TimeSpan.FromMilliseconds(1500)),
                    new RecoveryItem(RecoveryAction.RestartComputer, TimeSpan.FromDays(1)),
                    new RecoveryItem(RecoveryAction.Nothing, TimeSpan.FromSeconds(90))
                ],
                Timeout.InfiniteTimeSpan)
            {
                Command = "\"C:\\Program Files\\fix.cmd\" $env %1% -x",
                RebootMessage = null
            },
            true));

    /// <summary>
    /// Every ask the core has a verb for, with the value the fourth one needs.
    ///
    /// <b>Built from the enumeration rather than listed</b>, so a fifth ask arrives here without
    /// anybody remembering to add it - and arrives loudly, because a kind with no arm below fails
    /// to render at all.
    /// </summary>
    private static ServiceAction Asking(ActionKind kind, bool withDependents = false) =>
        new(kind, "Spooler", withDependents, kind == ActionKind.SetStartType ? StartSetting.Manual : null,
            Recovery: kind == ActionKind.SetRecovery ? EveryPart : null);

    private static ActionKind[] Rendered =>
        [.. Enum.GetValues<ActionKind>().Where(EquivalentCommand.HasAVerb)];

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

        foreach (var kind in Rendered)
        {
            // Asked for with the cascade, because that is the only way a switch appears at all today.
            //
            // AND IT IS THE ASK THAT CAUGHT THE SECOND FAULT THIS GUARD HAS FOUND. A selection
            // carries one cascade flag for every entry in it, so a start type change built from a
            // ticked box arrives here carrying one - and the core rendered --dependents onto it
            // until 2026-08-25, on a verb that refuses the switch and for an ask where nothing
            // cascades at all.
            var rendered = EquivalentCommand.For(Asking(kind, withDependents: true));
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
    ///
    /// <b>THROUGH THE READER RATHER THAN THROUGH THE NAME OF THE ENUMERATION VALUE, SINCE
    /// 2026-08-25.</b> It used to ask Enum.TryParse whether the rendered word matched a member of
    /// CommandKind, which worked for exactly as long as every verb was its member name lower-cased.
    /// The fourth is written "start-type" and its member is SetStartType, so the old check would
    /// have gone red on a verb that is perfectly good - and, worse, it would have stayed green for
    /// a member whose spelling nobody had taught the parser. Arguments.TryVerb is the thing that
    /// really reads what somebody types, so asking it is the stronger question as well as the one
    /// that survives a hyphen.
    /// </summary>
    [Fact]
    public void Every_verb_the_core_renders_is_one_this_tool_has()
    {
        foreach (var kind in Rendered)
        {
            var word = EquivalentCommand.For(Asking(kind)).Split(' ')[1];

            Assert.True(
                Arguments.TryVerb(word, out var parsed) && WriteVerbs.Contains(parsed),
                $"The core renders the verb '{word}' and this tool has no write command by that name.");
        }
    }

    /// <summary>
    /// The value the fourth verb needs is one this tool reads back as the same start type.
    ///
    /// <b>The half neither check above can see.</b> A verb can exist and a switch can belong to it
    /// and the line can still fail on paste, because this verb takes a word that is not a switch and
    /// not a name. The core writes it and the command line reads it, and a round trip through both
    /// is the only thing that says they agree.
    /// </summary>
    [Theory]
    [InlineData(StartSetting.Automatic)]
    [InlineData(StartSetting.AutomaticDelayed)]
    [InlineData(StartSetting.Manual)]
    [InlineData(StartSetting.Disabled)]
    public void The_start_type_the_core_writes_is_one_this_tool_reads_back(StartSetting type)
    {
        var word = EquivalentCommand
            .For(new ServiceAction(ActionKind.SetStartType, "Spooler", To: type))
            .Split(' ')[3];

        Assert.Equal(type, WriteCommands.Named(word));
    }

    /// <summary>
    /// A name that needs quoting, typed the way the core writes it, reaches this tool as that name.
    ///
    /// <b>Stability report W-12, owner's decision 2026-10-05.</b> The line is cut the way PowerShell
    /// cuts it - measured on pwsh 7.6.6 that day: a word in double quotes arrives whole, a word in
    /// single quotes arrives whole with each doubled quote mark made one, <c>--</c> reaches the
    /// program - and then read by the same reader that reads a real command line. Every switch has to
    /// survive too, which is what catches a switch written after <c>--</c>.
    /// </summary>
    [Theory]
    [InlineData("AMD Crash Defender Service")]
    [InlineData("MSSQL$SQLEXPRESS")]
    [InlineData("it's here")]
    [InlineData("50% off")]
    [InlineData("a\"b")]
    [InlineData("-leading")]
    [InlineData("-leading $and space")]
    [InlineData("typographic 0x2019 0x201D")]
    public void A_name_that_needs_quoting_reaches_this_tool_as_itself(string name)
    {
        name = name.Replace("0x2019", char.ConvertFromUtf32(0x2019), StringComparison.Ordinal)
            .Replace("0x201D", char.ConvertFromUtf32(0x201D), StringComparison.Ordinal);

        foreach (var action in new[]
        {
            new ServiceAction(ActionKind.Stop, name, IncludeDependents: true),
            new ServiceAction(ActionKind.SetStartType, name, To: StartSetting.Disabled, AlsoStop: true),
            new ServiceAction(ActionKind.ForceRestart, name, Immediate: true)
        })
        {
            var line = EquivalentCommand.For(action);
            var read = CommandLine.Read([.. new PowerShellWords().Of(line).Skip(1)]);

            Assert.True(read.ServiceName == name, $"'{line}' reached this tool as the name '{read.ServiceName}'.");
            Assert.True(read.Rejected.Count == 0 && read.Extra.Count == 0, $"'{line}' left words this tool refuses.");
            Assert.Equal(action.IncludeDependents, read.Dependents);
            Assert.Equal(action.AlsoStop, read.Setting.AlsoStop);
            Assert.Equal(action.Immediate, read.Force);
        }
    }

    /// <summary>
    /// The recovery line the core writes - for an ask and for a way back alike, one renderer - is read back by
    /// this tool as the same change, part for part and value for value. Backlog 555 part (a2), 2026-11-24.
    ///
    /// <b>The bridge the way back stands on</b>: a line that pastes and reads back as a different list puts
    /// back something nobody had. Cut the way PowerShell cuts it, as the quoting test above does, and with a
    /// name that begins with a dash, so the items after <c>--</c> are checked too.
    /// </summary>
    [Theory]
    [InlineData("Spooler")]
    [InlineData("-leading $and space")]
    public void The_recovery_the_core_writes_is_the_recovery_this_tool_reads_back(string name)
    {
        RecoveryChange[] changes =
        [
            EveryPart,
            EveryPart with { Parts = RecoveryParts.Actions | RecoveryParts.ResetPeriod, To = EveryPart.To with { List = new RecoveryList([], TimeSpan.Zero) } },
            EveryPart with { Parts = RecoveryParts.Command | RecoveryParts.RebootMessage, To = EveryPart.To with { List = EveryPart.To.List with { Command = "-dash", RebootMessage = "it's \"done\"" } } },
            EveryPart with { Parts = RecoveryParts.OnNonCrashFailures, To = EveryPart.To with { OnNonCrashFailures = false } }
        ];

        foreach (var change in changes)
        {
            var line = EquivalentCommand.For(new ServiceAction(ActionKind.SetRecovery, name, Recovery: change));
            var read = CommandLine.Read([.. new PowerShellWords().Of(line).Skip(1)]);
            var back = RecoveryAsks.Change(read.Recovery);

            Assert.True(read.Kind == CommandKind.SetRecovery && read.ServiceName == name, $"'{line}' read as {read.Kind} {read.ServiceName}.");
            Assert.True(read.NothingWrong, $"'{line}' left something this tool refuses.");
            Assert.True(back is not null, $"'{line}' did not read back as a change.");

            // The reader names only the parts on the line - the empty list writes its period of zero by itself.
            Assert.Equal(change.Parts & ~(change.To.List.Items.Count == 0 ? RecoveryParts.ResetPeriod : RecoveryParts.None), back!.Parts);
            Assert.True(change.Agrees(change.To, back.To), $"'{line}' read back other values.");
        }
    }

    /// <summary>
    /// The verb an ask is checked against: the command of the same name, and kill for both forcing asks.
    ///
    /// <b>By name, with no fallback.</b> A fallback to one verb checks every ask it catches against that
    /// verb's switch list - and the guard then reports a fault in the core that is really a fault in this
    /// line. An ask with no command of its name throws here instead.
    /// </summary>
    private static CommandKind Verb(ActionKind kind) =>
        kind is ActionKind.ForceStop or ActionKind.ForceRestart
            ? CommandKind.Kill
            : Enum.Parse<CommandKind>(kind.ToString());
}
