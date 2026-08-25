using Bws.Core;
using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// Everything the window says about an answer.
///
/// <b>The fourth seam the size ratchet asked MainViewModel for, and the one the code had been
/// asking for longer than that.</b> Four sentences live here and they are four different jobs:
/// the count beside the box, what the answer has to admit about itself, what is wrong with the
/// question, and what the middle of an empty list says. Deciding which of them to show is
/// reasoning about words, and everything else in the view model is about rows, queries and
/// threads.
///
/// <b>It holds the sentences. <see cref="Sentences"/> composes them and <see cref="ListState"/>
/// works out which one applies.</b> Three files rather than one because they fail differently -
/// a wrong sentence is a translation problem, a wrong choice of sentence is a logic problem, and
/// a sentence appearing at the wrong moment is a notification problem.
/// </summary>
public sealed class Says : Observable
{
    private string _status = Texts.Of("gui.status.reading");
    private string _notice = string.Empty;
    private string _problem = string.Empty;
    private string _refusal = string.Empty;
    private string _layout = string.Empty;
    private bool _incomplete;
    private bool _narrowed;
    // The state a window is in before it has read anything: the first reading is still out, so the
    // scope facts cannot say anything yet and do not have to - Loading is decided before any of
    // them is looked at.
    private ListState _list = ListState.Of(
        firstLook: true,
        failed: false,
        shown: 0,
        everything: 0,
        inScope: 0,
        scope: Scopes.Opening,
        askedElsewhere: false);

    /// <summary>
    /// The count, beside the box that changes it.
    ///
    /// <b>It moved out from under the list on 2026-08-05 and stopped being grey.</b> Complaint 8
    /// of the eleven in `docs/11`: it was the least visible thing on the screen and is the main
    /// answer to every keystroke. Never empty - "reading" is a state worth showing.
    /// </summary>
    public string Status
    {
        get => _status;
        internal set => Set(ref _status, value);
    }

    /// <summary>
    /// What this result has to admit about itself: entries judged on something nobody could
    /// read, entries never judged at all, questions about data this window has not read, and
    /// a list holding still because somebody is using it.
    ///
    /// Empty when there is nothing to admit, which is the ordinary case. Separate from
    /// <see cref="Problem"/> because these are facts about the answer and that one is a fact
    /// about the question.
    /// </summary>
    public string Notice
    {
        get => _notice;
        internal set => Set(ref _notice, value);
    }

    /// <summary>
    /// What is wrong with what was asked for - a query that will not parse, or something the
    /// window tried and could not do. Empty while it reads.
    ///
    /// A mistake in a query leaves the list alone - `docs/07` - so this is the only sign that
    /// the box and the list have stopped agreeing, and it has to be visible.
    ///
    /// <b>An action's refusal wins over a query's, and outlives a tick.</b> Both would otherwise
    /// be written whenever anything on the machine moves, so a copy that failed would announce
    /// itself and be gone within the second, on a busy machine before anybody read it. It clears
    /// when the person asks for something else.
    ///
    /// The right home for a finished action's result is a transient one, and WPF UI has a
    /// Snackbar for it - `docs/10` section 4. This line is where it goes until there is a slice
    /// that puts one in.
    /// </summary>
    public string Problem => _refusal.Length > 0 ? _refusal
        : _problem.Length > 0 ? _problem
        : _layout;

    /// <summary>
    /// Whether the reading admitted to gaps. Shown, never swallowed.
    ///
    /// Rule 8 in the window: a listing that quietly dropped what it could not read looks
    /// complete, and looking complete is exactly what makes it dangerous.
    ///
    /// About the reading, not about the query. A result the query could only half answer says
    /// so in <see cref="Notice"/> - folding the two together would put "the machine refused"
    /// and "this filter had nothing to go on" behind one flag.
    /// </summary>
    public bool Incomplete
    {
        get => _incomplete;
        internal set => Set(ref _incomplete, value);
    }

    /// <summary>
    /// What the middle of the window says when it has no rows, and the one way out of it.
    ///
    /// Empty whenever there are rows, which is how the markup knows to show nothing - the same
    /// shape <see cref="Notice"/> and <see cref="Problem"/> already use.
    /// </summary>
    public string ListMessage => _list.Message;

    /// <summary>The way out, or empty when there is none to offer. `docs/11` 9.2, Nielsen's third.</summary>
    public string ListWayOut => _list.WayOut;

    /// <summary>
    /// Whether a query is holding entries back. Complaint 7 - "you cannot see THAT it is
    /// filtering" - and the count line wears it as a weight, which is seen without being read.
    /// </summary>
    public bool Narrowed => _narrowed;

    /// <summary>Which of the five, for a test that wants to name it rather than read its words.</summary>
    internal ListFace Face => _list.Face;

    /// <summary>
    /// Whether this session has administrator rights. Asked once, because it cannot change while
    /// the window is open.
    ///
    /// <b>It lives here because it only ever matters as a SENTENCE.</b> Nothing about the rows,
    /// the query or the reading changes with elevation - what changes is that the list is short
    /// and somebody has to be told. Settable so a test can have the other answer, which a working
    /// session cannot produce on demand.
    /// </summary>
    internal bool Elevated { get; init; } = Session.IsElevated();

    /// <summary>
    /// The same fact the other way round, because markup can only ask for what is public and can
    /// only test for a value it is given.
    ///
    /// <b>It exists for the one control in this window that offers a way out of the state</b> - the
    /// button beside the sentence about it. Binding that button to <see cref="Notice"/> would show
    /// it whenever the window admitted anything at all, and most of what that line says has nothing
    /// to do with rights.
    /// </summary>
    public bool NotElevated => !Elevated;

    /// <summary>
    /// Everything this answer has to admit about itself, composed and stored in one step.
    ///
    /// The composing lives in <see cref="Sentences"/>, the deciding in the view model, and the
    /// holding here - so the view model never has to know that elevation is one of the things
    /// worth saying.
    /// </summary>
    internal void AboutTheAnswer(
        Query query, bool held, int unreadable, int tooCostly, ExtraRead have, bool filling) =>
        Notice = Sentences.Admissions(query, held, unreadable, tooCostly, Elevated, have, filling);

    /// <summary>
    /// Something the window tried on the person's behalf and could not do.
    ///
    /// Rule 8 in a place it is easy to think does not apply: an action that quietly did nothing
    /// leaves somebody believing it did. The clipboard is the live example - it belongs to
    /// whichever process grabbed it last, so copying genuinely fails on a working machine.
    /// </summary>
    public void CouldNotDo(string because)
    {
        _refusal = Texts.Of("gui.status.couldNotDo", because);

        Raise(nameof(Problem));
    }

    /// <summary>
    /// What a kept column layout could not give the window, said once at startup.
    ///
    /// <b>The quietest of the three, and last in the order for that reason.</b> A query problem is
    /// about what somebody is doing right now and an action's refusal is about what just failed -
    /// both are newer news than a file read before the window appeared. It outlives the first tick
    /// on purpose: the reading finishes in half a second, and a sentence gone by then is a
    /// sentence nobody was given.
    ///
    /// Rule 8 in the one place a window can break it without anything looking wrong. A layout half
    /// applied looks exactly like a layout somebody misremembers arranging.
    /// </summary>
    internal void AboutTheLayout(string sentence)
    {
        _layout = sentence;

        Raise(nameof(Problem));
    }

    /// <summary>Puts away what the last action could not do, because the person asked for something else.</summary>
    internal void Moved()
    {
        if (_refusal.Length == 0 && _layout.Length == 0)
        {
            return;
        }

        _refusal = string.Empty;

        // The layout note goes with it. Somebody who has started typing a query has been in the
        // window long enough to have read a line that was there when it opened, and a startup
        // admission still standing an hour later reads as though it just happened.
        _layout = string.Empty;

        Raise(nameof(Problem));
    }

    /// <summary>What is wrong with the query, which may be nothing. Silent while an action's refusal stands.</summary>
    internal void AboutTheQuery(string problem)
    {
        if (_problem == problem)
        {
            return;
        }

        _problem = problem;

        Raise(nameof(Problem));
    }

    /// <summary>
    /// Works out what the middle of the window says, and says it changed.
    ///
    /// Called from everywhere the answer could have moved rather than from one place, because
    /// the four facts it reads move at different times: a reading starting, a reading failing,
    /// a query narrowing the list, the machine handing over nothing.
    /// </summary>
    internal void AboutTheList(
        bool firstLook, bool failed, int shown, int everything, int inScope, EntryScope scope, bool askedElsewhere)
    {
        var list = ListState.Of(firstLook, failed, shown, everything, inScope, scope, askedElsewhere);
        var narrowed = shown != everything;

        // SAID ONLY WHEN IT MOVED, SINCE 2026-08-13. This runs on every keystroke and on every
        // tick, which is once a second for as long as the window is open - so an unconditional
        // announcement is three bindings re-read about eighty thousand times an hour to be told
        // nothing changed. A record struct compares by value, so this costs one comparison.
        //
        // It is not only waste: WPF re-evaluates the triggers hanging off these, and a person
        // watching a sentence that is being reassigned every second is the complaint this went in
        // with.
        if (_list == list && _narrowed == narrowed)
        {
            return;
        }

        _list = list;
        _narrowed = narrowed;

        Raise(nameof(ListMessage));
        Raise(nameof(ListWayOut));
        Raise(nameof(Narrowed));
    }
}
