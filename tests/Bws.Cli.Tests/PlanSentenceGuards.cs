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
