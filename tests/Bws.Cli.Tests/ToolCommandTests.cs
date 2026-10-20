using Bws.Core.Planning;
using Bws.Core.Tools;

namespace Bws.Cli.Tests;

/// <summary>
/// <c>bws tool</c> and <c>bws tool fix</c> - the command line half of the Tools tab
/// (`docs/PROJEKT-TOOLS-20261020.md`, owner's decisions T-c and T-g).
///
/// <b>The claim worth guarding is that a fix IS a restart with dependants</b>, read as one the moment the
/// words are read, so the rest of the run cannot treat it differently. Measured the same day on the owner's
/// machine: <c>bws tool fix printer --dry-run</c> and <c>bws restart Spooler --dependents --dry-run</c> printed
/// identical text, and the two JSON documents for the sound fix were identical too.
///
/// <b>Every tool and every fix is walked rather than named</b>, so the next one is covered the day it is
/// added - including the case nobody would write a test for: a tool in the catalogue with no word on the
/// command line, which is the parity the owner's decision behind the ideas report asks for.
/// </summary>
[Collection(nameof(SharedConsole))]
public sealed class ToolCommandTests
{
    [Fact]
    public void Every_fix_reads_as_a_restart_of_its_service_with_its_dependants()
    {
        foreach (var fix in QuickFixes.All)
        {
            var options = CommandLine.Read(["tool", "fix", fix.Name]);

            Assert.Equal(CommandKind.Fix, options.Kind);
            Assert.True(options.NothingWrong);
            Assert.True(options.IsWrite);
            Assert.Equal(fix.Ask, new ServiceAction(options.Action, options.ServiceName, options.Dependents));
            Assert.Null(Refusals.Answer(options));
        }
    }

    [Fact]
    public void Every_tool_in_the_catalogue_has_a_word_and_a_line_on_the_command_line()
    {
        foreach (var tool in ToolCatalog.All)
        {
            Assert.True(Arguments.TryTool(tool.Name, out _), $"bws tool {tool.Name} is not a command.");
        }

        var (code, output) = Answered(["tool"]);

        Assert.Equal(ExitCode.Ok, code);
        Assert.All(ToolCatalog.All, tool => Assert.Contains(tool.Name, output, StringComparison.Ordinal));
    }

    [Fact]
    public void The_list_of_fixes_names_each_one_with_the_service_it_restarts()
    {
        var (code, output) = Answered(["tool", "fix"]);

        Assert.Equal(ExitCode.Ok, code);

        foreach (var fix in QuickFixes.All)
        {
            var line = Assert.Single(output.Split(Environment.NewLine), row => row.TrimStart().StartsWith(fix.Name + " ", StringComparison.Ordinal));

            Assert.Contains(fix.ServiceName, line, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("tool")]
    [InlineData("fix")]
    public void Neither_list_swallows_a_switch_somebody_typed(string listed)
    {
        // The licence's guard, for the same ordering trap: the lists are answered before the line is judged
        // (Immediate), so a switch beside one must leave a complaint behind - --dry-run belongs to a fix
        // somebody NAMED, and printing the list instead would drop it in silence.
        string[] words = listed == "tool" ? ["tool"] : ["tool", "fix"];
        var swallowed = new List<string>();

        foreach (var (option, _) in OptionSurface.Surface)
        {
            var options = CommandLine.Read([.. words, option]);

            if (options.NothingWrong || Immediate.Answer(options) is not null)
            {
                swallowed.Add(option);
            }
        }

        Assert.True(swallowed.Count == 0, "Accepted beside the list and dropped: " + string.Join(", ", swallowed));
    }

    [Fact]
    public void A_fix_refuses_dependents_typed_beside_it_rather_than_ignoring_the_word()
    {
        // A fix always takes its dependants along, so the switch would do nothing - and the reader sets the
        // dependants without the word being GIVEN, which is what keeps this refusal possible.
        var options = CommandLine.Read(["tool", "fix", "printer", "--dependents"]);

        Assert.Contains("--dependents", options.Misplaced);
        Assert.Equal(ExitCode.Usage, Quietly(() => Refusals.Answer(options)));
    }

    [Theory]
    [InlineData(new[] { "tool", "fix", "prnter" }, "printer")]
    [InlineData(new[] { "tool", "fxi" }, "fix")]
    [InlineData(new[] { "fix", "printer" }, "tool fix")]
    public void A_mistyped_tool_or_fix_is_offered_the_nearest_one(string[] words, string offered)
    {
        var (code, error) = Refused(words);

        Assert.Equal(ExitCode.Usage, code);
        Assert.Contains("Did you mean " + offered + "?", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_fix_nobody_is_near_lists_the_fixes_there_are()
    {
        var (code, error) = Refused(["tool", "fix", "bluetooth"]);

        Assert.Equal(ExitCode.Usage, code);
        Assert.Contains(string.Join(", ", QuickFixes.All.Select(fix => fix.Name)), error, StringComparison.Ordinal);
    }

    [Fact]
    public void The_line_a_card_shows_reads_back_as_the_fix_it_names()
    {
        // The window prints QuickFix.Typed at the foot of each card (owner's decision T-h). A line that
        // does not run would teach the wrong command, and nothing in the window could notice.
        foreach (var fix in QuickFixes.All)
        {
            var words = fix.Typed.Split(' ');

            Assert.Equal("bws", words[0]);

            var options = CommandLine.Read(words[1..]);

            Assert.Equal(CommandKind.Fix, options.Kind);
            Assert.Equal(fix.ServiceName, options.ServiceName);
        }
    }

    [Fact]
    public void A_fix_is_named_without_case()
    {
        var options = CommandLine.Read(["TOOL", "Fix", "PRINTER"]);

        Assert.Equal(CommandKind.Fix, options.Kind);
        Assert.Equal(QuickFixes.Printer.ServiceName, options.ServiceName);
    }

    private static (int? Code, string Output) Answered(string[] words)
    {
        string output = string.Empty;
        var code = Caught(() => Immediate.Answer(CommandLine.Read(words)), (said, _) => output = said);

        return (code, output);
    }

    private static (int? Code, string Error) Refused(string[] words)
    {
        string error = string.Empty;
        var code = Caught(() => Refusals.Answer(CommandLine.Read(words)), (_, said) => error = said);

        return (code, error);
    }

    private static int? Quietly(Func<int?> answer) => Caught(answer, (_, _) => { });

    private static int? Caught(Func<int?> answer, Action<string, string> heard)
    {
        var (wasOut, wasError) = (Console.Out, Console.Error);
        using var output = new StringWriter();
        using var error = new StringWriter();

        Console.SetOut(output);
        Console.SetError(error);

        try
        {
            var code = answer();
            heard(output.ToString(), error.ToString());

            return code;
        }
        finally
        {
            Console.SetOut(wasOut);
            Console.SetError(wasError);
        }
    }
}
