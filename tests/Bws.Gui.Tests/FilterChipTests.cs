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
        // BUILT HERE RATHER THAN FOUND IN THE CATALOGUE SINCE 2026-08-19, AND THE REASON IS A
        // FINDING RATHER THAN A CONVENIENCE. This test used to reach into the real groups for one
        // of each. When the drivers chip left for the scope switch, the last mixed group lost its
        // second field - so EVERY group in the catalogue now adds up, the narrowing sentence became
        // unreachable, and this test went red for the honest reason that there was no longer one of
        // each to find.
        //
        // TextKeyGuards did NOT catch that, and the limit is worth writing down: it looks for the
        // key at a call site, and Texts.Of("gui.filter.hint.narrows") is still written in
        // FilterGroup.Hint. A sentence that can be reached by no data on the machine is invisible
        // to it. Backlog 215.
        //
        // The property under test was never about the catalogue - it is that the hint follows the
        // field count - so it is asked of two groups made for the question. The capability is kept
        // rather than deleted because `A5` still names families that will arrive on other fields.
        var group = new List<FilterChip>
        {
            new("gui.filter.stopped", "status", "stopped", negated: false, () => string.Empty, _ => { }),
            new("gui.filter.running", "status", "running", negated: false, () => string.Empty, _ => { })
        };

        var mixed = new List<FilterChip>
        {
            group[0],
            new("gui.filter.triggered", "trigger", "any", negated: false, () => string.Empty, _ => { })
        };

        var adding = new FilterGroup("gui.filter.group.state", group);
        var narrowing = new FilterGroup("gui.filter.group.about", mixed);

        Assert.True(adding.AddsUp);
        Assert.False(narrowing.AddsUp);

        Assert.NotEqual(adding.Hint, narrowing.Hint);
        Assert.NotEmpty(adding.Hint);
        Assert.NotEmpty(narrowing.Hint);
    }

    /// <summary>
    /// Every group in the catalogue adds up today, and that is stated rather than left to be
    /// noticed.
    ///
    /// <b>This is not a property anybody wants - it is a record of where the row stands.</b> It
    /// became true on 2026-08-19 when the drivers chip left, and it means the window cannot
    /// currently show the sentence about groups narrowing each other. If a chip on a new field
    /// joins an existing group, this goes red and whoever wrote it gets to decide on purpose
    /// whether the boundary the hint describes is back.
    /// </summary>
    [Fact]
    public void No_group_in_the_catalogue_mixes_fields_today()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        Assert.All(model.FilterGroups, group => Assert.True(group.AddsUp));
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
    /// EVERY CHIP OF ONE FIELD STAYS LIT WHEN THE NEXT ONE IS CLICKED - owner's report, 2026-08-13.
    ///
    /// <b>The case no test had, and the one a person reaches for first.</b> The tests above click
    /// two chips of DIFFERENT fields, which the query ANDs - so nothing here ever asked what happens
    /// when three chips of one field are on at once, which is the thing the whole grouping of this
    /// row is about: <see cref="FilterGroup.AddsUp"/> promises that a group adds up, and a row where
    /// only the last click shows is that promise visibly broken.
    ///
    /// Both halves are claimed, because they can fail apart: the TEXT has to carry all three, and
    /// every chip has to READ itself out of it. Measured against the machine first - start:manual is
    /// 566 entries, start:disabled 50, start:boot 54, and all three together 670, which is their
    /// sum - so the language was never the part in doubt.
    /// </summary>
    [Fact]
    public async Task Every_chip_of_one_field_stays_lit_when_the_next_one_is_clicked()
    {
        var model = await Loaded();

        var manual = Chip(model, "start", "manual");
        var disabled = Chip(model, "start", "disabled");
        var boot = Chip(model, "start", "boot");

        manual.IsOn = true;
        disabled.IsOn = true;
        boot.IsOn = true;

        Assert.Equal("start:manual start:disabled start:boot", model.QueryText);

        // One assertion over all three, because which of them went out is the whole diagnosis and
        // three separate ones report only the first to fail.
        Assert.Equal(
            "manual=True disabled=True boot=True",
            $"manual={manual.IsOn} disabled={disabled.IsOn} boot={boot.IsOn}");
    }

    /// <summary>
    /// The same for the state facet, and in the opposite click order.
    ///
    /// <b>Order is asserted because a person clicks in whatever order they think in.</b> A row that
    /// only worked left to right would pass the test above and fail in front of somebody.
    /// </summary>
    [Fact]
    public async Task Turning_one_chip_of_a_group_off_leaves_the_others_lit()
    {
        var model = await Loaded();

        var paused = Chip(model, "status", "paused");
        var stopped = Chip(model, "status", "stopped");

        paused.IsOn = true;
        stopped.IsOn = true;

        Assert.True(paused.IsOn);
        Assert.True(stopped.IsOn);

        paused.IsOn = false;

        Assert.False(paused.IsOn);
        Assert.True(stopped.IsOn, "Turning one chip of a group off took another one with it.");
        Assert.Equal("status:stopped", model.QueryText);
    }

    /// <summary>
    /// No chip speaks for the type field any more, and this is the assertion that keeps it that way.
    ///
    /// <b>It replaces The_drivers_switch_and_the_drivers_chip_are_the_same_control, which held
    /// exactly the opposite and was right until 2026-08-19.</b> Scope is a state beside the query
    /// now - owner's decision - and a chip for <c>type:driver</c> standing beside it would be a
    /// second control writing what the switch decides, so the two would disagree the first time
    /// anybody touched either. That is the failure this file's own doctrine names about chips, met
    /// from the other direction.
    ///
    /// <b>The field rather than the chip's label</b>, because a chip added later for
    /// <c>type:ownProcess</c> would be the same fault wearing a different word.
    /// </summary>
    [Fact]
    public async Task No_chip_speaks_for_the_field_the_scope_switch_owns()
    {
        var model = await Loaded();

        Assert.DoesNotContain(model.Filters, chip => chip.Field == "type");
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

        // THE NEGATED EXAMPLE LEFT WITH THE DRIVERS CHIP ON 2026-08-19, and it was the only one -
        // no chip is negated today. FilterChip still knows how to write one, and that capability is
        // kept rather than removed because `A5` names a signature family whose useful question is
        // "not signed". Said here so that "nothing negated exists" is a fact somebody read rather
        // than a gap they have to rediscover.
        Assert.DoesNotContain(model.Filters, chip => chip.Negated);
    }

    /// <summary>
    /// THE WINDOW OPENS ON SERVICES - owner's decision, 2026-08-13, because <c>services.msc</c> does
    /// and that is the tool people will compare this against. Measured on this machine 2026-08-19:
    /// drivers are 472 of 812 entries.
    ///
    /// <b>THE CLAIM SURVIVED 2026-08-19 AND ITS MECHANISM DID NOT, which is the whole of what the
    /// scope switch changed.</b> Until then this was a MEMBER OF THE QUERY: the box opened saying
    /// <c>!type:driver</c>, the chip was lit, and one Escape gave the machine back - because a
    /// default that appeared nowhere would be a filter nothing on screen admits to, rule 8 broken by
    /// the first thing a person sees. The switch admits to it in a better place, so the box opens
    /// EMPTY and the position says which list this is.
    ///
    /// <b>And Escape stops being the way back, which is the part worth asserting rather than
    /// assuming.</b> Emptying the question now leaves the window on the list it was on.
    /// </summary>
    [Fact]
    public async Task The_window_opens_on_services_with_an_empty_box()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler"), Rows.Driver("beep")), new SteppedClock());

        await model.LoadAsync();

        Assert.Equal(EntryScope.Services, model.Scope);
        Assert.Equal(string.Empty, model.QueryText);
        Assert.Single(model.Rows);

        // Nothing to clear, so Escape is left alone for whatever else wants it - and the list does
        // not change, because the question was never what was hiding the drivers.
        Assert.False(model.ClearQuery());
        Assert.Single(model.Rows);

        model.Scope = EntryScope.Everything;

        Assert.Equal(2, model.Rows.Count);
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

    /// <summary>
    /// A window that has read a small machine, with the box emptied.
    ///
    /// <b>Emptied on purpose, since 2026-08-13.</b> The window opens on services - owner's decision -
    /// and every test in this file is about what a CLICK does, not about what the window opens with.
    /// Leaving the opening state in place would make each of them assert two things at once and
    /// report the wrong one first.
    ///
    /// <b>That the window opens that way is asserted once, on its own</b>, in
    /// <see cref="The_window_opens_on_services_with_an_empty_box"/> - which is where a change to
    /// that decision should go red, rather than in fourteen tests about something else.
    ///
    /// <b>The machine here holds no drivers at all</b>, so the opening scope selects everything in
    /// it and this helper needs no scope of its own. Adding one would silently make these tests
    /// depend on a decision they are not about.
    /// </summary>
    private static async Task<MainViewModel> Loaded()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler"), Rows.Entry("Winmgmt")), new SteppedClock());

        await model.LoadAsync();

        model.ClearQuery();

        return model;
    }
}
