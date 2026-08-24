using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// Which kind of SCM entry the window is listing.
///
/// <b>The words are the glossary's, not invented here.</b> `docs/03` trap P6 settles that "entry"
/// is the word for both kinds and that "service" must never be used as the collective one - the
/// sentence "bulk operations work on all services" becomes dangerously ambiguous the moment it is.
/// So the two named members are the two kinds, and the third is spelled Everything rather than All
/// Services.
/// </summary>
public enum EntryScope
{
    /// <summary>Own-process and shared-process entries. What <c>services.msc</c> shows.</summary>
    Services,

    /// <summary>Kernel and file system drivers. Both kinds, per `docs/07` - <c>type:driver</c>.</summary>
    Drivers,

    /// <summary>Both at once, which is the whole of what the manager holds.</summary>
    Everything
}

/// <summary>
/// One position of the scope switch.
///
/// <b>THIS HAS STATE AND EVERY FILTER CHIP DELIBERATELY DOES NOT, so the departure is written here
/// rather than left for somebody to find.</b> <see cref="FilterChip"/> holds nothing: whether it is
/// lit is a question asked of the query text, so a person typing <c>status:stopped</c> by hand
/// watches the chip light up on its own. That doctrine is right for a FILTER and it was right for
/// the drivers switch while the drivers switch was one.
///
/// <b>Scope is not a filter, and the owner settled that on 2026-08-19.</b> It decides WHICH LIST is
/// being asked, and the question is then asked inside it - so <c>type:</c> in the box stops
/// choosing between services and drivers and starts narrowing within whichever is on screen.
/// <c>type:kernelDriver</c> typed while Drivers is showing is a narrowing, not a contradiction.
/// This overturns `docs/07` part 429, which said the drivers switch "IS this member and is not a
/// state beside the query" - that document has been rewritten rather than left disagreeing.
///
/// <b>What the old arrangement bought and what it now costs, said plainly:</b> a switch that was
/// text could not disagree with the text, so no conflict could exist. A switch with state can:
/// asking for Services while the box says <c>type:driver</c> selects nothing. That state is real,
/// it is reachable, and the window has to say so rather than showing an empty list - rule 8.
/// </summary>
public sealed class ScopeChoice : Observable
{
    private readonly Func<EntryScope> _read;
    private readonly Action<EntryScope> _write;
    private readonly string _labelKey;

    internal ScopeChoice(string labelKey, EntryScope scope, Func<EntryScope> read, Action<EntryScope> write)
    {
        _labelKey = labelKey;
        _read = read;
        _write = write;

        Scope = scope;
    }

    /// <summary>The scope this position stands for.</summary>
    public EntryScope Scope { get; }

    /// <summary>What the position says, in the language of whoever is reading it.</summary>
    public string Label => Texts.Of(_labelKey);

    /// <summary>
    /// Whether this is the position the window is on.
    ///
    /// <b>The setter ignores false on purpose, and it is not defensive tidiness.</b> These are drawn
    /// as radio buttons, and a radio button group tells the OLD member it is now unchecked before it
    /// tells the new one it is checked. Acting on that false would mean writing a scope on the way
    /// out of one - and there is no such thing as "no scope", so the window would have to invent
    /// one. Only the arrival carries information.
    /// </summary>
    public bool IsOn
    {
        get => _read() == Scope;

        set
        {
            if (value)
            {
                _write(Scope);
            }

            Rethink();
        }
    }

    /// <summary>
    /// Asks this position to look at the scope again.
    ///
    /// Called for every position whenever the scope moves, for the same reason every chip is asked
    /// after every edit: one change moves two of them, and a position listening only to its own
    /// click would stay lit beside the one that took over from it.
    /// </summary>
    internal void Rethink()
    {
        Raise(nameof(IsOn));
        Raise(nameof(Label));
    }
}

/// <summary>
/// The scope switch - which positions there are, and what each one means to the query language.
///
/// <b>WHAT A SCOPE IS, EXPRESSED AS A QUERY RATHER THAN AS A PREDICATE, and this is the one design
/// decision in this file worth defending.</b> "Which entries are drivers" is already answered once,
/// by the language, and `docs/07` records that <c>type:driver</c> deliberately covers BOTH kinds
/// because "Windows has two kinds of driver and no word for both". A hand-written check on
/// <see cref="Bws.Core.EntryType"/> here would be a second answer to that question, living in the
/// window, drifting from the first without a sound - which is the exact failure
/// <see cref="FilterChip"/> already names about scanning text.
///
/// So a scope IS a query, the window narrows by it first and by what somebody typed second, and
/// there is still only one place that decides what a driver is.
/// </summary>
public static class Scopes
{
    /// <summary>The field and value the scope switch stands for, spelled once.</summary>
    internal const string DriverField = "type";

    internal const string DriverValue = "driver";

    /// <summary>
    /// What the window opens on - owner's decision, 2026-08-13, unchanged by the switch arriving.
    ///
    /// <b>Because <c>services.msc</c> does, and that is the tool people will compare this
    /// against.</b> Measured on this machine on 2026-08-19: drivers are 472 of 812 entries, so a
    /// window opening on Everything opens with more than half its list being things almost nobody
    /// came to look at.
    ///
    /// <b>What CHANGED is where that default lives, and it is the whole point of the switch.</b> It
    /// used to be text in the search box - <c>!type:driver</c> sitting there before anybody typed -
    /// because a filter nothing on screen admits to breaks rule 8 with the first thing a person
    /// sees. The switch admits to it in a better place: the box now opens EMPTY, and the position
    /// the window is standing on says which list this is.
    /// </summary>
    internal const EntryScope Opening = EntryScope.Services;

    /// <summary>
    /// The query that selects a scope, written the way it would be typed.
    ///
    /// <b>Everything is the empty string rather than a member that happens to select all.</b> There
    /// is no member meaning "both kinds" - <c>type:driver</c> and its negation are the two halves -
    /// and inventing one would put a term in the composed query that narrows nothing while costing
    /// a pass over the listing.
    /// </summary>
    internal static string QueryFor(EntryScope scope) => scope switch
    {
        EntryScope.Services => QueryMembers.Member(DriverField, DriverValue, negated: true),
        EntryScope.Drivers => QueryMembers.Member(DriverField, DriverValue, negated: false),
        EntryScope.Everything => string.Empty,

        // NOT A default THAT PICKS ONE, and the reason is written at `S7`'s analysis of packet 3: a
        // switch whose default arm means something is a silent wrong answer for every value added
        // later. A fourth scope must fail here, loudly, on the first run.
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "No query is written for this scope.")
    };

    /// <summary>
    /// Whether the query asks for entries this scope leaves out - the two halves working correctly
    /// and excluding each other.
    ///
    /// <b>Asked of the LANGUAGE rather than of a scanner written here</b>, which is the rule this
    /// whole file follows: <see cref="QueryMembers.Carries"/> is what decides whether a member is in
    /// a line of text, it copes with quotes and with case, and a second reader living in the window
    /// would drift from it in silence. It is the same call the filter chips use to light themselves.
    ///
    /// <b>What this catches and what it does NOT, said rather than discovered.</b> It catches the
    /// two contradictions that are the scope's own member turned against it - asking for drivers on
    /// the services list and the reverse. It does NOT catch <c>type:ownProcess</c> typed on the
    /// drivers list, which is the same fault a step further out: that one falls through to "nothing
    /// in this list matches", which is true, just less helpful. Widening it means teaching this
    /// method which of the six type values belong to which scope, and that is a second definition
    /// of what a driver is - the one thing the rest of this file refuses to write down twice.
    /// </summary>
    internal static bool AsksElsewhere(EntryScope scope, string query) => scope switch
    {
        EntryScope.Services => QueryMembers.Carries(query, DriverField, DriverValue, negated: false),
        EntryScope.Drivers => QueryMembers.Carries(query, DriverField, DriverValue, negated: true),

        // Everything leaves nothing out, so nothing can be asked for elsewhere.
        EntryScope.Everything => false,

        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "No contradiction is defined for this scope.")
    };

    /// <summary>The positions, in the order they are shown.</summary>
    internal static IReadOnlyList<ScopeChoice> Positions(Func<EntryScope> read, Action<EntryScope> write) =>
    [
        // SERVICES FIRST BECAUSE IT IS WHERE THE WINDOW OPENS, and the order is the reading order
        // of the two kinds rather than their size. Drivers are the larger half on this machine and
        // standing them first would put the rarer question in front of the common one.
        new ScopeChoice("gui.scope.services", EntryScope.Services, read, write),
        new ScopeChoice("gui.scope.drivers", EntryScope.Drivers, read, write),

        // EVERYTHING IS KEPT AND IT IS A CAPABILITY RATHER THAN A COURTESY - owner's decision,
        // 2026-08-19. Before the switch, one press of Escape gave back the whole machine, and two
        // scopes would have deleted that: there would be no way left to count what the manager
        // holds, or to see a service beside the driver it depends on, on one screen.
        new ScopeChoice("gui.scope.all", EntryScope.Everything, read, write)
    ];
}

/// <summary>
/// Which list is showing, and the listing already cut down to it.
///
/// <b>Its own class because the size ratchet asked on 2026-08-19, and the seam it found was real</b>
/// - the fifth time <see cref="MainViewModel"/> has given one up the same way, after
/// <see cref="Holding"/>, <see cref="RowIndex"/>, <see cref="Narrowing"/> and
/// <see cref="FilterBar"/>. What stays behind decides what to DO when the scope moves, which needs
/// a selection, a query and a list. What is here is which scope it is and what that leaves.
///
/// <b>THE CUT IS KEPT RATHER THAN RECOMPUTED, and that is a measurement rather than a habit.</b>
/// Narrowing runs on the interface thread on every character typed, against the 50 ms budget in
/// section 8.1 of the specification, and the scope cannot change while somebody types. Recutting
/// per keystroke would pay for a second pass over 812 entries per character, on exactly the path
/// measured and fixed on 2026-08-19.
/// </summary>
internal sealed class Scoping
{
    private readonly Func<IReadOnlyList<EntryRow>> _everything;

    internal Scoping(Func<IReadOnlyList<EntryRow>> everything) => _everything = everything;

    /// <summary>Which list is showing.</summary>
    internal EntryScope Current { get; private set; } = Scopes.Opening;

    /// <summary>The listing with everything outside the scope taken out.</summary>
    internal IReadOnlyList<EntryRow> InScope { get; private set; } = [];

    /// <summary>
    /// Moves to another scope, and says whether that was a move at all.
    ///
    /// <b>Answering rather than doing it silently</b>, because the caller has work that must happen
    /// exactly when this is a real change and must NOT happen otherwise - it lets go of what was
    /// selected. A radio button re-announcing the position it is already on would throw somebody's
    /// selection away for nothing.
    /// </summary>
    internal bool MoveTo(EntryScope scope)
    {
        if (Current == scope)
        {
            return false;
        }

        Current = scope;
        Recut();

        return true;
    }

    /// <summary>
    /// Cuts the listing down to the scope again.
    ///
    /// <b>By asking the query language rather than by testing a type here.</b> What counts as a
    /// driver is decided once, in the language, and `docs/07` records that <c>type:driver</c> covers
    /// both kinds deliberately because Windows has two and no word for both. A check on
    /// <see cref="Bws.Core.EntryType"/> written here would be a second answer to that question,
    /// living in the window and drifting from the first in silence - which is the failure
    /// <see cref="FilterChip"/> already names about scanning text.
    ///
    /// <b>Everything skips the narrowing rather than running an empty query over 812 rows</b>, which
    /// is the one case where the answer is known without asking.
    /// </summary>
    internal void Recut()
    {
        var everything = _everything();
        var scoping = Scopes.QueryFor(Current);

        if (scoping.Length == 0)
        {
            InScope = everything;

            return;
        }

        // Finished rather than BeingTyped: this text is ours and complete by construction, so there
        // is no half-written member to forgive. A complaint here would be a fault in this file
        // rather than in something somebody typed.
        var parsed = QueryParser.Parse(scoping, input: QueryInput.Finished);

        InScope = Narrowing.Of(parsed.Query!, everything).Selected;
    }
}
