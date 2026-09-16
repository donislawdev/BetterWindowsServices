using System.ComponentModel;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What the list under the search box SAYS about its rows - to a screen reader, and over itself.
///
/// <b>Split out of SuggestingTests on 2026-09-16, the day that file crossed the length ratchet by
/// eight lines</b> and was the third test file over 500 against two allowed. The seam is the one
/// the tests already had: SuggestingTests asks what the list OFFERS and when, this file asks what
/// it says - the spoken sentence of 2026-09-16 (backlog 361), and the caption and the reading
/// order of a row from the owner's fifth remark of the same day.
/// </summary>
public sealed class SuggestingWordingTests
{
    private static Suggesting Fresh() =>
        new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock()).Suggesting;

    [Fact]
    public void The_chosen_row_is_spoken_as_its_word_its_sentence_and_its_place_in_the_list()
    {
        var suggesting = Fresh();

        suggesting.Keyboard(present: true);
        suggesting.Follow("sta", 3, 0);

        // Two rows - status and start - and the first is chosen on opening. The position is part
        // of the sentence, because "1 of 2" is how a person without the screen knows this is a
        // list and where they are in it.
        var first = suggesting.Chosen!;

        Assert.Equal($"{first.Word}, {first.Meaning}, 1 of {suggesting.Offered.Count}", suggesting.Spoken);
        Assert.DoesNotContain("gui.", suggesting.Spoken, StringComparison.Ordinal);

        suggesting.Next();

        var second = suggesting.Chosen!;

        Assert.NotEqual(first, second);
        Assert.Equal($"{second.Word}, {second.Meaning}, 2 of {suggesting.Offered.Count}", suggesting.Spoken);
    }

    [Fact]
    public void A_row_without_a_sentence_is_spoken_without_one_and_a_closed_list_says_nothing()
    {
        var suggesting = Fresh();

        suggesting.Keyboard(present: true);
        suggesting.Follow("status:pend", 11, 0);

        // startPending has no chip and so no sentence - the bare form, without an empty gap where
        // the sentence would be.
        var bare = suggesting.Offered.Single(row => row.Word == "startPending");
        suggesting.Chosen = bare;

        var position = suggesting.Offered.ToList().IndexOf(bare) + 1;

        Assert.Equal($"startPending, {position} of {suggesting.Offered.Count}", suggesting.Spoken);

        suggesting.Close();

        Assert.Equal(string.Empty, suggesting.Spoken);
    }

    [Fact]
    public void A_question_leads_with_its_sentence_and_a_completion_leads_with_its_word()
    {
        var suggesting = Fresh();

        suggesting.Arrived(string.Empty, 0);

        // On an empty box the person has typed nothing and is reading, so the sentence comes first
        // and the syntax follows it - decision D1 of the packet of 2026-09-16.
        Assert.True(suggesting.IsOpen);
        Assert.All(suggesting.Offered, row =>
        {
            Assert.True(row.IsQuestion);
            Assert.Equal(row.Meaning, row.Lead);
            Assert.Equal(row.Word, row.Trail);
        });

        suggesting.Follow("sta", 3, 0);

        // Mid-word the person is looking for the word they started, so it leads.
        Assert.True(suggesting.IsOpen);
        Assert.All(suggesting.Offered, row =>
        {
            Assert.False(row.IsQuestion);
            Assert.Equal(row.Word, row.Lead);
            Assert.Equal(row.Meaning, row.Trail);
        });
    }

    [Fact]
    public void The_caption_says_which_list_is_open_and_nothing_while_none_is()
    {
        var suggesting = Fresh();
        var raised = 0;
        ((INotifyPropertyChanged)suggesting).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Suggesting.Caption))
            {
                raised++;
            }
        };

        Assert.Equal(string.Empty, suggesting.Caption);

        suggesting.Arrived(string.Empty, 0);

        Assert.Equal("Questions to start from", suggesting.Caption);
        Assert.Equal(1, raised);

        suggesting.Follow("sta", 3, 0);

        // A different kind of list under the same open popup, so the caption has to change - and
        // say so, or the popup keeps the old one.
        Assert.Equal("What can go here", suggesting.Caption);
        Assert.Equal(2, raised);
        Assert.DoesNotContain("gui.", suggesting.Caption, StringComparison.Ordinal);

        suggesting.Close();

        Assert.Equal(string.Empty, suggesting.Caption);
        Assert.Equal(3, raised);
    }
}
