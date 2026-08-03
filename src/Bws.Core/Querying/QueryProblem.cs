namespace Bws.Core.Querying;

/// <summary>
/// Whether a piece of query text is finished, or somebody is in the middle of writing it.
///
/// <b>The one thing that decides what a member saying nothing means.</b> A search box holds
/// <c>status:</c> between two keystrokes, so treating it as a mistake would turn the box red
/// through most of the time somebody is typing - and a warning that is usually wrong is a
/// warning people learn to skip. A terminal has no keystrokes: by the time the text reaches
/// this parser it is as finished as it will ever be, and the same tolerance meant
/// <c>bws list --query "status:"</c> answered with every entry on the machine and a code of
/// success.
///
/// An enum rather than a boolean for the same reason <see cref="NetworkPaths"/> is one: at the
/// call site <c>Parse(text, false, true)</c> says nothing to anybody reading it.
/// </summary>
public enum QueryInput
{
    /// <summary>
    /// Nobody is going to add to this. A member that constrains nothing is a mistake, because
    /// there is no later in which it could become something.
    /// </summary>
    Finished,

    /// <summary>
    /// Somebody is typing it, so a member that constrains nothing is simply not finished yet
    /// and is passed over without a word. The rest of the text still answers.
    /// </summary>
    BeingTyped
}

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
    BadSize,

    /// <summary>
    /// A word with nothing in it. Today that means a lone <c>!</c>, or one whose whole body is
    /// an empty pair of quotes.
    ///
    /// <b>Added 2026-08-03 because it was being dropped in silence.</b> The parser returned
    /// nothing for such a member and recorded no problem, so <c>bws list --query "!!!"</c> came
    /// back with all 810 entries and a code of success - a script with a typo in its query got
    /// the whole machine and a green light. That is rule 8 of CLAUDE.md broken in the place the
    /// rule is most about: an answer that looks complete and is not.
    ///
    /// Found by <c>tools/user-journey/journey.ps1</c> on its first run, shrunk to a single
    /// character by the property test beside it. Neither the four hundred tests then existing
    /// nor the analysers had anything to say.
    /// </summary>
    EmptyTerm,

    /// <summary>
    /// A wildcard with so many parts that the linear engine will not build it.
    ///
    /// Its own kind rather than <see cref="BadPattern"/>, and the difference is the sentence a
    /// person needs. A bad pattern is written wrongly and the engine says where. This one is
    /// written correctly and is simply too big, so the answer is "use fewer wildcards" rather
    /// than "check your syntax" - and the engine's own words here are about automaton nodes,
    /// which explain nothing to anybody who did not choose the engine.
    ///
    /// <b>Added 2026-08-03 because until then it was not a problem at all, it was a crash.</b>
    /// About a thousand repetitions of <c>*a</c> ended the process with an unhandled exception
    /// and an exit code outside the table. See <c>QueryPatterns.TryWildcard</c> for why this is
    /// refused rather than handed to the engine that would accept it.
    /// </summary>
    PatternTooComplex
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
