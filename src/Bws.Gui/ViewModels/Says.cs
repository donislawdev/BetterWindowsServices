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

    private string _queryProblem = string.Empty;
    private bool _asking;
    private string _refusal = string.Empty;
    private string _layout = string.Empty;
    private bool _incomplete;
    private bool _narrowed;
    private bool _partial;
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
        askedElsewhere: false,
        partial: false);

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
    /// What this result has to admit about itself: the session's rights, entries judged on
    /// something nobody could read, entries never judged at all, questions about data this window
    /// has not read, folded instances, and a list holding still because somebody is using it.
    ///
    /// Empty when there is nothing to admit, which is the ordinary case. The whole of it in one
    /// string, in the order it has always been said - the screen draws it in two places since
    /// 2026-09-23 (<see cref="Admitted"/>), and this is what a test or a reader asks when the
    /// question is what the window admits rather than where it says it.
    /// </summary>
    public string Notice => _admitted.Notice;

    /// <summary>
    /// The line under the list, in the four pieces the view draws it in - the rights sentence, the
    /// words before the link, the link, and the words after. Point 8(d) of `docs/11` 2.14: the
    /// sentence about folded instances names the switch, and the name is the switch.
    /// <see cref="Admitted"/> says why these are one record and not five strings.
    /// </summary>
    public string NoticeRights => _admitted.RightsPiece;

    public string NoticeBeforeLink => _admitted.BeforeLink;

    public string NoticeLink => _admitted.Link;

    public string NoticeAfterLink => _admitted.AfterLink;

    /// <summary>The line under the list whole - what decides whether it takes any room.</summary>
    public string NoticeLine => _admitted.Line;

    /// <summary>Whether the line has a link in it at all - what enables the link, so an empty one is never a Tab stop.</summary>
    public bool NoticeHasLink => _admitted.HasLink;

    /// <summary>
    /// What this answer could not judge, has not read yet, or ran out of time on - said under the
    /// search box since 2026-09-23, because each of them is about the question asked there.
    /// </summary>
    public string Reservations => _admitted.Reservations;

    private Admitted _admitted = Admitted.Nothing;

    private void Admit(Admitted admitted)
    {
        if (admitted == _admitted)
        {
            return;
        }

        _admitted = admitted;

        Raise(nameof(Notice));
        Raise(nameof(NoticeRights));
        Raise(nameof(NoticeBeforeLink));
        Raise(nameof(NoticeLink));
        Raise(nameof(NoticeAfterLink));
        Raise(nameof(NoticeLine));
        Raise(nameof(NoticeHasLink));
        Raise(nameof(Reservations));
        RaiseTheAnswerLine();
    }

    /// <summary>
    /// What is wrong with the query in the box, and that the list under it is the previous answer -
    /// or empty when the query reads.
    ///
    /// <b>Its own sentence since 2026-09-23, under the box - UX-GUI-002 of the audit that day.</b> It
    /// shared a line with an action's refusal at the foot of the window, about 830 pixels below the
    /// box, and the box itself carried no mark. A mistake in a query leaves the list alone -
    /// `docs/07`, and a test holds that decision - so this sentence is the only sign that the box
    /// and the list have stopped agreeing, and the second half of it says exactly that.
    /// </summary>
    public string QueryProblem => _queryProblem;

    /// <summary>
    /// The line under the search box: what is wrong with the query, or else what the answer to it
    /// has to admit.
    ///
    /// <b>One line rather than two, and the mistake wins</b> - a query that does not read has no
    /// answer of its own to qualify, and the list under it is the previous one.
    /// </summary>
    public string AnswerLine => _queryProblem.Length > 0 ? _queryProblem : _admitted.Reservations;

    /// <summary>Whether <see cref="AnswerLine"/> is a mistake - what colours it.</summary>
    public bool AnswerLineIsProblem => _queryProblem.Length > 0;

    /// <summary>
    /// Whether the line under the box takes any room: while there is text in the box, or while
    /// the line has something to say.
    ///
    /// <b>Not always, because the window's chrome already takes nearly half its height</b>
    /// (UX-GUI-007). <b>Not only while it has words, because then the list would jump</b> each time a
    /// reading note came and went under somebody's typing. Tied to the box instead, the line appears
    /// with the first character and goes when the box is emptied - a move the person made.
    /// </summary>
    public bool AnswerLineShown => _asking || AnswerLine.Length > 0;

    private void RaiseTheAnswerLine()
    {
        Raise(nameof(AnswerLine));
        Raise(nameof(AnswerLineIsProblem));
        Raise(nameof(AnswerLineShown));
    }

    /// <summary>
    /// Something the window tried and could not do, or what a kept column layout could not give it.
    /// Empty while there is neither.
    ///
    /// <b>A mistake in the query is NOT here since 2026-09-23</b> - it moved under the box, where
    /// <see cref="QueryProblem"/> says it. What is left is news about the WINDOW rather than about the
    /// question, and it stays at the foot of it.
    ///
    /// <b>An action's refusal wins over the layout note, and outlives a tick.</b> It would otherwise
    /// be written whenever anything on the machine moves, so a copy that failed would announce
    /// itself and be gone within the second, on a busy machine before anybody read it. It clears
    /// when the person asks for something else.
    ///
    /// The right home for a finished action's result is a transient one, and WPF UI has a
    /// Snackbar for it - `docs/10` section 4. This line is where it goes until there is a slice
    /// that puts one in.
    /// </summary>
    public string Problem => _refusal.Length > 0 ? _refusal : _layout;

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
    /// What the MACHINE READING is doing, for a screen that has no list on it to say it for.
    ///
    /// <b>Backlog 263. Two of the list's faces are about the reading and the rest are about the
    /// query</b>, and only the first two mean anything where there are no rows. "Nothing matched"
    /// on the machine overview would be a sentence about a list nobody can see - the same fault
    /// that screen was repaired for, arriving from the other direction.
    ///
    /// <b>Why this is not <see cref="ListMessage"/> with a screen check bolted on:</b> the question
    /// is not which screen is up, it is which SUBJECT the sentence is about. A reading that failed
    /// is worth saying on any screen, and a query that matched nothing is worth saying only where
    /// the query and its result are both visible.
    ///
    /// <b>And it is not <see cref="Status"/> either, deliberately.</b> That line carries the count
    /// as well, and a count is about the list the overview is standing in front of rather than
    /// about the numbers on it - the two are different sets, so putting it here would answer a
    /// question nobody asked with a number that does not match the ones above it.
    /// </summary>
    public string ReadingMessage => _list.Face is ListFace.Loading or ListFace.Failed
        ? _list.Message
        : string.Empty;

    /// <summary>
    /// Which of the two <see cref="ReadingMessage"/> is saying, so a screen can colour them apart.
    ///
    /// <b>A second member rather than a colour decided in the markup</b>, for the reason
    /// <see cref="NotElevated"/> gives about its own pair: markup can only test for a value it is
    /// given. And the two are not the same news - "still reading" is ordinary and passes, "could
    /// not read" is the failure rule 8 forbids a window to say quietly, so the difference has to
    /// reach the screen rather than stay in a sentence somebody has to finish reading.
    /// </summary>
    public bool ReadingFailed => _list.Face is ListFace.Failed;

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
    /// <remarks>
    /// It also keeps whether any entry could not be judged, for the middle of an empty list - which
    /// is decided in <see cref="AboutTheList"/>, called straight after this and also on its own.
    ///
    /// <b>The narrowed answer rather than its two counts, since 2026-09-23</b> - the shape guard
    /// counted this method among those standing near the ceiling of parameters, and the two counts
    /// are one fact that already travels as one value.
    /// </remarks>
    internal void AboutTheAnswer(
        ExtraRead needs, bool held, Narrowed answer, ExtraRead have, bool filling,
        int folded, bool listOnScreen)
    {
        _partial = answer.Unreadable > 0;

        Admit(Sentences.Admissions(
            needs, held, answer.Unreadable, answer.TooCostly, Elevated, have, filling, folded, listOnScreen));
    }

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
    /// <b>The quieter of the two, and second in the order for that reason.</b> An action's refusal is
    /// about what just failed - newer news than a file read before the window appeared. (A query
    /// problem stood in this order too until 2026-09-23, and has a line of its own now.) It outlives the first tick
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

    /// <summary>
    /// What is wrong with the query in the box, which may be nothing, and whether the box holds
    /// anything at all. Answers whether the sentence changed, so the caller can tell the box
    /// without telling it once a second for nothing.
    ///
    /// <b>The second half of the sentence is written here rather than by the caller</b>, because it
    /// is true of every mistake: the list stays, so it is the previous answer.
    /// </summary>
    internal bool AboutTheQuery(string text, string problem)
    {
        var sentence = problem.Length == 0
            ? string.Empty
            : problem + " " + Texts.Of("gui.query.listIsPrevious");

        var asking = text.Length > 0;

        if (_queryProblem == sentence && _asking == asking)
        {
            return false;
        }

        var changed = _queryProblem != sentence;

        _queryProblem = sentence;
        _asking = asking;

        Raise(nameof(QueryProblem));
        RaiseTheAnswerLine();

        return changed;
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
        var list = ListState.Of(firstLook, failed, shown, everything, inScope, scope, askedElsewhere, _partial);
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
        Raise(nameof(ReadingMessage));
        Raise(nameof(ReadingFailed));
        Raise(nameof(Narrowed));
    }
}
