using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// One clickable filter: a control that stands for one member of the query.
///
/// <b>`A5`, and the promise is teaching rather than convenience.</b> Somebody clicks "stopped"
/// and watches <c>status:stopped</c> appear in the box they can type into - so the language gets
/// learned by using the tool instead of by reading about it. That only works while the chip and
/// the text are the same fact seen twice, which is why nothing here holds state.
///
/// <b>No state of its own, exactly like the switch it replaces.</b> Whether a chip is on is a
/// question asked of the query text, so somebody who types <c>status:stopped</c> by hand watches
/// the chip light up on its own, and somebody who deletes it watches it go out. A chip with a
/// boolean inside it would be a second copy of the answer, and the two would disagree the first
/// time anybody edited the box.
///
/// <b>Where the work happens: <see cref="QueryMembers"/> in the core, not here.</b> Finding a
/// member in text somebody typed is the language's job - the interface asking for it is the same
/// arrangement the drivers switch had, and the reason is that a second scanner living in the
/// window would drift from the real one without a sound.
/// </summary>
public sealed class FilterChip : Observable
{
    private readonly Func<string> _read;
    private readonly Action<string> _write;
    private readonly string _labelKey;

    internal FilterChip(
        string labelKey, string field, string value, bool negated, Func<string> read, Action<string> write)
    {
        _labelKey = labelKey;
        _read = read;
        _write = write;

        Field = field;
        Value = value;
        Negated = negated;
    }

    /// <summary>The field this chip constrains, in the language's own spelling.</summary>
    public string Field { get; }

    /// <summary>The value it constrains it to, as a person would write it.</summary>
    public string Value { get; }

    /// <summary>Whether it excludes rather than selects. Two chips, not one with a sign.</summary>
    public bool Negated { get; }

    /// <summary>What the chip says, in the language of whoever is reading it.</summary>
    public string Label => Texts.Of(_labelKey);

    /// <summary>
    /// The member this chip stands for, written the way it appears in the box.
    ///
    /// Shown to the person as the chip's tooltip, because seeing the member before clicking is
    /// half of what `A5` is for - the other half is watching it arrive in the box.
    /// </summary>
    public string Member => QueryMembers.Member(Field, Value, Negated);

    /// <summary>
    /// Whether the query already carries this member - read from the text, never remembered.
    ///
    /// Setting it edits the text and then says the property changed <b>unconditionally</b>,
    /// because nothing was stored: if the edit did not take, the chip reads the query again and
    /// goes back to where it was, which is the honest answer rather than a control showing a
    /// state the query does not agree with.
    /// </summary>
    public bool IsOn
    {
        get => QueryMembers.Carries(_read(), Field, Value, Negated);

        set
        {
            _write(value
                ? QueryMembers.With(_read(), Field, Value, Negated)
                : QueryMembers.Without(_read(), Field, Value, Negated));

            Rethink();
        }
    }

    /// <summary>
    /// Asks the chip to look at the query again.
    ///
    /// Called for every chip whenever the text changes, because one edit can move several of
    /// them - clearing the box turns them all off at once, and a chip that only listened to its
    /// own click would stay lit over a query that no longer says anything about it.
    /// </summary>
    internal void Rethink()
    {
        Raise(nameof(IsOn));
        Raise(nameof(Label));
    }
}

/// <summary>
/// Which chips there are, and why these.
///
/// <b>Not the cross product.</b> The four enumerations behind these accept twenty-nine values
/// between them, and a row of twenty-nine controls is a worse instrument than the search box it
/// sits above - `docs/11` opens with eleven complaints about this window and the first of them is
/// that it was a table rather than something to search with. So what is here is the questions an
/// administrator actually arrives with.
///
/// <b>Every chip stands for a member the language really has, and a test proves it.</b> A chip
/// carrying a typo would compose a query that selects nothing and say nothing about why - the
/// exact silence rule 8 forbids, arriving through a control rather than through a reading.
///
/// <b>WHAT `A5` PROMISES AND WHAT IS HERE, because the difference is measured rather than
/// chosen.</b> It names eight families: status, start type, account, service type, delayed
/// auto-start, trigger-start, signature, and whether there are dependencies.
///
///   status, start type, service type, delayed, trigger  - here, all expressible today
///   signature                                           - the reading is second phase and the
///                                                         window has no second phase yet, so a
///                                                         chip for it would filter on something
///                                                         nobody looked at. Backlog 21
///   account                                             - a text field with an open set of
///                                                         values. Chips need a closed one, and
///                                                         which accounts matter is a question
///                                                         about a machine rather than about this
///                                                         product
///   dependencies                                        - the language has no field for it. A
///                                                         chip would need one first, and adding
///                                                         to the query language is a change to a
///                                                         surface people write scripts against
/// </summary>
/// <summary>
/// One facet: chips that answer the same question, under a name saying which question.
///
/// <b>Three groups rather than one row of eight, from 2026-08-12.</b> The row mixed three fields -
/// what an entry is DOING, what it is SET to do, and facts about the entry itself - and presented
/// them as one undifferentiated line, so nothing on screen said that two of them add up while a
/// third narrows.
///
/// <b>The grouping is not decoration: it is the semantics, drawn.</b> Measured on this machine
/// before the row was rebuilt - <c>status:running</c> gives 323, <c>status:stopped</c> 485, and the
/// two together 808, which is their sum. Across fields it multiplies instead:
/// <c>status:running start:automatic</c> gives 90. So chips in one group ADD and chips in different
/// groups NARROW, and a person who cannot see the boundary cannot predict either.
/// </summary>
public sealed class FilterGroup
{
    private readonly string _labelKey;

    internal FilterGroup(string labelKey, IReadOnlyList<FilterChip> chips)
    {
        _labelKey = labelKey;
        Chips = chips;
    }

    /// <summary>What this group is called, in the language of whoever is reading it.</summary>
    public string Label => Texts.Of(_labelKey);

    public IReadOnlyList<FilterChip> Chips { get; }

    /// <summary>
    /// Whether clicking two of these shows MORE rather than less.
    ///
    /// <b>Worked out from the chips rather than declared, because it is a fact about the query
    /// rather than a choice about the row.</b> One field means the language ORs them and the group
    /// adds up. Several fields mean it ANDs them and each chip narrows on its own.
    ///
    /// <b>This property exists because the guard for it went red on the first row that was
    /// built.</b> Three groups were drawn and two of them added up - the third holds a trigger
    /// question and a driver question, which are different fields, so the label above it was
    /// promising something the parser does not do. The answer is not to force the two apart into
    /// groups of one, it is to stop claiming the same thing about both kinds.
    /// </summary>
    public bool AddsUp => Chips.Select(chip => chip.Field).Distinct(StringComparer.Ordinal).Count() == 1;

    /// <summary>
    /// The sentence under this group's name, which differs by the answer above.
    ///
    /// Two calls rather than one with a choice inside it, and that is <c>TextKeyGuards</c>'s own
    /// precedent obeyed rather than worked around: a key travelling as anything but a literal in
    /// the call is invisible to it, and the fix it chose for <c>ListState</c> was to move the keys
    /// into their calls instead of widening the pattern. A key visible where it is chosen is
    /// better for a reader too.
    /// </summary>
    public string Hint => AddsUp
        ? Texts.Of("gui.filter.hint.adds")
        : Texts.Of("gui.filter.hint.narrows");
}

public sealed class FilterBar : Observable
{
    private readonly IReadOnlyList<FilterChip> _chips;
    private readonly FilterChip _drivers;

    internal FilterBar(Func<string> read, Action<string> write)
    {
        Groups = FilterChips.Grouped(read, write);
        _chips = [.. Groups.SelectMany(group => group.Chips)];

        // Found by what it stands for rather than by its position, so reordering the row cannot
        // silently point the named switch at a different filter.
        _drivers = _chips.Single(chip =>
            chip.Field == FilterChips.DriverField
            && chip.Value == FilterChips.DriverValue
            && chip.Negated);
    }

    /// <summary>The chips, in the order they are shown.</summary>
    public IReadOnlyList<FilterChip> Chips => _chips;

    /// <summary>The same chips, in the facets they belong to. What the window draws.</summary>
    public IReadOnlyList<FilterGroup> Groups { get; }

    /// <summary>
    /// Whether kernel drivers are in the list. <c>A7</c>, which had a name before <c>A5</c> had
    /// chips - so it keeps the name and stops being a second implementation.
    ///
    /// <b>A view over one chip, and that is a deletion rather than an indirection.</b> The switch
    /// read the query for a member and wrote the member into the query, which is the whole of
    /// what a chip does. The sense is inverted because the control says "show" and the member
    /// says "hide", and that is a label rather than a rule.
    /// </summary>
    public bool ShowDrivers
    {
        get => !_drivers.IsOn;
        set => _drivers.IsOn = !value;
    }

    /// <summary>
    /// Asks every chip to look at the query again.
    ///
    /// <b>Every one, not the one that was clicked.</b> A single edit moves several at once -
    /// clearing the box turns them all off, and typing a member by hand lights one nobody
    /// touched. A chip listening only to its own click would sit lit over a query that has
    /// stopped saying anything about it, which is the state <c>A5</c> exists to make impossible.
    /// </summary>
    internal void Rethink()
    {
        foreach (var chip in _chips)
        {
            chip.Rethink();
        }

        Raise(nameof(ShowDrivers));
    }
}

internal static class FilterChips
{
    /// <summary>The field and value the named drivers switch stands for, spelled once.</summary>
    internal const string DriverField = "type";

    internal const string DriverValue = "driver";

    /// <summary>
    /// What the box says before anybody types: kernel drivers hidden - owner's decision, 2026-08-13.
    ///
    /// <b>Because <c>services.msc</c> does, and that is the tool people will compare this
    /// against.</b> On this machine drivers are 470 of 810 entries, so a window that shows them all
    /// opens with more than half its list being things almost nobody came to look at.
    ///
    /// <b>AS A MEMBER OF THE QUERY RATHER THAN AS A HIDDEN DEFAULT, and that is the decision rather
    /// than the implementation.</b> `docs/07` settled it once already, by the owner: the drivers
    /// switch IS this member and is not a state beside the query. So the window opens with the
    /// member visible in the box - the chip is lit, the count says how many were held back, and
    /// Escape clears it exactly like anything else somebody typed. A default living anywhere else
    /// would be a filter nothing on screen admits to, which is rule 8 broken by the first thing a
    /// person sees.
    ///
    /// <b>What this costs, said rather than discovered:</b> the window no longer opens on the whole
    /// machine, so every count a person compares against <c>sc query</c> is a count of services.
    /// One click on the chip, or one Escape, gives back the full listing - and the box says which
    /// of the two they are looking at.
    ///
    /// Composed rather than spelled, so the one place that decides how a member is written stays
    /// the one place.
    /// </summary>
    internal static string OpeningQuery => QueryMembers.Member(DriverField, DriverValue, negated: true);

    /// <summary>
    /// The chips, in the facets they belong to and in the order they are shown.
    ///
    /// <b>Three groups since 2026-08-12, and the boundary between them is the query's own
    /// semantics.</b> Chips inside a group are members of ONE field, so the language ORs them and
    /// clicking two shows both. Chips in different groups are different fields, so it ANDs them and
    /// clicking two narrows. Both halves measured on this machine before the row was rebuilt, not
    /// assumed - the numbers are in <see cref="FilterGroup"/>.
    ///
    /// <b>What arrived with the groups, and each one closes something that could not be clicked at
    /// all:</b> Paused and In transition, so that the state facet can express more than two of the
    /// eight values the language accepts - on this machine "running or stopped" is 808 of 809, so
    /// exactly one entry had no chip that could reach it. Boot and System, two start types that
    /// account for a large share of the list and had no control. And Automatic (delayed) now says
    /// what the Start column says, instead of "Delayed".
    ///
    /// <b>Still not the cross product, and the argument for that is unchanged.</b> The four
    /// enumerations accept twenty-nine values between them and a row of twenty-nine controls is a
    /// worse instrument than the box above it. What is here is a facet a person can complete a
    /// thought in - every value of the state, every value of the start type - rather than every
    /// value of every field. The eleven trigger kinds stay behind <c>trigger:any</c>, because
    /// somebody asking which kind is already looking at triggers.
    ///
    /// <b>What is deliberately NOT here, said rather than left to be noticed:</b> a chip for
    /// "against its start type". The window has a column for it since backlog 165, and the query
    /// language has no field for it at all - so the chip would have nothing to write into the box,
    /// and a chip that cannot be expressed as a member is the one thing this design forbids.
    /// Adding the field is a change to a surface people write scripts against, which is a decision
    /// rather than a slice of this one.
    /// </summary>
    internal static IReadOnlyList<FilterGroup> Grouped(Func<string> read, Action<string> write) =>
    [
        new FilterGroup("gui.filter.group.state",
        [
            new FilterChip("gui.filter.running", "status", "running", negated: false, read, write),
            new FilterChip("gui.filter.stopped", "status", "stopped", negated: false, read, write),
            new FilterChip("gui.filter.paused", "status", "paused", negated: false, read, write),

            // One word for the four pending states, added to the language for this chip. Nobody
            // arrives asking whether something is specifically continue-pending - they ask what is
            // in the middle of something, and that is one question with four answers.
            new FilterChip("gui.filter.pending", "status", "pending", negated: false, read, write)
        ]),

        new FilterGroup("gui.filter.group.start",
        [
            new FilterChip("gui.filter.automatic", "start", "automatic", negated: false, read, write),

            // Its own value in the language rather than a qualifier on "automatic", which is what
            // made it expressible at all - checked before this chip was written rather than assumed.
            new FilterChip("gui.filter.delayed", "start", "delayed", negated: false, read, write),

            new FilterChip("gui.filter.manual", "start", "manual", negated: false, read, write),
            new FilterChip("gui.filter.disabled", "start", "disabled", negated: false, read, write),

            // The two that belong to drivers and had no control at all. They are a large share of
            // the list and the row that hides drivers is right beside them, so somebody who does
            // not want them can say so in one click either way.
            new FilterChip("gui.filter.boot", "start", "boot", negated: false, read, write),
            new FilterChip("gui.filter.system", "start", "system", negated: false, read, write)
        ]),

        new FilterGroup("gui.filter.group.about",
        [
            // NOT A START TYPE, WHICH IS WHY IT MOVED. A service can be Manual and trigger-started
            // at once - services.msc writes that as "Manual (Trigger Start)" and so does this
            // window's Start column. It is an orthogonal fact about the entry, and standing it
            // among the start types said it was one of them.
            //
            // "any" is the language's reserved word for "this was read and there is something in
            // it", so this asks for entries that have a trigger at all rather than one of the
            // eleven kinds.
            new FilterChip("gui.filter.triggered", "trigger", QueryFields.Any, negated: false, read, write),

            // THE ONE THAT REPLACES A CONTROL RATHER THAN ADDING ONE. The drivers switch was a
            // checkbox beside the search box with its own text-editing helpers, and `docs/11`
            // complaint 7 left the grouping of that row deliberately unfinished, because `A5` was
            // going to arrive and rewrite it. It has.
            //
            // It sits here rather than in a scope control of its own, and that is a smaller answer
            // than the UI document asked for. Its argument - that scope is not a facet - is real,
            // but this chip IS a query member like every other one, and a segmented services /
            // drivers / all control is an IA change rather than a grouping. Named, not smuggled.
            new FilterChip("gui.filter.hideDrivers", DriverField, DriverValue, negated: true, read, write)
        ])
    ];
}
