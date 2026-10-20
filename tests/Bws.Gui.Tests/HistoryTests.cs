using System.ComponentModel;
using System.IO;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The change history in the window, without a window - J2, owner's decisions J2-a to J2-d of
/// 2026-10-20. The lines and the choices over them (J2-b), the reading and its four states, and the
/// section at the foot of the panel about one entry (J2-c).
/// </summary>
public sealed class HistoryTests
{
    [Fact]
    public void Identical_changes_share_a_line_by_default_and_the_line_under_show_says_so()
    {
        // Seven changes: BITS twice each way by SYSTEM, a driver installed twice, Spooler once.
        var filter = new HistoryFilter(HistoryLog.Ordinary().Changes);

        Assert.Equal(4, filter.Rows.Count);
        Assert.Equal(Texts.Of("gui.history.showing.grouped.many", 7, 4), filter.Showing);
        Assert.Equal("2", filter.Rows[0].Times);
        Assert.Equal(Texts.Of("gui.history.displayNameOnly", "WinDivert"), filter.Rows[0].Entry);
        Assert.Equal(string.Empty, filter.Rows[3].Times);
        Assert.Equal(string.Empty, filter.Rows[3].Since);
    }

    [Fact]
    public void Each_change_on_its_own_line_shows_all_of_them_and_hides_nothing()
    {
        var filter = new HistoryFilter(HistoryLog.Ordinary().Changes);

        filter.Groups[0][1].IsChosen = true;

        Assert.Equal(7, filter.Rows.Count);
        Assert.Equal(string.Empty, filter.Showing);
        Assert.False(filter.Groups[0][0].IsChosen);
    }

    [Fact]
    public void The_second_group_counts_each_choice_and_not_by_system_says_how_many_of_how_many()
    {
        var filter = new HistoryFilter(HistoryLog.Ordinary().Changes);

        Assert.Equal(
            [
                Counted("gui.history.show.all", 7),
                Counted("gui.history.show.notSystem", 3),
                Counted("gui.history.show.installed", 2),
                Counted("gui.history.show.startType", 5)
            ],
            filter.Groups[1].Select(choice => choice.Label));

        filter.Groups[1][1].IsChosen = true;

        // What SYSTEM did not do: the driver twice and Spooler once - two lines.
        Assert.Equal(2, filter.Rows.Count);
        Assert.Equal(Texts.Of("gui.history.showing.narrowedGrouped.many", 3, 7, 2), filter.Showing);
        Assert.Equal(Texts.Of("gui.history.show", Texts.Of("gui.history.show.notSystem")), filter.Button);
    }

    [Fact]
    public void A_choice_that_narrows_nothing_is_not_offered()
    {
        // Only SYSTEM, only start types: every narrower choice would show nothing or everything.
        var filter = new HistoryFilter(
        [
            HistoryLog.BySystem("BITS", HistoryLog.Day(20, 11)),
            HistoryLog.BySystem("BITS", HistoryLog.Day(20, 10), back: true)
        ]);

        Assert.Equal([Counted("gui.history.show.all", 2)], filter.Groups[1].Select(choice => choice.Label));
    }

    [Fact]
    public void A_log_with_no_change_offers_no_button_and_no_rows()
    {
        var filter = new HistoryFilter([]);

        Assert.False(filter.Offered);
        Assert.Empty(filter.Rows);
        Assert.Equal(string.Empty, filter.Showing);
    }

    [Fact]
    public void A_new_reading_keeps_the_table_and_both_ticks()
    {
        // GUI rule 3: nothing resets on a refresh - the collection a sort lies over, and the two ticks.
        var filter = new HistoryFilter(HistoryLog.Ordinary().Changes);
        var rows = filter.Rows;

        filter.Groups[0][1].IsChosen = true;
        filter.Groups[1][1].IsChosen = true;

        var more = HistoryLog.Ordinary().Changes
            .Prepend(HistoryLog.ByPerson("Spooler", HistoryLog.Day(21, 9), back: true))
            .ToList();

        filter.Renew(more);

        Assert.Same(rows, filter.Rows);
        Assert.True(filter.Groups[0][1].IsChosen);
        Assert.Equal(Counted("gui.history.show.notSystem", 4), filter.Groups[1].Single(choice => choice.IsChosen).Label);
        Assert.Equal(4, filter.Rows.Count);
    }

    [Fact]
    public void A_new_reading_that_no_longer_offers_the_tick_goes_back_to_every_change()
    {
        var filter = new HistoryFilter(HistoryLog.Ordinary().Changes);

        filter.Groups[1][1].IsChosen = true;
        filter.Renew([HistoryLog.BySystem("BITS", HistoryLog.Day(21, 9))]);

        Assert.True(filter.Groups[1][0].IsChosen);
        Assert.Single(filter.Rows);
    }

    [Fact]
    public void A_new_reading_says_the_choices_changed_so_the_menu_is_made_again()
    {
        var filter = new HistoryFilter(HistoryLog.Ordinary().Changes);
        var raised = new List<string?>();

        filter.PropertyChanged += (_, changed) => raised.Add(changed.PropertyName);
        filter.Renew(HistoryLog.Ordinary().Changes);

        Assert.Contains(nameof(IRowChoices.Groups), raised);
    }

    [Fact]
    public async Task Opening_the_view_reads_the_log_and_puts_it_where_the_list_was()
    {
        var log = new HistoryLog(HistoryLog.Ordinary());
        var model = Model(log);

        await model.OpenHistory();

        var sheet = Assert.IsType<HistorySheet>(model.History.Shown);

        Assert.False(model.ListOnScreen);
        Assert.Equal(string.Empty, sheet.Said);
        Assert.Equal(4, sheet.Filter.Rows.Count);
        Assert.StartsWith(Texts.Of("gui.history.log.cleared", HistoryWords.When(HistoryLog.Day(11, 16)), @"PC\someone"), sheet.Admissions[0], StringComparison.Ordinal);
        Assert.Contains(Texts.Of("gui.history.installedUnderDisplayName"), sheet.Admissions);
        Assert.Equal(1, log.Reads);
    }

    [Fact]
    public async Task While_the_first_reading_is_out_the_view_says_it_is_reading()
    {
        var log = new HistoryLog(HistoryLog.Ordinary());
        var model = Model(log);

        log.Hold();

        var opening = model.OpenHistory();

        Assert.Equal(Texts.Of("gui.history.reading"), model.History.Shown?.Said);
        Assert.Empty(model.History.Shown!.Filter.Rows);

        log.Release();
        await opening;

        Assert.Equal(string.Empty, model.History.Shown?.Said);
    }

    [Fact]
    public async Task A_log_that_cannot_be_read_is_a_sentence_with_the_way_out_and_no_rows()
    {
        var log = new HistoryLog(HistoryLog.Ordinary()) { FailWith = new Win32Exception(5) };
        var model = Model(log);

        await model.OpenHistory();

        var sheet = model.History.Shown!;

        Assert.Equal(Texts.Of("gui.history.readFailed", new Win32Exception(5).Message), sheet.Said);
        Assert.Empty(sheet.Filter.Rows);
        Assert.Empty(sheet.Admissions);
        Assert.False(sheet.Filter.Offered);
    }

    [Fact]
    public async Task A_failure_after_a_good_reading_leaves_nothing_of_the_good_one_on_screen()
    {
        // Rule 8: rows from an earlier reading under a new failure would read as the answer.
        var log = new HistoryLog(HistoryLog.Ordinary());
        var model = Model(log);

        await model.OpenHistory();

        log.FailWith = new InvalidDataException("An event too large to render.");
        await model.LoadAsync();

        Assert.Empty(model.History.Shown!.Filter.Rows);
        Assert.Equal(Texts.Of("gui.history.readFailed", "An event too large to render."), model.History.Shown.Said);
    }

    [Fact]
    public async Task A_second_ask_while_a_reading_is_out_waits_for_it_rather_than_reading_again()
    {
        var log = new HistoryLog(HistoryLog.Ordinary());
        var model = Model(log);

        log.Hold();

        var opening = model.OpenHistory();
        var again = model.History.Open();

        log.Release();
        await Task.WhenAll(opening, again);

        Assert.Equal(1, log.Reads);
    }

    [Fact]
    public async Task F5_reads_the_log_again_only_once_somebody_has_asked_for_it()
    {
        var log = new HistoryLog(HistoryLog.Ordinary());
        var model = Model(log);

        await model.LoadAsync();

        Assert.Equal(0, log.Reads);

        await model.OpenHistory();
        await model.LoadAsync();

        Assert.Equal(2, log.Reads);
    }

    [Fact]
    public async Task Escape_puts_the_view_away_and_the_list_comes_back()
    {
        var model = Model(new HistoryLog(HistoryLog.Ordinary()));

        await model.OpenHistory();

        Assert.True(model.PutAwayAView());
        Assert.Null(model.History.Shown);
        Assert.True(model.ListOnScreen);
        Assert.False(model.PutAwayAView());
    }

    [Fact]
    public async Task The_panel_ends_with_the_entrys_history_identical_changes_together()
    {
        var log = new HistoryLog(HistoryLog.Ordinary());
        var model = await Shown(log, "BITS");

        var history = model.Chosen.Sections[^1];

        Assert.Equal(Texts.Of("gui.details.section.history"), history.Heading);
        Assert.Equal(2, history.Lines.Count);
        Assert.Equal(HistoryWords.When(HistoryLog.Day(20, 11)), history.Lines[0].Label);
        Assert.Equal(
            Texts.Of("gui.history.line.many", Texts.Of("gui.history.startTypeChanged", "auto start", "demand start"), @"NT AUTHORITY\SYSTEM", 2, HistoryWords.When(HistoryLog.Day(16, 7))),
            history.Lines[0].Shown);

        // How far back, and why no installation is here - said whenever the log holds one (J1-b).
        Assert.Contains(Texts.Of("gui.history.installationsNotByName"), history.Note, StringComparison.Ordinal);
        Assert.Contains(Texts.Of("gui.history.windowsWords"), history.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entry_with_no_change_says_so_under_its_section()
    {
        var model = await Shown(new HistoryLog(HistoryLog.Ordinary()), "Dhcp");

        var history = model.Chosen.Sections[^1];

        Assert.Empty(history.Lines);
        Assert.Contains(Texts.Of("gui.history.nothingFor"), history.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Moving_the_panel_from_entry_to_entry_reads_the_log_once()
    {
        var log = new HistoryLog(HistoryLog.Ordinary());
        var model = await Shown(log, "BITS");

        model.Chosen.Row = model.Rows.Single(row => row.ServiceName == "Spooler");
        model.Chosen.Show();
        await model.History.Out;

        Assert.Equal(1, log.Reads);
        Assert.Single(model.Chosen.Sections[^1].Lines);
    }

    [Fact]
    public async Task While_the_log_is_read_the_panel_section_says_so_and_fills_when_it_is_back()
    {
        var log = new HistoryLog(HistoryLog.Ordinary());
        var model = Model(log);

        await model.LoadAsync();
        log.Hold();

        model.Chosen.Row = model.Rows.Single(row => row.ServiceName == "BITS");
        model.Chosen.Show();

        Assert.Equal(Texts.Of("gui.history.reading"), model.Chosen.Sections[^1].Note);

        log.Release();
        await model.History.Out;

        Assert.Equal(2, model.Chosen.Sections[^1].Lines.Count);
    }

    [Fact]
    public async Task A_model_built_without_a_log_has_no_history_section()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        await model.LoadAsync();
        model.Chosen.Row = model.Rows.Single();
        model.Chosen.Show();

        Assert.DoesNotContain(model.Chosen.Sections, section => section.Heading == Texts.Of("gui.details.section.history"));
    }

    private static MainViewModel Model(HistoryLog log) =>
        new(new LiveMachine(Rows.Entry("Spooler"), Rows.Entry("BITS"), Rows.Entry("Dhcp")), new SteppedClock(), history: log);

    private static async Task<MainViewModel> Shown(HistoryLog log, string name)
    {
        var model = Model(log);

        await model.LoadAsync();
        model.Chosen.Row = model.Rows.Single(row => row.ServiceName == name);
        model.Chosen.Show();
        await model.History.Out;

        return model;
    }

    private static string Counted(string key, int count) => Texts.Of("gui.history.show.counted", Texts.Of(key), count);
}
