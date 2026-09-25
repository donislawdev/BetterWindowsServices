using Bws.Core.Querying;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The list under the search box, checked without a window: when it opens, what it holds, what
/// is chosen, and what the sentence beside each word is.
///
/// <b>Three of these hold rules that a window would have got wrong quietly</b>, which is why the
/// rules live in the model rather than in the handlers - `docs/PROJEKT-PODPOWIEDZI-20260915.md`
/// section 4.5. A chip writing into the model must not open a list under a box nobody is typing
/// into, a selection must not be completed as if the caret were at zero, and focus handed back by
/// the framework after Alt+Tab is not a person arriving.
/// </summary>
public sealed class SuggestingTests
{
    private static Suggesting Fresh() =>
        new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock()).Suggesting;

    private static IEnumerable<string> Words(Suggesting suggesting) => suggesting.Offered.Select(row => row.Word);

    [Fact]
    public void A_person_arriving_at_an_empty_box_is_offered_the_questions_that_fit_the_list()
    {
        var suggesting = Fresh();

        suggesting.Arrived(string.Empty);

        // The questions for the list the window opens on - five of the six since backlog 358,
        // because "services only" asked of the services list selects the whole of it.
        Assert.True(suggesting.IsOpen);
        Assert.Equal(QueryExamples.For(Scopes.Opening).Select(example => example.Query), Words(suggesting));
        Assert.Equal(QueryExamples.For(Scopes.Opening).Select(example => example.Label), suggesting.Offered.Select(row => row.Meaning));
        Assert.Equal(suggesting.Offered[0], suggesting.Chosen);

        // Each one replaces the whole of the box, which is nothing.
        Assert.All(suggesting.Offered, row => Assert.Equal(0..0, row.Replaces));
    }

    [Fact]
    public void A_person_arriving_at_a_box_with_text_in_it_is_offered_nothing()
    {
        var suggesting = Fresh();

        suggesting.Arrived("status:running");

        Assert.False(suggesting.IsOpen);
    }

    /// <summary>
    /// Decision 2: the examples are for somebody who ARRIVES. Emptying the box by typing, or by
    /// Escape, brings nothing back - otherwise Escape, Escape, Escape would mean close the list,
    /// clear the box, and here is the list again.
    /// </summary>
    [Fact]
    public void Emptying_the_box_does_not_bring_the_questions_back()
    {
        var suggesting = Fresh();

        suggesting.Arrived(string.Empty);
        suggesting.Follow("s", 1, 0);

        Assert.Equal(["status", "start", "signed", "sidtype", "sddl"], Words(suggesting));

        suggesting.Follow(string.Empty, 0, 0);

        Assert.False(suggesting.IsOpen);
    }

    /// <summary>
    /// The keyboard restored by the framework - the window's start, the return after Alt+Tab -
    /// is not a person asking for help. Said quietly, and the box stays as it was.
    /// </summary>
    [Fact]
    public void The_keyboard_merely_being_in_the_box_offers_nothing()
    {
        var suggesting = Fresh();

        suggesting.Keyboard(present: true);

        Assert.False(suggesting.IsOpen);

        // And it is enough for typing to be followed.
        suggesting.Follow("sta", 3, 0);

        Assert.Equal(["status", "start"], Words(suggesting));
    }

    /// <summary>
    /// A chip writes into the model, the binding writes into the box, and the box says its text
    /// changed - with the keyboard in the chip. Nothing may open under a box nobody is typing
    /// into, because nothing but Escape could close it.
    /// </summary>
    [Fact]
    public void Typing_followed_without_the_keyboard_in_the_box_opens_nothing()
    {
        var suggesting = Fresh();

        suggesting.Follow("status:", 7, 0);

        Assert.False(suggesting.IsOpen);

        // And a list that was open closes the moment the keyboard leaves.
        suggesting.Arrived(string.Empty);
        Assert.True(suggesting.IsOpen);

        suggesting.Left();
        Assert.False(suggesting.IsOpen);

        suggesting.Follow("status:", 7, 0);
        Assert.False(suggesting.IsOpen);
    }

    /// <summary>
    /// After Ctrl+F the whole text is selected and the caret stands at zero, so a prefix counted
    /// there would be false - and whatever is typed next replaces the selection anyway.
    /// </summary>
    [Fact]
    public void A_selection_is_never_completed()
    {
        var suggesting = Fresh();

        suggesting.Arrived("status:running");
        suggesting.Follow("status:running", 0, 14);

        Assert.False(suggesting.IsOpen);
        Assert.False(suggesting.Ask("status:running", 0, 14));

        // THE CASE THAT PROVES THE RULE IS THERE - found by the mutation registry. With the whole
        // text selected the caret stands at zero, where the field prefix is empty and typing is
        // quiet anyway. A selection of the last letter puts the caret after the colon, where an
        // empty prefix means every value: twelve rows over a selected `r`.
        suggesting.Follow("status:r", 7, 1);
        Assert.False(suggesting.IsOpen);
        Assert.False(suggesting.Ask("status:r", 7, 1));

        // And it closes a list that was open when the selection arrives. Opened by TYPING the next
        // letter since 2026-09-25 - collapsing the selection with the text unchanged is a caret
        // move, and a caret move closes (SuggestingTypingTests).
        suggesting.Follow("status:ru", 9, 0);
        Assert.True(suggesting.IsOpen);

        suggesting.Follow("status:ru", 7, 2);
        Assert.False(suggesting.IsOpen);
    }

    [Fact]
    public void Down_on_a_closed_list_asks_on_purpose_and_says_whether_it_opened()
    {
        var suggesting = Fresh();

        suggesting.Keyboard(present: true);

        // Every field on an empty member, which typing alone never offers.
        Assert.True(suggesting.Ask("status:running ", 15, 0));
        Assert.Equal(QueryFields.Names, Words(suggesting));

        suggesting.Close();

        // The questions on an empty box, as arriving does.
        Assert.True(suggesting.Ask(string.Empty, 0, 0));
        Assert.Equal(QueryExamples.For(Scopes.Opening).Select(example => example.Query), Words(suggesting));

        suggesting.Close();

        // And nothing where nothing fits, said rather than swallowed.
        Assert.False(suggesting.Ask("foo:", 4, 0));
        Assert.False(suggesting.IsOpen);
    }

    [Fact]
    public void Down_and_Up_move_the_choice_and_wrap_at_both_ends()
    {
        var suggesting = Fresh();

        suggesting.Keyboard(present: true);
        suggesting.Follow("sta", 3, 0);

        Assert.Equal("status", suggesting.Chosen!.Word);
        Assert.True(suggesting.Next());
        Assert.Equal("start", suggesting.Chosen!.Word);
        Assert.True(suggesting.Next());
        Assert.Equal("status", suggesting.Chosen!.Word);
        Assert.True(suggesting.Previous());
        Assert.Equal("start", suggesting.Chosen!.Word);

        suggesting.Close();

        Assert.False(suggesting.Next());
        Assert.False(suggesting.Previous());
    }

    /// <summary>
    /// Down, Down, a letter: the choice stays on the word it was on where that word is still
    /// offered, rather than going back to the first row every time the list is replaced.
    /// </summary>
    [Fact]
    public void The_choice_is_kept_by_word_across_a_replacement_of_the_list()
    {
        var suggesting = Fresh();

        suggesting.Keyboard(present: true);
        suggesting.Follow("status:", 7, 0);
        suggesting.Next();
        suggesting.Next();
        suggesting.Next();

        Assert.Equal("pending", suggesting.Chosen!.Word);

        // Kept, and NOT the first row of the new list - the mutation registry found a version
        // of this test where the kept word happened to be first, which proves nothing.
        suggesting.Follow("status:p", 8, 0);

        Assert.Equal(
            ["paused", "pending", "pausePending", "startPending", "stopPending", "continuePending"],
            Words(suggesting));
        Assert.Equal("pending", suggesting.Chosen!.Word);

        // Gone from the list, so the first row.
        suggesting.Follow("status:pa", 9, 0);

        Assert.Equal(["paused", "pausePending"], Words(suggesting));
        Assert.Equal("paused", suggesting.Chosen!.Word);
    }

    /// <summary>
    /// THE ORDER OF THE TWO ASSIGNMENTS IS THE BEHAVIOUR. The window's list binds SelectedItem
    /// two ways, so replacing the items sets the choice to null through the binding - this
    /// plays that binding, and the choice has to survive it.
    /// </summary>
    [Fact]
    public void The_choice_is_set_after_the_list_so_that_a_two_way_binding_cannot_clear_it()
    {
        var suggesting = Fresh();
        var order = new List<string>();

        suggesting.PropertyChanged += (_, changed) =>
        {
            order.Add(changed.PropertyName!);

            if (changed.PropertyName == nameof(Suggesting.Offered))
            {
                // What a ListBox does when its ItemsSource is replaced.
                suggesting.Chosen = null;
            }
        };

        suggesting.Keyboard(present: true);
        suggesting.Follow("sta", 3, 0);

        Assert.Equal("status", suggesting.Chosen!.Word);
        Assert.True(order.IndexOf(nameof(Suggesting.Offered)) < order.LastIndexOf(nameof(Suggesting.Chosen)));
    }

    [Fact]
    public void Taking_returns_the_chosen_row_and_closes_the_list()
    {
        var suggesting = Fresh();

        suggesting.Keyboard(present: true);
        suggesting.Follow("sta", 3, 0);
        suggesting.Next();

        var taken = suggesting.Take();

        Assert.Equal("start", taken!.Word);
        Assert.Equal("start:", taken.Written);
        Assert.Equal(0..3, taken.Replaces);
        Assert.False(suggesting.IsOpen);

        // A closed list hands back nothing, so a press that took nothing can be handed on.
        Assert.Null(suggesting.Take());
    }

    [Fact]
    public void Closing_says_whether_there_was_anything_to_close()
    {
        var suggesting = Fresh();

        Assert.False(suggesting.Close());

        suggesting.Arrived(string.Empty);

        Assert.True(suggesting.Close());
        Assert.False(suggesting.Close());
        Assert.Empty(suggesting.Offered);
        Assert.Null(suggesting.Chosen);
    }

    [Fact]
    public void Opening_and_closing_are_announced_so_the_popup_can_follow()
    {
        var suggesting = Fresh();
        var announced = new List<string>();

        suggesting.PropertyChanged += (_, changed) => announced.Add(changed.PropertyName!);

        suggesting.Arrived(string.Empty);
        Assert.Contains(nameof(Suggesting.IsOpen), announced);

        announced.Clear();
        suggesting.Close();
        Assert.Contains(nameof(Suggesting.IsOpen), announced);
    }

    /// <summary>
    /// A second arrival at the same empty box leaves the six rows where they are rather than
    /// rebuilding them under the pointer - GUI rule 3, nothing jumps on a refresh.
    /// </summary>
    [Fact]
    public void The_same_list_offered_again_is_left_alone()
    {
        var suggesting = Fresh();
        var announced = 0;

        suggesting.Arrived(string.Empty);
        suggesting.Next();
        suggesting.PropertyChanged += (_, _) => announced++;

        suggesting.Arrived(string.Empty);

        Assert.Equal(0, announced);
        Assert.Equal(QueryExamples.For(Scopes.Opening)[1].Query, suggesting.Chosen!.Word);
    }

    [Fact]
    public void A_field_carries_its_own_sentence_and_the_four_that_cost_a_second_reading_say_so()
    {
        var suggesting = Fresh();

        suggesting.Keyboard(present: true);
        suggesting.Ask("s", 1, 0);

        var signed = suggesting.Offered.Single(row => row.Word == "signed");

        Assert.Equal(Texts.Of("gui.suggest.field.signed"), signed.Meaning);
        Assert.Contains("takes seconds", signed.Meaning, StringComparison.Ordinal);

        Assert.All(suggesting.Offered, row => Assert.False(string.IsNullOrWhiteSpace(row.Meaning)));
        Assert.All(suggesting.Offered, row => Assert.DoesNotContain("gui.", row.Meaning, StringComparison.Ordinal));
    }

    [Fact]
    public void A_reserved_word_carries_one_of_three_sentences()
    {
        var suggesting = Fresh();

        suggesting.Keyboard(present: true);
        suggesting.Follow("name:", 5, 0);

        Assert.Equal(
            [Texts.Of("gui.suggest.word.any"), Texts.Of("gui.suggest.word.none"), Texts.Of("gui.suggest.word.unreadable")],
            suggesting.Offered.Select(row => row.Meaning));
    }

    /// <summary>
    /// Decision 3: a value gets the label of the chip standing for it - the same word somebody
    /// sees in the row above the list - and nothing where no chip exists.
    /// </summary>
    [Fact]
    public void A_value_carries_the_label_of_its_chip_and_nothing_where_there_is_no_chip()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());
        var suggesting = model.Suggesting;

        suggesting.Keyboard(present: true);
        suggesting.Follow("status:pend", 11, 0);

        var pending = model.Filters.Single(chip => chip.Field == "status" && chip.Value == "pending");

        Assert.Equal(pending.Label, suggesting.Offered.Single(row => row.Word == "pending").Meaning);
        Assert.Equal(string.Empty, suggesting.Offered.Single(row => row.Word == "startPending").Meaning);

        // A chip that EXCLUDES a value does not lend its label to the value: its words say the
        // opposite of what writing the value would do. No such chip exists in the row today, so
        // one is built here to hold the rule.
        var excluding = new Suggesting(
            () => [new FilterChip("gui.filter.stopped", "type", "driver", negated: true, () => string.Empty, _ => { })],
            () => QueryExamples.All);

        excluding.Keyboard(present: true);
        excluding.Follow("type:dri", 8, 0);

        Assert.Equal(string.Empty, excluding.Offered.Single(row => row.Word == "driver").Meaning);
    }
}
