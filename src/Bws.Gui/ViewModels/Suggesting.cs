using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// One row under the search box: what would be typed, what it means, and the edit it stands for.
/// </summary>
/// <remarks>
/// <see cref="Replaces"/> and <see cref="Written"/> rather than the finished text, so the window
/// can write it THROUGH THE SELECTION - which the text box can undo - rather than by replacing its
/// text, which it cannot. Decision 13 of the design.
/// </remarks>
/// <param name="Word">What would be typed, as the language spells it.</param>
/// <param name="Meaning">What that means, in the reader's language. Empty when nothing has a sentence for it.</param>
/// <param name="Replaces">The range of the box's text it stands in for.</param>
/// <param name="Written">What goes into that range.</param>
/// <param name="IsQuestion">One of the questions to start from: it leads with its sentence.</param>
/// <param name="IsRecent">A search made lately (RecentQueries): it leads with the query, a mark beside it says so, and Delete forgets it.</param>
/// <param name="Heading">The name of the group this row opens, drawn over it - only on the first row of a group that does not open the list.</param>
public sealed record Suggestion(
    string Word, string Meaning, Range Replaces, string Written, bool IsQuestion = false, bool IsRecent = false, string? Heading = null)
{
    /// <summary>
    /// What the row leads with: the sentence for a question, the word for a completion. A person
    /// completing a word is looking for the word they started - a person who has typed nothing is
    /// looking for something to read, and the syntax teaches itself in the second column. The
    /// owner's fifth remark of 2026-09-16, decision D1 of the packet.
    /// </summary>
    public string Lead => IsQuestion ? Meaning : Word;

    /// <summary>What follows the lead - the other of the two.</summary>
    public string Trail => IsQuestion ? Word : Meaning;
}

/// <summary>
/// The list under the search box - whether the keyboard is in the box, what is offered, whether
/// the list is open, and which row is chosen.
///
/// <b>Point 9 of the review in `docs/11` 2.14, backlog 15, built 2026-09-15 to
/// `docs/PROJEKT-PODPOWIEDZI-20260915.md`.</b> The language was explained in the box's tooltip
/// alone, and a person who does not know what goes in a box clicks it rather than pointing at it.
///
/// <b>Every rule here can be checked without a window, and three of them exist because a window
/// would have got them wrong quietly.</b> <see cref="Follow"/> opens nothing while the keyboard is
/// elsewhere - a chip writes into the model, the binding writes into the box, the box says its
/// text changed, and a list under a box nobody is typing into would have nothing to close it. It
/// opens nothing over a selection - after Ctrl+F the whole text is selected and the caret stands
/// at zero, so a prefix counted there would be false and whatever is typed next replaces the
/// selection anyway. And <see cref="Arrived"/> is a different event from the keyboard merely being
/// here: WPF hands the box its focus back after Alt+Tab, and that is not a person asking for help.
///
/// <b>The window owns the keys, the caret and the popup.</b> This owns what the list holds and
/// says so through notifications, exactly as <see cref="Holding"/> owns the rule about the list
/// and is told by the window whether somebody is using it.
/// </summary>
public sealed class Suggesting : Observable
{
    private readonly Func<IReadOnlyList<FilterChip>> _chips;
    private readonly Func<IReadOnlyList<QueryExample>> _examples;

    private IReadOnlyList<Suggestion> _offered = [];
    private Suggestion? _chosen;
    private bool _keyboardHere;
    private bool _questions;
    private bool _guiding;
    private bool _composing;

    // What the box held, and where its caret stood, the last time the list was told - so that a
    // caret moving through text nobody changed can be told apart from typing. See Follow.
    private string? _followedText;
    private int _followedCaret;

    // Constants rather than literals in the expression below, because TextKeyGuards knows the
    // shapes a key is used in and a literal inside a conditional is not one of them.
    private const string QuestionsCaption = "gui.suggest.caption.questions";
    private const string WordsCaption = "gui.suggest.caption.words";
    private const string RecentCaption = "gui.suggest.caption.recent";
    private const string GuideCaption = "gui.suggest.caption.guide";
    private const string QuestionsKeys = "gui.suggest.keys";
    private const string WordsKeys = "gui.suggest.keys.words";
    private const string RecentKeys = "gui.suggest.keys.recent";
    private const string GuideKeys = "gui.suggest.keys.guide";

    /// <summary>
    /// The chips are read rather than copied, because their labels are read in whatever language
    /// the machine is set to and a copy would be a second list of the same words.
    ///
    /// <b>The examples are read the same way, since 2026-09-16, because they follow the scope</b> -
    /// backlog 358. A list copied at construction would offer the account example on Drivers for
    /// as long as the window stayed open, which is exactly the tooltip that row found.
    /// </summary>
    public Suggesting(Func<IReadOnlyList<FilterChip>> chips, Func<IReadOnlyList<QueryExample>> examples)
    {
        _chips = chips;
        _examples = examples;
    }

    /// <summary>
    /// The searches made lately, newest first, as the window keeps them in the layout file - asked
    /// each time the list opens on an empty box. Handed in by the window, which holds the file
    /// (RecentSearches.Watch). Nothing until it does.
    /// </summary>
    public Func<IReadOnlyList<string>>? RecentQueries { get; set; }

    /// <summary>What the list holds. Replaced whole, never changed in place.</summary>
    public IReadOnlyList<Suggestion> Offered
    {
        get => _offered;
        private set => Set(ref _offered, value);
    }

    /// <summary>
    /// The row Enter would write. Kept by word across a replacement of the list, so that Down, Down
    /// and a letter does not go back to the first row - and otherwise the first.
    ///
    /// <b>Public to set, because the window's list binds to it two ways</b>: a click chooses a row
    /// here, and a replacement of the list clears it through the same binding, which is why
    /// <see cref="Offer"/> sets it AFTER the list rather than before.
    /// </summary>
    public Suggestion? Chosen
    {
        get => _chosen;
        set
        {
            var wasRecent = _chosen is { IsRecent: true };

            // THE KEYS FOLLOW THE ROW ONLY AS FAR AS DELETE: a search made lately can be forgotten and
            // nothing else can. A list changing kind is said by Offer, so each of the two raises says
            // one thing - with this one raising on every row, Offer's went unchecked by any test.
            if (Set(ref _chosen, value) && wasRecent != value is { IsRecent: true })
            {
                Raise(nameof(Keys));
            }
        }
    }

    /// <summary>Whether there is a list to show. Nothing offered means nothing open.</summary>
    public bool IsOpen => _offered.Count > 0;

    /// <summary>
    /// The sentence under the list saying which keys do what - under the list rather than in the
    /// tooltip, because under the list is where somebody is looking while the keys matter.
    /// Decision 9 of the design.
    ///
    /// <b>Two sentences since 2026-09-25, because Tab means two things</b> - it writes a word and
    /// it walks past the questions. `docs/PROJEKT-PODPOWIEDZI-UX-20260925.md`, P3.
    /// </summary>
    public string Keys => Texts.Of(
        _guiding ? GuideKeys
        : !_questions ? WordsKeys
        : _chosen is { IsRecent: true } ? RecentKeys
        : QuestionsKeys);

    /// <summary>
    /// Whether the list is the guide to the language - the ways to write a value and the fields over
    /// the questions - opened by the button at the end of the box or F1 in it. The owner's second
    /// choice of 2026-11-16: until then the syntax was in the box's tooltip alone.
    /// </summary>
    public bool Guiding => _guiding && IsOpen;

    /// <summary>
    /// Whether Tab writes the chosen row - the owner's reflex from PowerShell and every editor,
    /// 2026-09-25, which reversed decision 7 of the first design.
    ///
    /// <b>For a list of WORDS and never for the questions.</b> The questions open when somebody
    /// arrives at an empty box, and arriving by Tab is one of the ways - so a Tab that wrote them
    /// would make walking through the window with the keyboard type a query into the box and stop
    /// there. With nothing to write, Tab walks on as it always did.
    /// </summary>
    public bool TabWrites => CanTake && !_questions;

    /// <summary>
    /// Whether Enter or Tab may write the chosen row now: a list is open, and no input method is in
    /// the middle of composing a character in the box.
    ///
    /// <b>Review of PR 22.</b> An input method - the way Chinese, Japanese and Korean are typed -
    /// holds the characters it is still composing in the box until they are committed, and a row
    /// written over them would replace text the person has not finished. Whether a key reaches the
    /// window as itself during a composition depends on the input method, and there is none on the
    /// machine this was built on - NOT MEASURED - so the list simply takes nothing until the
    /// composition ends. <see cref="Composing"/> is told by the window.
    /// </summary>
    public bool CanTake => IsOpen && !_composing;

    /// <summary>
    /// An input method began composing text in the box, or finished. Said by the window from the
    /// text composition events. Whether ordinary typing raises the start too was not checked - if
    /// it does, the end follows with the character, so between two keystrokes this is false either
    /// way.
    /// </summary>
    public void Composing(bool open) => _composing = open;

    /// <summary>
    /// What kind of list is open, said over it: the questions on an empty box, or what can go where
    /// the caret is. Empty while nothing is open. Five rows of syntax with no sentence about what
    /// they were was half of "unintuitive" - the owner's fifth remark of 2026-09-16.
    /// </summary>
    public string Caption => !IsOpen
        ? string.Empty
        : Texts.Of(
            _guiding ? GuideCaption
            : !_questions ? WordsCaption
            : _offered[0].IsRecent ? RecentCaption
            : QuestionsCaption);

    /// <summary>
    /// What a screen reader is told about the chosen row - its word, its sentence when it has
    /// one, and where it stands in the list. Empty when there is no list or no choice.
    ///
    /// <b>Backlog 361. The keyboard stays in the box while the list is open, which is the whole
    /// design - and a screen reader announces what has the keyboard.</b> So a person arrowing
    /// through the rows heard nothing, while every probe found the rows perfectly named in the
    /// tree. Measured on 2026-09-16 with a listener subscribed as a reader would be: no focus
    /// event ever names a row, and the one thing that arrives is a selection event from the row's
    /// own peer - which does not exist yet when the list opens, so the first choice is silent
    /// even there. The window raises a notification carrying this sentence instead, on the box
    /// that has the keyboard - the mechanism Windows documents for exactly this, and the one that
    /// needs no element to be visible. Composed here rather than in the window so that the words
    /// have a test without one.
    ///
    /// <b>The position is part of it</b>, because "2 of 21" is how a person without the screen
    /// knows the list is a list and how far along it they are - what a sighted person reads from
    /// the highlight moving.
    /// </summary>
    public string Spoken
    {
        get
        {
            if (_chosen is null || !IsOpen)
            {
                return string.Empty;
            }

            var position = IndexOf(_chosen) + 1;

            return string.IsNullOrEmpty(_chosen.Meaning)
                ? Texts.Of("gui.suggest.spoken.bare", _chosen.Word, position, _offered.Count)
                : Texts.Of("gui.suggest.spoken", _chosen.Word, _chosen.Meaning, position, _offered.Count);
        }
    }

    /// <summary>
    /// The box has, or has not, the keyboard - said quietly, without offering anything. The
    /// window's start and the return after Alt+Tab, where WPF restores focus on its own.
    /// </summary>
    public void Keyboard(bool present)
    {
        _keyboardHere = present;

        if (!present)
        {
            // A composition does not outlive the keyboard leaving the box - and one abandoned
            // without an end said to the window would otherwise keep Tab and Enter from writing.
            _composing = false;
            Close();
        }
    }

    /// <summary>
    /// A PERSON came to the box - a click, Tab, Ctrl+F. The keyboard is here, and an empty box
    /// offers the questions to start from - the ones that fit the list on screen, backlog 358.
    /// Decision 2 of the design: examples on arrival at an empty box, fields from the first
    /// letter or on Down.
    ///
    /// A box with text in it is left as it is. A click into a box the list is already open
    /// under moves the caret first, and closing the list here would take away what
    /// <see cref="Follow"/> has just offered for the new caret.
    /// </summary>
    public void Arrived(string? text)
    {
        _keyboardHere = true;

        if (string.IsNullOrWhiteSpace(text))
        {
            _questions = true;
            Offer(Examples(text ?? string.Empty));
        }
    }

    /// <summary>The keyboard left the box, and the list goes with it.</summary>
    public void Left() => Keyboard(present: false);

    /// <summary>
    /// The text or the caret moved. The list follows TYPING - and opens only while the keyboard is
    /// in the box and nothing is selected. Read from the box's own text rather than from the
    /// model's, which is 400 ms behind it.
    ///
    /// <b>A caret moving through text nobody changed CLOSES the list, since 2026-09-25</b> - owner's
    /// decision P2 of `docs/PROJEKT-PODPOWIEDZI-UX-20260925.md`. Until then one Left arrow in
    /// <c>status:running</c> opened a list of one row saying <c>running</c>, and correcting a query
    /// with the arrows blinked a list at every press. Down still asks for it on purpose.
    ///
    /// <b>Told apart by the text, not by which event arrived</b>, because the box raises two for
    /// one keystroke - its text changed and its selection changed - and the window does not rely
    /// on their order. A new text is typing. The same text with the caret elsewhere is a move. The
    /// same text at the same caret is the second event of a keystroke already followed, and leaves
    /// the list as it is.
    /// </summary>
    public void Follow(string? text, int caret, int selectionLength)
    {
        var sameText = string.Equals(text, _followedText, StringComparison.Ordinal);
        var sameCaret = caret == _followedCaret;

        _followedText = text;
        _followedCaret = caret;

        if (!_keyboardHere || selectionLength > 0 || (sameText && !sameCaret))
        {
            Close();

            return;
        }

        if (!sameText)
        {
            _questions = false;
            Offer(Rows(QueryCompletions.WhileTyping(text, caret)));
        }
    }

    /// <summary>
    /// The window has just written a row into the box. Followed as typing, whatever the box's own
    /// events said on the way.
    ///
    /// <b>Needed because of the rule in <see cref="Follow"/>.</b> The window writes through the
    /// selection and then puts the caret after what it wrote - so the last thing the box reports
    /// is a caret moving through text that did not change, which closes the list. That would take
    /// away the values offered the moment a field is written, and <c>sta</c> Tab Tab giving
    /// <c>status:running</c> is the whole of the Tab decision.
    /// </summary>
    public void Wrote(string? text, int caret)
    {
        _followedText = text;
        _followedCaret = caret;

        if (!_keyboardHere)
        {
            return;
        }

        _questions = false;
        Offer(Rows(QueryCompletions.WhileTyping(text, caret)));
    }

    /// <summary>
    /// Down on a closed list: the same question asked on purpose, so everything that fits is
    /// offered - every field on an empty member, a word already written in full included. An
    /// empty box gives the examples, as arriving at one does. Whether it opened.
    /// </summary>
    public bool Ask(string? text, int caret, int selectionLength)
    {
        if (!_keyboardHere || selectionLength > 0)
        {
            return false;
        }

        _questions = string.IsNullOrWhiteSpace(text);
        Offer(_questions
            ? Examples(text ?? string.Empty)
            : Rows(QueryCompletions.OnRequest(text, caret)));

        return IsOpen;
    }

    /// <summary>
    /// The guide to the language: the ways to write a value as rows, one example each with what it
    /// does, and the fields under them - SearchGuide.xaml draws those from <see cref="QueryGuide"/>.
    /// Asked for by the button at the end of the box or by F1 in it, so the window puts the keyboard
    /// in the box first. An example chosen here goes at the END of whatever the box holds, so the
    /// guide helps somebody halfway through a query as well as somebody starting one - and it is
    /// written through the selection like every row, so Ctrl+Z takes it back.
    ///
    /// <b>The questions to start from are not repeated here</b>, since the second look at the first
    /// build (2026-11-17): with them the list stood about 690 pixels tall on a window whose least
    /// height is 640, and they are one click away on the empty box anyway.
    /// </summary>
    public void Guide(string? text)
    {
        _keyboardHere = true;
        _questions = true;
        Offer(QueryGuide.Rows(text ?? string.Empty), guiding: true);
    }

    /// <summary>
    /// A search made lately was forgotten, so the list is offered again without it - the row that
    /// took its place is chosen, so Delete pressed again goes on down the list as it would in any
    /// list somebody is clearing.
    /// </summary>
    public void Forgot(string? text)
    {
        var at = _chosen is null ? 0 : Math.Max(0, IndexOf(_chosen));

        _questions = true;
        Offer(Examples(text ?? string.Empty));

        if (IsOpen)
        {
            Chosen = _offered[Math.Min(at, _offered.Count - 1)];
        }
    }

    /// <summary>The next row, wrapping at the end. False when there is no list.</summary>
    public bool Next() => Step(+1);

    /// <summary>The previous row, wrapping at the start. False when there is no list.</summary>
    public bool Previous() => Step(-1);

    /// <summary>
    /// The chosen row - the first, when nothing is chosen - and the list closes. Null when there
    /// was no list, so a press that took nothing can be handed back.
    /// </summary>
    public Suggestion? Take()
    {
        if (!IsOpen)
        {
            return null;
        }

        var taken = _chosen ?? _offered[0];

        Close();

        return taken;
    }

    /// <summary>Puts the list away. Whether there was one - Escape needs the answer.</summary>
    public bool Close()
    {
        if (!IsOpen)
        {
            return false;
        }


        _guiding = false;
        Offered = [];
        Chosen = null;
        Raise(nameof(IsOpen));
        Raise(nameof(Guiding));
        Raise(nameof(Caption));
        Raise(nameof(Keys));

        return true;
    }

    private bool Step(int by)
    {
        if (!IsOpen)
        {
            return false;
        }

        var at = _chosen is null ? -1 : IndexOf(_chosen);
        var count = _offered.Count;

        Chosen = _offered[((at + by) % count + count) % count];

        return true;
    }

    /// <summary>
    /// Replaces the list, and keeps the choice by word where the word is still there.
    ///
    /// <b>The order of the two assignments is the behaviour, not housekeeping.</b> The window's
    /// list binds SelectedItem two ways, so replacing its items sets the choice to null through
    /// the binding - and a choice made before the list was replaced would be gone by the time
    /// the list arrived. A list that is the same as the one showing is left alone, so that a
    /// second click into an empty box does not rebuild six rows under the pointer.
    /// </summary>
    private void Offer(IReadOnlyList<Suggestion> rows, bool guiding = false)
    {
        if (rows.Count == 0)
        {
            Close();

            return;
        }

        if (rows.SequenceEqual(_offered) && guiding == _guiding)
        {
            return;
        }

        var wasOpen = IsOpen;
        var kept = _chosen?.Word;

        _guiding = guiding;
        Offered = rows;
        Raise(nameof(Guiding));
        Raise(nameof(Caption));
        Raise(nameof(Keys));
        Chosen = rows.FirstOrDefault(row => string.Equals(row.Word, kept, StringComparison.Ordinal)) ?? rows[0];

        if (!wasOpen)
        {
            Raise(nameof(IsOpen));
        }
    }

    private int IndexOf(Suggestion row)
    {
        for (var index = 0; index < _offered.Count; index++)
        {
            if (_offered[index] == row)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// What an empty box offers: the searches made lately, then the questions that fit the list on
    /// screen, each replacing the whole of the box. A search is left out when it is one of those
    /// questions - the row is there already - or when it no longer reads, which is how a search
    /// written in a field a later build took away would otherwise come back as a row that fails.
    /// </summary>
    private IReadOnlyList<Suggestion> Examples(string text)
    {
        var questions = _examples();
        var recent = (RecentQueries?.Invoke() ?? [])
            .Where(query => !questions.Any(question => string.Equals(question.Query, query, StringComparison.Ordinal))
                && QueryParser.Parse(query).IsValid)
            .Select(query => new Suggestion(query, Texts.Of("gui.suggest.recent"), 0..text.Length, query, IsRecent: true))
            .ToList();

        return [.. recent, .. Questions(text, heading: recent.Count > 0)];
    }

    /// <summary>The questions that fit the list on screen - the first carrying the name of its group when another group stands over it.</summary>
    private IEnumerable<Suggestion> Questions(string text, bool heading) =>
        _examples().Select((example, at) => new Suggestion(
            example.Query, example.Label, 0..text.Length, example.Query, IsQuestion: true,
            Heading: heading && at == 0 ? Texts.Of(QuestionsCaption) : null));

    private IReadOnlyList<Suggestion> Rows(IReadOnlyList<QueryCompletion> completions) =>
        [.. completions.Select(completion => new Suggestion(
            completion.Word, Meanings.Of(completion, _chips()), completion.Replaces, completion.Written))];
}
