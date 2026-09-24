using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The entry's name set apart in the plan panel - owner's decision, 2026-09-24. A sheet saying
/// "stop" six times read as one block, with the name each line was about lost in it.
///
/// <b>Half of this is about the cut and half about the window</b>, because either can be wrong on
/// its own: a sentence cut in the right place and drawn with a space too many between its runs, or
/// drawn perfectly with the weight on the wrong word.
/// </summary>
public sealed class PlanNameGuards
{
    [Fact]
    public void A_sentence_is_cut_where_the_name_stands()
    {
        var cut = NamedSentence.Around($"1. stop {NamedSentence.Slot} (asked for)", "Spooler");

        Assert.Equal(new NamedSentence("1. stop ", "Spooler", " (asked for)"), cut);
        Assert.Equal("1. stop Spooler (asked for)", cut.Text);
    }

    /// <summary>
    /// No mark, or two, and the sentence comes back whole with nothing set apart - every word
    /// still right, which is what it read like before names were set apart at all.
    /// </summary>
    [Fact]
    public void A_sentence_without_exactly_one_mark_comes_back_whole()
    {
        Assert.Equal(
            NamedSentence.Unnamed("What stopping these 2 entries would do"),
            NamedSentence.Around("What stopping these 2 entries would do", "Spooler"));

        Assert.Equal(
            NamedSentence.Unnamed("Spooler, and again Spooler"),
            NamedSentence.Around($"{NamedSentence.Slot}, and again {NamedSentence.Slot}", "Spooler"));
    }

    /// <summary>
    /// A service name is somebody else's input (`docs/09`), so one holding the mark itself must
    /// come through whole rather than be cut a second time.
    /// </summary>
    [Fact]
    public void A_name_holding_the_mark_itself_is_kept_whole()
    {
        var odd = "odd" + NamedSentence.Slot + "name";

        var cut = NamedSentence.Around($"1. stop {NamedSentence.Slot} (asked for)", odd);

        Assert.Equal(new NamedSentence("1. stop ", odd, " (asked for)"), cut);
    }

    /// <summary>
    /// On the window: the title and a step each draw the name heavier than the words beside it,
    /// and the words read exactly as the model says - three runs with markup whitespace between
    /// them would put a space on screen that no sentence has.
    /// </summary>
    [Fact]
    public async Task The_panel_draws_the_name_heavier_and_every_word_as_the_model_says()
    {
        var window = await PlanFixture.Ready();
        var model = WpfHost.On(() => (MainViewModel)window.DataContext);

        WpfHost.On(() =>
        {
            window.Entries.UnselectAll();
            window.Entries.SelectedItem = model.Rows.First(row => row.ServiceName == "Spooler");
        });
        WpfHost.Settled();

        Assert.True(await WpfHost.On(() => window.Preview(ActionKind.Stop)));

        var (title, step) = WpfHost.On(() =>
        {
            window.Left = -4000;
            window.Show();
            window.UpdateLayout();

            var container = (DependencyObject)window.PlanPanel.StepList.ItemContainerGenerator.ContainerFromIndex(0);

            return (Drawn(window.PlanPanel.Heading), Drawn(Descendants(container).OfType<TextBlock>().First()));
        });

        var heading = WpfHost.On(() => model.Planned.Heading.Text);
        var line = WpfHost.On(() => model.Planned.Steps[0].Text);

        Assert.Equal((heading, heading), (title.Text, title.Spoken));
        Assert.Equal(("Print Spooler", true), (title.Name, title.NameIsHeavier));

        Assert.Equal((line, line), (step.Text, step.Spoken));
        Assert.Equal(("Spooler", true), (step.Name, step.NameIsHeavier));

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// What a block of runs draws, and what it says to a screen reader - two answers, because a
    /// TextBlock built from runs answers Text with nothing and its name has to be bound (PlanView.xaml).
    /// </summary>
    private static (string Text, string Spoken, string Name, bool NameIsHeavier) Drawn(TextBlock block)
    {
        var runs = block.Inlines.OfType<Run>().ToList();

        return (
            WpfHost.Drawn(block),
            System.Windows.Automation.AutomationProperties.GetName(block),
            runs[1].Text,
            runs[1].FontWeight.ToOpenTypeWeight() > runs[0].FontWeight.ToOpenTypeWeight()
                && runs[1].FontWeight.ToOpenTypeWeight() > runs[2].FontWeight.ToOpenTypeWeight());
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var at = 0; at < VisualTreeHelper.GetChildrenCount(parent); at++)
        {
            var child = VisualTreeHelper.GetChild(parent, at);

            yield return child;

            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }
}
