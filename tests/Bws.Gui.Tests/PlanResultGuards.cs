using System.Windows;
using System.Windows.Controls;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;
using static Bws.Gui.Tests.PlanFixture;

namespace Bws.Gui.Tests;

/// <summary>
/// What the plan sheet looks like once a run has ended, and the row of seconds under a long name -
/// the owner's two reports of 2026-11-16: the finished sheet did not read as done, and "Restart" with
/// a long display name cut the row to "Wait up to 60 seconds without pro".
/// </summary>
public sealed class PlanResultGuards
{
    /// <summary>
    /// The sentence names where the entry is now, for every kind a plan can be - "Done. Spooler is
    /// running." rather than "the entry is where you asked". Asserted against the language file, so
    /// rewording a sentence does not redden this - what is held is that each kind picks its own.
    /// </summary>
    [Theory]
    [InlineData(ActionKind.Stop, false, "gui.plan.notice.done.stopped.one")]
    [InlineData(ActionKind.ForceStop, false, "gui.plan.notice.done.stopped.one")]
    [InlineData(ActionKind.Start, false, "gui.plan.notice.done.running.one")]
    [InlineData(ActionKind.Restart, false, "gui.plan.notice.done.running.one")]
    [InlineData(ActionKind.ForceRestart, false, "gui.plan.notice.done.running.one")]
    [InlineData(ActionKind.SetStartType, false, "gui.plan.notice.done.set.one")]
    [InlineData(ActionKind.SetStartType, true, "gui.plan.notice.done.setAndStopped.one")]
    public void A_finished_run_of_one_entry_says_where_that_entry_is_now(ActionKind kind, bool alsoStop, string key)
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Of(kind, alsoStop, "Spooler"));
        panel.Finished(Ran(panel));

        var expected = kind == ActionKind.SetStartType
            ? Texts.Of(key, "Spooler", CellFaces.SettingLabel(StartSetting.Disabled))
            : Texts.Of(key, "Spooler");

        Assert.Equal(expected, panel.Notice);
        Assert.Equal(PlanOutcome.Arrived, panel.Outcome);
    }

    /// <summary>
    /// A selection counts its entries where one entry would be named, and a refusal anywhere turns
    /// the outcome - the colour beside the sentence and the sentence come from one answer.
    /// </summary>
    [Fact]
    public void A_selection_counts_its_entries_and_a_refusal_turns_the_outcome()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Of(ActionKind.Start, false, "Spooler", "W32Time"));
        Assert.Equal(PlanOutcome.None, panel.Outcome);
        Assert.False(panel.HasResult);

        panel.Finished(Ran(panel));
        Assert.Equal(Texts.Of("gui.plan.notice.done.running.many", 2), panel.Notice);
        Assert.Equal(PlanOutcome.Arrived, panel.Outcome);
        Assert.True(panel.HasResult);

        panel.Show(Of(ActionKind.Start, false, "Spooler", "W32Time"));
        panel.Finished(Ran(panel, refusing: "W32Time"));
        Assert.Equal(Texts.Of("gui.plan.notice.partly.many", 1, 2), panel.Notice);
        Assert.Equal(PlanOutcome.FellShort, panel.Outcome);
    }

    /// <summary>
    /// The box of seconds stays on the sheet during a run, greyed, so the buttons under it do not
    /// move when Interrupt appears - and goes once there is a result.
    /// </summary>
    [Fact]
    public void The_box_of_seconds_stays_greyed_through_a_run_and_goes_with_the_result()
    {
        var panel = new Planned { Elevated = true };

        panel.Show(Of(ActionKind.Restart, false, "Spooler"));
        Assert.True(panel.OffersWaiting);
        Assert.True(panel.Waits);

        panel.Starting();
        Assert.True(panel.OffersWaiting);
        Assert.False(panel.Waits);

        panel.Finished(Ran(panel));
        Assert.False(panel.OffersWaiting);
        Assert.False(panel.Waits);
    }

    /// <summary>
    /// On the window: the button that ran the plan goes, the one that closes the sheet takes its
    /// place and the keyboard, and the result block wears the rule of its outcome.
    ///
    /// <b>The rule's brush is read off the laid-out Border</b>, so a trigger whose enum value never
    /// matched - which would leave a finished sheet looking exactly like an unfinished one - reddens
    /// this rather than passing on the model alone.
    /// </summary>
    [Fact]
    public async Task A_finished_sheet_closes_rather_than_carries_out_and_its_result_wears_the_outcome()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.Settled();

        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.PlanPanel.CarryOut.Visibility));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.Done.Visibility));
        Assert.Null(WpfHost.On(() => Rule(window)));

        WpfHost.On(() => panel.Finished(Ran(panel)));
        WpfHost.Settled();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.CarryOut.Visibility));
        Assert.Equal(Visibility.Visible, WpfHost.On(() => window.PlanPanel.Done.Visibility));
        Assert.Same(WpfHost.On(() => window.PlanPanel.Done), WpfHost.On(() => window.PlanPanel.WayIn));
        Assert.Same(WpfHost.Resources["MeaningArrived"], WpfHost.On(() => Rule(window)));
        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => window.PlanPanel.Waiting.Visibility));

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));
        WpfHost.On(() => panel.Finished(Ran(panel, refusing: "Spooler")));
        WpfHost.Settled();

        Assert.Same(WpfHost.Resources["MeaningWarning"], WpfHost.On(() => Rule(window)));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The words of the row of seconds never lie under the button that carries the plan out, and end
    /// inside the sheet - beside a button naming a 116-character display name, the longest measured
    /// on the owner's machine (Values.xaml, WidthCarryOutMost).
    ///
    /// <b>MEASURED 2026-11-16 ON THE LAYOUT IT REPLACED, and the measurement is why this asks about
    /// rectangles rather than widths.</b> The row and the button shared a row of the grid. The row
    /// needed 256 and its cell had 230 beside the capped button, and WPF did not shrink it: it was
    /// laid out from 15 to 271 while the button began at 245, and the button, drawn later, covered
    /// the end of "without progress". The width the row was given was the width it asked for, so a
    /// test comparing those two passed on the broken layout - measured, that is the first version
    /// of this test.
    /// </summary>
    [Fact]
    public async Task The_row_of_seconds_never_lies_under_a_button_naming_a_long_entry()
    {
        var window = await Ready();
        var panel = WpfHost.On(() => (Planned)window.PlanPanel.DataContext);
        var longest = string.Concat(Enumerable.Repeat("Windows Management Instrumentation ", 4)).Trim()[..116];

        WpfHost.On(() => panel.Show(Of(ActionKind.Restart, false, "Winmgmt"), shownAs: longest));
        WpfHost.Settled();

        var (row, button, footerRight) = WpfHost.On(() =>
        {
            // Laid out by hand at the window's least size, the way WaitingBoxGuards does it - a
            // window built here is never shown, so nothing lays it out on its own (`docs/10` trap 19).
            window.PlanPanel.Measure(new Size(1000, 800));
            window.PlanPanel.Arrange(new Rect(0, 0, 1000, 800));
            window.PlanPanel.UpdateLayout();

            var sheet = window.PlanPanel.TheSheet;
            var footer = window.PlanPanel.Footer;
            var waiting = window.PlanPanel.Waiting;
            var carry = window.PlanPanel.CarryOut;

            // The words as wide as they ask to be, wherever they start - so a row squeezed into a
            // narrower cell is drawn as long as the text it really paints.
            waiting.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            return (
                new Rect(waiting.TranslatePoint(default, sheet), new Size(waiting.DesiredSize.Width, waiting.ActualHeight)),
                new Rect(carry.TranslatePoint(default, sheet), new Size(carry.ActualWidth, carry.ActualHeight)),
                footer.TranslatePoint(new Point(footer.ActualWidth, 0), sheet).X);
        });

        Assert.True(row.Width > 100, $"the row asked for {row.Width}, which is not a laid out row of words");
        Assert.True(button.Width > 100, $"the button is {button.Width} wide, which is not a button naming a long entry");

        var overlap = Rect.Intersect(row, button);

        Assert.True(
            overlap.IsEmpty || overlap.Width * overlap.Height == 0,
            $"the words of the row of seconds ({row}) lie under the button ({button}) - the end of the sentence is covered");
        Assert.True(
            row.Right <= footerRight + 0.5,
            $"the row of seconds ends at {row.Right}, past the foot of the sheet at {footerRight}");

        WpfHost.On(window.Close);
    }

    /// <summary>The brush of the rule beside the sentence at the top of the sheet, or nothing.</summary>
    private static object? Rule(MainWindow window) =>
        window.PlanPanel.Notice.Parent is FrameworkElement { Parent: Border block } && block.BorderThickness.Left > 0
            ? block.BorderBrush
            : null;

    /// <summary>A plan of one kind over the named entries, one step each, made by hand.</summary>
    private static BulkPlan Of(ActionKind kind, bool alsoStop, params string[] names)
    {
        var to = kind == ActionKind.SetStartType ? StartSetting.Disabled : (StartSetting?)null;
        var operation = kind switch
        {
            ActionKind.Stop or ActionKind.ForceStop => StepOperation.Stop,
            ActionKind.SetStartType => StepOperation.SetStartType,
            _ => StepOperation.Start
        };

        return new BulkPlan
        {
            Action = new BulkAction(kind, names, To: to, AlsoStop: alsoStop),
            Plans =
            [
                .. names.Select(name => new OperationPlan
                {
                    Action = new ServiceAction(kind, name, To: to, AlsoStop: alsoStop),
                    Steps = [new PlanStep(name, name, operation, StepReason.Requested)],
                    Warnings = [],
                    Problems = []
                })
            ],
            Problems = []
        };
    }
}
