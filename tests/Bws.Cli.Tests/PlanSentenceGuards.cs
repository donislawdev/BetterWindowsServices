using Bws.Core.Planning;

namespace Bws.Cli.Tests;

/// <summary>
/// What the plan says about a group, when the group turns out to be one.
///
/// <b>The command line twin of <c>PluralGuards</c> in the window's tests, and it exists because the
/// two interfaces had drifted apart on exactly this.</b> Backlog 207 measured it: the window was
/// given a singular for both of these sentences on 2026-08-19 and the command line was not, so for
/// a fortnight the same fact was reported two different ways depending on which one somebody was
/// looking at.
///
/// <b>THESE TWO ARE THE ONES NO PATTERN CAN FIND, AND THAT IS THE REASON FOR A FILE RATHER THAN A
/// LINE IN A SCAN.</b> "Those keep running" and "these drivers" hold a plural with NO NUMBER
/// anywhere in them, so the scan that found nine counted sentences in the language file walked past
/// both without a word. <c>CountedWordGuards</c> holds the counted ones by shape. These are held by
/// name, because an exact string is the only thing that can be checked here without a guard that
/// guesses at English.
///
/// <b>Asked of <see cref="PlanText"/> rather than of a running command</b>, which is what the
/// backlog row proposed and why: a plan can be built here, and a test that ran the tool would need
/// a machine with a shared process on it.
/// </summary>
public sealed class PlanSentenceGuards
{
    [Fact]
    public void A_shared_process_warning_about_one_entry_does_not_say_those()
    {
        var alone = PlanText.Describe(
            new PlanWarning(PlanWarningKind.SharedProcess, "Spooler", ["W32Time"]));

        Assert.Contains("That keeps running", alone, StringComparison.Ordinal);
        Assert.DoesNotContain("Those keep", alone, StringComparison.Ordinal);

        // The even half: two really do get the plural, so the assertion above is a singular being
        // chosen rather than the plural having been deleted.
        Assert.Contains(
            "Those keep running",
            PlanText.Describe(new PlanWarning(PlanWarningKind.SharedProcess, "Spooler", ["W32Time", "Dnscache"])),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Every kind of warning the core can produce has words of its own here.
    ///
    /// <b>Written 2026-09-06 with the wildcard that made it necessary, and the wildcard is the
    /// point.</b> This switch ended in <c>_ =&gt; "is already in that state, so nothing would
    /// change"</c>, so a warning kind added without a sentence did not go missing - it came out
    /// wearing another warning's words, on a screen whose whole job is telling somebody what is
    /// about to happen to their machine. A silence would have been survivable. That was not.
    ///
    /// <b>Two lengths, because half the sentences here come in pairs</b> and a key that exists for
    /// one count and not the other fails only when somebody happens to hit the other count.
    ///
    /// <b>It asks for a key coming back, which is what a missing string looks like.</b>
    /// <c>Texts.Of</c> answers with the key itself rather than throwing, deliberately - a missing
    /// string should look wrong on screen instead of taking the tool down mid-run - so the key
    /// arriving on screen is exactly the failure to look for.
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
                var said = PlanText.Describe(new PlanWarning(kind, "Spooler", related));

                if (said.StartsWith("cli.", StringComparison.Ordinal))
                {
                    wordless.Add($"{kind} with {related.Length}: {said}");
                }
            }
        }

        Assert.True(
            wordless.Count == 0,
            "A warning the core can produce has no sentence in the terminal, so its key reaches the "
            + "screen instead of words:" + Environment.NewLine + string.Join(Environment.NewLine, wordless));
    }

    /// <summary>
    /// Every kind of refusal the core can produce says its own thing, and no two say the same
    /// thing.
    ///
    /// <b>WRITTEN 2026-09-07 BECAUSE THE FAULT ABOVE HAD ALREADY HAPPENED IN THE SWITCH NEXT DOOR
    /// AND SHIPPED.</b> The warning switch got named arms and a throw on 2026-09-06. The refusal
    /// switch, six lines further down the same file, kept its wildcard - and the two refusals a
    /// forcing plan can produce arrived that night with no words at all. Measured on this machine
    /// on 2026-09-07 against the shipped build: <c>bws kill ALG --dry-run</c> printed <i>"ALG is a
    /// driver. This tool shows drivers but does not start or stop them."</i> about a service
    /// <c>sc qc</c> calls WIN32_OWN_PROCESS, which was simply stopped and therefore had no process
    /// to end. A confident false statement about the machine, on the one verb that can kill.
    ///
    /// <b>IT ASKS SOMETHING THE WARNING GUARD DOES NOT, AND HAD TO.</b> That one looks for a key
    /// arriving on screen, which is what a MISSING string looks like. A wildcard is never missing -
    /// it hands back another kind's sentence, fully worded. So the question here is whether two
    /// kinds say the same thing, which is the only shape a shared arm has.
    ///
    /// <b>The lists are two lengths apart for the reason the warning guard gives</b>, and the
    /// entries differ per kind so that two sentences comparing equal really are one sentence
    /// reused rather than two that happen to be about the same names.
    /// </summary>
    [Fact]
    public void Every_kind_of_refusal_says_its_own_thing()
    {
        var said = new Dictionary<string, PlanProblemKind>(StringComparer.Ordinal);
        var shared = new List<string>();

        foreach (var kind in Enum.GetValues<PlanProblemKind>())
        {
            var sentence = PlanText.Describe(new PlanProblem(kind, "Spooler", ["W32Time"]));

            if (said.TryGetValue(sentence, out var already))
            {
                shared.Add($"{already} and {kind} both say: {sentence}");
            }
            else
            {
                said.Add(sentence, kind);
            }

            if (sentence.StartsWith("cli.", StringComparison.Ordinal))
            {
                shared.Add($"{kind} has no sentence at all: {sentence}");
            }
        }

        Assert.True(
            shared.Count == 0,
            "Two refusals the core tells apart come out of the terminal as one sentence, so one of "
            + "them is wearing the other's words:" + Environment.NewLine
            + string.Join(Environment.NewLine, shared));
    }

    [Fact]
    public void A_refusal_naming_one_driver_does_not_call_it_these_drivers()
    {
        var one = PlanText.Describe(
            new PlanProblem(PlanProblemKind.CascadeNotOperable, "BFE", ["WdNisDrv"]));

        Assert.Contains("this driver first", one, StringComparison.Ordinal);
        Assert.DoesNotContain("these drivers", one, StringComparison.Ordinal);

        Assert.Contains(
            "these drivers first",
            PlanText.Describe(new PlanProblem(PlanProblemKind.CascadeNotOperable, "BFE", ["WdNisDrv", "wtd"])),
            StringComparison.Ordinal);
    }
}
