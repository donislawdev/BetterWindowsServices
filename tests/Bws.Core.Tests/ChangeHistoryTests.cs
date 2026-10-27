using System.Security.Principal;
using Bws.Core.History;

namespace Bws.Core.Tests;

/// <summary>
/// The rules the change history adds on top of the events: how far back a log reaches, which
/// changes belong to one entry, and which accounts get a name.
/// </summary>
public sealed class ChangeHistoryTests
{
    private const ulong Limit = 20 * 1024 * 1024;
    private static readonly SecurityIdentifier LocalDomain = new("S-1-5-21-1111111111-2222222222-3333333333");

    [Fact]
    public void A_log_with_room_left_is_not_overwriting()
    {
        // The owner's System log, 2026-10-15: 1 118 208 bytes of a 20 MB limit.
        var log = ChangeHistory.Cover("System", new LogFacts(1_118_208, Limit, Full: false, LogMode.Circular), At(), cleared: null);

        Assert.False(log.Overwritten);
        Assert.False(log.Full);
        Assert.Equal(LogMode.Circular, log.Mode);
    }

    [Fact]
    public void A_log_at_its_limit_is_overwriting()
    {
        // The owner's Security log the same day: a 4096 byte header plus the whole limit, overwriting.
        var log = ChangeHistory.Cover("System", new LogFacts(Limit + 4096, Limit, Full: false, LogMode.Circular), At(), cleared: null);

        Assert.True(log.Overwritten);
    }

    [Fact]
    public void An_archived_log_at_its_limit_is_not_called_overwritten()
    {
        // H02: Windows puts the full file aside and begins a new one, and documents EvtLogFull as always
        // false for such a log - which until 2026-10-27 made "not full and at the limit" read as overwriting.
        var log = ChangeHistory.Cover("System", new LogFacts(Limit + 4096, Limit, Full: false, LogMode.AutoBackup), At(), cleared: null);

        Assert.False(log.Overwritten);
        Assert.Equal(LogMode.AutoBackup, log.Mode);
    }

    [Fact]
    public void A_log_with_less_than_one_chunk_of_room_is_already_at_its_limit()
    {
        // The file grows a chunk at a time, so the last chunk that does not fit is the limit.
        Assert.True(new LogFacts(Limit - LogFacts.Chunk + 1, Limit, Full: false, LogMode.Circular).AtSizeLimit);
        Assert.False(new LogFacts(Limit - LogFacts.Chunk, Limit, Full: false, LogMode.Circular).AtSizeLimit);
    }

    [Fact]
    public void A_full_log_is_not_called_overwritten()
    {
        // Full means Windows keeps what it has and drops the new - the opposite end of the history is
        // missing, and saying "anything older is gone" too would be untrue.
        var log = ChangeHistory.Cover("System", new LogFacts(Limit + 4096, Limit, Full: true, LogMode.Retain), At(), cleared: null);

        Assert.True(log.Full);
        Assert.False(log.Overwritten);
    }

    [Fact]
    public void One_entry_gets_its_changes_by_identity_whatever_the_case()
    {
        var history = History(
            Changed("Spooler"), Changed("BITS"), Changed("spooler"), Installed("Spooler"), Changed(null));

        var spooler = history.Of("SPOOLER");

        Assert.Equal(2, spooler.Count);
        Assert.All(spooler, change => Assert.Equal(ChangeKind.StartTypeChanged, change.Kind));
    }

    [Fact]
    public void An_installation_is_never_given_to_an_entry_even_under_its_own_display_name()
    {
        // J1-b: the display name is all an installation has, and matching on it is the guess rule 3
        // forbids - so not even an exact match counts.
        var history = History(Installed("Spooler"));

        Assert.Empty(history.Of("Spooler"));
        Assert.True(history.HasInstallations);
    }

    [Fact]
    public void Start_type_changes_without_a_name_are_counted_for_the_view_that_cannot_list_them()
    {
        var history = History(Changed(null), Changed(null), Changed("BITS"), Installed("x"));

        Assert.Equal(2, history.Unnamed);
    }

    [Theory]
    [InlineData("S-1-5-18", true)]
    [InlineData("S-1-5-19", true)]
    [InlineData("S-1-5-32-544", true)]
    [InlineData("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464", true)]
    [InlineData("S-1-5-21-1111111111-2222222222-3333333333-1001", true)]
    [InlineData("S-1-5-21-4444444444-5555555555-6666666666-1001", false)]
    [InlineData("S-1-12-1-1111111111-2222222222-3333333333-4444444444", false)]
    [InlineData("S-1-1-0", false)]
    public void Only_accounts_this_machine_can_name_on_its_own_are_named(string sid, bool named)
    {
        // J1-c: an account of any other domain would make Windows ask a domain controller.
        Assert.Equal(named, AccountNames.NamedWithoutTheNetwork(new SecurityIdentifier(sid), LocalDomain));
    }

    [Fact]
    public void Without_the_local_domain_no_account_of_any_domain_is_named()
    {
        Assert.False(AccountNames.NamedWithoutTheNetwork(
            new SecurityIdentifier("S-1-5-21-1111111111-2222222222-3333333333-1001"), localDomain: null));
        Assert.True(AccountNames.NamedWithoutTheNetwork(new SecurityIdentifier("S-1-5-18"), localDomain: null));
    }

    [Fact]
    public void A_sid_that_is_not_shaped_like_one_gets_no_name_and_throws_nothing()
    {
        var names = AccountNames.Of(["not a sid", null, "S-1-5-21-4444444444-5555555555-6666666666-1001"], LocalDomain);

        Assert.Empty(names);
    }

    [Fact]
    public void System_is_named_by_this_machine()
    {
        // The name is in the language of the system ("NT AUTHORITY\SYSTEM", or its translation), so
        // only that there IS one is asked here.
        var names = AccountNames.Of(["S-1-5-18"], LocalDomain);

        Assert.False(string.IsNullOrWhiteSpace(names["S-1-5-18"]));
    }

    private static DateTimeOffset At() => new(2026, 10, 11, 14, 58, 5, TimeSpan.Zero);

    private static ChangeHistory History(params ServiceChange[] changes) =>
        new(ChangeHistory.Cover("System", new LogFacts(0, Limit, Full: false, LogMode.Circular), At(), cleared: null), changes);

    private static ServiceChange Changed(string? name) =>
        new() { At = At(), Kind = ChangeKind.StartTypeChanged, ServiceName = name, DisplayName = name };

    private static ServiceChange Installed(string display) =>
        new() { At = At(), Kind = ChangeKind.Installed, DisplayName = display };
}
