using Bws.Core.Querying;
using Bws.Core.Tests.Fakes;
using CsCheck;

namespace Bws.Core.Tests;

/// <summary>
/// The query language's contracts over text shaped like the language itself.
///
/// ParserPropertyTests holds the same contracts over an alphabet of awkward characters, and that
/// alphabet is good at punctuation and almost never spells a field with a value it accepts - so
/// on 2026-10-16 the random properties together ran 63 per cent of the language's lines and the
/// reader of field values 28. These run the same rules over Queries.Shaped, which writes fields,
/// aliases, enumeration values in every spelling the language folds, comparisons, ranges, sizes
/// with and without units, patterns, quotes and near misses of each - so that "any text comes back
/// as an answer or a reason" is checked on the text people actually type.
///
/// <b>Both readings, every time.</b> The command line reads a finished query and the search box
/// reads one being typed, and they differ exactly where a member says nothing. A property that
/// asked only one would leave the other's half of the code to the examples.
/// </summary>
#pragma warning disable CA1031
// Catching everything is the assertion here: the property is that nothing of any kind escapes.
public sealed class QueryShapePropertyTests
{
    private static readonly QueryInput[] Readings = [QueryInput.Finished, QueryInput.BeingTyped];

    [Fact]
    [Trait("search", "weekly")]
    public void Shaped_text_is_read_or_refused_in_both_readings_and_never_both()
    {
        Queries.Shaped.Searched(
            text => Readings.All(input =>
            {
                var parsed = Parsed(text, input);

                // Finished text that was accepted has to constrain something - the rule "!!!" and
                // the empty quotes broke, held here over the shapes those two were only examples of.
                // Blank text is the one exception, and a decision: it is the whole listing. The
                // first run of this property found that exception missing here, not in the parser.
                return (parsed.IsValid ^ (parsed.Problems.Count > 0))
                    && (input != QueryInput.Finished || !parsed.IsValid || string.IsNullOrWhiteSpace(text) || !parsed.Query!.IsEmpty);
            }),
            new(20_000) { Print = text => $"read and refused at once, or accepted and filtered nothing: <{text}>" });
    }

    [Fact]
    [Trait("search", "weekly")]
    public void Shaped_text_that_reads_judges_every_entry_without_failing()
    {
        // Parsing and judging fail differently: a comparison that reads and then trips over one
        // machine's value - a stopped entry with no process, a size past the integer a number
        // holds - would surface only on the machine that has it.
        //
        // Every specimen twice: as the listing leaves it and as the memory pass leaves it. The
        // specimens start with memory never read, so memory:>5MB is answered "could not read"
        // before any size is compared - and the first mutation run of this property came back
        // MISSED on a comparison that threw on every stopped entry, 2026-10-16. The pass is the
        // product's own, so a stopped entry is Absent here for the reason it is Absent there.
        IReadOnlyList<ScmEntry> entries = [.. Specimens.All, .. MemoryPass.Fill(Specimens.All, new FakeProcessMemoryReader())];

        Queries.Shaped.Searched(
            text =>
            {
                foreach (var input in Readings)
                {
                    var parsed = Parsed(text, input);

                    if (!parsed.IsValid)
                    {
                        continue;
                    }

                    foreach (var entry in entries)
                    {
                        try
                        {
                            _ = parsed.Query!.Match(entry);
                        }
                        catch (Exception failure)
                        {
                            throw new InvalidOperationException(
                                $"Judging '{entry.ServiceName}' by <{text}> threw {failure.GetType().Name}: {failure.Message}", failure);
                        }
                    }
                }

                return true;
            },
            new(5_000));
    }

    /// <summary>
    /// A filter chip clicked on writes a member the box then carries, and clicked off leaves the
    /// box carrying it no more - over any line somebody might have typed before clicking.
    ///
    /// The chips promise to compose into the query the person could have typed (A5), and the
    /// examples check that on lines somebody thought of. A line ending in a backslash, an open
    /// pattern or a colon is exactly the line nobody thinks of and somebody types.
    ///
    /// <b>One exception, and it is the rule the code states:</b> a line with an unclosed quote
    /// cannot be taken apart, so it carries nothing and is skipped here rather than failed.
    /// </summary>
    [Fact]
    [Trait("search", "weekly")]
    public void A_chip_turned_on_is_carried_and_turned_off_is_not_whatever_was_typed_before()
    {
        Gen.Select(Queries.Shaped, Queries.Chip).Searched(
            pair =>
            {
                var (text, (field, value, negated)) = pair;

                if (Parsed(text, QueryInput.BeingTyped).Problems.Any(problem => problem.Kind == QueryProblemKind.UnclosedQuote))
                {
                    return true;
                }

                var on = QueryMembers.With(text, field, value, negated);
                var off = QueryMembers.Without(on, field, value, negated);

                return QueryAsTyped.Of(on).Carries(field, value, negated)
                    && !QueryAsTyped.Of(off).Carries(field, value, negated);
            },
            new(20_000) { Print = pair => $"<{pair.Item1}> with {(pair.Item2.Negated ? "!" : "")}{pair.Item2.Field}:{pair.Item2.Value}" });
    }

    [Fact]
    [Trait("search", "weekly")]
    public void Asking_at_any_caret_in_shaped_text_never_throws_and_every_row_points_into_the_text()
    {
        WithCaret.Searched(
            pair => QueryCompletionTests.EveryRowPointsIntoTheText(pair.Text, pair.Caret),
            new(20_000) { Print = pair => $"a row pointed outside the text: <{pair.Text}> at {pair.Caret}" });
    }

    [Fact]
    [Trait("search", "weekly")]
    public void Nothing_offers_itself_again_in_shaped_text_once_it_has_been_written()
    {
        WithCaret.Searched(
            pair => QueryCompletionTests.NothingOffersItselfAgain(pair.Text, pair.Caret),
            new(20_000) { Print = pair => $"accepting a row reopened the list with the same row: <{pair.Text}> at {pair.Caret}" });
    }

    private static readonly Gen<(string Text, int Caret)> WithCaret =
        Queries.Shaped.SelectMany(text => Gen.Int[-3, text.Length + 3].Select(caret => (text, caret)));

    private static QueryParseResult Parsed(string text, QueryInput input)
    {
        try
        {
            return QueryParser.Parse(text, input);
        }
        catch (Exception failure)
        {
            throw new InvalidOperationException(
                $"Parsing <{text}> as {input} threw {failure.GetType().Name}: {failure.Message}", failure);
        }
    }
}
#pragma warning restore CA1031
