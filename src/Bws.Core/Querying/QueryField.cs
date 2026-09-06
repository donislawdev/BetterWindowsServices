namespace Bws.Core.Querying;

/// <summary>How a field is compared, which decides what a value after the colon may look like.</summary>
internal enum QueryFieldKind
{
    /// <summary>Contains by default, with exact, wildcard and regular expression forms.</summary>
    Text,

    /// <summary>Exact values only, and only ones that exist. A typo is an error, never an empty result.</summary>
    Enumeration,

    /// <summary>Equality, comparison and a closed range.</summary>
    Number,

    /// <summary>
    /// The same shapes as <see cref="Number"/>, over a quantity of bytes written with a
    /// unit: <c>500MB</c>, <c>&gt;1GB</c>, <c>100MB-1GB</c>.
    ///
    /// Its own kind rather than a number, because the unit is not optional. The
    /// specification's own example is <c>memory:&gt;500MB</c>, and reading a bare
    /// <c>memory:&gt;500</c> as bytes would match every running service while looking like
    /// it had filtered - the confident wrong answer this language is arranged to avoid.
    /// </summary>
    Size
}

/// <summary>
/// Data a query needs that a plain listing does not read.
///
/// Flags rather than a single yes-or-no, and that is not future-proofing for its own sake:
/// with one flag, a query about memory would set off a signature verification measured at
/// several seconds, to answer a question that costs under a millisecond. The caller asks
/// for what it needs and gets only that.
///
/// Kept here rather than as a list of field names in the command line, so that adding a
/// family cannot leave that list quietly out of date.
/// </summary>
[Flags]
public enum ExtraRead
{
    None = 0,

    /// <summary>Who signed each binary. Measured at 4620-7656 ms over 810 entries.</summary>
    Signatures = 1,

    /// <summary>What each running process is using. Measured at under a millisecond over 110 processes.</summary>
    Memory = 2,

    /// <summary>
    /// Who breaks if each entry stops. Measured at 236-259 ms over 313 services on 2026-09-05.
    ///
    /// <b>The third family, and the one that shows why this was flags rather than a yes-or-no from
    /// the start.</b> It sits between the other two - two hundred and fifty milliseconds against
    /// under one and against seven and a half seconds - so a query about dependents must not send
    /// the window to open eight hundred binaries, and a question about signatures must not walk the
    /// manager service by service. Each caller asks for what it needs and gets only that, which
    /// <c>Readings.Fill</c> honours one flag at a time.
    /// </summary>
    RequiredBy = 4
}

/// <summary>
/// What one field says about one entry, for enumeration fields.
///
/// Two parts, and the second is the point. A start type that could not be read produces
/// no symbols and an admission that something is missing, so a query about it can report
/// the result as partial instead of quietly answering "no".
/// </summary>
internal readonly record struct FieldSymbols(IReadOnlyList<string> Symbols, bool Incomplete)
{
    internal static readonly FieldSymbols Nothing = new([], Incomplete: true);

    internal static FieldSymbols Of(params string[] symbols) => new(symbols, Incomplete: false);

    internal static FieldSymbols Partial(params string[] symbols) => new(symbols, Incomplete: true);
}

/// <summary>
/// One accepted spelling of an enumeration value and the symbols it stands for.
///
/// A spelling can stand for more than one symbol, which is how <c>type:driver</c> covers
/// both driver kinds. That grouping is part of the contract, not a convenience: the query
/// language document uses it in its own examples.
/// </summary>
internal sealed record QueryValueName(string Text, params string[] Symbols);

/// <summary>
/// A field the query language can ask about.
///
/// Only fields the tool actually reads at this stage appear here. A field with no source
/// of data would answer every query with nothing, which is worse than an error, because
/// nothing looks like an answer.
/// </summary>
internal sealed class QueryField
{
    internal required string Name { get; init; }

    internal required QueryFieldKind Kind { get; init; }

    /// <summary>Other accepted spellings of the field name. Part of the contract.</summary>
    internal IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>
    /// What has to be read before this field has anything to say.
    ///
    /// Kept here rather than as a list of names somewhere else, so that a caller can ask
    /// "what does this query need" without knowing which fields those are. The command line
    /// must not carry that list: it would go stale the first time a family was added, and it
    /// would go stale silently.
    /// </summary>
    internal ExtraRead Needs { get; init; }

    /// <summary>
    /// Whether this field was read on this entry, which is what <c>none</c>, <c>any</c>
    /// and <c>?</c> ask about.
    /// </summary>
    internal required Func<ScmEntry, ReadOutcome> OutcomeOf { get; init; }

    /// <summary>Text fields only. Null when the value is absent or was refused.</summary>
    internal Func<ScmEntry, string?>? TextOf { get; init; }

    /// <summary>
    /// Text fields whose value is a list rather than one string. A member matches when any
    /// one of them matches, so <c>privilege:debug</c> finds a service that declares eight
    /// privileges of which one is SeDebugPrivilege.
    ///
    /// Its own hook rather than joining the list into one string, because joining changes
    /// what the operators mean. An exact match would have to equal the whole joined run and
    /// would never match anything, and a wildcard could span the gap between two values and
    /// match a privilege nobody declared.
    /// </summary>
    internal Func<ScmEntry, IReadOnlyList<string>?>? TextsOf { get; init; }

    /// <summary>Enumeration fields only.</summary>
    internal Func<ScmEntry, FieldSymbols>? SymbolsOf { get; init; }

    /// <summary>Enumeration fields only. Ordered, because it is also the list shown when a value is wrong.</summary>
    internal IReadOnlyList<QueryValueName> Values { get; init; } = [];

    /// <summary>Number fields only. Null when the value is absent or was refused.</summary>
    internal Func<ScmEntry, int?>? NumberOf { get; init; }

    /// <summary>
    /// Size fields only, in bytes. Null when the value is absent or was refused.
    ///
    /// Bytes and a long, not megabytes and an int. Rounding at the source would make a
    /// range that reads exactly right - <c>memory:1MB-2MB</c> - answer about something
    /// slightly different, and a process above two gigabytes is ordinary on a server.
    /// </summary>
    internal Func<ScmEntry, long?>? SizeOf { get; init; }
}
