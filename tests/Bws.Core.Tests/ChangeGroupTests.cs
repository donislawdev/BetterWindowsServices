using Bws.Core.History;

namespace Bws.Core.Tests;

/// <summary>
/// Changes identical in everything but their time, gathered into one line - owner's decision J2-b,
/// 2026-10-20. The shapes are the owner's machine that day: BITS switched both ways by SYSTEM dozens
/// of times, and one driver installed again by a person on every run of a Python tool.
/// </summary>
public sealed class ChangeGroupTests
{
    private const string SystemSid = "S-1-5-18";
    private const string PersonSid = "S-1-5-21-1111111111-2222222222-3333333333-1001";

    [Fact]
    public void Identical_changes_become_one_group_counting_them_between_the_first_and_the_last()
    {
        // Newest first, the order a history is in.
        var groups = ChangeGroup.Of(
        [
            Switched("BITS", Day(20)),
            Switched("BITS", Day(15)),
            Switched("BITS", Day(11))
        ]);

        var group = Assert.Single(groups);

        Assert.Equal(3, group.Times);
        Assert.Equal(Day(11), group.First);
        Assert.Equal(Day(20), group.Last);
        Assert.Equal(Day(20), group.Latest.At);
    }

    [Fact]
    public void A_switch_one_way_and_back_is_two_groups_not_one()
    {
        // The owner's BITS: 59 one way and 59 back. One line for both would hide which way it went.
        var groups = ChangeGroup.Of(
        [
            Switched("BITS", Day(20), back: true),
            Switched("BITS", Day(19)),
            Switched("BITS", Day(18), back: true)
        ]);

        Assert.Equal(2, groups.Count);
        Assert.Equal([2, 1], groups.Select(group => group.Times));
        Assert.Equal("auto start", groups[0].Latest.Words.StartTypeAfter);
    }

    [Fact]
    public void Groups_come_in_the_order_of_their_newest_member()
    {
        var groups = ChangeGroup.Of(
        [
            Switched("Spooler", Day(20)),
            Switched("BITS", Day(19)),
            Switched("Spooler", Day(12))
        ]);

        Assert.Equal(["Spooler", "BITS"], groups.Select(group => group.Latest.ServiceName));
    }

    [Fact]
    public void The_internal_name_compares_the_way_the_manager_compares_it()
    {
        var groups = ChangeGroup.Of(
        [
            Switched("Spooler", Day(20)),
            Switched("Spooler", Day(19)) with { ServiceName = "SPOOLER" }
        ]);

        Assert.Equal(2, Assert.Single(groups).Times);
    }

    [Fact]
    public void The_account_is_its_sid_and_its_name_does_not_split_a_group()
    {
        // Rule 3: one account named two ways - a name translated, or not translated at all - is one
        // account. Two accounts are two, whatever they are called.
        var named = Switched("BITS", Day(20)) with { By = new Actor(SystemSid, "NT AUTHORITY\\SYSTEM") };
        var unnamed = named with { At = Day(19), By = new Actor(SystemSid, null) };
        var other = named with { At = Day(18), By = new Actor(PersonSid, "NT AUTHORITY\\SYSTEM") };

        var groups = ChangeGroup.Of([named, unnamed, other]);

        // Which two share the line, not only how many - grouped by name instead of SID, the counts
        // would come out the same with the wrong pair in them.
        Assert.Equal([2, 1], groups.Select(group => group.Times));
        Assert.Equal(PersonSid, groups[1].Latest.By?.Sid);
    }

    [Fact]
    public void A_driver_installed_from_two_folders_is_two_groups()
    {
        // WinDivert on the owner's machine: 46, 6 and 4 - the same name from three different paths.
        var groups = ChangeGroup.Of(
        [
            Installed("WinDivert", @"C:\one\WinDivert64.sys", Day(20)),
            Installed("WinDivert", @"C:\two\WinDivert64.sys", Day(19)),
            Installed("WinDivert", @"C:\one\WinDivert64.sys", Day(18))
        ]);

        Assert.Equal([2, 1], groups.Select(group => group.Times));
    }

    [Fact]
    public void Changes_without_a_time_are_counted_and_leave_the_times_to_the_others()
    {
        var timeless = Switched("BITS", null);

        var groups = ChangeGroup.Of([timeless, Switched("BITS", Day(15)), timeless with { }]);

        var group = Assert.Single(groups);

        Assert.Equal(3, group.Times);
        Assert.Equal(Day(15), group.First);
        Assert.Equal(Day(15), group.Last);
    }

    [Fact]
    public void A_group_of_changes_that_carry_no_time_has_no_times()
    {
        var group = Assert.Single(ChangeGroup.Of([Switched("BITS", null), Switched("BITS", null)]));

        Assert.Null(group.First);
        Assert.Null(group.Last);
    }

    [Fact]
    public void Each_keeps_every_change_on_its_own_in_the_order_handed_in()
    {
        var changes = new[]
        {
            Switched("BITS", Day(20)),
            Switched("BITS", Day(19))
        };

        var each = ChangeGroup.Each(changes);

        Assert.Equal(2, each.Count);
        Assert.All(each, group => Assert.Equal(1, group.Times));
        Assert.Equal([Day(20), Day(19)], each.Select(group => group.Last));
        Assert.Same(changes[1], each[1].Latest);
    }

    [Fact]
    public void An_empty_history_has_no_groups()
    {
        Assert.Empty(ChangeGroup.Of([]));
        Assert.Empty(ChangeGroup.Each([]));
    }

    private static DateTimeOffset Day(int day) => new(2026, 10, day, 10, 18, 0, TimeSpan.Zero);

    /// <summary>A start type switched by SYSTEM - automatic to on demand, or the way back.</summary>
    private static ServiceChange Switched(string name, DateTimeOffset? at, bool back = false) => new()
    {
        At = at,
        Kind = ChangeKind.StartTypeChanged,
        ServiceName = name,
        DisplayName = name,
        By = new Actor(SystemSid, "NT AUTHORITY\\SYSTEM"),
        Words = back
            ? new WindowsWords("demand start", "auto start", null, null)
            : new WindowsWords("auto start", "demand start", null, null)
    };

    private static ServiceChange Installed(string display, string path, DateTimeOffset at) => new()
    {
        At = at,
        Kind = ChangeKind.Installed,
        DisplayName = display,
        By = new Actor(PersonSid, "PC\\someone"),
        BinaryPath = path,
        Words = new WindowsWords(null, null, "kernel mode driver", "demand start")
    };
}
