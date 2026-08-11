using Bws.Core.Querying;

namespace Bws.Gui.Tests;

/// <summary>
/// What the window says about a query it could not read.
///
/// <b>The command line has had this checked since S2 and the window had not</b>, which is the
/// asymmetry `docs/11` complains about in as many words: the two interfaces answer the same
/// question and only one of them was held to it. Found on 2026-08-11 while repairing backlog 161
/// - eight of the ten uncovered lines in this assembly's message table were whole branches, one
/// per kind of mistake somebody can make.
///
/// <b>The claim is not "a string comes back".</b> <see cref="Texts.Of(string)"/> returns the KEY
/// when the key is missing, so a message table with a hole in it produces
/// <c>gui.query.badSize</c> on screen and nothing throws. That is the failure this file is
/// about, and it is why every assertion below refuses the key as an answer.
///
/// <b>The kinds are enumerated rather than listed.</b> A test naming eight kinds passes on the
/// day a ninth arrives with no sentence behind it - which is exactly how a switch grows a
/// default branch that says the wrong thing.
/// </summary>
public sealed class QueryMessageTests
{
    [Fact]
    public void Every_kind_of_mistake_gets_a_sentence_of_its_own()
    {
        var said = new Dictionary<QueryProblemKind, string>();

        foreach (var kind in Enum.GetValues<QueryProblemKind>())
        {
            var message = QueryMessages.Of(Problem(kind));

            Assert.False(
                string.IsNullOrWhiteSpace(message),
                $"{kind} produced nothing to show somebody.");

            // The key coming back IS the failure mode. Texts.Of falls back to the key, so a
            // missing entry reaches the screen looking like debris rather than throwing.
            Assert.DoesNotContain("gui.query.", message, StringComparison.Ordinal);

            said[kind] = message;
        }

        // And they are eight different sentences, not one sentence reached eight ways. Without
        // this the test above passes on a table whose every branch says "that query is wrong",
        // which is the answer that sends somebody looking at characters that are correct.
        Assert.Equal(said.Count, said.Values.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The one message that is shortened, and the reason it is the only one.
    ///
    /// The text behind <see cref="QueryProblemKind.PatternTooComplex"/> is the engine's own
    /// complaint and runs to about two thousand characters. It goes into a single line under a
    /// list, so it is cut - and every other message is not, because their text is what somebody
    /// typed and cutting that would hide the character they got wrong.
    /// </summary>
    [Fact]
    public void The_only_message_that_is_shortened_is_the_one_that_has_to_be()
    {
        var enormous = new string('x', 400);

        var cut = QueryMessages.Of(Problem(QueryProblemKind.PatternTooComplex) with { Text = enormous });

        Assert.DoesNotContain(enormous, cut, StringComparison.Ordinal);
        Assert.Contains("...", cut, StringComparison.Ordinal);

        // The even claim. Without it this passes on a window that truncates everything, and the
        // value somebody typed is the one thing a message about their typing must show whole.
        var whole = QueryMessages.Of(Problem(QueryProblemKind.UnclosedQuote) with { Text = enormous });

        Assert.Contains(enormous, whole, StringComparison.Ordinal);
    }

    /// <summary>
    /// A near miss is offered by name, and that is a different sentence rather than the same one
    /// with a suffix - <c>UnknownValue</c> is the one kind with two branches.
    /// </summary>
    [Fact]
    public void A_value_with_a_near_miss_is_told_which_one()
    {
        var guessed = QueryMessages.Of(Problem(QueryProblemKind.UnknownValue) with { Nearest = "running" });
        var blind = QueryMessages.Of(Problem(QueryProblemKind.UnknownValue));

        Assert.Contains("running", guessed, StringComparison.Ordinal);
        Assert.DoesNotContain("running", blind, StringComparison.Ordinal);
    }

    /// <summary>
    /// One problem carrying everything any branch might ask for, so that a branch reaching for
    /// <c>Field</c> or <c>Detail</c> is answered rather than throwing on a null it required.
    /// </summary>
    private static QueryProblem Problem(QueryProblemKind kind) => new()
    {
        Kind = kind,
        Text = "wat",
        Field = "status",
        Detail = "the engine said no",
        Alternatives = ["status", "start"]
    };
}
