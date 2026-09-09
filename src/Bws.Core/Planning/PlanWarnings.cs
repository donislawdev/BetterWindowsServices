namespace Bws.Core.Planning;

/*
 * WHAT A PLAN HAS TO SAY BEFORE IT RUNS, WHICH IS A DIFFERENT SUBJECT FROM WHAT A PLAN IS.
 *
 * Out of OperationPlan.cs on 2026-09-09 because the size ratchet said so, and the seam it
 * pointed at is a real one rather than a convenient cut. Everything left there answers "what
 * was asked for and what will happen". Everything here answers "what does somebody need to
 * know before letting it" - and that second question is the one that grows, because every
 * measurement of a real machine turns up another thing worth saying.
 *
 * The ratchet asked twice in one change, which is itself the argument: adding one warning kind
 * for a start type took the file past a ceiling set when it held nine of them.
 */

/// <summary>Kinds of thing worth saying before somebody presses the button.</summary>
public enum PlanWarningKind
{
    /// <summary>Stopping this takes others down with it, because it was asked to.</summary>
    Cascade,

    /// <summary>
    /// Others are running that need this one, and they were not included. The manager
    /// refuses a stop in that situation, so this plan will not get past its first step.
    /// </summary>
    DependentsInTheWay,

    /// <summary>The entry shares its process with others, so the process does not go away.</summary>
    SharedProcess,

    /// <summary>Automatic, so stopping it lasts until the next boot and no longer.</summary>
    ReturnsAfterReboot,

    /// <summary>
    /// The cascade could not be read in full. The plan below may therefore be shorter than
    /// what actually happens, which is the one thing a preview must never hide.
    /// </summary>
    CascadeUnreadable,

    /// <summary>Already in the state being asked for, so the step would do nothing.</summary>
    AlreadyThere,

    /// <summary>
    /// An entry in this plan does not accept a stop, so the manager will refuse the control
    /// instead of taking it.
    ///
    /// <b>The first warning here that predicts a specific refusal rather than describing a
    /// consequence</b>, and it is the reason the field behind it is read at all. Without it the
    /// plan looked identical whether a stop was going to work or was never going to be accepted,
    /// and the difference only showed up afterwards, as an error number.
    ///
    /// <b>A warning rather than a problem, deliberately.</b> Refusing to build the plan would
    /// decide for somebody who may have asked for a cascade in which this entry is one of
    /// several, and the manager - not us - is the authority on what it will accept by the time
    /// the step actually runs. The line this holds is the same one <c>CannotComeBack</c> draws:
    /// we say what we already read, and we do not predict the manager.
    ///
    /// <see cref="PlanWarning.Related"/> names every entry in the plan this is true of, in the
    /// order their steps happen, because the one in the way is often the cascade rather than the
    /// entry somebody named.
    /// </summary>
    DoesNotAcceptStop,

    /// <summary>
    /// Ending this process ends every other entry living in it, whether or not they stopped first.
    ///
    /// <b>Not the same sentence as <see cref="SharedProcess"/>, which it replaces on a forcing
    /// plan.</b> That one says the process does not go away and the neighbours keep running, which
    /// is true of an ordinary stop and the exact opposite of what happens here. Two warnings whose
    /// wording contradicts each other about the same machine would be worse than either.
    ///
    /// <b>The neighbours are also STEPS on such a plan</b>, asked to stop politely first, so this
    /// warning is about what happens to the ones that do not - which is the same thing either way.
    /// </summary>
    TerminationTakesWithIt,

    /// <summary>
    /// The entry is one the machine does not work without, and this plan takes it down now.
    ///
    /// <b>A warning rather than a refusal, on the owner's decision of 2026-09-06.</b> An
    /// administrator has the right to manage their own machine, which is the line `R2` of the
    /// specification already draws - we do not make it harder than the system's own tools do, and
    /// we do not pretend the problem is absent either.
    ///
    /// <b>RAISED ON THE ORDINARY PLAN AS WELL AS THE FORCING ONE SINCE 2026-09-09, AND UNTIL THAT
    /// DAY IT WAS RAISED ONLY BY <see cref="ForcedStop"/>.</b> So the sentence existed, the list of
    /// names existed, and `bws stop PlugPlay` said nothing about either - the guard was wired into
    /// the path somebody reaches last rather than the one they reach first. Found by an audit
    /// reading the plan of four entries off this list on 2026-09-09, not by a test: nothing was
    /// asserting the absence, because nothing knew it was an absence.
    ///
    /// <b>Where the wording had to change, and it is the glossary rather than taste.</b> The
    /// sentence read "Ending it stops the machine", and `docs/03` binds "ending" to terminating a
    /// process - the one operation nobody can refuse on the machine's behalf. An ordinary stop is
    /// not that, so a sentence reusing the word would have taught the reader that stop and
    /// terminate are one thing on the day they most need to be different.
    ///
    /// <b>What the wording has to carry is the glossary's distinction `P1`</b>: this is "you should
    /// not", which is a different sentence from "you cannot" and from "confirm that you mean it".
    /// <b>The window adds the third of those on its own</b> - see <c>Planned.NeedsTyping</c>, owner's
    /// decision of 2026-09-09 - and that is the window deciding how heavy a confirmation to ask
    /// for, not this warning changing what it says.
    /// </summary>
    CriticalService,

    /// <summary>
    /// The entry is one the machine does not work without, and this plan stops it starting.
    ///
    /// <b>ITS OWN KIND RATHER THAN A SECOND USE OF <see cref="CriticalService"/>, because the
    /// consequence lands at a different time and a sentence has to say which.</b> Stopping a
    /// critical entry takes the machine down while somebody is watching. Disabling one changes
    /// nothing today and takes the machine down at the next boot, possibly weeks later and
    /// probably in front of somebody else. One sentence covering both would have to drop the
    /// timing, which is the half that decides what an administrator does next.
    ///
    /// <b>Added 2026-09-09 on the owner's decision</b>, alongside wiring the kind above into the
    /// ordinary plan. The audit that found the first absence did not look for this one - the
    /// question "what else reaches these seven names" did.
    ///
    /// <b>The kind is a public contract and this is an addition to it</b>, not a change: a script
    /// keying on `criticalService` keeps working and simply never sees this value on a plan it was
    /// already reading. `docs/02` carries the shape.
    /// </summary>
    CriticalStartType
}

/// <summary>
/// Something worth knowing before the plan runs.
///
/// Carries a kind and the facts, never a sentence. The layer above decides the words,
/// because the same warning reads differently in a window and in a terminal, and neither
/// wording belongs in the part that works out what will happen.
/// </summary>
public sealed record PlanWarning(PlanWarningKind Kind, string ServiceName, IReadOnlyList<string> Related)
{
    internal PlanWarning(PlanWarningKind kind, string serviceName)
        : this(kind, serviceName, [])
    {
    }
}
