using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// Checks on the catalogue itself, before anything is checked with it.
///
/// ADR-10 names the way a test double quietly stops being worth anything: if it cannot
/// tell apart the cases you are making claims about, a test guarding those claims passes
/// while checking nothing. Its example is a double returning the same value for the
/// service name and the display name, which would let every identity test go green on a
/// tool that had confused the two.
///
/// So the catalogue is held to the same standard as the code: these say what it is able
/// to distinguish, and they go red when it stops being able to.
/// </summary>
public sealed class SpecimenTests
{
    [Fact]
    public void No_two_specimens_are_the_same_entry()
    {
        var names = Specimens.All.Select(entry => entry.ServiceName).ToArray();

        // Case-insensitively, because Windows treats service names that way, except for
        // the one pair that exists precisely to differ only in case.
        var distinct = names.Distinct(StringComparer.OrdinalIgnoreCase).Count();

        Assert.Equal(names.Length - 1, distinct);
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_catalogue_tells_a_service_name_apart_from_a_display_name()
    {
        // The trap ADR-10 names. Some real entries genuinely have both the same, so the
        // claim is not that they always differ - it is that enough of them differ for a
        // test that mixed the two up to fail.
        var differing = Specimens.All.Count(entry =>
            !string.Equals(entry.ServiceName, entry.DisplayName, StringComparison.Ordinal));

        Assert.True(
            differing >= Specimens.All.Count / 2,
            $"Only {differing} of {Specimens.All.Count} specimens have a display name that differs " +
            "from the service name, which is too few to catch the two being confused.");
    }

    [Fact]
    public void The_catalogue_carries_all_four_states_a_field_can_be_in()
    {
        // The convention that everything else rests on. A catalogue missing one of these
        // would let the difference between "there is nothing" and "I was not allowed to
        // look" disappear without a single test noticing.
        Assert.Contains(Specimens.All, entry => entry.Account.Outcome == ReadOutcome.Present);
        Assert.Contains(Specimens.All, entry => entry.Account.Outcome == ReadOutcome.Absent);
        Assert.Contains(Specimens.All, entry => entry.Account.Outcome == ReadOutcome.Denied);
        Assert.Contains(Specimens.All, entry => entry.DelayedAuto.Outcome == ReadOutcome.NotRead
            || entry.DelayedAuto.Outcome == ReadOutcome.Absent);
    }

    [Fact]
    public void The_catalogue_carries_an_entry_read_in_part_and_refused_in_part()
    {
        // The state that cannot exist if the delay is a sixth start type. If this ever
        // becomes unrepresentable, the model has quietly gone back to the shape that
        // answers "is it delayed" with a confident guess.
        Assert.Contains(Specimens.All, entry =>
            entry.StartType.Outcome == ReadOutcome.Present
            && entry.DelayedAuto.Outcome == ReadOutcome.Denied);
    }

    [Theory]
    [InlineData(EntryType.KernelDriver)]
    [InlineData(EntryType.FileSystemDriver)]
    [InlineData(EntryType.OwnProcess)]
    [InlineData(EntryType.SharedProcess)]
    public void Every_kind_of_entry_appears(EntryType type)
    {
        Assert.Contains(Specimens.All, entry => entry.EntryType == type);
    }

    [Theory]
    [InlineData(StartType.Automatic)]
    [InlineData(StartType.Manual)]
    [InlineData(StartType.Disabled)]
    public void Every_start_type_the_catalogue_claims_to_cover_appears(StartType startType)
    {
        // Boot and System are deliberately absent: no specimen was captured for them, and
        // writing one would be guesswork dressed as a measurement. They arrive when a
        // driver carrying one is read off a machine.
        Assert.Contains(Specimens.All, entry =>
            entry.StartType.IsPresent && entry.StartType.Value == startType);
    }

    [Fact]
    public void Delayed_automatic_appears_in_all_three_forms_that_matter()
    {
        Assert.Contains(Specimens.All, entry => entry.DelayedAuto is { IsPresent: true, Value: true });
        Assert.Contains(Specimens.All, entry => entry.DelayedAuto is { IsPresent: true, Value: false });
        Assert.Contains(Specimens.All, entry => entry.DelayedAuto.Outcome == ReadOutcome.Denied);
    }

    [Fact]
    public void The_catalogue_carries_a_transitional_state()
    {
        // Everyday life on a real machine and never there when a test wants it, which is
        // the whole reason a double earns its keep.
        Assert.Contains(Specimens.All, entry => entry.Status == EntryStatus.StartPending);
    }

    [Fact]
    public void The_catalogue_carries_a_display_name_that_is_not_in_english()
    {
        // 05-PRZYPADKI-BRZEGOWE lists "system in another language" as a case with no
        // evidence behind it, and the whole identity rule of ADR-14 exists for it. These
        // display names were read off a Polish install, so the evidence is now in the
        // tests rather than only in the reasoning.
        Assert.Contains(Specimens.All, entry => entry.DisplayName.Any(character => !char.IsAscii(character)));
    }

    [Fact]
    public void The_catalogue_carries_a_dependency_on_a_load_order_group()
    {
        // The one case that cannot be treated as a service name. Without a specimen, a
        // cascade that resolves "+NetBIOSGroup" as a service would look right in every
        // test and be wrong on the one machine that has one.
        var withGroup = Specimens.All.Single(entry =>
            entry.DependsOn.IsPresent && entry.DependsOn.Value!.Any(ScmEntry.IsGroup));

        Assert.Equal("RemoteAccess", withGroup.ServiceName);
        Assert.Contains("+NetBIOSGroup", withGroup.DependsOn.Value!);

        // And the ordinary services in the same list are not mistaken for groups.
        Assert.False(ScmEntry.IsGroup("RpcSS"));
        Assert.True(ScmEntry.IsGroup("+NetBIOSGroup"));
    }

    [Fact]
    public void The_catalogue_carries_an_entry_declaring_nothing_and_one_declaring_several()
    {
        // Declaring nothing is the ordinary case for well over a third of a real listing,
        // so it has to be absent rather than an empty list somebody has to remember to check.
        Assert.Contains(Specimens.All, entry => entry.DependsOn.Outcome == ReadOutcome.Absent);
        Assert.Contains(Specimens.All, entry => entry.DependsOn.IsPresent && entry.DependsOn.Value!.Count > 1);
    }

    [Fact]
    public void The_double_hands_back_what_it_was_given_and_reads_once()
    {
        var catalog = Specimens.Catalog();

        var entries = catalog.ReadAll();

        Assert.Equal(Specimens.All.Count, entries.Count);
        Assert.Equal(1, catalog.Reads);
    }
}
