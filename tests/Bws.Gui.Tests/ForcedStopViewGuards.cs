// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;
using static Bws.Gui.Tests.ForcedStopFixture;
using static Bws.Gui.Tests.PlanFixture;

namespace Bws.Gui.Tests;

/// <summary>
/// The way out from under a failure as a thing on a window, and the sheet it opens.
///
/// <b>Everything the panel DECIDES is covered without a window in <see cref="ForcedStopGuards"/>,
/// so what is left here is the one question that cover cannot ask: whether any of it reaches a
/// screen.</b> This project has been caught four times by markup that binds correctly and paints
/// something else, and this slice moved the whole foot of the sheet into a file of its own - which
/// is exactly the shape that produces a green build and a panel with a button missing.
///
/// <b>NOTHING HERE ENDS A PROCESS.</b> The run is handed to the window through the seam that exists
/// for it, so what the window carries out is a record built by hand rather than a plan against a
/// manager. Whether a terminate step really ends anything is a question for a machine that can be
/// thrown away, and it is not asked here.
/// </summary>
public sealed class ForcedStopViewGuards
{
    /// <summary>
    /// The offer reaches the screen under the failure it belongs to, and the button really is
    /// there rather than the model merely being right about it.
    /// </summary>
    [Fact]
    public async Task The_way_out_reaches_the_screen_under_a_stop_that_gave_up()
    {
        var window = await Ready(carriedOutBy: GaveUp);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.True(await WpfHost.On(window.CarryOut));
        WpfHost.Settled();

        var offers = WpfHost.On(() => window.PlanPanel.OfferLines);

        Assert.NotEmpty(offers);
        Assert.All(offers, offer => Assert.Equal(Bws.Gui.Texts.Of("gui.plan.offer.forceStop"), offer));

        // AND THE TEMPLATE REALLY DRAWS A BUTTON THAT CARRIES ITS OWN FAILURE, which is the half a
        // model can be perfectly right about while the markup delivers nothing.
        var failure = Sheeted(window).Failures.First(one => one.HasOffer);
        var button = OfferedBy(failure);

        Assert.Same(failure, button.Tag);
        Assert.Equal(failure.Label, button.Content);
        Assert.Equal(Visibility.Visible, button.Showing);

        // AND IT IS NOT DRAWN FOR A FAILURE THAT OFFERS NOTHING, which is the state that has to be
        // right when a binding fails: most failures offer none.
        Assert.Equal(
            Visibility.Collapsed,
            OfferedBy(new PlanFailure("nothing to escalate", null)).Showing);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// Pressing it closes the sheet reporting the failure and opens a new one holding a plan that
    /// ends a process.
    ///
    /// <b>The design's first named collision, arriving on a window.</b> A sheet that has been
    /// carried out is a record, and growing a second question onto it would leave a report and a
    /// proposal on one surface under one button.
    /// </summary>
    [Fact]
    public async Task Taking_the_offer_opens_a_new_sheet_holding_a_plan_that_ends_a_process()
    {
        var window = await Ready(carriedOutBy: GaveUp);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.True(await WpfHost.On(window.CarryOut));
        WpfHost.Settled();

        var offered = WpfHost.On(() => Sheeted(window).Failures.First(one => one.HasOffer));

        Assert.True(await WpfHost.On(() => window.Force(offered)));
        WpfHost.Settled();

        var plan = WpfHost.On(() => Sheeted(window).Plan!);

        Assert.Equal(ActionKind.ForceStop, plan.Action.Kind);

        Assert.Contains(
            plan.Plans.SelectMany(one => one.Steps),
            step => step.Operation == StepOperation.Terminate);

        // THE REASON CAME ACROSS, which is the only thing left saying what happened before - the
        // sheet that knew it is gone by now.
        Assert.StartsWith(
            Bws.Gui.Texts.Of("gui.plan.because.timedOut", 60),
            WpfHost.On(() => window.PlanPanel.Notice.Text),
            StringComparison.Ordinal);

        // AND NOTHING HAS BEEN CARRIED OUT. The new sheet is a question, so it says so.
        Assert.Contains(
            Bws.Gui.Texts.Of("gui.plan.notice.notYet"),
            WpfHost.On(() => window.PlanPanel.Notice.Text),
            StringComparison.Ordinal);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The confirmation box reaches the screen, and the button that ends a process stays dead until
    /// the name is in it.
    ///
    /// <b>Every entry in this fixture answers with the same process number, so the forcing plan
    /// really does take neighbours with it</b> - which is the case that asks for typing, and the
    /// reason this fixture can reach it at all.
    /// </summary>
    [Fact]
    public async Task The_box_reaches_the_screen_and_the_button_waits_for_the_name()
    {
        var window = await ForcedSheet();

        Assert.True(WpfHost.On(() => window.PlanPanel.ConfirmShown));
        Assert.False(WpfHost.On(() => window.PlanPanel.CarryOut.IsEnabled));

        // TYPED INTO THE CONTROL RATHER THAN SET ON THE MODEL, because the binding is the half
        // being asked about: UpdateSourceTrigger on that box is the whole mechanism, and without
        // it the name would reach the model only when focus left the box.
        WpfHost.On(() => window.PlanPanel.Confirm.Text = WpfHost.On(() => Sheeted(window).TypeTheName));
        WpfHost.Settled();

        Assert.True(WpfHost.On(() => window.PlanPanel.CarryOut.IsEnabled));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The button on that sheet names the process, which is what pressing it does.
    /// </summary>
    [Fact]
    public async Task The_button_on_the_screen_names_the_process_it_would_end()
    {
        var window = await ForcedSheet();

        var said = WpfHost.On(() => window.PlanPanel.CarryOut.Content as string);

        Assert.NotNull(said);
        Assert.StartsWith("End process ", said, StringComparison.Ordinal);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// ENTER HAS NO WAY TO END A PROCESS, which is the design's fifth collision and the one worth
    /// the most.
    ///
    /// <b>What is asserted is the DECISION rather than where focus landed, and that is a fact about
    /// the harness rather than a softer test.</b> A window built for a test is never put on a
    /// screen, so Focus answers false for everything in it - a guard reading the real focus would
    /// pass by agreeing with nothing. The decision is the half that would be wrong.
    /// </summary>
    [Fact]
    public async Task The_keyboard_is_never_handed_to_the_button_that_ends_a_process()
    {
        var window = await ForcedSheet();

        var wayIn = WpfHost.On(() => window.PlanPanel.WayIn);

        Assert.NotSame(WpfHost.On(() => (Control)window.PlanPanel.CarryOut), wayIn);
        Assert.Same(WpfHost.On(() => (Control)window.PlanPanel.Confirm), wayIn);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// With no box to type into, the keyboard goes to the way OUT rather than to the way on.
    /// </summary>
    [Fact]
    public async Task With_nothing_to_type_the_keyboard_goes_to_the_way_out()
    {
        var window = await Ready();

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.False(WpfHost.On(() => window.PlanPanel.ConfirmShown));

        Assert.NotSame(
            WpfHost.On(() => (Control)window.PlanPanel.CarryOut),
            WpfHost.On(() => window.PlanPanel.WayIn));

        WpfHost.On(window.Close);
    }
}
