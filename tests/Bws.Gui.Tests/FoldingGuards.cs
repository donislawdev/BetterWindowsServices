using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Bws.Core;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What `A11` promises about the window: session copies folded under the template they came from,
/// a count beside its name, a switch that puts them back, and a plan that names every one of them
/// before anything runs.
///
/// <b>THE LAST OF THOSE IS THE ONE WORTH HAVING TESTS FOR, and the specification says why in its
/// own words</b> - "inaczej admin kliknie stop na jednym wierszu i zatrzyma cztery uslugi, nie
/// wiedzac o tym". Everything else here is a list looking tidier. That one is a preview shorter
/// than the run, which is the single failure `ADR-11` exists to prevent.
///
/// <b>The shapes come from a real machine rather than from imagination</b> - measured with
/// <c>sc.exe</c> over 798 names on 2026-08-25: 23 templates, 23 instances, one logged-on session,
/// no orphans, every template stopped, 8 of the instances running. The two shapes this machine
/// CANNOT produce are built by hand here and named as such: two sessions on one template, and an
/// instance whose template is missing.
/// </summary>
public sealed class FoldingGuards
{
    [Fact]
    public async Task A_session_copy_is_drawn_under_the_template_it_came_from()
    {
        var model = await Looking(
            Rows.Template("CDPUserSvc"),
            Rows.Instance("CDPUserSvc_7b537"),
            Rows.Entry("Spooler"));

        Assert.Equal(["CDPUserSvc", "Spooler"], model.Rows.Select(row => row.ServiceName));
    }

    /// <summary>
    /// The even claim, and without it the one above passes on a window that hides every instance
    /// whether or not anything is standing over it.
    /// </summary>
    [Fact]
    public async Task An_instance_whose_template_is_not_in_the_answer_stays_a_row_of_its_own()
    {
        var model = await Looking(
            Rows.Template("CDPUserSvc"),
            Rows.Instance("CDPUserSvc_7b537"));

        // The query that exists to find them. Folding before the query would hide exactly what was
        // asked for, and hide it under a row the query had rejected.
        model.QueryText = "peruser:instance";

        Assert.Equal(["CDPUserSvc_7b537"], model.Rows.Select(row => row.ServiceName));
    }

    /// <summary>
    /// The specimen that punishes grouping by name, and the reason the fold asks the type bits
    /// first. Both of these are ordinary services on a real machine and neither carries a per-user
    /// bit - a window that folded by suffix would file the power service away as session noise.
    /// </summary>
    [Fact]
    public async Task Two_names_that_look_like_a_family_and_are_not_are_left_alone()
    {
        var pair = Rows.TheirNamesLookLikeAFamily();

        var model = await Looking(pair.Looks, pair.LikeATail);

        Assert.Equal(["Power", "Power_a17007"], model.Rows.Select(row => row.ServiceName));
        Assert.All(model.Rows, row => Assert.Equal(string.Empty, row.StandsFor));
    }

    /// <summary>
    /// The same trap one level in, and it is the half a mutation can actually reach.
    ///
    /// <b>The test above cannot be broken by loosening ONE of the two questions the fold asks</b> -
    /// neither <c>Power</c> nor <c>Power_a17007</c> carries a per-user bit, so dropping either check
    /// alone still leaves the other refusing them. That makes it a good guard and a poor claim about
    /// which line is load bearing. This one names that line: a REAL template, and beside it an
    /// ordinary service whose name happens to begin with the template's own. Only the type bits
    /// separate them, and folding by name would swallow a service nobody has anything to do with.
    ///
    /// Not seen on this machine and not required to be. `docs/03` records that the suffix is wrong
    /// about two entries in 798, which is the same fault with one of the two sides already proved
    /// reachable.
    /// </summary>
    [Fact]
    public async Task A_service_that_only_looks_like_a_copy_of_a_real_template_is_left_alone()
    {
        var model = await Looking(
            Rows.Template("CDPUserSvc"),
            Rows.Entry("CDPUserSvc_backup"));

        Assert.Equal(["CDPUserSvc", "CDPUserSvc_backup"], model.Rows.Select(row => row.ServiceName));
    }

    /// <summary>
    /// An instance whose prefix names nothing. It cannot be produced on this machine - 23 of 23
    /// instances here have a template beside them - so it is built by hand, and it is the (D) case
    /// this file is written around: a row must never disappear because the window could not work
    /// out where to put it.
    /// </summary>
    [Fact]
    public async Task An_instance_with_nothing_to_stand_under_is_still_shown()
    {
        var model = await Looking(Rows.Instance("OrphanSvc_7b537"), Rows.Entry("Spooler"));

        Assert.Equal(["OrphanSvc_7b537", "Spooler"], model.Rows.Select(row => row.ServiceName));
    }

    [Fact]
    public async Task The_switch_puts_every_session_copy_back_on_a_row_of_its_own()
    {
        var model = await Looking(
            Rows.Template("CDPUserSvc"),
            Rows.Instance("CDPUserSvc_7b537"));

        model.ShowingEveryInstance = true;

        Assert.Equal(["CDPUserSvc", "CDPUserSvc_7b537"], model.Rows.Select(row => row.ServiceName));

        // AND THE ROW STOPS CLAIMING TO STAND FOR ANYTHING, which is not decoration. What a row
        // stands for is what a plan is built from, so a badge left behind here would put two
        // services under one press while both of them are on screen separately.
        Assert.All(model.Rows, row => Assert.Empty(row.Instances));
    }

    /// <summary>
    /// The owner's decision of 2026-08-25, and it answers a fault the fold would otherwise create.
    /// A template is never running, so a folded row's Status column says "Stopped" about a family
    /// that is working - the column stays true to the entry, because that is what <c>sc.exe</c>
    /// says about it, and the badge carries what the column cannot.
    /// </summary>
    [Fact]
    public async Task The_count_beside_the_name_says_how_many_of_the_family_are_running()
    {
        var model = await Looking(
            Rows.Template("CDPUserSvc"),
            Rows.Instance("CDPUserSvc_7b537") with { Status = EntryStatus.Running },
            Rows.Instance("CDPUserSvc_a1b2c") with { Status = EntryStatus.Stopped });

        var folded = Assert.Single(model.Rows);

        Assert.Equal(EntryStatus.Stopped, folded.Entry.Status);
        Assert.Equal("2 instances, 1 running", folded.StandsFor);
    }

    /// <summary>
    /// A Name column too narrow for both takes the COUNT away and never the name.
    ///
    /// <b>WRITTEN 2026-09-02 BECAUSE IT DID THE OPPOSITE AND SHIPPED THAT WAY.</b> The owner
    /// photographed a row of their own list reading <c>"...1 instance"</c> - a service with no name
    /// on it. The cell is a Grid, the name had the star and the badge was Auto, and a Grid gives an
    /// Auto column what it asks for and a star column whatever is left: so the annotation survived
    /// whole and the identity was trimmed to an ellipsis.
    ///
    /// <b>`ADR-14` is what decides it.</b> The internal name is the one string that is not
    /// translated, the one somebody types into a terminal, and the one every plan and every
    /// clipboard copy carries. A count of folded copies is a note ABOUT that row. A note may not be
    /// the thing that survives while the identity disappears.
    ///
    /// <b>Arranged at a width too small for both, which is the only width that can tell the two
    /// arrangements apart.</b> Give the cell enough room and either one passes.
    /// </summary>
    [Fact]
    public async Task A_name_column_too_narrow_for_both_gives_way_on_the_count_and_not_on_the_name()
    {
        var model = await Looking(
            Rows.Template("CDPUserSvc"),
            Rows.Instance("CDPUserSvc_7b537") with { Status = EntryStatus.Stopped });

        var folded = Assert.Single(model.Rows);

        Assert.Equal("1 instance", folded.StandsFor);

        var (name, badge) = WpfHost.On(() =>
        {
            var holder = new ContentPresenter
            {
                ContentTemplate = (DataTemplate)WpfHost.Resources["NameCell"],
                Content = folded
            };

            // Narrower than "CDPUserSvc" and "1 instance" together, on purpose.
            holder.Measure(new Size(70, 24));
            holder.Arrange(new Rect(0, 0, 70, 24));
            holder.UpdateLayout();

            var texts = new List<TextBlock>();
            Gather(holder, texts);

            return (
                texts.Single(text => text.Text == folded.Entry.ServiceName).ActualWidth,
                texts.Single(text => text.Text == folded.StandsFor).ActualWidth);
        });

        Assert.True(name > 0, "The name was given no width at all, so the row shows no service.");

        Assert.True(
            name >= badge,
            $"The name got {name} points and the count beside it got {badge}. In a cell too narrow "
            + "for both, the count is the half that has to give way - a row reading \"...1 instance\" "
            + "names nothing anybody could act on.");
    }

    /// <summary>Every TextBlock a template built, wherever it put them.</summary>
    private static void Gather(DependencyObject from, List<TextBlock> into)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(from); i++)
        {
            var child = VisualTreeHelper.GetChild(from, i);

            if (child is TextBlock text)
            {
                into.Add(text);
            }

            Gather(child, into);
        }
    }

    /// <summary>
    /// One instance and none of it running, which is the ordinary shape on a machine where nobody
    /// has used the feature yet. A singular beside every plural - backlog 207.
    /// </summary>
    [Fact]
    public async Task One_stopped_copy_is_counted_in_the_singular_and_says_nothing_about_running()
    {
        var model = await Looking(
            Rows.Template("CDPUserSvc"),
            Rows.Instance("CDPUserSvc_7b537") with { Status = EntryStatus.Stopped });

        Assert.Equal("1 instance", Assert.Single(model.Rows).StandsFor);
    }

    /// <summary>
    /// A template that stopped standing for anything has to lose the badge saying it does.
    ///
    /// <b>Reachable without the switch, which is why it is tested through the query.</b> Narrowing
    /// to the templates alone takes every instance out of the answer, so nothing is left to fold -
    /// and a badge that survived would be a row claiming a family that is no longer under it, with
    /// a plan built from the claim rather than from the screen.
    /// </summary>
    [Fact]
    public async Task A_row_that_stops_standing_for_a_family_stops_saying_that_it_does()
    {
        var model = await Looking(
            Rows.Template("CDPUserSvc"),
            Rows.Instance("CDPUserSvc_7b537") with { Status = EntryStatus.Stopped });

        Assert.Equal("1 instance", Assert.Single(model.Rows).StandsFor);

        model.QueryText = "peruser:template";

        var alone = Assert.Single(model.Rows);

        Assert.Equal(string.Empty, alone.StandsFor);
        Assert.Empty(alone.Instances);
    }

    /// <summary>
    /// Rule 8 where a fold can break it: the number over the list counts ENTRIES the query
    /// selected, the list under it is shorter, and this is the only thing that says why.
    /// </summary>
    [Fact]
    public async Task The_window_says_how_many_copies_it_folded_away()
    {
        var model = await Looking(
            Rows.Template("CDPUserSvc"),
            Rows.Instance("CDPUserSvc_7b537"),
            Rows.Entry("Spooler"));

        Assert.Contains("folded", model.Says.Notice, StringComparison.OrdinalIgnoreCase);

        // THE COUNT LINE IS UNTOUCHED, and that pair is the whole honesty of the arrangement. Three
        // entries matched, two rows are drawn, and the sentence above accounts for the difference.
        Assert.Equal("3 entries", model.Says.Status);
        Assert.Equal(2, model.Rows.Count);
    }

    /// <summary>
    /// The even claim. Without it the sentence above passes on a window that says it always, which
    /// is the shape <c>AdmissionTests</c> keeps catching in this same line of text.
    /// </summary>
    [Fact]
    public async Task A_machine_with_no_per_user_services_is_told_nothing_about_folding()
    {
        var model = await Looking(Rows.Entry("Spooler"));

        Assert.Equal(string.Empty, model.Says.Notice);
    }

    /// <summary>
    /// THE HALF THE SPECIFICATION WARNS ABOUT. A plan built from a folded row has to name the whole
    /// family before anything runs, or one press stops four services and the preview shows one.
    ///
    /// <b>The template is in the list beside its instances, and that is a measurement rather than a
    /// reading of the glossary sentence.</b> Template and instance agree on the start type 23 times
    /// out of 23 on this machine, and the template is what the next session's copy is made from - so
    /// a plan that left it out would disable the copies that exist and let the next logon make an
    /// automatic one, with nothing on screen having been wrong.
    /// </summary>
    [Fact]
    public async Task A_plan_over_a_folded_row_names_the_template_and_every_copy_under_it()
    {
        var machine = new LiveMachine(
            Rows.Template("CDPUserSvc"),
            Rows.Instance("CDPUserSvc_7b537"),
            Rows.Instance("CDPUserSvc_a1b2c"));

        var model = new MainViewModel(machine, new SteppedClock());
        var window = WpfHost.Window(model);

        await WpfHost.On(model.LoadAsync);

        var folded = Assert.Single(model.Rows);

        WpfHost.On(() => window.Entries.SelectedItem = folded);
        WpfHost.Settled();

        Assert.True(
            await WpfHost.On(() => window.Preview(ActionKind.Stop)),
            "The preview opened nothing, so this proves nothing about what it would have named.");

        Assert.Equal(
            ["CDPUserSvc", "CDPUserSvc_7b537", "CDPUserSvc_a1b2c"],
            model.Planned.Plan!.Action.ServiceNames);
    }

    /// <summary>
    /// The even claim, and it is the one that keeps the expansion honest rather than eager. With
    /// the copies on screen in their own right, a person picking the template alone has picked the
    /// template - widening that would be the window deciding what they meant.
    /// </summary>
    [Fact]
    public async Task A_plan_over_a_row_that_stands_alone_names_only_that_row()
    {
        var machine = new LiveMachine(
            Rows.Template("CDPUserSvc"),
            Rows.Instance("CDPUserSvc_7b537"));

        var model = new MainViewModel(machine, new SteppedClock());
        var window = WpfHost.Window(model);

        await WpfHost.On(model.LoadAsync);

        // ON THE INTERFACE THREAD, because the switch runs the whole pass and the pass reconciles a
        // list a real grid is bound to. WPF refuses a collection change from anywhere else, and the
        // refusal is the framework protecting a view that would otherwise be reading a list while
        // it moved.
        WpfHost.On(() => model.ShowingEveryInstance = true);

        var template = model.Rows.Single(row => row.ServiceName == "CDPUserSvc");

        WpfHost.On(() => window.Entries.SelectedItem = template);
        WpfHost.Settled();

        Assert.True(
            await WpfHost.On(() => window.Preview(ActionKind.Stop)),
            "The preview opened nothing, so this proves nothing about what it would have named.");

        Assert.Equal(["CDPUserSvc"], model.Planned.Plan!.Action.ServiceNames);
    }

    /// <summary>
    /// The words "Show every instance" in the sentence under the list are the switch's own words,
    /// cut out of the line as a link, and the line reads exactly as it did with them in it -
    /// point 8(d) of `docs/11` 2.14.
    ///
    /// <b>One line and three pieces from the same words</b>, which is what <c>Admitted</c> is for:
    /// a test reading the line, an automation name reading the line and a view drawing the pieces
    /// must never disagree about a word.
    /// </summary>
    [Fact]
    public async Task The_sentence_about_folded_copies_carries_the_switch_as_a_link_and_reads_as_one_line()
    {
        var model = await Looking(Rows.Template("CDPUserSvc"), Rows.Instance("CDPUserSvc_7b537"), Rows.Entry("Spooler"));

        Assert.True(model.Says.NoticeHasLink);
        Assert.Equal(Bws.Gui.Texts.Of("gui.instances.toggle"), model.Says.NoticeLink);
        Assert.Equal(model.Says.NoticeBeforeLink + model.Says.NoticeLink + model.Says.NoticeAfterLink, model.Says.Notice);
        Assert.Contains(Bws.Gui.Texts.Of("gui.status.folded.one", 1), model.Says.NoticeBeforeLink, StringComparison.Ordinal);
        Assert.Contains(Bws.Gui.Texts.Of("gui.status.folded.one.after"), model.Says.NoticeAfterLink, StringComparison.Ordinal);

        WpfHost.On(() => model.ShowingEveryInstance = true);

        // Nothing folds, so nothing to press: the link is empty, and empty is what keeps it out
        // of the Tab order.
        Assert.False(model.Says.NoticeHasLink);
        Assert.Equal(string.Empty, model.Says.NoticeLink);
        Assert.DoesNotContain("folded", model.Says.Notice, StringComparison.Ordinal);
    }

    /// <summary>
    /// And on the window, pressing the words presses the switch: the copies unfold and the
    /// sentence goes. The Click is raised on the Hyperlink as WPF raises it, and the handler in
    /// StatusRow.xaml.cs is what turns it into the model's own property.
    /// </summary>
    [Fact]
    public async Task Pressing_the_words_in_the_sentence_unfolds_the_copies()
    {
        var machine = new LiveMachine(Rows.Template("CDPUserSvc"), Rows.Instance("CDPUserSvc_7b537"));
        var model = new MainViewModel(machine, new SteppedClock()) { Says = new Says { Elevated = true } };
        var window = WpfHost.Window(model);

        await WpfHost.On(model.LoadAsync);
        WpfHost.Settled();

        var link = WpfHost.On(() => window.Status.NoticeLinkWords);

        Assert.True(WpfHost.On(() => link.IsEnabled), "the link is switched off while there is something folded to unfold");
        Assert.Single(model.Rows);

        // NOT VACUOUS: the line really carries the sentence before the press, so an empty line
        // after it cannot pass for a changed one.
        Assert.Contains("folded", WpfHost.On(() => Line((TextBlock)link.Parent)), StringComparison.Ordinal);

        WpfHost.On(() => link.RaiseEvent(new RoutedEventArgs(System.Windows.Documents.Hyperlink.ClickEvent, link)));
        WpfHost.Settled();

        Assert.True(model.ShowingEveryInstance);
        Assert.Equal(2, model.Rows.Count);
        Assert.False(WpfHost.On(() => link.IsEnabled), "nothing is folded any more, and an enabled empty link would be a Tab stop that does nothing");

        // THE LINE ON THE WINDOW MOVED TOO - read off the TextBlock the link lives in, because
        // three runs bound to three properties are three bindings that can each be left behind.
        //
        // THROUGH A TextRange AND NOT TextBlock.Text, WHICH IS MEASURED: a TextBlock built from
        // inlines answers an EMPTY string to Text, so the first version of this assertion passed
        // on nothing - and the mutation register said so, on the entry that takes one of the
        // three notifications away. The automation name is the whole line, measured the same day,
        // which is why the probes reading noticeLine are unaffected.
        Assert.DoesNotContain("folded", WpfHost.On(() => Line((TextBlock)link.Parent)), StringComparison.Ordinal);

        WpfHost.On(window.Close);
    }

    private static string Line(TextBlock block) =>
        new System.Windows.Documents.TextRange(block.ContentStart, block.ContentEnd).Text;

    /// <summary>
    /// A window looking at a machine, with its first reading already done.
    ///
    /// No window is built, deliberately. Everything above except the two plan tests is about what
    /// the view model decides, and that is checkable without an interface thread - which is the
    /// property <c>MainViewModel</c> is shaped for.
    /// </summary>
    private static async Task<MainViewModel> Looking(params ScmEntry[] machine)
    {
        var model = new MainViewModel(new LiveMachine(machine), new SteppedClock())
        {
            Says = new Says { Elevated = true }
        };

        await model.LoadAsync();

        return model;
    }
}
