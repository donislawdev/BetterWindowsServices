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
public sealed record Suggestion(string Word, string Meaning, Range Replaces, string Written, bool IsQuestion = false)
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

    // Constants rather than literals in the expression below, because TextKeyGuards knows the
    // shapes a key is used in and a literal inside a conditional is not one of them.
    private const string QuestionsCaption = "gui.suggest.caption.questions";
    private const string WordsCaption = "gui.suggest.caption.words";

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
        set => Set(ref _chosen, value);
    }

    /// <summary>Whether there is a list to show. Nothing offered means nothing open.</summary>
    public bool IsOpen => _offered.Count > 0;

    /// <summary>
    /// The sentence under the list saying which keys do what - under the list rather than in the
    /// tooltip, because under the list is where somebody is looking while the keys matter.
    /// Decision 9 of the design.
    /// </summary>
    public string Keys => Texts.Of("gui.suggest.keys");

    /// <summary>
    /// What kind of list is open, said over it: the questions on an empty box, or what can go where
    /// the caret is. Empty while nothing is open. Five rows of syntax with no sentence about what
    /// they were was half of "unintuitive" - the owner's fifth remark of 2026-09-16.
    /// </summary>
    public string Caption => !IsOpen
        ? string.Empty
        : Texts.Of(_questions ? QuestionsCaption : WordsCaption);

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
    /// The text or the caret moved. The list follows - and opens only while the keyboard is in
    /// the box and nothing is selected. Read from the box's own text rather than from the
    /// model's, which is 400 ms behind it.
    /// </summary>
    public void Follow(string? text, int caret, int selectionLength)
    {
        if (!_keyboardHere || selectionLength > 0)
        {
            Close();

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


        Offered = [];
        Chosen = null;
        Raise(nameof(IsOpen));
        Raise(nameof(Caption));

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
    private void Offer(IReadOnlyList<Suggestion> rows)
    {
        if (rows.Count == 0)
        {
            Close();

            return;
        }

        if (rows.SequenceEqual(_offered))
        {
            return;
        }

        var wasOpen = IsOpen;
        var kept = _chosen?.Word;

        Offered = rows;
        Raise(nameof(Caption));
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

    /// <summary>The questions that fit the list on screen, each replacing the whole of an empty box.</summary>
    private IReadOnlyList<Suggestion> Examples(string text) =>
        [.. _examples().Select(example => new Suggestion(example.Query, example.Label, 0..text.Length, example.Query, IsQuestion: true))];

    private IReadOnlyList<Suggestion> Rows(IReadOnlyList<QueryCompletion> completions) =>
        [.. completions.Select(completion => new Suggestion(
            completion.Word, Meanings.Of(completion, _chips()), completion.Replaces, completion.Written))];
}

/// <summary>A word of the language and the key of the sentence that stands beside it in the list.</summary>
internal sealed record Meaning(string Word, string Key);

/// <summary>
/// The sentence beside each word the list can offer.
///
/// <b>Three sources, and which one is the decision rather than the mechanism.</b> A field gets a
/// sentence of its own, because a field name is what a stranger cannot guess and the four that
/// cost a second reading have to say so before somebody writes them - decision 11 of the design.
/// A reserved word gets one of three. A value gets the label of the chip that stands for it,
/// where a chip exists, and nothing where none does: the values are the words <c>sc.exe</c>
/// prints, and a sentence for each of forty-five would be forty-five more keys to keep true
/// (decision 3).
///
/// <b>THE KEY IS WRITTEN BESIDE THE WORD RATHER THAN ASSEMBLED FROM IT</b>, and the reason is
/// <c>TextKeyGuards</c>: a key put together at run time is a key nothing can find by reading, so
/// a sentence missing from the language file would reach the screen as the key itself -
/// <c>Texts.Of</c> answers a missing key with the key. Written here, the guard reads every one of
/// them, and <c>SuggestionGuards</c> holds that every field of the language has a row in this
/// table and every row names a field that exists.
/// </summary>
internal static class Meanings
{
    internal static readonly IReadOnlyList<Meaning> Fields =
    [
        new Meaning("name", "gui.suggest.field.name"),
        new Meaning("display", "gui.suggest.field.display"),
        new Meaning("description", "gui.suggest.field.description"),
        new Meaning("type", "gui.suggest.field.type"),
        new Meaning("peruser", "gui.suggest.field.peruser"),
        new Meaning("status", "gui.suggest.field.status"),
        new Meaning("start", "gui.suggest.field.start"),
        new Meaning("account", "gui.suggest.field.account"),
        new Meaning("pid", "gui.suggest.field.pid"),
        new Meaning("trigger", "gui.suggest.field.trigger"),
        new Meaning("path", "gui.suggest.field.path"),
        new Meaning("file", "gui.suggest.field.file"),
        new Meaning("signed", "gui.suggest.field.signed"),
        new Meaning("publisher", "gui.suggest.field.publisher"),
        new Meaning("privilege", "gui.suggest.field.privilege"),
        new Meaning("dependson", "gui.suggest.field.dependson"),
        new Meaning("requiredby", "gui.suggest.field.requiredby"),
        new Meaning("sidtype", "gui.suggest.field.sidtype"),
        new Meaning("sddl", "gui.suggest.field.sddl"),
        new Meaning("memory", "gui.suggest.field.memory"),
        new Meaning("mismatch", "gui.suggest.field.mismatch")
    ];

    internal static readonly IReadOnlyList<Meaning> Words =
    [
        new Meaning(QueryFields.Any, "gui.suggest.word.any"),
        new Meaning(QueryFields.None, "gui.suggest.word.none"),
        new Meaning(QueryFields.Unreadable, "gui.suggest.word.unreadable")
    ];

    /// <summary>The sentence for one completion, or nothing where nothing has one.</summary>
    internal static string Of(QueryCompletion completion, IReadOnlyList<FilterChip> chips) =>
        completion.Kind switch
        {
            QueryCompletionKind.Field => Sentence(Fields, completion.Word),
            QueryCompletionKind.Word => Sentence(Words, completion.Word),
            _ => chips
                .FirstOrDefault(chip => !chip.Negated
                    && string.Equals(chip.Field, completion.Field, StringComparison.Ordinal)
                    && string.Equals(chip.Value, completion.Word, StringComparison.Ordinal))
                ?.Label ?? string.Empty
        };

    private static string Sentence(IReadOnlyList<Meaning> table, string word)
    {
        var meaning = table.FirstOrDefault(entry => string.Equals(entry.Word, word, StringComparison.Ordinal));

        return meaning is null ? string.Empty : Texts.Of(meaning.Key);
    }
}
