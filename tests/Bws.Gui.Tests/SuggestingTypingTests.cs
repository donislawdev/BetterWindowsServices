using Bws.Core.Querying;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The list under the search box follows TYPING, and Tab writes a word - the owner's two decisions
/// of 2026-09-25, `docs/PROJEKT-PODPOWIEDZI-UX-20260925.md` (T1 and P2), checked without a window.
///
/// <b>Its own file because both reverse something the first design settled</b> - Tab walked on
/// (decision 7) and a caret move was followed like a keystroke - and the reasons belong beside the
/// tests that hold them rather than scattered through SuggestingTests.
/// </summary>
public sealed class SuggestingTypingTests
{
    private static Suggesting Typing(string text, int caret)
    {
        var suggesting = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock()).Suggesting;

        suggesting.Keyboard(present: true);
        suggesting.Follow(text, caret, 0);

        return suggesting;
    }

    /// <summary>
    /// One Left arrow in <c>status:running</c> opened a list of one row saying <c>running</c> -
    /// measured on the core on 2026-09-25 - and correcting a query with the arrows blinked a list at
    /// every press. A caret moving through text nobody changed closes the list and opens none.
    /// </summary>
    [Fact]
    public void A_caret_moving_through_unchanged_text_opens_nothing_and_closes_what_was_open()
    {
        var suggesting = Typing("status:running", 14);

        Assert.False(suggesting.IsOpen);

        suggesting.Follow("status:running", 13, 0);
        Assert.False(suggesting.IsOpen);

        suggesting.Follow("status:running", 7, 0);
        Assert.False(suggesting.IsOpen);

        // Open by typing, then moved away from by the caret alone.
        suggesting.Follow("status:r", 8, 0);
        Assert.True(suggesting.IsOpen);

        suggesting.Follow("status:r", 7, 0);
        Assert.False(suggesting.IsOpen);

        // Down still asks for it on purpose, wherever the caret stands.
        Assert.True(suggesting.Ask("status:r", 7, 0));
    }

    /// <summary>
    /// One keystroke is two events from the box - its text changed and its selection changed - and
    /// the second one arrives with the same text at the same caret. It must leave the list the first
    /// one opened, or every letter would open a list and close it again.
    /// </summary>
    [Fact]
    public void The_second_event_of_one_keystroke_leaves_the_list_as_it_is()
    {
        var suggesting = Typing("sta", 3);

        Assert.Equal(["status", "start"], suggesting.Offered.Select(row => row.Word));

        suggesting.Next();
        suggesting.Follow("sta", 3, 0);

        Assert.True(suggesting.IsOpen);
        Assert.Equal("start", suggesting.Chosen!.Word);
    }

    /// <summary>
    /// The window writes a row through the selection and then puts the caret after it, so the last
    /// thing the box reports is a caret move - which closes. Written is followed as typing, so the
    /// values of a field just written are offered at once, and <c>sta</c> Tab Tab still gives
    /// <c>status:running</c>.
    /// </summary>
    [Fact]
    public void A_row_just_written_is_followed_as_typing_even_after_the_caret_moved()
    {
        var suggesting = Typing("sta", 3);

        // What the box reports while the window writes "status:" over "sta".
        suggesting.Follow("sta", 0, 3);
        suggesting.Follow("status:", 0, 7);
        suggesting.Follow("status:", 7, 0);

        Assert.False(suggesting.IsOpen);

        suggesting.Wrote("status:", 7);

        Assert.True(suggesting.IsOpen);
        Assert.Equal(
            QueryCompletions.WhileTyping("status:", 7).Select(completion => completion.Word),
            suggesting.Offered.Select(row => row.Word));
        Assert.Contains("running", suggesting.Offered.Select(row => row.Word));
        Assert.True(suggesting.TabWrites);

        // With the keyboard gone nothing opens - the rule every other road into the list keeps.
        suggesting.Left();
        suggesting.Wrote("status:", 7);

        Assert.False(suggesting.IsOpen);
    }

    /// <summary>
    /// Tab writes a WORD and never a question. The questions open when somebody arrives at an empty
    /// box - by Tab among other ways - so a Tab that wrote one would type a query into the box of
    /// anybody walking through the window with the keyboard.
    /// </summary>
    [Fact]
    public void Tab_writes_a_word_and_walks_past_the_questions_and_past_nothing()
    {
        Assert.True(Typing("sta", 3).TabWrites);

        var arriving = Typing(string.Empty, 0);

        arriving.Arrived(string.Empty);

        Assert.True(arriving.IsOpen);
        Assert.False(arriving.TabWrites);

        Assert.False(Typing("spooler", 7).TabWrites);
    }

    /// <summary>
    /// The sentence under the list names Tab for the words and leaves it out for the questions,
    /// because there it walks on - `docs/PROJEKT-PODPOWIEDZI-UX-20260925.md`, P3.
    /// </summary>
    [Fact]
    public void The_sentence_about_the_keys_names_Tab_only_where_Tab_writes()
    {
        var words = Typing("sta", 3);

        Assert.Equal(Texts.Of("gui.suggest.keys.words"), words.Keys);
        Assert.Contains("Tab", words.Keys, StringComparison.Ordinal);

        var questions = Typing(string.Empty, 0);

        questions.Arrived(string.Empty);

        Assert.Equal(Texts.Of("gui.suggest.keys"), questions.Keys);
        Assert.DoesNotContain("Tab", questions.Keys, StringComparison.Ordinal);
        Assert.DoesNotContain("gui.", questions.Keys, StringComparison.Ordinal);

        // And the window is told when it changes, or the sentence bound under the list would keep
        // saying the questions' keys over a list of words.
        var announced = new List<string>();

        questions.PropertyChanged += (_, changed) => announced.Add(changed.PropertyName!);
        questions.Follow("s", 1, 0);

        Assert.Contains(nameof(Suggesting.Keys), announced);
    }
}
