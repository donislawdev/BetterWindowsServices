namespace Bws.Core.Planning;

/// <summary>
/// A plan over a selection and what came of it, together.
///
/// <b>The same pair <see cref="PlanRun"/> is, one level up, and for the same reason: the pair is the
/// evidence.</b> The bulk plan said what was going to happen to twenty entries and these runs say
/// what did, and the whole promise of `ADR-11` is that somebody can hold those two side by side.
///
/// <b>It holds runs rather than replacing them, exactly as <see cref="BulkPlan"/> holds plans.</b>
/// Each run inside still concerns one entry and its cascade, carried out step for step by the
/// existing runner. What lives here is only what cannot be seen from inside one run: whether the
/// whole selection arrived, and the one way back out of all of it.
/// </summary>
public sealed record BulkRun
{
    public required BulkPlan Plan { get; init; }

    /// <summary>
    /// One per plan, in the order they were carried out.
    ///
    /// <b>ALWAYS AS LONG AS <see cref="BulkPlan.Plans"/>, INTERRUPTIONS INCLUDED</b>, and that is a
    /// property of <see cref="BulkRunner"/> rather than a coincidence. A run that stopped short and
    /// handed back fewer runs than there were plans would be a report that goes quiet about
    /// everything after the interruption - rule 8 of the project's untouchable rules, at the exact
    /// moment somebody most needs to know what was left alone.
    /// </summary>
    public required IReadOnlyList<PlanRun> Runs { get; init; }

    /// <summary>Every step of every run, in the order they happened.</summary>
    public IEnumerable<StepResult> Results => Runs.SelectMany(run => run.Results);

    /// <summary>Whether somebody interrupted, anywhere in the selection.</summary>
    public bool Cancelled => Runs.Any(run => run.Cancelled);

    /// <summary>
    /// Every entry of every plan ended up where its plan wanted it.
    ///
    /// <b>IT SPEAKS ABOUT THE PLANS AND NOT ABOUT THE SELECTION, and the difference is worth saying
    /// out loud because a caller could reasonably read it the other way.</b> Entries the builder had
    /// to refuse never got a plan at all and are in <see cref="BulkPlan.Problems"/>, which the
    /// preview showed before anything ran. So this answers "did everything the preview promised
    /// happen", and whoever tells a person how it went has to carry both halves.
    /// </summary>
    public bool Completed => Runs.All(run => run.Completed);

    /// <summary>
    /// What it would take to put the machine back where this whole selection found it. One answer
    /// for everything that ran, never one answer per run joined together.
    ///
    /// <b>Joining per-run answers is the obvious implementation and it is wrong in two ways at
    /// once</b>, which is why this hands every step of every run to <see cref="NetEffect"/> in one
    /// go. The order comes out reversed across the whole selection rather than within each run -
    /// otherwise the first line handed back is one the manager refuses, since a selection is dealt
    /// with dependants first and putting it back has to start with what they depend on. And an
    /// entry moved by two of the runs is named once rather than twice, because two lines about one
    /// service, one of them wrong, is worse than no lines at all.
    ///
    /// The worked example, and everything that decided the arithmetic, is at <see cref="NetEffect"/>.
    /// </summary>
    public IReadOnlyList<ReversalStep> Reversal => NetEffect.Of(Results);
}
