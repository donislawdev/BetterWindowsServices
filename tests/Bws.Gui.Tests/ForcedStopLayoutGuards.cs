// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.IO;
using System.Windows;
using System.Windows.Media;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;
using static Bws.Gui.Tests.ForcedStopFixture;

namespace Bws.Gui.Tests;

/// <summary>
/// WHAT THE SHEET THAT ENDS A PROCESS COSTS ON A SCREEN, and what it looks like.
///
/// <b>Its own file since 2026-09-07, and the size ratchet is what asked</b> - ForcedStopViewGuards
/// went past five hundred lines the moment the drawing arrived. The seam is a subject rather than a
/// line count: that file asks whether the way out WORKS, and this one asks what it takes up and
/// whether its colours reach a pixel. The two need the same fixture and nothing else of each other,
/// which is why the fixture is a third file rather than a base class.
///
/// <b>Both questions here are ones the design left open and called estimates in its own words</b>,
/// so both are answered with numbers rather than an eye - and go on being answered, which a
/// measurement taken once does not.
/// </summary>
public sealed class ForcedStopLayoutGuards
{
    /// <summary>
    /// THE SHEET STILL FITS THE SMALLEST WINDOW THIS PRODUCT SUPPORTS, WITH THE BOX ON IT.
    ///
    /// <b>Backlog 278 is why this is measured rather than reasoned about:</b> at 1000 by 800 the
    /// sheet once ran past the bottom of the window and cut the main button in half. The design
    /// that asked for a confirmation box put its cost at "about seventy units" and said in as many
    /// words that the number was an estimate rather than a measurement, and that it had to be
    /// checked at that size before any of it was built.
    ///
    /// <b>Measured off a real layout pass rather than off a screenshot</b>, which answers the same
    /// question with a number instead of an eye - and keeps answering it, which a picture taken
    /// once does not.
    /// </summary>
    [Fact]
    public async Task The_sheet_with_the_box_on_it_still_fits_a_window_a_thousand_by_eight_hundred()
    {
        var window = await ForcedSheet();
        var panel = Sheeted(window);

        var withBox = Measured(window, panel, PlanWarningKind.TerminationTakesWithIt);
        var without = Measured(window, panel);

        // A LAYOUT THAT NEVER HAPPENED WOULD MEASURE ZERO AND PASS, so the floor is asserted as
        // well as the ceiling. This is the shape that turns a guard into one agreeing with nothing.
        Assert.True(without > 100, $"the sheet measured {without} high, which is not a laid out sheet");

        Assert.True(
            withBox <= 800,
            $"the sheet wants {withBox} points with the confirmation box on it, and the smallest "
            + "window this product supports is 800 tall - so the foot of it, the box and the button "
            + "that ends a process, is off the bottom of the screen. Backlog 278, a second time.");

        // WHAT THE BOX COSTS, WHICH IS THE NUMBER THE DESIGN CALLED AN ESTIMATE. It put the label
        // and the field at "about +70 units" and said in as many words that the figure had to be
        // measured before any of it was built. Measured here rather than looked at, so it keeps
        // being measured: a ceiling on the difference catches a box that quietly grows a second
        // line where a ceiling on the total would still have room.
        Assert.True(
            withBox - without <= 130,
            $"the confirmation box adds {withBox - without} points to the footer, against the 130 "
            + "this guard allows. MEASURED AT 113 ON 2026-09-07 - the design that asked for the box "
            + "put it at \"about 70\" and said in as many words that the figure was an estimate, so "
            + "it was 62 per cent low. The sheet holding it wanted 519 points of the 800 a smallest "
            + "window has, which is why 113 is affordable and a second line of it might not be.");
    }

    /// <summary>
    /// THE SHEET THAT ENDS A PROCESS, DRAWN, WITH THE RED OF ITS BUTTON REACHING REAL PIXELS - and
    /// left behind as a picture, because the owner of this project reads screens rather than code.
    ///
    /// <b>Rendered rather than screenshotted, and the difference is what makes it possible at
    /// all.</b> This sheet is reachable in the running program only from a stop that failed, and
    /// producing one on the machine this suite runs on would mean asking the manager to stop
    /// something. So the real controls, the real theme and the real layout are drawn to a bitmap
    /// instead - everything a screenshot would show except the window frame around it.
    ///
    /// <b>THE ASSERTION IS THAT A COLOUR ARRIVED, WHICH IS NOT THE SAME QUESTION AS WHETHER A
    /// BINDING RESOLVED.</b> `docs/11` records this project losing three slices to a colour that
    /// was set correctly and never reached a pixel, and a fourth to prose drawn in system black on
    /// a dark panel with every test green. A rendered sheet with none of the destructive fill in it
    /// is a sheet whose most dangerous button is invisible, and nothing else in this assembly
    /// would say so.
    ///
    /// <b>IT IS DRAWN TWICE, AND THE PAIR IS THE ASSERTION.</b> Before the name is typed the fill
    /// is not there at all - the disabled trigger on that style paints the face transparent - and
    /// after it is typed it is. So the two counts together say something neither says alone: that
    /// the confirmation gate is visible rather than merely enforced. A gate a person cannot SEE is
    /// one they read as a broken button.
    ///
    /// <b>What it deliberately does NOT judge is whether the result looks good.</b> That is the
    /// owner's, and both pictures are written out for exactly that.
    /// </summary>
    [Fact]
    public async Task The_sheet_that_ends_a_process_is_drawn_and_the_gate_is_visible()
    {
        var window = await ForcedSheet();

        var waiting = Painted(window, "plan-sheet-force-waiting-1000x800.png");

        WpfHost.On(() => window.PlanPanel.Confirm.Text = WpfHost.On(() => Sheeted(window).TypeTheName));
        WpfHost.Settled();

        var armed = Painted(window, "plan-sheet-force-armed-1000x800.png");

        // A BUTTON RATHER THAN A STRAY PIXEL. The face of it is roughly 130 by 30 at this scale, so
        // a thousand is well under what a drawn button paints and far above what antialiasing on
        // anything else could produce.
        Assert.True(
            armed > 1000,
            $"only {armed} pixels of the destructive fill were painted with the name typed in, so "
            + "the button that ends a process is not on the drawn sheet. A colour that is set "
            + "correctly and never reaches a pixel is a fault this project has paid for three times.");

        Assert.True(
            waiting == 0,
            $"{waiting} pixels of the destructive fill were painted BEFORE the name was typed, so a "
            + "button that cannot be pressed is wearing the colour that means pressing it changes "
            + "your machine. The gate has to be visible, not only enforced.");

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// Draws the sheet at the smallest size this product supports, leaves the picture behind, and
    /// answers how much of the destructive fill reached a pixel.
    /// </summary>
    private static int Painted(Bws.Gui.MainWindow window, string named)
    {
        var drawn = Drawn.Of(window.PlanPanel, 1000, 800);
        var file = drawn.Save(named);

        Assert.True(new FileInfo(file).Length > 0, file);

        // #C43C2E, the one fill in this window that means "pressing this changes your machine".
        return drawn.Count(Color.FromRgb(0xC4, 0x3C, 0x2E));
    }

    /// <summary>
    /// How tall the sheet wants to be inside the smallest window this product supports, holding a
    /// plan that ends a process and whatever warnings the case under test needs.
    ///
    /// <b>THE PANEL IS MEASURED RATHER THAN THE WINDOW, and that is a fact about the harness rather
    /// than a softer question.</b> A Window that is never shown builds no content host, so
    /// measuring IT lays nothing out and every rectangle inside comes back zero - which a guard
    /// reading only a ceiling would have called a pass. The panel covers the whole window, so
    /// handing it 1000 by 800 asks exactly what the window would ask it.
    /// </summary>
    private static double Measured(
        Bws.Gui.MainWindow window, Planned panel, params PlanWarningKind[] warnings)
    {
        WpfHost.On(() => panel.Show(Forcing(warnings), "Print Spooler"));
        WpfHost.Settled();

        return WpfHost.On(() =>
        {
            window.PlanPanel.Measure(new Size(1000, 800));
            window.PlanPanel.Arrange(new Rect(0, 0, 1000, 800));
            window.PlanPanel.UpdateLayout();

            return window.PlanPanel.TheSheet.DesiredSize.Height;
        });
    }

    /// <summary>
    /// The same plan that ends a process twice over, differing only in whether it asks for typing.
    ///
    /// <b>Built by hand rather than from the fixture machine, and the pair is why.</b> Every entry
    /// that machine holds answers with the same process number, so a plan built against it always
    /// takes neighbours with it - which is the case that shows the box and leaves no way to measure
    /// the same sheet without it. The difference between the two is the whole question.
    /// </summary>
    private static BulkPlan Forcing(params PlanWarningKind[] warnings) => new()
    {
        Action = new BulkAction(ActionKind.ForceStop, ["Spooler"]),
        Plans =
        [
            new OperationPlan
            {
                Action = new ServiceAction(ActionKind.ForceStop, "Spooler"),
                Steps =
                [
                    new PlanStep("Spooler", "Print Spooler", StepOperation.Stop, StepReason.Requested),
                    new PlanStep(
                        "Spooler", "Print Spooler", StepOperation.Terminate,
                        StepReason.Escalation, ProcessId: 4812)
                ],
                Warnings = [.. warnings.Select(kind => new PlanWarning(kind, "Spooler", ["W32Time"]))],
                Problems = []
            }
        ],
        Problems = []
    };
}
