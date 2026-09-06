namespace Bws.Integration.Tests;

/// <summary>
/// Every switch the tool accepts is findable in the help it prints.
///
/// Written after adding <c>--signatures</c> and forgetting it here, which nothing noticed.
/// A command line switch has exactly one way to be discovered - the usage text - so one
/// that is accepted and unmentioned may as well not exist for anybody who did not watch it
/// being built. The glossary makes the same point about keyboard shortcuts: a shortcut
/// nobody can find out about is a hole in discoverability rather than a feature.
///
/// It runs here rather than in the architecture guards because it asks the built tool what
/// it accepts and what it prints, instead of reading the source and inferring both.
/// </summary>
/// <remarks>
/// <b>Marked as running anywhere, which is what puts it in continuous integration.</b> Everything
/// in this class is about the command line surface - what the tool accepts, what it says when it
/// does not, and which exit code comes back. None of it depends on which services this machine
/// happens to have, so none of the reasons the rest of this project stays off a build agent apply.
///
/// The whole project used to be excluded, and the reason given covered only part of it: comparing
/// against this machine's sc.exe and holding budgets from section 8.1 are indeed facts about one
/// machine. Asking whether `bws --help` ends with code zero is not, and it went unchecked on every
/// push for the same reason - which is how that exact fault lived through the whole life of the
/// product once already.
/// </remarks>
[Trait("runs", "anywhere")]
public sealed class UsageContractTests
{
    /// <summary>
    /// The whole switch surface, spelled out rather than derived.
    ///
    /// A list here can go stale, and that is the point: adding it here without documenting it
    /// fails. Deriving the list from the code would make the guard agree with whatever the code
    /// does, which is not a guard.
    ///
    /// <b>THE OTHER HALF OF THAT SENTENCE WAS FALSE AND IS CORRECTED, 2026-09-06.</b> It read
    /// "adding a switch to the tool without adding it here fails", and nothing anywhere compared
    /// this list against what the tool accepts - so a switch added to the surface and never
    /// written down was invisible to every check in this file. It happened: --restart shipped in
    /// the parser and the switch table with the help saying nothing about it, and this file
    /// stayed green. <c>UsageSurfaceGuards</c> in the command line tests is what makes the
    /// sentence true, and it lives there because it needs to see OptionSurface.
    /// </summary>
    private static readonly string[] EverySwitch =
    [
        "--query", "--signatures", "--memory", "--json", "--timing",
        "--dry-run", "--dependents", "--timeout", "--note",

        // Added 2026-09-06 with the forcing verb, and they were added BY HAND after a guard in
        // Bws.Cli.Tests went red about them - which is the repair this list needed. The comment
        // above promises that adding a switch without adding it here fails, and until that day
        // nothing made the promise true: --restart lived in the tool, in the parser and in the
        // switch table for a whole package while this file said nothing at all.
        "--restart",

        // Added 2026-08-02, and their absence from this list is why nobody noticed that the
        // first of them did not work at all. This guard was green while `bws --help` answered
        // "Unknown option: --help" and ended with the code for a mistyped command - because it
        // only ever asked about switches somebody had remembered to write down here.
        //
        // Spelling out the surface rather than deriving it is deliberate, and it has a cost
        // this entry is the receipt for: a list that has to be remembered can be forgotten.
        // Deriving it would have made the guard agree with whatever the code does, which is
        // worse - it would have been green for the same reason and taught nobody anything.
        "--help", "-h", "--version"
    ];

    /// <summary>
    /// Every command, spelled the way somebody types it.
    ///
    /// Here because a two-word command is a place where the spelling can drift out of the
    /// enumeration value behind it, and it did: the message telling somebody where a switch
    /// works answered "snapshotcreate", which is not a thing anybody can type. Nothing
    /// noticed, because every test that exercised the command spelled it correctly itself.
    /// </summary>
    private static readonly string[] EveryCommand =
        ["list", "stop", "start", "restart", "kill", "snapshot create"];

    [Fact]
    public void The_usage_text_mentions_every_switch_the_tool_accepts()
    {
        // No verb at all prints the usage on the DATA channel and ends with zero, and that is a
        // deliberate change of contract from 2026-08-02. It used to be code 2 on the error
        // channel - the code this tool reserves for what somebody typed wrongly - so asking how
        // to use it was answered as a mistake. This assertion is the old contract's headstone:
        // it said Equal(2) and Equal(string.Empty, StandardOutput), and it went red on the
        // change, which is exactly what it was for.
        var usage = CommandLineTool.Run();

        Assert.Equal(0, usage.ExitCode);
        Assert.Equal(string.Empty, usage.StandardError);

        var missing = EverySwitch
            .Where(option => !usage.StandardOutput.Contains(option, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"Accepted but not mentioned in the usage text: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Every_switch_in_the_usage_text_is_actually_accepted_somewhere()
    {
        // The other direction, and the one that rots quietly: a switch that was removed or
        // renamed leaves a line in the help promising something the tool will refuse.
        var usage = CommandLineTool.Run().StandardOutput;

        foreach (var option in EverySwitch)
        {
            // Offered to a verb that does not take it, the answer names the verbs that do.
            // Offered to nobody at all, the answer is that nothing knows the word.
            var refusal = CommandLineTool.Run("list", option);

            Assert.DoesNotContain(
                "Unknown option",
                refusal.StandardError,
                StringComparison.Ordinal);
        }

        Assert.Contains("bws list", usage, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_command_names_itself_the_way_it_is_typed()
    {
        // Two directions, and the second is the one that caught a real mistake. Every
        // command has to appear in the help, and every command the tool talks about has to
        // be a command the tool accepts - a message that answers "use it with snapshotcreate"
        // sends somebody to type a word that does not exist.
        var usage = CommandLineTool.Run().StandardOutput;

        foreach (var command in EveryCommand)
        {
            Assert.Contains(command, usage, StringComparison.Ordinal);
        }

        // --note belongs to exactly one command, so the refusal names it - and that is the
        // message the wrong spelling was going out in.
        var refusal = CommandLineTool.Run("list", "--note", "anything").StandardError;

        Assert.Contains("snapshot create", refusal, StringComparison.Ordinal);
        Assert.DoesNotContain("snapshotcreate", refusal, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Asking_how_to_use_it_is_answered_on_the_data_channel_and_is_not_a_failure(string asked)
    {
        // The contract this fixes, in one sentence: `bws --help` used to answer "Unknown option:
        // --help", print the usage on the error channel, and end with code 2 - the code reserved
        // for what somebody typed wrongly. Asking for help was reported as a mistake, and it is
        // the first thing anybody types.
        var help = CommandLineTool.Run(asked);

        Assert.Equal(0, help.ExitCode);
        Assert.Equal(string.Empty, help.StandardError);
        Assert.Contains("bws list", help.StandardOutput, StringComparison.Ordinal);

        // Leading with examples rather than a list of flags, which is the other half of what
        // clig.dev asks for and the half a switch inventory cannot check.
        Assert.StartsWith("Examples:", help.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void The_tool_can_say_what_version_it_is()
    {
        // It could not until 2026-08-02, while the number sat in every binary. An administrator
        // wanting to know what was on their production server had to read file properties.
        var version = CommandLineTool.Run("--version");

        Assert.Equal(0, version.ExitCode);
        Assert.Equal(string.Empty, version.StandardError);

        // The number itself is not asserted - it is the owner's to set by rule 11, and a test
        // holding a copy would fail on the release it is meant to survive. What is asserted is
        // that something version-shaped comes back rather than the word this tool prints when
        // it cannot read one.
        Assert.Matches(@"^bws \d+\.\d+\.\d+", version.StandardOutput.Trim());
    }

    /// <summary>
    /// AND WHAT SHAPE OF FILE IT WRITES, WHICH IS A SECOND, INDEPENDENT NUMBER.
    ///
    /// <b>The reason is forensic rather than tidy.</b> A build can change without the snapshot
    /// format changing, and the format cannot change without a reader somewhere needing to know.
    /// When the same snapshot gives a different answer in six months, a log carrying only the
    /// program version cannot say which of the two moved - and this tool exists to compare files
    /// taken months apart.
    ///
    /// The number is not asserted for the same reason the program version is not: it belongs to
    /// whoever changes the schema. What is asserted is that it is named at all.
    /// </summary>
    [Fact]
    public void The_tool_also_says_what_shape_of_snapshot_it_writes()
    {
        var version = CommandLineTool.Run("--version");

        Assert.Equal(0, version.ExitCode);
        Assert.Matches(@"snapshot schema \d+", version.StandardOutput);
    }

    [Theory]
    [InlineData("lst", "list")]
    [InlineData("stpo", "stop")]
    [InlineData("snpashot", "snapshot")]
    public void A_mistyped_command_is_called_a_command_and_offered_the_nearest_one(string typed, string meant)
    {
        // Two faults in the old answer, "Unknown option: lst": the word is not an option, and
        // nothing was offered. Naming the wrong kind of thing is the same family of mistake as
        // a switch without its value reporting itself as unknown, which this tool made once
        // already and fixed once already.
        var wrong = CommandLineTool.Run(typed);

        Assert.Equal(2, wrong.ExitCode);
        Assert.DoesNotContain("Unknown option", wrong.StandardError, StringComparison.Ordinal);

        // The whole sentence rather than the word, and the mutation registry is why. Asserting
        // that the answer merely CONTAINS "list" passed with suggestions turned off entirely -
        // because the fallback answer lists every command, and "list" is one of them. A test
        // that is satisfied by the thing it exists to rule out.
        Assert.Contains($"Did you mean {meant}?", wrong.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void A_word_close_to_nothing_is_not_given_a_guess()
    {
        // A suggestion that is wrong is worse than none: it reads as though the tool understood.
        // So the far-away word gets the list instead, and the list comes from the code rather
        // than from a sentence somebody has to remember to update.
        var nonsense = CommandLineTool.Run("qwertyuiop");

        Assert.Equal(2, nonsense.ExitCode);
        Assert.DoesNotContain("Did you mean", nonsense.StandardError, StringComparison.Ordinal);
        Assert.Contains("list", nonsense.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void A_command_that_is_half_typed_is_told_what_is_missing()
    {
        // Not "unknown option". Snapshot is a command and it is not unknown - it is waiting
        // for its verb, and calling it an option sends somebody to check the spelling of a
        // word they spelled correctly. The same mistake this tool already made once with a
        // switch given without its value.
        var missing = CommandLineTool.Run("snapshot");

        Assert.Equal(2, missing.ExitCode);
        Assert.DoesNotContain("Unknown option", missing.StandardError, StringComparison.Ordinal);
        Assert.Contains("create", missing.StandardError, StringComparison.Ordinal);

        // And a word under it that does not exist gets the other answer, which says so
        // rather than pretending the whole command is unheard of.
        var wrong = CommandLineTool.Run("snapshot", "restore", "baseline.json");

        Assert.Equal(2, wrong.ExitCode);
        Assert.DoesNotContain("Unknown option", wrong.StandardError, StringComparison.Ordinal);
        Assert.Contains("restore", wrong.StandardError, StringComparison.Ordinal);
    }

    /// <summary>
    /// A word a command has no room for is not called an option, and the answer says what the
    /// command does take.
    ///
    /// <b>Backlog 233, and the sentence it replaced was wrong twice over.</b>
    /// <c>bws start type Spooler manual</c> answered "Unknown option: Spooler, manual" - neither
    /// word is an option and both are spelled perfectly, so the answer sent somebody hunting for a
    /// typo they had not made. The person is one hyphen from what they meant, and what helps is
    /// being told how many names the verb has room for.
    ///
    /// <b>The sentence is read from the product's own language file rather than written out
    /// here</b>, which is what <see cref="Sentences"/> exists for: a copy of a sentence drifts from
    /// the sentence, and then the guard is about the copy.
    /// </summary>
    [Fact]
    public void A_word_a_command_has_no_room_for_is_not_called_an_option()
    {
        var typed = CommandLineTool.Run("start", "type", "Spooler", "manual");

        Assert.Equal(2, typed.ExitCode);
        Assert.DoesNotContain("Unknown option", typed.StandardError, StringComparison.Ordinal);

        Assert.Contains(
            Sentences.Of("cli.wordsNotTaken", "start", Sentences.Of("cli.takes.oneName"), "Spooler, manual"),
            typed.StandardError,
            StringComparison.Ordinal);

        // AND THE COMMAND THAT TAKES NO NAME AT ALL. Without this the guard would pass against one
        // sentence repeated for every verb, which is the answer that helps nobody the moment the
        // shapes differ - and they do: list narrows with a query rather than naming an entry.
        var listed = CommandLineTool.Run("list", "Spooler");

        Assert.Equal(2, listed.ExitCode);
        Assert.DoesNotContain("Unknown option", listed.StandardError, StringComparison.Ordinal);

        Assert.Contains(
            Sentences.Of("cli.wordsNotTaken", "list", Sentences.Of("cli.takes.query"), "Spooler"),
            listed.StandardError,
            StringComparison.Ordinal);
    }
    [Fact]
    public void A_switch_is_never_swallowed_as_the_value_of_another_one()
    {
        // MEASURED before this held: `bws list --query --json` ended with code 0, an empty table
        // on the data channel, and no JSON anywhere - because --json had been taken as the text
        // to search for. A script asking for a machine readable listing got a human one and a
        // green light, and the switch it typed was silently gone.
        //
        // That is the same silence the belonging table exists to end, arriving from the other
        // side: there the option was refused for being in the wrong place, and here it was never
        // seen as an option at all.
        var swallowed = CommandLineTool.Run("list", "--query", "--json");

        Assert.Equal(2, swallowed.ExitCode);
        Assert.Contains("--query", swallowed.StandardError, StringComparison.Ordinal);

        // Nothing on the data channel, because a failed run must not drop a stray line into
        // whatever comes next in the pipeline.
        Assert.Equal(string.Empty, swallowed.StandardOutput.Trim());
    }

    [Theory]
    [InlineData("status:")]
    [InlineData("=")]
    [InlineData("\"\"")]
    [InlineData("!!!")]
    public void Text_that_constrains_nothing_is_refused_here_however_it_is_spelled(string query)
    {
        // FOUR SPELLINGS OF ONE FAULT, and the first three used to answer with every entry on
        // the machine and a code of success. Each was repaired by naming it - !!! first, then
        // the empty pair of quotes - and each time the next spelling turned up within the hour.
        //
        // The repair that closed the family stopped naming spellings. The parser is told whether
        // the text is finished, the window says it is not because it holds status: between two
        // keystrokes, and a terminal says it is because there are no keystrokes. Owner's
        // decision, 2026-08-03.
        //
        // Here rather than only in the core tests because the code is the half that matters to a
        // script, and a script is what this protects: a typo in a query used to return the whole
        // machine and a green light.
        var refused = CommandLineTool.Run("list", "--query", query);

        Assert.Equal(2, refused.ExitCode);
        Assert.Equal(string.Empty, refused.StandardOutput.Trim());
    }

    [Fact]
    public void The_same_text_still_answers_while_somebody_is_typing_it()
    {
        // The other side of the line, and it lives in the window rather than here - so what this
        // checks is that the strictness above did not reach the language itself. A finished
        // member with a value is still a query, and a bare word that happens to be short is not
        // suddenly a mistake.
        Assert.Equal(0, CommandLineTool.Run("list", "--query", "status:running").ExitCode);
        Assert.Equal(0, CommandLineTool.Run("list", "--query", "sta").ExitCode);
    }

    [Fact]
    public void An_option_given_twice_is_refused_rather_than_quietly_halved()
    {
        // The last one used to win in silence, so `--query a --query b` searched for b and said
        // nothing about a. Same fault as a switch swallowed as another one's value, from a third
        // direction: the tool accepted something somebody wrote and did nothing with it.
        var twice = CommandLineTool.Run("list", "--query", "status:running", "--query", "status:stopped");

        Assert.Equal(2, twice.ExitCode);
        Assert.Contains("--query", twice.StandardError, StringComparison.Ordinal);
        Assert.Equal(string.Empty, twice.StandardOutput.Trim());

        // A flag repeated means what it meant once, so it is left alone - only the three options
        // carrying a value can lose one of two.
        Assert.Equal(0, CommandLineTool.Run("list", "--json", "--json").ExitCode);
    }

    [Fact]
    public void A_value_that_merely_looks_like_a_switch_is_still_a_value()
    {
        // The other side of the line, and it decides the shape of the check above. Refusing
        // every value that opens with a hyphen would be simpler and would take away a note
        // somebody would plausibly write - so the question asked is whether the next word is one
        // of THIS TOOL'S options, not whether it starts with a dash.
        var dashed = CommandLineTool.Run("list", "--query", "-notanoption");

        Assert.Equal(0, dashed.ExitCode);
        Assert.DoesNotContain("--query", dashed.StandardError, StringComparison.Ordinal);
    }
}
