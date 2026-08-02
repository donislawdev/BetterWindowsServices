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

    [Fact]
    public void Reading_a_query_never_fails_in_a_way_it_did_not_plan_for()
    {
        // The contract is total: any text at all comes back either as something that can
        // filter or as a list of problems. An escaping mistake that throws would reach a
        // person as a stack trace from typing into a search box.
        Awkward.Sample(
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
            iter: 20_000);
    }

    [Fact]
    public void Reading_a_query_as_expressions_never_fails_either()
    {
        // The switch beside the search box sends every bare word to the regular expression
        // engine, which is a second way in for the same text and has its own way of throwing.
        Awkward.Sample(
            text =>
            {
                var parsed = QueryParser.Parse(text, bareWordsAreExpressions: true);

                return parsed.IsValid ^ (parsed.Problems.Count > 0);
            },
            iter: 20_000);
    }

    [Fact]
    public void A_query_that_reads_can_judge_any_entry_without_failing()
    {
        // Parsing and evaluating fail differently. A pattern that compiles and then trips over
        // a particular value would surface only on the machine that has that value.
        var entries = Specimens.All;

        Awkward.Sample(
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
            iter: 5_000);
    }

    [Fact]
    public void Resolving_a_launch_path_never_fails_however_it_is_written()
    {
        // What a service registered, which nobody here chose. Six shapes were measured on a
        // real machine and none of them is validated anywhere - the resolver's job is to make
        // sense of whatever is there, so the one thing it may never do is throw.
        Awkward.Sample(
            command =>
            {
                _ = BinaryPathResolver.Resolve(
                    command,
                    serviceName: "Probe",
                    isDriver: false,
                    windowsDirectory: @"C:\Windows",
                    exists: _ => false);

                _ = BinaryPathResolver.Resolve(
                    command,
                    serviceName: "Probe",
                    isDriver: true,
                    windowsDirectory: @"C:\Windows",
                    exists: _ => true);

                return true;
            },
            iter: 20_000);
    }

    [Fact]
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

        Gen.Select(Gen.Int[0, original.Length - 1], Gen.Int[0, 40], Gen.Int[0, 3])
            .Sample(
                damage =>
                {
                    var (at, length, kind) = damage;
                    var broken = Damage(original, at, length, kind);

                    try
                    {
                        _ = SnapshotJson.TryRead(broken, out var snapshot, out var failure);

                        // Exactly one of the two comes back, whichever way it went.
                        return (snapshot is null) ^ (failure is null);
                    }
                    catch (Exception thrown)
                    {
                        throw new InvalidOperationException(
                            $"Reading a snapshot damaged at {at} by {length} ({kind}) threw " +
                            $"{thrown.GetType().Name}: {thrown.Message}", thrown);
                    }
                },
                iter: 20_000);
    }

    /// <summary>
    /// Four ways a stored file goes wrong, and all four are things that happen to real files
    /// rather than things somebody would invent: it stops early, a piece falls out, a byte
    /// flips, or something is inserted.
    /// </summary>
    private static string Damage(string text, int at, int length, int kind)
    {
        var end = Math.Min(text.Length, at + length);

        return kind switch
        {
            0 => text[..at],
            1 => text[..at] + text[end..],
            2 => text[..at] + '\uFFFD' + text[Math.Min(text.Length, at + 1)..],
            _ => text[..at] + new string('}', Math.Max(1, length)) + text[at..]
        };
    }
}
