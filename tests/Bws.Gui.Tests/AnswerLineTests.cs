using System.ComponentModel;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The line under the search box, and the box's own wrong state - UX-GUI-002 and UX-GUI-009 of the
/// audit of 2026-09-23.
///
/// <b>What was wrong, measured in that audit on a window without rights:</b> the sentence about a
/// mistake in the query stood about 830 pixels below the box, in a red line it shared with the
/// sentence about rights, the folded instances and every reservation about the answer. The box
/// carried no mark. The sentences are now in two places by subject - about the question under the
/// box, about the window at its foot - and these tests hold where each one goes.
/// </summary>
public sealed class AnswerLineTests
{
    /// <summary>
    /// The four kinds of sentence, each in its own piece, and the whole line in the order it has
    /// always been said - rights, reservations, footing - because that is what every other test in
    /// this project reads as Notice.
    /// </summary>
    [Fact]
    public void Rights_reservations_and_footing_are_three_pieces_of_one_admission()
    {
        var admitted = Sentences.Admissions(
            needs: Bws.Core.Querying.ExtraRead.Memory, held: true, unreadable: 0, tooCostly: 0,
            elevated: false, have: Bws.Core.Querying.ExtraRead.None, filling: false, folded: 1,
            listOnScreen: true);

        var rights = Texts.Of("gui.status.notElevated");
        var unread = Texts.Of("gui.query.unreadMemory");

        Assert.Equal(rights, admitted.Rights);
        Assert.Equal(unread, admitted.Reservations);
        Assert.StartsWith(Texts.Of("gui.status.folded.one", 1), admitted.Footing, StringComparison.Ordinal);
        Assert.EndsWith(Texts.Of("gui.status.holding"), admitted.Footing, StringComparison.Ordinal);

        // Nothing about the reservations reaches the line under the list.
        Assert.Equal(rights + " " + admitted.Footing, admitted.Line);
        Assert.DoesNotContain(unread, admitted.Line, StringComparison.Ordinal);

        Assert.Equal(rights + " " + unread + " " + admitted.Footing, admitted.Notice);
    }

    [Fact]
    public void The_rights_sentence_takes_no_trailing_space_when_nothing_follows_it()
    {
        var admitted = Sentences.Admissions(
            needs: Bws.Core.Querying.ExtraRead.None, held: false, unreadable: 0, tooCostly: 0,
            elevated: false, have: Bws.Core.Querying.ExtraRead.None, filling: false, folded: 0,
            listOnScreen: true);

        Assert.Equal(Texts.Of("gui.status.notElevated"), admitted.RightsPiece);
        Assert.Equal(admitted.Rights, admitted.Line);
        Assert.Equal(admitted.Rights, admitted.Notice);
    }

    /// <summary>
    /// A reservation about the answer stands under the box, in the answer line, and never in the
    /// line under the list.
    /// </summary>
    [Fact]
    public async Task A_reservation_about_the_answer_stands_under_the_box()
    {
        var model = await Loaded();

        model.QueryText = "signed:no";

        Assert.Contains(Texts.Of("gui.query.unreadSignatures"), model.Says.Reservations, StringComparison.Ordinal);
        Assert.Equal(model.Says.Reservations, model.Says.AnswerLine);
        Assert.False(model.Says.AnswerLineIsProblem);
        Assert.True(model.Says.AnswerLineShown);
        Assert.DoesNotContain(Texts.Of("gui.query.unreadSignatures"), model.Says.NoticeLine, StringComparison.Ordinal);
    }

    /// <summary>
    /// A mistake wins the line under the box over a reservation: a query that does not read has no
    /// answer of its own to qualify. And it says the list is the previous answer - the list staying
    /// is the decision MainViewModelTests.A_query_with_a_mistake_leaves_the_list_alone_and_says_what_is_wrong
    /// holds, and this sentence is what stops the window pretending the list is current.
    /// </summary>
    [Fact]
    public async Task A_mistake_takes_the_line_under_the_box()
    {
        var model = await Loaded();

        model.QueryText = "signed:no";
        model.QueryText = "signed:no stat:runing";

        Assert.True(model.Says.AnswerLineIsProblem);
        Assert.Equal(model.Says.QueryProblem, model.Says.AnswerLine);
        Assert.EndsWith(Texts.Of("gui.query.listIsPrevious"), model.Says.AnswerLine, StringComparison.Ordinal);

        // The foot of the window is for what the window could not do - the mistake is not there.
        Assert.Equal(string.Empty, model.Says.Problem);
    }

    /// <summary>
    /// The line takes room while there is text in the box, even with nothing to say - so a reading
    /// note that comes and goes under somebody's typing does not move the list - and gives the room
    /// back when the box is emptied.
    /// </summary>
    [Fact]
    public async Task The_line_takes_room_while_the_box_holds_text_and_gives_it_back_when_emptied()
    {
        var model = await Loaded();

        Assert.False(model.Says.AnswerLineShown);

        model.QueryText = "spool";

        Assert.Equal(string.Empty, model.Says.AnswerLine);
        Assert.True(model.Says.AnswerLineShown);

        model.ClearQuery();

        Assert.False(model.Says.AnswerLineShown);
    }

    /// <summary>
    /// THE BOX IS TOLD IT IS WRONG through the framework's own interface, which is what lights the
    /// red edge - and told it is right again when the mistake goes.
    /// </summary>
    [Fact]
    public async Task The_box_is_wrong_exactly_while_its_query_has_a_mistake()
    {
        var model = await Loaded();
        var box = (INotifyDataErrorInfo)model;

        model.QueryText = "stat:runing";

        Assert.True(box.HasErrors);
        Assert.Equal([model.Says.QueryProblem], box.GetErrors(nameof(MainViewModel.QueryText)).Cast<string>());
        Assert.Empty(box.GetErrors(nameof(MainViewModel.Scope)).Cast<string>());

        model.QueryText = "status:running";

        Assert.False(box.HasErrors);
        Assert.Empty(box.GetErrors(nameof(MainViewModel.QueryText)).Cast<string>());
    }

    /// <summary>
    /// <b>Told when it changed, and not once a second.</b> The pass that works the answer out runs on
    /// every tick of the clock too, and a binding told that its errors changed takes the error down
    /// and puts it back - a red edge blinking while nothing happened.
    /// </summary>
    [Fact]
    public async Task The_box_is_told_about_its_mistake_once_and_not_on_every_pass()
    {
        var model = await Loaded();
        var told = 0;

        ((INotifyDataErrorInfo)model).ErrorsChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(MainViewModel.QueryText))
            {
                told++;
            }
        };

        model.QueryText = "stat:runing";

        Assert.Equal(1, told);

        // Another pass over the same text - what a tick of the clock or a scope switch does.
        model.Scope = EntryScope.Everything;

        Assert.Equal(1, told);

        model.QueryText = string.Empty;

        Assert.Equal(2, told);
    }

    private static async Task<MainViewModel> Loaded()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler"), Rows.Driver("disk")), new SteppedClock());

        await model.LoadAsync();

        return model;
    }
}
