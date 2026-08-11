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
public sealed class FilterBar : Observable
{
    private readonly IReadOnlyList<FilterChip> _chips;
    private readonly FilterChip _drivers;

    internal FilterBar(Func<string> read, Action<string> write)
    {
        _chips = FilterChips.All(read, write);

        // Found by what it stands for rather than by its position, so reordering the row cannot
        // silently point the named switch at a different filter.
        _drivers = _chips.Single(chip =>
            chip.Field == FilterChips.DriverField
            && chip.Value == FilterChips.DriverValue
            && chip.Negated);
    }

    /// <summary>The chips, in the order they are shown.</summary>
    public IReadOnlyList<FilterChip> Chips => _chips;

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
    /// The chips, in the order they are shown.
    ///
    /// Ordered by how often the question gets asked rather than by field, because the row is read
    /// left to right and the first two are what somebody opening this on an unknown server wants:
    /// what is running, and what should be and is not.
    /// </summary>
    internal static IReadOnlyList<FilterChip> All(Func<string> read, Action<string> write) =>
    [
        new FilterChip("gui.filter.running", "status", "running", negated: false, read, write),
        new FilterChip("gui.filter.stopped", "status", "stopped", negated: false, read, write),

        new FilterChip("gui.filter.automatic", "start", "automatic", negated: false, read, write),
        new FilterChip("gui.filter.manual", "start", "manual", negated: false, read, write),
        new FilterChip("gui.filter.disabled", "start", "disabled", negated: false, read, write),

        // Its own value in the language rather than a qualifier on "automatic", which is what
        // made it expressible at all - checked before this chip was written rather than assumed.
        new FilterChip("gui.filter.delayed", "start", "delayed", negated: false, read, write),

        // "any" is the language's reserved word for "this was read and there is something in
        // it", so this asks for entries that have a trigger at all rather than one of the eleven
        // kinds. The kinds are a question for somebody already looking at triggers.
        new FilterChip("gui.filter.triggered", "trigger", QueryFields.Any, negated: false, read, write),

        // THE ONE THAT REPLACES A CONTROL RATHER THAN ADDING ONE. The drivers switch was a
        // checkbox beside the search box with its own text-editing helpers, and `docs/11`
        // complaint 7 left the grouping of that row deliberately unfinished, because `A5` was
        // going to arrive and rewrite it. It has.
        new FilterChip("gui.filter.hideDrivers", DriverField, DriverValue, negated: true, read, write)
    ];
}
