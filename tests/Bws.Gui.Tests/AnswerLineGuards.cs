using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The line under the search box and the foot of the window as things on a window - UX-GUI-002 and
/// 009 of the audit of 2026-09-23.
///
/// <b>AnswerLineTests holds what the model decides. This holds what only a laid-out window can
/// answer</b>: that the box's red edge reaches a pixel through somebody else's template - GUI rule
/// 10, and the fault this project has paid for three times - that the line under the box is bound
/// to the model and coloured by what it says, and that at the foot of the window only the sentence
/// about rights is red.
/// </summary>
public sealed class AnswerLineGuards
{
    /// <summary>
    /// The search box wears the problem colour on its edge while its query has a mistake in it, and
    /// not before - counted off rendered pixels inside the box's own rectangle, the way the box of
    /// seconds is counted in WaitingBoxGuards, because the line under the box wears the same red.
    /// </summary>
    [Fact]
    public async Task The_search_box_wears_the_problem_colour_on_its_edge_while_its_query_is_wrong()
    {
        var (window, model) = await Opened();

        var fine = RedInsideTheBox(window, "fine");

        WpfHost.On(() => model.QueryText = "stat:runing");
        WpfHost.Settled();

        var wrong = RedInsideTheBox(window, "wrong");

        Assert.True(
            WpfHost.On(() => Validation.GetHasError(window.Search.Box)),
            "the binding on the search box never heard that the query is wrong - INotifyDataErrorInfo "
            + "is not reaching Validation.HasError, so no style can colour the edge");

        Assert.Equal(0, fine);
        Assert.True(
            wrong > 20,
            $"only {wrong} pixels of the problem colour were painted inside the search box while it "
            + "holds 'stat:runing'. The style sets BorderBrush and the library's template is not drawing it.");

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// The line under the box reads the model: the mistake in the problem colour, a reservation in
    /// the notice colour, and no room at all while the box is empty.
    /// </summary>
    [Fact]
    public async Task The_line_under_the_box_says_a_mistake_in_red_and_a_reservation_in_the_notice_colour()
    {
        var (window, model) = await Opened();

        Assert.Equal(Visibility.Collapsed, WpfHost.On(() => Line(window, "answerLine").Visibility));

        WpfHost.On(() => model.QueryText = "signed:no");
        WpfHost.Settled();

        var (reservation, reservationBrush, shown) = WpfHost.On(() =>
        {
            var line = Line(window, "answerLine");

            return (line.Text, Colour(line.Foreground), line.Visibility);
        });

        Assert.Equal(Visibility.Visible, shown);
        Assert.Equal(WpfHost.On(() => model.Says.Reservations), reservation);
        Assert.NotEqual(string.Empty, reservation);
        Assert.Equal(WpfHost.Declared("MeaningWarning"), reservationBrush);

        WpfHost.On(() => model.QueryText = "stat:runing");
        WpfHost.Settled();

        var (mistake, mistakeBrush) = WpfHost.On(() =>
        {
            var line = Line(window, "answerLine");

            return (line.Text, Colour(line.Foreground));
        });

        Assert.Equal(WpfHost.On(() => model.Says.QueryProblem), mistake);
        Assert.Equal(WpfHost.Declared("MeaningRejected"), mistakeBrush);

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// At the foot of the window only the sentence about rights is red - the rest of the line is in
    /// the notice colour. Until 2026-09-23 the whole line was red while the session had no rights,
    /// so the sentence about folded copies wore the colour of a refusal.
    /// </summary>
    [Fact]
    public async Task Only_the_sentence_about_rights_is_red_at_the_foot_of_the_window()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Template("CDPUserSvc"), Rows.Instance("CDPUserSvc_7b537"), Rows.Entry("Spooler")),
            new SteppedClock())
        {
            Says = new Says { Elevated = false }
        };

        await model.LoadAsync();

        var window = WpfHost.Window(model);
        WpfHost.Settled();

        var (rights, rightsBrush, lineBrush) = WpfHost.On(() =>
        {
            var line = Line(window, "noticeLine");
            var first = (Run)line.Inlines.FirstInline;

            return (first.Text, Colour(first.Foreground), Colour(line.Foreground));
        });

        Assert.StartsWith(Texts.Of("gui.status.notElevated"), rights, StringComparison.Ordinal);
        Assert.Equal(WpfHost.Declared("MeaningRejected"), rightsBrush);
        Assert.Equal(WpfHost.Declared("MeaningWarning"), lineBrush);
        Assert.Contains(Texts.Of("gui.instances.toggle"), WpfHost.On(() => model.Says.NoticeLine), StringComparison.Ordinal);

        WpfHost.On(window.Close);
    }

    private static async Task<(MainWindow Window, MainViewModel Model)> Opened()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler"), Rows.Driver("disk")), new SteppedClock());

        await model.LoadAsync();

        var window = WpfHost.Window(model);
        WpfHost.Settled();

        return (window, model);
    }

    private static TextBlock Line(MainWindow window, string automationId) =>
        Descendants(window).OfType<TextBlock>().Single(block => AutomationProperties.GetAutomationId(block) == automationId);

    private static Color? Colour(Brush brush) => (brush as SolidColorBrush)?.Color;

    /// <summary>
    /// The search row drawn at 1000 by 200, the picture left in artifacts/gui, and the problem
    /// colour counted in a strip down the LEFT EDGE of the field's frame.
    ///
    /// <b>The edge rather than the field, since 2026-09-25.</b> The frame draws the red edge now, not
    /// the text box's error template - the text box is the middle third of the field - and the short
    /// sentence at the field's right end wears the same red, so a count over the whole field would
    /// pass on the sentence alone. Nothing but the edge is red at the left: the magnifier is grey.
    /// </summary>
    private static int RedInsideTheBox(MainWindow window, string state)
    {
        var drawn = Drawn.Of(window.Search, 1000, 200);
        var frame = drawn.Around(window.Search.SearchField);

        Assert.True(frame.Width > 0 && frame.Height > 0, "the search field has no rectangle, so it was never laid out");

        drawn.Save($"search-row-{state}-1000x200.png");

        return drawn.Count(WpfHost.Declared("MeaningRejected"), new Int32Rect(frame.X, frame.Y, EdgeStrip, frame.Height));
    }

    /// <summary>How far in from the frame's left edge the red is counted - the edge and its antialiasing, short of the magnifier.</summary>
    private const int EdgeStrip = 4;

    /// <summary>
    /// THE SEARCH ROW KEEPS ITS HEIGHT WHATEVER THE ANSWER SAYS - owner, 2026-09-25: the window jumped
    /// while he typed. tools/gui-probe/typing-jump.ps1 measured it at 31 device pixels on the first
    /// character: the sentence about the query took a line of its own under the box. It stands in the
    /// field now, and the full text in a panel that measures to nothing, so a row that grows with what
    /// the answer says is that fault back. Asked with the longest thing it can say - a mistake, whose
    /// sentence lists every value a field accepts.
    /// </summary>
    [Fact]
    public async Task The_search_row_keeps_its_height_whatever_the_answer_says()
    {
        var (window, model) = await Opened();

        var empty = Height(window);

        WpfHost.On(() => model.QueryText = "stat:runing");
        WpfHost.Settled();

        Assert.NotEqual(string.Empty, WpfHost.On(() => model.Says.AnswerLine));
        Assert.Equal(empty, Height(window));

        WpfHost.On(window.Close);
    }

    private static double Height(MainWindow window) => WpfHost.On(() =>
    {
        window.Search.Measure(new Size(1000, double.PositiveInfinity));

        return window.Search.DesiredSize.Height;
    });

    /// <summary>
    /// The LOGICAL tree rather than the visual one, which is the difference between this file and
    /// the six others with a helper of this name: the window here is never shown, so no template
    /// has been applied and the visual tree under a UserControl is empty - the lines this looks for
    /// are declared in markup, and markup is the logical tree.
    /// </summary>
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;

            foreach (var below in Descendants(child))
            {
                yield return below;
            }
        }
    }
}
