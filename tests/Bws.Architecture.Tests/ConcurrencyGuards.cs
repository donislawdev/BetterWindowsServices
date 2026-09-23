using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// Keeps the places that can do two things at once down to a list somebody wrote on purpose.
///
/// Borrowed 2026-08-02 from a Go codebase, where the same guard names the files allowed to
/// hold a goroutine or a channel. <b>The method transfers, none of its decisions do</b> - the
/// constructs below are C#'s, and the list is ours.
///
/// The argument for containing it at the source rather than testing for its symptoms: a race
/// is the one defect class that does not reproduce on demand, so a test for one is a test that
/// passes on the days it is wrong. What can be checked exactly is <b>where the code is even
/// able to have one</b>, and today that is three files out of forty two.
///
/// <b>What this is not.</b> It does not make the listed files safe - `SecondPass` earns its
/// place with an oracle test and a probe, and the view model with a reentrancy test. It makes
/// the fourth file a decision instead of an accident.
///
/// False alarm estimate, as 04-PLAN-PRAC requires before building a guard: measured over the
/// whole product at the moment it was written, three files match and all three are listed. So
/// zero. Every hit from here on is a real question until somebody shows otherwise.
/// </summary>
public sealed class ConcurrencyGuards
{
    /// <summary>
    /// Where doing two things at once is allowed, and why, one line each.
    ///
    /// Adding a file here is the deliberate act, and the reason belongs beside it rather than
    /// in a commit message nobody will find again.
    /// </summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.Ordinal)
    {
        ["SecondPass.cs"] =
            "ADR-22. Verifying signatures one file at a time measured 4926-8500 ms over 810 " +
            "entries and 544 files, and several at once measures 1100-1245. The degree follows " +
            "the processor count, the work set is fixed before it starts, and the order of the " +
            "result is restored afterwards - all three of those are tested.",

        ["WindowsBinaryInspector.cs"] =
            "The publisher cache the pass above reads from several threads at once. A plain " +
            "dictionary here would be the quiet kind of race: right on most runs.",

        ["Readings.cs"] =
            "Reading the manager off the interface thread, because a full reading takes about " +
            "half a second and a window that stops answering for half a second looks broken. " +
            "Only the reading runs out there - nothing it returns touches anything on screen " +
            "until it is back. It was MainViewModel.cs until 2026-08-18, when the state machine " +
            "was cut out into its own file - backlog 198.",

        ["Readings.SecondPhase.cs"] =
            "The expensive pass, off the interface thread, moved out of Readings.cs on 2026-09-03 " +
            "when the size ratchet asked. Same argument as the file it came from and one number " +
            "of its own: this pass was measured at about seven and a half seconds of processor " +
            "over 810 entries, against under one and a half without it, so a window that ran it " +
            "on the interface thread would stop answering for that long. Nothing crosses the " +
            "boundary except the list going out and the filled list coming back through an await. " +
            "It runs INSIDE a reading, and the reentrancy guard in the other file is what makes a " +
            "second concurrency mechanism unnecessary rather than missing - which is the argument " +
            "that had to move with the code.",

        ["Execution.cs"] =
            "Ctrl+C, and this file was concurrent long before it said so. The handler behind " +
            "Console.CancelKeyPress runs on a thread of the runtime's choosing while the plan " +
            "is being carried out on this one, so the count of presses is shared state and " +
            "always was. It was raised with ++ until 2026-08-03, where two presses arriving " +
            "together could both read zero, both take the first level, and never reach the " +
            "second - the level that exists because a real run on a virtual machine left a " +
            "cascade half down. Interlocked is what made this visible to the guard, not what " +
            "made the file concurrent, and that is worth knowing: this list is only as good " +
            "as the constructs it looks for, and an event handler is not one of them.",

        ["Carrying.cs"] =
            "Carrying a plan out off the interface thread, added 2026-08-19. The measurement " +
            "points the OPPOSITE way to the one that decided how a plan is built: building one " +
            "over twenty selected entries took 37-40 ms and stayed on the calling thread, while " +
            "a single StartService for a service that never reports itself took 30 375-30 450 ms " +
            "across three runs on Windows Server 2025 - half a minute on one step, and a " +
            "selection has many. Two things cross the boundary and no more. The BulkRun comes " +
            "back through an await, so nothing it holds is touched out there. The progress " +
            "callback is handed over by MainWindow wrapped in a Progress<T> built on the " +
            "interface thread, which posts back to it - the one place this could go wrong " +
            "silently, since a bound property written from a worker thread is right often " +
            "enough to pass a test. There is no shared state at all: WindowsScmControl has no " +
            "instance fields and opens its own handle per call, which is what lets the " +
            "once-a-second reading keep running beside a run. WHAT IS AND IS NOT COVERED, said " +
            "plainly: CarryingGuards drives the panel through every state a run puts it in and " +
            "reads what reaches the screen, and it asserts that a press with nothing to carry " +
            "out never gets here at all. NO TEST CROSSES THIS THREAD BOUNDARY, because a test " +
            "that pressed the button would stop real services on the machine running it - that " +
            "half is covered by a run on a throwaway machine, which is the project's hard rule " +
            "about writes rather than a gap nobody noticed.",

        ["MainViewModel.Planning.cs"] =
            "Working out what an operation over a SELECTION would do, off the interface thread, " +
            "added 2026-09-03 for backlog 301. The comment this file used to carry defended the " +
            "opposite arrangement with a real measurement of the wrong axis: it timed how DEEP a " +
            "cascade is - 316-360 ms against 325-366 with none at all - where a reader looks for " +
            "how MANY entries are selected. tools/plan-probe measured that one over 799 entries " +
            "on 2026-09-02: a hundred selected and asked to stop cost 19-27 ms, four hundred " +
            "95-112, and the whole listing 224-240. Start at the same size is 0-1 ms because it " +
            "orders nothing, so every millisecond of it is a round trip to the manager rather " +
            "than a loop around one - which is why neither a topological sort nor a dictionary " +
            "would have bought anything. Owner's decision: 240 ms with the window not answering " +
            "is too much. EXACTLY ONE THING CROSSES THE BOUNDARY AND IT IS A SNAPSHOT: " +
            "RowIndex.Everything builds a new list on every read, so the entries handed out " +
            "there cannot be touched by the reading that runs once a second on this one. The " +
            "BulkPlan comes back through an await, and BulkPlanBuilder reads and reasons and " +
            "never writes. PlanBuildingGuards asserts the manager is not asked from the drawing " +
            "thread, and that a preview overtaken by a later one is dropped rather than shown - " +
            "a race that did not exist until this line did.",

        ["WindowsScmCatalog.cs"] =
            "Describing entries several at a time, added 2026-08-02. The same loop over the " +
            "same 810 entries costs 13-22 ms without opening a handle per entry and 455-475 " +
            "with, so about 440 ms of every listing was one processor waiting on the manager " +
            "while fifteen did nothing. Measured after: 108-122 ms, and the whole bws list " +
            "process went from 569-595 ms to 231-260. The ranges do not touch. " +
            "Order is held by index rather than by sorting afterwards, so the answer is the " +
            "sequential one by construction, and ReadAllContractTests compares a parallel " +
            "reading against a single-threaded one field by field - which is also the only " +
            "check on the assumption underneath this: that one OpenSCManager handle may be " +
            "used by OpenService from several threads at once.",

        ["Catalogue.Machines.cs"] =
            "The component catalogue's LOADING state, added 2026-09-16 (docs/PROJEKT-KATALOG-" +
            "20260916.md, section 3.4). A view is shown loading by being put over a model whose " +
            "machine has not answered, and the machine has not answered because its read is " +
            "waiting on a gate - a ManualResetEventSlim - that the sheet opens when it closes. " +
            "One pool thread per loading sample, released by Prepared.Dispose, and the count of " +
            "reads that reached the gate is Interlocked so a test can read it from the other " +
            "side. CatalogueViewGuards holds both: that disposing lets the read go, and that " +
            "the sheet's model reads its machine once.",

        ["Catalogue.Views.cs"] =
            "The same screen, one file over: the models behind the views are loaded with await " +
            "off any window - through the model's own Task.Run, which Readings.cs argued for - " +
            "and the views are built over them afterwards on the thread that can build a " +
            "control. The seam between the two is the file's header. What is added here is the " +
            "one read that is started and not awaited, which is the loading sample and is " +
            "listed as such in BackgroundWorkGuards.",

        ["ShellHandover.cs"] =
            "The Donate button's call into the shell, off the interface thread, added 2026-09-23 " +
            "after a review asked why the window waited on another process with no limit. The " +
            "argument is structural rather than measured: finding the desktop took 22-28 ms here " +
            "and a hung shell was never reproduced, but nothing bounded it, and the thread that " +
            "waited was the one that draws. A dedicated thread rather than Task.Run because the " +
            "shell's objects expect a single threaded apartment and pool threads are not one. " +
            "Nothing crosses the boundary but the answer, which comes back through an await, and " +
            "the one field is touched on the interface thread only. ExternalLinksGuards holds the " +
            "apartment, the bound, one hand-over at a time, and a throw reaching the press."
    };

    /// <summary>
    /// Constructs that mean work is happening somewhere else, or that state is being shared
    /// with somewhere else.
    ///
    /// Deliberately not <c>async</c> and <c>await</c>. Those are how a single thread waits
    /// without blocking, which is the opposite of the problem - and forbidding them would
    /// make the guard fire on the ordinary shape of a window.
    ///
    /// <b>WHITESPACE IS ALLOWED ROUND EVERY DOT SINCE 2026-09-03, AND WITHOUT IT THIS GUARD HAD A
    /// HOLE THE FORMATTER COULD OPEN BY ITSELF.</b> A call long enough to wrap is written
    /// <c>await Task</c> on one line and <c>.Run(...)</c> on the next, and a pattern asking for
    /// those two with nothing between them does not see it. There was exactly one such call in the
    /// product - the expensive pass in the window - and it had been invisible here for as long as
    /// it has existed.
    ///
    /// <b>It came to light because the OTHER assertion below went red.</b> A seam moved that pass
    /// into a file of its own, the file was listed as concurrent, and the guard against a list
    /// rotting into a wish reported that the new entry named a place doing nothing of the kind.
    /// So the check on the list is what found the hole in the pattern under it - which is the
    /// shape this whole class rests on: a rule nobody can keep by remembering it needs the thing
    /// checking it to be checked as well.
    /// </summary>
    private static readonly Regex Concurrency = new(
        @"\bTask\s*\.\s*Run\b|\bParallel\s*\.|\bnew\s+Thread\b|\block\s*\(|" +
        @"\bInterlocked\s*\.|\bMonitor\s*\.|\bVolatile\s*\.|\bThreadPool\s*\.|" +
        @"\bConcurrent(Dictionary|Bag|Queue|Stack)\b|" +
        @"\bSemaphoreSlim\b|\bManualResetEvent|\bAutoResetEvent\b|\bMutex\b|\bBarrier\b",
        RegexOptions.Compiled,
        Sources.Ceiling);

    [Fact]
    public void Only_the_places_that_argued_for_it_can_do_two_things_at_once()
    {
        var found = Matching();
        var strangers = found.Where(file => !Allowed.ContainsKey(file)).ToArray();

        Assert.True(
            strangers.Length == 0,
            "Concurrency appeared somewhere that has not argued for it. A race is the one " +
            "defect that does not reproduce on demand, so where it becomes possible is a " +
            "decision rather than a detail. Either this file belongs in the list with its " +
            "reason and its own test, or the work belongs on the thread that asked for it:" +
            Environment.NewLine + string.Join(Environment.NewLine, strangers));
    }

    [Fact]
    public void The_list_does_not_name_places_that_stopped_doing_it()
    {
        // The other direction, and the one that lets a list rot into a wish. An entry naming a
        // file that no longer does anything concurrently reads as an argument still being made.
        var found = Matching();
        var gone = Allowed.Keys.Where(file => !found.Contains(file)).ToArray();

        Assert.True(
            gone.Length == 0,
            "These files are listed as doing two things at once and no longer do. Remove them, " +
            "so the list keeps meaning what it says:" +
            Environment.NewLine + string.Join(Environment.NewLine, gone));
    }

    private static HashSet<string> Matching()
    {
        var files = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Sources.Shipped())
        {
            if (Concurrency.IsMatch(File.ReadAllText(file)))
            {
                files.Add(Path.GetFileName(file));
            }
        }

        return files;
    }
}
