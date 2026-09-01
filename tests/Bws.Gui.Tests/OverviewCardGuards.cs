using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Bws.Core;
using Bws.Gui.ViewModels;
using Xunit;

namespace Bws.Gui.Tests;

/// <summary>
/// HOW THE FIRST SCREEN IS DRAWN, as opposed to what it counts - `G`.
///
/// <b>Split out of OverviewGuards.cs on 2026-09-01, and the seam is by SUBJECT rather than by line
/// count.</b> That file stood at 509 lines and the size ratchet refused a third long test file,
/// which is the second time in one day this screen's guards asked for a seam - ScreenWithNoListGuards
/// came out the same way that morning. What was left when the counting tests stayed behind is a
/// coherent subject on its own: whether the numbers and their words end up where somebody can read
/// them.
///
/// <b>WHY THERE IS ANYTHING TO GUARD HERE AT ALL.</b> The owner rejected this screen three times,
/// and the fault the third rejection was really about could not be seen by any test in this project
/// - it was the composition. What CAN be checked is the small number of arrangement facts that,
/// when they went wrong, produced "113services running" on a shipped build for weeks. Those are
/// here.
/// </summary>
public sealed class OverviewCardGuards
{
    /// <summary>
    /// The number and the words it counts do not touch, on both templates this screen has.
    ///
    /// <b>FOUND BY LOOKING AT THE SCREEN ON 2026-09-01, WHICH IS WHAT THAT STEP OF THE PACKAGE WAS
    /// FOR.</b> The first screen anybody sees read "113services running", "1set to start
    /// automatically and did not come up", "0orphans" - every line of it, since the day it shipped.
    /// Every guard in this project passed over it, because a missing space is not a missing element
    /// and nothing here had ever rendered this screen for a person to look at.
    ///
    /// <b>The cause was a margin named for what it separates rather than for which side it is on.</b>
    /// The label carried MarginBetweenControls, which is 0,0,8,0 - a gap on its RIGHT, where nothing
    /// stands. So it pushed away from what came after it and left nothing between itself and the
    /// number in front of it.
    ///
    /// <b>REWRITTEN THE SAME DAY, WHEN THE SCREEN BECAME CARDS, AND THE REWRITE IS THE INTERESTING
    /// PART.</b> This walked one template called OverviewRow and cast its way down to a grid. That
    /// template no longer exists, so the guard THREW rather than failing - a walk describing a shape
    /// it can no longer see. The fault it was written for is still possible, so the check follows the
    /// shape rather than retiring with it, and it now has to answer for BOTH templates: a headline
    /// stacks its number above its words, a qualification still stands them side by side.
    ///
    /// <b>Asked of the templates rather than of a rendered window</b>, because that is where the
    /// values live and a rendered check would need a desktop this test process does not have.
    /// </summary>
    [Fact]
    public void The_number_does_not_touch_the_words_it_counts()
    {
        // FROM THE VIEW'S OWN RESOURCES RATHER THAN FROM THE MERGED THEME, and the first version of
        // this asked the theme and got a null. Both templates are declared inside OverviewView.xaml,
        // where they belong - they are about one screen rather than about the window - so the only
        // way to reach them is to build the screen that owns them.
        // THE THICKNESSES COME BACK RATHER THAN THE TEXTBLOCKS, and an early version returned the
        // controls and read their Margin here. A WPF element belongs to the thread that made it, so
        // that read threw about thread access rather than failing on the value - which is the
        // arrangement WpfHost exists to make visible.
        var (beside, under, stacked) = WpfHost.On(() =>
        {
            _ = WpfHost.Resources;

            var view = new OverviewView();

            // A qualification: number in column zero, words in column one, so the gap that matters
            // is the one on the number's RIGHT - the original fault exactly.
            var small = (DataTemplate)view.Resources["OverviewQualification"];
            var grid = (Grid)((Button)small.LoadContent()).Content;
            var gapBeside = ((TextBlock)grid.Children[0]).Margin;

            // A headline: number over words. Running together horizontally is impossible here, and
            // the check that replaces it is that they are genuinely on two lines with air between.
            var tile = (DataTemplate)view.Resources["OverviewTile"];
            var card = (StackPanel)((Border)tile.LoadContent()).Child;
            var pair = (StackPanel)((Button)card.Children[0]).Content;
            var gapUnder = ((TextBlock)pair.Children[1]).Margin;

            return (gapBeside, gapUnder, pair.Orientation);
        });

        Assert.True(
            beside.Right >= 8,
            $"A qualification's number has {beside.Right} after it, so it runs into the word beside it.");

        // THE EVEN CLAIM, and without it the one below passes on a card that puts the number and its
        // words side by side with no gap at all - which is the original fault, arriving by a
        // different route.
        Assert.Equal(Orientation.Vertical, stacked);

        Assert.True(
            under.Top > 0,
            $"A headline's words sit {under.Top} under the number, so the two run together.");
    }

    /// <summary>
    /// Every qualification is drawn inside the number it takes from, and the grouping is READ from
    /// the order rather than decided a second time.
    ///
    /// <b>Added 2026-09-01 with the cards.</b> Until then a qualification was tied to its number by
    /// sitting under it, being smaller, and carrying a rule down its left edge - three weak signals
    /// where containment is one strong one. What the panel binds to is now a grouped projection of
    /// the flat list every other test here reads, so the two cannot come apart.
    ///
    /// <b>This is the property that makes the projection safe to have at all.</b> A second decision
    /// about which number owns which sentence would be a second place to look and a second thing to
    /// get wrong - so the only input is the authored order, and this says so.
    /// </summary>
    [Fact]
    public async Task Each_qualification_belongs_to_the_number_written_above_it()
    {
        var model = await Looking(Rows.Entry("Spooler"));

        var findings = model.OverviewFindings;

        // Three findings out of six lines, which is the whole point of the grouping.
        Assert.Equal(3, findings.Count);
        Assert.Equal(6, findings.Sum(finding => 1 + finding.Qualifications.Count));

        var notUp = findings.Single(finding =>
            finding.Headline.Label.Contains("did not come up", StringComparison.Ordinal));

        Assert.Equal(2, notUp.Qualifications.Count);
        Assert.True(notUp.HasQualifications);

        Assert.Contains(notUp.Qualifications, line =>
            line.Label.Contains("per-user templates", StringComparison.Ordinal));
        Assert.Contains(notUp.Qualifications, line =>
            line.Label.Contains("waiting for a trigger", StringComparison.Ordinal));

        // THE EVEN CLAIM, and without it this passes on a projection that hangs every qualification
        // off every headline. The first number stands on its own and its card must draw nothing
        // under it - a heading over an empty space states an untruth, complaint 12.2 of `docs/11`.
        var running = findings.Single(finding =>
            finding.Headline.Label.Contains("services running", StringComparison.Ordinal));

        Assert.Empty(running.Qualifications);
        Assert.False(running.HasQualifications);
    }

    /// <summary>
    /// A qualification with no number in front of it is refused rather than drawn as a finding of
    /// its own.
    ///
    /// <b>The throw itself is a tripwire and this test is the guarantee</b>, which is the same
    /// division <c>Overview.Line</c> already carries and for the same measured reason: WPF catches
    /// exceptions out of a binding source and turns them into a trace nobody reads, so on a running
    /// window the loud failure would be a blank screen. It can only be reached by editing
    /// <c>Overview.Of</c> wrongly, which is a build rather than a session - so a build is where it
    /// is asked.
    /// </summary>
    [Fact]
    public void A_qualification_with_nothing_to_belong_to_is_refused()
    {
        var orphaned = ViewModels.Overview.Of([], counted: true)
            .Where(line => !line.Leading)
            .ToList();

        // The specimen has to be real or this test proves nothing about the shape it guards.
        Assert.NotEmpty(orphaned);

        Assert.Throws<InvalidOperationException>(() => ViewModels.Overview.Findings(orphaned));

        // AND THE EVEN CLAIM: the same lines WITH their headline in front group without complaint,
        // so this is about the missing number rather than about the lines themselves.
        Assert.NotEmpty(ViewModels.Overview.Findings(ViewModels.Overview.Of([], counted: true)));
    }


    /// <summary>
    /// A window looking at a machine, showing the overview, with its first reading already done.
    ///
    /// A copy of the helper in OverviewGuards rather than a shared base class, which would be a seam
    /// nobody asked for between two files that only happen to set up the same four lines.
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
