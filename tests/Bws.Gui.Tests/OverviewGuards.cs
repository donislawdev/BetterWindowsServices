using System.Windows;
using System.Windows.Controls;
using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What `G` promises about the first screen: a handful of numbers instead of several hundred rows,
/// every one of them a question that can be asked again, and one number named as missing rather
/// than guessed at.
///
/// <b>THE NUMBER THIS SCREEN LIVES OR DIES BY IS THE SECOND ONE, and it is not the literal answer to
/// acceptance scenario 1.</b> Measured on a real machine on 2026-08-25: the literal query answers 7
/// or 8 depending on the minute and six or seven of those are false alarms - per-user templates,
/// which never run because a session copy runs instead, and entries with a trigger, which are
/// stopped because nothing has asked for them. A first screen saying "8 services did not come up"
/// on a machine where one did is the window being confidently wrong in the first thing anybody
/// reads.
/// </summary>
public sealed class OverviewGuards
{
    [Fact]
    public async Task The_headline_leaves_out_the_two_false_alarms_and_names_both_of_them()
    {
        var model = await Looking(
            // The one true finding: automatic, stopped, no trigger, not part of the per-user family.
            Rows.Stopped("AsusUpdateCheck"),

            // A template. Automatic and stopped because it is a pattern rather than a service, with
            // its session copy running beside it.
            Rows.Template("CDPUserSvc") with { StartType = Reading<StartType>.Present(StartType.Automatic) },
            Rows.Instance("CDPUserSvc_7b537"),

            // And one waiting to be asked for.
            Rows.Stopped("gpsvc") with
            {
                // Bws.Core.TriggerAction spelled out, because System.Windows declares one too and
                // this file has both namespaces in scope.
                Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Present(
                    [new ServiceTrigger(TriggerKind.Unknown, Bws.Core.TriggerAction.Start)])
            });

        var lines = model.Overview;

        var headline = lines.Single(line => line.Label.Contains("did not come up", StringComparison.Ordinal));

        Assert.Equal(1, headline.Count);
        Assert.True(headline.Leading);

        // BOTH EXCLUSIONS ARE ON SCREEN, WHICH IS WHAT MAKES THE HEADLINE HONEST RATHER THAN
        // CONVENIENT. A number of 1 with nothing under it is a window being quietly incomplete.
        var templates = lines.Single(line => line.Label.Contains("per-user templates", StringComparison.Ordinal));
        var waiting = lines.Single(line => line.Label.Contains("waiting for a trigger", StringComparison.Ordinal));

        Assert.Equal(1, templates.Count);
        Assert.Equal(1, waiting.Count);
        Assert.False(templates.Leading);
        Assert.False(waiting.Leading);
    }

    /// <summary>
    /// A line that found nothing says so as a question markup can ask.
    ///
    /// <b>Added 2026-09-01 after the built window showed a zero shouting as loudly as a hundred.</b>
    /// The screen exists to show what is wrong with a machine, so "0 orphans" - the line where
    /// nothing is - was spending the loudest voice on the best news.
    ///
    /// <b>A bool rather than a trigger comparing the number to zero, and this test is what holds
    /// that choice.</b> A DataTrigger read from markup keeps its Value as the string the file held,
    /// and the first attempt asked the number and did not fire on the running window. Every trigger
    /// on this screen that demonstrably works asks a bool, so the zero asks one too.
    /// </summary>
    [Fact]
    public async Task A_line_that_found_nothing_says_so()
    {
        var model = await Looking(Rows.Entry("Spooler"));

        var orphans = model.Overview.Single(line => line.Label.Contains("orphans", StringComparison.Ordinal));
        var running = model.Overview.Single(line => line.Label.Contains("services running", StringComparison.Ordinal));

        Assert.Equal(0, orphans.Count);
        Assert.True(orphans.Nothing);

        // The even claim, and without it this passes on a screen that calls every line empty.
        Assert.Equal(1, running.Count);
        Assert.False(running.Nothing);
    }

    /// <summary>
    /// The even claim, and without it the one above passes on a screen that answers 1 to everything.
    /// A machine where the query is genuinely right answers with the literal number.
    /// </summary>
    [Fact]
    public async Task A_machine_with_no_false_alarms_answers_the_literal_question()
    {
        var model = await Looking(Rows.Stopped("AsusUpdateCheck"), Rows.Stopped("Spooler"));

        var headline = model.Overview.Single(line =>
            line.Label.Contains("did not come up", StringComparison.Ordinal));

        Assert.Equal(2, headline.Count);

        Assert.All(
            model.Overview.Where(line => !line.Leading),
            line => Assert.Equal(0, line.Count));
    }

    /// <summary>
    /// EVERY NUMBER IS COUNTED INSIDE THE LIST THE WINDOW WILL SHOW, and this is the property that
    /// keeps the click honest. Clicking one lands on the services list, so a number counted over the
    /// whole machine would be the screen contradicting itself in the one gesture it promises.
    /// </summary>
    [Fact]
    public async Task No_number_counts_a_driver()
    {
        var model = await Looking(
            Rows.Entry("Spooler"),
            Rows.Driver("tapnordvpn") with { Status = EntryStatus.Running });

        var running = model.Overview.Single(line => line.Label.Contains("services running", StringComparison.Ordinal));

        Assert.Equal(1, running.Count);
        Assert.All(model.Overview, line => Assert.Contains("!type:driver", line.Query, StringComparison.Ordinal));
    }

    /// <summary>
    /// The orphan of `C13` is a COMPOSITION rather than a word, and the number beside it is the one
    /// this machine actually has - owner's decision, 2026-08-25. A screen whose number is always
    /// zero teaches that the screen is useless, so the promise is kept and the true finding is shown
    /// under it.
    /// </summary>
    [Fact]
    public async Task An_orphan_is_told_apart_from_an_entry_that_merely_lost_its_file()
    {
        var model = await Looking(
            Rows.Entry("LibreOfficeMaintenance") with
            {
                StartType = Reading<StartType>.Present(StartType.Disabled),
                BinaryOnDisk = Reading<bool>.Present(false)
            },
            Rows.Stopped("Orphaned") with { BinaryOnDisk = Reading<bool>.Present(false) });

        var orphans = model.Overview.Single(line => line.Label.Contains("orphans", StringComparison.Ordinal));
        var gone = model.Overview.Single(line => line.Label.Contains("whatever their startup type", StringComparison.Ordinal));

        Assert.Equal(1, orphans.Count);
        Assert.Equal(2, gone.Count);
    }

    /// <summary>
    /// `G` promises a fourth number this build cannot count - it needs `D6`, the stock baseline,
    /// which `docs/01` puts in Phase 3 twice. Naming it is that document's own answer to its own
    /// risk R3: mark the absence rather than quietly show a bad result.
    /// </summary>
    [Fact]
    public async Task The_number_this_build_cannot_count_is_named_rather_than_left_out()
    {
        var model = await Looking(Rows.Entry("Spooler"));

        Assert.False(string.IsNullOrWhiteSpace(model.OverviewMissing));
        Assert.DoesNotContain(model.Overview, line => line.Label.Contains("stock", StringComparison.Ordinal));
    }

    /// <summary>
    /// `G`'s own promise about the gesture: one click from every number leads to the filtered list.
    /// It writes the QUERY rather than filtering by another route, which is `A5`'s promise applied
    /// to a screen with no chips on it.
    /// </summary>
    [Fact]
    public async Task Clicking_a_number_asks_its_question_and_puts_the_screen_away()
    {
        var model = await Looking(Rows.Entry("Spooler"), Rows.Driver("tapnordvpn"));

        var running = model.Overview.Single(line => line.Label.Contains("services running", StringComparison.Ordinal));

        model.Ask(running);

        Assert.False(model.ShowingOverview);
        Assert.Equal(running.Query, model.QueryText);
        Assert.Equal(["Spooler"], model.Rows.Select(row => row.ServiceName));
    }

    [Fact]
    public async Task A_profile_that_has_never_put_it_away_opens_on_it()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());
        var window = WpfHost.Window(model, seenTheOverview: false);

        await WpfHost.On(model.LoadAsync);
        WpfHost.Settled();

        Assert.True(model.ShowingOverview);

        // THE LIST IS OFF THE SCREEN RATHER THAN BEHIND IT, which is what `G` asks for in as many
        // words - not the raw alphabetical list. Asked of the control rather than of the state,
        // because a state nothing acted on looks exactly like a screen that works.
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Entries.Visibility));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Overview.Visibility));
    }

    /// <summary>
    /// The even claim, and it is the one that keeps eleven probes and every other window test
    /// working: a profile that has seen this screen opens on the list.
    /// </summary>
    [Fact]
    public async Task A_profile_that_has_put_it_away_opens_on_the_list()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());
        var window = WpfHost.Window(model);

        await WpfHost.On(model.LoadAsync);
        WpfHost.Settled();

        Assert.False(model.ShowingOverview);
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.Entries.Visibility));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Overview.Visibility));
    }

    /// <summary>
    /// The way back, which `G` does not ask for and which the screen needs - owner's decision,
    /// 2026-08-25. Without it this appears once in the life of a profile, and the person it is
    /// written for opens the tool on a new server every time.
    /// </summary>
    [Fact]
    public async Task The_bar_can_ask_for_it_back()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());
        var window = WpfHost.Window(model);

        await WpfHost.On(model.LoadAsync);

        Assert.False(model.ShowingOverview);

        WpfHost.On(() => window.Actions.OverviewBack.RaiseEvent(
            new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)));

        WpfHost.Settled();

        Assert.True(model.ShowingOverview);
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.Entries.Visibility));
    }

    /// <summary>
    /// PUTTING IT AWAY IS REMEMBERED, which is the whole of what schema 4 bought.
    ///
    /// <b>Written the moment it is dismissed rather than when the window closes</b> - a first run
    /// ended by a machine going down would otherwise show this screen again to somebody who had
    /// already read it, and half this project's probes end by killing the process.
    ///
    /// Read back through a second reader rather than asked of the one that wrote it, because a
    /// value held in memory proves nothing about a file.
    /// </summary>
    [Fact]
    public void Putting_the_overview_away_is_written_down_and_read_back()
    {
        var file = WpfHost.Nowhere(seenTheOverview: false);

        Assert.False(new KeptColumns(file).OverviewSeen);

        new KeptColumns(file).TheOverviewWasSeen(new Says());

        Assert.True(new KeptColumns(file).OverviewSeen);
    }

    /// <summary>
    /// The numbers are counted from the LISTING, and the listing arrives after the window does.
    ///
    /// <b>This is a fault this screen shipped for one build and it was found by opening the window
    /// rather than by reasoning</b> - six zeroes on a machine with 798 entries. The screen is asked
    /// for in the constructor, when the first reading is still out, so nothing but a second look
    /// after it lands can make the numbers true.
    /// </summary>
    /// <summary>
    /// Every question this screen asks is one the query language can answer.
    ///
    /// <b>Written 2026-08-26, and what it replaces is a claim rather than a mechanism.</b>
    /// <c>Overview.Line</c> throws when a query will not parse, with the argument that a screen
    /// quietly showing zero for a question it failed to ask is the shape rule 8 forbids. The
    /// argument is right and the loudness was not: the only caller is a property the window binds
    /// to, and WPF catches exceptions out of a binding source and turns them into a line in a trace
    /// nobody reads. On a running machine that throw is a blank screen.
    ///
    /// <b>So the guarantee moves to where the mistake can actually be made.</b> These queries are
    /// constants - nobody types them - so the moment one can be wrong is a build. Asked here
    /// directly, against an empty listing, because what is being checked is whether the language
    /// understands them and not what they select.
    ///
    /// <b>Other tests in this file reach the same code through the view model</b>, so a broken
    /// query would redden several of them. That is not the same thing: those would go red about
    /// counts, and this one says which query and why. Found by an outside review, which named the
    /// mechanism this file's own comment had wrong.
    /// </summary>
    [Fact]
    public void Every_question_this_screen_asks_parses()
    {
        // counted: true, because this test is about whether the questions parse and not about what
        // the screen shows before it has read anything - backlog 263 put that on its own test.
        var asked = ViewModels.Overview.Of([], counted: true);

        Assert.NotEmpty(asked);

        // Six today, and the number is here so that a line quietly disappearing is a red test
        // rather than one fewer thing being checked.
        Assert.Equal(6, asked.Count);

        Assert.All(asked, line => Assert.False(
            string.IsNullOrWhiteSpace(line.Query),
            "A line on this screen carries no question, so clicking its number would ask nothing."));
    }

    [Fact]
    public async Task The_numbers_are_counted_again_once_the_machine_has_been_read()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock())
        {
            Says = new Says { Elevated = true }
        };

        model.ShowingOverview = true;

        Assert.All(model.Overview, line => Assert.Equal(0, line.Count));

        var told = 0;

        model.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(MainViewModel.Overview))
            {
                told++;
            }
        };

        await model.LoadAsync();

        // THE ANNOUNCEMENT RATHER THAN THE VALUE, because a computed property answers correctly
        // whenever it is asked and that proves nothing about whether anybody was told to ask. The
        // same claim EntryRow's cells are guarded by, for the same reason.
        Assert.True(told > 0, "Nothing announced that the overview had changed, so a bound screen would still read zero.");

        Assert.Equal(1, model.Overview.Single(line =>
            line.Label.Contains("services running", StringComparison.Ordinal)).Count);
    }

    /// <summary>
    /// A window looking at a machine, showing the overview, with its first reading already done.
    ///
    /// No window is built for most of these, deliberately: what the screen says is a view model's
    /// answer and is checkable without an interface thread.
    /// </summary>
    private static async Task<MainViewModel> Looking(params ScmEntry[] machine)
    {
        var model = new MainViewModel(new LiveMachine(machine), new SteppedClock())
        {
            Says = new Says { Elevated = true }
        };

        await model.LoadAsync();

        model.ShowingOverview = true;

        return model;
    }

}
