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
public sealed class UsageContractTests
{
    /// <summary>
    /// The whole switch surface, spelled out rather than derived.
    ///
    /// A list here can go stale, and that is the point: adding a switch to the tool without
    /// adding it here fails, and adding it here without documenting it fails too. Deriving
    /// the list from the code would make the guard agree with whatever the code does, which
    /// is not a guard.
    /// </summary>
    private static readonly string[] EverySwitch =
    [
        "--query", "--signatures", "--memory", "--json", "--timing",
        "--dry-run", "--dependents", "--timeout", "--note"
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
        ["list", "stop", "start", "restart", "snapshot create"];

    [Fact]
    public void The_usage_text_mentions_every_switch_the_tool_accepts()
    {
        // No verb at all prints the usage and ends with the code for a mistaken command.
        var usage = CommandLineTool.Run();

        Assert.Equal(2, usage.ExitCode);
        Assert.Equal(string.Empty, usage.StandardOutput);

        var missing = EverySwitch
            .Where(option => !usage.StandardError.Contains(option, StringComparison.Ordinal))
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
        var usage = CommandLineTool.Run().StandardError;

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
        var usage = CommandLineTool.Run().StandardError;

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
}
