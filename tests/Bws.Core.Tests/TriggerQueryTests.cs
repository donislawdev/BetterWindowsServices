using Bws.Core.Querying;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// Asking about what starts an entry by itself.
///
/// The question this field exists to answer is not "which kind" but "is there one at all".
/// Measured on a real machine on 2026-08-01: of the ten entries that answered "automatic
/// and not running", four were waiting to be asked for and six had nothing to bring them
/// up, and until triggers were read nothing told those two groups apart. That listing is
/// the tool's headline signal, so the difference between six and ten is the difference
/// between a signal somebody acts on and a list they learn to ignore.
/// </summary>
public sealed class TriggerQueryTests
{
    [Fact]
    public void An_entry_that_waits_for_a_trigger_is_not_an_entry_that_failed_to_start()
    {
        var waiting = Match("start:auto !status:running trigger:any");
        var stuck = Match("start:auto !status:running trigger:none");

        Assert.Contains("sppsvc", waiting);
        Assert.Contains("AsusUpdateCheck", stuck);

        // The two questions divide the same set. An entry cannot be in both, and the whole
        // value of the field is that it is in exactly one.
        Assert.Empty(waiting.Intersect(stuck, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Having_no_triggers_and_nobody_having_looked_are_different_answers()
    {
        // ADR-13 in one assertion. Answering "has none" about an entry nobody read would be
        // a confident answer about something nobody looked at, and on this field that is
        // the difference between a broken service and one that is waiting.
        Assert.Contains("AsusUpdateCheck", Match("trigger:none"));
        Assert.DoesNotContain("TriggersUnknown", Match("trigger:none"));
    }

    [Fact]
    public void Nobody_having_looked_is_asked_for_with_the_question_mark()
    {
        // A GAP UNTIL 2026-10-05, pinned here under the name
        // Nobody_having_looked_is_a_state_the_language_cannot_ask_about. 07-JEZYK-ZAPYTAN says
        // all four states of a field must be expressible and gave three words, ? meaning refused
        // only - so an entry nobody read answered no to all three and could not be asked for at
        // all. The owner closed it on 2026-09-30 by widening ? rather than adding a fourth word:
        // both states that have no answer are one question.
        Assert.Contains("TriggersUnknown", Match("trigger:?"));
        Assert.DoesNotContain("AsusUpdateCheck", Match("trigger:?"));
    }

    [Theory]
    [InlineData("trigger:none")]
    [InlineData("trigger:any")]
    public void None_and_any_do_not_answer_for_a_field_nobody_read(string query)
    {
        // Stability report Q-4. Whether a field nobody read is empty is the very thing nobody
        // knows, and until 2026-10-05 these two said a confident no - the entry left the answer
        // without being counted among the ones it is unsure about, while every other kind of
        // value counted it. The exclusion is the half that hurt most: !trigger:any kept the
        // entry with nothing to say the keeping was a guess.
        var match = QueryParser.Parse(query).Query!.Match(Specimens.TriggersNotRead);
        var excluded = QueryParser.Parse("!" + query).Query!.Match(Specimens.TriggersNotRead);

        Assert.Equal(new QueryMatch(Matched: false, Unreadable: true, TooCostly: false), match);
        Assert.Equal(new QueryMatch(Matched: true, Unreadable: true, TooCostly: false), excluded);
    }

    [Fact]
    public void A_kind_can_be_asked_for_by_name()
    {
        Assert.Contains("Appinfo", Match("trigger:network"));
        Assert.DoesNotContain("Appinfo", Match("trigger:device"));

        // The kind the interop metadata has no name for, and the second most common on a
        // real machine. It is asked for like any other rather than hiding under "unknown".
        Assert.Contains("sppsvc", Match("trigger:state"));
    }

    [Fact]
    public void The_action_is_askable_next_to_the_kind()
    {
        // Two questions about one thing, sharing one set of words on purpose. A trigger
        // that stops a service is a very different fact from one that starts it, and
        // somebody asking should not have to know which of the two lists their word is on.
        Assert.Contains("sppsvc", Match("trigger:start"));
        Assert.Empty(Match("trigger:stop"));
    }

    [Fact]
    public void Several_triggers_of_one_kind_are_not_reported_as_several_kinds()
    {
        // Six network endpoint triggers on one entry, read from a real machine. The field
        // answers about kinds present, so six of one kind is still that one kind.
        var entry = Specimens.ManyTriggersOfOneKind;

        Assert.Equal(6, entry.Triggers.Value!.Count);
        Assert.Contains("Appinfo", Match("trigger:network"));
    }

    private static List<string> Match(string query)
    {
        var parsed = QueryParser.Parse(query);

        Assert.True(parsed.IsValid, string.Join(", ", parsed.Problems.Select(problem => problem.Kind)));

        return [.. parsed.Query!.Filter(Specimens.All).Entries.Select(entry => entry.ServiceName)];
    }
}
