using Bws.Core.Querying;
using Bws.Core.Snapshots;
using Bws.Core.Tests.Fakes;
using CsCheck;

namespace Bws.Core.Tests;

/// <summary>
/// Properties of everything here that reads text somebody else wrote.
///
/// Three parsers take input this project does not control: the query language takes what is
/// typed into a search box, the snapshot reader takes a file that may have been edited, moved
/// between machines or truncated by a full disk, and the launch path resolver takes whatever a
/// service happens to have registered. All three have the same contract - <b>any input at all
/// comes back as an answer or as a stated problem, and never as an exception</b> - and a
/// contract about every input cannot be checked with examples.
///
/// <b>Why a library rather than the loop this project already had.</b> The hand written
/// version, which stays next door for the properties it does better, prints the random string
/// that broke and leaves the reader to work out which character mattered. This one shrinks a
/// failure to the smallest input that still fails and prints a seed to reproduce it. That is
/// the whole of what CsCheck buys, and it is worth a test-only dependency because these
/// failures arrive at three in the morning with no other clue attached.
///
/// Generation is deliberately built from the characters that mean something in one of these
/// three languages - quotes, backslashes, commas, colons, exclamation marks, slashes, stars,
/// braces - because random letters exercise the boring path and prove nothing about the
/// interesting one.
/// </summary>
#pragma warning disable CA1031
// Catching everything is the assertion here, not a lapse. The property being checked is that
// no exception of any kind escapes, so narrowing the catch would narrow the claim to the
// failures somebody already thought of - which are exactly the ones that do not need this.
public sealed class ParserPropertyTests
{
    /// <summary>
    /// Text made of characters that carry meaning somewhere in this project, plus enough
    /// ordinary letters that a field name can appear by chance.
    /// </summary>
    private static readonly Gen<string> Awkward =
        Gen.String[
            Gen.OneOfConst(
                '"', '\\', ',', ':', '!', '*', '?', '/', '=', '<', '>', '-', ' ', '\t',
                '{', '}', '[', ']', '.', '%', '\'', 'a', 'b', 'e', 'i', 'm', 'n', 'o', 'r',
                's', 't', 'u', '0', '1', '9'),
            0,
            40];

    /// <summary>
    /// Text that was typed either constrains the listing or is called a mistake, never neither.
    ///
    /// <b>A different class of property from the three below, and the difference is the reason
    /// this one exists.</b> Those check TOTALITY - that no input escapes as an exception - and
    /// <c>!!!</c> satisfies every one of them. This checks that nothing is quietly dropped,
    /// which is rule 8 of CLAUDE.md applied to a parser: an answer that looks complete and is
    /// not is the worst failure this product has.
    ///
    /// <b>Found by using the tool, not by reading it.</b> tools/user-journey/journey.ps1 ran
    /// <c>bws list --query "!!!"</c> on 2026-08-03 and got all 810 entries back with a code of
    /// success. A script with a typo in its query gets the whole machine and a green light.
    ///
    /// <b>THE EXCEPTION IS WRITTEN INTO THIS PROPERTY RATHER THAN LEFT OUT OF IT.</b> The first
    /// version said "nothing typed may ever be dropped in silence" and broke four tests at once,
    /// all of them holding the same deliberate decision: <c>status:</c> is what a search box
    /// contains between the colon and the value, and making that an error would flash red after
    /// every keystroke. So the property below skips members that look half typed, and says so.
    /// A property that quietly avoided them would be a property nobody could argue with.
    /// </summary>
    [Fact]
    [Trait("search", "weekly")]
    public void Text_that_was_typed_either_narrows_the_listing_or_is_called_a_mistake()
    {
        Awkward.Searched(
            text =>
            {
                // Nothing typed means nothing expected. The empty query is the listing, and
                // that is a decision rather than an oversight.
                if (text.Trim().Length == 0)
                {
                    return true;
                }

                // THREE EXCLUSIONS STOOD HERE AND ALL THREE ARE GONE, 2026-08-03. That is the
                // whole point of this entry now, so it is worth saying what they were.
                //
                // Text holding a colon or an equals sign was skipped, because a member might be
                // half typed and this property had nothing to say about those. Text holding a
                // quote was skipped BY NAME, because the property was false there - an empty
                // pair of quotes was a finished member saying nothing, and `--query '""'`
                // answered with the whole machine and a code of success.
                //
                // Both are answered by the same thing, and it is not a longer list of suspicious
                // punctuation: the caller now says whether the text is finished. A search box
                // says it is not and keeps its tolerance. Everything else - which is every use
                // this property describes - says it is, and then a member that constrains
                // nothing is a mistake with no exceptions to enumerate.
                //
                // <b>So the property is now unconditional for any text at all</b>, which is what
                // it was reaching for from the start. Three repairs were needed to get here and
                // each of the first two named one spelling: `!!!`, then `""`. Naming spellings
                // is how the next one gets missed.
                var parsed = QueryParser.Parse(text);

                // Saying what is wrong is the other legal answer, and the better one.
                if (!parsed.IsValid)
                {
                    return true;
                }

                // Accepted, and constrains nothing. Whatever was typed went into a bin.
                return !parsed.Query!.IsEmpty;
            },
            new(20_000) { Print = text => $"accepted and filtered nothing: <{text}>" });
    }

    [Fact]
    [Trait("search", "weekly")]
    public void Reading_a_query_never_fails_in_a_way_it_did_not_plan_for()
    {
        // The contract is total: any text at all comes back either as something that can
        // filter or as a list of problems. An escaping mistake that throws would reach a
        // person as a stack trace from typing into a search box.
        Awkward.Searched(
            text =>
            {
                QueryParseResult parsed;

                try
                {
                    parsed = QueryParser.Parse(text);
                }
                catch (Exception failure)
                {
                    throw new InvalidOperationException(
                        $"Parsing '{text}' threw {failure.GetType().Name}: {failure.Message}", failure);
                }

                return parsed.IsValid ^ (parsed.Problems.Count > 0);
            },
            new(20_000));
    }

    /// <summary>
    /// The same contract, over text far longer than anybody types by hand.
    ///
    /// <b>Its own property rather than a wider generator, and the reason is a measurement.</b>
    /// The one above runs twenty thousand times and the strings it makes are up to forty
    /// characters, which is the right size for finding a shape - most interesting query text is
    /// short. Widening it to thousands would multiply the cost of every one of those runs to
    /// reach a case that needs length rather than shape.
    ///
    /// And length is a case. <b>A wildcard of about a thousand parts ended the process</b> with
    /// an exit code outside the table until 2026-08-03 - the linear engine refuses a pattern
    /// whose automaton passes ten thousand nodes, and the threshold is around two thousand
    /// characters. The property above was true of everything its generator will ever produce,
    /// and stayed true while the tool crashed.
    ///
    /// Fewer runs and a bounded length, because each one is expensive and the space being covered
    /// is thin: what matters is the order of magnitude, not the variety. <b>Measured while writing
    /// it</b> - a first version multiplying up to 1500 times took this project's core suite from
    /// nine seconds to two minutes forty, which is a test nobody would keep running. The sizes
    /// below straddle the threshold that used to crash, which is all this has to do.
    /// </summary>
    [Fact]
    [Trait("search", "weekly")]
    public void Reading_a_very_long_query_never_fails_in_a_way_it_did_not_plan_for()
    {
        Gen.Select(Awkward, Gen.Int[40, 200])
            .Searched(
                grown =>
                {
                    var (seed, times) = grown;
                    var text = seed.Length == 0 ? new string('*', times) : string.Concat(Enumerable.Repeat(seed, times));

                    QueryParseResult parsed;

                    try
                    {
                        parsed = QueryParser.Parse(text);
                    }
                    catch (Exception failure)
                    {
                        throw new InvalidOperationException(
                            $"Parsing {text.Length} characters threw {failure.GetType().Name}: {failure.Message}",
                            failure);
                    }

                    return parsed.IsValid ^ (parsed.Problems.Count > 0);
                },
                new(150));
    }

    [Fact]
    [Trait("search", "weekly")]
    public void Reading_a_query_as_expressions_never_fails_either()
    {
        // The switch beside the search box sends every bare word to the regular expression
        // engine, which is a second way in for the same text and has its own way of throwing.
        Awkward.Searched(
            text =>
            {
                var parsed = QueryParser.Parse("/" + text + "/");

                return parsed.IsValid ^ (parsed.Problems.Count > 0);
            },
            new(20_000));
    }

    [Fact]
    [Trait("search", "weekly")]
    public void A_query_that_reads_can_judge_any_entry_without_failing()
    {
        // Parsing and evaluating fail differently. A pattern that compiles and then trips over
        // a particular value would surface only on the machine that has that value.
        var entries = Specimens.All;

        Awkward.Searched(
            text =>
            {
                var parsed = QueryParser.Parse(text);

                if (!parsed.IsValid)
                {
                    return true;
                }

                foreach (var entry in entries)
                {
                    _ = parsed.Query!.Match(entry);
                }

                return true;
            },
            new(5_000));
    }

    [Fact]
    [Trait("search", "weekly")]
    public void Resolving_a_launch_path_never_fails_however_it_is_written()
    {
        // What a service registered, which nobody here chose. Six shapes were measured on a
        // real machine and none of them is validated anywhere - the resolver's job is to make
        // sense of whatever is there, so the one thing it may never do is throw.
        Awkward.Searched(
            command =>
            {
                _ = BinaryPathResolver.Resolve(
                    command,
                    serviceName: "Probe",
                    isDriver: false,
                    windowsDirectory: @"C:\Windows",
                    exists: _ => Reading<bool>.Present(false),
                    networkPaths: NetworkPaths.Follow);

                _ = BinaryPathResolver.Resolve(
                    command,
                    serviceName: "Probe",
                    isDriver: true,
                    windowsDirectory: @"C:\Windows",
                    exists: _ => Reading<bool>.Present(true),

                    // Follow, so that arbitrary text is put through the whole resolver rather
                    // than through the branch that returns early for anything starting with
                    // two backslashes. Skipping here would quietly stop this property from
                    // covering the shape it was written to cover.
                    networkPaths: NetworkPaths.Follow);

                return true;
            },
            new(20_000));
    }

    [Fact]
    [Trait("search", "weekly")]
    public void Reading_a_snapshot_never_fails_in_a_way_it_did_not_plan_for()
    {
        // A snapshot is a file kept for months and carried between machines, so by the time it
        // is read again it may have been truncated by a full disk, edited by somebody curious,
        // or written by a version that has since changed. The reader's contract is the same as
        // the query parser's: an answer or a stated reason, never a stack trace.
        //
        // Generated by damaging a real one rather than from nothing. Random text fails at the
        // first character and proves only that the first character is checked - the inputs
        // worth trying are the ones that look almost right.
        var original = SnapshotJson.Render(
            Snapshot.Of([Entries.Any, Entries.Inspected], note: "property", new FakeClock()));

        Damaged.In(original)
            .Searched(
                damage =>
                {
                    var broken = damage.To(original, '}');

                    try
                    {
                        _ = SnapshotJson.TryRead(broken, out var snapshot, out var failure);

                        // Exactly one of the two comes back, whichever way it went.
                        return (snapshot is null) ^ (failure is null);
                    }
                    catch (Exception thrown)
                    {
                        throw new InvalidOperationException(
                            $"Reading a snapshot damaged {damage} threw " +
                            $"{thrown.GetType().Name}: {thrown.Message}", thrown);
                    }
                },
                new(20_000));
    }
}
