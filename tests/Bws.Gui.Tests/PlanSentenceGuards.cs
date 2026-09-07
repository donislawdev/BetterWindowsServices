using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// Every KIND the core can produce has words of its own in this window, and no two kinds share a
/// sentence.
///
/// <b>Its own file since 2026-09-07, and the size ratchet is what asked - PluralGuards went past
/// five hundred lines when the second of these arrived.</b> The seam is a subject rather than a
/// line count, and the two questions are genuinely different: that file asks whether a sentence
/// counting things has a singular beside its plural, and this one asks whether every VALUE the
/// core can hand over has a sentence at all. A file answering both would be answering "how many"
/// and "which one" under one name.
///
/// <b>It is named after the file in the terminal's tests that asks the same two questions</b>, so
/// the pair is findable from either side. Both surfaces have shipped this fault, six days apart.
///
/// <b>NOTHING HERE READS A MACHINE.</b> Every value is walked out of its enum, which is what makes
/// these guards notice a kind added tomorrow rather than one somebody remembered to test.
/// </summary>
public sealed class PlanSentenceGuards
{
    /// <summary>
    /// Every kind of warning the core can produce has words of its own in this window.
    ///
    /// <b>The twin of the same guard in the terminal tests, written 2026-09-06 with the wildcard
    /// that made both necessary.</b> Each switch ended in <c>_ =&gt; "is already in that state, so
    /// nothing would change"</c>, so a warning kind added without a sentence did not go missing -
    /// it arrived wearing another warning's words, in the panel whose whole job is telling somebody
    /// what is about to happen to their machine.
    ///
    /// <b>Two lengths, because half of these come in a singular and a plural</b> and a key written
    /// for one count and not the other fails only when somebody happens to hit the other count.
    /// </summary>
    [Fact]
    public void Every_kind_of_warning_has_words_of_its_own()
    {
        string[][] lengths = [["W32Time"], ["W32Time", "Dnscache"]];

        var wordless = new List<string>();

        foreach (var kind in Enum.GetValues<PlanWarningKind>())
        {
            foreach (var related in lengths)
            {
                var said = PlanWords.Describe(new PlanWarning(kind, "Spooler", related));

                if (said.StartsWith("gui.", StringComparison.Ordinal))
                {
                    wordless.Add($"{kind} with {related.Length}: {said}");
                }
            }
        }

        Assert.True(
            wordless.Count == 0,
            "A warning the core can produce has no sentence in the window, so its key reaches the "
            + "panel instead of words:" + Environment.NewLine + string.Join(Environment.NewLine, wordless));
    }

    /// <summary>
    /// Every kind of refusal the core can produce says its own thing here too, and no two say the
    /// same thing.
    ///
    /// <b>The mirror of the guard of the same name in the command line's tests, written the same
    /// day and for the same reason.</b> That surface had already SHIPPED the fault - a stopped
    /// service reported as a driver - and this switch was one step behind it: the two refusals a
    /// forcing plan can produce fell through a wildcard onto the sentence about a disabled entry
    /// that cannot come back. It had never reached a screen only because the window could not build
    /// such a plan yet, which is exactly what this slice changed.
    ///
    /// <b>It asks a different question from the warning guard above and had to.</b> That one looks
    /// for a key arriving on screen, which is what a missing string looks like. A wildcard is never
    /// missing - it answers with somebody else's sentence, fully worded - so the only shape it has
    /// is two kinds saying one thing.
    /// </summary>
    [Fact]
    public void Every_kind_of_refusal_says_its_own_thing()
    {
        var said = new Dictionary<string, PlanProblemKind>(StringComparer.Ordinal);
        var shared = new List<string>();

        foreach (var kind in Enum.GetValues<PlanProblemKind>())
        {
            var sentence = PlanWords.Describe(new PlanProblem(kind, "Spooler", ["W32Time"]));

            if (said.TryGetValue(sentence, out var already))
            {
                shared.Add($"{already} and {kind} both say: {sentence}");
            }
            else
            {
                said.Add(sentence, kind);
            }

            if (sentence.StartsWith("gui.", StringComparison.Ordinal))
            {
                shared.Add($"{kind} has no sentence at all: {sentence}");
            }
        }

        Assert.True(
            shared.Count == 0,
            "Two refusals the core tells apart come out of this panel as one sentence, so one of "
            + "them is wearing the other's words:" + Environment.NewLine
            + string.Join(Environment.NewLine, shared));
    }
}
