namespace Bws.Core.Querying;

/// <summary>
/// What is wrong with a query, as a kind plus the facts needed to say so.
///
/// The core never builds the sentence. It reports which kind of problem happened and
/// what the offending text was, and the layer above turns that into words from its own
/// resource file. That is rule 3 of part 2 of 06-STRUKTURA-I-KONWENCJE: a core that
/// phrases messages has decided what the interface says.
/// </summary>
public enum QueryProblemKind
{
    /// <summary>A field nobody knows. <see cref="QueryProblem.Alternatives"/> lists the real ones.</summary>
    UnknownField,

    /// <summary>A value outside the set a field accepts. <see cref="QueryProblem.Nearest"/> is the closest one.</summary>
    UnknownValue,

    /// <summary>A regular expression the engine refused to compile.</summary>
    BadPattern,

    /// <summary>A quote that never closes.</summary>
    UnclosedQuote,

    /// <summary>A number or a range that does not read as one.</summary>
    BadNumber,

    /// <summary>
    /// A size that does not read as one, which most often means the unit was left off.
    ///
    /// Its own kind rather than <see cref="BadNumber"/>, because the answer a person needs
    /// is different. A bad number is a typo. A bad size is usually <c>memory:&gt;500</c>,
    /// written by somebody who meant megabytes and gets told so, instead of being told their
    /// number is not a number when it plainly is.
    /// </summary>
    BadSize
}

/// <summary>
/// One thing wrong with a query.
///
/// A query carrying any of these filters nothing at all and says what is wrong, rather
/// than returning an empty list. An empty list is an answer, and here there is no answer.
/// </summary>
public sealed record QueryProblem
{
    public required QueryProblemKind Kind { get; init; }

    /// <summary>The fragment the person wrote that caused this.</summary>
    public required string Text { get; init; }

    /// <summary>Which field the problem is about, when it is about one.</summary>
    public string? Field { get; init; }

    /// <summary>
    /// Everything that would have been accepted here. Populated for unknown fields and
    /// unknown values, because a list of what works solves the mistake on the spot while
    /// "unknown value" only tells someone to start guessing.
    /// </summary>
    public IReadOnlyList<string> Alternatives { get; init; } = [];

    /// <summary>The closest accepted spelling, when one is close enough to be worth offering.</summary>
    public string? Nearest { get; init; }

    /// <summary>Why the engine rejected a pattern. Set for <see cref="QueryProblemKind.BadPattern"/>.</summary>
    public string? Detail { get; init; }
}
