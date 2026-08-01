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
        ["--query", "--signatures", "--json", "--timing", "--dry-run", "--dependents", "--timeout"];

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
}
