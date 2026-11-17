using System.Windows;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The wavy line under a mistake in the search box - the owner's third choice of 2026-11-16: the
/// sentence under the box said what was wrong, and nothing in the box said where.
///
/// <b>Asked of a laid-out row, never of the model alone</b>, because the question is where the
/// characters are drawn - only the text box can answer it, and only once it has been laid out
/// (`docs/10` trap 19). The picture is kept in artifacts/gui so a red run comes with something to
/// look at.
/// </summary>
public sealed class MistakeMarkGuards
{
    private const string Typed = "name:sql stat:running";

    /// <summary>
    /// One mistake, one line, from the left edge of its first character to the right edge of its
    /// last, under the letters - and nothing under the member that reads.
    /// </summary>
    [Fact]
    public async Task A_mistake_is_marked_under_its_own_characters_and_nowhere_else()
    {
        var window = await Opened(Typed);

        Drawn.OnItsWindow(window.Search, 1000, 90).Save("search-row-mistake-marked-1000x90.png");

        var (lines, first, last) = WpfHost.On(() =>
        {
            var box = window.Search.QueryBox;
            var marks = window.Search.Marks;

            return (
                marks.Lines(),
                box.TranslatePoint(box.GetRectFromCharacterIndex(Typed.IndexOf("stat", StringComparison.Ordinal)).BottomLeft, marks),
                box.TranslatePoint(box.GetRectFromCharacterIndex(Typed.Length - 1, trailingEdge: true).BottomRight, marks));
        });

        var line = Assert.Single(lines);

        Assert.True(first.X > 0, $"the member that reads, name:sql, stands before the mistake, so the mark starts past 0 - it starts at {first.X}");
        Assert.Equal(first.X, line.From.X, 0.5);
        Assert.Equal(last.X, line.To.X, 0.5);
        Assert.True(line.From.Y >= first.Y, $"the mark at {line.From.Y} is over the letters, whose foot is at {first.Y}");

        WpfHost.On(window.Close);
    }

    /// <summary>
    /// While the box holds a text the model has not read yet - somebody typing inside the 400 ms the
    /// binding waits - the places describe other letters, so nothing is marked. A mark under the
    /// wrong characters would be worse than one that comes a moment late.
    /// </summary>
    [Fact]
    public async Task Nothing_is_marked_while_the_box_holds_a_text_the_marks_were_not_read_from()
    {
        var window = await Opened(Typed);

        Assert.NotEmpty(WpfHost.On(() => window.Search.Marks.Lines()));

        WpfHost.On(() => window.Search.QueryBox.Text = "x" + Typed);

        Assert.Empty(WpfHost.On(() => window.Search.Marks.Lines()));

        WpfHost.On(window.Close);
    }

    /// <summary>A query with nothing wrong in it carries no marks at all.</summary>
    [Fact]
    public async Task A_query_that_reads_is_not_marked()
    {
        var window = await Opened("name:sql status:running");

        Assert.Same(QueryMistakes.None, WpfHost.On(() => window.Search.Marks.Mistakes));
        Assert.Empty(WpfHost.On(() => window.Search.Marks.Lines()));

        WpfHost.On(window.Close);
    }

    private static async Task<MainWindow> Opened(string query)
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler"), Rows.Driver("disk")), new SteppedClock());

        await model.LoadAsync();

        var window = WpfHost.Window(model);
        WpfHost.Settled();

        WpfHost.On(() => model.QueryText = query);
        WpfHost.Settled();

        // Laid out by hand, as every guard of a window that is never shown has to (`docs/10` trap 19).
        WpfHost.On(() =>
        {
            window.Search.Measure(new Size(1000, 200));
            window.Search.Arrange(new Rect(0, 0, 1000, 200));
            window.Search.UpdateLayout();
        });

        return window;
    }
}
