using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Cli.Tests;

/// <summary>
/// The recovery verb as somebody types it - the spelling frozen in `docs/02` on 2026-11-24 (backlog 555 part
/// a2). Read by the real reader, so a word the documents promise and the reader refuses goes red here.
/// </summary>
public sealed class RecoveryCommandTests
{
    [Fact]
    public void The_example_in_the_documents_reads_as_every_part_it_names()
    {
        var read = CommandLine.Read(
            ["recovery", "Spooler", "restart:2m", "restart:5m", "nothing", "--reset", "1d",
                "--program", "C:\\fix.cmd \"%1%\"", "--message", "", "--on-error-stops", "yes"]);

        var change = RecoveryAsks.Change(read.Recovery);

        Assert.True(read.NothingWrong);
        Assert.NotNull(change);
        Assert.Equal(
            RecoveryParts.Actions | RecoveryParts.ResetPeriod | RecoveryParts.Command
                | RecoveryParts.RebootMessage | RecoveryParts.OnNonCrashFailures,
            change!.Parts);
        Assert.Equal(
            [
                new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromMinutes(2)),
                new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromMinutes(5)),
                new RecoveryItem(RecoveryAction.Nothing, TimeSpan.Zero)
            ],
            change.To.List.Items);
        Assert.Equal(TimeSpan.FromDays(1), change.To.List.ResetPeriod);
        Assert.Equal("C:\\fix.cmd \"%1%\"", change.To.List.Command);

        // An empty message is the removal the switch documents - the part is named and its value is none.
        Assert.Null(change.To.List.RebootMessage);
        Assert.True(change.To.OnNonCrashFailures);
    }

    [Fact]
    public void A_part_left_out_is_not_named_and_the_period_can_be_never()
    {
        var change = RecoveryAsks.Change(CommandLine.Read(["recovery", "Spooler", "--reset", "never"]).Recovery);

        Assert.Equal(RecoveryParts.ResetPeriod, change!.Parts);
        Assert.Equal(Timeout.InfiniteTimeSpan, change.To.List.ResetPeriod);
    }

    [Theory]
    [InlineData("cli.recovery.nothing", "recovery Spooler")]
    [InlineData("cli.recovery.badDelay", "recovery Spooler restart")]
    [InlineData("cli.recovery.badDelay", "recovery Spooler reboot:5x")]
    [InlineData("cli.recovery.badDelay", "recovery Spooler restart:4294967296ms")]
    [InlineData("cli.recovery.badItem", "recovery Spooler restrat:1m")]
    [InlineData("cli.recovery.noneAlone", "recovery Spooler none restart:1m")]
    [InlineData("cli.recovery.noneWithReset", "recovery Spooler none --reset 1d")]
    [InlineData("cli.recovery.badReset", "recovery Spooler --reset 1.5d")]
    [InlineData("cli.recovery.badYesNo", "recovery Spooler --on-error-stops true")]
    public void A_recovery_that_does_not_read_is_refused_with_its_own_sentence(string key, string line)
    {
        var words = line.Split(' ');
        var read = CommandLine.Read(words);
        var mistake = RecoveryAsks.Mistake(read.Recovery, read.ServiceName);

        Assert.Null(RecoveryAsks.Change(read.Recovery));
        Assert.NotNull(mistake);
        Assert.Equal(Texts.Of(key, ShownWord(words)), mistake);
    }

    [Fact]
    public void A_switch_given_twice_is_refused_rather_than_the_last_one_winning()
    {
        var read = CommandLine.Read(["recovery", "Spooler", "--program", "a.cmd", "--program", "b.cmd"]);

        Assert.Contains("--program", read.Repeated);
    }

    [Fact]
    public void A_recovery_switch_on_another_verb_is_misplaced_rather_than_ignored()
    {
        var read = CommandLine.Read(["stop", "Spooler", "--reset", "1d"]);

        Assert.Contains("--reset", read.Misplaced);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1500ms", 1500)]
    [InlineData("90s", 90_000)]
    [InlineData("2m", 120_000)]
    [InlineData("4294967295ms", 4294967295)]
    public void A_delay_reads_in_its_unit_and_writes_back_in_the_largest_that_divides_it(string typed, long milliseconds)
    {
        var delay = RecoveryCommandWords.ReadDelay(typed);

        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), delay);
        Assert.Equal(delay, RecoveryCommandWords.ReadDelay(RecoveryCommandWords.DelayWord(delay!.Value)));
    }

    /// <summary>The word a refusal repeats back: the item or value at fault, or the name typed for the others.</summary>
    private static string ShownWord(string[] words) =>
        words.Length > 2 && !words[2].StartsWith('-') && words[2] != "none" ? words[2]
        : words.SkipWhile(word => word is not ("--reset" or "--on-error-stops")).Skip(1).FirstOrDefault()
            ?? EquivalentCommand.Typed(words[1]);
}
