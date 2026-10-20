using System.ComponentModel;
using System.Windows.Data;
using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// Which rows of a comparison the table shows - "Show", owner's decision W2-b of 2026-10-15. Over one
/// comparison with every kind of row in it: an entry added, an entry the side without rights could
/// not see, and a startup type that moved.
/// </summary>
public sealed class DiffFilterTests
{
    [Fact]
    public void Each_choice_carries_its_count_and_only_choices_that_narrow_anything_are_offered()
    {
        // No "Not compared" - nothing here is - and no "Status", which did not move.
        var filter = Mixed().Filter;

        Assert.Equal(
            [
                Counted("gui.diff.show.all", 3),
                Counted("gui.diff.show.differences", 2),
                Texts.Of("gui.diff.show.counted", DiffLines.Label("startType"), 1),
                Counted("gui.diff.show.uncertain", 1)
            ],
            filter.Choices.Select(choice => choice.Label));
        Assert.True(filter.Choices[0].IsChosen);
        Assert.Equal(string.Empty, filter.Showing);
    }

    [Fact]
    public void A_choice_narrows_the_rows_in_place_and_says_how_many_are_hidden()
    {
        var sheet = Mixed();
        var rows = sheet.Filter.Rows;

        sheet.Filter.Choices[1].IsChosen = true;

        Assert.Same(rows, sheet.Filter.Rows);
        Assert.Equal(["Added", "Moved"], rows.Select(row => row.ServiceName));
        Assert.Equal(Texts.Of("gui.diff.showing.many", 2, 3), sheet.Filter.Showing);
        Assert.Equal(Texts.Of("gui.diff.show", Texts.Of("gui.diff.show.differences")), sheet.Filter.Button);
        Assert.False(sheet.Filter.Choices[0].IsChosen);

        // And back: every row, in the comparison's own order, and nothing said about hiding.
        sheet.Filter.Choices[0].IsChosen = true;

        Assert.Equal(sheet.Lines, rows);
        Assert.Equal(string.Empty, sheet.Filter.Showing);
    }

    [Fact]
    public void A_choice_tells_the_table_once_rather_than_once_a_row()
    {
        // A review of 2026-10-15: the first version removed and inserted row by row, and the grid
        // answers every notification - the list measured that at about 285 ms for 810 rows (RowList).
        var sheet = Mixed();
        var told = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();

        sheet.Filter.Rows.CollectionChanged += (_, changed) => told.Add(changed.Action);
        sheet.Filter.Choices[1].IsChosen = true;
        sheet.Filter.Choices[0].IsChosen = true;

        Assert.Equal([System.Collections.Specialized.NotifyCollectionChangedAction.Reset, System.Collections.Specialized.NotifyCollectionChangedAction.Reset], told);
        Assert.Equal(sheet.Lines, sheet.Filter.Rows);
    }

    [Fact]
    public void The_tick_on_what_is_shown_cannot_be_taken_off()
    {
        // A table always shows something: clicking the ticked item says "this" again, not "nothing".
        var sheet = Mixed();
        var raised = new List<string?>();

        sheet.Filter.Choices[2].IsChosen = true;
        sheet.Filter.Choices[2].PropertyChanged += (_, changed) => raised.Add(changed.PropertyName);
        sheet.Filter.Choices[2].IsChosen = false;

        Assert.True(sheet.Filter.Choices[2].IsChosen);
        Assert.Contains(nameof(RowChoice.IsChosen), raised);
        Assert.Equal(["Moved"], sheet.Filter.Rows.Select(row => row.ServiceName));
    }

    [Fact]
    public void A_sort_somebody_chose_survives_a_choice_and_the_way_back()
    {
        // The grid sorts a view over these rows, the way the list keeps its sort (RowList) - a new
        // collection would take the sort with it, which is GUI rule 3 broken without a sound.
        var sheet = Mixed();

        var order = WpfHost.On(() =>
        {
            var view = new ListCollectionView(sheet.Filter.Rows);

            view.SortDescriptions.Add(new SortDescription(nameof(DiffLine.ServiceName), ListSortDirection.Descending));
            sheet.Filter.Choices[1].IsChosen = true;
            sheet.Filter.Choices[0].IsChosen = true;

            return view.Cast<DiffLine>().Select(line => line.ServiceName).ToList();
        });

        Assert.Equal(["Moved", "Gone", "Added"], order);
    }

    /// <summary>
    /// Added in the later side, gone from the side read without rights (so "seen on one side only"),
    /// and a startup type that moved - three rows of three kinds.
    /// </summary>
    internal static DiffSheet Mixed() =>
        TwoFiles.Sheet(
            TwoFiles.Of(new Side(), Rows.Entry("Gone"), Rows.Entry("Moved")),
            TwoFiles.Of(new Side { Hour = 1, Elevated = false }, Rows.Entry("Added"), Rows.Entry("Moved") with { StartType = Reading<StartType>.Present(StartType.Disabled) }));

    private static string Counted(string name, int count) => Texts.Of("gui.diff.show.counted", Texts.Of(name), count);
}
