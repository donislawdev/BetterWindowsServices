using Bws.Core.Querying;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The clickable filters of `A5`, asked through the view model rather than through the chip.
///
/// <b>The promise being checked is the round trip, not the filtering.</b> `A5` exists to teach
/// the query language by using it: click a chip and the member it stands for appears in a box
/// you can read and edit, type that member by hand and the chip lights. Either half alone is a
/// control that lies half the time.
///
/// The narrowing itself is not retested here - it is the same <see cref="Query"/> the command
/// line uses and it has its own tests. What is new is the composing.
/// </summary>
public sealed class FilterChipTests
{
    /// <summary>
    /// EVERY CHIP STANDS FOR A MEMBER THE LANGUAGE ACTUALLY KNOWS.
    ///
    /// <b>The guard that makes the catalogue safe to add to.</b> A chip carrying a typo -
    /// <c>start:delayd</c>, or a field renamed in the language and not here - composes a query
    /// that selects nothing and says nothing about why. That is the silence rule 8 forbids,
    /// arriving through a control rather than through a reading, and it would look exactly like
    /// "no services match", which is a sentence this window says legitimately all day.
    ///
    /// Asked of the parser rather than of a list kept here, so adding a chip for a value the
    /// language gains later needs no second edit.
    /// </summary>
    /// <summary>
    /// EVERY CHIP IN A GROUP ASKS ABOUT THE SAME FIELD, which is what makes the grouping true
    /// rather than tidy.
    ///
    /// <b>The row says something about the language and this is what keeps it honest.</b> Members
    /// of one field are ORed by the parser and members of different fields are ANDed - measured on
    /// the real window before the row was rebuilt: <c>status:running</c> 323, <c>status:stopped</c>
    /// 485, both together 808, while <c>status:running start:automatic</c> gives 90. So a group is
    /// a promise that its chips ADD UP, and one chip from another field quietly turns that group
    /// into a narrowing - with three labels on screen still claiming otherwise.
    ///
    /// It is a property of the CATALOGUE rather than of any one chip, which is why it cannot be
    /// checked by the test below however carefully that one is written.
    /// </summary>
    /// <summary>
    /// A group that adds up and a group that narrows do not say the same sentence.
    ///
    /// <b>The claim on screen has to differ where the behaviour differs, or the labels are
    /// decoration.</b> Two chips in the state group show both; two chips in the last group narrow
    /// each other, because they are different fields. A single hint over both would be true for one
    /// of them and false for the other - which is how a window teaches somebody the wrong rule and
    /// then behaves correctly.
    /// </summary>
    [Fact]
    public void A_group_that_adds_up_says_something_different_from_one_that_narrows()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        var adding = model.FilterGroups.First(group => group.AddsUp);
        var narrowing = model.FilterGroups.First(group => !group.AddsUp);

        Assert.NotEqual(adding.Hint, narrowing.Hint);
        Assert.NotEmpty(adding.Hint);
        Assert.NotEmpty(narrowing.Hint);
    }

    [Fact]
    public void No_group_says_its_chips_add_up_while_the_query_narrows_them()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        Assert.NotEmpty(model.FilterGroups);

        foreach (var group in model.FilterGroups)
        {
            var fields = group.Chips.Select(chip => chip.Field).Distinct(StringComparer.Ordinal).ToList();

            // The claim on screen and the fact underneath it, asserted against each other rather
            // than the claim being taken on trust.
            Assert.Equal(fields.Count == 1, group.AddsUp);

            // AND THE HALF WITH TEETH. A group of several fields is a row of independent switches
            // and says so - but two chips of the SAME field inside it would be ORed with each other
            // while everything around them ANDs, which is one group behaving two ways with nothing
            // on screen dividing it.
            Assert.True(
                group.AddsUp || fields.Count == group.Chips.Count,
                $"The group '{group.Label}' mixes fields AND repeats one of them - "
                + string.Join(", ", group.Chips.Select(chip => chip.Field))
                + ". Two chips of one field are ORed by the language while the rest of the group is "
                + "ANDed, so half of this group adds up and half narrows, under one name.");
        }
    }

    [Fact]
    public void Every_chip_stands_for_a_member_the_language_knows()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        Assert.NotEmpty(model.Filters);

        foreach (var chip in model.Filters)
        {
            var parsed = QueryParser.Parse(chip.Member);

            Assert.True(
                parsed.IsValid,
                $"The chip '{chip.Label}' stands for '{chip.Member}', which the language refuses: "
                + string.Join(", ", parsed.Problems.Select(problem => problem.Kind + " " + problem.Text)));

            // And it is the member it says it is, rather than one that merely parses.
            Assert.True(
                parsed.Query!.Carries(chip.Field, chip.Value, chip.Negated),
                $"'{chip.Member}' parses but does not carry {chip.Field}:{chip.Value}.");
        }
    }

    /// <summary>Two chips standing for the same member would be two controls fighting.</summary>
    [Fact]
    public void No_two_chips_stand_for_the_same_member()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        var members = model.Filters.Select(chip => chip.Member).ToList();

        Assert.Equal(members.Count, members.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Clicking_a_chip_writes_its_member_into_the_box()
    {
        var model = await Loaded();
        var stopped = Chip(model, "status", "stopped");

        Assert.False(stopped.IsOn);

        stopped.IsOn = true;

        Assert.Equal("status:stopped", model.QueryText);
        Assert.True(stopped.IsOn);
    }

    /// <summary>
    /// The other direction, which is the half that makes it teaching rather than a shortcut.
    /// </summary>
    [Fact]
    public async Task Typing_the_member_by_hand_lights_the_chip()
    {
        var model = await Loaded();
        var stopped = Chip(model, "status", "stopped");

        model.QueryText = "STATUS:Stopped";

        Assert.True(stopped.IsOn);

        model.QueryText = string.Empty;

        Assert.False(stopped.IsOn);
    }

    /// <summary>
    /// Clicking a chip off takes its member out and leaves everything else, including a member
    /// that arrived after it - the case the drivers switch could never do.
    /// </summary>
    [Fact]
    public async Task Clicking_a_chip_off_leaves_the_rest_of_the_query()
    {
        var model = await Loaded();
        var stopped = Chip(model, "status", "stopped");

        model.QueryText = "status:stopped spool";

        Assert.True(stopped.IsOn);

        stopped.IsOn = false;

        Assert.Equal("spool", model.QueryText);
        Assert.False(stopped.IsOn);
    }

    /// <summary>
    /// One edit moves every chip that has an opinion about it, not the one that was clicked.
    /// </summary>
    [Fact]
    public async Task Clearing_the_box_turns_every_chip_off()
    {
        var model = await Loaded();

        Chip(model, "status", "stopped").IsOn = true;
        Chip(model, "start", "disabled").IsOn = true;

        Assert.Equal(2, model.Filters.Count(chip => chip.IsOn));

        model.QueryText = string.Empty;

        Assert.DoesNotContain(model.Filters, chip => chip.IsOn);
    }

    /// <summary>
    /// EVERY CHIP IS TOLD TO LOOK AGAIN, AND THE CLAIM IS THE NOTIFICATION RATHER THAN THE VALUE.
    ///
    /// <b>Written because the test above could not see the fault.</b> The mutation runner broke
    /// the loop that tells the chips to re-read the query and
    /// <see cref="Clearing_the_box_turns_every_chip_off"/> stayed green - correctly, because
    /// <see cref="FilterChip.IsOn"/> is worked out on every read, so asking it directly always
    /// gets the right answer. What breaks is the only thing a chip on screen has: the change
    /// notification. Nothing would move, the row would sit lit over a query that no longer says
    /// anything about it, and every property read would insist it was fine.
    ///
    /// That is the shape this window has been caught by four times - a value that is right
    /// everywhere except where somebody is looking.
    /// </summary>
    [Fact]
    public async Task An_edit_anywhere_tells_every_chip_to_look_again()
    {
        var model = await Loaded();
        var told = new HashSet<string>(StringComparer.Ordinal);

        foreach (var chip in model.Filters)
        {
            var named = chip;

            named.PropertyChanged += (_, change) =>
            {
                if (change.PropertyName == nameof(FilterChip.IsOn))
                {
                    told.Add(named.Member);
                }
            };
        }

        // An edit that concerns exactly one of them. Every chip still has to look, because none
        // of them can know that from where it sits.
        model.QueryText = "status:stopped";

        Assert.Equal(
            model.Filters.Select(chip => chip.Member).OrderBy(member => member, StringComparer.Ordinal),
            told.OrderBy(member => member, StringComparer.Ordinal));
    }

    /// <summary>
    /// The named switch of `A7` and the chip of `A5` are one control, so they cannot disagree.
    /// Without this they are two views over one member that nothing holds together.
    /// </summary>
    [Fact]
    public async Task The_drivers_switch_and_the_drivers_chip_are_the_same_control()
    {
        var model = await Loaded();
        var drivers = model.Filters.Single(chip => chip.Negated && chip.Value == "driver");

        Assert.True(model.ShowDrivers);
        Assert.False(drivers.IsOn);

        drivers.IsOn = true;

        Assert.False(model.ShowDrivers);
        Assert.Equal("!type:driver", model.QueryText);

        model.ShowDrivers = true;

        Assert.False(drivers.IsOn);
        Assert.Equal(string.Empty, model.QueryText);
    }

    /// <summary>
    /// A chip says the member before it is clicked, so the box is not the only place the
    /// language appears. It is what the window shows as the tooltip.
    /// </summary>
    [Fact]
    public async Task A_chip_says_which_member_it_stands_for()
    {
        var model = await Loaded();

        Assert.Equal("status:stopped", Chip(model, "status", "stopped").Member);
        Assert.Equal("!type:driver", model.Filters.Single(chip => chip.Negated).Member);
    }

    /// <summary>Every chip says something a person can read, from the language file.</summary>
    [Fact]
    public async Task Every_chip_has_a_label_that_is_not_its_key()
    {
        var model = await Loaded();

        foreach (var chip in model.Filters)
        {
            Assert.False(string.IsNullOrWhiteSpace(chip.Label));
            Assert.DoesNotContain("gui.filter.", chip.Label, StringComparison.Ordinal);
        }
    }

    private static FilterChip Chip(MainViewModel model, string field, string value) =>
        model.Filters.Single(chip => chip.Field == field && chip.Value == value && !chip.Negated);

    private static async Task<MainViewModel> Loaded()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler"), Rows.Entry("Winmgmt")), new SteppedClock());

        await model.LoadAsync();

        return model;
    }
}
