using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The second list the plan panel carries: the entries that get no plan at all, and why.
///
/// <b>FOUR HUNDRED DRIVERS USED TO PRODUCE FOUR HUNDRED SENTENCES DIFFERING ONLY BY A NAME, and it
/// is a fault the scope bar created rather than one that was always there.</b> A refusal belongs to
/// its own entry and the rest of the selection carries on - the owner's decision of 2026-08-18, and
/// the right one - but while drivers sat behind a query, picking them by the hundred was not
/// something anybody did by accident. Backlog 217.
///
/// <b>These are about the words rather than about the window</b>, so they ask PlanWords directly.
/// The panel hands it the list and renders what comes back, and a test driving a whole window to
/// read three sentences would be slower and would say less about which of the two decided anything.
/// </summary>
public sealed class PlanRefusalGuards
{
    /// <summary>
    /// Refusals that say the same thing come back as one sentence with a count - and every name.
    ///
    /// <b>The names are asserted as well as the count, and that is the half worth guarding.</b> The
    /// complaint this answers is a reason repeated four hundred times, not four hundred names, so a
    /// version that dropped the names would be tidier and would take away the part somebody about
    /// to change a machine is reading it for.
    /// </summary>
    [Fact]
    public void Refusals_that_say_the_same_thing_come_back_as_one_sentence_with_a_count()
    {
        var said = PlanWords.Describe(
        [
            Driver("amdkmdag"),
            Driver("nvlddmkm"),
            Driver("iaStorAC")
        ]);

        var only = Assert.Single(said);

        Assert.Contains("3", only, StringComparison.Ordinal);

        foreach (var name in new[] { "amdkmdag", "nvlddmkm", "iaStorAC" })
        {
            Assert.Contains(name, only, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// One of them still reads as one of them.
    ///
    /// The singular kept its own sentence and did not become "1 of these are drivers", which is the
    /// counted plural this window has gone red over before.
    /// </summary>
    [Fact]
    public void One_refusal_is_still_said_in_the_singular()
    {
        var only = Assert.Single(PlanWords.Describe([Driver("amdkmdag")]));

        Assert.Equal(PlanWords.Describe(Driver("amdkmdag")), only);
        Assert.DoesNotContain("1 of these", only, StringComparison.Ordinal);
    }

    /// <summary>
    /// REFUSALS THAT NAME OTHER ENTRIES ARE NEVER GATHERED, however alike they read.
    ///
    /// <b>Rule 8 of the untouchable rules, at the point where gathering would break it.</b> A
    /// cascade refusal carries the drivers that stand in the way, and two of them are two different
    /// facts even when the wording either side of the list is identical. Folding those together
    /// would leave the panel saying that two things were refused without saying what either of them
    /// was in the way of.
    /// </summary>
    [Fact]
    public void Refusals_that_name_other_entries_are_never_gathered()
    {
        var said = PlanWords.Describe(
        [
            new PlanProblem(PlanProblemKind.CascadeNotOperable, "BFE", ["WdNisDrv"]),
            new PlanProblem(PlanProblemKind.CascadeNotOperable, "mpssvc", ["wtd"])
        ]);

        Assert.Equal(2, said.Count);
        Assert.Contains(said, one => one.Contains("WdNisDrv", StringComparison.Ordinal));
        Assert.Contains(said, one => one.Contains("wtd", StringComparison.Ordinal));
    }

    /// <summary>
    /// A gathered group keeps the place where its first member appeared.
    ///
    /// <b>So the panel still reads in the order somebody picked things.</b> Sorting by kind, or
    /// pushing every gathered sentence to the end, would be an order this method invented - and the
    /// list beside it, the steps, is in the order they will happen. Two lists on one screen in two
    /// unrelated orders is a screen somebody has to hold two ideas about.
    /// </summary>
    [Fact]
    public void A_gathered_group_keeps_the_place_its_first_member_had()
    {
        var said = PlanWords.Describe(
        [
            Driver("amdkmdag"),
            new PlanProblem(PlanProblemKind.CascadeNotOperable, "BFE", ["WdNisDrv"]),
            Driver("nvlddmkm")
        ]);

        Assert.Equal(2, said.Count);
        Assert.Contains("amdkmdag", said[0], StringComparison.Ordinal);
        Assert.Contains("nvlddmkm", said[0], StringComparison.Ordinal);
        Assert.Contains("BFE", said[1], StringComparison.Ordinal);
    }

    private static PlanProblem Driver(string serviceName) =>
        new(PlanProblemKind.NotOperable, serviceName, []);
}
