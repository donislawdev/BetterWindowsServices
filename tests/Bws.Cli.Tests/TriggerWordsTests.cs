using Bws.Core;

namespace Bws.Cli.Tests;

/// <summary>
/// Every trigger kind the core names has a word of its own in the terminal - its own word, not one it
/// shares with another kind and not the word for a kind nobody can name.
///
/// <b>Written 2026-10-28 for backlog 627</b>, when the core named a tenth kind, Aggregate. The words are a
/// switch with a fallback, so a kind added to the core and forgotten here prints "Unknown" - the very word
/// the new name was added to replace - and nothing else would say so. The first version asked only that,
/// and the review of PR #99 caught what it let through: Aggregate pointed at the word for Custom would have
/// passed. Now the table below pins each word, and the guard after it asks that no two kinds share one, so
/// a kind added later and missing from the table is still held. CellFaceTests asks the same of the window.
/// </summary>
public sealed class TriggerWordsTests
{
    [Theory]
    [InlineData(TriggerKind.DeviceArrival, "cli.cell.trigger.deviceArrival")]
    [InlineData(TriggerKind.IpAddress, "cli.cell.trigger.ipAddress")]
    [InlineData(TriggerKind.DomainJoin, "cli.cell.trigger.domainJoin")]
    [InlineData(TriggerKind.FirewallPort, "cli.cell.trigger.firewallPort")]
    [InlineData(TriggerKind.GroupPolicy, "cli.cell.trigger.groupPolicy")]
    [InlineData(TriggerKind.NetworkEndpoint, "cli.cell.trigger.networkEndpoint")]
    [InlineData(TriggerKind.Custom, "cli.cell.trigger.custom")]
    [InlineData(TriggerKind.CustomSystemStateChange, "cli.cell.trigger.systemState")]
    [InlineData(TriggerKind.Aggregate, "cli.cell.trigger.aggregate")]
    [InlineData(TriggerKind.Unknown, "cli.cell.unknown")]
    public void Each_kind_reads_as_its_word(TriggerKind kind, string key)
    {
        Assert.Equal(Texts.Of(key), TriggerWords.Of(kind));
    }

    [Fact]
    public void Every_kind_the_core_names_has_a_word_of_its_own()
    {
        var named = Enum.GetValues<TriggerKind>().Where(kind => kind != TriggerKind.Unknown).ToList();
        var words = named.Select(TriggerWords.Of).ToList();

        Assert.DoesNotContain(Texts.Of("cli.cell.unknown"), words);
        Assert.Equal(named.Count, words.Distinct(StringComparer.Ordinal).Count());
    }
}
