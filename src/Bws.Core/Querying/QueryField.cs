namespace Bws.Core.Querying;

/// <summary>How a field is compared, which decides what a value after the colon may look like.</summary>
internal enum QueryFieldKind
{
    /// <summary>Contains by default, with exact, wildcard and regular expression forms.</summary>
    Text,

    /// <summary>Exact values only, and only ones that exist. A typo is an error, never an empty result.</summary>
    Enumeration,

    /// <summary>Equality, comparison and a closed range.</summary>
    Number
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
    /// Whether this field has no data until the second pass has run.
    ///
    /// Kept here rather than as a list of names somewhere else, so that a caller can ask
    /// "does this query need the expensive read" without knowing which fields those are.
    /// The command line must not carry that list: it would go stale the first time a family
    /// of expensive data was added, and it would go stale silently.
    /// </summary>
    internal bool NeedsSecondPass { get; init; }

    /// <summary>
    /// Whether this field was read on this entry, which is what <c>none</c>, <c>any</c>
    /// and <c>?</c> ask about.
    /// </summary>
    internal required Func<ScmEntry, ReadOutcome> OutcomeOf { get; init; }

    /// <summary>Text fields only. Null when the value is absent or was refused.</summary>
    internal Func<ScmEntry, string?>? TextOf { get; init; }

    /// <summary>Enumeration fields only.</summary>
    internal Func<ScmEntry, FieldSymbols>? SymbolsOf { get; init; }

    /// <summary>Enumeration fields only. Ordered, because it is also the list shown when a value is wrong.</summary>
    internal IReadOnlyList<QueryValueName> Values { get; init; } = [];

    /// <summary>Number fields only. Null when the value is absent or was refused.</summary>
    internal Func<ScmEntry, int?>? NumberOf { get; init; }
}
