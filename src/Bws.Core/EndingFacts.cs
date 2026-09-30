namespace Bws.Core;

/// <summary>
/// What a process will say about itself before anybody ends it.
///
/// <b>Two facts rather than one type each, because they are read in the same instant, from the
/// same process, for the same reason.</b> Both are asked while a plan is being BUILT, which is the
/// only moment at which a preview can promise anything, and both are about the one step in this
/// product that nobody can refuse on the machine's behalf.
///
/// <b>Nothing here changes anything, and that is worth saying because one of the two looks as
/// though it does.</b> Asking whether a process can be ended means opening a handle that carries
/// the right to end it - and then closing it. A handle is a question. <c>TerminateProcess</c> is
/// the answer, and it is not in this file.
///
/// <b>WHAT IS DELIBERATELY NOT HERE: the protection level.</b> It reads perfectly - measured on
/// 2026-09-08 over 186 processes on two machines, answered every time - and it is the wrong
/// question. Seven processes were protected on one machine and three refused to be opened for
/// ending, four and two on the other, and one program answered opposite ways on the two machines
/// at the same protection level. A refusal built on it would have turned away four processes out
/// of seven that this tool can in fact end. It is a good sentence to show a person and a bad
/// sentence to decide with, so it lives in <c>tools/scm-probe/kill-rights-probe.ps1</c> and in
/// <c>docs/POMIAR-ZABIJANIE-20260908.md</c>, and not in the product. Reading it here would also
/// mean carrying a table of level numbers that Microsoft names without publishing, which is
/// exactly the kind of constant this project has been wrong about before.
/// </summary>
/// <param name="CanBeEnded">
/// Whether a handle carrying the right to end this process could be opened, which is the whole of
/// what can be known before trying.
///
/// <b>All four read states mean something here, which is unusual and is why this is a
/// <see cref="Reading{T}"/> rather than a bool with a code beside it.</b>
/// <list type="bullet">
/// <item><description><c>NotRead</c> - nobody asked. The plan is built the way it always was, and
/// the first anybody hears of a refusal is the step that meets it.</description></item>
/// <item><description><c>Present(true)</c> - the handle opened. Measured on both machines: where
/// it opened, ending worked, including on a process Windows was protecting.</description></item>
/// <item><description><c>Denied</c> - the system said no, and its number and its own words come
/// with it. This is the answer rung five of specification <c>C3</c> asks for.</description></item>
/// <item><description><c>Absent</c> - there is no such process any more. It went away between the
/// listing and this question, which is ordinary and is not a refusal.</description></item>
/// </list>
/// </param>
/// <param name="Created">
/// When the process started, as a file time, and it is here as an IDENTITY rather than as
/// something to show anybody.
///
/// Windows hands out process numbers again once a process is gone, so a number on its own stops
/// meaning what a preview said the moment the process behind it exits. The pair of number and
/// creation time is the nearest thing to an identity Windows will give out, and the two halves
/// have to be read together to be worth anything. Backlog 323.
///
/// <b>Never shown to anybody.</b> The preview names the process by number because that is what a
/// person can check against Task Manager. A file time in a preview would be noise carrying no
/// decision.
/// </param>
/// <param name="Critical">
/// Whether Windows marked this process critical - ending one stops the whole machine with the stop
/// error <c>CRITICAL_PROCESS_DIED</c>, which is a different sentence from "a service the machine
/// needs" and a much shorter one to act on.
///
/// <b>Here since 2026-09-30, and the plan refuses on it</b> (stability report W-3, the owner's decision
/// of that day). Counted on the owner's machine that day: four of 114 processes behind services are
/// critical, and those four hold every one of the seven services whose recovery restarts the computer.
/// An unreadable answer is a refusal too - the one step nobody can undo is not previewed half blind.
/// </param>
public readonly record struct EndingFacts(Reading<bool> CanBeEnded, Reading<long> Created, Reading<bool> Critical)
{
    /// <summary>
    /// The honest answer when there was nobody to ask.
    ///
    /// <b>A named value rather than a null, because "not asked" is a state this project has a word
    /// for and null is not that word.</b> A plan built without a reader behaves exactly as it did
    /// before any of these facts existed - it names the process, tries, and finds out. What it
    /// does NOT do is claim to have checked.
    /// </summary>
    public static EndingFacts NobodyAsked() =>
        new(Reading<bool>.NotRead(), Reading<long>.NotRead(), Reading<bool>.NotRead());
}

/// <summary>
/// One thing the manager does when an entry's process dies without the entry saying it stopped.
///
/// <b>Read for the plan that ends a process and for nothing else, since 2026-09-30</b> (stability
/// report W-3). Ending a process IS that death - measured on the throwaway machine that day, the
/// restart came in ten endings of ten with the flag that widens these actions switched off. The
/// full recovery list with its delays belongs to phase 2 of the plan, in the listing and the details,
/// and none of this is in the machine readable output.
///
/// <b>Which item of the list runs is not knowable from outside.</b> The manager counts failures since
/// the machine started and runs item N for failure N, repeating the last - and no call hands out the
/// count. So the plan asks what is ANYWHERE in the list.
/// </summary>
public enum RecoveryAction
{
    /// <summary>An item that does nothing - Microsoft's own "take no action".</summary>
    Nothing,

    /// <summary>The manager starts the service again - the commonest: 204 of 312 services on one machine.</summary>
    RestartService,

    /// <summary>The manager runs the command the entry names.</summary>
    RunProgram,

    /// <summary>The manager restarts the computer.</summary>
    RestartComputer,

    /// <summary>
    /// A type Microsoft does not document. Measured 2026-09-30: <c>Schedule</c> carries type 4 first,
    /// and sc.exe prints nothing for it. Named rather than guessed at, and never left out.
    /// </summary>
    Unnamed
}

/// <summary>
/// Asks what ending a process would set off: the three questions above of the process, and what the
/// manager does afterwards to each entry living in it.
///
/// <b>Its own interface rather than a method on <see cref="IScmControl"/>, and the reason is the
/// promise section F of the specification makes.</b> Read-only mode is the absence of that
/// interface - one thing not to hold. Everything here is a read, so putting it there would mean a
/// read-only tool could not even show a preview of a forced stop, and previews are exactly what a
/// read-only tool should be able to show.
///
/// <b>It is not part of <see cref="IScmCatalog"/> either, and since 2026-09-30 that needs a better
/// reason than "these are asked of a process"</b>, because <see cref="ReadRecovery"/> is asked of the
/// manager. The reason is the subject: this interface answers "what happens if this process ends",
/// only a plan that ends one ever asks it, and both interfaces that build such a plan already hold
/// one. The catalog is the seam of the listing, with eight implementations, and a recovery reading
/// there would be the start of the phase 2 details rather than one plan's safety question.
/// </summary>
public interface IEndingFactsReader
{
    /// <summary>
    /// Everything worth knowing about one process before ending it.
    ///
    /// <b>Asked of one process rather than of a listing, deliberately.</b> A plan concerns one
    /// entry, so one call answers it. Filling this in for every running entry would put two
    /// handle opens on the listing path for a fact that is different a second later - the same
    /// argument the specification already makes about memory, which is off by default because it
    /// is a measurement rather than a setting.
    /// </summary>
    EndingFacts Read(int processId);

    /// <summary>
    /// What the manager does to this entry when its process dies, item by item.
    ///
    /// <b>Absent when the entry is not there any more</b> - it went between the listing and this
    /// question, so it will not die with anything. Denied when the manager refused, with its number,
    /// and the plan refuses on that: a casualty list whose consequences are known to be missing is
    /// the same shape as one known to be short.
    /// </summary>
    Reading<IReadOnlyList<RecoveryAction>> ReadRecovery(string serviceName);
}

/// <summary>
/// The reader a plan gets when there is nobody to ask - every answer is <see cref="ReadOutcome.NotRead"/>,
/// so the plan is built exactly as it was before any of these questions existed.
///
/// <b>An object rather than a null threaded through</b>, for the reason <see cref="EndingFacts.NobodyAsked"/>
/// gives: "not asked" is a state with a name here.
/// </summary>
internal sealed class NobodyToAsk : IEndingFactsReader
{
    internal static readonly NobodyToAsk Instance = new();

    public EndingFacts Read(int processId) => EndingFacts.NobodyAsked();

    public Reading<IReadOnlyList<RecoveryAction>> ReadRecovery(string serviceName) =>
        Reading<IReadOnlyList<RecoveryAction>>.NotRead();
}
