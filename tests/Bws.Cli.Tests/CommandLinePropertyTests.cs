using Bws.Core.Planning;
using CsCheck;

namespace Bws.Cli.Tests;

/// <summary>
/// Anything typed after <c>bws</c> ends before the service manager is opened in one of two ways: an
/// answer that says something, or a command whole enough to run - never an exception, and never a
/// mistake let through in silence.
///
/// <b>The same three steps Program.cs takes, in the same order, and nothing after them.</b> The
/// reader, the answers that need no machine (version, help, the licence), and the refusals. What a
/// run does next needs a machine and is not asked here. The claim about what passes is the one
/// Program.cs makes in a comment and nothing held until now: a start type is never missing where
/// the verb needs one, because a refusal has already turned that back.
///
/// <b>Why help may win over a mistake.</b> Help and version are read before the line is judged, on
/// purpose (Immediate.cs) - so the property does not ask that every mistake is refused, only that
/// every ending says something: an exit code from the table, and a line on the channel it belongs on.
/// </summary>
[Collection(nameof(SharedConsole))]
public sealed class CommandLinePropertyTests
{
    [Fact]
    [Trait("search", "weekly")]
    public void Any_words_end_in_an_answer_that_says_something_or_a_command_whole_enough_to_run()
    {
        Lines.Searched(Ends, new(5_000) { Threads = 1, Print = words => $"[{string.Join(", ", words.Select(word => $"<{word}>"))}]" });
    }

    /// <summary>
    /// The verbs below are a list in this file, because the command line reads them in a switch that
    /// nothing can enumerate. This keeps the copy honest the only way it can be: a verb added to the
    /// tool and not here would leave a kind of command nobody draws, and that kind turns this red.
    /// </summary>
    [Fact]
    public void The_words_drawn_above_reach_every_kind_of_command()
    {
        // "snapshot" is a noun and reaches nothing alone - the two words after it are what it reaches.
        var reached = Verbs
            .Select(verb => CommandLine.Read([verb]).Kind)
            .Concat(new[] { "create", "diff" }.Select(word => CommandLine.Read(["snapshot", word]).Kind))
            .ToHashSet();

        var missing = Enum.GetValues<CommandKind>().Where(kind => kind != CommandKind.None && !reached.Contains(kind)).ToArray();

        Assert.True(missing.Length == 0, $"No word drawn by the property reaches {string.Join(", ", missing)}.");
    }

    private static bool Ends(string[] words) =>
        WithTheConsoleCaught((output, error) =>
        {
            var read = CommandLine.Read(words);

            if (Immediate.Answer(read) is { } answered)
            {
                return answered == ExitCode.Ok && !string.IsNullOrWhiteSpace(output.ToString());
            }

            return Refusals.Answer(read) is { } refused
                ? refused == ExitCode.Usage && !string.IsNullOrWhiteSpace(error.ToString())
                : WholeEnoughToRun(read);
        });

    /// <summary>What Program.cs goes on to rely on once nothing has answered or refused.</summary>
    private static bool WholeEnoughToRun(CommandLine read) =>
        read.NothingWrong
        && read.Kind != CommandKind.None
        && (!read.IsWrite || read.ServiceName.Length > 0)
        && (!WriteCommands.NeedsAStartType(read.Kind) || WriteCommands.Named(read.Setting.Word) is not null);

    private static bool WithTheConsoleCaught(Func<StringWriter, StringWriter, bool> check)
    {
        var (wasOut, wasError) = (Console.Out, Console.Error);
        using var output = new StringWriter();
        using var error = new StringWriter();

        Console.SetOut(output);
        Console.SetError(error);

        try
        {
            return check(output, error);
        }
        finally
        {
            Console.SetOut(wasOut);
            Console.SetError(wasError);
        }
    }

    private static readonly string[] Verbs =
        ["list", "show", "stop", "start", "restart", "start-type", "kill", "history", "license", "snapshot"];

    private static readonly Gen<string> Cased = Gen.OneOfConst(Verbs).SelectMany(verb =>
        Gen.OneOfConst(verb, verb.ToUpperInvariant(), char.ToUpperInvariant(verb[0]) + verb[1..]));

    private static readonly Gen<string> Value = Gen.Frequency(
        (3, Gen.OneOfConst([.. Enum.GetValues<StartSetting>().Select(setting => StartTypeWords.Of(setting) ?? "none")])),
        (3, Gen.OneOfConst("5", "0", "-1", "600", "601", "99999999999", "1.5", "abc", "", " ", "\t")),
        (2, Gen.OneOfConst("status:running", "!!!", "name:\"x", "\"\"", "name:spool*", "pid:>4")),
        (2, Gen.OneOfConst("Spooler", "AMD Crash Defender Service", "MSSQL$SQLEXPRESS", "-leading", "a.json", "create", "diff", "restore")));

    private static readonly Gen<string> Switch = Gen.Frequency(
        (8, Gen.OneOfConst([.. OptionSurface.Surface.Select(entry => entry.Option)])),
        (2, Gen.OneOfConst("--help", "-h", "--version", "--", "-", "--=", "-json", (char)0x2014 + "json", "--JSON")),
        (2, Gen.OneOfConst("--query=", "--note=", "--timeout=").SelectMany(option => Value.Select(value => option + value))),
        (1, Gen.OneOfConst([.. OptionSurface.Surface.Select(entry => entry.Option)]).Select(option => option[..^1])));

    private static readonly Gen<string> Word = Gen.Frequency(
        (4, Cased),
        (5, Switch),
        (5, Value),
        (1, Gen.String[Gen.Char[' ', '~'], 0, 12]));

    private static readonly Gen<string[]> Lines = Word.Array[0, 8];
}

/// <summary>
/// Tests that swap the console for a writer of their own, run with nothing beside them. The console
/// belongs to the whole process, so a test writing to it while another one listens would put its
/// words into somebody else's assertion.
/// </summary>
[CollectionDefinition(nameof(SharedConsole), DisableParallelization = true)]
public sealed class SharedConsole;
