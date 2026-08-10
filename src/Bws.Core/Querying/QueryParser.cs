using System.Globalization;

namespace Bws.Core.Querying;

/// <summary>One member of a query, compiled and grouped, ready to be asked about entries.</summary>
internal sealed record QueryTerm
{
    /// <summary>Null for a bare word, which is searched across every free-search field.</summary>
    internal QueryField? Field { get; init; }

    internal required IReadOnlyList<IQueryValue> Values { get; init; }

    internal bool Negated { get; init; }

    /// <summary>
    /// The values as they were written, before they were compiled into matchers.
    ///
    /// Kept so that something can ask whether a query contains a particular member without
    /// re-reading the text - see <see cref="Query.Excludes"/>. A compiled value cannot answer
    /// that: <c>type:driver</c> becomes two symbols and no longer knows it was spelled with
    /// the word the person clicked.
    /// </summary>
    internal IReadOnlyList<string> Written { get; init; } = [];
}

/// <summary>What came of reading a query: either something that can filter, or what is wrong with it.</summary>
public sealed record QueryParseResult
{
    internal QueryParseResult(Query? query, IReadOnlyList<QueryProblem> problems)
    {
        Query = query;
        Problems = problems;
    }

    /// <summary>Null when <see cref="Problems"/> is not empty.</summary>
    public Query? Query { get; }

    public IReadOnlyList<QueryProblem> Problems { get; }

    public bool IsValid => Problems.Count == 0 && Query is not null;
}

/// <summary>
/// Reads query text into something that can be run against entries.
///
/// Two rules shape the error handling and both come from the query language document. A
/// query with a mistake in it filters nothing and says what is wrong, rather than
/// returning an empty list, because an empty list is an answer and here there is none. An
/// unfinished member, on the other hand, is not a mistake: the interface validates on
/// every keystroke and would spend most of its time showing red if half-typed text were
/// an error, so a member with nothing after the colon is simply ignored.
/// </summary>
public static class QueryParser
{
    /// <summary>
    /// The version of the syntax this build understands.
    ///
    /// A saved query is stored text, so it is a promise that has to hold for years. The
    /// syntax may grow and may not change meaning, and when a saved query starts carrying
    /// this number it will be able to keep behaving the way it did on the day it was
    /// written, including which fields a bare word searched back then.
    /// </summary>
    public const int SyntaxVersion = 2;

    /// <summary>
    /// An empty query means everything, which has to be said out loud because the
    /// alternative - an empty search box showing an empty list - would be absurd.
    /// </summary>
    /// <remarks>
    /// <b>A BARE WORD BETWEEN SLASHES IS A PATTERN, AND THERE IS NO LONGER A MODE.</b> Until
    /// 2026-08-11 a switch beside the search box decided whether a word with no field was text
    /// or a regular expression - so the same typing meant two things depending on a checkbox
    /// elsewhere on the screen, and nothing in the text said which. The owner asked why the
    /// switch existed, and the answer was that it should not.
    ///
    /// <b>The state went away rather than moving.</b> That is the part worth keeping: a mode has
    /// to be stored beside a saved query for the query to still mean what it meant, which was
    /// recorded as backlog 18 and is now moot - the text carries its own meaning, which is what
    /// stored text has to do.
    ///
    /// It also closed a parity gap nobody had written down: the command line never offered the
    /// switch at all, so <c>bws list --query "^spool"</c> and the same text in the window could
    /// disagree. Now they cannot.
    ///
    /// Only bare words are affected. <c>name:spool*</c> keeps its own operators exactly as before.
    /// </remarks>
    /// <param name="input">
    /// Whether this is text somebody has finished writing or text they are in the middle of.
    ///
    /// <b>The difference decides what a member that says nothing means</b>, and until 2026-08-03
    /// there was no difference. A member with nothing after its colon was dropped in silence
    /// everywhere, because a search box holds <c>status:</c> between two keystrokes and turning
    /// that red after every one of them teaches people to ignore red. That reasoning is sound
    /// for a window and has no meaning at all in a terminal, which has no keystrokes - and there
    /// the same tolerance meant <c>bws list --query "status:"</c> answered with every entry on
    /// the machine and a code of success.
    ///
    /// That is the third shape of one fault. <c>!!!</c> was repaired on 2026-08-03, then the
    /// empty pair of quotes, and each repair closed one spelling of "somebody wrote something
    /// that constrains nothing". <b>This closes the family</b>, including the spellings nobody
    /// has thought of, because it stops asking which shapes are suspicious and asks instead
    /// whether anything was understood.
    ///
    /// An enum rather than a boolean at the call site, the same choice and the same reason as
    /// <c>NetworkPaths</c>: <c>Parse(text, false, true)</c> says nothing to anybody reading it.
    ///
    /// <b>Finished is the default</b>, so a new caller gets the strict reading by saying nothing
    /// and only the window has to ask for the other. Owner's decision, 2026-08-03.
    /// </param>
    public static QueryParseResult Parse(
        string? query, QueryInput input = QueryInput.Finished)
    {
        var problems = new List<QueryProblem>();

        if (string.IsNullOrWhiteSpace(query))
        {
            return new QueryParseResult(new Query([]), problems);
        }

        if (!QueryScanner.TryScan(query, out var members, out var unclosedQuoteAt))
        {
            problems.Add(new QueryProblem
            {
                Kind = QueryProblemKind.UnclosedQuote,
                Text = query[unclosedQuoteAt..]
            });

            return new QueryParseResult(null, problems);
        }

        var terms = new List<QueryTerm>();

        foreach (var member in members)
        {
            // THE CHOKE POINT, and it went in and came straight back out on 2026-08-03 before
            // it could work. Reporting every member that produced nothing broke four tests at
            // once, all of them holding the same deliberate decision about half-typed text - so
            // it was reverted and replaced by a rule naming one shape, the lone exclamation
            // mark. Two more shapes turned up within the hour.
            //
            // What was missing was not the choke point. It was somebody having said which of
            // the two situations the text is in, and now the caller says.
            var before = problems.Count;
            var term = ReadMember(member, problems);

            if (term is not null)
            {
                terms.Add(term);

                continue;
            }

            // Nothing came of it, and nothing has been said about why. In a window that is
            // somebody mid-word. In a terminal there is no mid-word.
            if (input is QueryInput.Finished && problems.Count == before)
            {
                problems.Add(new QueryProblem
                {
                    Kind = QueryProblemKind.EmptyTerm,
                    Text = member.Text.Length == 0 ? "\"\"" : member.Text
                });
            }
        }

        return problems.Count > 0
            ? new QueryParseResult(null, problems)
            : new QueryParseResult(new Query(Fold(terms)), problems);
    }

    /// <summary>
    /// Collapses repeated mentions of one field into an alternative.
    ///
    /// <c>status:running status:stopped</c> means either, not both. Without this rule,
    /// ticking two status boxes in the interface would produce a query that can never
    /// match anything, and ticking two boxes is the most ordinary thing a person does. So
    /// the rule is forced by the promise that clicking filters writes a query, not chosen
    /// for elegance.
    /// </summary>
    private static List<QueryTerm> Fold(List<QueryTerm> terms)
    {
        var folded = new List<QueryTerm>(terms.Count);
        var positionOf = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var term in terms)
        {
            // Bare words stay separate: two of them mean "contains both", and exclusions
            // are already absolute, so neither gains anything from being merged.
            if (term.Field is null || term.Negated)
            {
                folded.Add(term);
                continue;
            }

            if (positionOf.TryGetValue(term.Field.Name, out var position))
            {
                folded[position] = folded[position] with
                {
                    Values = [.. folded[position].Values, .. term.Values]
                };

                continue;
            }

            positionOf[term.Field.Name] = folded.Count;
            folded.Add(term);
        }

        return folded;
    }


    /// <summary>
    /// A member with no field on it: free search across everything a person would search.
    ///
    /// <b>BETWEEN SLASHES IT IS A PATTERN. ANYTHING ELSE IS TEXT.</b> This replaced a switch
    /// beside the search box on 2026-08-11 at the owner's decision, and the argument against the
    /// switch is the argument against modes in general - the same typing meant two different
    /// things depending on a checkbox elsewhere on the screen, and nothing in the text said which.
    ///
    /// <b>"Treat every bare word as a pattern" was the other candidate and it is worse than
    /// either.</b> A dot stops being a dot, so a search for <c>svchost.exe</c> starts matching
    /// <c>svchostXexe</c>, and a pasted <c>C:\Windows</c> becomes a pattern with an invalid escape
    /// in it. Whatever else changes, the common case has to stay the cheap one.
    ///
    /// <b>Quoting is the way back out, and it falls out of the scanner rather than being built.</b>
    /// Quotes take the special meaning off the slashes, so <c>"/foo/"</c> searches for that text,
    /// slashes and all. Nobody has to know the rule exists until it bites, and the fix is then the
    /// one they already know from every other field.
    ///
    /// <b>NOTHING HERE READS THE SLASHES, AND THAT IS THE REAL FINDING OF 2026-08-11.</b> The first
    /// version of this repair added that reading, and it was a second copy of a rule
    /// <see cref="QueryValueReader.ReadTextValue"/> has carried all along - for values AND for free
    /// search. So the syntax the owner asked for already existed, and the switch was a second way
    /// of saying the same thing, applied to the whole word instead of what was between the marks.
    ///
    /// Which is why this repair is a deletion. The parity test next door had the answer written in
    /// it the whole time: it drove the window with the switch on and the terminal with
    /// <c>/^w/</c>, and expected them to agree.
    /// </summary>
    private static QueryTerm? ReadBareWord(ScannedText body, bool negated, List<QueryProblem> problems)
    {
        var free = QueryValueReader.ReadTextValue(body, forFreeSearch: true, problems);

        return free is null
            ? null
            : new QueryTerm { Values = [free], Negated = negated, Written = [body.Text] };
    }

    private static QueryTerm? ReadMember(ScannedText member, List<QueryProblem> problems)
    {
        var negated = member.StartsWithSpecial('!');
        var body = negated ? member.Slice(1) : member;

        // A member made of nothing but exclamation marks. Said rather than dropped, and until
        // 2026-08-03 it was dropped: `bws list --query "!!!"` came back with all 810 entries and
        // a code of success, so a script with a typo in its query got the whole machine and a
        // green light. Rule 8 broken in the place that rule is most about.
        //
        // Found by tools/user-journey/journey.ps1 on its first run and shrunk to one character
        // by the property test beside it.
        //
        // NARROW ON PURPOSE, and the boundary is the interesting part. `status:` and `=` are
        // also members that produce nothing, and they are left alone because they are what a
        // search box holds WHILE SOMEBODY IS TYPING - three tests and one in the window hold
        // that decision. An exclamation mark on its own is not half typed. It is finished and
        // it says nothing.
        // AN ATTEMPT TO ASK THE SCANNER INSTEAD WENT IN HERE AND CAME STRAIGHT BACK OUT, and it
        // is written down because the guess was reasonable and wrong. ScannedText carries a
        // Literal flag per character, and "no literal characters means no content" reads well -
        // but that flag marks characters whose special meaning was taken away by quoting, not
        // characters a person meant. An ordinary word has none of them. The condition turned 156
        // green tests red in one build.
        if (body.Length == 0 || body.Text.All(character => character == '!'))
        {
            problems.Add(new QueryProblem
            {
                Kind = QueryProblemKind.EmptyTerm,

                // Written back out when the member left no characters behind, because it did
                // leave something: a pair of quotes, which is what somebody typed and what the
                // message has to name. An empty fragment in a sentence about a fragment reads
                // as a message with a hole in it.
                Text = member.Text.Length == 0 ? "\"\"" : member.Text
            });

            return null;
        }

        var colon = body.IndexOfSpecial(':');

        // No field name means no field. A member that merely contains a colon, or opens
        // with one, is a bare word: paths and times have colons in them and none of that
        // is an attempt to name a field.
        if (colon <= 0)
        {
            return ReadBareWord(body, negated, problems);
        }

        var fieldName = body.Slice(0, colon).Text;
        var field = QueryFields.Find(fieldName);

        if (field is null)
        {
            problems.Add(new QueryProblem
            {
                Kind = QueryProblemKind.UnknownField,
                Text = fieldName,
                Alternatives = [.. QueryFields.All.Select(known => known.Name)]
            });

            return null;
        }

        var values = new List<IQueryValue>();
        var written = new List<string>();

        foreach (var part in body.Slice(colon + 1).SplitOnSpecial(','))
        {
            if (part.Length == 0)
            {
                // Nothing after the colon is somebody mid-keystroke and is dropped below. An
                // empty PAIR OF QUOTES is not: they opened it and closed it, so the member is
                // finished and asks for a value that is the empty string - which nothing has.
                //
                // Until 2026-08-03 the two were the same absence, and `name:""` came back with
                // every entry on the machine and a code of success. Backlog item 66.
                if (part.IsExplicitlyEmpty)
                {
                    problems.Add(new QueryProblem
                    {
                        Kind = QueryProblemKind.EmptyTerm,
                        Text = member.Text
                    });

                    return null;
                }

                continue;
            }

            var value = QueryValueReader.ReadValue(field, part, problems);

            if (value is not null)
            {
                values.Add(value);
                written.Add(part.Text);
            }
        }

        // Nothing after the colon at all. Half-typed, not wrong, so the rest of the query
        // still answers and the member waits for the person to finish.
        return values.Count == 0
            ? null
            : new QueryTerm { Field = field, Values = values, Negated = negated, Written = written };
    }

}
