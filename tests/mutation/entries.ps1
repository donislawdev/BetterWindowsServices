# The mutation registry: one entry per guard. Each names a behaviour, the edit that breaks it
# and the test that is supposed to notice.
#
# DATA ONLY. tests/mutation/mutate.ps1 runs it - on GitHub through .github/workflows/mutation.yml,
# every entry once a week in sixteen parts and the entries a change reaches on every pull
# request, and on a developer machine by hand. This file returns the list and nothing else: every
# object it writes to the pipeline is taken as an entry.
#
# Moved here on 2026-10-16. Until then the list and its runner were one file of 13466 lines
# outside version control, which is why CI could not run it and why a full pass happened on one
# machine, rarely. The cut was made by content - the runner kept what runs, this file kept what
# an entry is and the advice on writing one - and the list was compared entry by entry before and
# after the cut.
#
# When writing a new entry, prefer flipping a condition to "cond && false" over replacing it
# with "false" - the second orphans variables and gives you BROKEN instead of a result.
#
# THREE WAYS AN ENTRY IS WRONG, COUNTED ON 2026-08-03 WHEN TWELVE OF THEM WERE WRONG IN ONE
# SESSION. The owner said so out loud, and the three causes have different fixes.
#
#   STALE, six times.  The entry anchored in code that the author then MOVED. The size ratchet
#                      forces splits, a split carries methods into a new file, and the entry
#                      still names the old one. GREP THIS FILE FOR EVERY FILE YOU SPLIT, before
#                      running anything - it takes seconds and this run takes minutes.
#
#   MISSED, three.     The mutation did not remove the behaviour, most often because the
#                      behaviour had TWO gates and taking one away changed nothing. Ask the
#                      question before writing the entry: does this one edit really remove it,
#                      or does something else still hold it? If something does, the entry is not
#                      the problem - the code has one rule in two places.
#
#   BROKEN, twice.     The mutation did not compile. Once because weakening `if (x is null)`
#                      left the compiler unable to prove the next line safe, once because it
#                      named a parameter that had been removed. Mutate a VERDICT or a VALUE, not
#                      a condition that is proving something to the compiler.
#
# EIGHT MORE WAYS, COUNTED BETWEEN 2026-08-11 AND 2026-09-07. They lived in the assistant's
# memory outside the repository until 2026-09-21, where the sessions that needed them mostly did
# not look - cause 5 was repeated on 2026-09-06 with the note already written. They are here
# now because this is the file a session opens before writing an entry.
#
#   4. MISSED because the guard asked for a VALUE and the mutation broke the NOTIFICATION
#      (2026-08-11). A computed property answers correctly on every read whether or not anyone
#      was told it changed, so breaking the loop that raises PropertyChanged leaves the test
#      green - rightly. For a COMPUTED property ask which is the test: the value, or the
#      notification. If the value will always be right, the test has to be the notification.
#      The runner says "fix the SENTENCE or the guard, never the mutation", and it is right.
#
#   5. MISSED because the mutation DID NOT COMPILE and the runner could not tell that apart
#      from a guard that caught nothing (2026-08-13, and AGAIN 2026-09-06 with this note in
#      hand). `if (false)` makes the code below it unreachable, unreachable code is a warning,
#      and warnings are errors in this tree - so the mutation never went in and the verdict read
#      MISSED, which reads exactly like a guard that does not work. Worse than BROKEN, because
#      BROKEN at least says something went wrong. NEVER zero a condition with a constant. Invert
#      the comparison (`==` to `!=`), or mutate the VALUE the condition asks about - an enum
#      member to a sibling member compiles, is false in the case the test sets up, and leaves
#      nothing unreachable.
#
#   6. THE ENTRY SURVIVED A REVERSAL OF THE BEHAVIOUR AND WENT ON PROVING THE OPPOSITE OF THE
#      PRODUCT - IN GREEN (2026-08-25). An entry proving "no command is offered for an ask the
#      command line cannot make" broke the product by ADDING a fourth kind to the list of things
#      with a verb. When the command line grew that verb, the entry still compiled, still had a
#      live anchor, still came back caught - and proved the opposite rule to the one the product
#      now holds. stale-check.ps1 cannot see it: the anchor exists. When you close an ABSENCE,
#      search this file for that absence by name - an entry proving something is NOT there has
#      to be reversed in the same change that makes it be there. Swapping From and To and
#      renaming the test was all it took.
#
#   7. MISSED because the entry pointed at a guard STANDING ON A CLOCK, so it stopped firing
#      exactly when the suite was busiest (2026-08-26, backlog 238). A parallelism entry came
#      back MISSED in the full run and caught alone. Measured: the same mutation on a QUIET
#      machine passed silently one time in five, so load was the symptom, not the cause. The
#      cause was the shape of the measurement - every pair timed the slow variant first, then
#      the fast one, always in that order, so warm-up went to the fast one's account. Two-part
#      cure: interleave the order AND point the entry at a guard with no clock. The clock test
#      keeps its job and DELIBERATELY has no entry here - an entry that can come back MISSED
#      through nobody's fault is worse than no entry. What NOT to do: loosen the threshold. A
#      threshold chosen to pass under load will not catch a regression on a quiet machine, which
#      turns the guard into decoration in both directions at once.
#
#   8. A SEAM IN A FILE ORPHANS ENTRIES, SEVERAL TIMES IN ONE SESSION - cause 1 counted again
#      (2026-08-26). Four seams in one day, all forced by the size ratchet striking five times,
#      orphaned seven entries. stale-check.ps1 caught every one before anything ran, and that is
#      its whole worth: seconds where the full run costs twenty minutes. Run it AFTER EVERY SEAM,
#      not at the end of the packet - and remember that right after a mutation run its test
#      count is wrong, because the binaries are mid-restore (it once said 1033 against 1079).
#
#   9. MISSED because the test SPECIMEN FAILED IN BOTH DIRECTIONS - the default way a guard for
#      "we REFUSE this" proves nothing (2026-08-26). The guard was to show that a file with bytes
#      outside UTF-8 is refused rather than silently guessed. The specimen was five bytes of
#      nonsense - and the substituting decoder ALSO turns those into something that does not
#      parse, so both sides gave the same answer. General cure for every guard of a refusal: the
#      specimen must be something the version WITHOUT the guard ACCEPTS. Here, a real settings
#      file plus one key nobody reads with one bad byte inside: after silent substitution it
#      reads fine, with the refusing decoder it does not. Ask before writing the specimen: what
#      will the code this guard watches do with it when the guard is gone?
#
#  10. BROKEN because a `when (false)` filter is a CONSTANT and warnings are errors here -
#      cause 5 in new clothes (2026-08-26). To disarm `catch (JsonException)` the entry added
#      `when (false)`; the compiler sees a constant and the build refuses. `when (template is
#      null)` on a NON-NULLABLE parameter is the same thing. The filter has to be NEVER TRUE, not
#      CONSTANT: `when (source is null)` where null was excluded five lines up, or `when
#      (values.Length < 0)` on an array - neither folds, neither fires.
#
#  11. MISSED NOT FROM THE ENTRY BUT FROM THIS TOOL - the first cause on this list outside the
#      entry (2026-09-07, backlog 327). Two new entries came back MISSED while the same mutation
#      applied by hand reddened the named test at once. The run covers the WHOLE solution and the
#      test class name existed in TWO suites - on purpose, both surfaces have PlanSentenceGuards
#      asking the same questions. The suite the mutation did not touch printed zero failures
#      next to the one that went red, and the verdict read that FIRST. Why this is the worst
#      verdict this script can get wrong: MISSED says "the guard does not check what it claims",
#      so the next session deletes or rewrites a guard that worked. Fixed here - the xUnit
#      failure marker is asked first, because the filter names one test, so any such hit in the
#      output is that test - and both entries name the test with its namespace. The general cure
#      that stays regardless: having seen MISSED, apply the mutation by hand and run the named
#      test BEFORE touching the guard. One build, and it separates a guard fault from a tool
#      fault.
#
# BEFORE A FULL RUN, three things dry: grep this file for every file you touched, ask the
# two-gates question for every new entry, and compile the mutation in your head. Run the whole
# registry once per closed packet, not after every entry - `-Touched` in between.

# THE SIZE AND SHAPE ENTRIES NO LONGER LOOK FOR THE LONGEST FILE, SINCE 2026-09-23. They did from
# 2026-08-02, and the reason was sound: a ceiling set at the longest file is proved only by making
# THAT file longer, and which file that is changes every time somebody splits one - two entries
# naming a file came back MISSED for exactly that. This block worked the longest file out at run
# time by counting raw lines, in PowerShell.
#
# Two things ended that on the same day. The ratchet counts LINES OF CODE now, through a C# parser in
# tests/Bws.Architecture.Tests/CodeShape.cs - so finding "the longest file" here would take a second
# copy of that measure in PowerShell, and a second copy of a measure is a second thing to get wrong.
# And every ceiling is pinned to the measurement exactly, so what an entry has to show is simpler:
# that adding MORE than any ceiling standing today allows turns the named test red. The padding
# below is sized far past every number in SizeCeilings.cs and ShapeCeilings.cs on the day, and goes
# into one small fixed file each. A size that does not depend on today's headroom cannot go stale,
# which is the lesson the markup entry wrote down on 2026-08-19.
#
# COMMENTS ARE NOT PADDING ANY MORE, and that is the one thing to remember when writing an entry
# here: the old entries added "// one more line", which the lines-of-code ratchet does not count,
# so every one of them would have come back MISSED. The padding is code or markup.
#
# Generated rather than typed, because four hundred lines inside a registry entry is not something
# anybody reads.
$codePadding = 'internal enum ShapeMutantPadding' + [char]10 + '{' + [char]10 +
    ((0..399 | ForEach-Object { "    Member$_," }) -join [char]10) + [char]10 + '}' + [char]10 + [char]10
$markupPadding = '    <system:String x:Key="ShapeMutantPadding">' + [char]10 +
    ((0..299 | ForEach-Object { "        line $_" }) -join [char]10) + [char]10 + '    </system:String>' + [char]10
$longMethod = 'internal static class ShapeMutantLength' + [char]10 + '{' + [char]10 +
    '    internal static int Count()' + [char]10 + '    {' + [char]10 + '        var total = 0;' + [char]10 +
    ((0..149 | ForEach-Object { '        total++;' }) -join [char]10) + [char]10 +
    '        return total;' + [char]10 + '    }' + [char]10 + '}' + [char]10 + [char]10
$forkingMethod = 'internal static class ShapeMutantForks' + [char]10 + '{' + [char]10 +
    '    internal static bool Decide(int value) => ' + ((0..39 | ForEach-Object { "value == $_" }) -join ' || ') + ';' + [char]10 +
    '}' + [char]10 + [char]10
$deepMethod = 'internal static class ShapeMutantDepth' + [char]10 + '{' + [char]10 +
    '    internal static int Nest(bool a) { if (a) { if (a) { if (a) { if (a) { if (a) { if (a) { return 1; } } } } } } return 0; }' + [char]10 +
    '}' + [char]10 + [char]10
$wideSignature = 'internal static class ShapeMutantWidth' + [char]10 + '{' + [char]10 +
    '    internal static int Take(' + ((0..14 | ForEach-Object { "int value$_" }) -join ', ') + ') => value0;' + [char]10 +
    '}' + [char]10 + [char]10
$manyMethods = 'internal static class ShapeMutantMethods' + [char]10 + '{' + [char]10 +
    ((0..99 | ForEach-Object { "    internal static void Member$_() { }" }) -join [char]10) + [char]10 + '}' + [char]10 + [char]10
$muchState = 'internal sealed class ShapeMutantState' + [char]10 + '{' + [char]10 +
    ((0..49 | ForEach-Object { "    internal int Member$_ { get; set; }" }) -join [char]10) + [char]10 + '}' + [char]10 + [char]10

# Each entry names one behaviour, the edit that breaks it, and the test that is supposed to
# notice. The test name is the whole point: "some test went red" is not the claim being
# made - the claim is that THIS guard covers THIS behaviour.
$mutations = @(
    # RELEASE-001 of the pre-release audit, 2026-09-09. The list of seven names, the warning kind
    # and the sentence all existed since 2026-09-06 and only ForcedStop asked - so `stop PlugPlay`
    # said nothing about it. THESE TWO WERE VERIFIED BY HAND BEFORE BEING WRITTEN DOWN: breaking
    # the call reddened four of the eight guards in CriticalEntryGuards, and the four that stayed
    # green are the ones asserting an ABSENCE plus the forcing path, which that break does not
    # touch. So the registry is recording a check that was watched failing, not one assumed to.
    @{
        Name = 'critical entry: an ordinary stop names the ones the machine needs'
        File = 'src\Bws.Core\Planning\CriticalEntries.cs'
        #
        # RE-ANCHORED 2026-09-24: the condition became PlanBuilder.StopsPolitely when a startup
        # setting of disabled learned to carry a stop. The swap it makes is unchanged.
        From = 'if (PlanBuilder.StopsPolitely(action, target))
        {
            var stopping = Named([.. cascade, target]);'
        To = 'if (PlanBuilder.StopsPolitely(action, target))
        {
            var stopping = Named([]);'
        Test = 'CriticalEntryGuards.A_stop_names_the_entry_the_machine_does_not_work_without'
    },
    @{
        # ASKED OF THE CASUALTY LIST RATHER THAN THE TARGET, which is the half a person never
        # types. This mutation leaves the target answered and drops the cascade, so a plan for an
        # entry somebody named still warns and one that drags a critical entry in silently does
        # not - the exact shape the warning exists for.
        Name = 'critical entry: a cascade is asked as well as the entry somebody named'
        File = 'src\Bws.Core\Planning\CriticalEntries.cs'
        From = 'var stopping = Named([.. cascade, target]);'
        To = 'var stopping = Named([target]);'
        Test = 'CriticalEntryGuards.A_critical_entry_arriving_in_the_cascade_is_named_as_well'
    },
    @{
        # The deferred half, owner's decision of 2026-09-09. Its own kind because the harm lands at
        # the next boot rather than now, and this mutation is what would happen if somebody decided
        # the distinction was not worth two enum values.
        Name = 'critical entry: disabling one is said in its own words'
        File = 'src\Bws.Core\Planning\CriticalEntries.cs'
        From = 'PlanWarningKind.CriticalStartType, target.ServiceName, disabling));'
        To = 'PlanWarningKind.CriticalService, target.ServiceName, disabling));'
        Test = 'CriticalEntryGuards.Disabling_one_is_its_own_sentence_rather_than_the_stop_one'
    },
    @{
        # The window half of RELEASE-001, and it is a decision rather than a mechanism: the box
        # appeared on an ordinary stop by itself the moment the core raised the warning there,
        # because NeedsTyping reads warnings rather than action kinds. Owner's decision of
        # 2026-09-09 was to keep it. This entry is what would notice somebody deciding otherwise.
        Name = 'critical entry: the window asks for the name on an ordinary stop too'
        File = 'src\Bws.Gui\ViewModels\Planned.Forcing.cs'
        # Anchored on Heavy since 2026-09-16, the day NeedsTyping and the footer's danger line
        # started reading one list of kinds instead of two copies of it.
        From = 'warning.Kind is PlanWarningKind.TerminationTakesWithIt
            or PlanWarningKind.CriticalService
            or PlanWarningKind.CriticalStartType;'
        To = 'warning.Kind is PlanWarningKind.TerminationTakesWithIt;'
        Test = 'CriticalEntryWindowGuards.An_ordinary_stop_of_a_critical_entry_asks_for_the_name_as_well'
    },
    @{
        # RELEASE-003. PublishSingleFile produced six files from the day the window existed, and
        # every build was green throughout - nothing in the suite had ever looked at what a publish
        # writes. The guard reads the declaration rather than the output, so this mutation is the
        # shape the loss would really take: somebody deleting a property they did not recognise.
        Name = 'portable executable: the window bundles the native halves of WPF'
        File = 'src\Bws.Gui\Bws.Gui.csproj'
        From = '<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>'
        To = '<IncludeNativeLibrariesForSelfExtract>false</IncludeNativeLibrariesForSelfExtract>'
        Test = 'PortableExecutableGuards.The_window_publishes_as_one_file_including_the_native_halves_of_wpf'
    },
    @{
        # RELEASE-008, and the one that would matter on a real machine: a box somebody types ninety
        # into, and a run that waits sixty. Everything else about this control could be right and
        # this one line makes the whole of it a lie.
        Name = 'waiting: what was typed into the box is what the run is given'
        File = 'src\Bws.Gui\MainWindow.Carrying.cs'
        From = 'TimeSpan.FromSeconds(_model.Planned.Waiting),'
        # The default moved to the core on 2026-09-15 - this said Carrying.Ceiling, which no
        # longer exists, and stale-check does not see a To that does not compile.
        To = 'StepCeiling.Default,'
        Test = 'WaitingGuards.What_was_typed_into_the_box_is_what_the_run_is_given'
    },
    @{
        # Found by a screenshot rather than by a test, and this is that fault written down: the
        # ceiling is read at the press, so a box still editable during the run takes a number and
        # throws it away. Twelve assertions passed while it was wrong.
        Name = 'waiting: a run takes the box off the screen'
        File = 'src\Bws.Gui\ViewModels\Planned.Waiting.cs'
        From = 'Showing
        && !Busy
        && _run is null'
        To = 'Showing
        && _run is null'
        Test = 'WaitingGuards.A_run_takes_the_box_off_the_screen'
    },
    @{
        # docs/11 section 7 asks for progress above ten seconds. Without the elapsed half the line
        # names the step and says nothing about how long it has been going, which is the state this
        # window was in from the day it could carry a plan out.
        Name = 'waiting: the line says how long this step has been going'
        File = 'src\Bws.Gui\ViewModels\PlanWords.cs'
        From = 'return seconds < 1
            ? step
            : Texts.Of("gui.plan.progress.waiting", step, seconds, ceiling);'
        To = 'return step;'
        Test = 'WaitingGuards.Past_a_second_it_says_how_long_and_out_of_how_long'
    },
    # RELEASE-007 of the same audit. The window has said this since it learned to plan and the
    # terminal never did - an entry the manager did not hand over leaves nothing to count, so this
    # is the only admission that cannot be worked out from the reading afterwards.
    @{
        Name = 'elevation: a run without administrator rights says so'
        File = 'src\Bws.Cli\Execution.cs'
        From = '!elevated && kind != CommandKind.SnapshotCreate;'
        To = 'false && kind != CommandKind.SnapshotCreate;'
        Test = 'ElevationNoticeGuards.An_unelevated_run_says_so_on_every_command_but_the_one_that_says_it_better'
    },
    @{
        # The other direction, and it is the one that decides whether anybody still reads the line
        # in a month. Saying it on an elevated run is not a smaller mistake than staying silent on
        # an unelevated one - it is the mistake that makes the true sentence invisible.
        Name = 'elevation: an elevated run stays quiet'
        File = 'src\Bws.Cli\Execution.cs'
        From = '!elevated && kind != CommandKind.SnapshotCreate;'
        To = 'kind != CommandKind.SnapshotCreate;'
        Test = 'ElevationNoticeGuards.An_elevated_run_says_nothing_on_any_command'
    },
    # RELEASE-010 of the same audit. Two entries because the change has two halves that fail
    # independently: dropping the serialiser's vocabulary, and keeping the position it carried.
    # A repair that did only the first would read cleanly and send nobody to the right line.
    @{
        Name = 'unreadable snapshot: the serialiser vocabulary does not reach a person'
        File = 'src\Bws.Core\Snapshots\SnapshotJson.cs'
        From = 'failure = Said(problem);'
        To = 'failure = problem.Message;'
        Test = 'SnapshotTests.A_parser_failure_is_said_in_this_tool_s_words_rather_than_the_serialiser_s'
    },
    @{
        # Counting from zero is the version of this that looks right and points at the wrong line -
        # confidently, which is the failure this project treats as worse than saying nothing.
        Name = 'unreadable snapshot: the position is counted the way an editor counts'
        File = 'src\Bws.Core\Snapshots\SnapshotJson.cs'
        From = 'var where = (line + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);'
        To = 'var where = line.ToString(System.Globalization.CultureInfo.InvariantCulture);'
        Test = 'SnapshotTests.The_position_of_the_trouble_survives_and_is_counted_from_one'
    },
    @{
        # S-11 of the performance report, 2026-09-29. Chosen because NO other test in the core
        # suite sees it: every Polish display name would come out as \u escapes, the file would
        # still parse, still sort and still end with a newline - and every snapshot anybody keeps
        # would differ from the next one on each of those lines. Only the kept copy notices.
        Name = 'snapshot golden: characters are written as themselves, not escaped'
        File = 'src\Bws.Core\Snapshots\SnapshotJson.cs'
        From = 'Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping'
        To = 'Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default'
        Test = 'SnapshotGoldenTests.The_file_is_written_byte_for_byte_as_the_kept_copy'
    },
    # Backlog 8, 2026-09-06. The field behind these four went unread from the day the enumeration
    # was written, so a stop the manager was never going to take looked exactly like one that was
    # going to work - and the difference arrived afterwards, as an error number.
    @{
        Name = 'refused stop: an entry that will not take one is named before anything runs'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = '.Where(entry => entry.AcceptsStop is { IsPresent: true, Value: false })'
        To = '.Where(entry => false)'
        Test = 'PlanPreflightTests.An_entry_that_will_not_take_a_stop_is_named_before_anything_runs'
    },
    @{
        # The four read states earning their keep. An empty set of accepted controls is what the
        # manager reports for anything not running, so a check that forgot to ask whether the field
        # was READ would tell somebody a stopped service refuses to be stopped.
        Name = 'refused stop: an entry already stopped is not called a refusal'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = '.Where(entry => entry.AcceptsStop is { IsPresent: true, Value: false })'
        To = '.Where(entry => !entry.AcceptsStop.IsPresent || !entry.AcceptsStop.Value)'
        Test = 'PlanPreflightTests.An_entry_that_is_already_stopped_is_not_reported_as_refusing_a_stop'
    },
    @{
        # The one entry here that no unit test could hold. Every test of the path above hands the
        # field in already decided, so all of them stay green if the mask tests the wrong bit. Only
        # the comparison against ServiceController on a real machine can see it.
        Name = 'refused stop: the bit read is the stop bit'
        File = 'src\Bws.Core\ManagerBlocks.cs'
        # Re-anchored 2026-10-05: the recognizer bit (stability R-7) now follows on the next line.
        From = 'AcceptsStop: (status.dwControlsAccepted & PInvoke.SERVICE_ACCEPT_STOP) != 0,'
        To = 'AcceptsStop: true,'
        Test = 'PlanContractTests.Whether_an_entry_takes_a_stop_is_what_the_system_says_it_is'
    },
    @{
        # The other half of the same slice: an entry stuck in StopPending cannot be moved by asking
        # again, so the process still holding it is the only thing a person can act on. Dropping
        # the reading leaves the sentence true and useless.
        Name = 'refused stop: a step that gave up names the process holding the entry'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '            _ => Result(step, StepOutcome.TimedOut, Where(seen), Holding(seen), Elapsed(started))'
        To = '            _ => Result(step, StepOutcome.TimedOut, Where(seen), Reading<int>.NotRead(), Elapsed(started))'
        Test = 'PlanRunnerTests.An_entry_that_stops_reporting_progress_is_given_up_on_when_its_own_promise_runs_out'
    },
    @{
        # The machine readable half of the same fact. Owner's decision 2026-09-06: a number readable
        # only out of an English sentence is a number a script has to match prose for, which the
        # contract in docs/02 forbids one field over.
        Name = 'refused stop: the document names the process as well as the sentence does'
        File = 'src\Bws.Cli\PlanJson.cs'
        From = 'result.ProcessId.IsPresent ? result.ProcessId.Value : null,'
        To = 'null,'
        Test = 'PlanDocumentGuards.A_step_that_gave_up_names_the_process_still_holding_the_entry'
    },

    # The forcing verb, 2026-09-06. Seven entries, and two of them hold decisions that came out the
    # other way round when the edge cases were walked before any of this was written.
    @{
        Name = 'forced stop: an entry already asked to stop is not asked again'
        File = 'src\Bws.Core\Planning\ForcedStop.cs'
        From = 'if (!ProcessNeighbours.AlreadyAsked(target))'
        To = 'if (true)'
        Test = 'ForcedStopTests.An_entry_already_asked_to_stop_is_not_asked_again'
    },
    @{
        # Sharing a process is not depending on one. The cascade word is "would break otherwise",
        # which claims a dependency that is not there - found on a live machine within a minute of
        # the first plan, where ending RpcSs takes RpcEptMapper with it.
        Name = 'forced stop: a housemate is not called a dependant'
        File = 'src\Bws.Core\Planning\ForcedStop.cs'
        From = 'steps.Add(step(sharing, StepOperation.Stop, StepReason.SharesTheProcess));'
        To = 'steps.Add(step(sharing, StepOperation.Stop, StepReason.Cascade));'
        Test = 'ForcedStopTests.Entries_living_in_the_same_process_are_steps_with_a_reason_of_their_own'
    },
    @{
        # The ordinary rule stops the plan once a step fails to arrive, and under it the only step
        # that was ever going to help would be skipped on the exact plan built for that situation.
        Name = 'forced stop: the escalation runs after the stop it stands behind'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = '(forwardFailed && !putsBack && !behind)'
        To = '(forwardFailed && !putsBack)'
        Test = 'EscalationRunTests.The_process_is_ended_when_the_stop_before_it_ran_out_of_time'
    },
    @{
        # And the other half of that decision, which is the opposite of how a restoring step behaves.
        Name = 'forced stop: nothing is ended after somebody asks to stop'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = 'if (abandoned || (cancelled && !putsBack))'
        To = 'if (abandoned || (cancelled && !putsBack && !behind))'
        Test = 'EscalationRunTests.Nothing_is_ended_after_somebody_asks_to_stop'
    },
    @{
        # Windows hands out process numbers again once a process is gone, and a plan is built one
        # moment and carried out the next.
        Name = 'forced stop: a process that moved since the preview is left alone'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = 'return holding.IsPresent && holding.Value == step.ProcessId'
        To = 'return holding.IsPresent'
        Test = 'EscalationRunTests.A_process_that_is_no_longer_the_one_the_plan_named_is_left_alone'
    },
    # Package B of the external stability report, 2026-09-29 (W-4, W-2, W-6, W-10, W-12). Every
    # entry below is a preview that promised less than its run, or a run that did more than its
    # preview, on the one ask that ends a process.
    @{
        # Running dependants outside the plan are a refusal on a forced stop, where an ordinary stop
        # gets a warning - the last step asks no manager, so nothing else would stop the ending.
        Name = 'forced stop: running dependants outside the plan are a refusal'
        File = 'src\Bws.Core\Planning\ForcedStop.cs'
        From = 'action.IncludeDependents || blocking.Count == 0'
        To = 'true'
        Test = 'ForcedStopRefusalTests.A_forced_stop_refuses_while_entries_depending_on_it_run_outside_the_plan'
    },
    @{
        # Skipping the courtesy skips the cascade too, so the pair would promise dependants stopped.
        Name = 'forced stop: skipping the courtesy cannot carry the dependants'
        File = 'src\Bws.Core\Planning\ForcedStop.cs'
        From = 'if (action is { Immediate: true, IncludeDependents: true })'
        To = 'if (action is { Immediate: true, IncludeDependents: true, AlsoStop: true })'
        Test = 'ForcedStopRefusalTests.Skipping_the_courtesy_cannot_carry_the_dependants'
    },
    @{
        # A running entry outside the plan that needs a neighbour would lose it with the process.
        Name = 'forced stop: a running entry needing a neighbour is a refusal'
        File = 'src\Bws.Core\Planning\ForcedStop.cs'
        From = 'needing.AddRange(Running(entries, dependents).Where(name => !dying.Contains(name)));'
        To = 'needing.AddRange(Running(entries, dependents).Where(name => false));'
        Test = 'ForcedStopRefusalTests.A_running_entry_that_needs_a_neighbour_is_a_refusal'
    },
    @{
        Name = 'forced stop: a neighbour whose dependants cannot be read is a refusal'
        File = 'src\Bws.Core\Planning\ForcedStop.cs'
        From = 'if (dependents.Outcome == ReadOutcome.Denied)'
        To = 'if (dependents.Outcome == ReadOutcome.NotRead)'
        Test = 'ForcedStopRefusalTests.A_neighbour_whose_dependants_cannot_be_read_is_a_casualty_list_known_to_be_short'
    },
    @{
        Name = 'forced stop: a critical entry arriving with the cascade is named'
        File = 'src\Bws.Core\Planning\ForcedStop.cs'
        From = 'CriticalEntries.Named([.. cascade, .. ending.Sharing, target])'
        To = 'CriticalEntries.Named([.. ending.Sharing, target])'
        Test = 'ForcedStopRefusalTests.A_critical_entry_arriving_with_the_cascade_is_named_on_a_forced_stop'
    },
    @{
        # With --force the neighbours have no steps, and this list is all a way back can learn them from.
        Name = 'forced stop: the ending names what it takes with it'
        File = 'src\Bws.Core\Planning\ForcedStop.cs'
        From = 'TakesWithIt: [.. ending.Sharing.Select(entry => entry.ServiceName)]));'
        To = 'TakesWithIt: []));'
        Test = 'ForcedStopRefusalTests.The_ending_names_the_neighbours_it_takes_with_it'
    },
    @{
        # Neighbours first let a neighbour's refusal skip the entry's own polite stop (W-2).
        Name = 'forced stop: the entry is asked before its neighbours'
        File = 'src\Bws.Core\Planning\ForcedStop.cs'
        From = 'steps.Add(step(sharing, StepOperation.Stop, StepReason.SharesTheProcess));'
        To = 'steps.Insert(steps.Count - 1, step(sharing, StepOperation.Stop, StepReason.SharesTheProcess));'
        Test = 'ForcedStopRefusalTests.The_entry_is_asked_before_its_neighbours_and_the_neighbours_before_the_ending'
    },
    @{
        Name = 'forced restart: a disabled neighbour could not come back'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = 'StuckDown(asked.Kind, target, [.. cascade, .. ending?.Sharing ?? []])'
        To = 'StuckDown(asked.Kind, target, cascade)'
        Test = 'ForcedStopRefusalTests.A_disabled_neighbour_of_a_forced_restart_could_not_come_back'
    },
    @{
        Name = 'refused stop: a neighbour that will not take one is named'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = 'IEnumerable<ScmEntry> asked = [.. cascade, target, .. ending?.Sharing ?? []];'
        To = 'IEnumerable<ScmEntry> asked = [.. cascade, target];'
        Test = 'ForcedStopRefusalTests.A_neighbour_that_will_not_take_a_stop_is_named_before_anything_runs'
    },
    @{
        # A dependant that refused is still running on the process - the owner's decision of 2026-09-29.
        Name = 'forced stop: a dependant that did not stop holds the ending'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = 'cascadeFailed |= step.Reason == StepReason.Cascade;'
        To = 'cascadeFailed |= step.Reason == StepReason.Restore;'
        Test = 'ForcedRunTests.A_dependant_that_will_not_stop_holds_the_ending'
    },
    @{
        # A neighbour dies with the process either way, so its refusal must not skip the ending.
        Name = 'forced stop: a neighbour refusing holds nothing back'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = 'var behind = reason is StepReason.Escalation or StepReason.SharesTheProcess;'
        To = 'var behind = reason is StepReason.Escalation;'
        Test = 'ForcedRunTests.A_neighbour_that_will_not_stop_holds_nothing_back'
    },
    @{
        # An entry that stops politely leaves the process, and its neighbours, where they are.
        Name = 'forced stop: neighbours stay running when the entry stops by itself'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = 'return !(holding.IsPresent && holding.Value == ending.ProcessId);'
        To = 'return holding.IsPresent && holding.Value != ending.ProcessId;'
        Test = 'ForcedRunTests.Neighbours_stay_running_when_the_entry_stops_by_itself'
    },
    @{
        Name = 'forced stop: a neighbour is left alone when the entry cannot be read'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = 'return !(holding.IsPresent && holding.Value == ending.ProcessId);'
        To = 'return holding.IsPresent && holding.Value != ending.ProcessId;'
        Test = 'ForcedRunTests.A_neighbour_is_left_alone_when_the_entry_cannot_be_read'
    },
    @{
        Name = 'forced stop: a neighbour is left alone when the process moved'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = 'return !(holding.IsPresent && holding.Value == ending.ProcessId);'
        To = 'return !holding.IsPresent;'
        Test = 'ForcedRunTests.A_neighbour_is_left_alone_when_the_process_is_not_the_one_the_plan_named'
    },
    @{
        # Re-anchored 2026-10-07 (backlog 539): the filter became a switch over the skip reason, and this
        # still takes the neighbour's stop back into the count.
        Name = 'completed: a neighbour the plan did not need is not counted'
        File = 'src\Bws.Core\Planning\PlanRun.cs'
        From = '        SkipReason.ProcessStays => true,'
        To = '        SkipReason.ProcessStays => false,'
        Test = 'ForcedRunTests.Neighbours_stay_running_when_the_entry_stops_by_itself'
    },
    @{
        # Backlog 539: a forced restart whose polite stop worked ended with exit code 3, because the starts
        # that would have put the neighbours back were counted as not arrived.
        Name = 'completed: the start of a neighbour whose process stayed is not counted'
        File = 'src\Bws.Core\Planning\PlanRun.cs'
        From = '        SkipReason.NothingToPutBack => Stayed(result.Step.ServiceName),'
        To = '        SkipReason.NothingToPutBack => false,'
        Test = 'ForcedRunTests.A_forced_restart_whose_polite_stop_works_is_complete'
    },
    @{
        # The other half of 539: nothing to put back stays "not arrived" for the entry itself (W-5 c).
        Name = 'completed: nothing to put back still counts for an entry whose own stop was not skipped'
        File = 'src\Bws.Core\Planning\PlanRun.cs'
        From = '        SkipReason.NothingToPutBack => Stayed(result.Step.ServiceName),'
        To = '        SkipReason.NothingToPutBack => true,'
        Test = 'ForcedRunTests.A_forced_restart_of_an_entry_somebody_stopped_first_is_not_complete'
    },
    @{
        # A run with one neighbour skipped must not take every put-back out of the count, the entry's own
        # included - the name is what ties the start to the stop.
        Name = 'completed: a neighbour stays out of the count by its own name'
        File = 'src\Bws.Core\Planning\PlanRun.cs'
        From = '        && string.Equals(result.Step.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));'
        To = '        && serviceName.Length > 0);'
        Test = 'ForcedRunTests.A_forced_restart_of_an_entry_somebody_stopped_first_is_not_complete'
    },
    @{
        # W-10: a neighbour the ending took down had no line on the way back.
        Name = 'way back: a neighbour that died with the process is handed back'
        File = 'src\Bws.Core\Planning\NetEffect.cs'
        From = 'if (!moves.ContainsKey(name) && !foundStopped.Contains(name))'
        To = 'if (!moves.ContainsKey(name) && foundStopped.Contains(name))'
        Test = 'ForcedRunTests.A_neighbour_that_died_with_the_process_is_handed_back'
    },
    @{
        Name = 'way back: a neighbour found already stopped is not handed back'
        File = 'src\Bws.Core\Planning\NetEffect.cs'
        From = 'if (result is { SkippedBecause: SkipReason.AlreadyThere, Step.Operation: StepOperation.Stop })'
        To = 'if (result is { SkippedBecause: SkipReason.Cancelled, Step.Operation: StepOperation.Stop })'
        Test = 'ForcedRunTests.A_neighbour_found_already_stopped_is_not_handed_back'
    },
    @{
        # W-12: kill takes --dependents, and a line without it plans a different ask.
        Name = 'command: kill hands back its dependants'
        File = 'src\Bws.Core\Planning\EquivalentCommand.cs'
        From = 'or ActionKind.ForceStop or ActionKind.ForceRestart)'
        To = ')'
        Test = 'EquivalentCommandTests.A_forced_stop_with_its_dependants_hands_back_the_switch_it_was_planned_with'
    },
    @{
        Name = 'command line: force and dependents are refused together and not apart'
        File = 'src\Bws.Cli\Refusals.cs'
        From = 'if (options.Kind != CommandKind.Kill || !options.Force || !options.Dependents)'
        To = 'if (options.Kind != CommandKind.Kill || !options.Force || options.Dependents)'
        Test = 'ArgumentRefusalTests.Force_and_dependents_on_kill_are_refused_together_and_not_apart'
    },
    @{
        # No offer under 1051 or 5 - the forced plan refuses the first and cannot answer the second.
        Name = 'forcing: no offer under a refusal forcing does not answer'
        File = 'src\Bws.Gui\ViewModels\Planned.Forcing.cs'
        From = '|| result.ErrorCode is DependentsStillRunning or AccessDenied'
        To = '|| result.ErrorCode is DependentsStillRunning + AccessDenied'
        Test = 'ForcedStopGuards.A_stop_refused_for_a_reason_forcing_does_not_answer_offers_nothing'
    },
    @{
        # Zero is what the manager reports for an entry that is not running, and it is also the
        # system idle process. Four is the kernel.
        Name = 'forced stop: a number no service process has is refused'
        File = 'src\Bws.Core\Planning\ProcessNeighbours.cs'
        From = 'return processId is 0 or TheKernel || processId == Environment.ProcessId ? null : processId;'
        To = 'return processId;'
        Test = 'ForcedStopTests.A_number_no_service_process_ever_has_is_refused_rather_than_ended'
    },
    @{
        # On an ordinary stop an incomplete cascade is said out loud. Here it is a list of what
        # dies, known to be short, and rule 5 of the untouchable rules leaves no wording for that.
        Name = 'forced stop: a casualty list known to be short is a refusal'
        File = 'src\Bws.Core\Planning\ForcedStop.cs'
        # THE KIND LOOKED FOR RATHER THAN THE CONDITION, AND THE FIRST ATTEMPT WAS `if (false)` -
        # which made the return under it unreachable, which is an error in this build. A mutation
        # that does not compile proves nothing in either direction.
        From = 'warning.Kind == PlanWarningKind.CascadeUnreadable'
        To = 'warning.Kind == PlanWarningKind.AlreadyThere'
        Test = 'ForcedStopTests.A_casualty_list_known_to_be_short_is_a_refusal_rather_than_a_warning'
    },
    @{
        # Re-anchored 2026-09-29, when the condition grew a line for answers kept from an earlier
        # reading. The mutation is the same: every entry's file asked about, repeats included.
        Name = 'second pass: one question per distinct file'
        File = 'src\Bws.Core\SecondPass.cs'
        From = '                && !Answered(entry)
                && seen.Add(entry.BinaryFile.Value!))'
        To = '                && !Answered(entry))'
        Test = 'SecondPassTests.One_question_per_distinct_file_however_many_are_asked_at_once'
    },
    @{
        Name = 'second pass: answers stay attached to their own file'
        File = 'src\Bws.Core\SecondPass.cs'
        From = 'byFile[files[index]] = answers[index];'
        To = 'byFile[files[index]] = answers[(index + 1) % files.Count];'
        Test = 'SecondPassConcurrencyTests.Every_answer_belongs_to_the_file_it_is_attached_to'
    },
    @{
        Name = 'second pass: result keeps the order of the input'
        File = 'src\Bws.Core\SecondPass.cs'
        From = @'
        foreach (var entry in entries)
        {
            filled.Add(Fill(entry, answers));
        }
'@
        To = @'
        foreach (var entry in entries.OrderBy(entry => entry.ServiceName, StringComparer.Ordinal))
        {
            filled.Add(Fill(entry, answers));
        }
'@
        Test = 'SecondPassTests.The_order_of_the_result_is_the_order_of_the_input_whoever_answered_first'
    },
    @{
        Name = 'signatures: the catalogue path is walked'
        File = 'src\Bws.Core\WindowsBinaryInspector.Catalogue.cs'
        # Forces the catalogue lookup to find nothing, rather than skipping the branch. The
        # obvious edit - turning the condition above into "if (true)" - makes the next
        # statement unreachable, and warnings are errors here, so it comes back BROKEN and
        # proves nothing. This is the "cond && false" advice from the header, in the shape
        # this particular guard needs.
        From = 'var catalogue = PInvoke.CryptCATAdminEnumCatalogFromHash(admin, hash);'
        To = 'var catalogue = PInvoke.CryptCATAdminEnumCatalogFromHash(admin, hash) & 0;'
        Test = 'SignatureContractTests.The_catalogue_path_is_walked_and_not_quietly_skipped'
    },
    @{
        Name = 'diff: a refused field is not compared'
        File = 'src\Bws.Core\Snapshots\SnapshotDiff.cs'
        From = '.Union(refused, StringComparer.Ordinal)'
        To = ''
        Test = 'SnapshotDiffTests.A_field_one_side_could_not_read_is_named_and_not_counted_as_a_change'
    },
    @{
        Name = 'diff: only the elevated side can hide an entry'
        File = 'src\Bws.Core\Snapshots\SnapshotDiff.cs'
        From = 'private static bool Invisible(bool elevationDiffers, bool blind) => elevationDiffers && !blind;'
        To = 'private static bool Invisible(bool elevationDiffers, bool blind) => elevationDiffers;'
        Test = 'SnapshotDiffTests.An_entry_only_the_restricted_snapshot_holds_really_did_appear'
    },
    @{
        Name = 'diff: a new process identifier is never a difference'
        File = 'src\Bws.Core\Snapshots\SnapshotDiff.cs'
        From = 'field != Identity && field != Unstable &&'
        To = 'field != Identity &&'
        Test = 'SnapshotDiffTests.A_new_process_identifier_is_never_a_difference'
    },
    @{
        Name = 'diff: running state is not filed as configuration'
        File = 'src\Bws.Core\Snapshots\SnapshotDiff.cs'
        From = 'field == State ? DifferenceGroup.RunningState : DifferenceGroup.Configuration'
        To = 'DifferenceGroup.Configuration'
        Test = 'SnapshotDiffTests.What_was_running_is_reported_apart_from_how_it_is_set_up'
    },
    @{
        # New on 2026-08-04 with the breaking change that made the engine total. Before it, a
        # snapshot holding one service name twice ended the comparison from inside a dictionary,
        # with a message naming neither side - and the engine was safe only because every document
        # reaching it had come through the reader that refuses such a file.
        Name = 'diff: a broken snapshot is refused, not thrown over'
        File = 'src\Bws.Core\Snapshots\SnapshotDiff.cs'
        From = 'if (snapshot.MissingParts(out var fault) || snapshot.BrokenEntries(out fault))'
        To = 'if ((snapshot.MissingParts(out var fault) || snapshot.BrokenEntries(out fault)) && false)'
        Test = 'DiffPropertyTests.Any_two_snapshots_give_back_a_comparison_or_a_reason'
    },
    @{
        # The worst fault this engine can have: every field one side never asked about reported as
        # drift. A field NEITHER side read is comparable, because both hold nothing - a field
        # exactly one side read is not. Widening that intersection to a union collapses the two.
        #
        # Found by asking what the property would let through, before writing the entry rather
        # than after. The first version of this comment claimed nothing else would catch it, and
        # MEASURING IT SAID OTHERWISE: with the mutation applied, the whole suite reddens in two
        # places - this property, and DiffContractTests.A_snapshot_taken_now_has_no_configuration
        # _differences_against_the_machine_it_came_from. No UNIT test sees it, because every
        # fixture leaves the same fields unread on both sides, so the union and the intersection
        # agree. The integration test does, and it needs a live machine to do it.
        Name = 'diff: a field only one side asked about is not compared'
        File = 'src\Bws.Core\Snapshots\SnapshotDiff.cs'
        From = 'var neither = NotAsked(before).Intersect(NotAsked(after), StringComparer.Ordinal)'
        To = 'var neither = NotAsked(before).Union(NotAsked(after), StringComparer.Ordinal)'
        Test = 'DiffPropertyTests.A_field_one_side_could_not_read_is_never_a_difference'
    },
    @{
        # Measured on a real pair of files rather than imagined: an elevated snapshot against a
        # restricted one put five entries under "changed" with nothing changed about them, only a
        # descriptor one side was refused. The summary read "5 changed, 0 differences" and
        # --exit-code would have failed a pipeline over a comparison that found nothing.
        Name = 'diff: an entry with nothing changed is not filed as changed'
        File = 'src\Bws.Core\Snapshots\SnapshotDiff.cs'
        From = '(difference.Differences.Count > 0 ? changed : partial).Add(difference);'
        To = 'changed.Add(difference);'
        Test = 'DiffPropertyTests.What_is_filed_as_changed_has_something_changed_and_what_is_not_has_not'
    },
    @{
        # The first entry ever to point at Bws.Cli, because until 2026-08-04 that assembly had no
        # test project - everything testing it ran the built executable as a separate process.
        #
        # The mutation is the code as it stood before 2026-08-03: a column measured in units of
        # storage. It reads as harmless because the padding was measured the same way, so the two
        # were wrong together and agreed with each other while disagreeing with the screen.
        Name = 'listing: a column is as wide as a terminal sees it'
        File = 'src\Bws.Cli\ListingTable.cs'
        From = @'
        var elements = System.Globalization.StringInfo.GetTextElementEnumerator(cell);
        var width = 0;

        while (elements.MoveNext())
        {
            width++;
        }

        return width;
'@
        To = @'
        return cell.Length;
'@
        Test = 'ListingTableTests.A_name_holding_a_surrogate_pair_does_not_shift_the_column_after_it'
    },
    @{
        # The live side has to read what the file holds. Without it every entry lands under
        # "one side never read this" - and an uncompared field is not a difference, so the
        # comparison still reports nothing changed. The guard had to be strengthened before
        # this entry could catch anything, which is the point of writing the entry.
        Name = 'diff: the live side reads what the file holds'
        File = 'src\Bws.Cli\Program.cs'
        # Anchor re-cut 2026-08-02: the constructor gained an argument and this entry went
        # STALE, which is the tool reporting that the sentence above had stopped being proved.
        # RE-ANCHORED 2026-10-14: the live side reads through SnapshotPasses, shared with snapshot
        # create and the window. The mutant still reads nothing on top of the listing.
        From = '            entries = SnapshotPasses.Fill(entries, catalog!, new WindowsBinaryInspector(networkPaths), out inspected, out asked);'
        To = '            inspected = asked = 0;'
        Test = 'DiffContractTests.A_snapshot_taken_now_has_no_configuration_differences_against_the_machine_it_came_from'
    },
    @{
        # Anchor followed ReadDelayedAuto into ScmDetailReader.cs on 2026-08-02, when the size
        # ratchet forced that split. The entry went STALE and said so, which is the tool doing
        # its job - a moved method is exactly the case that leaves an entry proving nothing.
        # Came back BROKEN on 2026-08-03: the mutation named a `startType` parameter that had
        # been removed, because the body never read it. Restoring the old behaviour no longer
        # needs it - asking only about automatic entries is what the start type was there for,
        # and the entry type says the same thing about drivers.
        Name = 'delay flag: read wherever Windows stores one'
        File = 'src\Bws.Core\ScmDetailReader.cs'
        From = 'var applies = !enumerated.IsDriver;'
        To = 'var applies = !enumerated.IsDriver && enumerated.Status == Core.EntryStatus.Running;'
        Test = 'QueryContractTests.The_delayed_flag_is_read_wherever_Windows_stores_one'
    },
    @{
        # Re-anchored 2026-08-05. The rule moved out of ListingTable and into the core when the
        # window started showing the same column - two copies of one rule was the alternative.
        Name = 'delay flag: the table marks it only where it means something'
        File = 'src\Bws.Core\StartQualifiers.cs'
        From = 'Delayed = delayMeansSomething'
        To = 'Delayed = true'
        Test = 'QueryContractTests.The_table_marks_a_delay_only_where_it_changes_what_the_start_type_means'
    },
    @{
        # The same rule from the other end. The command line and the window take their words
        # from two language files and their DECISION from one place, and this is what says the
        # window is really reading that place rather than having grown its own copy.
        Name = 'delay flag: the window marks it only where it means something too'
        File = 'src\Bws.Core\StartQualifiers.cs'
        From = 'Delayed = delayMeansSomething'
        To = 'Delayed = true'
        Test = 'CellFaceTests.A_delay_flag_is_only_said_where_it_does_anything'
    },
    @{
        # The fix for the fault gui-probe\check.ps1 found on 2026-08-05, and the mutation is the
        # code exactly as it stood: holding was applied to the FIRST fill as well, so a window
        # that opened under the pointer said "810 entries" over an empty grid until the pointer
        # moved away. Nothing in the view model tests could see it - they all loaded first and
        # interacted afterwards, which is the order a person uses and not the order a script
        # starting a window uses.
        # Re-anchored 2026-08-05: the whole rule moved into Holding.cs when the size ratchet
        # asked MainViewModel for a seam.
        Name = 'holding: an empty list is not something anybody is using yet'
        File = 'src\Bws.Gui\ViewModels\Holding.cs'
        From = 'if (Interacting && shown.Count > 0)'
        To = 'if (Interacting)'
        Test = 'HoldingTests.A_list_that_is_still_empty_fills_even_while_somebody_is_pointing_at_it'
    },
    @{
        # The WORDING half, which is the part that stayed in Bws.Cli when the rule moved to the
        # core. It had no test at all until the coverage gate went red and pointed at it - and
        # what the gate found was not new code without a test, but old code that stopped being
        # tested because of a change somewhere else.
        Name = 'start cell: a missing file is said out loud beside the start type'
        File = 'src\Bws.Cli\ListingTable.cs'
        From = 'return qualifies.FileMissing ? Texts.Of("cli.cell.startFileMissing", cell) : cell;'
        To = 'return cell;'
        Test = 'ListingTableTests.The_start_cell_says_delayed_then_trigger_then_a_missing_file'
    },
    @{
        Name = 'start type: a trigger that stops an entry is not one that starts it'
        File = 'src\Bws.Core\StartQualifiers.cs'
        From = 'trigger.Action == TriggerAction.Start'
        To = 'trigger.Action != TriggerAction.Unknown'
        Test = 'CellFaceTests.A_trigger_that_only_stops_the_entry_is_not_reported_as_one_that_starts_it'
    },
    @{
        # The half a screenshot cannot check. A shape code that equals a translated word is a
        # theme trigger matching English, which stops matching the day a second language file
        # arrives - and stops silently, leaving the row uncoloured with nothing said.
        Name = 'row shapes: the code a theme matches on is never the word a person reads'
        # Re-anchored 2026-08-26: CellShapes moved out of CellFaces.cs into its own file when the
        # size ratchet fired on the per-user role column. The claim is unchanged, only the anchor.
        File = 'src\Bws.Gui\ViewModels\CellShapes.cs'
        From = 'private const string Prefix = "shape.";'
        To = 'private const string Prefix = "";'
        Test = 'CellFaceTests.The_shape_is_never_the_word'
    },
    @{
        # A guard on a floor is worth exactly what the floor is. This one was 11 for four
        # slices, under the 12 the guidance asks for, carrying three of the four sentences the
        # window ever says.
        Name = 'type scale: nothing in the theme is smaller than the floor'
        File = 'src\Bws.Gui\Themes\Type.xaml'
        From = '<system:Double x:Key="TextSizeSmall">12</system:Double>'
        To = '<system:Double x:Key="TextSizeSmall">11</system:Double>'
        Test = 'TypeScaleGuards.No_declared_text_size_is_below_the_floor_for_plain_text'
    },
    # TWO ENTRIES FOR ONE RULE SINCE 2026-08-11, AND THE SPLIT OF THE THEME IS WHY. There are two
    # semibold styles in this product and the same setter line carries both, so while they lived in
    # one file the runner - which replaces every occurrence in the file it is given - broke both at
    # once and one entry proved both. Backlog 156 rejected a three way split of the theme on exactly
    # that reasoning. It happened anyway on 163, and the two ended up on opposite sides of the seam:
    # CountLine stayed in Controls.xaml and ColumnHeading went to List.xaml. One entry would now
    # prove one of them and say nothing about the other, which is the quiet half of a guard going
    # missing.
    @{
        # Semibold answers to a higher floor than plain text, which is the half that is easy to
        # get wrong: 13 is fine and 13 semibold is not. This one is the count line.
        Name = 'type scale: semibold answers to the higher floor, in the count line'
        File = 'src\Bws.Gui\Themes\Text.xaml'
        From = '<Setter Property="FontSize" Value="{StaticResource TextSizeHeader}" />'
        To = '<Setter Property="FontSize" Value="{StaticResource TextSizeNormal}" />'
        Test = 'TypeScaleGuards.Nothing_set_semibold_is_below_the_heavier_floor'
    },
    @{
        # And the column heading, which is the one the guard was written for.
        Name = 'type scale: semibold answers to the higher floor, in the column heading'
        File = 'src\Bws.Gui\Themes\List.xaml'
        From = '<Setter Property="FontSize" Value="{StaticResource TextSizeHeader}" />'
        To = '<Setter Property="FontSize" Value="{StaticResource TextSizeNormal}" />'
        Test = 'TypeScaleGuards.Nothing_set_semibold_is_below_the_heavier_floor'
    },
    @{
        # The separator between two rows is the lowest floor in the contrast table and still a
        # floor. #2A2A2A is the value hover was condemned at - 1.14, below being seen at all.
        Name = 'row line: the separator is still above the point where it can be seen'
        File = 'src\Bws.Gui\Themes\Surfaces.xaml'
        From = '<SolidColorBrush x:Key="SurfaceRowLine">#333333</SolidColorBrush>'
        To = '<SolidColorBrush x:Key="SurfaceRowLine">#2A2A2A</SolidColorBrush>'
        Test = 'ContrastGuards.Every_declared_colour_clears_the_ratio_its_role_needs'
    },
    @{
        Name = 'read states: the refusal number is not flattened away'
        File = 'src\Bws.Core\Snapshots\EntryDocument.cs'
        From = 'new UnreadableDocument(reading.ErrorCode'
        To = 'new UnreadableDocument(5'
        Test = 'SnapshotTests.A_service_that_vanished_is_told_apart_from_one_that_was_refused'
    },
    @{
        # MOVED FROM EntryRow.cs ON 2026-08-11 BECAUSE THE CODE DID. Turning a reading into a cell
        # was private to the row until eleven more columns needed it, so it went to CellFaces -
        # where the same line now appears twice, in Say and in StartLabel. The runner replaces
        # every occurrence in a file, so this one entry breaks both and both tests redden, which is
        # more than it proved before rather than less.
        #
        # RE-ANCHORED 2026-10-05: both arms now call one helper choosing between "no access" and
        # "could not be read" (stability R-7), so the helper is what collapses - both words to
        # nothing, with the parameter still read so the mutant compiles.
        Name = 'window: the four read states do not collapse in a cell'
        File = 'src\Bws.Gui\ViewModels\CellFaces.cs'
        From = 'errorCode == AccessDenied.Code ? Texts.Of("gui.cell.noAccess") : Texts.Of("gui.cell.unreadable");'
        To = 'errorCode == AccessDenied.Code ? string.Empty : string.Empty;'
        Test = 'MainViewModelTests.The_four_read_states_do_not_look_alike_in_a_cell'
    },

    # THE SEVEN BELOW ARE S6d2, THE COLUMNS SOMEBODY CHOOSES. Two of them break a NOTIFICATION
    # rather than a value, and that distinction is the whole lesson of the slice before this one:
    # a cell is worked out at the moment it is asked, so it answers correctly whether or not
    # anybody was ever told to ask - and a test that asks it directly stays green through a
    # binding that has stopped being told anything.
    @{
        Name = 'columns: a cheap reading tells the cells they changed'
        File = 'src\Bws.Gui\ViewModels\EntryRow.cs'
        From = @'
        Raise(EveryCell);

        ChangedAt = now;
'@
        To = @'
        ChangedAt = now;
'@
        Test = 'ColumnGuards.A_row_that_takes_a_cheap_reading_says_its_cells_changed'
    },
    @{
        # And the full reading, where the notice is UNCONDITIONAL - eleven columns read parts of an
        # entry that the movement test does not compare, so a conditional notice leaves those cells
        # showing yesterday's answer with every property on the row telling the truth.
        Name = 'columns: a full reading tells the cells they changed even when nothing moved'
        File = 'src\Bws.Gui\ViewModels\EntryRow.cs'
        From = @'
        Raise(EveryCell);

        if (moved)
'@
        To = @'
        if (moved)
'@
        Test = 'ColumnGuards.A_row_says_its_cells_changed_even_when_nothing_it_calls_movement_did'
    },
    @{
        # A DRIVER HAS NO PROCESS IDENTIFIER - 0 of 472 on this machine, measured 2026-08-19. Taking
        # the flag away is the state this window shipped in until the scope switch: one layout for
        # both lists, so the longer one spends a column on a value that is never there.
        Name = 'scope: a driver list starts without the columns a driver cannot fill'
        File = 'src\Bws.Gui\ViewModels\Columns.cs'
        #
        # MOVED FROM THE PROCESS ID TO THE ACCOUNT ON 2026-09-03, AFTER COMING BACK MISSED IN THE
        # FIRST FULL GATE SINCE THE CHANGE THAT BROKE IT. The process id went off at the start for
        # EVERY scope on 2026-09-02, on the owner's decision to open on the five columns
        # services.msc opens on - so its scope rule stopped being observable from outside: the
        # column is off for drivers whether the rule is there or not. The test says exactly that in
        # its own comment, written the same day, and nobody moved this entry with it.
        #
        # The account is the one that still proves OffAtFirstIn does anything, and it is the one
        # the test asserts BOTH WAYS: off for drivers, on for services. The process id rule stays
        # in the code for the reason written beside it - so that turning the column back on for
        # services cannot quietly turn it on for drivers too - and nothing here claims to prove it.
        #
        # RE-ANCHORED 2026-09-15 when the account cell stopped reading the spelling straight off
        # the entry - the comment between the two lines is what the anchor now spans.
        From = @'
            OffAtFirstIn = EntryScope.Drivers,

            // THE NAME A PERSON KNOWS
'@
        To = @'
            // THE NAME A PERSON KNOWS
'@
        Test = 'ColumnLayoutGuards.A_driver_list_starts_without_the_columns_a_driver_cannot_fill'
    },
    @{
        # OUR OWN PAST IS NOT ANOTHER BUILD. The mutation restores the rule exactly as it read until
        # 2026-08-19 - any version but the current one is left alone - which on the day the schema
        # went to 2 would have thrown away the column widths of everybody who had ever dragged one.
        Name = 'layout: a file from the previous schema is carried forward rather than left behind'
        File = 'src\Bws.Gui\ViewModels\ColumnLayout.cs'
        From = 'if (version > ColumnLayout.CurrentSchemaVersion || version < ColumnLayout.OldestSchemaVersionRead)'
        To = 'if (version != ColumnLayout.CurrentSchemaVersion)'
        Test = 'ColumnLayoutGuards.A_layout_from_the_previous_schema_is_carried_forward_rather_than_left_behind'
    },
    @{
        # Sections told apart by name rather than by luck. Handing back the wrong one puts somebody's
        # driver columns onto their services list, which is a fault that looks like they did it.
        Name = 'layout: each scope keeps its own section of the file'
        File = 'src\Bws.Gui\ViewModels\ColumnLayout.cs'
        From = '        EntryScope.Drivers => Drivers,'
        To = '        EntryScope.Drivers => Services,'
        Test = 'ColumnLayoutGuards.Each_scope_keeps_its_own_layout_through_the_trip_to_text_and_back'
    },
    @{
        # A list with no columns at all is 809 rows of nothing above a count line still saying 809.
        Name = 'columns: the last one showing cannot be turned off'
        File = 'src\Bws.Gui\ViewModels\ColumnBar.cs'
        # Re-anchored 2026-09-24: the count is taken on the LIST since the details panel can take
        # the description off it (UX-GUI-012), so the anchor reads IsOnList.
        From = 'choice.MayHide = !(alone && choice.IsOnList);'
        To = 'choice.MayHide = true;'
        Test = 'ColumnGuards.The_last_column_left_showing_may_not_be_turned_off'
    },
    @{
        # THE DEFECT EXACTLY AS IT SHIPPED between 2026-08-10 and 2026-08-11: the column binds to
        # text, so sorted by what the grid saw, 103292 came before 9.
        Name = 'columns: the process id is ordered as a number'
        File = 'src\Bws.Gui\ViewModels\Columns.cs'
        From = 'Sorts = entry => entry.ProcessId.IsPresent ? entry.ProcessId.Value : null'
        To = 'Sorts = null'
        # Re-anchored 2026-08-26: the ordering guards moved to their own class when the size
        # ratchet fired on ColumnGuards.cs. Same claim, new home.
        Test = 'ColumnOrderGuards.The_process_id_is_ordered_as_a_number_rather_than_as_text'
    },
    @{
        # These identifiers become a file people keep at S6d3, so two columns answering to one name
        # is a layout that cannot say which it means.
        # A comparer that gives the same answer whichever way round it was asked sorts perfectly
        # and reverses nothing - and clicking a heading twice is how everybody checks a sort.
        #
        # THE OBVIOUS MUTATION HERE - dropping the condition for `return order;` - CAME BACK BROKEN
        # RATHER THAN CAUGHT, and the reason is worth keeping. It leaves the primary constructor
        # parameter unread, which is CS9113, which this build turns into an error. An entry that
        # cannot compile proves nothing about the guard it names, so the direction is INVERTED
        # instead: same shape of fault, still reads the parameter.
        Name = 'columns: asking for the other direction gives the other direction'
        File = 'src\Bws.Gui\ViewModels\CellOrder.cs'
        From = 'return ascending ? order : -order;'
        To = 'return ascending ? -order : order;'
        Test = 'ColumnOrderGuards.A_column_orders_two_rows_by_what_it_says_and_reverses_when_asked'
    },
    @{
        # A button whose menu carries nothing looks exactly like a feature that is not there. The
        # menu ACTUALLY OPENING is guarded by tools/window-journey on a real window rather than
        # here, because the window these tests build is never shown - the note on the test says so.
        Name = 'columns: the button carries the list of them'
        File = 'src\Bws.Gui\MainWindow.Columns.cs'
        # The anchor moved twice on 2026-08-12: to a grouped view, and back to a flat list when
        # grouping turned out to take the menu out of the automation tree.
        From = 'menu.ItemsSource = _columns.Entries;'
        To = 'menu.ItemsSource = null;'
        Test = 'WindowGuards.The_columns_button_carries_every_column'
    },
    @{
        # A UserControl IS a ContentControl, and a ContentControl is focusable and a tab stop by
        # default while the Grid this replaced was neither - measured, not assumed. Dropping this
        # breaks no binding, throws nothing and paints nothing differently: it puts one silent step
        # into the keyboard order, in the window docs/11 9.1 says an administrator works from.
        #
        # THE ANCHOR MOVED ON 2026-08-13 AND THE ENTRY BESIDE IT WENT AWAY, which is worth knowing.
        # While this row was a style carrying a template, its button had to be found by name and a
        # missing part refused with an exception - so there was a second entry proving that refusal.
        # Backlog 185 made the row a UserControl, the button became a field, and a behaviour that
        # only existed to work around a theme file stopped existing. An entry for it would now be
        # proving a mechanism nobody has.
        Name = 'window: the row holding the filters takes no focus of its own'
        File = 'src\Bws.Gui\FilterRow.xaml'
        From = '             Focusable="False"'
        To = '             Focusable="True"'
        Test = 'FilterRowGuards.The_filters_row_is_not_a_stop_on_the_way_to_the_filters'
    },
    @{
        # A row keeps its identity for the life of the window and its cells move underneath it, so a
        # panel built once at the moment somebody pressed Enter would go on showing that moment -
        # correctly, forever, with every property on it telling the truth. That is the shape this
        # window has been caught by four times.
        Name = 'details: the panel keeps up with the row it was opened on'
        File = 'src\Bws.Gui\ViewModels\Chosen.cs'
        # Re-anchored 2026-09-24: Moved also starts the panel's own reading now (UX-GUI-005), so the
        # body is a block and the mutation takes out the rebuild alone. Re-anchored 2026-09-28: a
        # filter on the property name stands before the rebuild now (G-8).
        From = '        Rebuild();' + [char]10 + [char]10 + '        // THE ROW MOVED, AND A FULL READING'
        To = '        // THE ROW MOVED, AND A FULL READING'
        Test = 'ChosenGuards.The_panel_moves_when_the_row_moves'
    },
    @{
        # The teardown case. Without it a closed panel goes on rebuilding five sections once a
        # second for as long as the window is open, and keeps itself alive to do it - which nothing
        # on screen would ever show.
        Name = 'details: a closed panel stops following the row'
        File = 'src\Bws.Gui\ViewModels\Chosen.cs'
        From = @'
            _followed.PropertyChanged -= Moved;
            _followed = null;
'@
        To = @'
            _ = _followed;
'@
        Test = 'ChosenGuards.A_closed_panel_stops_following_the_row'
    },
    @{
        # Rule 8 where a details panel breaks it most easily: a panel carrying on with the last
        # reading answers questions about a service that is not there.
        Name = 'details: an entry that left the listing is said out loud'
        File = 'src\Bws.Gui\ViewModels\Chosen.cs'
        From = 'Gone = !everything.Contains(row);'
        To = 'Gone = false;'
        Test = 'ChosenGuards.An_entry_that_left_the_listing_is_said_and_a_narrowed_one_is_not'
    },
    @{
        # The order, not the two actions. Both still happen either way round - what changes is that
        # one press takes somebody's query away while they were reaching for the panel.
        Name = 'window: Escape closes the panel before it touches the query'
        File = 'src\Bws.Gui\MainWindow.Keyboard.cs'
        #
        # RE-ANCHORED 2026-08-18: the plan panel joined the front of this queue in packet 2, so the
        # line grew a third call. What is asserted is unchanged - a panel goes before the query.
        # RE-ANCHORED 2026-09-16: the list under the search box joined the FRONT of this queue.
        # What is asserted is unchanged - a panel goes before the query.
        # RE-ANCHORED 2026-09-29: the chain went onto five lines and a run that is going now takes the
        # press first (G-1). What is asserted is unchanged.
        # RE-ANCHORED 2026-10-14: a comparison with a snapshot joined the queue beside the panel, and
        # the two went behind one question to the model, PutAwayAView. What is asserted is unchanged.
        From = '                return _model.Suggesting.Close()
                    || _model.Planned.Busy
                    || _model.Planned.Hide()
                    || _model.PutAwayAView()
                    || _model.ClearQuery();'
        To = '                return _model.Suggesting.Close()
                    || _model.ClearQuery()
                    || _model.Planned.Busy
                    || _model.Planned.Hide()
                    || _model.PutAwayAView();'
        Test = 'KeyboardTests.Escape_closes_the_panel_before_it_touches_the_query'
    },
    @{
        # A view model that changed and a trigger that never fired look identical from every other
        # test in this assembly - which is the fault this window has been caught by four times.
        # Collapsed rather than hidden, or the column keeps its 380 points away from the rows.
        Name = 'details: the panel is off the window until somebody asks'
        File = 'src\Bws.Gui\DetailsView.xaml'
        From = '<DataTrigger Binding="{Binding Showing}" Value="True">'
        To = '<DataTrigger Binding="{Binding Showing}" Value="False">'
        Test = 'DetailsViewGuards.The_panel_is_off_the_window_until_somebody_asks_for_it'
    },
    @{
        # `docs/10` trap 8, in the second place it applies. A UserControl is a ContentControl and is
        # focusable and a tab stop by default, so this setter is what keeps Tab from stopping on a
        # container in front of the only button the panel holds.
        Name = 'details: the panel takes no focus of its own'
        File = 'src\Bws.Gui\DetailsView.xaml'
        From = '             Focusable="False"'
        To = '             Focusable="True"'
        Test = 'DetailsViewGuards.The_panel_is_not_a_stop_on_the_way_to_the_button_inside_it'
    },
    # ---- UX-GUI-005 and 012, 2026-09-24: the panel reads for itself and sits beside the list ----
    @{
        # The whole of 005. Without it the panel goes back to "not read" beside the signature and
        # sends the reader to a column that reads the machine.
        Name = 'details: opening the panel reads what its entry lacks'
        File = 'src\Bws.Gui\ViewModels\Chosen.cs'
        From = '        Opened?.Invoke(this, EventArgs.Empty);' + [char]10 + [char]10 + '        Keep();'
        To = '        Opened?.Invoke(this, EventArgs.Empty);'
        Test = 'PanelReadingTests.Opening_the_panel_reads_what_its_entry_is_missing'
    },
    @{
        # An answer read against entries a full reading has since replaced, written into the fresh
        # ones - a signature of a file that may have been swapped, shown as current.
        Name = 'details: an answer for a replaced listing is dropped'
        File = 'src\Bws.Gui\ViewModels\Readings.OneEntry.cs'
        From = 'if (_gone || listing != _listing)'
        To = 'if (_gone || listing < 0)'
        Test = 'PanelReadingTests.An_answer_for_a_listing_that_has_been_replaced_is_thrown_away'
    },
    @{
        # The other half of the same rule: a new listing must let the panel ask again, or the
        # dropped answer leaves the panel on "not read" until the next opening.
        Name = 'details: a new listing lets the panel ask again'
        File = 'src\Bws.Gui\ViewModels\Readings.OneEntry.cs'
        From = '        _listing++;' + [char]10 + '        _claimed.Clear();'
        To = '        _listing++;'
        Test = 'PanelReadingTests.An_answer_for_a_listing_that_has_been_replaced_is_thrown_away'
    },
    @{
        # A family that comes back unread - a file on another machine - asked for again on every
        # refresh of the row, forever. The test waits with a ceiling, so this reddens rather than
        # hangs - note 2 at the head of this file.
        Name = 'details: a family that comes back unread is asked for once'
        File = 'src\Bws.Gui\ViewModels\Readings.OneEntry.cs'
        From = 'var owed = missing & Available & ~claimed;'
        To = 'var owed = missing & Available;'
        Test = 'PanelReadingTests.A_field_the_panel_cannot_read_is_asked_for_once'
    },
    @{
        # One filled row counted as the list having read: a query about signatures would then judge
        # eight hundred rows on a field read for one, and say nothing about it.
        Name = 'details: a panel reading is not the list having read'
        File = 'src\Bws.Gui\ViewModels\Readings.OneEntry.cs'
        From = '        _index.AbsorbOne(Carry(now, read, families, sameProcess));'
        To = '        _have |= families;' + [char]10 + '        _index.AbsorbOne(Carry(now, read, families, sameProcess));'
        Test = 'PanelReadingTests.A_panel_reading_does_not_count_as_the_list_having_read'
    },
    @{
        # A stranger's memory in the right field: the service restarted between question and answer.
        Name = 'details: the memory is kept only for the process that was asked'
        File = 'src\Bws.Gui\ViewModels\Readings.OneEntry.cs'
        From = 'if (families.HasFlag(ExtraRead.Memory) && sameProcess)'
        To = 'if (families.HasFlag(ExtraRead.Memory) && (sameProcess || true))'
        Test = 'PanelReadingTests.A_memory_read_for_a_process_that_has_since_restarted_is_read_again_for_the_new_one'
    },
    @{
        # Review of PR #16: a restart arrives through the tick, which never clears a claim, so the
        # dropped memory stayed "not read" until the next full reading and Enter did nothing.
        Name = 'details: a restart lets the panel ask for the memory again'
        File = 'src\Bws.Gui\ViewModels\Readings.OneEntry.cs'
        From = '            _claimed[row.ServiceName] = had & ~ExtraRead.Memory;'
        To = '            _claimed[row.ServiceName] = had;'
        Test = 'PanelReadingTests.A_memory_read_for_a_process_that_has_since_restarted_is_read_again_for_the_new_one'
    },
    @{
        # Review of PR #16, case one: the failure followed the panel to the next entry.
        Name = 'details: a failure is said for the entry it happened to'
        File = 'src\Bws.Gui\ViewModels\Readings.OneEntry.cs'
        From = 'return _failures.GetValueOrDefault(row.ServiceName, string.Empty);'
        To = 'return _failures.Values.FirstOrDefault() ?? string.Empty;'
        Test = 'PanelReadingTests.A_failure_is_said_for_the_entry_that_failed_and_not_for_the_next'
    },
    @{
        # Review of PR #16, case two: opened again, the failed entry said nothing - its claim stood,
        # nothing was read, and the lines implied nobody had tried.
        Name = 'details: a failure stays said while its claim stands'
        File = 'src\Bws.Gui\ViewModels\Readings.OneEntry.cs'
        From = '        var claimed = _claimed.GetValueOrDefault(row.ServiceName);'
        To = '        _failures.Remove(row.ServiceName);' + [char]10 + '        var claimed = _claimed.GetValueOrDefault(row.ServiceName);'
        Test = 'PanelReadingTests.A_failure_stays_said_when_the_panel_opens_on_that_entry_again'
    },
    @{
        Name = 'details: a line being read says so'
        File = 'src\Bws.Gui\ViewModels\Details.cs'
        From = 'outcome == ReadOutcome.NotRead && (column.Needs & reading) != ExtraRead.None ? Texts.Of(ReadingNow) : null);'
        To = 'outcome == ReadOutcome.NotRead && (column.Needs & reading) != ExtraRead.None && false ? Texts.Of(ReadingNow) : null);'
        Test = 'PanelReadingTests.While_it_reads_its_lines_say_so'
    },
    @{
        # The failure swallowed: lines saying "not read" as though nobody had tried. Rule 8.
        Name = 'details: a failed panel reading is said'
        File = 'src\Bws.Gui\ViewModels\Readings.OneEntry.cs'
        From = '                _failures[row.ServiceName] = string.Join(" ", Causes.Of(failure));'
        To = '                _ = failure;'
        Test = 'PanelReadingTests.A_reading_that_fails_is_said_in_the_panel'
    },
    @{
        # The old note back under any unread line - the sentence that stopped being true.
        Name = 'details: the network note is said only over a file on another machine'
        File = 'src\Bws.Gui\ViewModels\Details.cs'
        From = 'var note = OnAnotherMachine(entry)'
        To = 'var note = OnAnotherMachine(entry) || true'
        Test = 'DetailsGuards.The_fields_this_panel_once_apologised_for_carry_values_now'
    },
    @{
        Name = 'details: what it runs comes second'
        File = 'src\Bws.Gui\ViewModels\Details.cs'
        From = 'private static readonly string[] Order = [Columns.Basics, Columns.Binary, Columns.About, Columns.Advanced];'
        To = 'private static readonly string[] Order = [Columns.Basics, Columns.About, Columns.Binary, Columns.Advanced];'
        Test = 'PanelBesideTheListGuards.What_it_runs_comes_before_what_kind_of_entry_it_is'
    },
    @{
        # Found by reading on 2026-09-24: the new entry wore the old one's "no longer in the listing".
        Name = 'details: a new opening starts without the last notice'
        File = 'src\Bws.Gui\ViewModels\Chosen.cs'
        From = '        Gone = false;' + [char]10 + [char]10 + '        // The log is read the first time any panel opens'
        To = '        // The log is read the first time any panel opens'
        Test = 'PanelBesideTheListGuards.Opening_the_panel_on_another_entry_does_not_carry_the_last_ones_notice'
    },
    @{
        # Found by reading on 2026-09-24: the panel was drawn over the machine overview.
        Name = 'details: the overview puts the panel away'
        File = 'src\Bws.Gui\MainWindow.Overview.cs'
        From = '            _model.Chosen.Hide();'
        To = ''
        Test = 'PanelBesideTheListGuards.The_overview_puts_the_panel_away'
    },
    @{
        Name = 'columns: the open panel takes the description off the list'
        File = 'src\Bws.Gui\ViewModels\ColumnBar.cs'
        From = 'choice.Yielding = _panelOpen && others && choice.Column.YieldsToPanel;'
        To = 'choice.Yielding = others && false;'
        Test = 'PanelBesideTheListGuards.An_open_panel_takes_the_description_off_the_list_and_leaves_it_chosen'
    },
    @{
        # A list whose only chosen column is the description goes blank when the panel opens.
        Name = 'columns: the panel never takes the last column off the list'
        File = 'src\Bws.Gui\ViewModels\ColumnBar.cs'
        From = 'choice.Yielding = _panelOpen && others && choice.Column.YieldsToPanel;'
        To = 'choice.Yielding = _panelOpen && (others || true) && choice.Column.YieldsToPanel;'
        Test = 'PanelBesideTheListGuards.The_panel_never_takes_the_last_column_off_the_list'
    },
    @{
        # Counted in the choices, a list of the name and the description lets the name go too
        # while the panel has the description.
        Name = 'columns: with the panel open the last column on the list stays'
        File = 'src\Bws.Gui\ViewModels\ColumnBar.cs'
        From = 'var alone = Choices.Count(choice => choice.IsOnList) <= 1;'
        To = 'var alone = Choices.Count(choice => choice.IsShown) <= 1;'
        Test = 'PanelBesideTheListGuards.With_the_panel_open_the_one_column_left_on_the_list_may_not_be_hidden'
    },
    @{
        # THE TRAP OF VARIANT C, silent: every close with the panel open writes the description
        # into the profile as turned off, and the next start opens without it.
        Name = 'layout: a column away for the panel is kept as chosen'
        File = 'src\Bws.Gui\ListColumns.cs'
        From = 'ColumnShowing.IsChosen(column),'
        To = 'column.Visibility == Visibility.Visible,'
        Test = 'PanelBesideTheListGuards.A_column_away_for_the_panel_is_kept_as_chosen_in_the_saved_layout'
    },
    @{
        # Measured on the laid out panel, because a width in markup that the layout ignores looks
        # exactly like one that works - GUI rule 10.
        Name = 'layout: an open panel takes a third of the window'
        File = 'src\Bws.Gui\MainWindow.xaml'
        From = '<Setter Property="Width" Value="{StaticResource ShareDetailsPanel}" />'
        To = '<Setter Property="Width" Value="Auto" />'
        Test = 'PanelBesideTheListGuards.The_panel_takes_a_third_of_the_window_within_its_limits'
    },
    @{
        # THE SEAM OF 2026-08-18 IS WHAT THIS ENTRY IS ABOUT, not the sentence. While the empty state
        # lived in MainWindow.xaml it read Says.ListMessage through the window's own DataContext. It
        # now reads ListMessage through a DataContext of its own, and pointing that at the wrong half
        # of the model is the way the move fails: every binding in the panel resolves to nothing, the
        # trigger never fires, and the window paints an EMPTY RECTANGLE in the middle of itself.
        #
        # That rectangle is complaint 9 of the eleven, which is the complaint this panel exists to
        # answer - so the failure mode of the seam is the regression of the feature, with a green
        # build and nothing on a screenshot to say which of the two states it is in.
        Name = 'empty state: the sentence on the window is the one the window holds'
        File = 'src\Bws.Gui\MainWindow.xaml'
        From = 'DataContext="{Binding Says}"'
        To = 'DataContext="{Binding Chosen}"'
        Test = 'EmptyStateViewGuards.What_it_says_on_the_window_is_what_the_window_holds'
    },
    @{
        # The other direction, and the one that decides whether the list is usable at all. This panel
        # sits OVER the middle of the grid rather than instead of it, so a trigger that never fires
        # is not a cosmetic fault - it is a sentence held over eight hundred rows for as long as the
        # window is open.
        #
        # The value is changed rather than the binding, so the entry lands on exactly one test: while
        # nothing has been read the message is not empty and the trigger would not fire either way,
        # which is the state the guard above asks about.
        Name = 'empty state: it comes off the window as soon as there are rows'
        File = 'src\Bws.Gui\EmptyState.xaml'
        From = '<DataTrigger Binding="{Binding ListMessage}" Value="">'
        To = '<DataTrigger Binding="{Binding ListMessage}" Value="never">'
        Test = 'EmptyStateViewGuards.It_is_off_the_window_as_soon_as_there_are_rows'
    },
    @{
        # THE MOST IMPORTANT LINE IN THE PANEL, and the one whose absence is invisible. Everything the
        # panel shows is written in the conditional, and a numbered list of steps still reads as a
        # report of something that happened. A preview without this sentence is a false record of a
        # change to somebody's machine - drawn correctly, with every other binding working.
        #
        # RE-ANCHORED 2026-08-19 when the window learned to carry a plan out. The sentence stopped
        # being one line and became three, because the panel now has three states to be in rather
        # than one - and the entry beside it covers the state this one cannot: that a run in progress
        # does not read as a preview.
        Name = 'plan: the panel says that nothing has been done'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        # RE-ANCHORED 2026-09-07: the sentence gained a leading reason for a plan somebody was
        # offered, so the arm it stood on is now two arms. The mutation moves to the branch that
        # still answers for every plan opened from a menu, which is what this guard drives.
        From = '                ? Texts.Of("gui.plan.notice.notYet")'
        To = '                ? string.Empty'
        Test = 'PlanViewGuards.What_it_shows_is_the_plan_for_the_picked_rows'
    },
    @{
        # THE TWO PANELS SHARE ONE CELL OF THE GRID. Without this the details panel stays open behind
        # the plan, both visible in the same place, and the narrower one draws over the other - which
        # reads as a rendering fault rather than as two open panels. Arranged by the window because
        # neither panel has any business knowing the other exists.
        Name = 'plan: opening the plan puts the details panel away'
        File = 'src\Bws.Gui\MainWindow.Menu.cs'
        From = '        _model.Chosen.Hide();'
        To = '        _ = _model.Chosen.Showing;'
        Test = 'PlanViewGuards.Opening_one_panel_puts_the_other_away'
    },
    @{
        # THE ORDER IS THE DECISION RATHER THAN THE IMPLEMENTATION, the same one the details panel
        # joined this queue with: one press doing two things at once takes somebody's query away while
        # they were reaching for a panel, and a query is the more expensive of the two to type again.
        Name = 'plan: Escape backs out of the plan before it touches the query'
        File = 'src\Bws.Gui\MainWindow.Keyboard.cs'
        # RE-ANCHORED 2026-09-16: the list under the search box joined the FRONT of this queue.
        # What is asserted is unchanged - a panel goes before the query.
        # RE-ANCHORED 2026-09-29: the chain went onto five lines and a run that is going now takes the
        # press first (G-1). What is asserted is unchanged.
        # RE-ANCHORED 2026-10-14: a comparison with a snapshot joined the queue beside the panel, and
        # the two went behind one question to the model, PutAwayAView. What is asserted is unchanged.
        From = '                return _model.Suggesting.Close()
                    || _model.Planned.Busy
                    || _model.Planned.Hide()
                    || _model.PutAwayAView()
                    || _model.ClearQuery();'
        To = '                return _model.Suggesting.Close()
                    || _model.ClearQuery()
                    || _model.Planned.Busy
                    || _model.Planned.Hide()
                    || _model.PutAwayAView();'
        Test = 'PlanViewGuards.Escape_closes_the_plan_before_it_clears_the_query'
    },
    @{
        # THE SECOND LIST THE PREVIEW CARRIES, and the price of the owner's decision of 2026-08-18: a
        # refusal belongs to its own entry and the rest of the selection carries on. Emptied, a
        # selection of two with one refused looks exactly like a selection of one - which is the silent
        # kind of wrong this project spends most of its rules on.
        Name = 'plan: an entry the plan refuses reaches the screen'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        #
        # Emptied by taking none of them rather than by returning a bare list: both branches of the
        # ternary would then be the same expression, which does not compile here - warnings are
        # errors, and a mutation that does not build proves nothing while looking like it tried.
        # RE-ANCHORED 2026-09-30: the refusals are lines that can carry an offer (W-4).
        From = ': PlanSentence.Of(plan.Problems, offering: _run is null && !Busy);'
        To = ': PlanSentence.Of([.. plan.Problems.Take(0)], offering: _run is null && !Busy);'
        Test = 'PlanViewGuards.An_entry_with_no_plan_is_named_on_screen'
    },
    @{
        # `E5`, the half of the preview a person takes away with them. The panel is the only place in
        # the window that says these commands exist, and an empty list looks exactly like a feature
        # that was never built rather than like one that broke.
        Name = 'plan: the equivalent commands reach the screen'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        # Re-anchored 2026-09-28: the commands are rendered once, when the plan is shown (G-7).
        # Re-anchored 2026-09-29: they are held in a CommandBlock since W6.
        From = '        _commandBlock = new CommandBlock(EquivalentCommand.For(plan));'
        To = '        _commandBlock = CommandBlock.None;'
        Test = 'PlanViewGuards.The_commands_on_screen_are_the_ones_the_core_renders'
    },
    @{
        # BLACK TEXT ON GREY SHIPPED AND EVERY GUARD IN THIS PROJECT WAS SATISFIED. This template
        # named a face and a size and no colour, so the framework's default arrived instead, which
        # is black - 1.69 against the panel's own #343434 where WCAG 2.2 SC 1.4.3 asks 4.5. It
        # covered the warnings, the refusals, the failures and the command a person is meant to
        # copy, and it was the owner who found it rather than any instrument here.
        #
        # WHY THE TWO GUARDS THAT LOOK LIKE THEY COVER IT DO NOT, which is the half worth keeping.
        # AppearanceGuards refuses a theme TextBlock STYLE naming no colour, and this is a
        # DataTemplate. ContrastGuards measures every DECLARED brush against the WINDOW, and here
        # nobody declared one and the surface is the panel rather than the window. Both are right
        # about what they check. Only a guard reading the colour the built TextBlock ENDED UP with
        # can answer this, so that is what the named test does.
        Name = 'plan panel: every sentence on it names a colour that reads on the panel'
        # RE-ANCHORED 2026-09-07: Plan.xaml split in two when the markup ratchet fired on it, and
        # what one LINE of a plan looks like went to PlanLines.xaml. The colour this proves is on
        # the sentence template, so it went with it. The register's own header says to grep this
        # file for every file you split - this is that.
        # MISSED ON THE GATE BEFORE 0.3.0 (backlog 464): since PR 17 this template draws only the
        # problem list, and the test's plan had no problems. Since 2026-09-28 it picks a driver too.
        # RE-ANCHORED 2026-09-30: the refusals and the warnings are one template in PlanOffers.xaml
        # now, and the anchor names it, because PlanFailureTemplate in the same file has the line too.
        File = 'src\Bws.Gui\Themes\PlanOffers.xaml'
        From = '    <DataTemplate x:Key="PlanSentenceTemplate">
        <StackPanel Margin="{StaticResource MarginBetweenControls}">
            <TextBlock Text="{Binding Text}"
                       TextWrapping="Wrap"
                       Foreground="{StaticResource TextOnSurface}" />'
        To = '    <DataTemplate x:Key="PlanSentenceTemplate">
        <StackPanel Margin="{StaticResource MarginBetweenControls}">
            <TextBlock Text="{Binding Text}"
                       TextWrapping="Wrap" />'
        Test = 'PlanViewGuards.Every_word_on_the_panel_can_be_read_on_the_surface_it_is_drawn_on'
    },
    @{
        # THE ONE LINE WITHOUT WHICH EVERY OTHER SELECTION GUARD PASSES ANYWAY. WPF drops a second
        # selected item under Single, so a copy of two rows comes back as a copy of one and every
        # assertion about the text would be about the wrong thing while looking satisfied.
        Name = 'selection: the list takes more than one row'
        File = 'src\Bws.Gui\MainWindow.xaml'
        From = 'SelectionMode="Extended"'
        To = 'SelectionMode="Single"'
        Test = 'SelectionGuards.The_list_takes_more_than_one_row'
    },
    @{
        # A COPY ACTS ON EVERY PICKED ROW, and the failure this prevents is the quiet one: five rows
        # highlighted, one of them copied, and nothing on screen to say which. Owner's decision,
        # 2026-08-18 - a menu opened over a selection acts on the selection.
        #
        # Take(1) rather than removing the read, because the shape being asserted is "all of them"
        # rather than "any of them" and a version that copies the first is exactly what somebody would
        # write by accident when the grid still took one row.
        #
        # THE TEST NAMED HERE IS THE SECOND ONE TRIED, AND THE FIRST BEING MISSED WAS A FINDING ABOUT
        # THE TEST. SelectionGuards asks Copying directly, so this mutation changed nothing it could
        # see - the window's own path from a key press to the clipboard was never on it. Everything
        # about a copy that can go wrong lives on that path, so the entry points at the guard that
        # walks it.
        Name = 'selection: a copy acts on every picked row rather than the first'
        File = 'src\Bws.Gui\MainWindow.Menu.cs'
        From = 'var rows = Entries.SelectedItems.OfType<EntryRow>().ToList();'
        To = 'var rows = Entries.SelectedItems.OfType<EntryRow>().Take(1).ToList();'
        Test = 'CopyingAndPanelGuards.Control_C_over_two_chosen_rows_copies_both_of_them'
    },
    @{
        # A RIGHT CLICK ON A ROW OUTSIDE THE SELECTION REPLACES IT. Marking a row selected only ever
        # ADDS, so without this the menu acts on what somebody pointed at PLUS whatever was
        # highlighted earlier - the confidently wrong answer PointAtRowBeforeMenu exists to prevent,
        # arriving by the back door.
        #
        # THE FIRST VERSION OF THIS ENTRY CAME BACK MISSED AND THAT WAS A FINDING ABOUT THE CODE rather
        # than about the guard. It removed an UnselectAll standing before the assignment and nothing
        # broke, because assigning SelectedItem on a grid in Extended mode already clears the rest. The
        # call was redundant and is gone. The anchor is now the line that carries the behaviour, mutated
        # into the mistake somebody would really make: adding to the selection instead of replacing it,
        # which is exactly what marking a row selected does.
        Name = 'selection: a menu on a row nobody picked replaces the selection'
        File = 'src\Bws.Gui\MainWindow.Menu.cs'
        From = 'Entries.SelectedItem = entry;'
        To = 'Entries.SelectedItems.Add(entry);'
        Test = 'SelectionGuards.A_menu_opened_on_a_row_outside_the_selection_acts_on_that_row_alone'
    },
    @{
        # THE OTHER DIRECTION, and without it the selection would be useless: a right click inside it
        # has to keep it, or there is no way to reach the menu for five rows at once.
        #
        # The condition is made unsatisfiable with a comparison rather than with `false`, because a
        # constant condition makes the branch unreachable and warnings are errors here - the mutation
        # would come back BROKEN and prove nothing.
        Name = 'selection: a menu on a row already picked keeps the whole selection'
        File = 'src\Bws.Gui\MainWindow.Menu.cs'
        From = 'if (Entries.SelectedItems.Contains(entry))'
        To = 'if (Entries.SelectedItems.Contains(entry) && Entries.Items.Count < 0)'
        Test = 'SelectionGuards.A_menu_opened_on_a_row_inside_the_selection_keeps_the_whole_selection'
    },
    @{
        # THE FAULT THIS BRIDGE ACTUALLY FOUND, PUT BACK. The command line accepts --dependents on a
        # stop and a restart and REFUSES it on a start - starting is not the mirror of stopping,
        # because the manager brings up whatever an entry needs by itself. Without the second half of
        # this condition the core renders `bws start X --dependents`, which reads exactly like a
        # command and is rejected the moment somebody pastes it.
        #
        # It is not a hypothetical shape: a selection carries ONE such flag for every entry in it, so
        # somebody ticking the cascade and asking for a start would have been handed a bad line for
        # every service they picked. OptionSurface's own comment says the same omission was made one
        # layer down and missed the first time round.
        Name = 'command: a switch is rendered only onto a verb that accepts it'
        File = 'src\Bws.Core\Planning\EquivalentCommand.cs'
        From = '&& action.Kind is ActionKind.Stop or ActionKind.Restart or ActionKind.ForceStop or ActionKind.ForceRestart)'
        To = '&& true)'
        Test = 'EquivalentCommandBridgeGuards.Every_switch_the_core_renders_is_one_this_tool_accepts_on_that_verb'
    },
    @{
        # A verb nobody can type renders a whole command that does not exist, switches and all. The
        # spelling is deliberate rather than derived from the name of the enum value, because
        # lower-casing a name works for exactly as long as every value is one word - and this registry
        # already carries that lesson for text keys elsewhere.
        Name = 'command: the verb is one the tool really has'
        File = 'src\Bws.Core\Planning\EquivalentCommand.cs'
        From = 'ActionKind.Restart => "restart",'
        To = 'ActionKind.Restart => "restarts",'
        Test = 'EquivalentCommandBridgeGuards.Every_verb_the_core_renders_is_one_this_tool_has'
    },
    @{
        # The order is what makes these lines worth pasting into a runbook. LanmanWorkstation needs
        # MRxSmb20, so a runbook in the order somebody clicked fails on its first line - and the
        # plans are already in the order this tool would use, which is the whole reason the lines are
        # taken from them rather than from what was asked.
        Name = 'command: the lines come out in the order the plans will run'
        File = 'src\Bws.Core\Planning\EquivalentCommand.cs'
        From = '[.. plan.Plans.Where(one => Renders(one.Action)).Select(one => For(one.Action))];'
        To = '[.. plan.Plans.Where(one => Renders(one.Action)).OrderByDescending(one => one.Action.ServiceName, StringComparer.Ordinal).Select(one => For(one.Action))];'
        Test = 'EquivalentCommandTests.A_selection_renders_in_the_order_the_plans_will_run'
    },
    @{
        # A BRIDGE THAT FINDS NOTHING MUST GO RED RATHER THAN GREEN, and this project has now paid for
        # that shape four times - most recently in audit.ps1 on 2026-08-18, where a check looked for a
        # shape in one named file, a seam moved it elsewhere, the loop never ran once and the script
        # printed "5 checks run, 0 problems".
        #
        # THIS ENTRY BREAKS THE PRODUCT TO PROVE A PROPERTY OF THE GUARD, which is unusual and is the
        # only way round: the guard walks the switches the core renders, so a core that renders none
        # leaves every assertion inside the loop unexecuted. "No switches rendered" and "every switch
        # is valid" are then the same green.
        #
        # The condition is made unsatisfiable with a comparison rather than with `false`, because a
        # constant condition makes the branch unreachable and warnings are errors here - the mutation
        # would come back BROKEN and prove nothing, which is the trap this registry's header names.
        Name = 'command: the bridge reddens when the core renders no switch at all'
        File = 'src\Bws.Core\Planning\EquivalentCommand.cs'
        #
        # AND IT WENT MISSED ON 2026-09-06, HAVING SURVIVED A BEHAVIOUR CHANGE - which is the
        # sixth way this registry gets broken and the one nothing detects. It used to disable
        # the --dependents arm, on the premise that this was the only switch the core rendered,
        # so switching it off left nothing for the loop to check. The forcing verb arrived with
        # --restart, which renders unconditionally, so the counter never reached zero again and
        # the entry quietly proved something else while staying green in stale-check.
        #
        # It now disables the JOIN rather than one arm, which is the only place all of them
        # pass through and stays true however many switches there are.
        #
        # RE-ANCHORED 2026-10-05 (stability report W-12): the join moved into Line, which writes the
        # switches after the name - or before `--` for a name that begins with a dash. The bridge asks
        # about Spooler, so the arm that matters is the ordinary one.
        From = ': [Tool, verb, .. words, .. switches];'
        To = ': [Tool, verb, .. words];'
        Test = 'EquivalentCommandBridgeGuards.Every_switch_the_core_renders_is_one_this_tool_accepts_on_that_verb'
    },
    @{
        # THE ONE ANSWER A SINGLE PLAN CANNOT GIVE, so it is the one that has to be proved. The
        # manager refuses to stop an entry while something running needs it, and a selection holding
        # both sides of a dependency has exactly one order that works.
        #
        # The two branches are SWAPPED rather than one being removed, and that is deliberate on two
        # counts. Returning the input in both branches leaves a parameter unused and an analyser
        # ready to call it an error, which would come back BROKEN and prove nothing - the trap this
        # registry's header warns about. And the asymmetry is itself the behaviour: taking things
        # down is ordered because the manager refuses otherwise, starting is not because the manager
        # already starts what a service needs. Swapping reddens both halves of that sentence, which
        # is what the name claims.
        Name = 'bulk: taking a selection down is ordered and starting one is not'
        File = 'src\Bws.Core\Planning\BulkPlanBuilder.cs'
        #
        # RE-ANCHORED 2026-08-26, when setting a start type joined the unordered side and the
        # expression stopped being one line. The swap it makes is unchanged.
        # RE-ANCHORED AGAIN 2026-09-24, when a setting carrying a stop went back to the ordered
        # side and the method started taking the whole ask. The swap it makes is unchanged.
        # RE-ANCHORED 2026-09-29 (S-7): the ordering asks through the build's own DependentsAskedOnce,
        # passed in as `asking`, rather than through the catalogue field. The swap it makes is unchanged.
        From = @'
        action.Kind is ActionKind.Start || (action.Kind == ActionKind.SetStartType && !action.AlsoStop)
            ? asked
            : DependentsFirst.Order(asking, asked);
'@
        To = @'
        action.Kind is ActionKind.Start || (action.Kind == ActionKind.SetStartType && !action.AlsoStop)
            ? DependentsFirst.Order(asking, asked)
            : asked;
'@
        Test = 'BulkPlanTests.An_entry_something_else_in_the_selection_needs_is_dealt_with_last'
    },
    @{
        # Owner's decision, 2026-08-18, and the failure mode is the expensive one: one driver caught
        # by a rubber-band selection holding up nineteen services. With `break` the refusal abandons
        # everything after it, which is the second of the three answers the specification's open
        # question 9 offers.
        #
        # The test it names puts the refused entry FIRST for exactly this reason. With it last, the
        # mutation would change nothing and come back MISSED against a guard that is in fact fine -
        # which is how a green registry entry can describe a test that proves nothing.
        Name = 'bulk: an entry the plan refuses does not stop the rest of the selection'
        File = 'src\Bws.Core\Planning\BulkPlanBuilder.cs'
        From = 'continue;'
        To = 'break;'
        Test = 'BulkPlanTests.An_entry_the_plan_refuses_does_not_stop_the_rest_of_the_selection'
    },
    # S-7 of the performance report, 2026-09-29: one answer per name for the whole of one bulk
    # build. Three entries because the behaviour has three halves that fail apart - the plans
    # sharing the ordering's answers, anything being remembered at all, and nothing outliving the
    # build. The measured cost behind it: 1433 questions about 800 names before, tools/plan-probe.
    @{
        Name = 'bulk: the plans share the answers the ordering already got'
        File = 'src\Bws.Core\Planning\BulkPlanBuilder.cs'
        From = 'var builder = new PlanBuilder(entries, asking, processes);'
        To = 'var builder = new PlanBuilder(entries, catalog, processes);'
        Test = 'BulkPlanTests.A_bulk_plan_asks_who_depends_on_each_name_once'
    },
    @{
        Name = 'bulk: an answer about a name is remembered for the rest of the build'
        File = 'src\Bws.Core\Planning\DependentsAskedOnce.cs'
        From = '            _answers[serviceName] = answer;'
        To = '            _ = answer;'
        Test = 'BulkPlanTests.A_bulk_plan_asks_who_depends_on_each_name_once'
    },
    @{
        # A static field is the shape an "optimisation" of this would take - share the memory
        # between presses - and it turns a cascade of a moment ago into the one shown now.
        Name = 'bulk: nothing remembered outlives the build that asked'
        File = 'src\Bws.Core\Planning\DependentsAskedOnce.cs'
        From = '    private readonly Dictionary<string, Reading<IReadOnlyList<string>>> _answers'
        To = '    private static readonly Dictionary<string, Reading<IReadOnlyList<string>>> _answers'
        Test = 'BulkPlanTests.The_next_build_asks_the_manager_again'
    },
    # Backlog 475, 2026-09-29: a selection carrying a heavy warning is refused out loud rather than
    # asked for a name that could change nothing. Six entries, one per half: the box goes, the
    # refusal comes, the button says it, the markup binds it, no single name agrees to a selection,
    # and the section carried out of the footer is not a Tab stop.
    @{
        Name = 'heavy ask: a selection gets no box asking for a name'
        File = 'src\Bws.Gui\ViewModels\Planned.Forcing.cs'
        From = 'public bool NeedsTyping => AsksHeavily && Asked(_plan!) == 1;'
        To = 'public bool NeedsTyping => AsksHeavily;'
        Test = 'PlanConfirmationGuards.A_selection_holding_a_critical_entry_is_refused_rather_than_asked_for_a_name'
    },
    @{
        Name = 'heavy ask: a selection carrying one is refused out loud'
        File = 'src\Bws.Gui\ViewModels\Planned.Forcing.cs'
        From = 'public bool RefusesSelection => AsksHeavily && Asked(_plan!) > 1;'
        To = 'public bool RefusesSelection => AsksHeavily && Asked(_plan!) > 1000;'
        Test = 'PlanConfirmationGuards.A_selection_holding_a_critical_entry_is_refused_rather_than_asked_for_a_name'
    },
    @{
        # The dangerous one. With the box's own clause in place of the refusal's, the first entry's
        # name - the one the box used to ask for - agrees to every entry in the selection.
        Name = 'heavy ask: one name never agrees to a selection'
        File = 'src\Bws.Gui\ViewModels\Planned.Forcing.cs'
        From = @'
        !AsksHeavily
        || (NeedsTyping
'@
        To = @'
        !AsksHeavily
        || (AsksHeavily
'@
        Test = 'PlanConfirmationGuards.A_selection_holding_a_critical_entry_is_refused_rather_than_asked_for_a_name'
    },
    @{
        Name = 'heavy ask: the grey button says the refusal, not a box'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        From = ': !Confirmed && RefusesSelection ? Texts.Of("gui.plan.confirm.selection")'
        To = ': !Confirmed && false ? Texts.Of("gui.plan.confirm.selection")'
        Test = 'PlanConfirmationGuards.A_selection_holding_a_critical_entry_is_refused_rather_than_asked_for_a_name'
    },
    @{
        Name = 'heavy ask: the refusal sentence is bound to the refusal'
        File = 'src\Bws.Gui\PlanConfirmation.xaml'
        From = 'Visibility="{Binding RefusesSelection, Converter={StaticResource WhenTrue}}"'
        To = 'Visibility="{Binding NeedsTyping, Converter={StaticResource WhenTrue}}"'
        Test = 'PlanConfirmationGuards.A_refused_selection_shows_its_sentence_where_the_box_would_stand_and_no_box'
    },
    @{
        Name = 'heavy ask: the section carried out of the footer is not a Tab stop'
        File = 'src\Bws.Gui\PlanConfirmation.xaml'
        From = 'IsTabStop="False">'
        To = 'IsTabStop="True">'
        Test = 'PlanConfirmationGuards.The_section_is_not_a_stop_on_the_way_to_the_box'
    },
    @{
        # Rule 8 applied to a preview. Two selected entries can produce plans that both name the
        # same third one, and tidying the repeat away would make the preview SHORTER than the run -
        # the one thing `ADR-11` exists to prevent, because the runner takes the steps exactly as
        # shown and never works anything out again.
        #
        # Raising the threshold to three leaves the property answering "nothing overlaps" on a
        # selection where three entries do, which is the silent version of the same fault: the
        # preview shows a service twice and says nothing about why.
        Name = 'bulk: an entry named by two plans is said out loud rather than tidied away'
        File = 'src\Bws.Core\Planning\BulkPlan.cs'
        From = '.Where(group => group.Count() > 1)'
        To = '.Where(group => group.Count() > 2)'
        Test = 'BulkPlanTests.An_entry_named_by_two_plans_is_said_out_loud'
    },
    @{
        # `C2`'s own sentence is "stopping these 3 will drag another 7", and the second number is
        # wrong in both directions if the asked-for set is not taken out. Inverted, the property
        # reports the entries somebody selected as though they were consequences nobody asked for -
        # so a selection of two would announce that it drags in two extra services, and a person
        # reading the preview would be told the operation is twice the size it is.
        Name = 'bulk: what comes along excludes what was asked for'
        File = 'src\Bws.Core\Planning\BulkPlan.cs'
        From = '!asked.Contains(name)'
        To = 'asked.Contains(name)'
        Test = 'BulkPlanTests.What_comes_along_is_counted_once_and_excludes_what_was_asked_for'
    },
    @{
        # THE ENTRY WORTH HAVING OUT OF THE FOUR BELOW, because the mutation IS the implementation
        # somebody reaches for: ask each run for its own way back and join the answers. It compiles,
        # it reads better than the real thing, and it hands somebody a first line the manager
        # refuses. A selection is dealt with dependants first, so putting it back has to start with
        # what they depend on - joined run by run, the order comes out exactly backwards.
        Name = 'bulk run: the way back is one answer for the selection, not one per run joined'
        File = 'src\Bws.Core\Planning\BulkRun.cs'
        From = 'public IReadOnlyList<ReversalStep> Reversal => NetEffect.Of(Results);'
        To = 'public IReadOnlyList<ReversalStep> Reversal => [.. Runs.SelectMany(run => run.Reversal)];'
        Test = 'BulkRunTests.The_way_back_is_one_answer_for_the_whole_selection_in_the_reverse_order'
    },
    @{
        # State (T) of rule 10, one level up from where this project already paid for it. Stopping at
        # the first plan after an interruption is cheaper and hands back a report that says nothing
        # at all about the entries after it - which is how somebody ends up not knowing whether the
        # other nineteen services were touched. The runner already writes "cancelled" onto every step
        # it was given, so going through them all is what makes that report exist.
        Name = 'bulk run: every plan gets a run, an interrupted selection included'
        File = 'src\Bws.Core\Planning\BulkRunner.cs'
        From = 'foreach (var one in plan.Plans)'
        To = 'foreach (var one in plan.Plans.Take(1))'
        Test = 'BulkRunTests.An_interrupted_selection_still_reports_what_it_left_alone'
    },
    @{
        # Progress is read against the numbered list the panel showed, and that list is numbered
        # across the whole selection. Dropping the offset restarts at one for every plan, so a person
        # watching a run over four entries sees "step 1" four times and cannot tell how far it has
        # got. The same failure PlanRunner already refuses inside one plan, arriving between them.
        Name = 'bulk run: a step announces its place in the selection, not in its own plan'
        File = 'src\Bws.Core\Planning\BulkRunner.cs'
        From = ': (step, number) => starting(step, offset + number);'
        To = ': (step, number) => starting(step, number);'
        Test = 'BulkRunTests.A_step_announces_its_place_in_the_whole_selection'
    },
    @{
        # Checked for all of them before any of them runs. Weakened to the bulk's own emptiness, a
        # plan with problems is refused by the runner that reaches it - on the fourteenth plan, with
        # thirteen already carried out and an exception where the report should be. A write path that
        # throws after moving a machine has destroyed the only record of what it moved.
        Name = 'bulk run: a plan with problems is refused BEFORE anything runs'
        File = 'src\Bws.Core\Planning\BulkRunner.cs'
        From = 'if (!plan.IsRunnable || plan.Plans.Any(one => !one.IsRunnable))'
        To = 'if (!plan.IsRunnable)'
        Test = 'BulkRunTests.A_selection_holding_a_plan_with_problems_is_never_carried_out'
    },
    @{
        # THE PROMISE OF `ADR-11` ITSELF, AND IT HAD NO ENTRY UNTIL 2026-08-19. The runner takes the
        # steps exactly as the preview showed them and never works anything out again - one level up,
        # that means the PLANS go in the order the preview showed. Reversed, the machine is changed in
        # an order nobody was shown, which is the single thing this whole pattern exists to prevent,
        # and every other assertion in BulkRunTests would still be green.
        #
        # The same anchor as the entry above it, with a different edit. Take(1) proves that every plan
        # gets a run, this proves the ORDER of them, and neither covers the other.
        Name = 'bulk run: the plans go in the order the preview showed'
        File = 'src\Bws.Core\Planning\BulkRunner.cs'
        From = 'foreach (var one in plan.Plans)'
        To = 'foreach (var one in plan.Plans.Reverse())'
        Test = 'BulkRunTests.Every_plan_is_carried_out_in_the_order_the_preview_showed'
    },
    @{
        # THE OWNER'S DECISION OF 2026-08-18: entries in a selection are independent, so one that will
        # not move does not hold up the rest. One driver in a selection does not stop nineteen
        # services.
        #
        # The mutation is the implementation BulkRunner's own comment argues against by name - "
        # breaking out here would be cheaper and would hand back a report that says nothing at all
        # about the entries after the interruption". Written as an added guard rather than a changed
        # one, so nothing is orphaned and the tree still compiles.
        Name = 'bulk run: a plan that fails does not stop the plans after it'
        File = 'src\Bws.Core\Planning\BulkRunner.cs'
        From = 'runs.Add(runner.Run(one, timeout, cancellation, abandonment, announce));'
        To = 'runs.Add(runner.Run(one, timeout, cancellation, abandonment, announce)); if (!runs[^1].Completed) { break; }'
        Test = 'BulkRunTests.A_plan_that_fails_does_not_stop_the_plans_after_it'
    },
    @{
        # THE PRESS THAT ARRIVES WHILE A RUN IS ALREADY GOING, from a second click or from the
        # keyboard, before the first one has reached the screen. A live button here is a second ask
        # one click away - and this tool is asking a manager to stop services.
        #
        # ONE ENTRY IS DELIBERATELY ABSENT FROM THIS FILE AND IT BELONGS BESIDE THIS ONE. The guard
        # clause in MainWindow.CarryOut, which turns a press back before anything is built, has no
        # mutation and never will: a mutation that let a press through would make the test suite stop
        # real services on whatever machine ran it. That half is covered by a run on a throwaway
        # machine, which is this project's hard rule about writes.
        Name = 'plan panel: the button goes quiet while a run is happening'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        # RE-ANCHORED 2026-09-07, when a fifth clause arrived: the name typed back, for a plan
        # that ends more than the entry somebody named. AND AGAIN 2026-09-15, when the sixth did:
        # the box of seconds saying something that is not seconds.
        From = '        Showing && Elevated && !Busy && _run is null && _plan is { IsRunnable: true }
        && !HasWaitingProblem && Confirmed;'
        To = '        Showing && Elevated && _run is null && _plan is { IsRunnable: true }
        && !HasWaitingProblem && Confirmed;'
        Test = 'CarryingGuards.The_button_is_live_only_while_there_is_a_plan_nobody_has_carried_out'
    },
    @{
        # The worst bug this panel could have had: a report of what happened to five services sitting
        # under the steps of a plan for five different ones, with nothing on screen saying the two do
        # not belong together. The mutation is one deleted line, which is how it would arrive.
        Name = 'plan panel: a new plan drops the report of the last run'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        # Re-anchored 2026-09-28: the step count and the commands are set between the plan and the
        # run now (G-7), so the anchor is the run and what follows it in Show.
        # Re-anchored 2026-09-30: the progress line moved to Underway (backlog 497).
        From = '        _run = null;
        Busy = false;
        Underway.End();
        Showing = true;'
        To = '        Busy = false;
        Underway.End();
        Showing = true;'
        Test = 'PlanReportGuards.Asking_about_something_else_drops_the_report_of_the_last_run'
    },
    @{
        # `ADR-11` promises a plan that can be reversed, and this is that promise arriving in a
        # window for the first time. Emptied, the panel reports a machine changed and offers no way
        # back - Nielsen's third heuristic, which this product names as its own weakest.
        Name = 'plan panel: the way back reaches the screen'
        # Re-anchored 2026-09-29: the way back is rendered once per run into a CommandBlock (W6).
        File = 'src\Bws.Gui\ViewModels\Planned.Sections.cs'
        From = 'new CommandBlock([.. run.Reversal.Select(EquivalentCommand.For)])'
        To = 'new CommandBlock([.. run.Reversal.Take(0).Select(EquivalentCommand.For)])'
        Test = 'PlanReportGuards.The_way_back_reaches_the_screen_as_commands'
    },
    @{
        # Rule 8 of the untouchable rules at the moment it matters most. A run that half worked and
        # reports only a count is the silent partial answer that rule exists against, and the person
        # reading it is holding a machine other people depend on.
        Name = 'plan panel: what did not work reaches the screen'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        From = 'result.Outcome is StepOutcome.Failed or StepOutcome.TimedOut;'
        To = 'result.Outcome is StepOutcome.TimedOut;'
        Test = 'PlanReportGuards.What_did_not_work_reaches_the_screen_with_the_reason'
    },
    @{
        # Three states, three sentences, and the list of steps reads as a report of something done in
        # every one of them - so this line is the only thing telling somebody which one they are
        # looking at. Inverted onto the wrong key, a run in progress reads as a preview.
        Name = 'plan panel: the sentence says a run is happening while one is'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        # Re-anchored 2026-09-30: during a run Underway words the sentence (backlog 497).
        From = ': Busy ? Underway.Notice'
        To = ': Busy ? Texts.Of("gui.plan.notice.notYet")'
        Test = 'CarryingGuards.The_sentence_says_which_of_the_three_states_this_is'
    },
    @{
        # A way to stop a run that is on screen when there is no run is a button that does nothing,
        # and one that is absent while a run happens leaves closing the window as the only way out -
        # which is the failure the command line pays three levels of Ctrl+C for.
        Name = 'plan panel: the way to stop a run is on screen only while one is happening'
        # RE-ANCHORED 2026-09-07: the whole foot of the sheet moved to PlanFooter.xaml when the
        # markup ratchet fired and the confirmation box needed room. The interrupt button went with
        # it, and so does this.
        File = 'src\Bws.Gui\PlanFooter.xaml'
        From = 'Visibility="{Binding Busy, Converter={StaticResource WhenTrue}}"'
        To = 'Visibility="{Binding Showing, Converter={StaticResource WhenTrue}}"'
        Test = 'CarryingGuards.The_way_to_stop_a_run_is_on_screen_only_while_one_is_happening'
    },
    @{
        # THE OWNER'S DECISION OF 2026-08-18, WHICH THE FIRST VERSION OF THIS PANEL DID NOT KEEP, and
        # a screenshot is what found it rather than anything in this file. The first look at the panel
        # was on a session without administrator rights - the window said so at the bottom - and the
        # button was live with nothing beside it to explain. Pressing it buys a column of refusals
        # from the manager and somebody working out why, which is what this clause exists against.
        Name = 'plan panel: a session that cannot change anything cannot press the button'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        # RE-ANCHORED 2026-09-07 with the clause above, for the same reason, and again 2026-09-15.
        From = '        Showing && Elevated && !Busy && _run is null && _plan is { IsRunnable: true }
        && !HasWaitingProblem && Confirmed;'
        To = '        Showing && !Busy && _run is null && _plan is { IsRunnable: true }
        && !HasWaitingProblem && Confirmed;'
        Test = 'CarryingGuards.A_session_that_cannot_change_anything_says_so_before_anybody_presses'
    },
    @{
        # BACKLOG 203, and the second thing the owner said about the first screenshot of this panel.
        # A heading reading "Not included, and why" over an empty list states something false - and a
        # test reading the LINES rather than the section would call that correct, which is why the
        # guard asks whether the section is on the screen at all.
        Name = 'plan panel: a section with nothing in it takes its heading with it'
        File = 'src\Bws.Gui\ViewModels\Planned.Sections.cs'
        From = 'public bool HasProblems => Problems.Count > 0;'
        To = 'public bool HasProblems => true;'
        Test = 'CarryingGuards.A_section_with_nothing_in_it_takes_its_heading_off_the_screen'
    },
    @{
        # THE STATE (T) OF RULE 10, WHICH THIS PROJECT HAS ALREADY PAID FOR ONCE - two presses of
        # Ctrl+C in the command line, found by a run on a virtual machine rather than by asking the
        # question beforehand. Without the refusal, WPF ends the process when the last window closes,
        # so a reach for the corner half way through a run leaves a cascade switched off and prints
        # nothing at all.
        #
        # THIS AND THE TWO ENTRIES BELOW WERE IMPOSSIBLE UNTIL 2026-08-19, and that is the whole
        # point of the seam they lean on. Nothing could put this window into a run without starting
        # a real one, so the method was never executed by anything - MainWindow.TakeThisAsARun,
        # added on the owner's decision, takes a task and a token source and nothing that could
        # become a write.
        Name = 'window: a close half way through a run is refused'
        File = 'src\Bws.Gui\MainWindow.Carrying.cs'
        From = 'e.Cancel = true;'
        To = 'e.Cancel = false;'
        Test = 'CarryingGuards.A_close_during_a_run_is_refused_once_and_taken_again_when_the_run_ends'
    },
    @{
        # A refusal that does not ASK THE RUN TO STOP is a window hanging on a corner nobody can use.
        # The anchor takes both lines because the same call stands in the interrupt handler fifteen
        # screens up, and this registry replaces every occurrence of what it is given.
        Name = 'window: refusing to close also asks the run to stop'
        File = 'src\Bws.Gui\MainWindow.Carrying.cs'
        # Re-anchored 2026-09-30: the close tells the sheet first, through Underway (backlog 497).
        From = '        _model.Planned.Underway.Interrupt(closing: true);
        _stopping?.Cancel();'
        To = '        _model.Planned.Underway.Interrupt(closing: true);'
        Test = 'CarryingGuards.A_close_during_a_run_is_refused_once_and_taken_again_when_the_run_ends'
    },
    @{
        # The third way this one behaviour fails, and the only one that is not silent: refused
        # forever, somebody is left with a window that will not go away. Three entries on one test
        # because its own comment names three failure modes, and a register showing one of them
        # would say the other two were considered and are not held.
        Name = 'window: the close is taken again once the run has ended'
        File = 'src\Bws.Gui\MainWindow.Carrying.cs'
        From = '        Close();'
        To = ''
        Test = 'CarryingGuards.A_close_during_a_run_is_refused_once_and_taken_again_when_the_run_ends'
    },
    @{
        # BACKLOG 298. The flag went up before the first step and came down in one place only, so
        # anything thrown in between left the panel claiming a run was under way for as long as it
        # stayed open - with the button dead and a tooltip explaining that it is dead BECAUSE a run
        # is happening. Mutating the call rather than the method body, because the method is the
        # easy half: what was missing was anybody calling it on the way out.
        Name = 'window: a run that throws stops the panel claiming one is under way'
        File = 'src\Bws.Gui\MainWindow.Carrying.cs'
        From = '            _model.Planned.NoLongerRunning();'
        To = ''
        Test = 'CarryingGuards.A_run_that_throws_leaves_the_panel_able_to_ask_again'
    },
    @{
        # The other half of the same repair, and it fails differently: the flag comes down and the
        # bindings are never told, so the button stays grey while the model says it should not be.
        # This project has been caught four times by markup that binds correctly and paints
        # something else, which is why the test reads the button rather than the property.
        Name = 'plan panel: the button hears that the run has stopped'
        File = 'src\Bws.Gui\ViewModels\Planned.Running.cs'
        # Re-anchored 2026-09-24 without the closing braces: the review of PR #17 put a raise of the
        # warnings after these three, and this is the only place they stand in this order.
        From = '        Raise(nameof(CanCarryOut));
        Raise(nameof(CarryOutTip));
        Raise(nameof(Notice));'
        To = '        Raise(nameof(Notice));'
        Test = 'CarryingGuards.A_run_that_throws_leaves_the_panel_able_to_ask_again'
    },
    @{
        # BACKLOG 308(c). The reader builds one string out of every byte, so a path pointing at
        # something enormous ends the process with an out of memory failure - the one shape the
        # entry point's own catch cannot turn into a sentence worth reading.
        Name = 'snapshot: a file too big to be one is refused before it is read'
        File = 'src\Bws.Core\Snapshots\SnapshotOnDisk.cs'
        From = '            if (File.Exists(path) && new FileInfo(path).Length > Biggest)'
        To = '            if (File.Exists(path) && new FileInfo(path).Length > long.MaxValue)'
        Test = 'SnapshotSizeGuards.A_file_too_big_to_be_a_snapshot_is_turned_back_before_it_is_even_opened'
    },
    @{
        # The same shape on the other file this product reads, and this one is read while the
        # window is being constructed - so the failure is a program that will not start.
        Name = 'layout: a file too big to be one is refused before it is read'
        File = 'src\Bws.Gui\PreferencesFile.cs'
        From = '            if (new FileInfo(Where).Length > Biggest)'
        To = '            if (new FileInfo(Where).Length > long.MaxValue)'
        Test = 'PreferencesFileGuards.A_file_too_big_to_be_a_layout_is_refused_rather_than_read_into_memory'
    },
    @{
        # BACKLOG 305. The cascade goes into a dictionary keyed by name, and a dictionary meets a
        # repeated key by throwing - so a name EnumDependentServices happened to give twice turned a
        # PREVIEW into a sentence about a failure, with nothing to say the failure was ours.
        Name = 'plan: a dependant named twice is one step rather than a failure'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = '            .DistinctBy(entry => entry.ServiceName, StringComparer.OrdinalIgnoreCase)
'
        To = ''
        Test = 'PlanBuilderTests.A_dependant_the_manager_names_twice_is_one_step_rather_than_a_failure'
    },
    @{
        # BACKLOG 307. Walking InnerException is the whole chain for an ordinary exception and one
        # branch of a tree for an AggregateException - and the two readings this tool leans on both
        # fail several times at once. What a person got was "One or more errors occurred."
        Name = 'causes: a wrapper holding several failures gives all of them'
        File = 'src\Bws.Core\Causes.cs'
        From = '            if (level is AggregateException several && several.InnerExceptions.Count > 0)'
        To = '            if (level is AggregateException several && several.InnerExceptions.Count > 0 && false)'
        Test = 'CausesTests.A_wrapper_holding_several_failures_gives_all_of_them_rather_than_its_own_sentence'
    },
    @{
        # BACKLOG 308(a). The converter refuses text that is not a number and accepts every number
        # there is, so 1e300 came through as a width - and the column makes its own width its floor,
        # so the grid was asked for something that cannot be drawn and the list went with it.
        Name = 'columns: a width no screen could show is not a width'
        File = 'src\Bws.Gui\ListColumns.cs'
        From = '        && width.Value <= Widest;'
        To = '        && width.Value <= double.MaxValue;'
        Test = 'KeptWidthGuards.A_width_no_screen_could_show_falls_back_to_the_theme_and_is_named'
    },
    @{
        # BACKLOG 308(e). The base call is what raises Closing, and it stood before the decision to
        # refuse - so a close refused half way through changing a machine told the one listener, the
        # column layout, that the window was going. It wrote the file for a window that was staying,
        # and again on the close that really happened.
        Name = 'window: a refused close does not tell anybody the window is going'
        File = 'src\Bws.Gui\MainWindow.Carrying.cs'
        From = '        if (_running is not { IsCompleted: false } running)
        {
            base.OnClosing(e);

            return;
        }'
        To = '        base.OnClosing(e);

        if (_running is not { IsCompleted: false } running)
        {
            return;
        }'
        Test = 'CarryingGuards.A_close_during_a_run_is_refused_once_and_taken_again_when_the_run_ends'
    },
    @{
        # BACKLOG 300, and what it holds is smaller than the report claimed - the process does not
        # die, which was measured. What is left is a reading coming back to rebuild rows and move a
        # status line for a window that has gone.
        Name = 'window: a reading coming back to a closed window writes nothing'
        File = 'src\Bws.Gui\ViewModels\Readings.cs'
        From = '    internal void NoLongerWanted() => _gone = true;'
        To = '    internal void NoLongerWanted() => _gone = false;'
        Test = 'WindowGuards.A_reading_that_comes_back_after_the_window_has_gone_writes_nothing'
    },
    @{
        # BACKLOG 299. The runner asked one clock both what time it is and how long something took,
        # and a machine resuming moves the first without moving the second. The mutation swaps the
        # ruler for a wall clock reading of the same TYPE, which is the only way to put the fault
        # back now that the two are different members - a plain revert would not compile, and a
        # mutation that does not compile proves nothing either way.
        # MISSED ON THE FIRST TRY, AND THE MISS IS WORTH KEEPING. The first version swapped the
        # SUBTRACTION to a wall clock reading and left the start on the ruler, which gives a large
        # positive number rather than a negative one - the guard stayed green and would have been
        # recorded as holding something it does not. Both ends of a duration have to come from the
        # same clock for the fault to exist at all, so the mutation moves the START.
        Name = 'plan: a step is timed from a ruler rather than from a wall clock'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = '        var started = clock.Elapsed;'
        To = '        var started = clock.Now.TimeOfDay;'
        Test = 'PlanRunClockTests.A_clock_jumping_backward_does_not_report_a_step_as_having_taken_less_than_no_time'
    },
    @{
        # The other half, and the one that decides whether a healthy service is given up on: the
        # deadline a step is watched against. A step can be watched for up to a minute, and an hour
        # arriving on the wall clock is what a suspended virtual machine looks like from in here.
        # MISSED ON 2026-09-30 after the limit started counting from the last progress: the test's
        # entry rose at every look, so both ends of the comparison moved together. The test now has
        # two looks without a rise, which is where a wall clock jump breaks a healthy step.
        Name = 'plan: a deadline is measured with a ruler rather than a wall clock'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '            var now = clock.Elapsed;'
        To = '            var now = clock.Now.TimeOfDay;'
        Test = 'PlanRunClockTests.A_clock_jumping_forward_does_not_give_up_on_a_service_that_is_still_working'
    },
    @{
        # BACKLOG 301. The comment that defended the old arrangement carried a real measurement of
        # how DEEP a cascade is and stood where a reader looks for how MANY entries are selected.
        # The whole listing selected and asked to stop was measured at 224-240 ms, all of it round
        # trips to the manager - so this is about a thread rather than about an algorithm, and the
        # mutation puts the work back where it was.
        Name = 'plan: a bulk plan is not worked out on the thread that draws'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Planning.cs'
        # The third argument arrived on 2026-09-08 and stale-check found this anchor the same
        # minute, which is the whole reason that script runs before this one.
        From = '        return Task.Run(() => new BulkPlanBuilder(entries, _catalog, _processes).Build(action));'
        To = '        return Task.FromResult(new BulkPlanBuilder(entries, _catalog, _processes).Build(action));'
        Test = 'PlanBuildingGuards.The_manager_is_not_asked_about_a_cascade_on_the_thread_that_draws'
    },
    @{
        # The race the line above creates, which did not exist while a plan was built in the
        # handler: two asks can now be in flight, and the one that finishes second is not
        # necessarily the one that was asked second.
        Name = 'plan: an overtaken preview does not land on top of a later one'
        File = 'src\Bws.Gui\MainWindow.Menu.cs'
        From = '        if (asked != _previews)'
        To = '        if (asked != _previews && false)'
        Test = 'PlanBuildingGuards.A_preview_overtaken_by_a_later_one_does_not_land_on_top_of_it'
    },
    @{
        # BACKLOG 302. F5 is the one shortcut whose work is a real reading, so it is the one branch
        # that used to hand its answer back after the press had been routed. Mutating the verdict
        # rather than the shape: the shape is now held by the signature, and a test cannot be
        # reddened by something that does not compile.
        Name = 'window: F5 claims its key rather than passing it on'
        File = 'src\Bws.Gui\MainWindow.Keyboard.cs'
        From = '                work = _model.LoadAsync();

                return true;'
        To = '                work = _model.LoadAsync();

                return false;'
        Test = 'KeyboardTests.Refresh_claims_its_key_before_the_reading_it_starts_has_finished'
    },
    @{
        # A guard already said the interrupt is on screen exactly while a run is - a statement about
        # VISIBILITY. Whether pressing it reaches anything was held by nothing until 2026-08-19, and
        # a button that looks right and does nothing is the worse of the two failures: somebody
        # presses it, watches the steps carry on, and has no way to tell whether the tool heard them.
        Name = 'window: pressing the interrupt reaches the run'
        File = 'src\Bws.Gui\MainWindow.xaml.cs'
        From = 'PlanPanel.InterruptRequest += (_, _) => AskTheRunToStop();'
        To = 'PlanPanel.InterruptRequest += (_, _) => { };'
        Test = 'CarryingGuards.Pressing_the_interrupt_reaches_the_run_rather_than_only_the_screen'
    },
    @{
        # THE FAULT THAT SHIPPED FOR A DAY, PUT BACK EXACTLY AS IT ARRIVED. Two styles that mean
        # different things had identical setters, so the line saying whether anything had happened
        # yet was drawn as a label for a list, and the panel had four headings and no body. It came
        # in through a CORRECTION - the notice wore the warning colour, that was real, and moving it
        # onto weight landed it on the other style's exact values.
        #
        # THE EDIT CHANGED ON 2026-09-02 AND THE BEHAVIOUR IT NAMES DID NOT, which is the only
        # shape in which touching a mutation is allowed. It used to add FontWeight, because back
        # then weight was the single thing holding these two styles apart. Backlog 281 took the
        # section label from 14 SemiBold TextOnSurface down to 13 regular TextSubdued, so today the
        # one thing between them is COLOUR - and adding weight now makes them differ MORE rather
        # than land on top of each other. The registry reported MISSED, correctly: the edit had
        # stopped reaching the state the guard forbids.
        #
        # It is a real regression rather than a contrivance. Somebody muting this notice - and the
        # notice is the quieter of the two sentences - lands on the section label's exact values.
        Name = 'theme: a state sentence is not dressed as a section label'
        File = 'src\Bws.Gui\Themes\Text.xaml'
        # Since 2026-09-16 both styles are aliases of the two body styles, so the mutation is the
        # alias pointing at the other body - and the guard resolves BasedOn since the same day, or
        # two aliases with nothing of their own would have read as two empty and equal lists.
        From = '<Style x:Key="PlanNoticeText" TargetType="TextBlock" BasedOn="{StaticResource BodyText}" />'
        To = '<Style x:Key="PlanNoticeText" TargetType="TextBlock" BasedOn="{StaticResource BodySubduedText}" />'
        Test = 'TypeScaleGuards.A_state_sentence_is_not_dressed_as_a_section_label'
    },
    @{
        # A step drawn at the subdued SIZE rather than the subdued COLOUR, which is where this panel
        # started: what will happen to the machine was the smallest text on it, under a heading at
        # fourteen semibold reading "In this order". The label was bigger than the thing it labelled.
        Name = 'theme: the substance of a plan is not smaller than the label above it'
        File = 'src\Bws.Gui\Themes\Text.xaml'
        # An alias since 2026-09-16, so the mutation gives it a size of its own under the base's.
        From = '<Style x:Key="PlanStepText" TargetType="TextBlock" BasedOn="{StaticResource BodySubduedText}" />'
        To = '<Style x:Key="PlanStepText" TargetType="TextBlock" BasedOn="{StaticResource BodySubduedText}"><Setter Property="FontSize" Value="{StaticResource TextSizeSmall}" /></Style>'
        Test = 'TypeScaleGuards.The_substance_of_a_plan_is_not_smaller_than_the_label_above_it'
    },
    @{
        # The title that went on asking what would happen after it had. Found by the owner's second
        # screenshot rather than by anything here: "What stopping Spooler would do" over "Done. The
        # entry is where you asked." and over a row already reading Stopped. Frozen on the preview
        # wording, the panel calls a report a preview for as long as it is open.
        Name = 'plan panel: the title stops asking what would happen once something has'
        File = 'src\Bws.Gui\ViewModels\Planned.Title.cs'
        From = '        : _run is null'
        To = '        : true'
        Test = 'PlanReportGuards.The_title_stops_asking_what_would_happen_once_something_has'
    },
    @{
        # `E5` belongs to the preview, so it goes when there is a result. Left standing, the last two
        # lines of the panel read "to put this back: bws start Spooler" and then "bws stop Spooler" -
        # the second describing something that already happened, in the place a reader scanning up
        # from the button lands first.
        Name = 'plan panel: the command that would ask for the same thing goes when something was asked'
        File = 'src\Bws.Gui\ViewModels\Planned.Sections.cs'
        # Re-anchored 2026-09-29: HasCommands became the block the section binds (W6).
        From = 'public CommandBlock CommandBlock => _run is null ? _commandBlock : CommandBlock.None;'
        To = 'public CommandBlock CommandBlock => _commandBlock;'
        Test = 'PlanReportGuards.The_command_that_would_ask_for_the_same_thing_goes_away_once_something_was_asked'
    },
    @{
        # THE ONE BLOCK BACKLOG 203 MISSED, and it was the only one. Every other conditional part of
        # this panel took its heading off the screen with it - this had no visibility of its own, so
        # an empty sentence stood in the tree always, carrying its margin. That is the gap between
        # the state sentence and the first section in the owner's first screenshot.
        Name = 'plan panel: nothing in the way means no line reserved for saying so'
        File = 'src\Bws.Gui\ViewModels\Planned.Sections.cs'
        From = 'public bool HasBlocked => Blocked.Length > 0;'
        To = 'public bool HasBlocked => true;'
        Test = 'CarryingGuards.Nothing_in_the_way_means_no_line_reserved_for_saying_so'
    },
    @{
        # BACKLOG 202, open since Krok 1 of packet 2: the most critical part of a plan - the order a
        # cascade comes down in - was held by three assertions and nobody had checked they could
        # fail. Cheaper now than it was, because the algorithm moved into DependentsFirst and both
        # the cascade and the selection ask it, so one mutation covers both.
        #
        # The candidate the backlog names. With All, a name is ready only once everything inside the
        # set that depends on it has gone; with Any over an empty set the answer is always false, so
        # nothing is ever ready and every name falls through to the cycle escape below - which hands
        # back the input order. That is precisely the order PlanBuilder writes about as "right by
        # accident and wrong on somebody else's machine at the worst moment".
        Name = 'plan: a cascade comes down dependants first, worked out rather than as it arrived'
        File = 'src\Bws.Core\Planning\DependentsFirst.cs'
        From = 'blockedBy[name].All(other =>'
        To = 'blockedBy[name].Any(other =>'
        Test = 'BulkPlanTests.An_entry_something_else_in_the_selection_needs_is_dealt_with_last'
    },
    @{
        # `docs/10` trap 8 in a third place, and THE ONE PROPERTY HERE THAT IS NOT A DEFAULT BEING
        # RESTORED. Focusable and IsTabStop are what a ContentControl arrives with, and this registry
        # already proves that shape twice - on the row of filters and on the details panel - so a
        # third entry for them would be paying four seconds a run to prove a mechanism nothing has
        # changed. They are covered by the guard, which reads all three, and only these two are
        # covered by an entry: that is the honest split rather than a silent one.
        #
        # Hit testing is different because the StackPanel this replaced set it BY HAND. A UserControl
        # arrives hit testable, so losing this line does not put a tab stop somewhere harmless - it
        # makes an invisible panel swallow clicks on their way to the rows, with nothing on the
        # screen to suggest where they went.
        Name = 'empty state: it takes no clicks on their way to the rows beneath it'
        File = 'src\Bws.Gui\EmptyState.xaml'
        From = 'IsHitTestVisible="False"'
        To = 'IsHitTestVisible="True"'
        Test = 'EmptyStateViewGuards.It_takes_neither_the_keyboard_nor_the_mouse'
    },
    @{
        # `A10` reads the machine every second. With the negation gone, a window that has already
        # read calls every one of those readings a first look - so the empty state comes off the
        # screen and goes back once a second, a sentence somebody is reading replaced by a progress
        # message.
        #
        # RE-ANCHORED 2026-08-18, backlog 196. It used to break `reading && everything == 0` in
        # ListState.cs, and that condition is gone: the count was a proxy for "has any reading ever
        # finished" and was wrong on the machine that hands over nothing. The question is now asked
        # of a real bit, one caller up.
        #
        # The NEGATION rather than the field, because dropping `!_everRead` altogether would leave
        # that field assigned and never read - CS0414, and this build treats warnings as errors, so
        # the mutation would fail to compile and report itself as uncaught while proving nothing.
        Name = 'window: a refresh does not take the empty state off the screen'
        File = 'src\Bws.Gui\ViewModels\Readings.cs'
        From = 'internal bool FirstLook => _reading && !_everRead;'
        To = 'internal bool FirstLook => _reading && _everRead;'
        Test = 'EmptyStateTests.A_refresh_does_not_take_the_empty_state_off_the_screen'
    },
    @{
        # The other half of the same complaint. This runs on every keystroke and every tick, so
        # announcing unconditionally re-evaluates the triggers hanging off three properties about
        # eighty thousand times an hour to say nothing moved.
        Name = 'window: what the list is doing is announced only when it moved'
        File = 'src\Bws.Gui\ViewModels\Says.cs'
        #
        # NOT `if (false)`, AND THAT COST ONE MISSED RUN. Warnings are errors in this tree, so an
        # unreachable early return does not compile and the runner reports a mutation that never
        # applied as one nothing caught - which reads exactly like a guard that does not work.
        # Turning the comparison round compiles and is false in the case the test arranges.
        From = 'if (_list == list && _narrowed == narrowed)'
        To = 'if (_list != list && _narrowed != narrowed)'
        Test = 'EmptyStateTests.A_refresh_does_not_take_the_empty_state_off_the_screen'
    },
    @{
        # A term holds what it MATCHES and what it was WRITTEN as, and they answer different
        # questions - the values decide which entries come back, the spellings decide which chip is
        # lit. Folding the values while dropping the spellings gave a query that filtered on three
        # start types and admitted to one, which nothing but a clickable filter could see.
        Name = 'query: a repeated field keeps every spelling, not just the first'
        File = 'src\Bws.Core\Querying\QueryParser.cs'
        From = 'Written = [.. folded[position].Written, .. term.Written]'
        To = 'Written = folded[position].Written'
        Test = 'QueryMemberTests.Every_value_of_a_repeated_field_is_carried_rather_than_the_first'
    },
    @{
        Name = 'columns: no two of them answer to the same name'
        File = 'src\Bws.Gui\ViewModels\Columns.cs'
        From = 'Id = "securityDescriptor",'
        To = 'Id = "status",'
        Test = 'ColumnGuards.Every_column_is_offered_and_each_is_named_exactly_once'
    },
    @{
        # The row and the catalogue have to be the same answer. Two ways to reach one cell drift
        # the first time somebody changes one of them, and nothing about a green build notices.
        Name = 'columns: a row says through a column what the column reads'
        File = 'src\Bws.Gui\ViewModels\EntryRow.cs'
        From = 'Columns.Of(column) is { } known ? known.Reads(_entry) : column;'
        To = 'Columns.Of(column) is { } known ? string.Empty : column;'
        Test = 'ColumnGuards.A_row_says_through_a_column_exactly_what_that_column_reads'
    },
    @{
        # Complaint 6 of the eleven, in the one place it was never fixed. Photographed before this
        # setter existed: "Required priv", cut in the middle of a word with nothing to say so.
        # Anchored on the heading's own block rather than on the attribute, which appears in
        # CellText as well - one entry breaking both would go red for the right reason by accident
        # and would stop saying anything the day the cells were guarded separately.
        Name = 'columns: a heading too long for its column gives way with an ellipsis'
        File = 'src\Bws.Gui\Themes\List.xaml'
        # RE-ANCHORED 2026-09-25: the heading's TextBlock names ColumnHeadingText now, between Text
        # and the trimming.
        From = @'
                    <TextBlock Text="{Binding}"
                               Style="{StaticResource ColumnHeadingText}"
                               TextTrimming="CharacterEllipsis"
                               ToolTip="{Binding}" />
'@
        To = @'
                    <TextBlock Text="{Binding}" Style="{StaticResource ColumnHeadingText}" ToolTip="{Binding}" />
'@
        Test = 'WindowGuards.A_column_heading_gives_way_with_an_ellipsis_like_every_cell_does'
    },
    @{
        # THE CASE THE SYSTEM REPORTS BY NOT FAILING. The manager resolves a description out of a
        # binary's resources, and when the resource is missing the call SUCCEEDS and hands the
        # indirection back. Flattening that to absent would say "this entry has no description"
        # about eight entries of 809 that have one nobody could read - and would compare clean in a
        # snapshot against a machine where it resolved.
        Name = 'description: an unresolved indirection is a failed read rather than an absent one'
        File = 'src\Bws.Core\ServiceDescription.cs'
        From = "        ['@', ..] => Reading<string>.Denied(ResourceNotFound, ManagerTerms.Describe(ResourceNotFound)),"
        To = "        ['@', ..] => Reading<string>.Absent(),"
        Test = 'ServiceDescriptionTests.An_unresolved_indirection_is_a_failed_read_rather_than_a_description'
    },
    @{
        # A LATENT FAULT THE DESCRIPTION EXPOSED. A wildcard is anchored at both ends and a dot does
        # not cross a newline by default, so *word* against a two-line value silently matched
        # NOTHING. No field could carry a line break until the description arrived, and two entries
        # of 819 on this machine have one.
        Name = 'query: a wildcard reaches past a line break in a value'
        # Re-anchored 2026-08-26: QueryPatterns moved out of QueryValues.cs into its own file
        # when the size ratchet fired on a comment added there.
        File = 'src\Bws.Core\Querying\QueryPatterns.cs'
        From = 'Shared | RegexOptions.NonBacktracking | RegexOptions.Singleline,'
        To = 'Shared | RegexOptions.NonBacktracking,'
        Test = 'QueryOverSpecimensTests.A_wildcard_reaches_past_a_line_break_in_a_value'
    },
    @{
        # The owner's decision of 2026-08-12 put back to the one it reversed, which is one word.
        # Photographed with all seventeen columns on before it changed: nothing past the right edge
        # and SEVEN columns crushed to twenty pixels, each heading a single full stop over a column
        # of sliced dots. A window that shows seven columns saying nothing is rule 8 of the notes.
        Name = 'window: the list scrolls sideways rather than crushing its columns'
        File = 'src\Bws.Gui\MainWindow.xaml'
        From = 'HorizontalScrollBarVisibility="Auto"'
        To = 'HorizontalScrollBarVisibility="Disabled"'
        Test = 'WindowGuards.The_list_scrolls_sideways_rather_than_crushing_its_columns'
    },
    @{
        # THE LINE THAT ACTUALLY MADE SEVENTEEN COLUMNS READABLE, put back to the version that did
        # not. Turning the scrollbar on was not enough: measured on the real window with sideways
        # scrolling already working, seven columns still sat at twenty pixels, because a width is a
        # request and DataGrid takes room back down to its own MinColumnWidth. The mutation is the
        # sentence that used to stand in that file - a floor on a fixed column would be a second
        # number deciding the same width - which is true of a different number and false of this one.
        Name = 'columns: a fixed column may not be squeezed below the width it was sized for'
        File = 'src\Bws.Gui\ListColumns.cs'
        # Re-anchored 2026-08-13 at S6d3: the question became "is this an absolute number" rather
        # than "is this a share", because a width can now come out of a file and a hand written
        # Auto has no pixel value of its own to use as a floor.
        From = @'
        built.MinWidth = built.Width.IsAbsolute
            ? built.Width.Value
            : (double)grid.FindResource("ColumnFloor");
'@
        To = @'
        if (!built.Width.IsAbsolute)
        {
            built.MinWidth = (double)grid.FindResource("ColumnFloor");
        }
'@
        Test = 'WindowGuards.No_column_can_be_squeezed_below_the_width_it_was_sized_for'
    },
    @{
        # THE FAULT A LITERAL ONE HAS, written out as the mutation. WPF freezes the first N of the
        # display order and a collapsed column keeps its place in it - measured on the built window,
        # where the name column turned off stayed frozen while collapsed and the leftmost column
        # actually on screen did not. The freeze then scrolls away and the failure looks exactly like
        # nothing having been frozen at all.
        Name = 'columns: the frozen count reaches past columns that are turned off'
        File = 'src\Bws.Gui\ListColumns.cs'
        From = '                grid.FrozenColumnCount = position + 1;'
        To = '                grid.FrozenColumnCount = 1;'
        Test = 'WindowGuards.The_frozen_column_follows_whichever_one_is_leftmost_on_screen'
    },
    @{
        # THE OTHER HALF OF THE SAME GUARD, and it is the mistake this file's author actually made.
        # The collection's order is the display order until somebody drags a heading, so this
        # mutation is invisible to any check that never reorders anything - which is what makes the
        # reordering half of that guard worth its lines.
        Name = 'columns: the frozen column follows a dragged heading'
        File = 'src\Bws.Gui\ListColumns.cs'
        From = 'if (grid.ColumnFromDisplayIndex(position).Visibility == Visibility.Visible)'
        To = 'if (grid.Columns[position].Visibility == Visibility.Visible)'
        Test = 'WindowGuards.The_frozen_column_follows_whichever_one_is_leftmost_on_screen'
    },
    @{
        Name = 'window: an appearance value written into a view is refused'
        File = 'src\Bws.Gui\MainWindow.xaml'
        From = '<Grid Margin="{StaticResource PadPanel}">'
        To = '<Grid Margin="7">'
        Test = 'AppearanceGuards.No_view_invents_an_appearance_value_of_its_own'
    },
    @{
        Name = 'window: a failed reading is reported instead of taking it down'
        File = 'src\Bws.Gui\ViewModels\Readings.cs'
        From = 'Incomplete = true;'
        To = 'Incomplete = false;'
        Test = 'MainViewModelTests.A_manager_that_will_not_open_is_reported_instead_of_taking_the_window_down'
    },
    @{
        # A broad catch appearing somewhere that never argued for one. The list is the
        # deliberate act, and this proves the list is read rather than admired.
        Name = 'broad catch: a new one has to be argued for'
        File = 'src\Bws.Core\SecondPass.cs'
        From = '        var files = Distinct(entries);'
        To = '#pragma warning disable CA1031' + [char]10 + '#pragma warning restore CA1031' + [char]10 + '        var files = Distinct(entries);'
        Test = 'BroadCatchGuards.Every_broad_catch_is_in_a_place_that_argued_for_one'
    },
    @{
        Name = 'plan: a switch on the wrong verb is refused'
        # Followed the belonging table to OptionSurface.cs on 2026-08-02, when adding help and
        # version pushed CommandLine.cs past the size ratchet and the table moved out whole.
        # The registry said STALE rather than passing, which is the verdict that earns this
        # tool its keep: an entry anchored on text that no longer exists proves nothing while
        # looking exactly like an entry that does.
        File = 'src\Bws.Cli\OptionSurface.cs'
        From = '("--note", [CommandKind.SnapshotCreate]),'
        To = '("--note", [CommandKind.SnapshotCreate, CommandKind.List]),'
        Test = 'UsageContractTests'
    },
    @{
        # Rewritten 2026-08-02: the three entries here went STALE and BROKEN the moment S6c
        # reshaped the view model, which is the tool doing its job on its own author. A stale
        # entry proves nothing while looking like it did, so the run that reported it was worth
        # more than the run before it.
        #
        # AND AGAIN ON 2026-08-25, THE SAME LINE AND THE SAME MECHANISM. `A11` put a fold between
        # the narrowing and the list, so the anchor became `Show(rolled.Rows)` and stale-check
        # named it before the run rather than after - which is what that script is for.
        Name = 'search: the box actually narrows the list'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.cs'
        From = '        Show(rolled.Rows);'
        To = '        Show([.. everything]);'
        Test = 'MainViewModelTests.Typing_a_query_narrows_the_list_and_the_count_says_out_of_how_many'
    },
    @{
        # The sentence "a query with a mistake filters nothing" means the opposite here to what
        # it means in a terminal. This is the half that only the window can get wrong.
        Name = 'search: a mistyped query leaves the list where it was'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.cs'
        # Re-anchored 2026-09-23: the call goes through MainViewModel.Checking.cs now, which also
        # tells the box its error changed.
        # The mutant changed 2026-10-05 (G-9, package I): Apply goes on past a mistake with the last
        # query that could be read, so clearing the rows here was refilled two lines later and this
        # entry would have passed for nothing. What breaks "the list stays" now is forgetting that
        # query - an empty one answers with everything.
        From = '            AboutTheQuery(string.Join(" ", parsed.Problems.Select(QueryMessages.Of)));'
        To = '            AboutTheQuery(string.Join(" ", parsed.Problems.Select(QueryMessages.Of)));' + [char]10 + '            _query = QueryParser.Parse(null).Query!;'
        Test = 'MainViewModelTests.A_query_with_a_mistake_leaves_the_list_alone_and_says_what_is_wrong'
    },
    @{
        # Nobody looked, versus the machine would not let us. Two states this project spends
        # most of its rules keeping apart, arriving from the query engine as one number.
        # Followed Admissions into Sentences.cs on 2026-08-02, same split, same reason.
        # RE-ANCHORED 2026-09-23: UX-GUI-001 widened the condition to (needs & ~have), and the
        # mutation is still the same one - drop the guard entirely.
        # RE-ANCHORED 2026-10-06 (security report S-6): the counts arrive as one Narrowed and the
        # three reservation sentences moved into Sentences.Unjudged, which takes needs & ~have.
        Name = 'search: an unread family is not reported as a refusal'
        File = 'src\Bws.Gui\ViewModels\Sentences.cs'
        From = 'if (answer.Unreadable > 0 && unread == ExtraRead.None)'
        To = 'if (answer.Unreadable > 0)'
        Test = 'MainViewModelTests.Asking_about_signatures_says_nobody_read_them_rather_than_that_they_were_refused'
    },
    @{
        # UX-GUI-001, 2026-09-23: the guard above held the sentence back for every query needing a
        # second phase family, EVEN AFTER THE PASS HAD RUN - memory:>100MB said "nothing matches"
        # on a window without rights while 103 running services had refused their memory. The
        # mutation puts the old condition back.
        # RE-ANCHORED 2026-10-06: the condition now arrives as Unjudged's argument, so the old one
        # goes back in there - handing it the needs instead of what is still unread.
        Name = 'search: a refusal the pass brought back is admitted'
        File = 'src\Bws.Gui\ViewModels\Sentences.cs'
        From = 'notes.AddRange(Unjudged(answer, unread: needs & ~have));'
        To = 'notes.AddRange(Unjudged(answer, unread: needs));'
        Test = 'AdmissionTests.A_refusal_the_memory_pass_brought_back_is_admitted_beside_the_answer'
    },
    @{
        # The boundary of the same repair: while ONE needed family is still unread the refusal
        # sentence stays back, because the count cannot tell the two states apart. Swapping the
        # operands asks about what was read beyond the need instead, and lets it through.
        # RE-ANCHORED 2026-10-06, the same move: the operands are swapped where they now stand.
        Name = 'search: a family still unread keeps the refusal sentence back'
        File = 'src\Bws.Gui\ViewModels\Sentences.cs'
        From = 'notes.AddRange(Unjudged(answer, unread: needs & ~have));'
        To = 'notes.AddRange(Unjudged(answer, unread: have & ~needs));'
        Test = 'AdmissionTests.A_family_still_unread_keeps_the_refusal_sentence_back'
    },
    @{
        # UX-GUI-011, 2026-09-23: status:r put "status has no value r" on the screen on the
        # keystroke after status:, while the list under the box offered running. The mutation
        # switches the tolerance off.
        Name = 'typing tolerance: the start of a value is passed over while typed'
        File = 'src\Bws.Core\Querying\QueryParser.cs'
        From = '                && StillBeingTyped(query, member, ReferenceEquals(member, members[^1]), problems, before))'
        To = '                && problems.Count < 0)'
        Test = 'QueryParserTests.The_start_of_a_value_in_the_member_being_typed_is_not_an_error'
    },
    @{
        # A member somebody has moved on from is finished - status:r start:manual is a mistake.
        Name = 'typing tolerance: only the member being typed is tolerated'
        File = 'src\Bws.Core\Querying\QueryParser.cs'
        From = '        if (!last || char.IsWhiteSpace(query[^1]) || problems.Count - before != 1)'
        To = '        if (char.IsWhiteSpace(query[^1]) || problems.Count - before != 1)'
        Test = 'QueryParserTests.The_start_of_a_value_is_still_a_mistake_everywhere_else'
    },
    @{
        # A review of PR 10, 2026-09-23: the scanner takes the quotes away, so status:"r" reached the
        # tolerance as status:r and was passed over - a value finished by closing its quotes,
        # dropped as though it were half typed. The mutation forgets the closing quote.
        # Re-anchored 2026-10-05: the quote moved to a condition of its own, below the one for a
        # pattern still being typed, because a quote inside a pattern closes nothing.
        Name = 'typing tolerance: a value closed by a quote is finished'
        File = 'src\Bws.Core\Querying\QueryParser.cs'
        From = '        if (query[^1] == ''"'''
        To = '        if (query[^1] == ''\0'''
        Test = 'QueryParserTests.The_start_of_a_value_is_still_a_mistake_everywhere_else'
    },
    @{
        # WITHDRAWN ONCE ON 2026-09-23 AND BACK THE SAME DAY with the case it really holds. The
        # first entry claimed the condition kept status:running,st a mistake - it does not, that
        # member reads `running` and is a term before the tolerance is asked. What it holds is
        # status:st, - a value somebody moved on from with a comma, which is the start of stopped
        # and would otherwise be passed over. The mutation loosens "the whole of what follows the
        # colon" to "somewhere in the member".
        #
        # THE FIRST VERSION'S MISSED WAS THE TRUTH, NOT A RUNNER FAULT - settled 2026-09-23. It
        # deleted the only use of `member`, and a strict build refuses that (IDE0060 is a warning in
        # .editorconfig, made an error by TreatWarningsAsErrors in Directory.Build.props). But this
        # runner builds the mutated tree with TreatWarningsAsErrors=false (Test-Mutation), so it
        # compiled, the test ran on it, and stayed green because the test had no status:st, case.
        Name = 'typing tolerance: a value followed by a comma is finished'
        File = 'src\Bws.Core\Querying\QueryParser.cs'
        From = '            || !member.Text.EndsWith(":" + problem.Text, StringComparison.Ordinal))'
        To = '            || !member.Text.Contains(problem.Text, StringComparison.Ordinal))'
        Test = 'QueryParserTests.The_start_of_a_value_is_still_a_mistake_everywhere_else'
    },
    # THE LINE UNDER THE BOX AND THE LINE UNDER THE LIST - UX-GUI-002 and 009, 2026-09-23. The
    # sentences about the question moved under the box, the rights and the footing stayed at the
    # foot, and the box learnt a wrong state. One entry per rule, each on a value, not a constant.
    @{
        # The list stays when the query has a mistake - that is a decision a test holds - so the
        # sentence has to say the list is the previous answer, or the window pretends it is current.
        Name = 'answer line: a mistake says the list is the previous answer'
        File = 'src\Bws.Gui\ViewModels\Says.cs'
        From = '            : problem + " " + Texts.Of("gui.query.listIsPrevious");'
        To = '            : problem;'
        Test = 'AnswerLineTests.A_mistake_takes_the_line_under_the_box'
    },
    @{
        # A query that does not read has no answer of its own to qualify, so its mistake takes the
        # line. The mutation lets a reservation left from the previous answer win instead.
        Name = 'answer line: a mistake wins the line over a reservation'
        File = 'src\Bws.Gui\ViewModels\Says.cs'
        From = '    public string AnswerLine => _queryProblem.Length > 0 ? _queryProblem : _admitted.Reservations;'
        To = '    public string AnswerLine => _admitted.Reservations.Length > 0 ? _admitted.Reservations : _queryProblem;'
        Test = 'AnswerLineTests.A_mistake_takes_the_line_under_the_box'
    },
    @{
        # The reservations are about the question, so they stand under the box and nowhere at the
        # foot. The mutation puts them back into the line under the list.
        Name = 'answer line: a reservation stands under the box and not under the list'
        File = 'src\Bws.Gui\ViewModels\Sentences.cs'
        From = '    public string Line => RightsPiece + Footing;'
        To = '    public string Line => RightsPiece + Reservations + Footing;'
        Test = 'AnswerLineTests.Rights_reservations_and_footing_are_three_pieces_of_one_admission'
    },
    @{
        # What lights the red edge: the box's binding asks for the errors of QueryText. The
        # mutation hands them to another property, so the box never hears of its mistake.
        Name = 'answer line: the box is wrong while its query has a mistake'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Checking.cs'
        From = '        property == nameof(QueryText) && Says.QueryProblem.Length > 0 ? [Says.QueryProblem] : [];'
        To = '        property == nameof(Scope) && Says.QueryProblem.Length > 0 ? [Says.QueryProblem] : [];'
        Test = 'AnswerLineTests.The_box_is_wrong_exactly_while_its_query_has_a_mistake'
    },
    @{
        # Told only when the sentence changed - the pass runs on every tick, and a binding told
        # its errors changed takes the error down and puts it back. The mutation tells it always.
        Name = 'answer line: the box is told about its mistake only when it changed'
        # RE-ANCHORED 2026-09-25: the query's text left the call with backlog 461.
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Checking.cs'
        From = '        if (Says.AboutTheQuery(problem))'
        To = '        Says.AboutTheQuery(problem);'
        Test = 'AnswerLineTests.The_box_is_told_about_its_mistake_once_and_not_on_every_pass'
    },
    @{
        # THE VIEWS OF THE SAME PACKAGE. The search box wears the reporting style - without it the
        # model says the box is wrong and no edge on the screen shows it. Proved off pixels.
        Name = 'answer line: the search box wears the red edge while its query is wrong'
        #
        # RE-ANCHORED 2026-09-25: the field's FRAME draws the red edge now, not the text box's error
        # template - SearchField.xaml. Turned round, the edge is red on a query that reads.
        File = 'src\Bws.Gui\Themes\SearchField.xaml'
        From = @'
            <DataTrigger Binding="{Binding Says.AnswerLineIsProblem}" Value="True">
                <Setter Property="BorderBrush" Value="{StaticResource MeaningRejected}" />
'@
        To = @'
            <DataTrigger Binding="{Binding Says.AnswerLineIsProblem}" Value="False">
                <Setter Property="BorderBrush" Value="{StaticResource MeaningRejected}" />
'@
        Test = 'AnswerLineGuards.The_search_box_wears_the_problem_colour_on_its_edge_while_its_query_is_wrong'
    },
    @{
        # The line under the box reads the answer line, not one of its halves - bound to the
        # reservations alone, a mistake would never reach the screen.
        Name = 'answer line: the line under the box reads the whole answer line'
        File = 'src\Bws.Gui\SearchRow.xaml'
        From = '                   Text="{Binding Says.AnswerLine}"'
        To = '                   Text="{Binding Says.Reservations}"'
        Test = 'AnswerLineGuards.The_line_under_the_box_says_a_mistake_in_red_and_a_reservation_in_the_notice_colour'
    },
    @{
        # A mistake is red and a reservation is not. The mutation turns the colours round.
        Name = 'answer line: a mistake under the box is red and a reservation is not'
        #
        # RE-ANCHORED 2026-09-25: the sentence stands at the right end of the field, styled by
        # SearchMessageText in SearchField.xaml.
        File = 'src\Bws.Gui\Themes\SearchField.xaml'
        From = @'
            <DataTrigger Binding="{Binding Says.AnswerLineIsProblem}" Value="True">
                <Setter Property="Foreground" Value="{StaticResource MeaningRejected}" />
'@
        To = @'
            <DataTrigger Binding="{Binding Says.AnswerLineIsProblem}" Value="False">
                <Setter Property="Foreground" Value="{StaticResource MeaningRejected}" />
'@
        Test = 'AnswerLineGuards.The_line_under_the_box_says_a_mistake_in_red_and_a_reservation_in_the_notice_colour'
    },
    @{
        # The sentence is shown while it has words. Never matching the empty case, it would stand
        # in an empty field. (Until 2026-09-25 the line took its room from AnswerLineShown, which
        # went with backlog 461.)
        Name = 'answer line: the line under the box is shown while the model says so'
        #
        # RE-ANCHORED 2026-09-25: the sentence in the field is there while it has words, and the
        # room it took under the box is gone. Never matching, it would stand in an empty box.
        File = 'src\Bws.Gui\Themes\SearchField.xaml'
        From = @'
        <Setter Property="TextWrapping" Value="NoWrap" />
        <Style.Triggers>
            <DataTrigger Binding="{Binding Says.AnswerLine}" Value="">
'@
        To = @'
        <Setter Property="TextWrapping" Value="NoWrap" />
        <Style.Triggers>
            <DataTrigger Binding="{Binding Says.AnswerLine}" Value="never">
'@
        Test = 'AnswerLineGuards.The_line_under_the_box_says_a_mistake_in_red_and_a_reservation_in_the_notice_colour'
    },
    @{
        # UX-GUI-009 at the foot of the window: the rights sentence is red on its own. Without its
        # colour it takes the line's, and a session without rights stops looking like one.
        Name = 'rights: the sentence about rights is red at the foot of the window'
        File = 'src\Bws.Gui\StatusRow.xaml'
        From = '<Run Text="{Binding Says.NoticeRights, Mode=OneWay}" Foreground="{StaticResource MeaningRejected}" />'
        To = '<Run Text="{Binding Says.NoticeRights, Mode=OneWay}" />'
        Test = 'AnswerLineGuards.Only_the_sentence_about_rights_is_red_at_the_foot_of_the_window'
    },
    @{
        # And ONLY that sentence. The mutation puts back the trigger that coloured the whole line
        # red while the session had no rights - the decision struck out on 2026-09-23, whose reason
        # was not true of the sentence about folded copies.
        Name = 'rights: the rest of the line at the foot is not red'
        File = 'src\Bws.Gui\StatusRow.xaml'
        From = '                            <DataTrigger Binding="{Binding Says.NoticeLine}" Value="">'
        To = '                            <DataTrigger Binding="{Binding Says.NotElevated}" Value="True"><Setter Property="Foreground" Value="{StaticResource MeaningRejected}" /></DataTrigger>
                            <DataTrigger Binding="{Binding Says.NoticeLine}" Value="">'
        Test = 'AnswerLineGuards.Only_the_sentence_about_rights_is_red_at_the_foot_of_the_window'
    },
    @{
        # The remainder of UX-GUI-001: with entries nobody could judge, the middle of an empty
        # list says "nothing that could be checked" ("could be read" until the review of PR #11).
        # The mutation keeps only the other count.
        # RE-ANCHORED 2026-10-06 (security report S-6): a third count joined the line.
        Name = 'empty list: nothing that could be checked, when entries could not be read'
        File = 'src\Bws.Gui\ViewModels\Says.cs'
        From = '        _partial = answer.Unreadable > 0 || answer.TooCostly > 0 || answer.Unvouched > 0;'
        To = '        _partial = answer.TooCostly > 0 || answer.Unvouched > 0;'
        Test = 'EmptyStateTests.Nothing_that_could_be_checked_matches_when_some_entries_could_not_be_read'
    },
    @{
        # The review of PR #11: an entry the expression ran out of time on was never checked
        # either, and the middle said "nothing matches". The mutation drops that count again.
        # RE-ANCHORED 2026-10-06 (security report S-6): a third count joined the line.
        Name = 'empty list: nothing that could be checked, when the expression ran out of time'
        File = 'src\Bws.Gui\ViewModels\Says.cs'
        From = '        _partial = answer.Unreadable > 0 || answer.TooCostly > 0 || answer.Unvouched > 0;'
        To = '        _partial = answer.Unreadable > 0 || answer.Unvouched > 0;'
        Test = 'EmptyStateTests.Nothing_that_could_be_checked_matches_when_the_expression_ran_out_of_time'
    },
    @{
        # Rule 8: with the box empty a shown column's reading note stands in the field's sentence
        # or nowhere. RE-POINTED 2026-09-25 with backlog 461 - it mutated AnswerLineShown, which is
        # gone, and the half it held is the sentence itself. The mutation says nothing but mistakes.
        Name = 'answer line: the line under an empty box still speaks while it has something to say'
        File = 'src\Bws.Gui\ViewModels\Says.cs'
        From = '    public string AnswerLine => _queryProblem.Length > 0 ? _queryProblem : _admitted.Reservations;'
        To = '    public string AnswerLine => _queryProblem;'
        Test = 'AnswerLineTests.A_shown_column_still_being_read_is_said_under_an_empty_box'
    },
    @{
        Name = 'search: a query can say which member it carries'
        File = 'src\Bws.Core\Querying\QueryParser.cs'
        From = ': new QueryTerm { Field = field, Values = values, Negated = negated, Written = written };'
        To = ': new QueryTerm { Field = field, Values = values, Negated = negated };'
        Test = 'QueryForTheWindowTests.A_query_says_whether_it_excludes_a_value'
    },
    @{
        # BOTH OF THESE WENT STALE ON 2026-08-11 AND WERE REPOINTED RATHER THAN DELETED, which is
        # the interesting half. They named the regex switch, and the switch was removed the day
        # before - but the BEHAVIOUR each one guarded outlived it, because removing the switch
        # turned out to be a deletion rather than a change: QueryValueReader had been reading
        # slashes all along, for values and for free search alike. So the entries follow the rule
        # to where it actually lives instead of leaving two holes behind a green run.
        #
        # The mutation keeps the slashes in the pattern instead of stripping them, so /^spooler$/
        # compiles - it is a valid expression matching literal slashes - and simply stops matching.
        # A pattern that would not compile is refused by a different test, which stays green, so
        # this entry breaks the reading rather than the error path.
        # THE FOUR BELOW CAME WITH THE CLICKABLE FILTERS, 2026-08-11, S6d1.
        Name = 'filters: every chip stands for a member the language knows'
        File = 'src\Bws.Gui\ViewModels\FilterChips.cs'
        From = 'new FilterChip("gui.filter.delayed", "start", "delayed"'
        To = 'new FilterChip("gui.filter.delayed", "start", "delayd"'
        Test = 'FilterChipTests.Every_chip_stands_for_a_member_the_language_knows'
    },
    @{
        # UX-GUI-002, 2026-09-23: a mistake anywhere in the line meant no member counted, so the
        # chip for Running went dark over status:running pid:abc and a click wrote it twice. The
        # mutation asks each member about the other side of the sign, which compiles and leaves
        # nothing unreachable - the whole line is still asked first, so only a line with a mistake
        # in it can tell.
        Name = 'filters: a member beside a mistake is still carried'
        # Re-anchored 2026-09-28: the member-by-member answer moved into QueryAsTyped, which parses
        # the members once for every question (G-4).
        File = 'src\Bws.Core\Querying\QueryAsTyped.cs'
        From = '        return Members().Any(member => member?.Carries(field, value, negated) ?? false);'
        To = '        return Members().Any(member => member?.Carries(field, value, !negated) ?? false);'
        Test = 'QueryMemberTests.A_member_is_carried_even_when_another_member_has_a_mistake'
    },
    @{
        # UX-GUI-002, 2026-09-23: the path for a query that does not parse left before the chips
        # were asked, so Running and Manual stayed lit over stat:runing. Asked through the
        # notification, because IsOn is worked out on every read - pitfall 4 in this file's header.
        Name = 'filters: a query with a mistake tells every chip to look again'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.cs'
        # Re-anchored 2026-10-05 (G-9, package I): Apply no longer leaves early on a mistake, so the
        # one Rethink at its end is the one every query - read or not - goes through.
        From = '        // when the list answers an earlier one - UX-GUI-002, `stat:runing` with Running still lit.
        _filters.Rethink();'
        To = '        // when the list answers an earlier one - UX-GUI-002, `stat:runing` with Running still lit.'
        Test = 'FilterChipTests.A_query_with_a_mistake_tells_every_chip_to_look_again'
    },
    @{
        # A chip that removed only the first copy would turn off and leave the list narrowed,
        # which is a control visibly not working. Somebody gets two copies by typing the member
        # and then clicking the chip for it, which is nobody's mistake.
        Name = 'filters: turning a chip off takes every copy of its member out'
        File = 'src\Bws.Core\Querying\QueryMembers.cs'
        From = '        for (var index = cuts.Count - 1; index >= 0; index--)'
        To = '        for (var index = cuts.Count - 1; index >= cuts.Count - 1; index--)'
        Test = 'QueryMemberTests.Every_copy_of_the_member_comes_out_not_the_first'
    },
    @{
        # The cut takes the gap it made and nothing else. Swallowing whitespace on both sides
        # reformats a line somebody is looking at, and the promise is that the rest of the text
        # comes back as it was typed.
        Name = 'filters: cutting a member leaves the spacing it did not touch'
        File = 'src\Bws.Core\Querying\QueryMembers.cs'
        From = '        while (cut.Whole && start > 0 && char.IsWhiteSpace(left[start - 1]))'
        To = '        while (cut.Whole && start > 0 && char.IsWhiteSpace(left[start - 1]) && false)'
        Test = 'QueryMemberTests.A_member_at_either_end_comes_out_without_leaving_a_gap'
    },
    @{
        # Backlog 588: a line ending in a lone backslash took the space a chip wrote into the word,
        # so the member was never a member and a second click wrote it again. Doubling the lone
        # backslash keeps the same literal backslash and gives the space back its meaning.
        Name = 'filters: a chip after a lone backslash doubles it'
        File = 'src\Bws.Core\Querying\QueryMembers.cs'
        From = '(backslashes % 2 == 1 ? "\\ " : " ")'
        To = '(backslashes < 0 ? "\\ " : " ")'
        Test = 'QueryMemberTests.A_member_after_a_lone_backslash_is_a_member_and_the_word_before_it_is_unchanged'
    },
    @{
        # The control for the one above. Doubling every trailing backslash gives C:\\ a third,
        # and the third disarms the space all over again.
        Name = 'filters: a chip after paired backslashes adds only the gap'
        File = 'src\Bws.Core\Querying\QueryMembers.cs'
        From = '(backslashes % 2 == 1 ? "\\ " : " ")'
        To = '(backslashes > 0 ? "\\ " : " ")'
        Test = 'QueryMemberTests.A_member_after_paired_backslashes_adds_only_the_gap'
    },
    @{
        # Every chip reads the query again after any edit, not the one that was clicked. Without
        # it a chip sits lit over a query that has stopped saying anything about it - which is
        # the state A5 exists to make impossible, and it looks like the filter is still applied.
        #
        # THIS ENTRY CAME BACK MISSED ON ITS FIRST RUN AND THE GUARD WAS WRONG, NOT THE MUTATION.
        # It named Clearing_the_box_turns_every_chip_off, which asks IsOn directly - and IsOn is
        # worked out on every read, so it is right whether or not anybody was told. What the loop
        # actually carries is the notification, which only a binding sees, so the test that can
        # fail is the one watching PropertyChanged.
        Name = 'filters: one edit makes every chip look at the query again'
        File = 'src\Bws.Gui\ViewModels\FilterChips.cs'
        From = '        foreach (var chip in _chips)'
        To = '        foreach (var chip in System.Linq.Enumerable.Empty<FilterChip>())'
        Test = 'FilterChipTests.An_edit_anywhere_tells_every_chip_to_look_again'
    },
    @{
        Name = 'search: text between slashes is a pattern and nothing else is'
        File = 'src\Bws.Core\Querying\QueryValueReader.cs'
        From = '        var pattern = value.WithEscapes(1, value.Length - 2);'
        To = '        var pattern = value.WithEscapes(0, value.Length);'
        Test = 'QueryForTheWindowTests.A_bare_word_between_marks_is_an_expression'
    },
    @{
        # Pointed at the parity check on purpose. The unit tests would catch a broken reading too,
        # and the claim being proved is the other one: that one query asked of both interfaces
        # really does compare them.
        #
        # Quoting is the mutation because it is the documented way OUT of the pattern reading -
        # QueryParser says quotes take the special meaning off the slashes. So the window starts
        # searching for the four characters /^w/ while the terminal still reads an expression, and
        # the two interfaces disagree about one query, which is exactly what this test is for.
        Name = 'parity: the window reads the marks the way the terminal does'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.cs'
        # Re-anchored 2026-09-28: Apply takes the parse kept beside the text (G-4).
        From = '        var parsed = AsTyped().Parsed;'
        To = '        var parsed = QueryParser.Parse("\"" + _queryText + "\"", input: QueryInput.BeingTyped);'
        Test = 'QueryParityContractTests.The_window_and_the_terminal_read_the_same_marks_the_same_way'
    },
    @{
        # Followed HideDrivers into Sentences.cs on 2026-08-02, out of it again on 2026-08-11 when
        # the switch stopped editing text by hand and started going through QueryMembers with every
        # other chip, and into Scopes.cs on 2026-08-19 when it stopped being a chip at all.
        #
        # THE MUTATION IS THE SAME AND WHAT IT PROVES IS NOT. While the switch WAS the member there
        # was nothing to check: the window and the terminal were handed the same string. The window
        # composes a query from a scope now, and nobody ever sees that string - so this is the one
        # place a scope quietly meaning kernelDriver would show up. `docs/07` says type:driver covers
        # both kinds deliberately, and half a scope is a window that hides file system drivers from
        # somebody who asked to see drivers.
        Name = 'parity: a scope selects what its written member selects'
        File = 'src\Bws.Gui\ViewModels\Scopes.cs'
        From = 'internal const string DriverValue = "driver";'
        To = 'internal const string DriverValue = "kernelDriver";'
        Test = 'QueryParityContractTests.Each_scope_selects_what_the_written_member_selects'
    },
    @{
        # THE SWITCH PUTS NOTHING IN THE BOX, which is the owner's decision of 2026-08-19 and the
        # thing four deleted tests used to pin the opposite of. Making the scope write its member
        # into the query is the most tempting wrong version of this change - it would look right on
        # screen and would silently give the window two places that decide which entries are wanted.
        Name = 'scope: the switch is not a filter and writes nothing into the query'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Scope.cs'
        From = '            Chosen.Row = null;'
        To = @'
            QueryText = Scopes.QueryFor(value);
            Chosen.Row = null;
'@
        Test = 'ScopeTests.The_scope_switch_puts_nothing_in_the_box'
    },
    @{
        # MOVING THE SCOPE LETS GO OF WHAT WAS CHOSEN, and it is a safety property rather than
        # tidiness: everything that changes a machine acts on the selection, so one that outlived
        # the move would stand services under a button on a screen saying Drivers.
        Name = 'scope: moving it lets go of what was chosen'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Scope.cs'
        From = '            Chosen.Row = null;'
        To = '            _ = Chosen.Row;'
        Test = 'ScopeTests.Moving_the_scope_lets_go_of_what_was_chosen'
    },
    @{
        # THE COUNT IS AGAINST THE SCOPE, NOT THE MACHINE. "12 of 812" over a list of 472 answers a
        # question nobody asked, and it is the sentence a person checks against sc query.
        Name = 'scope: the count answers about the list on screen'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.cs'
        From = '        var everything = _scoping.InScope;'
        To = '        var everything = _index.Ordered;'
        Test = 'ScopeTests.The_count_is_against_the_scope_rather_than_against_the_machine'
    },
    @{
        # The whole reason a window can ask once a second. Measured: 13-22 ms against 423-500.
        Name = 'live list: a tick asks the cheap question, not the expensive one'
        File = 'src\Bws.Gui\ViewModels\RowIndex.cs'
        From = 'if (statuses.Count != _byName.Count || statuses.Any(status => !_byName.ContainsKey(status.ServiceName)))'
        To = 'if (statuses.Count != _byName.Count || statuses.Any(status => !_byName.ContainsKey(status.ServiceName)) || statuses.Count >= 0)'
        Test = 'MainViewModelTests.A_refresh_asks_the_cheap_question_and_not_the_expensive_one'
    },
    @{
        Name = 'live list: a name nobody knows costs a full reading'
        File = 'src\Bws.Gui\ViewModels\RowIndex.cs'
        From = @'
            return Freshening.CompositionChanged;
        }
'@
        To = @'
            return Freshening.Unchanged;
        }
'@
        Test = 'MainViewModelTests.A_service_installed_from_outside_appears_with_all_of_its_columns'
    },
    @{
        # A row appearing above the one somebody is aiming at moves their target while they
        # are reaching for it. This is the first sentence A10 spends on the subject.
        # Re-anchored 2026-08-05: the condition gained "and there is something to hold", after a
        # window that opened under the pointer turned out never to fill.
        Name = 'live list: the list holds still while somebody is using it'
        File = 'src\Bws.Gui\ViewModels\Holding.cs'
        From = '        if (Interacting && shown.Count > 0)'
        To = '        if (Interacting && false)'
        Test = 'MainViewModelTests.While_somebody_is_using_the_list_nothing_joins_or_leaves_it'
    },
    @{
        Name = 'live list: a row that moved says so'
        File = 'src\Bws.Gui\ViewModels\EntryRow.cs'
        From = 'RecentlyChanged = true;'
        To = 'RecentlyChanged = false;'
        Test = 'MainViewModelTests.A_row_that_moved_is_marked_and_stops_being_marked_a_few_seconds_later'
    },
    @{
        # Reentrancy rather than a data race: every continuation comes back to the interface
        # thread, so nothing is written twice at once. What breaks is ordering - the reading
        # that finished later wins over the one that looked later.
        Name = 'reentrancy: two readings are never out at once'
        File = 'src\Bws.Gui\ViewModels\Readings.cs'
        # RE-ANCHORED 2026-09-29: the guard now owes the reading rather than dropping it (G-5).
        # What is asserted is unchanged - the guard goes, and two readings can be out at once.
        From = @'
        if (_reading)
        {
            _owed = how == Relisting.Afresh || _owed == Relisting.Afresh ? Relisting.Afresh : Relisting.Keeping;

            return;
        }

        _reading = true;

        try
        {
            await LoadEverything(how).ConfigureAwait(true);
        }
'@
        To = @'
        try
        {
            await LoadEverything(how).ConfigureAwait(true);
        }
'@
        Test = 'MainViewModelTests.Two_readings_are_never_out_at_once'
    },
    @{
        # Anchored in RowList.cs since 2026-08-03, and it was in MainViewModel.cs before that.
        # The run said STALE the moment the reconciliation moved, which is the verdict earning
        # its place: the entry still compiled, still ran, and had stopped proving anything.
        Name = 'reentrancy: the list never holds a row the query rejects'
        File = 'src\Bws.Gui\ViewModels\RowList.cs'
        From = 'if (!keeping.Contains(this[index]))'
        To = 'if (!keeping.Contains(this[index]) && false)'
        Test = 'MainViewModelTests.The_list_always_matches_the_query_however_the_operations_are_interleaved'
    },
    @{
        # The guard itself has to be able to fail, or it is a list nobody is checking.
        Name = 'background work: an async void outside the list is refused'
        File = 'src\Bws.Core\Clock.cs'
        From = '    public void Wait(TimeSpan duration) => Thread.Sleep(duration);'
        To = '    public void Wait(TimeSpan duration) => Thread.Sleep(duration);' + [char]10 + [char]10 + '    internal async void Slip() => await Task.Yield();'
        Test = 'BackgroundWorkGuards.Every_async_void_is_somewhere_that_argued_for_one'
    },
    @{
        # The second half of `ADR-13` in the window - backlog 21. With this gone the list still
        # appears and every cheap field is right, so nothing looks wrong: the only sign is that a
        # question about signatures comes back with an empty list, which is exactly what "there
        # are none" looks like. That is the failure this whole slice exists to remove.
        Name = 'second phase: the window reads the expensive families rather than admitting it did not'
        File = 'src\Bws.Gui\ViewModels\Readings.cs'
        From = @'
        await FillAsync(entries).ConfigureAwait(true);
'@
        To = @'
'@
        Test = 'SecondPhaseTests.A_question_about_signatures_is_answered_once_the_reading_is_done'
    },
    @{
        Name = 'background work: work started and abandoned is refused'
        File = 'src\Bws.Gui\ViewModels\Readings.cs'
        From = '        Says.Incomplete = false;
        _failed = false;'
        To = '        Says.Incomplete = false;
        _ = RefreshAsync();
        _failed = false;'
        Test = 'BackgroundWorkGuards.No_shipped_code_starts_work_and_walks_away_from_it'
    },
    @{
        # Borrowed from the owner's Go projects, where the same guard names the files allowed
        # to hold a goroutine. A race is the one defect that does not reproduce on demand, so
        # what gets checked is where it becomes possible at all.
        # The cheap first fill, applied where it is not cheap. Somebody removes the emptiness
        # check because "a reset is faster anyway", and every refresh from then on tells the
        # DataGrid to throw the selection and the scroll position away - which no test that
        # looks at row objects can see, because the row objects are the same either way.
        Name = 'live list: a refresh never resets the list it already filled'
        File = 'src\Bws.Gui\ViewModels\RowList.cs'
        From = '        if (Count == 0 && wanted.Count > 0)'
        To = '        if (wanted.Count > 0)'
        Test = 'RowListTests.A_refresh_never_resets_the_list_it_already_filled'
    },
    @{
        # The fence around it, from the other side. Without the throw, the mistake above stops
        # being a mistake anybody can make loudly.
        Name = 'live list: resetting a list somebody is looking at is refused'
        File = 'src\Bws.Gui\ViewModels\RowList.cs'
        From = '        if (!nothingToPreserve)'
        To = '        if (false)'
        Test = 'RowListTests.Resetting_a_list_somebody_is_looking_at_is_refused'
    },
    @{
        # The file somebody keeps for months, replaced without a word. This was the state of
        # the product until 2026-08-02 - verified by overwriting a file holding the words
        # "this is not a snapshot" and getting exit code 0 for it.
        # RE-ANCHORED 2026-08-03, and the reason is worth more than the entry. It used to remove
        # the early check in Program.cs and came back MISSED the moment a second check went in
        # after the file name is worked out - because the behaviour now has two gates and taking
        # one away changes nothing. MISSED was the honest verdict: the mutation no longer removed
        # the behaviour it names. It now removes the gate that actually decides.
        # TEST MOVED 2026-10-06, security report S-8: the integration test wrote a file that is NOT a
        # snapshot, and since that day such a file is refused before this gate is reached - so the
        # old test would pass with the gate gone. The gate now decides only for a real snapshot, and
        # the unit test hands it one.
        Name = 'snapshot: an existing file is not overwritten without being told'
        File = 'src\Bws.Core\Snapshots\SnapshotOnDisk.cs'
        From = '        if (!force)'
        To = '        if (!force && false)'
        Test = 'SnapshotTargetTests.A_snapshot_is_not_replaced_without_force'
    },
    @{
        # The realistic way this one goes: somebody pins the degree while debugging something
        # else and leaves it. Every other test in the project stays green while a listing takes
        # four times as long, because nothing else in it measures anything.
        Name = 'speed: a listing read one entry at a time is refused'
        File = 'src\Bws.Core\WindowsScmCatalog.cs'
        From = '    public static int DefaultDegreeOfParallelism => Environment.ProcessorCount;'
        To = '    public static int DefaultDegreeOfParallelism => 1;'
        # RE-POINTED 2026-08-26, and the reason is worth more than the entry. This named the
        # timing test, which answers with the machine in it - so under a full run it came back
        # MISSED, and MISSED reads like a guard with nothing to say. Backlog 238.
        #
        # Measured before moving it, with this very mutation applied on an IDLE machine: the
        # timing test passed silently in one run of five. The load in that backlog row was
        # incidental. The real fault was that every pair measured one-at-a-time first and
        # several-at-once second, so warming across the pair was credited to the wide run - it
        # came out lower in five runs of five with no parallelism in the code at all.
        #
        # Both were fixed: the order alternates now, and this entry points at the claim that
        # has no clock in it. The timing test keeps its own job - catching parallelism removed
        # some other way - and deliberately has no registry entry, because an entry that can
        # report MISSED for reasons nobody caused is worse than no entry.
        Test = 'ReadAllContractTests.Describing_entries_uses_the_whole_machine_by_default'
    },
    @{
        # A field per row, which is the realistic way this budget gets broken: somebody caches
        # something on EntryRow and 810 of them arrive at once. Eight kilobytes each is 6.6 MB
        # against a ceiling of 2.5 (1.5 until 2026-09-16, backlog 362 - the raise was checked
        # against this entry before it was made, because a ceiling raised past the mutation's
        # reach would leave the entry green and the guard gone).
        Name = 'memory: a listing holding more per entry is refused'
        File = 'src\Bws.Gui\ViewModels\EntryRow.cs'
        From = '    private bool _recentlyChanged;'
        To = '    private bool _recentlyChanged;' + [char]10 + '    private readonly byte[] _cached = new byte[8192];'
        Test = 'MemoryBudgetTests.A_loaded_listing_holds_no_more_than_it_did'
    },
    @{
        # The rule that costs 21 053 ms and a set of credentials when it is not there. The
        # mutation inverts the policy rather than deleting it, so both halves are exercised:
        # Skip starts following and Follow starts skipping.
        Name = 'network: a path on another machine is not asked about'
        File = 'src\Bws.Core\BinaryPathResolver.cs'
        # Re-anchored 2026-10-05 (stability R-3): every place a candidate can mean is asked about on
        # its own now, because a bare name searched along PATH can meet a share among local folders.
        From = 'var offLimits = NetworkPaths == NetworkPaths.Skip && NetworkPath.LeavesThisMachine(place);'
        To = 'var offLimits = NetworkPaths == NetworkPaths.Follow && NetworkPath.LeavesThisMachine(place);'
        Test = 'BinaryPathResolverTests.A_path_on_another_machine_is_not_asked_about_at_all'
    },
    @{
        # THE MUTATION IS THE CODE THAT STOOD HERE UNTIL 2026-09-08, which is why this entry is
        # worth more than a made-up break: it puts the real defect back. ExpandEnvironmentVariables
        # reads the block THIS process started with, and that block carries the signed-in user's
        # variables over the machine's - so one entry could resolve to two different files for two
        # people, and their snapshots would differ with nothing on the machine changed.
        #
        # Verified by hand before this entry was written: with the line below restored, exactly the
        # two cases of the named test fail and the other 35 in the file stay green. The measurement
        # that made the fix safe is in the method's own comment - 270 of 786 entries carry a percent
        # sign here and all 270 expand identically under both rules, so nothing on this machine
        # moved. What moved is the machine where TEMP is the user's rather than the system's.
        Name = 'resolving: a percent name means what the manager would see, not what we would'
        File = 'src\Bws.Core\BinaryPathResolver.cs'
        # Re-anchored 2026-10-05: the expansion moved to ManagerEnvironment with the machine's PATH.
        From = '                value = ManagerEnvironment.Expand(value, WindowsDirectory);'
        To = '                value = Environment.ExpandEnvironmentVariables(value);'
        Test = 'BinaryPathResolverTests.The_Windows_directory_a_percent_name_means_is_the_one_handed_in'
    },
    @{
        # The leak this one is named for really happened: the CI workflow carried a sentence of
        # Polish - a remark quoted out of a conversation - for two days, in a file that goes out
        # with the repository. Two rules said it should not be there and neither was checked by
        # anything. This injects the same shape rather than a different one.
        Name = 'publishing: text outside plain ASCII in a published file is refused'
        File = 'src\Bws.Cli\Release.cs'
        From = 'internal static class Release'
        To = '// zdanie po polsku, ktorego tu nie powinno byc' + [char]0x0142 + [char]10 + 'internal static class Release'
        Test = 'PublicSurfaceGuards.Nothing_outside_plain_ascii_appears_without_a_reason_on_the_list'
    },
    @{
        # BOTH OF THE NEXT TWO MUTATE THE LEAK RATHER THAN THE GUARD, and that is the only way
        # they can prove anything. A guard looking for private data in a repository that has
        # none passes whatever you do to it - break its pattern and it still finds nothing. So
        # the mutation puts the leak in, and the guard has to notice.
        #
        # THE PLANTED PATH IS GLUED FROM TWO PIECES, SINCE 2026-10-16. This file joined version control
        # that day and is swept by the very guard this entry proves - so a path written out whole here
        # was itself a leak in a published file, and the guard said so on the first run. The value the
        # mutation writes is unchanged, byte for byte. Only the text of this file stopped having the
        # shape.
        Name = 'publishing: a home directory written into a published file is refused'
        File = 'src\Bws.Core\CoreAssembly.cs'
        From = '    public const string Name = "Bws.Core";'
        To = '    public const string Name = "Bws.Core"; // built from D:\' + 'Users\someone\src'
        Test = 'PublicSurfaceGuards.No_shape_that_belongs_to_a_person_is_written_into_a_published_file'
    },
    @{
        # The obligation, not the tidiness: the MIT licence grants what it grants on condition
        # that its notice travels with the copies. A package added to a shipped project arrives
        # with no prompt, so this is the thing that asks.
        Name = 'publishing: a shipped package missing from the notices is refused'
        File = 'THIRD-PARTY-NOTICES.md'
        # ASCII only in this anchor, deliberately. The first version reached into the copyright
        # holder's name, which carries accented letters, and came back STALE - Windows PowerShell
        # reads a file with no byte order mark differently from how the text was written, so the
        # two sides stopped being the same string without either being wrong.
        From = '| Meziantou.Analyzer 3.0.138 |'
        To = '| An analyser |'
        Test = 'LicenceNoticeGuards.Every_package_that_ships_is_named_in_the_notices'
    },
    @{
        # --timeout caps the watching, never the manager's own answer, and until 2026-08-04
        # nothing said so when the two came apart: `bws start X --timeout 1` ran 30 450 ms on
        # Windows Server 2025 and looked like a switch doing nothing.
        #
        # The mutation inverts the gate rather than deleting the test, so both halves are
        # exercised at once - a step that failed late stops being named, and a step that gave
        # up at the ceiling starts being named. The entry points at the first, which is the
        # one the sentence exists for.
        Name = 'plan: a step that ran past the ceiling is said out loud'
        File = 'src\Bws.Core\Planning\PlanRun.cs'
        From = '        [.. Results.Where(result => result.Answered > Ceiling.TotalMilliseconds)];'
        To = '        [.. Results.Where(result => result.Answered < Ceiling.TotalMilliseconds)];'
        Test = 'PlanRunCeilingTests.A_step_that_ran_past_the_ceiling_is_named'
    },
    @{
        # THE ENTRY WORTH HAVING OUT OF THE THREE, because the mutation is the implementation
        # somebody would reach for. Reversing the steps one at a time instead of asking each entry
        # where it started and where it ended tells whoever restarted a service to stop it - the
        # two steps of a restart cancel out. Dangerous advice, on the commonest write this tool
        # performs, printed in the same calm voice as the correct answer.
        Name = 'plan: a restart that ended where it began offers no way back'
        File = 'src\Bws.Core\Planning\NetEffect.cs'
        From = '            .Where(move => Before(move.Value.First) != After(move.Value.Last))'
        To = '            .Where(move => true)'
        Test = 'PlanReversalTests.A_restart_that_ended_where_it_began_says_nothing'
    },
    @{
        # A stop cascade takes the dependants down first, so putting it back starts the entry they
        # depend on first. Handed back in the order they happened, the first line is one the
        # manager refuses - a way out that does not work is worse than admitting there is none.
        Name = 'plan: the way back is handed over in the reverse order'
        File = 'src\Bws.Core\Planning\NetEffect.cs'
        # RE-ANCHORED 2026-09-24: the sort grew a second key (a setting goes back before its
        # entry's own move) and went over several lines. The swap it makes is unchanged.
        From = '            .OrderByDescending(one => one.When)'
        To = '            .OrderBy(one => one.When)'
        Test = 'PlanReversalTests.A_cascade_hands_the_commands_back_in_the_reverse_order'
    },
    @{
        # A timed out step is not a failed one: the manager took the request and the entry may have
        # arrived after we stopped watching. Dropping it here would go quiet about an entry
        # somebody was left holding, which is the direction of error that costs the most.
        Name = 'plan: a step that timed out still counts as having moved the entry'
        File = 'src\Bws.Core\Planning\NetEffect.cs'
        From = '                && result.Outcome != StepOutcome.TimedOut'
        To = '                && true'
        Test = 'PlanReversalTests.A_step_that_timed_out_counts_as_having_moved_the_entry'
    },
    @{
        # CreateProcess appends .exe to a name with no extension, and until 2026-08-04 the
        # resolver did not - so a service written `svchost -k TSLicensing` was reported as
        # having lost its binary while it was running. Found on Windows Server 2025, which has
        # one such entry, on a project written against a machine that has none.
        #
        # The mutation empties the fallback rather than removing the branch. Removing it would
        # leave an `if` whose body is gone, and the lesson from the last batch is to mutate the
        # value, not the shape - this yields the same candidate twice, which is a no-op with
        # nothing for the compiler to object to.
        #
        # Reddens exactly one test. Nothing_is_appended_to_a_name_that_already_carries_an_extension
        # stays green under it, on purpose: that one asserts the absence and the absence is what
        # the mutation produces.
        Name = 'path: a command with no extension resolves to the exe Windows would run'
        File = 'src\Bws.Core\BinaryPathResolver.cs'
        # Re-anchored 2026-10-05: one more level of indentation, inside the resolver's Asking.
        From = '                yield return place + ".exe";'
        To = '                yield return place;'
        Test = 'BinaryPathResolverTests.A_command_naming_no_extension_runs_the_exe_Windows_appends_to_it'
    },
    @{
        # The expensive half. This one opens and reads the file rather than asking whether it
        # is there, so losing it turns a listing into a file transfer over somebody's share.
        Name = 'network: the second pass does not open a file on another machine'
        File = 'src\Bws.Core\WindowsBinaryInspector.cs'
        From = '        networkPaths == NetworkPaths.Skip && NetworkPath.LeavesThisMachine(file);'
        To = '        networkPaths == NetworkPaths.Follow && NetworkPath.LeavesThisMachine(file);'
        Test = 'BinaryPathResolverTests.The_second_pass_does_not_open_a_file_on_another_machine_either'
    },
    @{
        Name = 'concurrency: a fourth place doing two things at once is refused'
        File = 'src\Bws.Core\Clock.cs'
        From = '    public void Wait(TimeSpan duration) => Thread.Sleep(duration);'
        To = '    public void Wait(TimeSpan duration) => Thread.Sleep(duration);' + [char]10 + [char]10 + '    private static long _elsewhere;' + [char]10 + [char]10 + '    internal static long Elsewhere() => Interlocked.Increment(ref _elsewhere);'
        Test = 'ConcurrencyGuards.Only_the_places_that_argued_for_it_can_do_two_things_at_once'
        # Was Task.Run(() => { }) until 2026-08-02, and came back BROKEN rather than caught.
        # The analyser rules taken the day before include MA0134, which refuses an async call
        # whose result nobody observes - so the mutation stopped compiling and this entry
        # stopped proving anything. It reported that honestly, which is the whole point of
        # having a BROKEN verdict at all.
        #
        # Worth keeping in mind beyond this line: the registry said 38 of 38 and had been
        # wrong since the analysers landed. A tool that proves guards can fail is itself a
        # thing that goes stale, and nothing but running it says so.
        #
        # Interlocked instead, because it returns a value nobody has to discard, and the guard
        # reads source text rather than compiled code - so what matters is that the mutation
        # is genuine concurrency rather than which construct it uses.
    },
    @{
        # One line onto the file that IS the ceiling. Anything smaller proves nothing, and the
        # first attempt at this entry added forty lines to a thirty line file and came back
        # MISSED - which is the runner reporting honestly that the mutation never got near
        # the thing being guarded.
        Name = 'size: the longest file may not get longer'
        #
        # THE ONLY ENTRY HERE THAT FINDS ITS OWN TARGET, and it had to become one. The ceiling
        # is set at the longest shipped file, so a line added to any shorter file proves
        # nothing - which means this entry has to name whichever file is longest today.
        #
        # It came back MISSED twice on 2026-08-02, both times honestly. First because
        # WindowsScmCatalog.cs had been split from 807 lines to 515 and no longer reached the
        # ceiling. Then again an hour later, after a comment in this very entry said "whoever
        # lowers the ceiling next has to move this with it" - and the person who lowered it
        # next was the person who wrote that sentence.
        #
        # An instruction that gets ignored by its own author within the hour is not an
        # instruction, it is a defect with a note attached. So the entry worked the file out at
        # run time, until 2026-09-23 - and then stopped needing to, for the reason written above
        # $codePadding: four hundred lines of code make ANY file longer than the ceiling, so the
        # entry names one small file that is not going anywhere and cannot go stale when the
        # longest one is split.
        File = 'src\Bws.Core\Clock.cs'
        From = 'public interface IClock'
        To = $codePadding + 'public interface IClock'
        Test = 'SizeRatchetGuards.No_shipped_file_is_longer_than_the_longest_one_was'
    },
    @{
        # The defect exactly as it shipped: "could not read" drawn as a whole ring, separated from
        # "stopped" by two points of grey per channel at eight pixels across. The owner found it by
        # asking what the dots meant, which is the only instrument that had ever looked at them.
        Name = 'marks: an unreadable value is not drawn like a value that was read'
        # Moved with the marks into Themes/Cells.xaml on 2026-08-12 (backlog 166), and on again
        # into Themes/Marks.xaml on 2026-08-25 when the markup ratchet asked Cells.xaml for a seam.
        File = 'src\Bws.Gui\Themes\Marks.xaml'
        From = @'
                <Setter Property="StrokeDashArray" Value="{StaticResource ShapeUnreadableOutline}" />
                <Setter Property="ToolTip" Value="{DynamicResource gui.start.mark.unknown}" />
'@
        To = @'
                <Setter Property="ToolTip" Value="{DynamicResource gui.start.mark.unknown}" />
'@
        Test = 'MarkDistinctionGuards.The_mark_for_something_nobody_could_read_is_a_broken_ring'
    },
    @{
        # `P2`: a group is a promise that its chips ADD UP, because the language ORs members of one
        # field and ANDs members of different ones. A chip from another field slipped into a group
        # turns that promise false while three labels on screen still make it.
        Name = 'chips: a group asks about one field'
        File = 'src\Bws.Gui\ViewModels\FilterChips.cs'
        From = 'new FilterChip("gui.filter.paused", "status", "paused", negated: false, read, write),'
        To = 'new FilterChip("gui.filter.paused", "start", "manual", negated: false, read, write),'
        Test = 'FilterChipTests.No_group_says_its_chips_add_up_while_the_query_narrows_them'
    },
    @{
        # BACKLOG 165, AND IT IS THE HALF THAT IS EASY TO LEAVE OUT RATHER THAN THE FEATURE. A mark
        # about the start type that only follows a FULL reading is true when the window opens and
        # quietly false a second later, because whether an entry runs against its start type
        # depends on the running state - and that is what the per-second reading watches.
        Name = 'marks: the start type mark follows the cheap reading too'
        File = 'src\Bws.Gui\ViewModels\EntryRow.cs'
        From = @'
        AgainstShape = CellFaces.AgainstShape(_entry.RunsAgainstItsStartType);
'@
        To = ''
        Test = 'ColumnGuards.The_mark_about_the_start_type_follows_a_cheap_reading'
    },
    @{
        # THE DEFECT EXACTLY AS IT SHIPPED, backlog 166, and it is one word. StartMark was BasedOn
        # StatusMark, which means it inherited triggers reading the RUN STATE - so a running entry
        # got a green Fill from the status trigger and the start column's own trigger, which sets
        # only Stroke, never took it away. Measured on the pixel before the repair: #6CCB5F in the
        # column about the NEXT start.
        #
        # It is here rather than as a repair to the two entries below because those two pass with
        # the fault present - they compare a style against itself and cannot see an inherited
        # trigger at all. That is why this one names a different test.
        Name = 'marks: the start mark is moved by the start type and by nothing else'
        File = 'src\Bws.Gui\Themes\Marks.xaml'
        From = '<Style x:Key="StartMark" TargetType="Ellipse" BasedOn="{StaticResource MarkShape}">'
        To = '<Style x:Key="StartMark" TargetType="Ellipse" BasedOn="{StaticResource StatusMark}">'
        Test = 'MarkDistinctionGuards.A_mark_is_moved_only_by_the_field_of_its_own_column'
    },
    @{
        # The same fault in the other column, and here the whole trigger goes - which is how the
        # status column carried it: no trigger at all, and the style default drew something
        # indistinguishable from a stopped entry.
        Name = 'marks: two meanings may not differ only by a shade'
        # Moved with the marks into Themes/Cells.xaml on 2026-08-12 (backlog 166), and on again
        # into Themes/Marks.xaml on 2026-08-25 when the markup ratchet asked Cells.xaml for a seam.
        File = 'src\Bws.Gui\Themes\Marks.xaml'
        From = @'
            <DataTrigger Binding="{Binding StatusShape}" Value="shape.unknown">
                <Setter Property="Stroke" Value="{StaticResource MeaningUnknown}" />
                <Setter Property="StrokeDashArray" Value="{StaticResource ShapeUnreadableOutline}" />
            </DataTrigger>
'@
        To = ''
        Test = 'MarkDistinctionGuards.No_two_marks_differ_only_by_a_shade_of_the_same_colour'
    },
    @{
        # ADR-21 left the precedence open and the code answered it. Swapped, a shipped translation
        # could be overridden by anybody dropping a file next to the executable - which is either a
        # feature or a supply chain question, and is certainly not something to acquire by accident.
        Name = 'language: a built-in translation wins over a file beside the program'
        File = 'src\Bws.Gui\Texts.cs'
        From = 'using var chosen = embedded(wanted) ?? beside(wanted);'
        To = 'using var chosen = beside(wanted) ?? embedded(wanted);'
        Test = 'LanguageGuards.A_built_in_language_wins_over_a_file_dropped_beside_the_program'
    },
    @{
        # English underneath, translation on top. Reverse the two and a half-finished translation
        # is silently undone by the fallback - every key reverts to English and the translation
        # looks like it was never installed.
        Name = 'language: an unfinished translation keeps the keys it did translate'
        File = 'src\Bws.Gui\Texts.cs'
        From = @'
        using (var fallback = embedded(Fallback))
        {
            Merge(strings, fallback);
        }
'@
        To = ''
        Test = 'LanguageGuards.An_unfinished_translation_shows_a_sentence_rather_than_a_key'
    },
    @{
        # A machine set to English must not go looking for an "en" translation to lay over the
        # English it already has.
        Name = 'language: english is not asked for twice'
        File = 'src\Bws.Gui\Texts.cs'
        From = 'if (!string.Equals(wanted, Fallback, StringComparison.OrdinalIgnoreCase))'
        To = 'if (true)'
        Test = 'LanguageGuards.English_is_never_asked_for_twice'
    },
    @{
        # THE ONLY ENTRY IN THIS REGISTRY THAT MUTATES A PROJECT FILE, and the failure it stands
        # for is the one that costs a whole window: MSBuild reads the ".en." in a file name as a
        # culture, compiles the file into a satellite assembly and strips the culture out of the
        # resource name. The build stays green and the window comes up showing keys.
        Name = 'language: the english file is embedded rather than turned into a satellite'
        File = 'src\Bws.Gui\Bws.Gui.csproj'
        From = @'
                      LogicalName="Bws.Gui.Resources.gui.en.json"
                      WithCulture="false" />
'@
        To = ' />
'
        Test = 'LanguageGuards.The_english_file_is_embedded_under_the_name_the_loader_asks_for'
    },
    @{
        Name = 'size: the longest markup file may not get longer'
        #
        # The second of the two, added 2026-08-10 when markup came under the ratchet at all. It
        # finds its own target exactly like the entry above, and for the same reason - but it could
        # not reuse that one's shape, because the anchor there is a namespace declaration and XAML
        # has nothing of the kind. A comment before the closing tag of the root element is the
        # smallest edit that adds a line and still parses.
        #
        # A comment, deliberately, rather than a resource or a setter. Anything that declares
        # something could collide with a key already in the file, and then the entry would be
        # proving that duplicate keys are refused rather than that a file may not grow.
        # TWO HUNDRED LINES RATHER THAN ONE SINCE 2026-08-19, AND THE ENTRY CAME BACK `MISSED`
        # BEFORE ANYBODY NOTICED WHY. One line was enough while the longest markup file sat EXACTLY
        # on the ceiling, which it did the day this was written - MainWindow.xaml at 397 against 397.
        # The scope switch took a seam out of that file and the longest is now Controls.xaml at 395,
        # so one added line makes 396 and crosses nothing. The guard was right the whole time.
        #
        # THIS IS STRENGTHENING THE MUTATION, WHICH IS THE OPPOSITE OF WHAT THE RULE FORBIDS. "Fix
        # the sentence or the guard, never the mutation" exists to stop somebody WEAKENING a
        # mutation until a weak guard looks good. Here the mutation had quietly stopped testing what
        # its name says, because it was calibrated against a headroom of zero that no longer holds.
        # A count that does not depend on today's headroom cannot go stale the same way - which is
        # what the entry below this one has always done, with five hundred.
        #
        # A RESOURCE, NOT A COMMENT, SINCE 2026-09-23 - and the paragraph above arguing for a
        # comment is exactly what that day made wrong. The ratchet counts lines of markup now, and
        # a comment is not one, so two hundred comment lines would change nothing and the entry
        # would come back MISSED against a guard that works. The collision the paragraph feared is
        # handled by the key: ShapeMutantPadding exists nowhere in the tree. The file is a small
        # fixed one rather than the longest, for the reason above $codePadding.
        File = 'src\Bws.Gui\Themes\Type.xaml'
        From = '</ResourceDictionary>'
        To = $markupPadding + '</ResourceDictionary>'
        Test = 'SizeRatchetGuards.No_shipped_markup_file_is_longer_than_the_longest_one_was'
    },
    @{
        Name = 'size: one more long file than there were is refused'
        # Four hundred lines of CODE since 2026-09-23, not five hundred of comment - the same reason
        # as the two entries above. Enough to put Clock.cs in the band near the ceiling and past it.
        File = 'src\Bws.Core\Clock.cs'
        From = 'public interface IClock'
        To = $codePadding + 'public interface IClock'
        Test = 'SizeRatchetGuards.Not_more_files_are_long_than_were_long'
    },
    @{
        # The half of the ratchet the slack used to forgive. One more than the measurement is a
        # ceiling standing above the file it holds, and the test has to say so on the next run.
        Name = 'size: a ceiling left above the longest file is refused'
        File = 'tests\Bws.Architecture.Tests\SizeCeilings.cs'
        From = 'internal const int LongestShippedFile = '
        To = 'internal const int LongestShippedFile = 1 + '
        Test = 'SizeRatchetGuards.The_ceilings_have_not_been_left_behind_by_the_code'
    },
    @{
        # THE SHAPE CEILINGS, 2026-09-23. Each entry below adds one small type to Clock.cs that is
        # past ONE axis and inside every other, so the one case of the theory that goes red is the
        # axis the entry is named after. The runner filters by method name and cannot tell the
        # cases apart - that is the cost, and the shape of the padding is what pays it.
        Name = 'shape: no method is longer than the longest one was'
        File = 'src\Bws.Core\Clock.cs'
        From = 'public interface IClock'
        To = $longMethod + 'public interface IClock'
        Test = 'CodeShapeGuards.Every_ceiling_is_exactly_the_largest_thing_under_it'
    },
    @{
        Name = 'shape: one more long method than there were is refused'
        File = 'src\Bws.Core\Clock.cs'
        From = 'public interface IClock'
        To = $longMethod + 'public interface IClock'
        Test = 'CodeShapeGuards.Every_crowd_count_is_exactly_what_stands_near_its_ceiling'
    },
    @{
        Name = 'shape: no method forks more than the most complex one did'
        File = 'src\Bws.Core\Clock.cs'
        From = 'public interface IClock'
        To = $forkingMethod + 'public interface IClock'
        Test = 'CodeShapeGuards.Every_ceiling_is_exactly_the_largest_thing_under_it'
    },
    @{
        Name = 'shape: no method nests deeper than the deepest one did'
        File = 'src\Bws.Core\Clock.cs'
        From = 'public interface IClock'
        To = $deepMethod + 'public interface IClock'
        Test = 'CodeShapeGuards.Every_ceiling_is_exactly_the_largest_thing_under_it'
    },
    @{
        Name = 'shape: no signature is wider than the widest one was'
        File = 'src\Bws.Core\Clock.cs'
        From = 'public interface IClock'
        To = $wideSignature + 'public interface IClock'
        Test = 'CodeShapeGuards.Every_ceiling_is_exactly_the_largest_thing_under_it'
    },
    @{
        Name = 'shape: no type carries more methods than the largest one did'
        File = 'src\Bws.Core\Clock.cs'
        From = 'public interface IClock'
        To = $manyMethods + 'public interface IClock'
        Test = 'CodeShapeGuards.Every_ceiling_is_exactly_the_largest_thing_under_it'
    },
    @{
        Name = 'shape: no type holds more state than the largest one did'
        File = 'src\Bws.Core\Clock.cs'
        From = 'public interface IClock'
        To = $muchState + 'public interface IClock'
        Test = 'CodeShapeGuards.Every_ceiling_is_exactly_the_largest_thing_under_it'
    },
    @{
        # The anchor is the declaration and not the number, so this entry does not go stale every
        # time the ceiling comes down - which is the routine door and is meant to happen often.
        Name = 'shape: a ceiling left above the longest method is refused'
        File = 'tests\Bws.Architecture.Tests\ShapeCeilings.cs'
        From = 'internal const int LongestMethod = '
        To = 'internal const int LongestMethod = 1 + '
        Test = 'CodeShapeGuards.Every_ceiling_is_exactly_the_largest_thing_under_it'
    },
    @{
        # The entry point is a named exemption. Without top-level statements measured as a unit, the
        # exemption names nothing - which is how this guard would have looked the day before it was
        # written, when nothing measured the command line's entry point at all.
        Name = 'shape: top-level statements are a unit, and an exemption names something real'
        File = 'tests\Bws.Architecture.Tests\CodeShape.cs'
        From = 'if (root is CompilationUnitSyntax compilation && CodeUnit.HasTopLevel(compilation))'
        To = 'if (root is CompilationUnitSyntax compilation && CodeUnit.HasTopLevel(compilation) && false)'
        Test = 'CodeShapeGuards.Every_exemption_names_one_unit_that_still_stands_over_its_ceiling'
    },
    @{
        # A scan that reads nothing passes every ceiling ever set. This is the canary for that.
        Name = 'shape: a scan that reads nothing is refused'
        File = 'tests\Bws.Architecture.Tests\Sources.cs'
        From = 'internal static IEnumerable<string> Shipped() => Under("src", "*.cs");'
        To = 'internal static IEnumerable<string> Shipped() => Under("src", "*.nothing");'
        Test = 'CodeShapeGuards.The_scan_read_the_whole_tree'
    },
    @{
        # THE MEASURE ITSELF. Four entries against the tests of the measure, because a ceiling
        # standing on a measure that lies is worse than none - and the first draft of this measure
        # DID lie, scoring four stacked `fixed` statements as four levels.
        Name = 'shape measure: stacked using, fixed and lock are one level'
        File = 'tests\Bws.Architecture.Tests\ShapeMeasures.cs'
        From = 'Stacks.Contains(node.Kind()) && node.Parent is { } parent && Stacks.Contains(parent.Kind());'
        To = 'Stacks.Contains(node.Kind()) && node.Parent is { } parent && Stacks.Contains(parent.Kind()) && false;'
        Test = 'CodeShapeMetricTests.Stacked_using_fixed_and_lock_are_one_level_and_braces_between_them_are_two'
    },
    @{
        Name = 'shape measure: a token over several lines counts every line'
        File = 'tests\Bws.Architecture.Tests\CodeShape.cs'
        From = 'for (var line = span.StartLinePosition.Line; line <= span.EndLinePosition.Line; line++)'
        To = 'for (var line = span.StartLinePosition.Line; line <= span.StartLinePosition.Line; line++)'
        Test = 'CodeShapeMetricTests.A_string_over_several_lines_counts_every_line_it_covers'
    },
    @{
        Name = 'shape measure: markup comments are free'
        File = 'tests\Bws.Architecture.Tests\CodeShape.cs'
        From = 'return (!inComment && !char.IsWhiteSpace(text[index]), 0);'
        To = 'return (!char.IsWhiteSpace(text[index]), 0);'
        Test = 'CodeShapeMetricTests.Markup_comments_are_free_and_markup_is_not'
    },
    @{
        Name = 'shape measure: a class is summed across its partial files'
        File = 'tests\Bws.Architecture.Tests\ShapeMeasures.cs'
        From = 'var key = $"{project}:{Naming.TypeOf(type)}";'
        To = 'var key = $"{file}:{Naming.TypeOf(type)}";'
        Test = 'CodeShapeMetricTests.A_class_is_measured_across_every_file_it_is_split_into'
    },
    @{
        # THE ANALYSER GUARD, 2026-09-23. A rule deleted from .editorconfig turns a red build green
        # and leaves the build looking exactly as guarded as before.
        Name = 'analysers: a rule taken out of .editorconfig is refused'
        File = '.editorconfig'
        From = 'dotnet_diagnostic.CA2000.severity = warning'
        To = 'dotnet_diagnostic.CA2000.severity = none'
        Test = 'AnalyzerRuleGuards.The_analyser_configuration_is_exactly_what_was_chosen'
    },
    @{
        # EnableNETAnalyzers rather than RunAnalyzers, deliberately: RunAnalyzers would take the
        # Win32 source generator down with it, Bws.Core would not compile, and the entry would come
        # back BROKEN - proving nothing about the guard.
        Name = 'analysers: a project switching the analysers off is refused'
        # Anchored on the one line of that file that appears once - <PropertyGroup> is there twice,
        # and a plain replacement would have edited both.
        File = 'src\Bws.Core\Bws.Core.csproj'
        From = '<AllowUnsafeBlocks>true</AllowUnsafeBlocks>'
        To = '<AllowUnsafeBlocks>true</AllowUnsafeBlocks>' + [char]10 + '    <EnableNETAnalyzers>false</EnableNETAnalyzers>'
        Test = 'AnalyzerRuleGuards.Nothing_switches_an_analyser_off_for_one_project'
    },
    @{
        Name = 'analysers: a warning silenced in the code is counted'
        File = 'src\Bws.Core\Clock.cs'
        From = 'public interface IClock'
        To = '#pragma warning disable CA2000' + [char]10 + '#pragma warning restore CA2000' + [char]10 + 'public interface IClock'
        Test = 'AnalyzerRuleGuards.Every_way_around_the_compiler_is_counted'
    },
    @{
        # FOUR MORE FROM REVIEW, 2026-09-23. The compiler's parser separates a key from its value
        # with a colon as well as an equals sign, and the later of two lines wins - so a line added
        # BELOW the recorded one overrides it in the build while the recorded one is still there.
        Name = 'analysers: a rule overridden by a colon line below it is refused'
        File = '.editorconfig'
        From = 'dotnet_diagnostic.CA2000.severity = warning'
        To = 'dotnet_diagnostic.CA2000.severity = warning' + [char]10 + 'dotnet_diagnostic.CA2000.severity: none'
        Test = 'AnalyzerRuleGuards.The_analyser_configuration_is_exactly_what_was_chosen'
    },
    @{
        Name = 'analysers: a project keeping the CA rules out of the gate is refused'
        File = 'src\Bws.Core\Bws.Core.csproj'
        From = '<AllowUnsafeBlocks>true</AllowUnsafeBlocks>'
        To = '<AllowUnsafeBlocks>true</AllowUnsafeBlocks>' + [char]10 + '    <CodeAnalysisTreatWarningsAsErrors>false</CodeAnalysisTreatWarningsAsErrors>'
        Test = 'AnalyzerRuleGuards.Nothing_switches_an_analyser_off_for_one_project'
    },
    @{
        # A build file scan that finds nothing passes this guard and SupplyChainGuards both.
        Name = 'analysers: a scan of build files that reads nothing is refused'
        File = 'tests\Bws.Architecture.Tests\Sources.cs'
        From = '.Where(path => !Beneath(path, "obj", "bin", "tools"));'
        To = '.Where(path => !Beneath(path, "obj", "bin", "tools") && false);'
        Test = 'AnalyzerRuleGuards.The_build_file_scan_reads_every_project_in_the_solution'
    },
    @{
        # THE HYGIENE PACKAGE, 2026-09-23. There was an entry above this one switching the
        # documentation file off in Bws.Core.csproj, and it came back BROKEN on its first run: the SDK
        # refuses to build a project with IDE0005 on and no documentation file, error
        # EnableGenerateDocumentationFile. That half is held by the build, which is stronger than a
        # test - so the entry went, and so did the guard line it was proving. This one stays,
        # because a documentation file in the release reddens nothing else.
        Name = 'analysers: the documentation file going out with the release is refused'
        File = 'Directory.Build.props'
        From = '<PublishDocumentationFile>false</PublishDocumentationFile>'
        To = '<PublishDocumentationFile>true</PublishDocumentationFile>'
        Test = 'AnalyzerRuleGuards.Nothing_switches_an_analyser_off_for_one_project'
    },
    @{
        # A public type nothing names. Public on purpose: a private one is IDE0051's, and the build
        # would refuse it before this guard ever ran - which the runner reports as BROKEN.
        Name = 'dead code: a public type nothing calls is refused'
        File = 'src\Bws.Core\Clock.cs'
        From = 'public sealed class SystemClock : IClock'
        To = 'public static class NothingCallsThis' + [char]10 + '{' + [char]10 + '    public static int Answer() => 42;' + [char]10 + '}' + [char]10 + [char]10 + 'public sealed class SystemClock : IClock'
        Test = 'DeadCodeGuards.Nothing_in_the_product_is_reached_only_by_its_tests_or_by_nothing'
    },
    @{
        # Counted as a root, a test would keep the eighteen screen seams alive - and every one of
        # them is on the exception list, which then names code that has a caller.
        Name = 'dead code: a mention from a test is not life'
        File = 'tests\Bws.Architecture.Tests\DeadCode.cs'
        From = 'var site = fromTests ? DeadCode.Test : SiteOf(name);'
        To = 'var site = fromTests ? DeadCode.Root : SiteOf(name);'
        Test = 'DeadCodeGuards.The_list_of_code_kept_without_a_caller_names_only_code_that_still_has_none'
    },
    @{
        # A VALUE, not the call. The first version replaced the call to SiteOf, which left SiteOf
        # unused - and IDE0051, switched on in the same package, refused the build: BROKEN.
        Name = 'dead code: a mention from inside dead code is not life'
        File = 'tests\Bws.Architecture.Tests\DeadCode.cs'
        From = '.FirstOrDefault(index => index != DeadCode.Root, DeadCode.Root);'
        To = '.FirstOrDefault(index => index == DeadCode.Test, DeadCode.Root);'
        Test = 'DeadCodeScanTests.A_mention_from_inside_dead_code_keeps_nothing_alive'
    },
    @{
        Name = 'dead code: two definitions naming only each other are both found'
        File = 'tests\Bws.Architecture.Tests\DeadCode.cs'
        From = 'site >= 0 && site != index && alive[site]'
        To = 'site >= 0 && site != index'
        Test = 'DeadCodeScanTests.Two_definitions_that_only_name_each_other_are_both_reported'
    },
    @{
        Name = 'dead code: a documentation reference is not life'
        File = 'tests\Bws.Architecture.Tests\DeadCode.cs'
        From = 'foreach (var name in root.DescendantNodes().OfType<SimpleNameSyntax>())'
        To = 'foreach (var name in root.DescendantNodes(descendIntoTrivia: true).OfType<SimpleNameSyntax>())'
        Test = 'DeadCodeScanTests.A_comment_or_a_documentation_reference_keeps_nothing_alive'
    },
    @{
        Name = 'dead code: a member the language calls by contract is not a definition'
        File = 'tests\Bws.Architecture.Tests\DeadCode.cs'
        From = '"Convert", "ConvertBack", "GetErrors", "HasErrors", "Dispose",'
        To = '"Convert", "ConvertBack", "GetErrors", "HasErrors",'
        Test = 'DeadCodeScanTests.An_override_and_a_member_called_by_contract_are_not_definitions'
    },
    @{
        Name = 'dead code: an attribute is reached by its short name'
        File = 'tests\Bws.Architecture.Tests\DeadCode.cs'
        From = 'Add(name.Identifier.ValueText + "Attribute", site);'
        To = 'Add(name.Identifier.ValueText, site);'
        Test = 'DeadCodeScanTests.Markup_and_an_attribute_each_keep_a_name_alive'
    },
    @{
        Name = 'silent catch: a new catch that drops what it caught is counted'
        File = 'src\Bws.Core\Clock.cs'
        From = '    public void Wait(TimeSpan duration) => Thread.Sleep(duration);'
        To = '    public void Wait(TimeSpan duration)' + [char]10 + '    {' + [char]10 + '        try { Thread.Sleep(duration); } catch (ThreadInterruptedException) { }' + [char]10 + '    }'
        Test = 'SilentCatchGuards.Every_catch_that_drops_what_it_caught_is_counted_file_by_file'
    },
    @{
        # Re-anchored 2026-09-23 when the review of PR 7 made the read skip nested functions that
        # shadow the caught name - stale-check named it the moment the line changed.
        Name = 'silent catch: a filter reading the exception is not a use of it'
        File = 'tests\Bws.Architecture.Tests\SilentCatchGuards.cs'
        From = '&& clause.Block.DescendantNodes(node => !Shadows(node, caught))'
        To = '&& clause.DescendantNodes(node => !Shadows(node, caught))'
        Test = 'SilentCatchGuards.A_catch_drops_what_it_caught_only_when_it_neither_throws_nor_reads_it'
    },
    @{
        # THE MAJOR FINDING OF THE PR 7 REVIEW, put back. A throw inside a lambda that may never
        # run counted as the catch throwing, so a dropping catch left the inventory. A VALUE rather
        # than the call, so IsNestedFunction stays used and the build does not refuse it.
        Name = 'silent catch: a throw inside a nested function is not the catch throwing'
        File = 'tests\Bws.Architecture.Tests\SilentCatchGuards.cs'
        From = 'body.DescendantNodes(node => !IsNestedFunction(node))'
        To = 'body.DescendantNodes(node => IsNestedFunction(node) || true)'
        Test = 'SilentCatchGuards.A_catch_drops_what_it_caught_only_when_it_neither_throws_nor_reads_it'
    },
    @{
        # A nested parameter with the caught name read as the exception itself.
        Name = 'silent catch: a nested parameter with the caught name is not a read'
        File = 'tests\Bws.Architecture.Tests\SilentCatchGuards.cs'
        From = '&& clause.Block.DescendantNodes(node => !Shadows(node, caught))'
        To = '&& clause.Block.DescendantNodes(node => Shadows(node, caught) || true)'
        Test = 'SilentCatchGuards.A_catch_drops_what_it_caught_only_when_it_neither_throws_nor_reads_it'
    },
    @{
        # A shorter marker closing a longer fence - the inside of a four-backtick fence read as prose.
        Name = 'prose: a fence closes only on its own marker'
        File = 'tests\Bws.Architecture.Tests\Prose.cs'
        From = 'marker.Mark == Mark && marker.Length >= Length'
        To = 'marker.Mark == Mark'
        Test = 'ProseGuards.A_fence_closes_only_on_its_own_marker'
    },
    @{
        # The whole file excused again rather than the one quotation - the PR 7 review finding.
        Name = 'prose: only the registered quotation is excused from the language rule'
        File = 'tests\Bws.Architecture.Tests\ProseGuards.cs'
        From = '? line.Text.Replace(excuse.Quotation, " ", StringComparison.Ordinal)'
        To = '? string.Empty'
        Test = 'ProseGuards.Only_the_quotation_a_file_is_excused_for_is_excused'
    },
    @{
        # The same mutation as "a mention from a test is not life" above, answered by the scan-level
        # test the PR 7 review asked for rather than by the live exception list.
        Name = 'dead code: the scan records a test mention as a test'
        File = 'tests\Bws.Architecture.Tests\DeadCode.cs'
        From = 'var site = fromTests ? DeadCode.Test : SiteOf(name);'
        To = 'var site = fromTests ? DeadCode.Root : SiteOf(name);'
        Test = 'DeadCodeScanTests.A_type_only_a_test_names_is_reported_by_the_scan'
    },
    @{
        Name = 'prose: a semicolon in a comment is refused'
        File = 'src\Bws.Core\Clock.cs'
        From = '/// The real one.'
        To = '/// The real one; the only one.'
        Test = 'ProseGuards.No_prose_carries_a_semicolon'
    },
    @{
        Name = 'prose: a Polish word without its diacritics is refused'
        File = 'src\Bws.Core\Clock.cs'
        From = '/// The real one.'
        To = '/// The real one, tylko jeden.'
        Test = 'ProseGuards.No_comment_is_written_in_polish'
    },
    @{
        Name = 'prose: code in backticks is not prose'
        File = 'tests\Bws.Architecture.Tests\Prose.cs'
        From = '<c>.*?</c>|`[^`]*`|'
        To = '<c>.*?</c>|'
        Test = 'ProseGuards.A_semicolon_counts_only_in_the_prose_itself'
    },
    @{
        Name = 'prose: a code block in a documentation comment is not prose'
        File = 'tests\Bws.Architecture.Tests\Prose.cs'
        # A pattern that never matches rather than a removed line: the value changes, the code around
        # it does not, so the tree still compiles and the verdict means something.
        From = 'new("<code>.*?</code>", RegexOptions.Singleline, Sources.Ceiling)'
        To = 'new("<code>.*?</codeX>", RegexOptions.Singleline, Sources.Ceiling)'
        Test = 'ProseGuards.A_code_block_in_a_documentation_comment_is_not_prose_and_keeps_its_line_numbers'
    },
    @{
        Name = 'shape measure: a pattern combinator is a fork'
        File = 'tests\Bws.Architecture.Tests\ShapeMeasures.cs'
        From = '        SyntaxKind.AndPattern, SyntaxKind.OrPattern,'
        To = ''
        Test = 'CodeShapeMetricTests.Every_other_kind_of_decision_forks_once_too'
    },
    @{
        # Found by the property test on the day it was written, not by reading the code.
        #
        # Note what is NOT mutated here: removing the null check itself does not compile, because
        # the nullable analysis then refuses the dereference two lines down. That half of the
        # behaviour is held by the compiler, which is stronger than a test and worth knowing.
        # What a test has to hold is the other half - that a refusal hands back a reason and no
        # snapshot, never both.
        Name = 'snapshot: a damaged file gives back a reason and no snapshot'
        File = 'src\Bws.Core\Snapshots\SnapshotJson.cs'
        # Re-anchored 2026-08-04: the sentence this used to hang on moved to Snapshot.MissingParts,
        # so the anchor now takes the branch with it. The behaviour is unchanged - what is mutated
        # is still the line that keeps a refused read from handing back a snapshot as well.
        From = @'
        if (snapshot.MissingParts(out failure))
        {
            snapshot = null;
'@
        To = @'
        if (snapshot.MissingParts(out failure))
        {
'@
        Test = 'ParserPropertyTests.Reading_a_snapshot_never_fails_in_a_way_it_did_not_plan_for'
    },
    @{
        Name = 'dependencies: a package in a shipped project is refused'
        File = 'src\Bws.Core\Bws.Core.csproj'
        # A package that really exists, so the restore succeeds and the guard is what fails.
        # The first attempt named an invented one and came back BROKEN - a failed restore is
        # not a caught mutation.
        #
        # THE ANCHOR CARRIED THE VERSION NUMBER UNTIL 2026-09-22 AND WENT STALE THE FIRST TIME A
        # BOT MOVED IT. Dependabot's second pull request took CsWin32 from 0.3.298 to 0.3.333 in
        # this file, and this entry stopped matching anything - so the guard it exercises had no
        # canary at all and nothing said so until stale-check was next run. It is the only entry
        # of the 558 that ever anchored on a version, checked the day it was fixed, and the rule
        # it leaves behind is worth more than the fix: AN ANCHOR MUST NOT CONTAIN A NUMBER
        # SOMEBODY ELSE MAINTAINS. The name of the package is ours to keep, its version is not.
        From = '<PackageReference Include="Microsoft.Windows.CsWin32"'
        To = '<PackageReference Include="CsCheck" Version="4.7.0" />' + [char]10 + '    <PackageReference Include="Microsoft.Windows.CsWin32"'
        Test = 'DeclaredReferenceGuards.A_shipped_project_takes_only_the_packages_it_is_allowed'
    },
    @{
        Name = 'live list: a full reading keeps the rows it already had'
        File = 'src\Bws.Gui\ViewModels\RowIndex.cs'
        From = @'
            if (_byName.TryGetValue(entry.ServiceName, out var row))
            {
                row.Absorb(entry, now);
            }
            else
            {
                row = EntryRow.Of(entry);
                _byName[entry.ServiceName] = row;
            }
'@
        To = @'
            var row = EntryRow.Of(entry);
            _byName[entry.ServiceName] = row;
'@
        Test = 'MainViewModelTests.A_service_installed_from_outside_appears_with_all_of_its_columns'
    },

    # The three below all mutate XAML rather than C#, which is new here and is the point of
    # them: XAML is the one surface in this product where a mistake compiles, runs, and shows
    # the wrong thing. Two of the three break a colour and one breaks a borrowed name.
    @{
        # Found by tools/user-journey/journey.ps1 on its first run and shrunk to one character
        # by the property test. Nothing else in the project had anything to say about it: the
        # query parsed, nothing threw, and the answer was the whole machine.
        Name = 'query: a member that says nothing is refused rather than dropped'
        File = 'src\Bws.Core\Querying\QueryParser.cs'
        From = 'if (body.Length == 0 || body.Text.All(character => character == ''!''))'
        # Not `if (false)`, which was tried first and came back BROKEN: warnings are errors here
        # and a constant condition makes the block unreachable. A comparison that never holds
        # keeps the compiler quiet and removes the behaviour just as completely.
        To = 'if (body.Length == -1)'
        Test = 'QueryParserTests.A_word_of_nothing_but_exclamation_marks_is_a_mistake'
    },
    @{
        # The behaviour this restores was broken from the first release until 2026-08-02, and
        # UsageContractTests was green throughout - because it only asked about switches
        # somebody had remembered to list in it, and nobody had listed --help.
        # Followed help into Immediate.cs on 2026-08-05, when the two answers that need nothing
        # were split out of Program.cs at the size ratchet's request.
        Name = 'cli: asking for help is answered, not refused'
        File = 'src\Bws.Cli\Immediate.cs'
        From = 'if (options.Help)'
        To = 'if (options.Help && options.Rejected.Count > 99)'
        Test = 'UsageContractTests.Asking_how_to_use_it_is_answered_on_the_data_channel_and_is_not_a_failure'
    },
    @{
        Name = 'cli: a mistyped command is offered the nearest one'
        File = 'src\Bws.Cli\Suggestions.cs'
        From = 'return bestDistance <= Furthest ? best : null;'
        To = 'return null;'
        Test = 'UsageContractTests.A_mistyped_command_is_called_a_command_and_offered_the_nearest_one'
    },
    @{
        Name = 'appearance: a colour below its required contrast is refused'
        File = 'src\Bws.Gui\Themes\Colours.xaml'
        From = '<SolidColorBrush x:Key="MeaningRejected">#F47F88</SolidColorBrush>'
        To = '<SolidColorBrush x:Key="MeaningRejected">#E81123</SolidColorBrush>'
        Test = 'ContrastGuards.Every_declared_colour_clears_the_ratio_its_role_needs'
    },
    @{
        Name = 'appearance: a colour with no declared threshold is refused'
        File = 'src\Bws.Gui\Themes\Colours.xaml'
        From = '<SolidColorBrush x:Key="TextSubdued">'
        To = '<SolidColorBrush x:Key="MeaningNobodyDecided">#123456</SolidColorBrush><SolidColorBrush x:Key="TextSubdued">'
        Test = 'ContrastGuards.No_colour_is_declared_without_a_ratio_it_has_to_clear'
    },
    @{
        # TWO ENTRIES FOR THE SECOND COLUMN OF THE FLOOR TABLE, 2026-09-16 - the surface a brush is
        # measured against. Both mutations pass against the window and fail only where the brush
        # is drawn, so a guard that quietly went back to measuring everything against the window
        # would let both through. The hover for the panel surfaces set to the window's own hover:
        # 1.70 against the window, 1.30 against the panel, and 1.30 is the value the floor of 1.5
        # was chosen to refuse.
        Name = 'appearance: a hover for the panel that is only tellable on the window is refused'
        File = 'src\Bws.Gui\Themes\Surfaces.xaml'
        From = '<SolidColorBrush x:Key="SurfaceHoverOnPanel">#565656</SolidColorBrush>'
        To = '<SolidColorBrush x:Key="SurfaceHoverOnPanel">#454545</SolidColorBrush>'
        Test = 'ContrastGuards.Every_declared_colour_clears_the_ratio_its_role_needs'
    },
    @{
        # The subdued grey darkened to #8A8A8A: 4.72 against the window, which clears the 4.5 text
        # asks, and 3.61 against the panel, where the details panel's labels are drawn in it.
        Name = 'appearance: a text grey that reads on the window and not on the panel is refused'
        File = 'src\Bws.Gui\Themes\Colours.xaml'
        From = '<SolidColorBrush x:Key="TextSubdued">#A0A0A0</SolidColorBrush>'
        To = '<SolidColorBrush x:Key="TextSubdued">#8A8A8A</SolidColorBrush>'
        Test = 'ContrastGuards.Every_declared_colour_clears_the_ratio_its_role_needs'
    },
    @{
        # THREE ENTRIES FOR A SETTING NOBODY CAN SEE IN A TEST, and that is why they are here.
        # The owner reported on 2026-08-17 that dragging the scrollbar slowly felt like it snagged.
        # Three timing probes had already said the window was fast, and all three were right and
        # useless: they measure latency, and a jump that happens instantly is fast and jerky at
        # once. The fix was judged by his eye, so the only thing standing between it and a future
        # session deleting two attributes it does not recognise is these.
        #
        # The default is Item, which is what the grid did for its whole life before this - so the
        # mutation is not an invented value, it is the behaviour being restored.
        # THE START TYPES HAVE TO BE TELLABLE FROM EACH OTHER, 2026-08-17. Two of the four painted
        # the same colour is the exact defect MarkDistinctionGuards was written for in the first
        # place - "stopped" and "could not read" two points of grey apart - arriving in a column
        # that just gained four marks at once.
        Name = 'marks: two start types may not be painted the same'
        File = 'src\Bws.Gui\Themes\Colours.xaml'
        From = '<SolidColorBrush x:Key="StartAutomatic">#B58CD9</SolidColorBrush>'
        To = '<SolidColorBrush x:Key="StartAutomatic">#7FA6D9</SolidColorBrush>'
        Test = 'MarkDistinctionGuards.No_two_marks_differ_only_by_a_shade_of_the_same_colour'
    },
    @{
        # THE OTHER HALF, AND THE ONE THE THEME GUARD CANNOT SEE. Collapsing a start type back to
        # the no-mark code leaves Cells.xaml holding four triggers that answer to codes nothing
        # produces any more - the theme still looks right and the column goes back to saying
        # nothing. A guard that reads markup cannot notice a switch that stopped reaching it.
        # THE ORDER OF THE TWO THINGS ESCAPE DOES, 2026-08-17. Swapped, one press shuts the panel
        # AND throws away the query somebody typed - and every test about the panel alone, and
        # every test about the query alone, stays green over it. Only the sequence can see it.
        # THE ONE WORD THAT KEEPS THE LIBRARY'S TOOLTIP - trap 4 of docs/10. Without BasedOn our
        # style REPLACES theirs rather than extending it, so the tooltip goes on wrapping while
        # losing its background, border and shadow. Nothing else in this suite looks at a popup.
        Name = 'appearance: the tooltip extends the librarys style rather than replacing it'
        File = 'src\Bws.Gui\Themes\Controls.xaml'
        From = '<Style TargetType="ToolTip" BasedOn="{StaticResource DefaultToolTipStyle}">'
        To = '<Style TargetType="ToolTip">'
        Test = 'ToolTipGuards.The_tooltip_style_extends_the_librarys_rather_than_replacing_it'
    },
    @{
        Name = 'appearance: a long tooltip wraps instead of running off the screen'
        File = 'src\Bws.Gui\Themes\Controls.xaml'
        From = '<TextBlock Text="{Binding}" TextWrapping="Wrap" />'
        To = '<TextBlock Text="{Binding}" TextWrapping="NoWrap" />'
        Test = 'ToolTipGuards.A_tooltip_is_bounded_and_its_content_wraps'
    },
    @{
        Name = 'window: Escape shuts the panel before it touches the query'
        File = 'src\Bws.Gui\MainWindow.Keyboard.cs'
        #
        # RE-ANCHORED 2026-08-18: the plan panel joined the front of this queue in packet 2, so the
        # line grew a third call. What is asserted is unchanged - a panel goes before the query.
        # RE-ANCHORED 2026-09-16: the list under the search box joined the FRONT of this queue.
        # What is asserted is unchanged - a panel goes before the query.
        # RE-ANCHORED 2026-09-29: the chain went onto five lines and a run that is going now takes the
        # press first (G-1). What is asserted is unchanged.
        # RE-ANCHORED 2026-10-14: a comparison with a snapshot joined the queue beside the panel, and
        # the two went behind one question to the model, PutAwayAView. What is asserted is unchanged.
        From = '                return _model.Suggesting.Close()
                    || _model.Planned.Busy
                    || _model.Planned.Hide()
                    || _model.PutAwayAView()
                    || _model.ClearQuery();'
        To = '                return _model.Suggesting.Close()
                    || _model.ClearQuery()
                    || _model.Planned.Busy
                    || _model.Planned.Hide()
                    || _model.PutAwayAView();'
        Test = 'CopyingAndPanelGuards.Escape_closes_the_panel_before_it_clears_the_query'
    },
    @{
        # Ctrl+C copying NOTHING while reporting success is the silence rule 8 forbids, arriving
        # through a shortcut. Until 2026-08-17 nothing checked it, because a sentence in
        # WindowGuards said the clipboard could not be tested from here. It can.
        Name = 'window: Ctrl+C copies the entry rather than quietly copying nothing'
        File = 'src\Bws.Gui\MainWindow.Keyboard.cs'
        #
        # RE-ANCHORED 2026-08-18, packet 2 of `S7`. The handler used to reach into Chosen for the one
        # chosen row. Copying moved out of that class when the grid started taking more than one row,
        # so this moved with it - and stale-check reported the old anchor in seconds, before the
        # mutation run itself would have.
        From = 'return Copy(Copying.Everything);'
        To = 'return Copy(rows => string.Empty);'
        Test = 'CopyingAndPanelGuards.Control_C_copies_the_whole_entry_or_says_it_could_not'
    },
    @{
        Name = 'marks: every start type still has a mark of its own'
        File = 'src\Bws.Gui\ViewModels\CellFaces.cs'
        From = 'StartType.Automatic => CellShapes.StartAutomatic,'
        To = 'StartType.Automatic => CellShapes.Ordinary,'
        Test = 'CellFaceTests.Each_start_type_wears_a_mark_of_its_own'
    },
    @{
        # TURNED ROUND ON 2026-08-31 WITH THE SETTING ITSELF. Pixel was bought in August to stop a
        # slow drag jumping and was given back when what it cost was measured - 22-38 ms of
        # processor per warm step against 8-17 - and the owner compared both builds by hand.
        Name = 'list: the list scrolls by whole rows rather than by pixel'
        File = 'src\Bws.Gui\MainWindow.xaml'
        From = 'VirtualizingPanel.ScrollUnit="Item"'
        To = 'VirtualizingPanel.ScrollUnit="Pixel"'
        Test = 'ListScrollingGuards.The_list_scrolls_by_whole_rows_rather_than_by_pixel'
    },
    @{
        Name = 'list: row containers are reused rather than rebuilt on every scroll'
        File = 'src\Bws.Gui\MainWindow.xaml'
        From = 'VirtualizingPanel.VirtualizationMode="Recycling"'
        To = 'VirtualizingPanel.VirtualizationMode="Standard"'
        Test = 'ListScrollingGuards.The_list_reuses_its_row_containers_instead_of_building_new_ones'
    },
    @{
        # INJECTED RATHER THAN REMOVED, because row virtualisation is on by default and there is
        # nothing in the file to break. The same shape the privacy guards use: a guard that watches
        # for an absence can only be proved by introducing the thing it is watching for.
        Name = 'list: the grid still virtualises its rows at all'
        File = 'src\Bws.Gui\MainWindow.xaml'
        From = 'VirtualizingPanel.ScrollUnit="Item"'
        To = 'EnableRowVirtualization="False" VirtualizingPanel.ScrollUnit="Item"'
        Test = 'ListScrollingGuards.The_list_still_virtualises_its_rows_at_all'
    },
    @{
        # NULL RATHER THAN A DIFFERENT COLOUR, because the fault this guards is not a wrong shade -
        # it is a row with no brush at all, which takes no mouse hits and turns the row into a hole.
        # Backlog 221 sampled that as 106 of 603 points landing on a cell border instead.
        Name = 'list: a row carries a brush so the pointer lands on the row rather than through it'
        File = 'src\Bws.Gui\Themes\List.xaml'
        From = '<Setter Property="Background" Value="{x:Static media:Brushes.Transparent}" />'
        To = '<Setter Property="Background" Value="{x:Null}" />'
        Test = 'RowStateGuards.A_row_carries_a_brush_of_its_own_so_the_pointer_lands_on_it'
    },
    @{
        # THE KEY STOPS BEING KEPT AT ALL. Removing instead of storing leaves every call building
        # the key again, which is the state this cache was added to leave - and it is a plausible
        # edit, not a contrived one: somebody clearing a stale entry rather than replacing it.
        Name = 'sorting: the key a row sorts by is worked out once rather than once per comparison'
        File = 'src\Bws.Gui\ViewModels\CellOrder.cs'
        From = '        _keys[entry] = (entry.Version, key);'
        To = '        _keys.Remove(entry);'
        Test = 'ColumnOrderGuards.The_key_a_row_sorts_by_is_worked_out_once_rather_than_once_per_comparison'
    },
    @{
        # AND THE OTHER WAY THIS BREAKS, WHICH IS THE ONE THAT WOULD NEVER LOOK BROKEN. Dropping the
        # version check leaves a cache that answers for a row as it was when first asked, so a live
        # list sorts by values that have since changed - in an order that still reads as correct,
        # with every cell on screen showing the truth.
        Name = 'sorting: a row that changed is sorted by what it says now'
        File = 'src\Bws.Gui\ViewModels\CellOrder.cs'
        From = 'if (_keys.TryGetValue(entry, out var kept) && kept.Version == entry.Version)'
        To = 'if (_keys.TryGetValue(entry, out var kept))'
        Test = 'ColumnOrderGuards.A_row_that_changed_is_sorted_by_what_it_says_now'
    },
    @{
        # THE CHIP OWNS ITS OWN TEMPLATE SINCE 2026-09-01, so every state it has is this product's
        # job. A state dropped from that template is invisible in every other way - green build,
        # window draws, chip works, and one of the four ways it answers a person stops happening.
        Name = 'appearance: a chip that is already on still answers the pointer'
        File = 'src\Bws.Gui\Themes\Chips.xaml'
        From = '                                <Condition Property="IsMouseOver" Value="True" />'
        To = ''
        Test = 'ChipGuards.A_chip_keeps_every_state_it_took_over_from_the_library'
    },
    @{
        # The pill is the signal carrying most of the separation from the action button beside it,
        # and it is the only one of the three that cannot be reached with a setter. Measured before
        # the change: chip 47 pixels tall, action button 48, same corner, same text size.
        Name = 'appearance: a chip is a pill rather than the same rectangle as an action button'
        File = 'src\Bws.Gui\Themes\Values.xaml'
        From = '<CornerRadius x:Key="CornerChip">14</CornerRadius>'
        To = '<CornerRadius x:Key="CornerChip">4</CornerRadius>'
        Test = 'ChipGuards.A_chip_is_rounded_far_enough_to_read_as_a_pill_rather_than_a_button'
    },
    @{
        # WPF UI marks checked with a fill a shade lighter than not-checked against a window that is
        # already dark - `docs/11` complaint 1, the one state this row exists to show carried in the
        # least visible difference there is.
        Name = 'appearance: a chip that is on wears the colour that means picked'
        File = 'src\Bws.Gui\Themes\Chips.xaml'
        From = @'
                            <Setter TargetName="Pill" Property="Background"
                                    Value="{StaticResource SurfaceSelected}" />
'@
        To = @'
                            <Setter TargetName="Pill" Property="Background"
                                    Value="{StaticResource SurfaceHover}" />
'@
        Test = 'ChipGuards.A_chip_that_is_on_is_painted_in_the_colour_that_means_picked'
    },
    @{
        # THE WHOLE PALETTE STUDY OF 2026-09-01 COMES DOWN TO THIS ONE COLOUR. Before it the single
        # saturated colour in the window did three jobs at once, so a button meant nothing. A second
        # brighter blue was measured and rejected at 1.02 against the selection colour.
        Name = 'appearance: the primary action wears the colour that means press this'
        File = 'src\Bws.Gui\Themes\Controls.xaml'
        From = @'
                    <Border x:Name="Face"
                            Background="{StaticResource SurfacePrimaryAction}"
'@
        To = @'
                    <Border x:Name="Face"
                            Background="{StaticResource SurfacePanel}"
'@
        Test = 'ButtonGuards.The_primary_action_wears_the_colour_that_means_press_this'
    },
    @{
        # The button at the foot of a plan is disabled exactly when the session cannot carry anything out, which is
        # the moment somebody most needs telling. An accent a shade darker reads as a button still
        # waiting to be pressed.
        Name = 'appearance: a primary action that cannot be pressed stops looking pressable'
        File = 'src\Bws.Gui\Themes\Controls.xaml'
        From = @'
                            <Setter TargetName="Face" Property="Background"
                                    Value="{x:Static media:Brushes.Transparent}" />
'@
        To = @'
                            <Setter TargetName="Face" Property="Background"
                                    Value="{StaticResource SurfacePrimaryAction}" />
'@
        Test = 'ButtonGuards.The_primary_action_stops_looking_pressable_when_it_cannot_be_pressed'
    },
    @{
        # Read off a capture of the real window before this style existed: the library's thumb was
        # five device pixels across, about three logical, against the 24 WCAG 2.2 SC 2.5.8 asks.
        Name = 'appearance: the scrollbar is wide enough to aim at'
        File = 'src\Bws.Gui\Themes\Values.xaml'
        From = '<system:Double x:Key="WidthScrollBar">12</system:Double>'
        To = '<system:Double x:Key="WidthScrollBar">4</system:Double>'
        Test = 'ButtonGuards.The_scrollbar_is_wide_enough_to_aim_at'
    },
    @{
        # THE FLOOR IS A FRAMEWORK RESOURCE AND NOT A PROPERTY OF OURS, which is the entire reason
        # this entry anchors where it does. Track works the thumb's length out while arranging and
        # floors it at half of what TryFindResource hands back for the scrollbar button - so five
        # attempts at MinHeight, at Height and at a template all satisfied their own assertions and
        # moved nothing on screen, and `docs/10` trap 17 recorded the whole thing as impossible for
        # a day. Halving the framework key is therefore the only edit that really removes the
        # behaviour: it leaves our own LengthScrollThumbLeast saying 44 while the bar arranges 16.1,
        # which is exactly the shape the shipped defect had.
        Name = 'appearance: the scrollbar thumb has a floor under its LENGTH'
        File = 'src\Bws.Gui\Themes\Values.xaml'
        From = '<system:Double x:Key="{x:Static windows:SystemParameters.VerticalScrollBarButtonHeightKey}">72</system:Double>'
        To = '<system:Double x:Key="{x:Static windows:SystemParameters.VerticalScrollBarButtonHeightKey}">24</system:Double>'
        Test = 'ButtonGuards.The_scrollbar_thumb_is_long_enough_to_catch'
    },
    @{
        Name = 'appearance: a name borrowed from the control library must still exist'
        File = 'src\Bws.Gui\Themes\Controls.xaml'
        From = 'BasedOn="{StaticResource DefaultTextBoxStyle}"'
        To = 'BasedOn="{StaticResource DefaultTextBoxStyleTheyRenamed}"'
        Test = 'WpfUiContractGuards.Every_name_this_product_borrows_from_the_library_still_exists'
    },

    # THE THREE BELOW INJECT THE DRIFT RATHER THAN BREAKING THE GUARD, which is the shape every
    # guard over an ABSENCE needs - the same pattern the privacy guards use, where a mutation
    # plants a leak because a clean repository gives the guard nothing to find. All three are legal
    # XAML that compiles and changes nothing on screen, so the only thing that may go red is the
    # claim each theme file makes about itself in its own first paragraph.
    #
    # ONE PER FILE, NOT ONE PER DIRECTION, AND THAT IS THE LESSON OF THE SECOND SPLIT. Two entries
    # covered two files exactly while there were two. The moment there were three, a guard that had
    # quietly gone back to naming two files by hand would have passed - so the entries follow the
    # files rather than the argument, and the guard loops rather than naming anything.
    @{
        # Values.xaml promises it holds no style. A style merged there resolves its names against
        # a dictionary that has not been merged yet, which is the failure the merge order exists
        # to prevent - and it would be invisible, because a keyed style nobody references is a
        # style nothing renders.
        Name = 'appearance: no style drifts into the file that holds the values'
        File = 'src\Bws.Gui\Themes\Values.xaml'
        From = '<system:Double x:Key="SizeNone">0</system:Double>'
        To = '<system:Double x:Key="SizeNone">0</system:Double><Style x:Key="DriftedStyle" TargetType="TextBlock" />'
        Test = 'AppearanceGuards.The_seams_between_the_theme_files_are_where_all_of_them_say_they_are'
    },
    @{
        # And the other direction. Thickness rather than system:Double on purpose: neither styles
        # file declares the system prefix, so a Double there would fail to compile and the entry
        # would come back BROKEN instead of caught - an entry that cannot build proves nothing
        # about the guard it names.
        Name = 'appearance: no value drifts into the file that holds the list'
        File = 'src\Bws.Gui\Themes\List.xaml'
        From = '<Style x:Key="ListGrid" TargetType="DataGrid" BasedOn="{StaticResource DefaultDataGridStyle}">'
        To = '<Thickness x:Key="DriftedValue">1</Thickness><Style x:Key="ListGrid" TargetType="DataGrid" BasedOn="{StaticResource DefaultDataGridStyle}">'
        Test = 'AppearanceGuards.The_seams_between_the_theme_files_are_where_all_of_them_say_they_are'
    },
    @{
        # The same direction again, in the file the text styles left for on 2026-08-25, because the
        # guard walks a list and a list is walked wrongly more easily than two names are mistyped.
        Name = 'appearance: no value drifts into the file that holds the text styles'
        File = 'src\Bws.Gui\Themes\Text.xaml'
        From = '<Style x:Key="HeadingText" TargetType="TextBlock">'
        To = '<Thickness x:Key="DriftedValue">1</Thickness><Style x:Key="HeadingText" TargetType="TextBlock">'
        Test = 'AppearanceGuards.The_seams_between_the_theme_files_are_where_all_of_them_say_they_are'
    },
    @{
        # THE MUTATION IS A TYPO, BECAUSE A TYPO IS THE WHOLE RISK OF THIS MECHANISM. A column
        # ceiling is found by naming convention - ListColumns asks TryFindResource for the width key
        # with "Ceiling" after it - so one wrong letter is an error in no build and no test. The
        # column just goes on growing, and the only way to see it is to maximise the window and
        # measure the columns, which is what nobody had ever done until 2026-09-08.
        #
        # ColumnAccounts, plural, names no width, so the guard has to say the cap caps nothing.
        Name = 'appearance: a column ceiling names a share that really exists'
        File = 'src\Bws.Gui\Themes\Columns.xaml'
        From = '<system:Double x:Key="ColumnAccountCeiling">280</system:Double>'
        To = '<system:Double x:Key="ColumnAccountsCeiling">280</system:Double>'
        Test = 'AppearanceGuards.A_column_ceiling_names_a_share_that_exists_and_sits_above_the_floor'
    },

    # THE FOUR BELOW REDDEN A GUARD THAT LOOKS AT A RENDERED PIXEL, WHICH IS NEW IN THIS PRODUCT
    # ON 2026-08-05. Everything above asks a property or a file what it says. RowStateGuards
    # renders the list and reads the colour back, which is the only question that would have
    # caught the fault it was built for: three row states set on DataGridRow.Background, fed to
    # the border by a TemplateBinding, and overwritten every frame by a trigger inside WPF UI's
    # own template. Every property read said the colour was there and no pixel ever had it.
    @{
        # THE ORIGINAL FAULT WAS PUT BACK HERE UNTIL 2026-09-01, AND IT STOPPED BEING A FAULT.
        # Dropping TargetName sent the colour to the row's own Background - the dead path - and on
        # 2026-08-05 that rendered #FF202020, the window background, instead of #FF3F7099.
        #
        # IT CAME BACK MISSED FROM THE FIRST FULL GATE AFTER 2026-08-31, and the cause is that
        # session's own repair rather than a guard going soft: the row is now the hit surface
        # through a Setter, and the template feeds `Background` straight to `RowSurface` with a
        # TemplateBinding - List.xaml says so in as many words. So the dead path is alive, the
        # mutation reaches the pixel, and the guard is right to stay green.
        #
        # THE CLAIM IS GONE, SO THE ENTRY NAMES A DIFFERENT ONE rather than being tuned until it
        # reddens again. What the test is called is what this now breaks: take the paint away and a
        # selected row is not painted in our colour at all. The Foreground setter beside it stays,
        # so the markup is still valid and only the one thing under test changes.
        Name = 'row states: a selected row is painted in our colour'
        File = 'src\Bws.Gui\Themes\List.xaml'
        From = @'
                            <Setter TargetName="RowSurface"
                                    Property="Background"
                                    Value="{StaticResource SurfaceSelected}" />
                            <Setter Property="Foreground" Value="{StaticResource TextOnSurface}" />
'@
        To = @'
                            <Setter Property="Foreground" Value="{StaticResource TextOnSurface}" />
'@
        Test = 'RowStateGuards.A_selected_row_is_painted_in_our_colour'
    },
    @{
        # The state WPF UI's template had nothing at all for, so before the rewrite whatever it
        # looked like on screen was an ordinary row.
        Name = 'row states: a row that moved a moment ago is painted'
        File = 'src\Bws.Gui\Themes\List.xaml'
        From = '<DataTrigger Binding="{Binding RecentlyChanged}" Value="True">'
        To = '<DataTrigger Binding="{Binding RecentlyChanged}" Value="False">'
        Test = 'RowStateGuards.A_row_that_moved_a_moment_ago_is_painted_at_all'
    },
    @{
        # THE EVEN CLAIM, WHICH IS THE HALF BACKLOG 128 ASKS FOR BY NAME. Inverting the trigger
        # above paints every untouched row instead, and a suite that only ever asserts a colour
        # IS present would stay green through it.
        Name = 'row states: an untouched row is left alone'
        File = 'src\Bws.Gui\Themes\List.xaml'
        From = '<DataTrigger Binding="{Binding RecentlyChanged}" Value="True">'
        To = '<DataTrigger Binding="{Binding RecentlyChanged}" Value="False">'
        Test = 'RowStateGuards.An_untouched_row_is_painted_in_neither_of_them'
    },
    @{
        # Where the setter lives is the whole lesson of this slice, so the focus ring is guarded
        # on that rather than on its colour. A ring moved back onto the row would be the fifth
        # time this product made the same mistake.
        Name = 'row states: the focus ring is set from inside the template'
        File = 'src\Bws.Gui\Themes\List.xaml'
        From = '<Setter TargetName="FocusOutline" Property="Visibility" Value="Visible" />'
        To = '<Setter Property="Tag" Value="{StaticResource FocusRing}" />'
        Test = 'RowStateGuards.The_focus_ring_is_set_from_inside_the_template_rather_than_from_a_style_trigger'
    },
    @{
        # A COLOUR THAT PASSES THE OLD GUARD AND FAILS THE NEW ONE, chosen for exactly that.
        # Measured: #808080 gives 4.12 against the window, so the table-driven test stays green -
        # and 1.33 on a selected row, 1.82 on one that just moved, 2.43 under the pointer. A ring
        # is drawn on top of those, not on the window.
        Name = 'appearance: the focus ring answers to every surface a row can take'
        File = 'src\Bws.Gui\Themes\Surfaces.xaml'
        From = '<SolidColorBrush x:Key="FocusRing">#D6D6D6</SolidColorBrush>'
        To = '<SolidColorBrush x:Key="FocusRing">#808080</SolidColorBrush>'
        Test = 'ContrastGuards.The_focus_ring_clears_its_ratio_against_every_surface_a_row_can_take'
    },
    @{
        # Ctrl+Shift+F and Ctrl+Alt+F belong to whatever claims them. A program that swallows
        # every combination merely CONTAINING Control breaks shortcuts it never heard of.
        Name = 'keyboard: only the exact modifier claims a key'
        File = 'src\Bws.Gui\Shortcuts.cs'
        From = 'if (key == Key.F && modifiers == ModifierKeys.Control)'
        To = 'if (key == Key.F && modifiers.HasFlag(ModifierKeys.Control))'
        Test = 'KeyboardTests.Anything_else_is_left_alone'
    },
    @{
        # Escape is taken before anything else in the window sees it, so an empty box has to hand
        # it back. Otherwise it becomes a key that silently stops working for whatever needs it.
        Name = 'keyboard: escape on an empty query is handed back'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.cs'
        From = @'
        if (_queryText.Length == 0)
        {
            return false;
        }
'@
        To = ''
        Test = 'KeyboardTests.Escape_on_an_empty_query_does_nothing_and_admits_it'
    },
    @{
        # Rule 8 where it is easy to think it does not apply: a copy that quietly did nothing
        # leaves somebody pasting the previous thing into a command that stops a service.
        # Followed Problem into Says.cs on 2026-08-05, when the empty states arrived and
        # everything the window SAYS became one object.
        Name = 'window: an action that could not happen is said out loud'
        File = 'src\Bws.Gui\ViewModels\Says.cs'
        # Re-anchored 2026-08-13 at S6d3, when a third sentence joined this line: what a kept
        # column layout could not give the window. The mutation is unchanged in meaning - the
        # refusal stops being said - and the layout note stays where it is so that this entry
        # keeps proving one thing rather than two.
        #
        # RE-ANCHORED AGAIN 2026-09-23 (UX-GUI-002): the query's mistake left this line for one of
        # its own under the search box, so the refusal and the layout note are all that is left.
        From = @'
    public string Problem => _refusal.Length > 0 ? _refusal : _layout;
'@
        To = @'
    public string Problem => _layout;
'@
        Test = 'RefusalTests.A_refusal_is_shown_rather_than_swallowed'
    },
    @{
        # And it goes away when the person asks for something else, which is the only signal
        # there is that they have moved on.
        Name = 'window: a refusal is put away when something else is asked for'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.cs'
        From = '                Says.Moved();'
        To = ''
        Test = 'RefusalTests.Asking_for_something_else_puts_the_refusal_away'
    },

    # ---- The empty states, 2026-08-05, appearance queue slice 5 ----
    #
    # Complaint 9 of the eleven, and the four below guard the distinctions rather than the words.
    # An empty rectangle saying nothing is one fault. A rectangle confidently saying the WRONG
    # one of five things is worse, because it sends somebody to fix a query when the manager
    # would not open.
    # ---- The two directions of a disagreement, backlog 170 and 172, 2026-08-18 ----
    @{
        # Widening this to any start type is the mistake the decision refused: the entry would then
        # be reported as running-while-disabled for every Manual service that happens to be up,
        # which is most of a machine. It is the shape a boolean takes when somebody makes it answer
        # a second question.
        Name = 'mismatch: running while disabled means DISABLED, not any start type'
        File = 'src\Bws.Core\ScmEntry.cs'
        From = 'StartType.Value == Core.StartType.Disabled && Status == EntryStatus.Running'
        To = 'Status == EntryStatus.Running'
        Test = 'ScmEntryTests.A_disabled_entry_that_is_running_anyway_is_its_own_signal'
    },
    @{
        # An entry that agrees with itself has to answer ABSENT, not present-with-no-symbols - and
        # this was the first version's actual fault rather than an invented one. Present makes
        # `mismatch:any` match every entry anybody could judge, including the eight hundred that are
        # fine, because `any` is asked of the OUTCOME rather than of the symbols. A filter that
        # filters nothing looks exactly like one that worked.
        Name = 'mismatch: an entry that agrees with itself is absent, not present with nothing'
        File = 'src\Bws.Core\Querying\QuerySymbols.cs'
        # Re-anchored 2026-08-26: the line it named was rewritten when absent stopped being folded
        # in with not-read - backlog 235. The claim it holds is unchanged, so only the anchor moved.
        From = '        return disagrees ? ReadOutcome.Present : ReadOutcome.Absent;'
        To = '        return ReadOutcome.Present;'
        Test = 'MismatchQueryTests.Any_covers_both_directions_and_none_covers_neither'
    },
    @{
        # The chip that asks for signatures, turned into one that asks for something already read.
        # Everything stays green except the join: the family still renders, the query still filters,
        # and the window simply never goes and looks - which is the state backlog 162 describes as
        # "an empty list plus a sentence saying nobody checked".
        Name = 'chips: turning on the signature chip is what sends the window to read signatures'
        File = 'src\Bws.Gui\ViewModels\FilterChips.cs'
        From = 'new FilterChip("gui.filter.signedNo", "signed", "no", negated: false, read, write),'
        To = 'new FilterChip("gui.filter.signedNo", "status", "stopped", negated: false, read, write),'
        Test = 'SecondPhaseTests.Turning_on_the_signature_chip_is_what_sends_the_window_to_read_them'
    },
    # ---- Rule 1 of the untouchable rules, backlog 34, 2026-08-18 ----
    @{
        # The writer built into a variable of its own instead of straight into the plan. It still
        # compiles, still runs, and is exactly the first step towards a call that skips the plan -
        # which is how this rule is described as eroding: one exception at a time, each reasonable.
        Name = 'plan: the writer is never held loose, only handed to a plan'
        File = 'src\Bws.Cli\Execution.cs'
        From = '            return new PlanRunner(new WindowsScmControl(), new SystemClock()).Run('
        To = '            var loose = new WindowsScmControl();' + [char]13 + [char]10 + '            return new PlanRunner(loose, new SystemClock()).Run('
        Test = 'PlanOnlyGuards.Every_writer_that_is_built_goes_straight_into_a_plan_runner'
    },
    @{
        # INJECTS rather than breaks, the same shape as the privacy guards, and for the same reason:
        # the list of places allowed to build the writer has exactly one entry on a healthy tree, so
        # there is nothing to break. What proves it is a SECOND place appearing.
        #
        # A discard rather than an unused local, because this build treats warnings as errors and a
        # mutation that fails to compile reports itself as uncaught while proving nothing.
        Name = 'plan: a second place building the writer is refused'
        File = 'src\Bws.Cli\Program.cs'
        From = '        var needs = parsed.Query!.Needs;'
        To = '        _ = new WindowsScmControl();' + [char]13 + [char]10 + '        var needs = parsed.Query!.Needs;'
        Test = 'PlanOnlyGuards.The_writer_is_only_ever_built_where_a_plan_will_carry_it'
    },
    @{
        # A LIST WITH NOTHING IN IT AGAINST A QUERY THAT MATCHED NOTHING, which is the branch above
        # one level in. The mutation restores what the window did until 2026-08-19: on a machine
        # with no drivers it blamed somebody for a query they had not written and offered to clear
        # an empty box.
        Name = 'empty state: a list with nothing in it is not a query that matched nothing'
        File = 'src\Bws.Gui\ViewModels\ListState.cs'
        From = '        if (inScope == 0)'
        To = '        if (inScope == -1)'
        Test = 'EmptyStateTests.A_list_with_nothing_in_it_says_that_rather_than_blaming_the_query'
    },
    @{
        # BACKLOG 214. Both halves working and excluding each other - the one state the drivers
        # switch could not reach while it WAS the query. Without this branch the window says nothing
        # matched, which sends somebody to edit a query that is doing exactly what they asked.
        Name = 'empty state: a query asking for the other list says which list it asks for'
        File = 'src\Bws.Gui\ViewModels\ListState.cs'
        From = '        if (askedElsewhere)'
        To = '        if (askedElsewhere && shown > 0)'
        Test = 'EmptyStateTests.A_query_asking_for_the_other_list_says_which_list_it_is_asking_for'
    },
    @{
        # THE REGRESSION THIS SHIPPED WITH FOR HALF A DAY. The way out said "press Esc and see
        # everything again", which stopped being true the moment hiding drivers left the query - and
        # nothing in this project could have caught a sentence that merely became false.
        Name = 'empty state: the way out does not promise the whole machine back'
        File = 'src\Bws.Gui\ViewModels\ListState.cs'
        From = '        return scope == EntryScope.Everything'
        To = '        return scope == EntryScope.Everything || true'
        Test = 'EmptyStateTests.The_way_out_of_an_empty_list_does_not_promise_the_whole_machine_back'
    },
    @{
        # A machine with nothing on it against a query with nothing to show. Collapsing the two
        # would have the window offer to clear a query nobody typed.
        Name = 'empty state: a machine with nothing on it is not a query that matched nothing'
        File = 'src\Bws.Gui\ViewModels\ListState.cs'
        From = 'if (everything == 0)'
        To = 'if (everything == -1)'
        Test = 'EmptyStateTests.A_machine_that_hands_over_nothing_is_not_a_query_that_matched_nothing'
    },
    @{
        # The order of the questions is the answer here. Asked after the others, a failed reading
        # comes out as "nothing matched" - the window blaming the person for the machine.
        Name = 'empty state: a failed reading is not blamed on the query'
        File = 'src\Bws.Gui\ViewModels\ListState.cs'
        From = '        if (failed)'
        To = '        if (failed && shown > 0)'
        Test = 'EmptyStateTests.A_reading_that_failed_says_so_rather_than_blaming_the_query'
    },

    # A TOMBSTONE, AND IT REPLACES AN ENTRY RATHER THAN JOINING THE LIST. What stood here broke
    # the line in the Reading setter that recomputed the empty-state face as the flag dropped.
    # That line was DELETED on 2026-08-18, backlog 196, and the entry was retired with it.
    #
    # WHY, because "the guard went missing" and "the behaviour went away" look identical from a
    # registry and only one of them is allowed. The face used to follow the reading flag through
    # a proxy - `everything == 0` standing in for "has any reading ever finished" - and the proxy
    # was wrong on the machine that hands over nothing, where the empty sentence flickered once a
    # second. Replacing the proxy with a real bit made the recompute unable to change an answer
    # on ANY path: every path that lowers the flag has already raised that bit or set the failed
    # flag. So the line was dead, and a mutation against dead code comes back MISSED by
    # construction while looking like a guard that lapsed.
    #
    # THE OWNER WAS ASKED BEFORE THIS WAS DONE, 2026-08-18, because the standing rule is to fix
    # the sentence or the guard and NEVER the mutation - and retiring an entry looks like exactly
    # what that rule forbids. It is allowed here only because the behaviour was deliberately
    # removed rather than left unguarded. The entry below guards what replaced it.
    @{
        # The fifth face, and the half the 2026-08-13 fix left out. Breaking this restores the
        # flicker on a machine with no entries: the bit stops being raised by the full reading,
        # so the window never stops calling it the first look.
        #
        # NOT the obvious mutation, and the reason is a trap this registry has been caught by
        # before. Rewriting FirstLook to drop `!_everRead` would leave the field assigned and
        # never read, which is CS0414, and the build treats warnings as errors - so that
        # mutation would fail to COMPILE and report itself as uncaught while proving nothing.
        Name = 'empty state: the bit is raised where the manager answered, not where the reading ended'
        File = 'src\Bws.Gui\ViewModels\Readings.cs'
        From = @'
        _everRead = true;

        _index.Absorb(entries);
'@
        To = @'
        _index.Absorb(entries);
'@
        Test = 'EmptyStateTests.A_refresh_does_not_take_the_empty_state_off_the_screen_on_an_empty_machine'
    },
    @{
        # Complaint 7: you cannot see THAT it is filtering. The count line wears it as a weight,
        # which is seen without being read.
        # ---- Text with no audience, 2026-08-05 ----
        #
        # THE FIRST TWO INJECT rather than break, because both guards are EMPTY on a clean
        # repository - the same shape as the privacy guards. Breaking their detection would prove
        # nothing about a repository that has nothing to detect.
        Name = 'text keys: a sentence that reaches no screen is refused'
        File = 'src\Bws.Gui\Resources\gui.en.json'
        From = '  "gui.cell.unknown": "unknown",'
        To = '  "gui.orphan.nobodySaysThis": "This sentence has no audience.",' + [char]10 + '  "gui.cell.unknown": "unknown",'
        Test = 'TextKeyGuards.Every_sentence_the_window_can_say_is_said_somewhere'
    },
    @{
        # The other direction, which fails differently: a key that is asked for and not declared
        # renders as the key itself, so the person sees gui.status.read on their screen and
        # nothing on the way there objects.
        Name = 'text keys: a sentence asked for and never declared is refused'
        File = 'src\Bws.Gui\ViewModels\ListState.cs'
        From = 'Texts.Of("gui.empty.loading")'
        To = 'Texts.Of("gui.empty.loadingThatIsNotDeclared")'
        Test = 'TextKeyGuards.Every_place_that_asks_for_a_sentence_asks_for_one_that_exists'
    },
    @{
        # The sentence this pair was built for. It existed, was translated, shipped, and reached
        # no screen - a window that knew the list was short and said nothing, which is the one
        # thing rule 8 forbids.
        # RE-ANCHORED 2026-09-23 (UX-GUI-009): the sentence is its own piece now rather than the
        # first note in a list, so the condition became the choice of a value. Same meaning - the
        # sentence stops being said while the rights are missing.
        # RE-ANCHORED 2026-10-06 (security report S-2): the sentence moved into Sentences.Rights, which
        # also says what an elevated session carries. Same meaning again - without rights, silence.
        # RE-ANCHORED 2026-10-06 (security report S-9): Rights became statements when it gained the
        # translation left out. Same meaning a third time.
        Name = 'admissions: a session without rights is told the list is short'
        File = 'src\Bws.Gui\ViewModels\Sentences.cs'
        From = '        if (!elevated)'
        To = '        if (!elevated && settings.Count < 0)'
        Test = 'AdmissionTests.A_session_without_administrator_rights_is_told_the_list_is_short'
    },
    @{
        # Two numbers that move independently, and a log carrying one of them cannot say which
        # moved. This tool compares files taken months apart, so it is the forensic half that
        # matters rather than the tidy one.
        Name = 'cli: the version names the shape of file it writes'
        File = 'src\Bws.Cli\Resources\cli.en.json'
        From = '"cli.version": "bws {0}\nsnapshot schema {1}\n'
        To = '"cli.version": "bws {0}\n'
        Test = 'UsageContractTests.The_tool_also_says_what_shape_of_snapshot_it_writes'
    },
    @{
        # THE ANCHOR MOVED ON 2026-08-13 and the entry is unchanged in what it claims. This method
        # stopped announcing unconditionally - it now works the two answers out first, compares, and
        # says nothing when neither moved - so the assignment this mutates is to a local.
        Name = 'count line: a narrowed list is marked as narrowed'
        File = 'src\Bws.Gui\ViewModels\Says.cs'
        From = '        var narrowed = shown != everything;'
        To = '        var narrowed = false;'
        Test = 'EmptyStateTests.The_window_knows_when_a_query_is_holding_entries_back'
    },

    # ---- Added 2026-08-03, after an audit that ran the tool rather than reading it ----
    #
    # Four guards for three faults, and every one of the three was a CRASH rather than a wrong
    # answer - which is why none of the five hundred tests then existing had anything to say
    # about them. A test asserts about a result, and there was no result.

    @{
        # Refusing must be the parser's answer, not an exception. Before 2026-08-03 a wildcard
        # of about a thousand parts ended the process with exit code 0xE0434352 and a stack
        # trace, in the command line tool and - on the same code path, from a binding, on every
        # keystroke - in the window.
        Name = 'query: a wildcard too big for the engine is refused, not thrown over'
        File = 'src\Bws.Core\Querying\QueryPatterns.cs'
        From = @'
            compiled = null;
            failure = refused.Message;

            return false;
'@
        To = @'
            compiled = null;
            failure = refused.Message;

            return true;
'@
        Test = 'QueryParserTests.A_wildcard_too_big_for_the_engine_is_refused_rather_than_thrown_over'
    },
    @{
        # The other end of the same line. A repair that simply refused every wildcard would pass
        # the entry above and take an ordinary "name:spool*" with it.
        Name = 'query: a wildcard the engine will build is still a wildcard'
        File = 'src\Bws.Core\Querying\QueryValueReader.cs'
        From = 'if (QueryPatterns.TryWildcard(value, out var wildcard, out var refused))'
        To = 'if (QueryPatterns.TryWildcard(value, out var wildcard, out var refused) && false)'
        Test = 'QueryParserTests.A_wildcard_the_engine_will_build_is_still_a_wildcard'
    },
    @{
        # Measured before the check existed: a file with `"entries": [ null, ... ]` ended
        # `bws snapshot diff` with "Object reference not set to an instance of an object."
        Name = 'snapshot: an entry that is empty is refused, not thrown over'
        # Moved out of SnapshotJson.cs on 2026-08-04, when the rule became a question asked of a
        # Snapshot rather than of a file - the comparison engine needed the same answer.
        File = 'src\Bws.Core\Snapshots\Snapshot.cs'
        # The condition itself cannot be mutated, and the first attempt proved it by coming back
        # BROKEN: weakening `if (entry is null)` leaves the compiler unable to prove the line
        # below is safe, and nullable warnings are errors here. So the verdict is mutated instead
        # of the test - the branch still runs, and stops saying the document is broken.
        From = @'
                failure = $"Entry {index + 1} is empty.";

                return true;
'@
        To = @'
                failure = $"Entry {index + 1} is empty.";

                return false;
'@
        Test = 'SnapshotTests.An_entry_that_is_empty_is_refused_rather_than_thrown_over'
    },
    @{
        # And a file naming one service twice ended it with "An item with the same key has
        # already been added", from inside a dictionary, naming neither of the two files.
        Name = 'snapshot: one service twice is refused, not thrown over'
        File = 'src\Bws.Core\Snapshots\Snapshot.cs'
        From = 'if (!seen.Add(entry.ServiceName))'
        To = 'if (!seen.Add(entry.ServiceName) && false)'
        Test = 'SnapshotTests.A_file_holding_one_service_twice_is_refused_rather_than_thrown_over'
    },
    @{
        # Names are folded because Windows folds them, and because the comparison engine matches
        # its two sides that way. An exact comparison here would let through the one file shape
        # the engine cannot handle - owner's decision, 2026-08-03.
        Name = 'snapshot: two names differing only in case are one service'
        File = 'src\Bws.Core\Snapshots\Snapshot.cs'
        From = 'var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);'
        To = 'var seen = new HashSet<string>(StringComparer.Ordinal);'
        Test = 'SnapshotTests.Two_names_differing_only_in_case_are_one_service_to_this_reader'
    },
    @{
        # A row already on screen is moved, never added a second time. Before this, two rows
        # swapping places left the list holding [B, A, B] - one service on screen twice - and the
        # invariant guarding the list could not see it, because in a swap both rows are wanted.
        Name = 'window: a row that moved is moved, not doubled'
        File = 'src\Bws.Gui\ViewModels\RowList.cs'
        From = 'if (already >= 0)'
        To = 'if (already >= 0 && false)'
        Test = 'RowListTests.Two_rows_that_swap_places_are_moved_rather_than_doubled'
    },
    @{
        # `bws list --query '""'` came back with the whole machine and a code of success, because
        # a member that produced no characters was dropped before anything could object to it.
        Name = 'query: a member that is only quotes is not dropped in silence'
        File = 'src\Bws.Core\Querying\QueryScanner.cs'
        From = 'if (text.Length == 0 && text.Blanks.Count == 0)'
        To = 'if (text.Length == 0)'
        Test = 'QueryParserTests.An_empty_pair_of_quotes_is_finished_and_says_nothing'
    },
    @{
        # And `name:""` the same way, one level down: the value was empty, which looked exactly
        # like a value somebody had not finished typing.
        # POINTED AT THE TYPING CASE SINCE 2026-08-03, and the move is the interesting part. It
        # used to name the test that parses finished text, and came back MISSED once finished
        # text started refusing anything that constrains nothing - the choke point caught the
        # empty quotes before this rule was reached, so removing this rule changed no outcome.
        #
        # The rule still earns its place, in the one situation the choke point deliberately stays
        # out of: a search box. `status:` is a member somebody has not finished. `name:""` is one
        # they closed, and no further keystroke turns it into something.
        Name = 'query: a value written as empty quotes is refused'
        File = 'src\Bws.Core\Querying\QueryScanner.cs'
        From = 'internal bool IsExplicitlyEmpty => Text.Length == 0 && Blanks is { Count: > 0 };'
        To = 'internal bool IsExplicitlyEmpty => Text.Length == 0 && Blanks is { Count: > 99 };'
        Test = 'QueryParserTests.An_empty_pair_of_quotes_is_finished_even_in_the_middle_of_typing'
    },
    @{
        # `ADR-19` broken a second time, and by a separator rather than by anything resembling a
        # network client. Windows parses a forward slash as a separator, so //server/share reaches
        # the same host as \\server\share - and this class is what stops an existence check
        # blocking for twenty one seconds while authenticating as whoever ran the tool.
        Name = 'network: a share written with forward slashes still leaves this machine'
        File = 'src\Bws.Core\NetworkPath.cs'
        From = "var value = path.TrimStart().Replace('/', '\\');"
        To = 'var value = path.TrimStart();'
        Test = 'BinaryPathResolverTests.What_counts_as_leaving_this_machine'
    },
    @{
        # `bws list --query --json` took --json as the text to search for, printed an empty table
        # and ended with code 0 - so a script asking for JSON got a human table and a green light.
        Name = 'cli: a switch is not swallowed as the value of another one'
        File = 'src\Bws.Cli\Arguments.cs'
        From = 'index + 1 >= arguments.Length || OptionSurface.IsOption(arguments[index + 1]);'
        To = 'index + 1 >= arguments.Length;'
        Test = 'UsageContractTests.A_switch_is_never_swallowed_as_the_value_of_another_one'
    },

    # ---- P2 of the same audit ----

    @{
        # Access denied was 5 out of the listing and -2147024891 out of a signature, and the
        # field's own documentation calls it "the system's own number", singular.
        Name = 'reading: one refusal carries one number'
        File = 'src\Bws.Core\Reading.cs'
        From = '((uint)code & 0xFFFF0000u) == 0x80070000u ? (int)((uint)code & 0xFFFFu) : code;'
        To = 'code;'
        Test = 'ReadingTests.One_refusal_carries_one_number_whichever_way_it_arrived'
    },
    @{
        # BACKLOG 235. A per-user template is automatic and never runs, because running is not what
        # it is for - a session copy runs beside it. Judged by the ordinary rule it looks exactly
        # like a service that failed to start, and it wore that accusation in three places at once
        # until 2026-08-26: bws show, the window column, and mismatch:stopped.
        #
        # Pointed at the family rather than deleted, because deleting the branch would also be
        # caught by a compiler warning about an unused field. Swapping which half of the family is
        # exempt is a change that compiles, runs, and is wrong - which is the only kind worth a
        # registry entry.
        Name = 'peruser: a template is not accused of failing to start'
        File = 'src\Bws.Core\ScmEntry.cs'
        From = '        if (PerUserRole == PerUserRole.Template)'
        To = '        if (PerUserRole == PerUserRole.Instance)'
        Test = 'ScmEntryTests.A_per_user_template_has_no_answer_to_whether_it_runs_against_its_start_type'
    },
    @{
        # The second half of backlog 235, and the one that could have gone wrong quietly. Absent
        # means "asked, and there is none". Not-read means "nobody could tell". This line used to
        # fold them together, which did no harm while neither half could be absent - and would have
        # moved every per-user template from one wrong sentence to another the moment one could.
        #
        # The mutation restores exactly the old line, so a green run here means somebody undid the
        # distinction and nothing else noticed.
        # The window's half of backlog 235. The switch that draws this mark carried a comment
        # saying absent was a state it could never produce - true when written, false the day a
        # per-user template started answering absent. The mutation restores exactly what the
        # default branch would have done: the unknown mark, meaning "I could not check", beside
        # every template on the machine.
        Name = 'peruser: a template wears no mark about a start type it cannot disobey'
        File = 'src\Bws.Gui\ViewModels\CellFaces.cs'
        From = '        ReadOutcome.Absent => CellShapes.Ordinary,'
        To = '        ReadOutcome.Absent => CellShapes.Unknown,'
        Test = 'ColumnGuards.A_per_user_template_wears_no_mark_about_a_start_type_it_cannot_disobey'
    },
    @{
        Name = 'mismatch: an absent half is an answer, not a failure to read'
        File = 'src\Bws.Core\Querying\QuerySymbols.cs'
        From = '        if (against.Outcome == ReadOutcome.NotRead || running.Outcome == ReadOutcome.NotRead)'
        To = '        if (against.Outcome != ReadOutcome.Present || running.Outcome != ReadOutcome.Present)'
        Test = 'MismatchQueryTests.A_per_user_template_agrees_with_itself_rather_than_being_unjudgeable'
    },
    @{
        # A text field asked about refused and not about unread, so an entry nobody looked at was
        # dropped from the result and never counted among the ones the answer is unsure about.
        Name = 'query: a field nobody read makes the answer say so'
        File = 'src\Bws.Core\Querying\QueryValues.cs'
        From = 'field.OutcomeOf(entry) is ReadOutcome.Denied or ReadOutcome.NotRead;'
        To = 'field.OutcomeOf(entry) is ReadOutcome.Denied;'
        Test = 'SignatureQueryTests.An_entry_nobody_read_the_signature_of_makes_the_answer_say_so'
    },
    @{
        # THREE ENTRIES ADDED 2026-08-26 WITH THE GUARD THEY BELONG TO, and the reason they
        # exist is that the claim they hold USED TO BE HELD BY A STOPWATCH. Every pattern in
        # the language carried a fifty millisecond match timeout, so a wrong engine choice
        # surfaced as a timeout - which meant the guard went red when the machine was busy
        # rather than when the choice was wrong. Backlog 137 and 174, both of which blamed the
        # query. The timeout came off the linear engine and these three hold what it held.
        #
        # This one is the mistake itself: the pattern a person typed sent to the backtracking
        # engine, where any input can make it run away over every entry on every keystroke.
        #
        # THE CEILING GOES BACK ON IN THE SAME BREATH, DELIBERATELY. Flipping the engine while
        # leaving the timeout infinite would hand (a+)+b to a backtracking engine with no net,
        # and the run would hang instead of failing - which is the exact outcome this whole
        # script exists to avoid.
        Name = 'query: a pattern somebody typed goes to the engine that cannot run away'
        File = 'src\Bws.Core\Querying\QueryPatterns.cs'
        From = @'
                Shared | RegexOptions.NonBacktracking,
                Regex.InfiniteMatchTimeout);
'@
        To = @'
                Shared,
                Ceiling);
'@
        Test = 'QueryEngineChoiceTests.A_pattern_the_linear_engine_can_take_is_given_to_it'
    },
    @{
        # The other direction, and the one the product's users would feel. A wildcard is typed
        # into a search box and evaluated over every entry on every keystroke, so a wall-clock
        # limit here is the one most likely to fire on a pause the pattern did not cause - and
        # then tell somebody their filter was too costly while they were still typing it.
        Name = 'query: a wildcard carries no wall-clock limit it did not earn'
        File = 'src\Bws.Core\Querying\QueryPatterns.cs'
        From = @'
                Shared | RegexOptions.NonBacktracking | RegexOptions.Singleline,
                Regex.InfiniteMatchTimeout);
'@
        To = @'
                Shared | RegexOptions.NonBacktracking | RegexOptions.Singleline,
                Ceiling);
'@
        Test = 'QueryEngineChoiceTests.A_wildcard_is_given_to_the_linear_engine_and_carries_no_time_limit'
    },
    @{
        # And the half that must NOT be taken away. A backreference is a thing somebody meant,
        # so it is served by the ordinary engine - where the time limit is the only net there
        # is. Take it off and a saved query with a backreference stops the window rather than
        # answering it.
        #
        # Safe to run because the named test only compiles the pattern and never matches with
        # it. The test that DOES match one of these is filtered out.
        Name = 'query: the backtracking fallback keeps the only net it has'
        File = 'src\Bws.Core\Querying\QueryPatterns.cs'
        From = '            compiled = new Regex(pattern, Shared, Ceiling);'
        To = '            compiled = new Regex(pattern, Shared, Regex.InfiniteMatchTimeout);'
        Test = 'QueryEngineChoiceTests.A_pattern_the_linear_engine_refuses_falls_back_and_keeps_its_time_limit'
    },
    @{
        # ADR-18 asks for quarantine rather than deletion, and until 2026-08-03 the code that
        # does it had no caller in the product - tested, green, and never run.
        # RE-ANCHORED 2026-10-06, security report S-8, owner's decision: what cannot be read back as a
        # snapshot is no longer moved aside so it can be replaced - it is refused and left where it is,
        # with --force too. Same claim, kept harder: the file survives. The mutant gives --force back
        # its old reach over any file.
        Name = 'snapshot: what cannot be read back is kept rather than replaced'
        File = 'src\Bws.Core\Snapshots\SnapshotOnDisk.cs'
        From = '        if (WhyNotASnapshot(target) is { } why)'
        To = '        if (!force && WhyNotASnapshot(target) is { } why)'
        Test = 'SnapshotTargetTests.A_file_that_is_not_a_snapshot_is_refused_with_or_without_force_and_left_where_it_is'
    },
    @{
        # The other side. Keeping a copy of every snapshot somebody deliberately replaced would
        # fill their directory with files they never asked for.
        # RE-ANCHORED 2026-10-06 with the rule above: a snapshot this build reads is still replaced
        # with --force, and the mutant takes that away by calling every file not a snapshot.
        Name = 'snapshot: a readable snapshot is replaced rather than kept'
        File = 'src\Bws.Core\Snapshots\SnapshotOnDisk.cs'
        From = '            return SnapshotJson.TryRead(ReadText(path), out _, out var failure)'
        To = '            return SnapshotJson.TryRead(ReadText(path), out _, out var failure) && false'
        Test = 'SnapshotTargetTests.A_snapshot_is_replaced_with_force'
    },
    @{
        # Security report S-8, 2026-10-06: the question "is this a snapshot" was asked by reading the
        # whole file into one string. Measured with the shipped 0.3.0 - 1.5 GB of zeros with --force
        # ended after 4045 ms with code 1 and "Insufficient memory". Load had the ceiling, this did not.
        Name = 'snapshot: a file at the target too big to be one is not read to find out'
        File = 'src\Bws.Core\Snapshots\SnapshotOnDisk.cs'
        From = '            if (new FileInfo(path).Length > Biggest)'
        To = '            if (new FileInfo(path).Length > long.MaxValue)'
        Test = 'SnapshotTargetTests.A_file_too_big_to_be_a_snapshot_is_refused_by_its_size_before_it_is_read'
    },

    # ---- P3, and the repair that closed a family rather than a shape ----

    @{
        # Three spellings of one fault were repaired by naming them - !!!, then the empty pair of
        # quotes - and each time the next turned up within the hour. This stops naming spellings.
        Name = 'query: text that constrains nothing is refused once nobody is typing it'
        File = 'src\Bws.Core\Querying\QueryParser.cs'
        From = 'if (input is QueryInput.Finished && problems.Count == before)'
        To = 'if (input is QueryInput.BeingTyped && problems.Count == before)'
        Test = 'QueryParserTests.The_same_text_is_a_mistake_once_nobody_is_typing_it_any_more'
    },
    @{
        # The other side, and without it a repair that simply refused everything would pass the
        # entry above. A search box holds "status:" between two keystrokes.
        Name = 'query: half typed text still answers while somebody is typing it'
        File = 'src\Bws.Core\Querying\QueryParser.cs'
        From = 'if (input is QueryInput.Finished && problems.Count == before)'
        To = 'if (problems.Count == before)'
        Test = 'QueryParserTests.A_member_still_being_typed_is_not_an_error'
    },
    @{
        # The last one used to win in silence, so --query a --query b searched for b.
        #
        # In Refusals.cs since 2026-08-25, where every check about what somebody typed moved when
        # the size ratchet asked Program.cs for a seam a second time.
        Name = 'cli: an option given twice is refused'
        File = 'src\Bws.Cli\Refusals.cs'
        From = 'if (options.Repeated.Count > 0)'
        To = 'if (options.Repeated.Count > 99)'
        Test = 'UsageContractTests.An_option_given_twice_is_refused_rather_than_quietly_halved'
    },
    @{
        # The narrow answer to MA0002, which is switched off with a measurement beside it. A
        # dictionary keyed by a service name and left to the default silently fails to match two
        # spellings of one service - rule 3 of the untouchable list, one layer down.
        Name = 'comparer: a collection over string keys says how the keys compare'
        File = 'src\Bws.Core\SecondPass.cs'
        From = 'var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);'
        To = 'var seen = new HashSet<string>();'
        Test = 'ComparerGuards.Every_collection_over_string_keys_says_how_the_keys_compare'
    },
    @{
        # CA1305 is on and warnings are errors, and it does not see an interpolated string - so
        # the one hole in the product that formatted a date built cleanly for its whole life. It
        # named a snapshot 25690803 on a Thai machine for a moment the file itself called 2026.
        # Both lines of the expression, because commenting out the first leaves the second
        # dangling and the tree stops compiling - which came back BROKEN and proved nothing.
        Name = 'formatting: nothing is formatted inside an interpolated string'
        File = 'src\Bws.Core\Snapshots\SnapshotOnDisk.cs'
        From = @'
            : "bws-snapshot-" + metadata.Machine + "-"
                + metadata.TakenAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json";
'@
        To = @'
            : $"bws-snapshot-{metadata.Machine}-{metadata.TakenAt:yyyyMMdd-HHmmss}.json";
'@
        Test = 'FormattingGuards.No_value_is_formatted_inside_an_interpolated_string'
    },
    @{
        # 297 entries reported as changed by a file compared against itself, because the bytes
        # had been re-encoded and the reader substituted rather than refused.
        Name = 'snapshot: bytes that are not UTF-8 are refused, not guessed at'
        File = 'src\Bws.Core\Snapshots\SnapshotOnDisk.cs'
        From = 'new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),'
        To = 'new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),'
        Test = 'SnapshotContractTests.A_snapshot_that_is_not_utf8_is_refused_rather_than_guessed_at'
    },
    @{
        # The other side: a mark is honoured, so a snapshot something else saved as UTF-16 still
        # reads. Without this, a repair that simply refused anything unusual would pass above.
        Name = 'snapshot: a byte order mark is still honoured'
        File = 'src\Bws.Core\Snapshots\SnapshotOnDisk.cs'
        From = 'detectEncodingFromByteOrderMarks: true);'
        To = 'detectEncodingFromByteOrderMarks: false);'
        Test = 'SnapshotContractTests.A_snapshot_saved_with_a_byte_order_mark_still_reads'
    },

    # EIGHT FOR THE KEPT COLUMN LAYOUT, S6d3, 2026-08-13. The first file this product writes for a
    # person rather than for a machine, so every one of these is a way a layout can come back
    # wrong while the window looks entirely normal - which is the shape of fault nobody
    # investigates.
    @{
        Name = 'layout: a column this build does not have is named rather than swallowed'
        File = 'src\Bws.Gui\ViewModels\ColumnPlan.cs'
        From = '                ignored.Add(column.Id);'
        To = ''
        Test = 'ColumnLayoutGuards.A_column_the_catalogue_does_not_know_is_left_out_and_said_out_loud'
    },
    @{
        Name = 'layout: a file hiding every column does not get an empty window'
        File = 'src\Bws.Gui\ViewModels\ColumnPlan.cs'
        From = '        if (noneWasShown)'
        To = '        if (noneWasShown && false)'
        Test = 'ColumnLayoutGuards.A_layout_with_every_column_hidden_gets_the_usual_columns_back_and_says_so'
    },
    @{
        Name = 'layout: a schema this build does not read is refused rather than guessed at'
        File = 'src\Bws.Gui\ViewModels\ColumnLayout.cs'
        From = '        if (version > ColumnLayout.CurrentSchemaVersion || version < ColumnLayout.OldestSchemaVersionRead)'
        To = '        if (version > ColumnLayout.CurrentSchemaVersion + 1000)'
        Test = 'ColumnLayoutGuards.A_layout_from_another_schema_is_refused_by_number_rather_than_by_damage'
    },
    @{
        # Quarantine rather than overwrite - ADR-18. The mutation keeps the refusal and drops the
        # moving aside, which is the half that cannot be seen from inside the window.
        Name = 'layout: a file that will not parse is moved aside rather than left to be overwritten'
        File = 'src\Bws.Gui\PreferencesFile.cs'
        From = 'return reading.Unreadable is null ? reading : reading with { MovedAside = Aside() };'
        To = 'return reading;'
        # Followed the test into PreferencesFileGuards on 2026-08-13, when the size ratchet split
        # the class at the same seam the product has: what a layout means, and where it lives.
        Test = 'PreferencesFileGuards.An_unreadable_file_is_moved_aside_with_its_content_intact'
    },
    @{
        # ADR-23 surviving a saved file: the theme is the STARTING width, so a column nobody moved
        # writes nothing and keeps following the theme. Mutated, every width is frozen into every
        # profile on the first close.
        Name = 'layout: only a width somebody moved is written down'
        File = 'src\Bws.Gui\ListColumns.cs'
        From = 'return width.UnitType == start.UnitType && width.Value.Equals(start.Value)'
        To = 'return false'
        Test = 'KeptColumnGuards.Only_a_width_that_moved_is_written_down_and_the_order_is_what_is_on_screen'
    },
    @{
        # Rule 3 one layer down: a width written as 2,5* on a Polish machine stops being a width
        # when the file crosses a border, and section H promises these files travel.
        Name = 'layout: a width is written in a form that does not depend on the machine'
        File = 'src\Bws.Gui\ListColumns.cs'
        From = ': Widths.ConvertTo(null, System.Globalization.CultureInfo.InvariantCulture, width, typeof(string))'
        To = ': Widths.ConvertTo(null, System.Globalization.CultureInfo.CurrentCulture, width, typeof(string))'
        Test = 'KeptColumnGuards.A_width_is_written_in_the_same_form_on_a_machine_that_writes_decimals_with_a_comma'
    },
    @{
        Name = 'layout: the order somebody dragged their headings into is applied'
        File = 'src\Bws.Gui\ListColumns.cs'
        From = '                column.DisplayIndex = position;'
        To = ''
        Test = 'KeptColumnGuards.The_order_that_was_kept_is_the_order_the_grid_shows'
    },
    @{
        # The details panel is the column catalogue applied to one entry, and this is the entry
        # that holds it to that. Mutated, a field says one thing in a cell and another in the
        # panel - which is what a second list of fields written out beside the columns would do
        # the first time only one of them was touched.
        #
        # RE-ANCHORED 2026-09-15: the line is composed by Said now, which reads the cell first and
        # adds what the column holds behind it. Mutating the read inside Said is the same claim.
        Name = 'details: a field says the same in the panel as in its own cell'
        File = 'src\Bws.Gui\ViewModels\Details.cs'
        From = '        var shown = column.Reads(entry);'
        To = '        var shown = column.Id;'
        Test = 'DetailsGuards.A_line_says_exactly_what_that_columns_cell_says'
    },
    # A TOMBSTONE. What stood here broke the panel's list of the four fields this window did not
    # read - signature, file version, hash and memory - and checked that the panel still named them
    # and said why. That whole section was DELETED on 2026-08-18, backlog 21: the window reads those
    # fields now, so they are columns like any other and the panel gets them the way it gets the
    # rest. A section still apologising for them would be the panel confidently wrong, which is the
    # fault the section existed to prevent, pointing the other way.
    #
    # Retired rather than re-pointed, for the reason the other tombstone in this file gives: a
    # mutation against deleted behaviour comes back MISSED by construction while looking exactly
    # like a guard that lapsed. The entry below guards what replaced it.
    @{
        # The panel IS the column catalogue applied to one entry, and this is the line that makes
        # that true. Dropping a heading from the walk leaves the panel quietly missing four fields
        # while every other test about columns stays green - the panel and the picker would simply
        # disagree, which is the second place to add a field to that the class comment refuses.
        Name = 'details: the panel is the column catalogue applied to one entry'
        File = 'src\Bws.Gui\ViewModels\Details.cs'
        # Re-anchored 2026-09-24: the order is a named array since What it runs moved second.
        From = 'private static readonly string[] Order = [Columns.Basics, Columns.Binary, Columns.About, Columns.Advanced];'
        To = 'private static readonly string[] Order = [Columns.Basics, Columns.About, Columns.Advanced];'
        Test = 'DetailsGuards.The_fields_this_panel_once_apologised_for_carry_values_now'
    },
    @{
        # The wiring rather than a rule: the machinery all works and reaches nothing. That is the
        # way this product has failed most often, and the only entry here that goes through the
        # real window from one open to the next.
        Name = 'layout: the window opens with the layout it was left with'
        File = 'src\Bws.Gui\MainWindow.Columns.cs'
        From = '        _columns.Follow(kept.Plan);'
        To = ''
        Test = 'KeptColumnGuards.A_column_turned_off_is_still_off_the_next_time_the_window_opens'
    },
    @{
        # THE SENTENCE THE PANEL SHIPPED WRONG, AND ONE ROW PICKED IS THE COMMONEST SELECTION THERE
        # IS - so "Done. All 1 entries are where you asked" was on screen far more often than the
        # plural it was written for. Mutated onto a count nothing reaches, every run takes the plural
        # key and the singular becomes text no screen ever shows, which is the state this panel was
        # in from the day it learned to report a run until the day somebody read the language file.
        Name = 'plan panel: a run over one entry says so in the singular'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        From = '? run.Runs.Count == 1'
        To = '? run.Runs.Count == 0'
        Test = 'PluralGuards.A_run_over_one_entry_says_so_in_the_singular'
    },
    @{
        # The warning that carries a number, and the one the command line has had a pair of keys for
        # since it learned to warn. A cascade of one rendered through the plural key reads "also stops
        # 1 other entries" - a counted plural, which is the one shape the guard finds by pattern
        # rather than by naming the sentence.
        Name = 'plan panel: a cascade of one is not counted as several'
        File = 'src\Bws.Gui\ViewModels\PlanWords.Warnings.cs'
        From = 'PlanWarningKind.Cascade => warning.Related.Count == 1'
        To = 'PlanWarningKind.Cascade => warning.Related.Count == 0'
        Test = 'PluralGuards.Nothing_the_panel_says_about_one_entry_is_written_for_more_than_one'
    },
    @{
        # The half no pattern can find: a group pronoun standing for a list of one. "These are running
        # and need Spooler" over a single name carries no number for anything to match on, so this
        # branch is held by name instead - which is why the guard file says out loud what its pattern
        # does not cover rather than leaving somebody to find the hole.
        Name = 'plan panel: one entry in the way is not called these'
        File = 'src\Bws.Gui\ViewModels\PlanWords.Warnings.cs'
        From = 'PlanWarningKind.DependentsInTheWay => warning.Related.Count == 1'
        To = 'PlanWarningKind.DependentsInTheWay => warning.Related.Count == 0'
        Test = 'PluralGuards.A_group_of_one_is_never_called_these_or_those'
    },
    @{
        # A refusal rather than a warning, and it is read by somebody deciding whether to press. One
        # entry described as "these" is a sentence they have to check against the list underneath it,
        # which is exactly the work the pair exists to take away.
        Name = 'plan panel: one entry that cannot come back is not called these'
        File = 'src\Bws.Gui\ViewModels\PlanWords.cs'
        # RE-ANCHORED 2026-09-07: the discard this stood on became a named arm and a throw, which
        # is the repair that stopped two kinds of refusal sharing one sentence. The count it proves
        # is unchanged and is now on the arm that owns it.
        From = 'PlanProblemKind.CannotComeBack => problem.Related.Count == 1'
        To = 'PlanProblemKind.CannotComeBack => problem.Related.Count == 0'
        Test = 'PluralGuards.A_group_of_one_is_never_called_these_or_those'
    },
    @{
        # THE GUARD THAT PROTECTS THE PAIRS THEMSELVES, and it mutates the language file rather than
        # any code. A singular added with no plural beside it renders on somebody's screen as its own
        # key - and TextKeyGuards cannot see that one coming, because both halves are perfectly
        # declared and perfectly referenced right up until the day the other count arrives.
        Name = 'plan panel: a singular sentence with no plural beside it is refused'
        File = 'src\Bws.Gui\Resources\gui.en.json'
        From = '"gui.plan.notice.done.many":'
        To = '"gui.plan.notice.doneWithNothingBesideIt.many":'
        Test = 'PluralGuards.Every_singular_sentence_has_a_plural_beside_it'
    },
    @{
        # ADDED 2026-08-26 WITH THE GUARD IT HOLDS, and the guard exists because the plural
        # pattern stopped guessing English and started naming this product's nouns. Backlog 225.
        #
        # Naming them made the guard rot the other way: it can now MISS a plural nobody listed
        # instead of shouting about a correct sentence. That is the better direction and it is
        # still a silence, so it needed closing - backlog 238 is what an unwatched silence costs.
        #
        # The mutation is a sentence counting something the list has never heard of. If the
        # classification guard is doing its job, a word appearing after a placeholder that is
        # neither a known plural noun nor a known non-noun stops the run until somebody says
        # which it is.
        Name = 'plural: a counted word nobody classified is refused'
        File = 'src\Bws.Gui\Resources\gui.en.json'
        From = '"gui.rollup.many": "{0} instances",'
        To = '"gui.rollup.many": "{0} sessions",'
        # Re-anchored the same day it was written: the size ratchet fired on PluralGuards.cs, so the
        # vocabulary and this guard moved to CountedWords.cs and the guard got its own class.
        Test = 'CountedWordGuards.Every_counted_word_in_the_language_files_is_classified'
    }

    # NO ENTRY FOR THE LISTING COLUMN WIDTH, and this one was written and deleted before it ran.
    #
    # ListingTable is internal to Bws.Cli, which has no test project of its own - the only thing
    # that exercises it is the integration suite, through the real tool, over whatever display
    # names this machine happens to have. None of them holds a character stored as two units, and
    # a service cannot be installed to provide one.
    #
    # An entry naming a test that does not exist would have come back BROKEN and proved nothing
    # while looking like a guard. Recorded in docs/08 as a behaviour without one instead.

    # NO ENTRY FOR THE SECOND OVERWRITE GATE, and that is an admission rather than an omission.
    #
    # An entry was written for it and came back MISSED, honestly: the test beside it passed a
    # file name explicitly, so the EARLY check refused first and the gate being mutated was never
    # reached. The test's name claimed something the test did not check, and the runner said so.
    #
    # Reaching that gate needs the tool to choose a name that already exists, which needs two
    # runs inside one second - not reproducible on demand from a test, because the command line
    # has no way to hand the clock in. The test was removed rather than left claiming a guard it
    # did not provide, and the behaviour is written down in docs/08 as one without a guard.

    # EIGHT ENTRIES FROM 2026-08-25, THE TWO PACKETS THAT CAME OUT OF A REPORT WRITTEN BY A CHAT
    # THAT HAD NEVER SEEN THIS PROJECT. Each one breaks the thing the guard beside it is named
    # after, and each one names a behaviour that reaches a person rather than an internal.
    @{
        # A bar of buttons over a list of services that stay live over an empty selection is a bar
        # that answers a press with nothing at all.
        Name = 'action bar: the buttons go dead when nothing is picked'
        File = 'src\Bws.Gui\ActionBar.xaml.cs'
        # Re-anchored 2026-09-16: the parameter became entries rather than rows.
        From = 'var anything = entries > 0 && !onlyDrivers;'
        To = 'var anything = !onlyDrivers;'
        Test = 'ActionBarGuards.With_nothing_picked_the_bar_is_off_and_says_why'
    },
    @{
        # THE FAILURE THIS WINDOW HAS MET MOST OFTEN: a mechanism that works in every part and is
        # wired to nothing. The press arrives, the event is raised, and the window ignores it.
        Name = 'action bar: a press reaches the window rather than nothing'
        File = 'src\Bws.Gui\MainWindow.xaml.cs'
        # RE-ANCHORED 2026-09-03: the plan behind the press moved off the window's thread - backlog
        # 301 - so the handler became an async lambda awaiting it.
        From = 'Actions.PreviewRequest += async (_, asked) => await Preview(asked.Kind).ConfigureAwait(true);'
        To = 'Actions.PreviewRequest += (_, asked) => _ = asked.Kind;'
        Test = 'ActionBarGuards.Pressing_one_opens_a_plan_rather_than_doing_anything'
    },
    @{
        # The way back that puts back what is already there. A restore reading the kept layout
        # instead of the defaults is a menu item that looks like it worked and changed nothing.
        Name = 'columns: the way back reaches the defaults rather than the kept layout'
        File = 'src\Bws.Gui\KeptColumns.cs'
        From = 'Plan = ColumnPlan.Of(null, _scope);'
        To = 'Plan = ColumnPlan.Of(_layouts.For(_scope), _scope);'
        Test = 'KeptColumnGuards.The_way_back_puts_the_usual_columns_on_and_keeps_them_that_way'
    },
    @{
        # The owner's complaint of 2026-08-19 put back: no focus visual, and the ring has to come
        # from somewhere else or not at all.
        Name = 'scope: the keyboard ring is a focus visual rather than a state of the control'
        File = 'src\Bws.Gui\ScopeBar.xaml'
        From = '<Setter Property="FocusVisualStyle" Value="{StaticResource ScopeFocusRing}" />'
        To = '<Setter Property="FocusVisualStyle" Value="{x:Null}" />'
        Test = 'ScopeBarGuards.The_scope_ring_is_drawn_for_the_keyboard_rather_than_for_a_click'
    },
    @{
        # The title back to the manager's own name - "What stopping pla would do" over a machine
        # where that entry is called Performance Logs and Alerts everywhere else.
        Name = 'plan panel: the title uses the name a person recognises'
        File = 'src\Bws.Gui\ViewModels\Planned.Title.cs'
        From = 'string.IsNullOrWhiteSpace(_shownAs) ? Only(plan) : _shownAs;'
        To = 'Only(plan);'
        Test = 'PlanViewGuards.The_title_names_the_entry_the_way_a_person_does_with_the_manager_name_under_it'
    },
    @{
        # A button that refuses without a reason, which is what it was until this packet: the
        # session cannot change anything and the tooltip says what the button would do.
        Name = 'plan panel: the greyed button names what is in the way'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        From = ': !Elevated ? Texts.Of("gui.plan.blocked.notElevated")'
        To = ': !Elevated ? Texts.Of("gui.plan.carryOut.hint")'
        Test = 'CarryingGuards.The_button_says_why_it_is_grey_rather_than_only_refusing'
    },
    @{
        # "1 entries" under the search box, which is where backlog 207 started.
        Name = 'count line: one entry is counted in the singular'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.cs'
        From = '            ? everything.Count == 1'
        To = '            ? everything.Count == 0'
        Test = 'PluralGuards.The_count_line_has_a_singular_for_a_machine_holding_one_entry'
    },
    @{
        # The fold inverted: the filters hide themselves when the switch is on. The window opens
        # with the switch checked, so this is a row that is gone before anybody touches it.
        Name = 'filters: the row folds when it is asked to rather than when it is not'
        File = 'src\Bws.Gui\FilterRow.xaml'
        From = '<DataTrigger Binding="{Binding IsChecked, ElementName=FiltersToggle}" Value="False">'
        To = '<DataTrigger Binding="{Binding IsChecked, ElementName=FiltersToggle}" Value="True">'
        Test = 'ActionBarGuards.The_filters_start_open_and_fold_away_when_asked'
    },
    @{
        # THE FAULT THIS ENTRY PUTS BACK WAS REAL AND LASTED AN HOUR. ColumnPlan.Of rebuilds the
        # layout out of the columns it accepted, and the first version left the sort behind - so
        # the file held the order, the window read the file, and the list came up unsorted with
        # nothing anywhere saying why. Found by the guard below rather than by anything else.
        #
        # RE-ANCHORED 2026-09-15, when a missing order started falling back to the opening one.
        # Dropping the whole expression - kept order and fallback - is the same fault as before:
        # the file holds an order and the plan forgets it. The fallback alone has its own pair of
        # entries further down.
        Name = 'layout: the order somebody put the list in survives being read back'
        File = 'src\Bws.Gui\ViewModels\ColumnPlan.cs'
        From = 'new ColumnLayout(kept, layout.Sort ?? ColumnLayout.DefaultFor(scope).Sort),'
        To = 'new ColumnLayout(kept),'
        Test = 'KeptColumnGuards.The_order_somebody_put_the_list_in_comes_back_the_next_time_it_opens'
    },
    @{
        # THE NARROW EXCEPTION PUT BACK TO BEING A WIDE ONE. The owner allowed the window to name a
        # process for one button on 2026-08-25, and for a second on 2026-09-23, on the condition
        # that only the files named in the guard may do it - this puts the name in a third file and
        # expects the source guard to say so. Retargeted 2026-09-23 when the guard was renamed from
        # Only_one_file_in_the_window_may_start_a_process.
        Name = 'elevation: a process is started from the named files and no other'
        File = 'src\Bws.Gui\MainWindow.xaml.cs'
        # Re-anchored 2026-09-23: Restart took the hand-over as its argument (UX-GUI-004 c).
        # Re-anchored 2026-10-05: the hand-over is read before the layout is written (G-7).
        From = 'if (Elevation.Restart(handOver) is { } trouble)'
        To = '_ = typeof(System.Diagnostics.ProcessStartInfo);
        if (Elevation.Restart(handOver) is { } trouble)'
        Test = 'LayeringGuards.Only_the_named_files_in_the_window_may_start_a_process'
    },
    @{
        # THE SECOND WAY TO START SOMETHING, IN A FILE THE GUARD DOES NOT NAME. The Donate button
        # hands its address to the desktop through COM, and that starts a browser without the word
        # Process anywhere - so the guard looks for a shell object being asked to execute too.
        #
        # Re-anchored 2026-09-23 when the press started awaiting the shell off the window's thread
        # after the PR 8 review. The handler lost its sender, so the never-true condition is now
        # one that needs nothing from the handler's signature.
        #
        # Re-anchored 2026-10-14 (backlog 568): the Donate button's own method went, and its press
        # takes OpenPage in MainWindow.Help.cs with the other two pages - the same window, another file.
        Name = 'donate: asking a shell object to execute is refused outside the named files'
        File = 'src\Bws.Gui\MainWindow.Help.cs'
        From = 'if (await PagesOpenedBy(destination).ConfigureAwait(true) is { } trouble)'
        To = 'if (Environment.ProcessorCount < 0) { ExternalLinks.Desktop()?.ShellExecute(default, string.Empty, string.Empty, string.Empty, 1); }
        if (await PagesOpenedBy(destination).ConfigureAwait(true) is { } trouble)'
        Test = 'LayeringGuards.Only_the_named_files_in_the_window_may_start_a_process'
    },
    @{
        # An address chosen outside the links file. The shell runs whatever it is handed, so a
        # caller with its own string is a way of starting anything. Re-anchored 2026-09-23 with
        # the entry above, and again 2026-10-14 with it (backlog 568). The guard reads sources and
        # never runs this line, which is why a mutant that would start a browser is safe here.
        Name = 'donate: nothing outside the links file hands the shell an address'
        File = 'src\Bws.Gui\MainWindow.Help.cs'
        From = 'if (await PagesOpenedBy(destination).ConfigureAwait(true) is { } trouble)'
        To = '_ = ExternalLinks.Start(ExternalLinks.Support);
        if (await PagesOpenedBy(destination).ConfigureAwait(true) is { } trouble)'
        Test = 'ExternalLinksGuards.Nothing_outside_the_links_file_hands_the_shell_an_address'
    },
    @{
        # THE SHELL ASKED FROM A POOL-LIKE APARTMENT. The desktop's shell objects expect a single
        # threaded apartment and the window's thread was one, so the thread that replaced it for
        # the PR 8 review has to be one as well.
        Name = 'donate hand-over: the shell is asked in a single threaded apartment of its own'
        File = 'src\Bws.Gui\ShellHandover.cs'
        From = 'thread.SetApartmentState(ApartmentState.STA);'
        To = 'thread.SetApartmentState(ApartmentState.MTA);'
        Test = 'ExternalLinksGuards.A_press_hands_the_shell_its_work_on_a_background_thread_of_the_windows_apartment'
    },
    @{
        # A foreground thread stuck in a shell that never answers keeps the process alive after
        # the window is closed - a program that looks closed and is still in the task list.
        Name = 'donate hand-over: the thread is a background thread'
        File = 'src\Bws.Gui\ShellHandover.cs'
        From = 'IsBackground = true,'
        To = 'IsBackground = false,'
        Test = 'ExternalLinksGuards.A_press_hands_the_shell_its_work_on_a_background_thread_of_the_windows_apartment'
    },
    @{
        # Every press its own hand-over. A person who sees nothing presses again, and a shell that
        # comes back opens the page once per press.
        Name = 'donate hand-over: a press while the shell has the last one joins it'
        File = 'src\Bws.Gui\ShellHandover.cs'
        From = 'if (_running is not { IsCompleted: false })'
        To = 'if (_running is not { IsCompleted: false } || patience > TimeSpan.Zero)'
        Test = 'ExternalLinksGuards.A_second_press_while_the_first_is_with_the_shell_starts_nothing_new'
    },
    @{
        # THE BOUND ITSELF. Without it the press waits for as long as the shell does, which is the
        # window waiting on another process with no limit - the finding this file answers. The
        # test's own limit is thirty seconds, so this entry costs that much to be killed.
        Name = 'donate hand-over: a shell that does not answer is a sentence once the bound has passed'
        File = 'src\Bws.Gui\ShellHandover.cs'
        From = 'await Task.WhenAny(running, Task.Delay(patience)).ConfigureAwait(true) == running'
        To = 'await Task.WhenAny(running, Task.Delay(Timeout.InfiniteTimeSpan)).ConfigureAwait(true) == running'
        Test = 'ExternalLinksGuards.A_shell_that_does_not_answer_is_a_sentence_once_the_bound_has_passed'
    },
    @{
        # A hand-over that swallows what the shell threw. The press would then read an unknown
        # failure as success, where on the window's thread it reached the last line in Mishaps.
        Name = 'donate hand-over: a throw on the shell thread reaches the press'
        File = 'src\Bws.Gui\ShellHandover.cs'
        From = 'var handover = new Task<string?>(work);'
        To = 'var handover = new Task<string?>(() => { try { return work(); } catch (InvalidOperationException) { return null; } });'
        Test = 'ExternalLinksGuards.A_throw_on_the_shells_thread_reaches_the_press_rather_than_ending_the_process'
    },
    @{
        # The runtime's own text in the status line again - "Specified cast is not valid." for an
        # interface the desktop does not offer - which is what the PR 8 review found.
        Name = 'donate hand-over: a desktop refusal reads as the window''s sentence and a number'
        File = 'src\Bws.Gui\ExternalLinks.cs'
        From = 'Texts.Of("gui.support.desktopRefused", failure.HResult.ToString("X8", CultureInfo.InvariantCulture));'
        To = 'Texts.Of("gui.support.desktopRefused", failure.Message, CultureInfo.InvariantCulture);'
        Test = 'ExternalLinksGuards.A_desktop_refusal_is_a_sentence_of_the_windows_own_with_the_number_windows_gave'
    },
    @{
        # THE WHOLE SAFETY PROPERTY OF THE DONATE BUTTON. A desktop that refuses, followed by the
        # direct start as a fallback, opens the browser with administrator rights - the outcome
        # ExternalLinks exists to avoid, and one that would look like the button working.
        Name = 'donate: an elevated window never falls back to starting the browser itself'
        File = 'src\Bws.Gui\ExternalLinks.cs'
        From = 'var reason = elevated ? handToDesktop(address) : start(address);'
        To = 'var reason = elevated ? (handToDesktop(address) is null ? null : start(address)) : start(address);'
        Test = 'ExternalLinksGuards.An_elevated_window_goes_to_the_desktop_and_never_starts_the_browser_itself'
    },
    @{
        # A failure that no longer says where the page is, which leaves a person with a refusal
        # and no way round it.
        Name = 'donate: a failure names the address to type by hand'
        File = 'src\Bws.Gui\ExternalLinks.cs'
        # Re-anchored 2026-09-24: the key became a parameter when the help menu's two pages came.
        From = 'return reason is null ? null : Texts.Of(failedKey, reason, address);'
        To = 'return reason is null ? null : Texts.Of(failedKey, reason, string.Empty);'
        Test = 'ExternalLinksGuards.An_elevated_window_goes_to_the_desktop_and_never_starts_the_browser_itself'
    },
    @{
        # A plaintext address, which would be the one insecure step this window could take and
        # nothing on screen would show.
        Name = 'donate: the address is the https one the tooltip promises'
        File = 'src\Bws.Gui\ExternalLinks.cs'
        From = 'internal const string Support = "https://donislawdev.com/support/";'
        To = 'internal const string Support = "http://donislawdev.com/support/";'
        Test = 'ExternalLinksGuards.The_support_address_is_the_one_the_button_promises'
    },
    @{
        # A shell refusal that escapes as an exception instead of coming back as a sentence - out
        # of a Click handler, to the window's last line rather than the status line.
        Name = 'donate: a shell refusal is a sentence rather than a throw'
        File = 'src\Bws.Gui\ExternalLinks.cs'
        From = 'catch (Win32Exception failure)'
        To = 'catch (InvalidOperationException failure)'
        Test = 'ExternalLinksGuards.An_address_the_shell_refuses_is_a_sentence_rather_than_a_throw'
    },
    @{
        # The button placed by its neighbour rather than by the row's right edge - GUI rule 19.
        Name = 'donate: help and donate line up with Columns at the right end of the top row'
        File = 'src\Bws.Gui\ScopeBar.xaml'
        From = 'HorizontalAlignment="Right"'
        To = 'HorizontalAlignment="Left"'
        Test = 'ExternalLinksGuards.Help_ends_the_top_row_in_line_with_Columns_and_Donate_stands_beside_it'
    },
    @{
        # The heart declared and never drawn - the word alone still reads, so nothing else would
        # notice.
        Name = 'donate: the heart is drawn beside the word'
        File = 'src\Bws.Gui\ScopeBar.xaml'
        From = '<Path Style="{StaticResource SupportMark}" />'
        To = ''
        Test = 'ExternalLinksGuards.Help_ends_the_top_row_in_line_with_Columns_and_Donate_stands_beside_it'
    },
    @{
        # 2026-10-14, the GitHub button. A plaintext address to the repository - the insecure step
        # the support address entry above guards against, on the second constant.
        Name = 'github: the address is the https one the tooltip promises'
        File = 'src\Bws.Gui\ExternalLinks.cs'
        From = 'internal const string RepositoryPage = "https://github.com/donislawdev/BetterWindowsServices";'
        To = 'internal const string RepositoryPage = "http://github.com/donislawdev/BetterWindowsServices";'
        Test = 'GitHubButtonGuards.The_repository_address_is_the_one_the_GitHub_button_promises'
    },
    @{
        # The button declared and never drawn. Donate and Help would still line up, so the guard
        # of the row's right end says nothing about it.
        Name = 'github: the button is drawn left of Donate'
        File = 'src\Bws.Gui\ScopeBar.xaml'
        From = 'ToolTip="{DynamicResource gui.repository.hint}"'
        To = 'ToolTip="{DynamicResource gui.repository.hint}" Visibility="Collapsed"'
        Test = 'GitHubButtonGuards.GitHub_stands_one_gap_left_of_Donate_and_clear_of_the_positions_in_the_narrowest_window'
    },
    @{
        # A way out offered to a session that has nothing to get out of. Most sessions of this tool
        # are elevated, so the inverted trigger is wrong nearly every time somebody sees it.
        #
        # Moved with the status row into StatusRow.xaml on 2026-08-25, when the markup ratchet asked
        # MainWindow.xaml for a seam. stale-check named it in step 3 of the gate, before any
        # mutation had run - which is what that step is for.
        Name = 'elevation: the way out shows only in a session without rights'
        File = 'src\Bws.Gui\StatusRow.xaml'
        From = '<DataTrigger Binding="{Binding Says.NotElevated}" Value="True">'
        To = '<DataTrigger Binding="{Binding Says.NotElevated}" Value="False">'
        Test = 'ActionBarGuards.The_way_out_of_a_session_without_rights_is_beside_the_sentence_about_it'
    },
    @{
        # The file built from the model rather than from the view: the right rows, in an order
        # nobody chose, in a file whose whole promise is "what the list shows".
        Name = 'export: the rows are written in the order somebody sorted them into'
        File = 'src\Bws.Gui\MainWindow.Exporting.cs'
        From = 'CollectionViewSource.GetDefaultView(Entries.ItemsSource) is { } view'
        To = 'CollectionViewSource.GetDefaultView(Entries.ItemsSource) is { } view && false'
        Test = 'ExportingGuards.The_rows_are_in_the_order_somebody_sorted_them_into'
    },
    @{
        # A value carrying a comma written straight into the file, which splits one field into two
        # and silently says something the window never did.
        Name = 'export: a value that would break the format is quoted'
        File = 'src\Bws.Gui\ViewModels\Exporting.cs'
        # Re-anchored 2026-10-05: the separator is the one Windows names (G-8).
        From = 'if (!value.Contains(separator, StringComparison.Ordinal)'
        To = 'if (true || !value.Contains(separator, StringComparison.Ordinal)'
        Test = 'ExportingGuards.A_value_that_would_break_the_format_is_quoted_and_its_quotes_are_doubled'
    },
    @{
        # The file written the way a snapshot is written - no mark - which is right for a file our
        # own reader opens and wrong for one a spreadsheet guesses at.
        Name = 'export: the file carries the mark a spreadsheet needs'
        File = 'src\Bws.Gui\MainWindow.Exporting.cs'
        From = 'AtomicFile.Write(path, Mark + text);'
        To = 'AtomicFile.Write(path, text);'
        Test = 'ExportingGuards.What_lands_on_disk_carries_the_mark_a_spreadsheet_needs'
    },
    @{
        # The step that stops a service where the plan promised a setting. The preview would say
        # "set X to Disabled" and the run would take X down - the one fault ADR-11 stands against.
        Name = 'start type: the step written is the one the plan promised'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = 'steps.Add(PlanSteps.Made(target, StepOperation.SetStartType, StepReason.Requested, to));'
        To = 'steps.Add(PlanSteps.Made(target, StepOperation.Stop, StepReason.Requested, to));'
        Test = 'StartTypeTests.A_run_writes_the_setting_and_asks_the_manager_to_move_nothing'
    },
    @{
        # The title of the panel calling it a restart, which is what the discard arm did until a
        # probe read it off a live window on 2026-08-25.
        Name = 'start type: the panel names the ask rather than calling it a restart'
        File = 'src\Bws.Gui\ViewModels\PlanWords.cs'
        From = 'ActionKind.SetStartType => Texts.Of("gui.plan.doing.setStartType"),'
        To = 'ActionKind.SetStartType => Texts.Of("gui.plan.doing.restart"),'
        Test = 'ActionBarGuards.The_start_type_menu_offers_four_settings_and_one_opens_a_plan_naming_it'
    },
    @{
        # TURNED ROUND ON 2026-08-25, AND THE TURN IS THE POINT. This entry used to break the
        # product by ADDING the fourth kind to the list of asks with a verb, back when the command
        # line had no such verb - the fault being a line that fails the moment somebody pastes it.
        # The command line learned the verb, so the fault is now the other direction: an ask whose
        # verb exists and whose section silently goes away.
        #
        # A registry entry that survived the behaviour reversing without being rewritten would have
        # gone on proving the opposite of what the product does, in green.
        Name = 'start type: the command line offered for this ask is not silently dropped'
        File = 'src\Bws.Core\Planning\EquivalentCommand.cs'
        From = 'kind is ActionKind.Stop or ActionKind.Start or ActionKind.Restart or ActionKind.SetStartType'
        To = 'kind is ActionKind.Stop or ActionKind.Start or ActionKind.Restart'
        Test = 'StartTypeTests.The_command_line_offered_for_this_ask_carries_the_type'
    },
    @{
        # THE VALUE IS THE HALF THAT MAKES IT A COMMAND. A line reading "bws start-type Spooler" is
        # not a shorter way of asking the same thing - the tool refuses it - so dropping the word
        # renders a line that looks right and fails on paste, which is the one fault this whole
        # class is arranged against.
        #
        # The line that comes out is the one in For(ServiceAction), whose other branch is a throw -
        # so the two arms do not become identical after the substitution, which is the shape that
        # comes back BROKEN and proves nothing. The one-line twin of this in For(ReversalStep) has
        # a plain command on its other arm and would have done exactly that.
        Name = 'start type: the rendered command carries the type as well as the name'
        File = 'src\Bws.Core\Planning\EquivalentCommand.cs'
        # RE-ANCHORED 2026-09-24, when the line learned to carry --stop, and again 2026-10-05, when
        # the name learned to be quoted and the line moved into Line (W-12). The word is dropped by a
        # comparison that is never true, so `word` stays read and the mutant compiles.
        From = 'return Line(Verb(action.Kind), action.ServiceName, word, action.AlsoStop ? [AlsoStop] : []);'
        To = 'return Line(Verb(action.Kind), action.ServiceName, word.Length < 0 ? word : null, action.AlsoStop ? [AlsoStop] : []);'
        Test = 'EquivalentCommandBridgeGuards.The_start_type_the_core_writes_is_one_this_tool_reads_back'
    },
    @{
        # A WAY BACK OFFERED FOR AN ENTRY WHOSE PREVIOUS TYPE NOBODY KNOWS. Two entries reach this:
        # one whose configuration read was refused, and one marked to start late - and the second is
        # the one that would have gone unnoticed, because "automatic" is a word this tool has and
        # "automatic, and late" is not. Measured on a real machine 2026-08-25: this tool writes the
        # start type and leaves the delay flag exactly as it found it, so the word alone does not
        # say which of the two an entry ends up as.
        Name = 'start type: no way back is offered where the type before is not known'
        File = 'src\Bws.Core\Planning\StartSetting.cs'
        #
        # RE-ANCHORED 2026-09-24: the way back reads the entry through StartSettings.Of, the table
        # the writer uses, and this is where "nobody could read the late flag" now lives. Letting a
        # DENIED flag through makes an automatic entry whose flag was refused read as plain
        # automatic - so the way back would put a possibly delayed entry back starting at boot,
        # which is the exact mistake worth a guard. HalfRead in the theory is that entry.
        From = 'entry.DelayedAuto.Outcome is not (ReadOutcome.Present or ReadOutcome.Absent)'
        To = 'entry.DelayedAuto.Outcome is not (ReadOutcome.Present or ReadOutcome.Absent or ReadOutcome.Denied)'
        Test = 'StartTypeTests.No_way_back_is_offered_where_the_type_before_is_not_known'
    },
    @{
        # The value the way back names is the type the entry had BEFORE, and nothing else in this
        # product knows it - a step knows what it set and a result knows what came of it. Reading
        # the type it was SET to instead renders a line that puts the entry where it already is,
        # in the calm voice of an undo.
        Name = 'start type: the way back names the type from before rather than the one written'
        File = 'src\Bws.Core\Planning\NetEffect.cs'
        From = '                    Step: new ReversalStep(written.Key, StepOperation.SetStartType, written.Value.From),'
        To = '                    Step: new ReversalStep(written.Key, StepOperation.SetStartType, written.Value.To),'
        Test = 'StartTypeTests.The_way_back_names_the_type_the_entry_had_before'
    },
    @{
        # Four hundred drivers, four hundred sentences differing only by a name. The scope bar made
        # that reachable by accident - while drivers sat behind a query, picking them by the hundred
        # was not something anybody did. Backlog 217.
        #
        # One kind is left gathering so the branch below stays live: blanking the whole answer would
        # make the other arm unreachable, and a constant condition is an error here.
        Name = 'panel: refusals that say the same thing are gathered rather than repeated'
        File = 'src\Bws.Gui\ViewModels\PlanWords.cs'
        From = 'kind is PlanProblemKind.UnknownService or PlanProblemKind.NotOperable;'
        To = 'kind is PlanProblemKind.UnknownService;'
        Test = 'PlanRefusalGuards.Refusals_that_say_the_same_thing_come_back_as_one_sentence_with_a_count'
    },
    @{
        # THE OTHER DIRECTION, AND IT IS THE ONE THAT BREAKS A RULE RATHER THAN AN APPEARANCE. A
        # cascade refusal names the drivers standing in the way, so two of them are two different
        # facts however alike the wording either side of the list reads. Gathered, the panel would
        # say two things were refused without saying what either was in the way of - rule 8 of the
        # untouchable list, met on the one screen where somebody is deciding whether to press.
        Name = 'panel: refusals that name other entries are never gathered'
        File = 'src\Bws.Gui\ViewModels\PlanWords.cs'
        From = 'kind is PlanProblemKind.UnknownService or PlanProblemKind.NotOperable;'
        To = 'kind is PlanProblemKind.UnknownService or PlanProblemKind.NotOperable or PlanProblemKind.CascadeNotOperable;'
        Test = 'PlanRefusalGuards.Refusals_that_name_other_entries_are_never_gathered'
    },
    @{
        # The refusals keep the order somebody picked things in, because the list beside them - the
        # steps - is in the order they will happen. Two lists on one screen in two unrelated orders
        # is a screen somebody has to hold two ideas about.
        Name = 'panel: a gathered refusal keeps the place its first member had'
        File = 'src\Bws.Gui\ViewModels\PlanWords.cs'
        From = '                .OrderBy(group => group.Min(one => one.at))'
        To = '                .OrderByDescending(group => group.Min(one => one.at))'
        Test = 'PlanRefusalGuards.A_gathered_group_keeps_the_place_its_first_member_had'
    },
    @{
        # THE ONE MISTAKE IN THIS FIELD THAT KEEPS EVERY COUNT PLAUSIBLE.
        #
        # An instance carries the template bit as well as its own - measured 0xe0 on a real
        # machine - so asking about the template bit first files every instance as a template.
        # The family stays the same size, nothing goes missing, and half of it is wrong. This
        # mutation is the reordering written out, not a nonsense edit.
        #
        # The test that catches it is on the real machine rather than over specimens, and that
        # is the point: the specimens carry whichever role somebody typed into them, so they
        # cannot disagree with a mapping they never go through.
        Name = 'per-user: the instance bit is asked about before the template bit'
        File = 'src\Bws.Core\ManagerTerms.cs'
        From = @'
        if ((type & PerUserInstance) == PerUserInstance)
        {
            return Core.PerUserRole.Instance;
        }

        return (type & PerUserTemplate) == PerUserTemplate
            ? Core.PerUserRole.Template
            : Core.PerUserRole.None;
'@
        To = @'
        if ((type & PerUserTemplate) == PerUserTemplate)
        {
            return Core.PerUserRole.Template;
        }

        return (type & PerUserInstance) == PerUserInstance
            ? Core.PerUserRole.Instance
            : Core.PerUserRole.None;
'@
        Test = 'ListingContractTests.The_two_sides_of_the_per_user_family_are_not_swapped'
    },
    @{
        # The field has to survive the journey into the document, not just exist on the entry.
        # Writing a constant here is what a hurried refactor looks like, and every count in the
        # listing would still add up to 798.
        Name = 'per-user: the document carries the role the entry has'
        File = 'src\Bws.Core\Snapshots\EntryDocument.cs'
        From = '            PerUserRole = entry.PerUserRole.ToString(),'
        To = '            PerUserRole = Core.PerUserRole.None.ToString(),'
        Test = 'ListingContractTests.The_two_sides_of_the_per_user_family_are_not_swapped'
    },
    @{
        # peruser:yes is a group of the two sides, the same shape as type:driver. Losing one
        # member of the group is the quiet version: the word still answers, with half the family.
        Name = 'per-user: yes covers both sides of the family'
        File = 'src\Bws.Core\Querying\QueryValueNames.cs'
        From = 'new QueryValueName("yes", "template", "instance"),'
        To = 'new QueryValueName("yes", "template"),'
        Test = 'QueryOverSpecimensTests.Yes_is_the_two_sides_together_and_no_is_the_rest_of_the_machine'
    },
    @{
        # RULE 8 SPELLED AS AN OPTION, WHICH IS THE ONE THING --full MUST NOT BE ABLE TO DO.
        #
        # The switch decides whether fields that are genuinely EMPTY are printed. Widening it by
        # one state - so that a field nobody could read is hidden too - reads like a tidier report
        # and is a refusal swallowed. Nothing about the shape of the output would look wrong.
        Name = 'show: the switch about empty fields cannot hide a failed read'
        File = 'src\Bws.Cli\EntryReport.cs'
        From = '.Where(pair => full || pair.Told.Outcome != ReadOutcome.Absent)'
        To = '.Where(pair => full || pair.Told.Outcome == ReadOutcome.Present)'
        Test = 'EntryReportTests.A_field_nobody_could_read_is_printed_without_being_asked_for'
    },
    @{
        # And the other half: a switch that prints everything either way is a switch that does
        # nothing, which is the silence the belonging table for options exists to end.
        Name = 'show: without the switch an empty field is left out'
        File = 'src\Bws.Cli\EntryReport.cs'
        From = '.Where(pair => full || pair.Told.Outcome != ReadOutcome.Absent)'
        To = '.Where(pair => pair.Told.Outcome != ReadOutcome.Denied || full || true)'
        Test = 'EntryReportTests.A_field_that_is_genuinely_empty_is_left_out_until_it_is_asked_for'
    },

    # THE SIX BELOW ARE `A11` IN THE WINDOW - folding a session's copy under the template it came
    # from. The first is the one worth having: a plan built from a folded row has to name the whole
    # family, or one press stops four services while the preview shows one, which is the single
    # failure `ADR-11` exists to prevent.
    @{
        # THE PREVIEW GOES SHORT AND NOTHING LOOKS WRONG. The row still says "2 instances", the plan
        # still opens, and the steps in it name one entry - the specification's own warning, word
        # for word: "admin kliknie stop na jednym wierszu i zatrzyma cztery uslugi".
        Name = 'folding: a plan over a folded row names every copy under it'
        File = 'src\Bws.Gui\MainWindow.Menu.cs'
        From = '            names.AddRange(row.Instances.Select(instance => instance.ServiceName));'
        To = ''
        Test = 'FoldingGuards.A_plan_over_a_folded_row_names_the_template_and_every_copy_under_it'
    },
    @{
        # THE OTHER HALF OF THE SAME PLAN, AND IT IS THE ONE A READING OF THE GLOSSARY WOULD GET
        # WRONG. Leaving the template out is a change that evaporates at the next logon: the copies
        # that exist are set and the pattern the next one is made from is not.
        Name = 'folding: a plan over a folded row names the template as well as its copies'
        File = 'src\Bws.Gui\MainWindow.Menu.cs'
        From = '            names.Add(row.ServiceName);'
        To = '            if (row.Instances.Count == 0) { names.Add(row.ServiceName); }'
        Test = 'FoldingGuards.A_plan_over_a_folded_row_names_the_template_and_every_copy_under_it'
    },
    @{
        # THE BITS DECIDE THE ROLE AND THE NAME ONLY LINKS TWO ROWS THE BITS HAVE SPOKEN ABOUT.
        # Dropping this question is how a window comes to fold by suffix, which `docs/03` measured
        # as wrong about two entries in 798 - and the entry it hides is the power service.
        Name = 'folding: what folds is decided by the type bits rather than by the name'
        File = 'src\Bws.Gui\ViewModels\Folding.cs'
        From = '                || row.Entry.PerUserRole != PerUserRole.Instance'
        To = ''
        Test = 'FoldingGuards.A_service_that_only_looks_like_a_copy_of_a_real_template_is_left_alone'
    },
    @{
        # WITHOUT THIS CLAUSE THE FOLD HIDES EXACTLY WHAT SOMEBODY ASKED FOR. `peruser:instance` is
        # the query that exists to find session copies, and it selects no template at all - so an
        # instance folded without one goes under a row that is not on screen and disappears.
        Name = 'folding: a copy folds only when the template it came from is in the same answer'
        File = 'src\Bws.Gui\ViewModels\Folding.cs'
        From = '                || !templates.Contains(template))'
        To = ')'
        Test = 'FoldingGuards.An_instance_whose_template_is_not_in_the_answer_stays_a_row_of_its_own'
    },
    @{
        # A ROW THAT KEEPS A FAMILY IT NO LONGER STANDS FOR IS A ROW A PLAN IS BUILT FROM WRONGLY.
        # Saying it only to the rows that gained something leaves the badge and the list behind on
        # every row that lost one, and the screen and the plan stop being the same answer.
        Name = 'folding: a row that stops standing for a family is told so'
        File = 'src\Bws.Gui\ViewModels\Folding.cs'
        From = '            row.StandsAlsoFor(under.TryGetValue(row.ServiceName, out var mine) ? mine : []);'
        To = '            if (under.TryGetValue(row.ServiceName, out var mine)) { row.StandsAlsoFor(mine); }'
        Test = 'FoldingGuards.A_row_that_stops_standing_for_a_family_stops_saying_that_it_does'
    },
    @{
        # RULE 8 WHERE A FOLD BREAKS IT. The number over the list counts entries and the list under
        # it is shorter, so the difference is owed a sentence - without one the window has quietly
        # taken rows off the screen and gone on printing the old count over them.
        #
        # `folded < 0` rather than `false`, and the difference is the canary's own lesson: a
        # constant condition makes the block unreachable, warnings are errors here, and the entry
        # comes back BROKEN - which proves nothing about the guard it names.
        # THE ANCHOR MOVED ON 2026-09-01 AND stale-check FOUND IT THE SAME MINUTE. Backlog 263 put
        # a second condition on this line, so the old anchor stopped matching - which is exactly
        # the shape that cost the previous session a full gate run, caught here by running the
        # cheap half before the expensive one, as this file's own header asks.
        Name = 'folding: the window says how many copies it folded away'
        File = 'src\Bws.Gui\ViewModels\Sentences.cs'
        # RE-ANCHORED 2026-09-15: the sentence is cut in three for the link, so the test moved to Folded.
        From = '        folded > 0 && listOnScreen'
        To = '        folded < 0 && listOnScreen'
        Test = 'FoldingGuards.The_window_says_how_many_copies_it_folded_away'
    },
    @{
        # A TEMPLATE IS NEVER RUNNING - 0 of 23 measured on a real machine against 8 of its 23
        # copies - so the Status column of a folded row says "Stopped" about a family that is
        # working. The badge is the only thing carrying that, and a badge that forgot it would put
        # the false alarm `peruser:` had just removed from the query into the most visible column
        # in the window.
        Name = 'folding: the count beside a folded name says how many of the family are running'
        File = 'src\Bws.Gui\ViewModels\Folding.cs'
        From = '        var running = instances.Count(row => row.Entry.Status == EntryStatus.Running);'
        To = '        var running = 0;'
        Test = 'FoldingGuards.The_count_beside_the_name_says_how_many_of_the_family_are_running'
    },
    @{
        # THIS SHIPPED THE OTHER WAY ROUND AND THE OWNER PHOTOGRAPHED IT: a row of their own list
        # reading "...1 instance", a service with no name on it. A Grid gives an Auto column what it
        # asks for and a star column whatever is left over, so with the name in the star column the
        # note about the row survived whole and the identity was trimmed to an ellipsis.
        #
        # `ADR-14` is what decides which of the two may vanish. The internal name is the string that
        # is never translated, the one somebody types into a terminal, and the one every plan and
        # every clipboard copy carries. A count of folded copies is a note ABOUT that row and may
        # not be the half that survives.
        #
        # THE COLUMNS ARE SWAPPED RATHER THAN THE WIDTHS BECAUSE THE WIDTHS ARE NOT UNIQUE - the
        # Auto-then-star pair appears four times in this file, and a plain string replacement would
        # edit all four while claiming to have edited one.
        Name = 'folding: a name column too narrow for both keeps the name and drops the count'
        File = 'src\Bws.Gui\Themes\Cells.xaml'
        From = @'
            <TextBlock Grid.Column="0"
                       Text="{Binding [serviceName]}"
                       Style="{StaticResource CellText}" />
            <TextBlock Grid.Column="1"
                       Text="{Binding StandsFor}"
                       Style="{StaticResource CellBadge}" />
'@
        To = @'
            <TextBlock Grid.Column="1"
                       Text="{Binding [serviceName]}"
                       Style="{StaticResource CellText}" />
            <TextBlock Grid.Column="0"
                       Text="{Binding StandsFor}"
                       Style="{StaticResource CellBadge}" />
'@
        Test = 'FoldingGuards.A_name_column_too_narrow_for_both_gives_way_on_the_count_and_not_on_the_name'
    },
    # THE SEVEN BELOW ARE `G` - what this machine looks like before anybody asks it anything. The
    # first two are the ones worth having: a first screen answering the LITERAL question would say
    # eight where one is true, and a first screen nobody can get back to appears once in the life of
    # a profile.
    @{
        # THE FALSE ALARMS COME BACK AND THE SCREEN LOOKS FINE. Measured on a real machine: the
        # literal query answers 7 or 8, six or seven of them per-user templates whose session copy is
        # running and entries stopped because nothing has asked for them. A window opening with that
        # number is confidently wrong in the first thing anybody reads.
        Name = 'overview: the headline leaves out what is not actually down'
        File = 'src\Bws.Gui\ViewModels\Overview.cs'
        From = 'Line(Texts.Of("gui.overview.notUp"), $"{NotUp} peruser:no trigger:none", rows, leading: true, counted),'
        To = 'Line(Texts.Of("gui.overview.notUp"), $"{NotUp}", rows, leading: true, counted),'
        Test = 'OverviewGuards.The_headline_leaves_out_the_two_false_alarms_and_names_both_of_them'
    },
    @{
        # AND THE OTHER HALF, because a headline of 1 with nothing under it is a window being
        # quietly incomplete rather than confidently wrong. Both exclusions are on screen with their
        # own counts and their own queries, so the person can click either and see what was taken.
        Name = 'overview: what the headline leaves out is named beside it'
        File = 'src\Bws.Gui\ViewModels\Overview.cs'
        From = 'Line(Texts.Of("gui.overview.notUp.template"), $"{NotUp} peruser:template", rows, leading: false, counted),'
        To = ''
        Test = 'OverviewGuards.The_headline_leaves_out_the_two_false_alarms_and_names_both_of_them'
    },
    @{
        # A NUMBER COUNTED OVER THE WHOLE MACHINE AND A CLICK THAT LANDS ON THE SERVICES LIST is the
        # screen contradicting itself in the one gesture `G` promises.
        Name = 'overview: every number is counted inside the list the click lands on'
        File = 'src\Bws.Gui\ViewModels\Overview.cs'
        From = 'Line(Texts.Of("gui.overview.running"), $"status:running {Services}", rows, leading: true, counted),'
        To = 'Line(Texts.Of("gui.overview.running"), "status:running", rows, leading: true, counted),'
        Test = 'OverviewGuards.No_number_counts_a_driver'
    },
    @{
        # THE ORPHAN OF `C13` IS A COMPOSITION, NOT A WORD - an automatic entry whose file is gone.
        # Collapsing it to the file half alone counts four entries on this machine where none is an
        # orphan, which is the alarm this screen exists to avoid raising.
        Name = 'overview: an orphan is more than a missing file'
        File = 'src\Bws.Gui\ViewModels\Overview.cs'
        From = 'Line(Texts.Of("gui.overview.orphans"), $"file:missing start:auto {Services}", rows, leading: true, counted),'
        To = 'Line(Texts.Of("gui.overview.orphans"), $"file:missing {Services}", rows, leading: true, counted),'
        Test = 'OverviewGuards.An_orphan_is_told_apart_from_an_entry_that_merely_lost_its_file'
    },
    @{
        # THE NUMBERS ARE COUNTED FROM A LISTING THAT ARRIVES AFTER THE WINDOW DOES. This is the
        # fault the screen shipped with for one build - six zeroes on a machine with 798 entries -
        # and it was found by opening the window rather than by reasoning.
        Name = 'overview: the numbers are counted again once the machine has been read'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.cs'
        From = @'
        if (_showingOverview)
        {
            Raise(nameof(Overview));
            Raise(nameof(OverviewFindings));
        }
'@
        To = ''
        Test = 'OverviewGuards.The_numbers_are_counted_again_once_the_machine_has_been_read'
    },
    @{
        # A SCREEN NOBODY CAN GET BACK TO APPEARS ONCE IN THE LIFE OF A PROFILE, and the person it
        # is written for opens this tool on a new server every time.
        Name = 'overview: the bar can ask for it back'
        File = 'src\Bws.Gui\MainWindow.Overview.cs'
        From = 'Actions.OverviewRequest += (_, _) => _model.ShowingOverview = true;'
        To = ''
        Test = 'OverviewGuards.The_bar_can_ask_for_it_back'
    },
    @{
        # PUTTING IT AWAY HAS TO SURVIVE THE PROCESS. Without the write, a first run ended by a
        # machine going down shows this screen again to somebody who has already read it - and every
        # probe in tools/ would open on it rather than on the list it exists to measure.
        Name = 'overview: putting it away is written down'
        File = 'src\Bws.Gui\KeptColumns.cs'
        # Re-anchored 2026-09-24: the write goes through KeepTheProfile since the filters joined it.
        From = 'KeepTheProfile(_layouts with { OverviewSeen = true }, says);'
        To = 'KeepTheProfile(_layouts, says);'
        Test = 'OverviewGuards.Putting_the_overview_away_is_written_down_and_read_back'
    },
    @{
        # The seventh theme file, following the same rule as the three above it: the entries follow
        # the FILES rather than the argument, because a guard that had quietly gone back to naming
        # the old set by hand would pass on the one that was added last.
        Name = 'appearance: no value drifts into the file that holds the marks'
        File = 'src\Bws.Gui\Themes\Marks.xaml'
        From = '<Style x:Key="MarkShape" TargetType="Ellipse">'
        To = '<Thickness x:Key="DriftedValue">1</Thickness><Style x:Key="MarkShape" TargetType="Ellipse">'
        Test = 'AppearanceGuards.The_seams_between_the_theme_files_are_where_all_of_them_say_they_are'
    },
    @{
        # The pointer half of the bound. Without it the walk follows whatever the block claims,
        # and a block claiming nothing at address zero is where the program ends.
        Name = 'blocks: a trigger pointer that lands nowhere is not followed'
        File = 'src\Bws.Core\ManagerBlocks.cs'
        From = 'if (info.pTriggers is null || offset < 0 || offset >= buffer.Length)'
        To = 'if ((info.pTriggers is null || offset < 0 || offset >= buffer.Length) && false)'
        Test = 'ManagerBlockTests.A_count_with_a_pointer_to_nowhere_is_refused_rather_than_followed'
    },
    @{
        # The count half. Max rather than a plain removal, so that `room` is still used and the
        # tree still compiles - the "mutate a value, not a proof" advice from the header.
        Name = 'blocks: a trigger count is held down to what the block holds'
        File = 'src\Bws.Core\ManagerBlocks.cs'
        From = 'var count = Math.Min(info.cTriggers, room);'
        To = 'var count = Math.Max(info.cTriggers, room);'
        Test = 'ManagerBlockTests.A_count_larger_than_the_block_is_held_down_to_what_the_block_holds'
    },
    @{
        # Sends the list fields back through the plain text comparison, which is order for order
        # and capital for capital.
        Name = 'diff: a list is compared as a set rather than as its text'
        File = 'src\Bws.Core\Snapshots\SnapshotDiff.cs'
        From = 'node is JsonArray items && Unordered.Contains(field, StringComparer.Ordinal)'
        To = 'node is JsonArray items && Unordered.Contains(field, StringComparer.Ordinal) && false'
        Test = 'SnapshotDiffTests.A_privilege_spelled_differently_is_the_same_privilege'
    },
    @{
        # Leaves the window with no memory of what it has already been to fetch, so every tick
        # decides the question is still unanswered and goes back to the manager for ever.
        Name = 'window: what the pass read counts as tried'
        # RE-ANCHORED 2026-09-03: the size ratchet moved the expensive pass into its own file.
        #
        # RE-ANCHORED AGAIN 2026-09-05, and the line it points at changed meaning rather than
        # moving. The passes were split, so the window no longer records both families after
        # filling both - it records what was ASKED for, before the work, which is also what keeps
        # a pass that throws from being asked for again a second later. Same test, because the
        # upper bound in it is what catches this: exactly one more reading, not "at least one".
        File = 'src\Bws.Gui\ViewModels\Readings.SecondPhase.cs'
        From = '        _tried |= wanted;'
        To = ''
        Test = 'SecondPhaseTests.The_second_question_does_not_pay_for_the_pass_again'
    },
    @{
        # Puts the two families back into one pass, which is what made the cheap answer cost the
        # expensive one's price: a question about memory would open 810 binaries to verify them.
        Name = 'window: memory is read without the signature pass behind it'
        File = 'src\Bws.Gui\ViewModels\Readings.SecondPhase.cs'
        From = '        if (wanted.HasFlag(ExtraRead.Signatures))'
        To = '        if (wanted != ExtraRead.None)'
        Test = 'SecondPhaseTests.A_question_about_memory_alone_opens_no_files'
    },
    @{
        # THE DEFECT EXACTLY AS THE OWNER MET IT, reported 2026-09-05: the memory column read
        # "unknown" on all 810 rows for ever, because only a typed member could ask for the family
        # that fills it and turning the column on reached nobody but the layout file.
        Name = 'window: a shown column asks for what fills it'
        # RE-ANCHORED THE SAME DAY IT WAS WRITTEN: the size ratchet fired on MainViewModel.cs and
        # this member led the block that came out - what the window asks the machine for, and when
        # it stops asking.
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Asking.cs'
        From = '    private ExtraRead Asked => _query.Needs | (ColumnsNeed?.Invoke() ?? ExtraRead.None);'
        To = '    private ExtraRead Asked => _query.Needs;'
        Test = 'SecondPhaseTests.Showing_a_column_is_what_sends_the_window_to_read_what_fills_it'
    },
    @{
        # Hands every entry back untouched, so the pass runs, costs its time and fills nothing - and
        # the column, the field and the snapshot all go on saying nobody looked. Re-anchored
        # 2026-09-29 when the loop became Parallel.For, breaking the same behaviour as before.
        Name = 'dependencies: the pass asks the manager about each entry'
        File = 'src\Bws.Core\RequiredByPass.cs'
        From = 'RequiredBy = catalog.ReadDependents(entries[index].ServiceName)'
        To = 'RequiredBy = entries[index].RequiredBy'
        Test = 'RequiredByPassTests.Each_entry_is_asked_about_once_and_keeps_its_own_answer'
    },
    @{
        # Leaves the language unable to say that this direction costs a call, so a query about it
        # answers "no matches" over a field nobody read - the one failure this whole language is
        # arranged to avoid.
        Name = 'dependencies: only the direction that costs a call declares a family'
        File = 'src\Bws.Core\Querying\QueryFields.cs'
        From = '            Needs = ExtraRead.RequiredBy,'
        To = ''
        Test = 'DependencyQueryTests.Only_the_direction_that_costs_a_call_declares_a_family'
    },
    @{
        # The switch parses, belongs to the verb, prints in the help, and reads nothing.
        Name = 'dependencies: --required-by actually sends the listing to read them'
        File = 'src\Bws.Cli\Program.cs'
        # NOT `catalog is null`, WHICH IS WHAT THIS SAID FIRST AND CAME BACK BROKEN: the compiler
        # then knows the handle is null inside the block, and passing it to a parameter that does
        # not take null is a warning, which is an error here. A mutation that does not compile
        # proves nothing either way - the note at the head of this file says so.
        #
        # Dropping the SWITCH and keeping the query path is the honest weakening anyway: it is
        # exactly the build where --required-by parses, belongs to the verb, prints in the help,
        # and reads nothing.
        From = '        if (catalog is not null && (options.RequiredBy || needs.HasFlag(ExtraRead.RequiredBy)))'
        To = '        if (catalog is not null && needs.HasFlag(ExtraRead.RequiredBy))'
        Test = 'ListingContractTests.Who_depends_on_a_service_is_read_when_asked_for_and_left_alone_when_not'
    },
    @{
        # The window's own half of the same fault: the column is on screen and nothing tells the
        # reading to go and fetch what fills it.
        Name = 'columns: the required-by column declares the family that fills it'
        File = 'src\Bws.Gui\ViewModels\Columns.cs'
        From = '            Needs = ExtraRead.RequiredBy,'
        To = ''
        Test = 'ColumnFamilyGuards.Every_column_fed_by_the_second_phase_declares_the_family_that_feeds_it'
    },
    @{
        # A column fed by the second phase that does not say which family feeds it. All five were
        # in this state until 2026-09-05, and nothing anywhere could tell.
        Name = 'columns: the memory column declares the family that fills it'
        File = 'src\Bws.Gui\ViewModels\Columns.cs'
        From = '            Needs = ExtraRead.Memory,'
        To = ''
        # RE-ANCHORED THE SAME DAY: the ratchet on test files moved this guard into its own
        # class, because ColumnGuards was four lines under five hundred and this test is eighty.
        Test = 'ColumnFamilyGuards.Every_column_fed_by_the_second_phase_declares_the_family_that_feeds_it'
    },
    @{
        # A column pointing at a field the language does not have. Nothing fails when this happens:
        # ValuesOf answers with an empty list for a name nobody knows, which is exactly what it
        # answers for a column that takes free text - so the heading just offers no values.
        Name = 'columns: a heading names a field the language really has'
        File = 'src\Bws.Gui\ViewModels\ColumnFilters.cs'
        From = '["status"] = "status",'
        To = '["status"] = "state",'
        Test = 'ColumnMenuGuards.Every_column_that_names_a_query_field_names_one_the_language_has'
    },
    @{
        # Turns the heading menu from "narrow to this" into "hide this". The tick would light beside
        # running while the box hid every running service, which is the worst shape a filter has.
        Name = 'columns: a value in the heading menu narrows to it rather than away from it'
        File = 'src\Bws.Gui\ViewModels\FilterChips.cs'
        From = '        new(() => value, field, value, negated: false, read, write);'
        To = '        new(() => value, field, value, negated: true, read, write);'
        Test = 'ColumnMenuGuards.Ticking_a_value_writes_the_member_into_the_query_and_unticking_takes_it_out'
    },
    @{
        # Lets the heading menu put away the last column still showing, which leaves 809 rows of
        # nothing above a count line still saying 809 - the second door into the fault the picker
        # already closes at its own.
        Name = 'columns: the heading menu obeys the last column rule too'
        File = 'src\Bws.Gui\ViewModels\ColumnMenu.cs'
        From = '    public bool MayHide => _choice is { MayHide: true };'
        To = '    public bool MayHide => true;'
        Test = 'ColumnMenuGuards.A_column_is_put_away_through_the_pickers_own_choice_and_the_last_one_is_refused'
    },
    @{
        # Stops the walk up the visual tree at whatever the pointer was actually over. A pointer
        # lands on the TEXT inside a heading far more often than on the heading, so right clicking a
        # column would do nothing at all while the code still reads as though it works.
        Name = 'columns: a right click inside a heading still finds the heading'
        File = 'src\Bws.Gui\MainWindow.Columns.cs'
        From = '        while (node is not null and not System.Windows.Controls.Primitives.DataGridColumnHeader)'
        To = '        while (node is null)'
        # RE-ANCHORED THE SAME DAY: moved to the class that owns this menu's other five guards,
        # when the ratchet on test files fired on WindowGuards.
        Test = 'ColumnMenuGuards.A_right_click_on_a_heading_opens_that_columns_menu_and_one_on_nothing_opens_nothing'
    },
    @{
        # Hands the spreadsheet back its formula.
        Name = 'export: a value a spreadsheet would run is made inert'
        File = 'src\Bws.Gui\ViewModels\Exporting.cs'
        From = 'value.Length > 0 && Dangerous.Contains(value[0])'
        To = 'value.Length > 0 && Dangerous.Contains(value[0]) && false'
        Test = 'ExportingGuards.A_value_a_spreadsheet_would_evaluate_is_made_inert'
    },
    @{
        # Points the guard at a verb that never carries a start type, so the shape it exists to
        # refuse gets through. Not "&& false", which would make the throw unreachable and turn a
        # warnings-are-errors build into BROKEN.
        Name = 'plan: setting a start type without one is refused where the plan is built'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = 'if (action.Kind == ActionKind.SetStartType && action.To is null)'
        To = 'if (action.Kind == ActionKind.Restart && action.To is null)'
        Test = 'StartTypeTests.A_start_type_change_with_no_type_is_refused_before_anything_is_planned'
    },
    @{
        # Lets an empty value through as a value, which is the fourth spelling of "a script with a
        # typo gets the whole machine and a green light".
        Name = 'command line: an option given nothing is incomplete rather than empty'
        File = 'src\Bws.Cli\CommandLine.Reading.cs'
        From = 'if (!string.IsNullOrWhiteSpace(value))'
        To = 'if (value is not null)'
        Test = 'ArgumentRefusalTests.An_empty_query_written_as_a_separate_word_is_incomplete'
    },
    @{
        # Puts the question back on the SELECTED row rather than on the one the panel is open on -
        # two things this window deliberately keeps apart.
        Name = 'panel: it answers for the entry it is open on'
        File = 'src\Bws.Gui\ViewModels\Chosen.cs'
        From = 'if (!Showing || _followed is not { } row)'
        To = 'if (!Showing || Row is not { } row)'
        Test = 'ChosenGuards.The_panel_answers_for_its_own_entry_rather_than_for_whatever_is_selected'
    },
    @{
        # Sends a configuration change back through the ordering that asks the manager one question
        # per selected entry to decide an order that does not matter.
        Name = 'bulk: setting a start type asks nothing about dependents'
        File = 'src\Bws.Core\Planning\BulkPlanBuilder.cs'
        # RE-ANCHORED 2026-09-24 with the method it lives in. Turning the plain setting to the
        # ordered side is what the test catches: a selection being disabled asks the manager.
        From = '(action.Kind == ActionKind.SetStartType && !action.AlsoStop)'
        To = '(action.Kind == ActionKind.SetStartType && action.AlsoStop)'
        Test = 'BulkPlanTests.Setting_a_start_type_asks_the_manager_nothing_about_dependents'
    },
    @{
        # A length nothing can have, so the check stands and never fires. The answer then comes
        # from where it used to - after eight hundred entries have been read.
        Name = 'refusals: show without a name is answered before the manager is opened'
        File = 'src\Bws.Cli\Refusals.cs'
        From = 'if (options.Kind == CommandKind.Show && options.ServiceName.Length == 0)'
        To = 'if (options.Kind == CommandKind.Show && options.ServiceName.Length == -1)'
        Test = 'ArgumentRefusalTests.Show_without_a_name_is_answered_before_the_manager_is_opened'
    },
    @{
        # Turns "either side missing" into "both sides missing", so one file on its own gets past.
        Name = 'refusals: a comparison with one side is answered before the manager is opened'
        File = 'src\Bws.Cli\Refusals.cs'
        From = 'if (options.Path.Length == 0 || (options.Against.Length == 0 && !options.Live))'
        To = 'if (options.Path.Length == 0 && (options.Against.Length == 0 && !options.Live))'
        Test = 'ArgumentRefusalTests.A_comparison_with_one_side_is_answered_before_the_manager_is_opened'
    },
    @{
        # The decoder that substitutes rather than the one that refuses - the choice this file made
        # by accident until 2026-08-26, while its neighbour had measured the cost of it.
        Name = 'layout: bytes that are not text are refused rather than guessed at'
        File = 'src\Bws.Gui\PreferencesFile.cs'
        From = 'new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),'
        To = 'new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),'
        Test = 'PreferencesFileGuards.Bytes_that_are_not_text_this_build_reads_are_refused_rather_than_guessed_at'
    },
    @{
        # A filter that never matches, so the throw travels again - out of a static initialiser,
        # which is how this used to end with no window at all.
        Name = 'texts: a language file that is not one leaves english standing'
        File = 'src\Bws.Gui\Texts.cs'
        #
        # A FILTER NAMING SOMETHING THAT IS NEVER TRUE THERE, RATHER THAN A CONSTANT. `when (false)`
        # was tried first and came back BROKEN - the compiler sees a constant filter and the build
        # refuses it, and warnings are errors here. `source` is checked for null five lines above,
        # so this filter can never match and nothing about it is constant.
        From = 'catch (JsonException)'
        To = 'catch (JsonException) when (source is null)'
        Test = 'LanguageGuards.A_translation_that_is_not_readable_json_leaves_english_standing'
    },
    @{
        Name = 'texts: a sentence that cannot hold its values comes back unformatted'
        File = 'src\Bws.Gui\Texts.cs'
        # Same shape as the entry above and for the same reason - a filter that is never true rather
        # than one the compiler can fold. Not `template is null`, which was tried and came back
        # BROKEN: the parameter is not nullable, so the compiler has an opinion about that test. A
        # length below zero is impossible and the compiler does not know it.
        From = 'catch (FormatException)'
        To = 'catch (FormatException) when (values.Length < 0)'
        Test = 'LanguageGuards.A_translation_asking_for_a_value_nobody_handed_over_does_not_throw'
    },
    @{
        # Puts back one of the four values that were written in the other convention, which is the
        # shape this document carried from the day the second one was added until 2026-08-26. It
        # broke nothing here - the query language normalises spelling - so the only reader it could
        # hurt was somebody else's script, silently.
        Name = 'document: an enumerated value is spelled the way its enumeration spells it'
        File = 'src\Bws.Core\Snapshots\EntryDocument.cs'
        From = 'SidType = entry.SidType.IsPresent ? entry.SidType.Value.ToString() : null,'
        To = 'SidType = entry.SidType.IsPresent ? entry.SidType.Value.ToString().ToLowerInvariant() : null,'
        Test = 'DocumentValueGuards.Every_enumerated_value_is_a_member_spelled_the_way_the_enumeration_spells_it'
    },
    @{
        # The number is the only thing that turns "your file is from an older build" into a sentence
        # its owner can act on. Moved without anybody arguing for it, it silently stops older
        # snapshots from being readable - so the pin is a literal in the test rather than a read of
        # this constant, which would agree with whatever it was moved to.
        # Sends the four names Windows itself matches without case back through an exact comparison,
        # which is what reported LocalSystem against localsystem as drift.
        Name = 'diff: a name the system matches without case is not a difference'
        File = 'src\Bws.Core\Snapshots\SnapshotDiff.cs'
        From = 'return TheSystemIgnoresCase.Contains(field, StringComparer.Ordinal)'
        To = 'return TheSystemIgnoresCase.Contains(field, StringComparer.Ordinal) && false'
        Test = 'SnapshotDiffTests.A_name_the_system_matches_without_case_is_not_a_difference'
    },
    @{
        # Takes one query off the screen. The other tests here would go red about COUNTS, which is
        # a different sentence from "this question is one the language cannot answer".
        Name = 'overview: every question this screen asks parses'
        File = 'src\Bws.Gui\ViewModels\Overview.cs'
        # RE-ANCHORED 2026-09-24: the second number of the orphan card counts `!start:auto` since
        # UX-GUI-015 (b).
        From = 'Line(Texts.Of("gui.overview.fileGone"), $"file:missing !start:auto {Services}", rows, leading: false, counted)'
        To = 'Line(Texts.Of("gui.overview.fileGone"), $"file:missing !start:auto {Services}", rows, leading: false, counted), Line(Texts.Of("gui.overview.fileGone"), "", rows, leading: false, counted)'
        Test = 'OverviewGuards.Every_question_this_screen_asks_parses'
    },
    @{
        # A list dropped from the set, which is what happens by omission when a field is ADDED and
        # nobody comes back here. Order-sensitivity returns for that one field and nothing says so.
        Name = 'diff: every list in the document has been decided about'
        File = 'src\Bws.Core\Snapshots\SnapshotDiff.cs'
        # RE-ANCHORED 2026-09-06: requiredBy joined the set the day it joined the document, and
        # the guard below only saw it once it was asked of the TYPE rather than of a rendered
        # specimen - a list field that is null in the fixture is not an array.
        From = 'internal static readonly string[] Unordered = ["dependsOn", "requiredBy", "requiredPrivileges", "triggers"];'
        To = 'internal static readonly string[] Unordered = ["dependsOn", "requiredBy", "requiredPrivileges"];'
        Test = 'DocumentValueGuards.Every_list_in_the_document_is_either_unordered_or_named_as_ordered'
    },
    @{
        Name = 'snapshot: the schema version is pinned rather than read back'
        File = 'src\Bws.Core\Snapshots\Snapshot.cs'
        # RE-ANCHORED 2026-09-30: schema five, stability report D-5, package E.
        # RE-ANCHORED 2026-10-06: schema six, package SB - the source of fileVersion.
        From = 'public const int CurrentSchemaVersion = 6;'
        To = 'public const int CurrentSchemaVersion = 7;'
        Test = 'SnapshotContractTests.The_file_says_what_it_needs_to_be_compared_later'
    },
    @{
        # Marks it handled and says nothing, which is the window still on screen pretending
        # everything worked - the exact silence this handler exists to end.
        Name = 'window: a failure nobody caught is said out loud'
        File = 'src\Bws.Gui\Mishaps.cs'
        # RE-ANCHORED 2026-09-03: the sentence now carries every cause rather than the top one -
        # backlog 307.
        From = 'model.Says.CouldNotDo(string.Join(" ", Bws.Core.Causes.Of(failure)));'
        To = ''
        Test = 'MishapGuards.A_failure_is_said_where_the_window_says_everything_else_it_could_not_do'
    },
    @{
        # The markup half of a number that lives in two files, moved on its own. Nothing in the
        # build has an opinion about the two agreeing, which is the whole reason for the guard.
        Name = 'window: the wait after a keystroke is one number in two files'
        File = 'src\Bws.Gui\SearchRow.xaml'
        From = 'UpdateSourceTrigger=PropertyChanged, Delay=400}'
        To = 'UpdateSourceTrigger=PropertyChanged, Delay=450}'
        Test = 'TypingDelayGuards.The_wait_after_a_keystroke_is_the_same_number_in_the_code_and_in_the_markup'
    },
    @{
        # BACKLOG 255's FAMILY, MEASURED 2026-09-01. The window writes the layout when it closes,
        # and the sort is applied only after the first reading arrives - so a window closed before
        # that, which is every probe and any impatient person, harvested a grid carrying no order
        # and wrote none. The guard opens a real window on a layout, closes it, and compares the
        # FILE.
        Name = 'window: an order the grid has not been given yet is not erased from the file'
        File = 'src\Bws.Gui\KeptColumns.cs'
        From = @'
        return harvested.Sort is null
            ? harvested with { Sort = _layouts.For(_scope).Sort }
            : harvested;
'@
        To = @'
        return harvested;
'@
        Test = 'KeptOrderGuards.A_window_opened_on_a_layout_and_closed_leaves_that_layout_alone'
    },
    # THE EXCEPTION THE ENTRY ABOVE IS BUILT AROUND, AND IT HAS NO ENTRY OF ITS OWN - RETIRED
    # 2026-09-03, BROUGHT BACK THE SAME DAY, AND RETIRED AGAIN WITH A MEASUREMENT BEHIND IT.
    # The sentence is kept because it is worth more than the entry ever was.
    #
    # WHAT IT MUTATED: the one line in KeptColumns.Defaults that decides what order the way back
    # leaves in the file. It read `Sort = null` until 2026-09-03 and reads `Sort = Plan.Layout.Sort`
    # now. Either way the test reads the file afterwards.
    #
    # WHY IT CAME BACK MISSED, TWICE, AND THE ANSWER IS THE SAME BOTH TIMES. Defaults hands the
    # grid the default layout through Reapply, which puts the default ORDER on it - and the write
    # that follows harvests the grid, so what this line left behind is read only when the grid has
    # no order to give. On a settled window it always has one, so both runs write the same file.
    #
    # MEASURED 2026-09-03 rather than reasoned, with a throwaway probe of twenty ways back:
    # SEVENTEEN of twenty left the grid carrying no order on a window built and never shown, and
    # ZERO of twenty did once the window had settled. The rows binding resolves at DataBind
    # priority; a person clicking a menu item is thousands of frames past it. So the fallback is
    # real and NOT REACHABLE BY CLICKING - which is why no honest guard can pin this line, and
    # why a guard that arranged the branch by hand was written, run, and deleted the same hour:
    # the write happens inside Defaults, before anything outside it can take the order off.
    #
    # WHAT THE MEASUREMENT CHANGED ANYWAY: the value. Clearing the order wrote a section with no
    # sort, and a section with no sort opens the list UNSORTED rather than the way this build
    # opens - so in those seventeen the way back wrote a file contradicting the owner's decision
    # of 2026-09-02. It writes the default order now, which is what the grid supplies on the
    # reachable path anyway, so the two branches agree instead of one being wrong.
    #
    # WHETHER THE LINE STAYS AT ALL IS THE OWNER'S - backlog 315. It is dead on every path a
    # person can reach, and deleting a safety net is not something a mutation registry decides.
    #
    #    Name = 'window: the way back leaves the usual order on the file rather than none'
    #    File = 'src\Bws.Gui\KeptColumns.cs'
    #    From = '                _layouts = _layouts.With(_scope, _layouts.For(_scope) with { Sort = Plan.Layout.Sort });'
    #    To = '                _layouts = _layouts.With(_scope, _layouts.For(_scope) with { Sort = null });'
    #    Test = 'KeptOrderGuards.The_way_back_takes_the_kept_order_off_the_file_as_well'
    #
    @{
        # BACKLOG 293, AND THE FAULT IT RESTORES WAS ON A PHOTOGRAPH FOR A DAY BEFORE ANYBODY
        # READ IT. Each filter group is a label beside a WrapPanel of chips. Take the wrap away
        # and the chips lay out in one endless line, which is exactly what the horizontal
        # StackPanel around them used to cause by measuring with infinite width - at 1000 by 800
        # the chips stood outside the window, unreadable and unclickable.
        #
        # THE GUARD ASKS AT THE NARROW SIZE AND ONLY THERE, which is the whole reason this went
        # unseen: at the size this window opens with, every group fits and the broken markup and
        # the repaired markup are the same picture.
        #
        # AND AT A ROW THE TEST NARROWS ITSELF, since 2026-10-16. At the narrowest window the fault
        # put back stood 8.7 units outside the row on the owner's machine (754 against 745.3, at 150
        # per cent) and not at all on GitHub's runners, where this entry came out MISSED twice. In
        # the row narrowed to its label column and two of its widest chip it stands 237 units out.
        Name = 'window: a filter chip never stands outside a narrow window'
        File = 'src\Bws.Gui\Themes\Chips.xaml'
        From = '                        <WrapPanel />'
        To = '                        <StackPanel Orientation="Horizontal" />'
        Test = 'FilterRowGuards.No_filter_chip_stands_outside_a_narrow_window'
    },
    @{
        # BACKLOG 315, AND THIS ENTRY IS THE REASON A LINE COULD BE DELETED RATHER THAN KEPT AS
        # AN UNGUARDED NET. By used to leave the moment it found no collection view, before
        # marking anything - so a caller that had asked for an order got one only if the grid's
        # rows binding had already resolved, which is a race. Measured over twenty ways back: a
        # window nobody had shown carried no order seventeen times, a settled one none out of
        # twenty, and after the repair twenty out of twenty.
        #
        # THE MUTATION PUTS THE EARLY RETURN BACK. The guard asks a BARE DataGrid, which is the
        # same state as a binding that has not resolved and the only one a test can arrange
        # without racing a dispatcher - which is why this entry is deterministic where two
        # earlier attempts on the same behaviour, asked through a window, came back MISSED.
        Name = 'window: an order asked for before the rows arrive still marks the heading'
        File = 'src\Bws.Gui\ListSorting.cs'
        From = '        var view = CollectionViewSource.GetDefaultView(grid.ItemsSource) as ListCollectionView;'
        To = '        var view = CollectionViewSource.GetDefaultView(grid.ItemsSource) as ListCollectionView; if (view is null) { return; }'
        Test = 'ColumnOrderGuards.An_order_asked_for_before_the_rows_arrive_is_still_marked_on_the_heading'
    },
    @{
        # THE HAZARD docs/10 TRAP 18 LEFT OPEN, AND THE MUTATION IS THE HAZARD ITSELF RATHER
        # THAN A STAND-IN FOR IT. The seven action buttons sit in a horizontal StackPanel and
        # cannot wrap, and every one of their words comes out of a language file - so the way
        # this bar overflows in real life is a longer word in another language, not a code
        # change. This lengthens one label and asks whether anything notices.
        #
        # MEASURED BEFORE THE GUARD WAS WRITTEN, which is why it is a guard and not a note: at
        # the narrowest window this build allows the buttons end at 571 against a bar ending at
        # 611. Forty units of six hundred is thin, and nothing was watching it.
        Name = 'window: no action button stands outside its bar at the narrowest window'
        File = 'src\Bws.Gui\Resources\gui.en.json'
        From = '  "gui.action.refresh": "Refresh",'
        To = '  "gui.action.refresh": "Refresh the whole machine right now, every entry",'
        Test = 'ActionBarGuards.No_action_button_stands_outside_the_bar_at_the_narrowest_window'
    },
    @{
        # BACKLOG 233. A bare word the verb has no room for used to join the unknown options, so
        # `bws start type Spooler manual` answered "Unknown option: Spooler, manual" about two
        # words spelled perfectly. The lists are separate now and the sentences differ.
        Name = 'command line: a word with nowhere to go is not an unknown option'
        File = 'src\Bws.Cli\CommandLine.Reading.cs'
        From = '                extra.Add(argument);'
        To = '                rejected.Add(argument);'
        Test = 'ArgumentRefusalTests.A_word_a_verb_has_no_room_for_is_not_an_unknown_option'
    },
    @{
        # The other half: the sentence has to be about THIS command. One shape repeated for every
        # verb would tell somebody that `list` takes one name, which it does not take at all.
        Name = 'command line: what a verb has room for is said per verb'
        File = 'src\Bws.Cli\OptionSurface.cs'
        From = '        CommandKind.List => "cli.takes.query",'
        To = '        CommandKind.List => "cli.takes.oneName",'
        Test = 'UsageContractTests.A_word_a_command_has_no_room_for_is_not_called_an_option'
    },
    @{
        # BACKLOG 207. Nine sentences on the command line counted entries with no singular beside
        # them, so a machine holding one read "Read 1 entries in 12 ms". The pairs exist now, and
        # this entry proves the guard notices when one half goes away.
        Name = 'command line: a counted sentence keeps both halves of its pair'
        File = 'src\Bws.Cli\Resources\cli.en.json'
        From = '  "cli.info.timingRead.one": "Read {0} entry in {1} ms.",'
        To = '  "cli.info.timingRead.singular": "Read {0} entry in {1} ms.",'
        Test = 'CountedWordGuards.Every_sentence_that_counts_something_is_one_half_of_a_pair'
    },
    @{
        # The half no pattern can find: a plural with no number in it. The window was given this
        # pair on 2026-08-19 and the command line was not, so the two interfaces said different
        # things about one fact for a fortnight.
        Name = 'command line: a plan sentence about one entry does not say those'
        File = 'src\Bws.Cli\PlanText.Warnings.cs'
        From = '            Count("cli.plan.warning.sharedProcess", warning), warning.ServiceName, Join(warning.Related)),'
        To = '            "cli.plan.warning.sharedProcess.many", warning.ServiceName, Join(warning.Related)),'
        Test = 'PlanSentenceGuards.A_shared_process_warning_about_one_entry_does_not_say_those'
    },
    @{
        # BACKLOG 124. The window has said "Start pending" since docs/11 3.7 asked for sentence
        # case and the listing printed "StartPending", the name of an enumeration value. Owner's
        # decision 2026-09-01: align them. The guard holds the JSON unchanged in the same breath,
        # because that half is a frozen contract every oracle tool reads.
        Name = 'command line: a state of two words is printed as two words'
        File = 'src\Bws.Cli\ListingTable.cs'
        From = '                StatusWords.Of(entry.Status),'
        To = '                entry.Status.ToString(),'
        Test = 'ListingTableTests.A_state_of_two_words_is_printed_as_two_words_and_the_json_is_left_alone'
    },
    @{
        # BACKLOG 192, AND WHAT IT GUARDS CHANGED WHEN THE OWNER REVERTED THE FEATURE ON 2026-09-01.
        # The growing is gone - it looked bad on the screen, which is the one verdict nothing here
        # can compute. The face is still what carries "this column is prose", and a face the builder
        # does not honour is still a catalogue entry that reaches nothing, so the entry keeps its
        # job and points at the guard that survived the revert.
        #
        # The entry that mutated the four line cap went with the values it named. It is not
        # commented out or left stale: a mutation of a number that no longer exists proves nothing
        # while looking like it did, which is what STALE means in this runner's own words.
        Name = 'window: the description column is built from its own style'
        File = 'src\Bws.Gui\ListColumns.cs'
        From = '            ColumnFace.Prose => Written(grid, column, "CellProse", headerStyle: null),'
        To = '            ColumnFace.Prose => Written(grid, column, "CellText", headerStyle: null),'
        Test = 'ProseCellGuards.The_description_column_is_still_the_one_built_from_prose'
    },
    @{
        # AND THE DECISION ITSELF, added 2026-09-01 when the feature came out. The complaint that
        # produced it is still open - a description runs to 1251 characters in a cell that gives it
        # one line - so the next session has every reason to build this again. The mutation puts the
        # trigger back, which is exactly the shape that was rejected.
        Name = 'window: the description cell does not grow for a chosen row'
        File = 'src\Bws.Gui\Themes\Cells.xaml'
        From = '    <Style x:Key="CellProse" TargetType="TextBlock" BasedOn="{StaticResource CellText}" />'
        To = '    <Style x:Key="CellProse" TargetType="TextBlock" BasedOn="{StaticResource CellText}"><Style.Triggers><DataTrigger Value="True" Binding="{Binding IsSelected, RelativeSource={RelativeSource AncestorType={x:Type DataGridCell}}}"><Setter Property="TextWrapping" Value="Wrap" /></DataTrigger></Style.Triggers></Style>'
        Test = 'ProseCellGuards.The_description_cell_does_not_grow_for_a_chosen_row'
    },
    @{
        # FOUND BY AN EYE ON 2026-09-01, not by a guard. The first screen anybody sees read
        # "113services running" and "0orphans" since the day it shipped, because the label carried a
        # margin that is 8 on its RIGHT - away from what comes after it, nothing towards the number
        # in front. A missing space is not a missing element, so nothing in this project noticed.
        #
        # MOVED THE SAME DAY, WHEN THE SCREEN BECAME CARDS. The gap now belongs to the NUMBER of a
        # qualification rather than to a label, because a headline stacks its words under its number
        # and only a qualification still stands the two side by side. Same fault, one template on.
        Name = 'window: the overview number does not touch the words it counts'
        File = 'src\Bws.Gui\OverviewView.xaml'
        From = '                               Margin="{StaticResource MarginAfterNumber}"'
        To = '                               Margin="{StaticResource MarginNone}"'
        Test = 'OverviewCardGuards.The_number_does_not_touch_the_words_it_counts'
    },
    @{
        # Backlog 263. The sentence explaining a folded count is a fact about ROWS, and the machine
        # overview REPLACES the list rather than sitting over it - so on that screen it explains a
        # difference nobody can see and names a switch that is not there.
        #
        # INVERTED RATHER THAN REMOVED. The reason written here first no longer holds: dropping the
        # condition leaves the parameter unused, and until 2026-09-23 that made the mutated build
        # fail (BROKEN). Since that day Test-Mutation builds the mutated tree with
        # TreatWarningsAsErrors=false, so an unused parameter compiles and the verdict is real.
        # Inverting stays anyway: it gets both screens wrong at once (the sentence appears on the
        # overview and leaves the list), where removing the condition gets only the overview wrong.
        Name = 'window: the folded count is explained only where there are rows to count'
        File = 'src\Bws.Gui\ViewModels\Sentences.cs'
        # RE-ANCHORED 2026-09-15, the same move.
        From = '        folded > 0 && listOnScreen'
        To = '        folded > 0 && !listOnScreen'
        Test = 'AdmissionTests.The_folded_count_is_not_explained_while_the_overview_has_the_window'
    },
    @{
        # The other half of backlog 263, and the half that is easy to leave out. Suppressing the
        # sentence is worth nothing if the notice holding it is not recomposed when the screen
        # changes - the pass runs on a one second tick, so raising alone leaves the sentence
        # standing over the overview for up to a second and missing under the list for the same
        # second on the way back.
        #
        # The condition guards a startup case rather than a cost: this property is set from the
        # kept file in the window's constructor, before the first reading, and an unguarded pass
        # over an empty index would write "0 entries" over the "reading" sentence for the 749-822 ms
        # that read takes. Inverting it is therefore the honest mutation - it keeps the guard and
        # aims it at the wrong moment.
        Name = 'window: changing which screen has the middle of the window recomposes what is said under it'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Overview.cs'
        From = '            if (!_readings.FirstLook)'
        To = '            if (_readings.FirstLook)'
        Test = 'AdmissionTests.The_folded_count_is_not_explained_while_the_overview_has_the_window'
    },
    @{
        # Backlog 260, first of three surfaces. The listing table printed NotSigned and
        # UntrustedRoot - the names of enumeration values - where the window has always said the
        # words. The mutation is the exact code that stood here before 2026-09-01.
        Name = 'listing: the signature column says a word for a person rather than the name of a value'
        File = 'src\Bws.Cli\ListingTable.cs'
        From = '        var status = SignatureWords.Of(signature.Status);'
        To = '        var status = signature.Status.ToString();'
        Test = 'ListingTableTests.A_signature_verdict_of_two_words_is_printed_as_two_words_and_the_json_is_left_alone'
    },
    @{
        # Backlog 260, second surface. The same verdict said by `bws show` rather than by the
        # table, and a repair that reached one and not the other would be the disagreement moved
        # rather than ended.
        Name = 'report: the signature line says a word for a person rather than the name of a value'
        File = 'src\Bws.Cli\EntryReport.cs'
        From = '            new Field("cli.show.signature", entry => Say(entry.Signature, value => SignatureWords.Of(value.Status))),'
        To = '            new Field("cli.show.signature", entry => Say(entry.Signature, value => value.Status.ToString())),'
        Test = 'EntryReportTests.A_signature_that_was_read_and_names_nobody_leaves_the_publisher_empty_rather_than_blank'
    },
    @{
        # Backlog 260, third and last surface. Seven of the nine trigger kinds read as words jammed
        # together, and this is the only place they are printed. The action beside them is
        # deliberately NOT part of this - its three members are already single words, measured
        # 2026-09-01, and TriggerWords carries that note.
        Name = 'report: the trigger kind says a word for a person rather than the name of a value'
        File = 'src\Bws.Cli\EntryReport.cs'
        From = '            TriggerWords.Of(trigger.Kind),'
        To = '            trigger.Kind.ToString(),'
        Test = 'EntryReportTests.A_trigger_kind_of_two_words_is_printed_as_two_words_and_its_action_is_left_alone'
    },
    @{
        # Backlog 263, second half. Four rows that act on a list are taken off the screen while the
        # list is not on it. Inverted rather than removed, so the mutation is a window that hides
        # its own controls exactly when they are the ones that work.
        Name = 'window: the lists own controls leave the screen that has no list'
        File = 'src\Bws.Gui\MainWindow.Overview.cs'
        # RE-ANCHORED 2026-10-14: asked as "is the list on screen", because a comparison hides it too.
        From = '        var chrome = _model.ListOnScreen ? Visibility.Visible : Visibility.Collapsed;'
        To = '        var chrome = _model.ListOnScreen ? Visibility.Collapsed : Visibility.Visible;'
        Test = 'ScreenWithNoListGuards.The_lists_own_controls_are_not_on_the_screen_that_has_no_list'
    },
    @{
        # Backlog 263. The screen with no list says what the READING is doing and nothing about a
        # query - the subject decides, not the screen. The mutation widens it to every face that
        # has something to say, which is how "nothing matched" would reach a screen where there is
        # no query on show to match anything.
        Name = 'window: the screen with no list talks about the reading and not about a query'
        File = 'src\Bws.Gui\ViewModels\Says.cs'
        From = '    public string ReadingMessage => _list.Face is ListFace.Loading or ListFace.Failed'
        To = '    public string ReadingMessage => _list.Face is not ListFace.Rows'
        Test = 'ScreenWithNoListGuards.A_query_that_matched_nothing_is_not_the_screens_business'
    },
    @{
        # Backlog 263. Zero is a claim and this screen made it before it had read anything, for the
        # 749-822 ms the first reading takes. Inverted rather than dropped, because dropping the
        # condition leaves the parameter unused and warnings are errors here - the entry would come
        # back BROKEN and prove nothing.
        Name = 'window: nothing read yet is not drawn as nothing there'
        File = 'src\Bws.Gui\ViewModels\Overview.cs'
        From = '            counted ? count.ToString(CultureInfo.CurrentCulture) : Texts.Of("gui.overview.notCounted"),'
        To = '            !counted ? count.ToString(CultureInfo.CurrentCulture) : Texts.Of("gui.overview.notCounted"),'
        Test = 'ScreenWithNoListGuards.Nothing_read_yet_is_not_the_same_as_nothing_there'
    },
    @{
        # Backlog 309. Two entries on this machine hand back an indirection nobody resolved
        # instead of a label, and the whole suite - 1180 tests - stayed green while the document
        # changed from one to the other. That is the only reason both of these entries exist: the
        # behaviour had no guard at all until the day it was written.
        #
        # THE DOCUMENT SIDE IS THE ONE WORTH GUARDING FIRST, because it is shared. ListingJson
        # builds this same type, so a document that kept the raw value would put a different text
        # in `bws list --json` from the one `bws list` printed - two halves of one command
        # disagreeing, which is the shape of fault this project already paid for once.
        Name = 'label: a document carries what a person is shown, not the indirection'
        File = 'src\Bws.Core\Snapshots\EntryDocument.cs'
        From = 'DisplayName = ServiceDisplayName.Of(entry.DisplayName, entry.ServiceName),'
        To = 'DisplayName = entry.DisplayName,'
        Test = 'SnapshotTests.An_unresolved_indirection_is_written_as_the_label_a_person_is_shown'
    },
    @{
        # The window is where this was actually seen and where it was worst: an at sign sorts
        # before every letter, so once the list gained a default order by display name these two
        # were its first two rows. Anchored on the constructor rather than on the refresh, because
        # a row is built before it is ever refreshed and the test loads a fresh window.
        Name = 'label: a row shows a label rather than an indirection'
        File = 'src\Bws.Gui\ViewModels\EntryRow.cs'
        From = '_displayName = ServiceDisplayName.Of(entry.DisplayName, entry.ServiceName);'
        To = '_displayName = entry.DisplayName;'
        Test = 'MainViewModelTests.A_row_shows_a_label_rather_than_an_indirection_nobody_resolved'
    },
    @{
        # THE ENTRY THAT EARNED THE OTHER TWO, and it is here because the other two were not
        # enough. Column.Reads takes an ScmEntry rather than a row, so the CELL and the ROW are
        # separate paths to one fact - the row guard above went green while the cell on screen
        # still read @todo.dll,-100, and only a photograph said so. This column declares no Sorts,
        # so the same lambda decides the ORDER, which is what put these two at the top of the list.
        Name = 'label: the display name column shows a label rather than an indirection'
        File = 'src\Bws.Gui\ViewModels\Columns.cs'
        From = 'Reads = entry => ServiceDisplayName.Of(entry.DisplayName, entry.ServiceName)'
        To = 'Reads = entry => entry.DisplayName'
        Test = 'ColumnGuards.The_display_name_column_shows_a_label_rather_than_an_indirection'
    },
    @{
        # Backlog 297. The walk called PWSTR.ToString(), which reads from an address until it meets
        # a null wherever that happens to be - no block, no ceiling. NameInside bounds it.
        #
        # ANCHORED ON THE NULL CASE RATHER THAN ON THE OUT-OF-BLOCK ONE, and that is a choice about
        # what a mutation entry may assert. Reverting this makes a null pointer come back as a null
        # STRING, which the test catches every time, on every machine. The out-of-block case reads
        # whatever happens to sit a page below a pinned array - it would usually fail and sometimes
        # not, and an entry that reports MISSED on a quiet machine is an entry nobody trusts again.
        #
        # WHAT THIS ENTRY DOES NOT PROVE, said here because the package it belongs to is mostly
        # about it: the PINNING. A test for that would have to lose a race with the collector on
        # purpose, so there is no entry for it and none should be written.
        Name = 'blocks: a dependent name is read no further than the block holding it'
        File = 'src\Bws.Core\ManagerBlocks.cs'
        From = 'names.Add(NameInside(records[index].lpServiceName, start, buffer.Length));'
        To = 'names.Add(records[index].lpServiceName.ToString());'
        Test = 'ManagerBlockTests.A_dependent_name_that_is_null_is_not_followed'
    },
    @{
        # Backlog 303, and this entry exists because the rest of that package cannot be tested at
        # all. Whether a sizing call is asked through its return value or through the last error
        # makes NO observable difference on a healthy machine - measured over 799 entries under
        # both tokens on 2026-09-02, the two shapes answer identically for every one of them.
        # So there is nothing behavioural to assert, and the guard is on the SOURCE instead.
        #
        # The mutation restores the old shape and still compiles, which is the whole trick: it
        # drops the answer on the floor and pretends the call worked. NativeCallGuards sees a
        # native call standing as a statement under a name nobody argued for.
        Name = 'interop: a native call does not get to drop its answer unargued'
        File = 'src\Bws.Core\ScmDetailReader.cs'
        From = 'var probed = PInvoke.QueryServiceConfig2W(service, level, default, out needed);'
        To = 'PInvoke.QueryServiceConfig2W(service, level, default, out needed); var probed = true;'
        Test = 'NativeCallGuards.Every_native_call_that_drops_its_answer_has_argued_for_dropping_it'
    },
    @{
        # ---- The first entry in this registry anchored in tests\ rather than in src\, 2026-09-05 ----
        #
        # WHY IT HAD TO BE. PublicSurfaceGuards has three sweeps and every one of them asserts
        # that a set is EMPTY - no private shape, no unexplained alphabet, no stale permission.
        # Widen the list of surfaces it skips until it skips everything and all three stay green,
        # because a sweep over nothing finds nothing. So the ordinary mutation - break the guard,
        # watch a test redden - cannot exist for this class: breaking it reddens nothing.
        #
        # The owner widened that list on 2026-09-05, from four surfaces to six, and docs/06
        # carries the decision. The list is now guarded by a test that asks BOTH directions, and
        # this entry is what proves that test can fail.
        #
        # WHAT IS MUTATED. One name out of the list of things kept out of the sweep. It still
        # compiles, the three sweeps still pass - the artifacts directory holds no .cs and no .md
        # that would trip them - and only the list guard notices. That is exactly the shape the
        # entry is for.
        Name = 'surfaces: the list of things outside git is guarded, not just written down'
        File = 'tests\Bws.Architecture.Tests\PublicSurfaceGuards.cs'
        From = 'string[] outside = ["docs", "tools", "artifacts", ".claude"];'
        To = 'string[] outside = ["docs", "tools", ".claude"];'
        Test = 'PublicSurfaceGuards.The_surfaces_left_out_of_the_sweep_are_the_ones_that_are_outside_git'
    }

    @{
        # THE FAULT THIS PROVES HAD ALREADY SHIPPED, which is why it is worth an entry rather
        # than a note. Both surfaces ended their refusal switch in a discard, so the two kinds a
        # forcing plan can produce came out wearing another kind's words. Measured on the shipped
        # build on 2026-09-07: `bws kill ALG --dry-run` said "ALG is a driver" about a service
        # `sc qc` calls WIN32_OWN_PROCESS, which was simply stopped.
        #
        # WHAT IS MUTATED, AND WHY IT IS THE DISCARD RATHER THAN AN ARM. Putting the wildcard back
        # is the fault itself, exactly as it stood. Deleting one arm instead would prove the guard
        # notices a MISSING sentence, which is what the older warning guard already asks - and it
        # is not the shape that shipped.
        Name = 'refusals: two kinds of refusal cannot share one sentence in the terminal'
        File = 'src\Bws.Cli\PlanText.cs'
        From = @'
        PlanProblemKind.NoProcessToEnd => Texts.Of("cli.plan.problem.noProcessToEnd", problem.ServiceName),
'@
        To = @'
        PlanProblemKind.NoProcessToEnd => Texts.Of("cli.plan.problem.notOperable", problem.ServiceName),
'@
        # THE NAMESPACE IS PART OF THE NAME HERE, AND ON THE ENTRY BELOW, BECAUSE THE CLASS NAME
        # IS SHARED ON PURPOSE - both surfaces have a PlanSentenceGuards asking the same two
        # questions, so the pair is findable from either side. An unqualified name would leave the
        # runner unable to say WHICH of the two proved the mutation.
        Test = 'Bws.Cli.Tests.PlanSentenceGuards.Every_kind_of_refusal_says_its_own_thing'
    }

    @{
        # The same fault on the other surface, one step behind it - this switch fell through to
        # the sentence about an entry that is disabled and cannot come back. It had never reached
        # a screen only because the window could not build a forcing plan yet, which is what this
        # packet changed.
        Name = 'refusals: two kinds of refusal cannot share one sentence in the window'
        File = 'src\Bws.Gui\ViewModels\PlanWords.cs'
        From = @'
        PlanProblemKind.CascadeUnreadable =>
            Texts.Of("gui.plan.problem.cascadeUnreadable", problem.ServiceName),
'@
        To = @'
        PlanProblemKind.CascadeUnreadable =>
            Texts.Of("gui.plan.problem.noProcessToEnd", problem.ServiceName),
'@
        Test = 'Bws.Gui.Tests.PlanSentenceGuards.Every_kind_of_refusal_says_its_own_thing'
    }

    @{
        # THE CONDITION THE ANALYSIS SAID WAS EASIEST TO TRIP OVER, and it is: a step that gave up
        # with no process has nothing to end, so an offer under it opens a sheet the core refuses
        # to build. A button that leads to an apology.
        #
        # The reading is made to answer true rather than the check removed, because removing it
        # leaves the value unused and the mutation comes back BROKEN.
        Name = 'forcing: no way out is offered under a failure with no process to end'
        File = 'src\Bws.Gui\ViewModels\Planned.Forcing.cs'
        From = '|| !result.ProcessId.IsPresent'
        To = '|| (!result.ProcessId.IsPresent && false)'
        Test = 'ForcedStopGuards.A_stop_that_gave_up_with_no_process_offers_nothing'
    }

    @{
        # THE CORRECTION SECTION 15.3 OF THE ANALYSIS BOUGHT BEFORE A LINE WAS WRITTEN. A stop that
        # failed inside a restart has to offer a forced RESTART - an offer ending at killing the
        # process would leave the machine without the service somebody had just asked to bring back.
        Name = 'forcing: a stop that failed inside a restart offers a restart, not a stop'
        File = 'src\Bws.Gui\ViewModels\Planned.Forcing.cs'
        From = 'ActionKind.Restart => ActionKind.ForceRestart,'
        To = 'ActionKind.Restart => ActionKind.ForceStop,'
        Test = 'ForcedStopGuards.A_stop_that_failed_inside_a_restart_offers_a_forced_restart'
    }

    @{
        # THE HEAVIEST CONFIRMATION THIS PROJECT HAS, AND THE ONE THING BETWEEN A PRESS AND A DEAD
        # PROCESS when what dies reaches past the entry somebody named. Mutated so that anything
        # typed counts, which is the shape a confirmation quietly stops being one.
        Name = 'forcing: the name really has to be typed before a process can be ended'
        File = 'src\Bws.Gui\ViewModels\Planned.Forcing.cs'
        From = '&& string.Equals(_typed.Trim(), TypeTheName, StringComparison.OrdinalIgnoreCase));'
        To = '&& _typed.Length >= 0);'
        Test = 'ForcedStopGuards.A_different_name_does_not_turn_the_button_on'
    }

    @{
        # ENTER HAS NO WAY TO END A PROCESS, and this is what holds it. The sheet hands the keyboard
        # to the box when there is one and to the way OUT when there is not - never to the button.
        # Mutated to the button, which is the one arrangement that turns a keystroke into a kill.
        Name = 'forcing: the keyboard is never handed to the button that ends a process'
        File = 'src\Bws.Gui\PlanView.xaml.cs'
        From = 'ConfirmShown ? Footer.Confirm : PlanCloseButton;'
        To = 'ConfirmShown ? Footer.CarryOut : PlanCloseButton;'
        Test = 'ForcedStopViewGuards.The_keyboard_is_never_handed_to_the_button_that_ends_a_process'
    }

    @{
        # THE ICON, 2026-09-07. One .ico above both shipped projects, and each of them has to point
        # at THAT one - a private copy in either would drift from the other and nothing would say
        # so. Mutated by taking the declaration off the command line program, which still builds
        # perfectly well and simply ships without an icon.
        #
        # WHAT THIS REGISTRY CANNOT COVER, and it is said here rather than left as a gap somebody
        # later reads as coverage: every icon guard except this one stands on a BINARY, and every
        # mutation here is a string replacement in a text file. Three of them, each proved by hand
        # instead, and the proof is written down so it is not re-derived:
        #
        #   IconGuards.The_product_icon_carries_every_size_windows_asks_for - 2026-09-07, the icon
        #   was repacked with five sizes, the test went red, the ten-size file was put back.
        #
        #   IconGuards.How_large_the_drawing_reads_in_every_frame_windows_draws_small - 2026-09-22,
        #   `git checkout -- src/bws.ico` restored the smaller bean that had shipped until that
        #   day, the test went red naming the frame and the percentage, and the new file was put
        #   back and it went green. That is a better mutation than any string swap would have been,
        #   because the thing restored is the real defect rather than an invented one.
        #
        #   GeneratorTests.The_sites_favicon_is_the_programs_icon - the same restore covered it.
        #   The two files differ the moment either is regenerated without the other.
        Name = 'icon: both shipped programs point at the one icon file'
        File = 'src\Bws.Cli\Bws.Cli.csproj'
        From = '<ApplicationIcon>..\bws.ico</ApplicationIcon>'
        To = '<!-- no icon -->'
        Test = 'IconGuards.Both_shipped_programs_point_at_that_one_icon'
    }
    @{
        # A video's still frame is an address the site check reads, added 2026-09-24 with the
        # front page's video. Without it a poster pointing at nothing ships as a black box.
        Name = 'site: a poster pointing at nothing is reported'
        File = 'site\Bws.Site\LinkCheck.cs'
        From = '        @"(?<=\s)(?<what>href|src|poster)\s*=\s*'
        To = '        @"(?<=\s)(?<what>href|src)\s*=\s*'
        Test = 'GeneratorTests.A_poster_pointing_at_nothing_is_reported'
    }
    @{
        # An attribute starts after whitespace - \b also matches inside data-src and data-href.
        Name = 'site: a data attribute is not read as an address'
        File = 'site\Bws.Site\LinkCheck.cs'
        From = '        @"(?<=\s)(?<what>href|src|poster)'
        To = '        @"\b(?<what>href|src|poster)'
        Test = 'GeneratorTests.A_data_attribute_is_not_read_as_an_address'
    }
    @{
        # HTML takes an attribute name in any case - a check reading one spelling passes the rest.
        Name = 'site: an address in capitals is still checked'
        File = 'site\Bws.Site\LinkCheck.cs'
        From = '        RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,'
        To = '        RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant,'
        Test = 'GeneratorTests.An_address_in_capitals_and_single_quotes_is_still_checked'
    }
    @{
        # A still frame is loaded like an image, so another host is refused for it too.
        Name = 'site: a poster from another host is refused'
        File = 'site\Bws.Site\LinkCheck.cs'
        From = '            if (!string.Equals(what, "href", StringComparison.OrdinalIgnoreCase))'
        To = '            if (string.Equals(what, "src", StringComparison.OrdinalIgnoreCase))'
        Test = 'GeneratorTests.A_poster_loaded_from_another_host_is_refused'
    }
    @{
        # A bad absolute address is reported, never thrown - a throw stopped the build on the first.
        Name = 'site: a malformed address is reported rather than thrown'
        File = 'site\Bws.Site\LinkCheck.cs'
        From = '            if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))'
        To = '            if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) && false)'
        Test = 'GeneratorTests.A_malformed_address_is_reported_rather_than_thrown'
    }

    @{
        # RUNG FIVE ITSELF. Without this check the plan is built, shown, and carried out until its
        # last step meets error 5 - which is what the tool did until 2026-09-08 and is a true
        # report of a worse experience, because by then the neighbours have already been stopped.
        #
        # The value is discarded rather than the check deleted, because deleting it leaves the
        # pattern variable unused and the mutation comes back BROKEN instead of caught.
        Name = 'ending: a process Windows will not open for ending is refused before any plan exists'
        File = 'src\Bws.Core\Planning\ForcedStop.cs'
        From = @'
        if (Unreachable(facts, target, processId) is { } unreachable)
        {
            return (unreachable, null);
        }
'@
        To = @'
        if (Unreachable(facts, target, processId) is { } unreachable)
        {
            _ = unreachable;
        }
'@
        Test = 'EndingRightsTests.A_process_windows_will_not_open_for_ending_is_refused_before_any_plan_exists'
    }

    @{
        # THE TWO REFUSALS COLLAPSED INTO ONE, which is the shape this packet spent a whole
        # measurement learning to keep apart. A process that has GONE is not a process that will
        # not be ended: one is a machine that moved on between the listing and the question, which
        # happens on any live machine, and the other is a permission. Saying the second about the
        # first is a confident sentence about the wrong subject.
        Name = 'ending: a process that went away is not a refusal about permission'
        File = 'src\Bws.Core\Planning\ForcedStop.cs'
        From = @'
            // It was there in the listing and it is not there now. The word for that already
            // exists and it is not a word about permissions.
            return Because(PlanProblemKind.NoProcessToEnd, target);
'@
        To = @'
            // It was there in the listing and it is not there now. The word for that already
            // exists and it is not a word about permissions.
            return Because(PlanProblemKind.ProcessCannotBeEnded, target);
'@
        Test = 'EndingRightsTests.A_process_that_went_away_is_nothing_to_end_rather_than_a_refusal_about_permission'
    }

    @{
        # The second half of the identity stops being frozen into the plan. Nothing looks any
        # different - the preview names the same process, the run ends the same process - and the
        # check that stops a recycled number from being ended silently has nothing to check
        # against. Backlog 323.
        # Re-anchored 2026-09-30: the ending is built in Aftermath.Weigh since package B2.
        Name = 'ending: the step that ends a process carries when that process started'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = '            facts.Created.IsPresent ? facts.Created.Value : null,'
        To = '            null,'
        Test = 'EndingRightsTests.The_step_that_ends_a_process_carries_when_that_process_started'
    }

    @{
        # The same half, one seam further down: the plan freezes it and the runner drops it on the
        # way. This is the mutation that matters most of the four, because the check itself lives
        # on the other side of a call no test in this project can reach - so the only thing a test
        # CAN prove is that the value gets there.
        # Re-anchored 2026-09-30: the call shares its line with the company check since package B2.
        Name = 'ending: the identity the plan froze reaches the call that ends the process'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = '            ? Crowded(step) ?? control.Terminate(holding.Value, step.ProcessCreatedAt)'
        To = '            ? Crowded(step) ?? control.Terminate(holding.Value, null)'
        Test = 'EndingRightsTests.The_ending_is_handed_the_identity_the_plan_froze'
    }

    @{
        # The third kind of refusal to be given somebody else's sentence, and the guard that
        # catches it was written on 2026-09-07 for the first two. A new kind added without a
        # sentence throws instead, which is a different failure - this is the quieter one, where
        # the sentence exists and describes a different machine.
        Name = 'ending: the refusal about a process Windows will not end says its own thing'
        File = 'src\Bws.Cli\PlanText.cs'
        From = '                "cli.plan.problem.processCannotBeEnded.plain",'
        To = '                "cli.plan.problem.noProcessToEnd",'
        Test = 'Bws.Cli.Tests.PlanSentenceGuards.Every_kind_of_refusal_says_its_own_thing'
    }

    # THE REAL READER, AGAINST REAL PROCESSES. The five entries above mutate the layers that
    # CONSUME the two facts, with a fake reader underneath. These three mutate the reader itself
    # and are held by the integration project, which asks this machine's processes - added
    # 2026-09-16 with the tests, backlog 347, when the reader was the one file in Bws.Core with
    # no line executed by any test.
    @{
        # RULE 8: A REFUSAL SWALLOWED. The handle did not open, the reader says it did, and rung
        # five of C3 - "say straight away that it cannot be done" - has nothing to say. The
        # specimen is the System process, which refuses ending with 5 on every install and from
        # every token this project has measured, including an elevated one with SeDebugPrivilege.
        Name = 'ending: a handle that would not open for ending is not reported as open'
        File = 'src\Bws.Core\WindowsEndingFactsReader.cs'
        From = '        if (!process.IsInvalid)'
        To = '        if (process.IsInvalid || !process.IsInvalid)'
        Test = 'EndingFactsContractTests.The_System_process_refuses_to_be_ended_and_still_says_when_it_started'
    }

    @{
        # The two failed opens collapsed into one, at the reader this time: "no such process"
        # arriving as a refusal about permission. The specimen is PID 0, which OpenProcess
        # documents as failing with ERROR_INVALID_PARAMETER - the one number no install can open.
        Name = 'ending: a number with no process behind it is absent, not denied'
        File = 'src\Bws.Core\WindowsEndingFactsReader.cs'
        From = '        code == (int)WIN32_ERROR.ERROR_INVALID_PARAMETER'
        To = '        code == (int)WIN32_ERROR.ERROR_ACCESS_DENIED'
        Test = 'EndingFactsContractTests.The_idle_process_number_reads_as_no_such_process_and_not_as_a_refusal'
    }

    @{
        # The halves of the file time put back the wrong way round. Always wrong, unlike dropping
        # the uint cast - which is wrong only when the top bit of the low half is set, so an entry
        # planting THAT fault would report MISSED half the time for reasons nobody caused. The
        # comparison is against Get-Process reading the same process, so the swapped number is
        # off by decades and the test says so.
        Name = 'ending: when a process started is the kernel file time, halves in order'
        File = 'src\Bws.Core\WindowsEndingFactsReader.cs'
        From = '        ((long)(uint)time.dwHighDateTime << 32) | (uint)time.dwLowDateTime;'
        To = '        ((long)(uint)time.dwLowDateTime << 32) | (uint)time.dwHighDateTime;'
        Test = 'EndingFactsContractTests.This_process_can_be_ended_and_started_when_PowerShell_says_it_did'
    }

    @{
        # THE FAULT THIS ONE PLANTS IS THE ONE THE CATALOGUE REALLY HAD, on the day it was built.
        # Requiring a dictionary to NAME Themes/ is true of everything App.xaml merges and false
        # of everything the test host merges - WpfHost reads the real files off disk and a
        # dictionary built from a stream has no Source. The sheet was complete in the product and
        # empty in the tests, which is the shape of fault that reaches a screenshot and stops
        # there.
        Name = 'catalogue: a theme file the sheet stops seeing is reported, not passed over'
        File = 'src\Bws.Gui\ViewModels\Catalogue.cs'
        From = 'if (source is not null && !source.Contains(Ours, StringComparison.OrdinalIgnoreCase))'
        To = 'if (source is null || !source.Contains(Ours, StringComparison.OrdinalIgnoreCase))'
        Test = 'Bws.Gui.Tests.CatalogueGuards.The_catalogue_leaves_out_no_component_the_window_declares'
    }

    @{
        # A COMPONENT THAT CANNOT BE DRAWN AND IS NOT EXPLAINED. Taking the cell out of the list of
        # things that only exist inside something else sends it to the factory table, which has no
        # DataGridCell in it - so the sheet would print "nothing here knows how to build a
        # DataGridCell to look at" and carry on. That row reads like a gap in the product rather
        # than a gap in this screen, which is why it is a red test.
        Name = 'catalogue: a component nothing can draw is a red build, not a quiet row'
        File = 'src\Bws.Gui\ViewModels\Catalogue.Samples.cs'
        From = 'nameof(DataGridCell) => "only exists inside a row, which only exists inside the list",'
        To = ''
        Test = 'Bws.Gui.Tests.CatalogueGuards.No_component_is_on_the_sheet_only_because_nothing_can_build_it'
    }

    @{
        # THE TWO LISTS OF THEME FILES DRIFTING APART, which is the hazard nothing watched until
        # 2026-09-10. A dictionary merged by the application and not by the test host does not
        # throw: it leaves the whole suite building a window whose styles are the control
        # library's defaults, silently. This plants the other direction, which is the one a person
        # actually causes - App.xaml losing a line somebody added to the host.
        Name = 'catalogue: the tests and the application merge the same theme files'
        File = 'src\Bws.Gui\App.xaml'
        From = '<ResourceDictionary Source="Themes/Catalogue.xaml" />'
        To = ''
        Test = 'Bws.Gui.Tests.CatalogueGuards.The_test_host_merges_the_same_theme_files_as_the_application'
    }
    @{
        # THE BLANK CELL PUT BACK - 2026-09-16, docs/PROJEKT-KATALOG-20260916.md, fault 2. The
        # pixel check says "paints" of every sample, so a shape that draws nothing goes back on the
        # sheet as an empty cell, and the guard that renders it AGAIN with its own code has to see
        # it. Mutates the verdict of a scan that found no pixel, not the scan.
        Name = 'catalogue: a sample that puts no pixel on the sheet is a sentence, not a blank'
        File = 'src\Bws.Gui\ViewModels\Catalogue.Samples.cs'
        From = '            return false;
        }
        catch (InvalidOperationException)'
        To = '            return true;
        }
        catch (InvalidOperationException)'
        Test = 'Bws.Gui.Tests.CatalogueGuards.A_component_that_draws_nothing_alone_says_so_rather_than_standing_blank'
    }
    @{
        # THE TALL FLAG NOT FOLLOWING THE MEASUREMENT. The plan sheet once went under the 96 pixel
        # ceiling, where its own margin left two faint edges of it visible - fault 3 of the same
        # document. RE-ANCHORED 2026-09-29: W6 moved that margin off the sheet and no style or
        # template sample is taller than the row today, so "never raised" would change nothing any
        # guard can see. Turned round instead, every short sample is marked tall.
        Name = 'catalogue: a sample taller than the row is marked tall'
        File = 'src\Bws.Gui\ViewModels\Catalogue.Samples.cs'
        From = '            return Sample.Of(element, tall: element.DesiredSize.Height > limits.RowCeiling);'
        To = '            return Sample.Of(element, tall: element.DesiredSize.Height <= limits.RowCeiling);'
        Test = 'Bws.Gui.Tests.CatalogueGuards.A_sample_taller_than_the_row_is_given_the_taller_ceiling'
    }
    @{
        # THE CHOSEN COLUMN GOES BLIND: no style is ever found to have a chosen state, and the chip
        # goes back to being drawn only unlit - docs/PROJEKT-KATALOG-20260916.md, section 3.1.
        Name = 'catalogue: a style whose template answers to IsChecked or IsSelected is shown chosen'
        File = 'src\Bws.Gui\ViewModels\Catalogue.States.cs'
        From = '        if (!Chooses(style))'
        To = '        if (Chooses(style) || !Chooses(style))'
        Test = 'Bws.Gui.Tests.CatalogueGuards.A_chip_is_shown_lit_and_a_suggestion_row_selected_in_the_chosen_column'
    }
    @{
        # THE TEMPLATES DROP OFF THE SHEET, which is exactly the sheet of 2026-09-10 to 2026-09-16:
        # complete about styles and silent about the eleven templates the window is also made of.
        Name = 'catalogue: the keyed templates of the theme files are on the sheet'
        File = 'src\Bws.Gui\ViewModels\Catalogue.cs'
        From = '        groups.AddRange(Templates(resources, limits));'
        To = ''
        Test = 'Bws.Gui.Tests.CatalogueGuards.Every_template_is_drawn_over_a_specimen_of_its_own_data'
    }
    @{
        # AN EXTREME SPECIMEN THAT IS NOT EXTREME. The ellipsis guard measures how wide the text
        # WANTS to be against the column it gets - a short label wants less, nothing is trimmed,
        # and a guard that only checked for TextTrimming in the markup would stay green.
        Name = 'catalogue: the trimmed label is drawn over a text long enough to be trimmed'
        File = 'src\Bws.Gui\ViewModels\Catalogue.Specimens.cs'
        From = '            "TrimmedButtonLabel" => ("Stop Print Spooler", "Stop " + LongName + " display name"),'
        To = '            "TrimmedButtonLabel" => ("Stop Print Spooler", "Stop"),'
        Test = 'Bws.Gui.Tests.CatalogueGuards.The_trimmed_button_label_ends_in_an_ellipsis_when_its_text_does_not_fit'
    }
    @{
        # A VIEW DROPPED FROM THE SHEET, which nothing in a dictionary would notice: a view is a
        # type, and the guard compares the sheet against the assembly's types in both directions.
        Name = 'catalogue: every view of the window is on the sheet'
        File = 'src\Bws.Gui\ViewModels\Catalogue.Views.cs'
        From = '            View(() => new ScopeBar(),
                data: ready.Data, empty: ready.Empty, loading: ready.Loading),'
        To = ''
        Test = 'Bws.Gui.Tests.CatalogueViewGuards.Every_view_the_window_is_built_out_of_is_on_the_sheet_and_nothing_else_is'
    }
    @{
        # THE PUMP TAKEN OUT - docs/10 trap 27 on the sheet. Without draining the dispatcher the
        # bindings have not attached when the views are measured, and three of them measure to
        # nothing and are put on the sheet as sentences about themselves.
        Name = 'catalogue: the views are measured only after their bindings have attached'
        File = 'src\Bws.Gui\ViewModels\Catalogue.Views.cs'
        From = '        Settle();

        return Group.OfViews('
        To = '        return Group.OfViews('
        Test = 'Bws.Gui.Tests.CatalogueViewGuards.Every_view_is_drawn_in_at_least_two_states_and_every_drawn_state_puts_a_pixel_on_the_sheet'
    }
    @{
        # THE PANEL CHOSEN BUT NOT SHOWN. Picking a row does not open the details panel in the
        # product either, so a sheet that only picked would draw the panel collapsed - the same
        # guard, a different cell.
        Name = 'catalogue: the details view is shown, not merely given a row'
        File = 'src\Bws.Gui\ViewModels\Catalogue.Views.cs'
        From = '            model.Chosen.Show();'
        To = ''
        Test = 'Bws.Gui.Tests.CatalogueViewGuards.Every_view_is_drawn_in_at_least_two_states_and_every_drawn_state_puts_a_pixel_on_the_sheet'
    }
    @{
        # THE GATE NEVER OPENED: a pool thread per sheet that never comes back, and a read that
        # nobody can wait for. The guard disposes and awaits the read - with the gate shut, the
        # await never returns and the test times out red.
        Name = 'catalogue: disposing the sheet lets the stalled read go'
        File = 'src\Bws.Gui\ViewModels\Catalogue.Views.cs'
        From = '        public void Dispose() => Stalling.Release();'
        To = '        public void Dispose() { }'
        Test = 'Bws.Gui.Tests.CatalogueViewGuards.Disposing_the_prepared_models_lets_the_stalled_reads_go'
    }
    @{
        # EVERY STYLE STRETCHED WITH THE SAME LONG LINE, numbers included - the sheet of
        # 2026-09-10 to 2026-09-16, on which a number column was never shown a number.
        Name = 'catalogue: a number style is stretched with a number'
        File = 'src\Bws.Gui\ViewModels\Catalogue.cs'
        From = '            Make(name, style, Extreme(key), enabled: true, limits));'
        To = '            Make(name, style, Long, enabled: true, limits));'
        Test = 'Bws.Gui.Tests.CatalogueExtremeGuards.A_style_that_draws_a_number_is_stretched_with_a_number_and_the_rest_with_a_line'
    }
    @{
        # THE PLAN LEAVING A NULL ORDER ALONE, which is the fault of 2026-09-15 put back exactly: a
        # layout file that names columns and no sort went to the grid as "take every order off",
        # and the owner's list opened in the manager's order for two weeks after a fresh profile
        # had started opening sorted. Plants the old line, so the plan-level guard has to see it.
        Name = 'layout: an order the file never mentioned becomes the opening order (plan)'
        File = 'src\Bws.Gui\ViewModels\ColumnPlan.cs'
        From = 'new ColumnLayout(kept, layout.Sort ?? ColumnLayout.DefaultFor(scope).Sort),'
        To = 'new ColumnLayout(kept, layout.Sort),'
        Test = 'Bws.Gui.Tests.ColumnLayoutGuards.A_layout_that_names_no_order_is_planned_with_the_order_this_build_opens_with'
    }
    @{
        # The same fault, seen from the window: the guard that opens a real window on such a file
        # has to find the display name heading marked, and this puts back the state in which it
        # was not. Two entries for one line because the two guards can be blinded separately - a
        # window that never handed the plan's order to the grid would pass the plan guard.
        Name = 'layout: an order the file never mentioned becomes the opening order (window)'
        File = 'src\Bws.Gui\ViewModels\ColumnPlan.cs'
        From = 'new ColumnLayout(kept, layout.Sort ?? ColumnLayout.DefaultFor(scope).Sort),'
        To = 'new ColumnLayout(kept, layout.Sort),'
        Test = 'Bws.Gui.Tests.KeptOrderGuards.A_file_that_remembers_columns_and_no_order_opens_sorted_by_display_name'
    }
    @{
        # THE ACCOUNT TABLE COMPARING WITH CASE, which is the fault the table was measured
        # against: 18 services on the owner's machine spell the system account localSystem and 19
        # spell the authority NT Authority. Compared with case, those show as two accounts beside
        # the same account under its documented spelling.
        Name = 'accounts: the documented spellings are matched without case'
        File = 'src\Bws.Gui\ViewModels\SystemAccounts.cs'
        From = 'string.Equals(account, documented, StringComparison.OrdinalIgnoreCase);'
        To = 'string.Equals(account, documented, StringComparison.Ordinal);'
        Test = 'Bws.Gui.Tests.SystemAccountTests.A_system_account_is_shown_by_its_name_whatever_its_case'
    }
    @{
        # THE ACCOUNT CELL SHOWING THE SPELLING AGAIN, which is the state before 2026-09-15 put
        # back exactly - and the export reads the cell, so the file would carry the spelling too.
        Name = 'accounts: the cell and the export show the name, not the spelling'
        File = 'src\Bws.Gui\ViewModels\Columns.cs'
        From = 'Reads = entry => SystemAccounts.Shown(entry.Account),'
        To = 'Reads = entry => CellFaces.Say(entry.Account, value => value),'
        Test = 'Bws.Gui.Tests.SystemAccountTests.The_export_writes_the_name_the_list_shows'
    }
    @{
        # THE COLUMN HOLDING NOTHING BEHIND THE NAME. The name would still show, so the list looks
        # right - and the tooltip would fall back to the trimming rule and say nothing over a
        # short name, which is the whole reason the column has to say what it holds.
        Name = 'accounts: the account cell offers the spelling it stands for'
        File = 'src\Bws.Gui\ViewModels\Columns.cs'
        From = 'Holds = entry => SystemAccounts.Held(entry.Account)'
        To = 'Holds = null'
        Test = 'Bws.Gui.Tests.ToolTipGuards.The_account_cell_offers_the_spelling_the_machine_holds_and_no_other_cell_does'
    }
    @{
        # THE TOOLTIP SWALLOWING WHAT THE COLUMN HOLDS. The column answers, the walk finds it, and
        # the tip says nothing - the shape of a tooltip that is wired and never speaks, which no
        # screenshot shows because nobody photographs a hover.
        Name = 'accounts: the tooltip says the spelling the column holds'
        File = 'src\Bws.Gui\CellTips.cs'
        From = 'return Texts.Of("gui.cell.held", held);'
        To = 'return null;'
        Test = 'Bws.Gui.Tests.ToolTipGuards.The_account_cell_offers_the_spelling_the_machine_holds_and_no_other_cell_does'
    }
    @{
        # THE PANEL DROPPING THE BRACKET. The details line would agree with the cell and disagree
        # with the reason the panel exists - the one place the exact value can be read and copied.
        # Repeats the name rather than dropping the bracket, because a branch that stopped using
        # `held` does not compile under this build's analysers - the first draft of this entry
        # came back BROKEN for exactly that.
        Name = 'accounts: the details line carries the spelling behind the name'
        File = 'src\Bws.Gui\ViewModels\Details.cs'
        From = '? Texts.Of("gui.details.held", shown, held)'
        To = '? Texts.Of("gui.details.held", shown, shown)'
        Test = 'Bws.Gui.Tests.DetailsGuards.The_account_line_carries_the_name_and_the_spelling_behind_it'
    }
    @{
        # THE ROW COMPARING SPELLINGS TO DECIDE WHETHER IT MOVED. A case change in the registry
        # would light the row up as changed while the cell shows the same name before and after.
        Name = 'accounts: a spelling changing case is not the row moving'
        File = 'src\Bws.Gui\ViewModels\EntryRow.cs'
        # Re-anchored 2026-09-28: the shown account is worked out once into a local (G-11).
        From = '            || account != _account'
        To = '            || entry.Account.Value != _entry.Account.Value'
        Test = 'Bws.Gui.Tests.SystemAccountTests.The_same_account_in_another_case_does_not_count_as_the_row_moving'
    }
    @{
        # THE KEY LEAVING THE ITEM, which is the whole point of the item: "Show details" without
        # "Enter" beside it is a menu that still keeps the shortcut secret.
        Name = 'details: the menu item names the key that does the same'
        File = 'src\Bws.Gui\RowMenu.cs'
        From = 'new RowMenuEntry("gui.menu.details", "gui.menu.details.gesture", () =>'
        To = 'new RowMenuEntry("gui.menu.details", null, () =>'
        Test = 'Bws.Gui.Tests.RowMenuGuards.The_menu_starts_with_show_details_and_writes_its_key_beside_it'
    }
    @{
        # THE STYLE DROPPING THE KEY. The entry would still say "Enter" and the item would draw
        # nothing - the fault the guard asserts on the ITEM for.
        Name = 'details: the style writes the key beside the label'
        File = 'src\Bws.Gui\Themes\Menus.xaml'
        From = '        <Setter Property="InputGestureText" Value="{Binding Gesture}" />'
        To = ''
        Test = 'Bws.Gui.Tests.RowMenuGuards.The_menu_starts_with_show_details_and_writes_its_key_beside_it'
    }
    @{
        # THE ITEM READING THE SELECTION INSTEAD OF THE POINTED ROW. Right click the second of two
        # selected rows and the panel shows the first - the confidently wrong answer the pointed
        # row exists to prevent.
        Name = 'details: the menu item shows the row that was pointed at'
        File = 'src\Bws.Gui\MainWindow.Menu.cs'
        From = '(_pointedAt ?? Entries.SelectedItem as EntryRow) is { } entry && OpenDetailsOf(entry);'
        To = '(Entries.SelectedItem as EntryRow) is { } entry && OpenDetailsOf(entry);'
        Test = 'Bws.Gui.Tests.RowMenuGuards.The_details_item_shows_the_row_that_was_pointed_at'
    }
    @{
        # THE DOUBLE CLICK TAKING WHATEVER IS SELECTED, so a double click on a heading - to fit its
        # column - opens a panel nobody asked for.
        Name = 'details: a double click on a heading opens nothing'
        File = 'src\Bws.Gui\MainWindow.Details.cs'
        From = 'if (e.ChangedButton != MouseButton.Left || RowUnder(e.OriginalSource) is not { } entry)'
        To = 'if (e.ChangedButton != MouseButton.Left || Entries.SelectedItem is not EntryRow entry)'
        Test = 'Bws.Gui.Tests.RowMenuGuards.A_double_click_on_a_row_opens_the_panel_and_one_on_a_heading_does_not'
    }
    @{
        # ANY BUTTON TWICE. A double right click is two menus, not a panel.
        Name = 'details: only the left button opens the panel twice over'
        File = 'src\Bws.Gui\MainWindow.Details.cs'
        From = 'if (e.ChangedButton != MouseButton.Left || RowUnder(e.OriginalSource) is not { } entry)'
        To = 'if (e.ChangedButton == MouseButton.Middle || RowUnder(e.OriginalSource) is not { } entry)'
        Test = 'Bws.Gui.Tests.RowMenuGuards.A_double_click_on_a_row_opens_the_panel_and_one_on_a_heading_does_not'
    }
    @{
        # THE HANDLER UNWIRED IN THE MARKUP. Every method behind it stays and every test calling a
        # method stays green - only a double click raised on the grid finds nothing listening.
        Name = 'details: a double click reaches the window at all'
        File = 'src\Bws.Gui\MainWindow.xaml'
        From = '                  MouseDoubleClick="OpenDetailsOnDoubleClick"'
        To = ''
        Test = 'Bws.Gui.Tests.RowMenuGuards.A_double_click_on_a_row_opens_the_panel_and_one_on_a_heading_does_not'
    }
    @{
        # THE PANEL OPENING OVER THE PLAN SHEET. Both share a column and both have to put the
        # other away - the plan's half has its own entry, this is the panel's.
        Name = 'details: opening the panel puts the plan sheet away'
        File = 'src\Bws.Gui\MainWindow.Details.cs'
        From = '        _model.Planned.Hide();'
        To = '        _ = _model.Planned.Showing;'
        Test = 'Bws.Gui.Tests.RowMenuGuards.Opening_the_panel_by_double_click_puts_the_plan_sheet_away'
    }
    @{
        # ITEMS BUILT AND WIRED TO NOTHING. The menu would look complete and every item would do
        # nothing - the shape a menu built from data can take that a menu in markup cannot.
        Name = 'details: every item of the row menu is wired to what it names'
        File = 'src\Bws.Gui\RowMenu.cs'
        # RE-ANCHORED 2026-09-24: one item is made in RowMenu.Made since the submenu arrived.
        From = '            made.Click += async (_, _) => await entry.Act().ConfigureAwait(true);'
        To = ''
        Test = 'Bws.Gui.Tests.CopyingAndPanelGuards.The_context_menu_copies_what_each_item_promises'
    }
    @{
        # ENTER LOSING ITS ROAD when the road was shared. The refactor that gave the panel three
        # doors could have closed the first, and the guard for Enter has stood since 2026-08-17.
        Name = 'details: Enter still opens the panel after the road was shared'
        File = 'src\Bws.Gui\MainWindow.Keyboard.cs'
        From = 'return Entries.SelectedItem is EntryRow chosen && OpenDetailsOf(chosen);'
        To = 'return Entries.SelectedItem is EntryRow;'
        Test = 'Bws.Gui.Tests.CopyingAndPanelGuards.Enter_opens_the_panel_on_the_window'
    }

    # ------------------------------------------------------------------------------------------
    # PACKET B OF THE REVIEW OF 2026-09-15, `docs/11` 2.14: the button at the foot of a plan names
    # the act, and the box of seconds says no out loud.
    # ------------------------------------------------------------------------------------------
    @{
        # THE NAME A PERSON RECOGNISES, not the manager's. A button reading "Stop WSearch" under a
        # heading reading "What stopping Windows Search would do" is two names for one thing - the
        # complaint point 6 of the same review is about.
        Name = 'carry out: the button names the entry the way the heading does'
        File = 'src\Bws.Gui\ViewModels\Planned.Title.cs'
        From = '        ActionKind.Stop => Texts.Of("gui.plan.carryOut.stop.one", Named(plan)),'
        To = '        ActionKind.Stop => Texts.Of("gui.plan.carryOut.stop.one", Only(plan)),'
        Test = 'Bws.Gui.Tests.CarryOutLabelGuards.The_button_names_the_act_and_the_entry_a_person_recognises'
    }
    @{
        # THE PROCESS NUMBER WINS OVER THE KIND, which is the owner's decision of 2026-09-06 kept
        # inside the wider one of 2026-09-15. Losing it would put "Force stop Spooler" on a button
        # whose press ends process 4812 - the preview and the button disagreeing.
        Name = 'carry out: a plan that ends one process still names the process'
        File = 'src\Bws.Gui\ViewModels\Planned.Title.cs'
        From = '        : EndsOneProcess(plan) is { } ending
            ? Texts.Of("gui.plan.carryOut.endProcess", ending)
            : Asked(plan) == 1'
        To = '        : Asked(plan) == 1'
        Test = 'Bws.Gui.Tests.ForcedStopGuards.The_button_on_a_forcing_plan_names_the_process'
    }
    @{
        # A COUNT FOR SEVERAL, A NAME FOR ONE. Naming the first of three on the button would be a
        # button about one entry on a sheet about three.
        Name = 'carry out: several entries are counted rather than the first one named'
        File = 'src\Bws.Gui\ViewModels\Planned.Title.cs'
        From = '            : Asked(plan) == 1
                ? OneOn(plan)'
        To = '            : Asked(plan) >= 1
                ? OneOn(plan)'
        Test = 'Bws.Gui.Tests.CarryOutLabelGuards.Several_entries_are_counted_on_the_button_rather_than_named'
    }
    @{
        # THE VALUE IS THE WORD THAT MATTERS on a start type plan - "Set 3 entries to Disabled" -
        # and the owner chose it over "Set the startup type of" for exactly that reason.
        Name = 'carry out: a start type plan over several entries still names the type'
        File = 'src\Bws.Gui\ViewModels\Planned.Title.cs'
        # RE-ANCHORED 2026-09-24: the arm split in two when a setting learned to carry a stop.
        From = '            : Texts.Of("gui.plan.carryOut.setStartType.many", Asked(plan), SetTo(plan)),'
        To = '            : Texts.Of("gui.plan.carryOut.stop.many", Asked(plan)),'
        Test = 'Bws.Gui.Tests.CarryOutLabelGuards.Several_entries_set_to_a_type_are_counted_and_the_type_is_still_named'
    }
    @{
        # A NAME IS UNBOUNDED AND THE ROW IS NOT. Without the cap a 116 character display name
        # pushes the waiting box off the footer's row, and the heading already carries the name.
        Name = 'carry out: the button stops growing at the width the theme allows it'
        File = 'src\Bws.Gui\PlanFooter.xaml'
        From = '                        MaxWidth="{StaticResource WidthCarryOutMost}"'
        To = ''
        Test = 'Bws.Gui.Tests.CarryOutLabelGuards.The_longest_display_name_on_this_machine_is_trimmed_inside_the_buttons_ceiling'
    }
    @{
        # CUT MID-LETTER OR TRIMMED WITH AN ELLIPSIS - `docs/11` 3.5. A capped button with a plain
        # string clips its word at the edge, and the template is the only thing that makes it trim.
        Name = 'carry out: a name too long for the button is trimmed rather than cut'
        File = 'src\Bws.Gui\PlanFooter.xaml'
        From = '                        ContentTemplate="{StaticResource TrimmedButtonLabel}"'
        To = ''
        Test = 'Bws.Gui.Tests.CarryOutLabelGuards.The_longest_display_name_on_this_machine_is_trimmed_inside_the_buttons_ceiling'
    }
    @{
        # THE RULE ITSELF, in the one place both interfaces read it: at least one second. Zero was
        # the first of the silent refusals the old setter made.
        Name = 'ceiling: zero seconds is not a number of seconds'
        File = 'src\Bws.Core\Planning\StepCeiling.cs'
        From = '        && seconds >= 1'
        To = '        && seconds >= 0'
        Test = 'Bws.Core.Tests.StepCeilingTests.Anything_else_names_no_number_of_seconds'
    }
    @{
        # THE TERMINAL'S HALF OF THE RULE: a value that is not seconds is handed back as the
        # mistake, never swallowed into the default. Held by a run of the real executable.
        Name = 'ceiling: the terminal refuses what is not seconds rather than defaulting'
        File = 'src\Bws.Cli\Arguments.cs'
        From = '        if (StepCeiling.Seconds(value) is not { } seconds)
        {
            return value;
        }'
        To = '        if (StepCeiling.Seconds(value) is not { } seconds)
        {
            return null;
        }'
        Test = 'Bws.Integration.Tests.PlanContractTests.A_timeout_that_is_not_a_number_of_seconds_is_a_mistake_rather_than_a_default'
    }
    @{
        # THE BOX SAYING NO OUT LOUD, which is the whole of point 5. With this gone the setter is
        # silent again: the text is kept, nothing under the box says why, and the button is live.
        Name = 'waiting: text that is not seconds is said to be wrong'
        File = 'src\Bws.Gui\ViewModels\Planned.Waiting.cs'
        From = '        Waits && StepCeiling.Seconds(_waitingText.Trim()) is null'
        To = '        Waits && StepCeiling.Seconds(_waitingText.Trim()) is null && false'
        Test = 'Bws.Gui.Tests.WaitingGuards.Text_that_is_not_a_number_of_seconds_is_said_to_be_wrong_and_greys_the_button'
    }
    @{
        # THE PRESS STOPS AT THE SEAM. A live button over a box reading "abc" would hand a run a
        # number the box does not hold - the screen and the machine disagreeing.
        Name = 'waiting: a wrong box greys the button'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        From = '        && !HasWaitingProblem && Confirmed;'
        To = '        && Confirmed;'
        Test = 'Bws.Gui.Tests.WaitingGuards.A_wrong_box_stops_the_press_before_any_run_is_given_a_number'
    }
    @{
        # THE ONE LENIENCY, said where it is: a space around the number is the window's to forgive.
        Name = 'waiting: a space around the number is forgiven by the window'
        File = 'src\Bws.Gui\ViewModels\Planned.Waiting.cs'
        From = '    public int Waiting => StepCeiling.Seconds(_waitingText.Trim()) ?? (int)StepCeiling.Default.TotalSeconds;'
        To = '    public int Waiting => StepCeiling.Seconds(_waitingText) ?? (int)StepCeiling.Default.TotalSeconds;'
        Test = 'Bws.Gui.Tests.WaitingGuards.A_whole_number_of_seconds_is_taken_and_the_button_comes_back'
    }
    @{
        # THE NOTIFICATION THAT WAS MISSING FOR SIX DAYS. Busy announces only itself, so without
        # this line the binding that hides the box during a run is never told to look again.
        Name = 'waiting: starting a run announces that the box is going'
        File = 'src\Bws.Gui\ViewModels\Planned.Running.cs'
        # Re-anchored 2026-09-24 without the closing brace, for the same raise of the warnings.
        From = '        Raise(nameof(Waits));
        RaiseTheProblem();'
        To = '        RaiseTheProblem();'
        Test = 'Bws.Gui.Tests.WaitingGuards.Starting_a_run_announces_that_the_box_is_going_so_the_screen_hears_it'
    }
    @{
        # THE BINDING HEARS IT IN THE FRAMEWORK'S WORDS, which is what colours the edge. With the
        # answer gone Validation.HasError never goes true and the edge stays the library's grey.
        Name = 'waiting: the binding on the box is told what is wrong'
        File = 'src\Bws.Gui\ViewModels\Planned.Waiting.cs'
        From = '        property == nameof(WaitingText) && HasWaitingProblem ? [WaitingProblem] : [];'
        To = '        property == nameof(WaitingText) && HasWaitingProblem && false ? [WaitingProblem] : [];'
        Test = 'Bws.Gui.Tests.WaitingBoxGuards.The_box_wears_the_problem_colour_on_its_edge_while_it_is_wrong_and_not_before'
    }
    @{
        # THE COLOUR REACHING A PIXEL, through somebody else's template. The setter is the only
        # thing painting the edge, and a style with the trigger and no setter looks identical in
        # markup that is read rather than rendered.
        # MOVED WITH THE STYLE 2026-09-23: WaitingBox is a style on ReportingBox now, in
        # Controls.xaml, and the setter lives there. The same pixel test proves it for both fields.
        Name = 'waiting: the wrong box wears the problem colour on its edge'
        File = 'src\Bws.Gui\Themes\Controls.xaml'
        From = '                <Setter Property="BorderBrush" Value="{StaticResource MeaningRejected}" />'
        To = ''
        Test = 'Bws.Gui.Tests.WaitingBoxGuards.The_box_wears_the_problem_colour_on_its_edge_while_it_is_wrong_and_not_before'
    }
    @{
        # THE SENTENCE REACHING THE SCREEN. A TextBlock bound to nothing keeps the reserved row
        # and says nothing in it - the fault this project has met as a dead binding before.
        Name = 'waiting: the sentence under the row reads the problem'
        File = 'src\Bws.Gui\PlanFooter.xaml'
        From = '                           Text="{Binding WaitingProblem}"'
        To = '                           Text=""'
        Test = 'Bws.Gui.Tests.WaitingBoxGuards.The_sentence_under_the_row_reads_the_problem_and_the_row_was_already_reserved_for_it'
    }
    @{
        # THE ROW IS RESERVED. Collapsing the sentence while it is empty is the natural edit, and
        # it makes the sheet grow by a line under a hand aiming at the button - GUI rule 3.
        Name = 'waiting: the row under the box is reserved so nothing jumps'
        File = 'src\Bws.Gui\PlanFooter.xaml'
        From = '                           Text="{Binding WaitingProblem}"
                           TextWrapping="Wrap"'
        To = '                           Text="{Binding WaitingProblem}"
                           Visibility="{Binding HasWaitingProblem, Converter={StaticResource WhenTrue}}"
                           TextWrapping="Wrap"'
        Test = 'Bws.Gui.Tests.WaitingBoxGuards.The_sentence_under_the_row_reads_the_problem_and_the_row_was_already_reserved_for_it'
    }
    @{
        # THE WIDTH IS THE TOKEN - backlog 352 - and a raw number put back in the view is the exact
        # edit this entry exists to catch.
        Name = 'waiting: the box is as wide as the theme says'
        File = 'src\Bws.Gui\PlanFooter.xaml'
        From = '                             Width="{StaticResource WidthWaitingBox}"'
        To = '                             Width="64"'
        Test = 'Bws.Gui.Tests.WaitingBoxGuards.The_box_is_as_wide_as_the_theme_says'
    }
    @{
        # THE CATALOGUE SHOWS THE WRONG STATE OF THE ONE COMPONENT THAT HAS ONE, read off the
        # style's own trigger. With the reading gone the sheet prints a dash where the state is.
        Name = 'catalogue: a component whose style answers to Validation.HasError is shown wrong'
        File = 'src\Bws.Gui\ViewModels\Catalogue.States.cs'
        From = '        if (!Triggers(style).Any(trigger => trigger.Property == Validation.HasErrorProperty))'
        To = '        if (!Triggers(style).Any(trigger => trigger.Property == Validation.HasErrorProperty) || true)'
        Test = 'Bws.Gui.Tests.CatalogueGuards.Every_component_that_can_be_wrong_is_shown_wrong_on_the_sheet'
    }
    @{
        # POINT 6: one word for one thing in the window, the dictionary's word. The mutation is the
        # exact edit a session would make on finding "start type" in the terminal's file and
        # "startup type" in the window's, looking the wrong way round.
        Name = 'wording: the window says startup type, never start type'
        File = 'src\Bws.Gui\Resources\gui.en.json'
        From = '  "gui.column.startType": "Startup type",'
        To = '  "gui.column.startType": "Start type",'
        Test = 'Bws.Gui.Tests.DictionaryWordGuards.The_window_says_startup_type_the_way_the_dictionary_and_services_msc_do'
    }

    # ------------------------------------------------------------------------------------------
    # PACKET C OF THE SAME REVIEW: the filter row as a form, and the small things.
    # ------------------------------------------------------------------------------------------
    @{
        # THE SHARED SIZE SCOPE IS WHAT MAKES FIVE LABEL COLUMNS ONE WIDTH. Without it every group
        # sizes its own label column and the chips start wherever that group's label ends - which
        # is five labels that happen to be left-aligned and not a column.
        Name = 'filters: the labels of the groups stand in one column'
        File = 'src\Bws.Gui\FilterRow.xaml'
        From = '                      Grid.IsSharedSizeScope="True"'
        To = ''
        Test = 'Bws.Gui.Tests.FilterRowGuards.Every_filter_group_has_its_own_row_and_the_labels_stand_in_one_column'
    }
    @{
        # THE WRAP PANEL PUT BACK, which is the exact markup this row had until 2026-09-15 and the
        # exact edit a session would make to win the row back: groups flow again, two share a row,
        # and the fifth label lands beside the fourth group's chips.
        Name = 'filters: every group has a row of its own'
        File = 'src\Bws.Gui\FilterRow.xaml'
        From = '                      ItemTemplate="{StaticResource FilterGroupTemplate}">
            <ItemsControl.Style>'
        To = '                      ItemTemplate="{StaticResource FilterGroupTemplate}">
            <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate>
                    <WrapPanel />
                </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
            <ItemsControl.Style>'
        Test = 'Bws.Gui.Tests.FilterRowGuards.Every_filter_group_has_its_own_row_and_the_labels_stand_in_one_column'
    }
    @{
        # THE SQUARE IS WHAT KEEPS THE BUTTON ONE WIDTH. Without it the Path is as wide as its
        # triangle again - eight pointing down, six and a half pointing right - and Columns steps
        # sideways on every press of Filters.
        Name = 'filters: the button is as wide folded as open'
        File = 'src\Bws.Gui\Themes\Chips.xaml'
        From = '        <Setter Property="Width" Value="{StaticResource SizeDisclosureMark}" />'
        To = ''
        Test = 'Bws.Gui.Tests.FilterRowGuards.The_filters_button_is_as_wide_folded_as_open'
    }
    @{
        # THE HINT NAMES THE LIST, and the drivers list saying "services" is the exact wrong
        # sentence this point was about, one scope over.
        Name = 'search hint: the drivers list says drivers'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Scope.cs'
        From = '        EntryScope.Drivers => Texts.Of("gui.search.hint.drivers"),'
        To = '        EntryScope.Drivers => Texts.Of("gui.search.hint.services"),'
        Test = 'Bws.Gui.Tests.ScopeTests.The_hint_behind_the_box_names_the_list_the_box_searches'
    }
    @{
        # THE NOTIFICATION, without which the binding keeps the sentence of the list before -
        # position 19, the fault that reddens nothing.
        Name = 'search hint: moving the scope announces the hint'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Scope.cs'
        From = '            Raise(nameof(Scope));
            Raise(nameof(SearchHint));'
        To = '            Raise(nameof(Scope));'
        Test = 'Bws.Gui.Tests.ScopeTests.The_hint_behind_the_box_names_the_list_the_box_searches'
    }
    @{
        # THE COUNT FROM EITHER SIDE OF THE CUT. On the services list the drivers are everything
        # minus what was cut, and taking the cut as the drivers puts the services count on the
        # Drivers position and the difference on Services.
        Name = 'scope counts: the services list counts what it holds'
        File = 'src\Bws.Gui\ViewModels\Scopes.cs'
        From = '        _drivers = Current == EntryScope.Drivers ? InScope.Count : _all - InScope.Count;'
        To = '        _drivers = InScope.Count;'
        Test = 'Bws.Gui.Tests.ScopeTests.Every_position_counts_the_list_it_stands_for_whatever_is_typed_or_chosen'
    }
    @{
        # THE POSITIONS ARE TOLD. A reading recuts the scope, and a position that never hears
        # about it keeps the machine as it was at start - position 19, the fault that reddens
        # nothing.
        Name = 'scope counts: a reading announces the count'
        File = 'src\Bws.Gui\ViewModels\Scopes.cs'
        From = '        Raise(nameof(Count));'
        To = ''
        Test = 'Bws.Gui.Tests.ScopeTests.A_reading_tells_every_position_that_its_count_may_have_moved'
    }
    @{
        # THE NUMBER REACHES THE WINDOW. A template with the count taken out draws three positions
        # exactly as they were for a month, and every model test stays green.
        Name = 'scope counts: every position draws its count'
        File = 'src\Bws.Gui\ScopeBar.xaml'
        From = '                                    <TextBlock Text="{Binding Count}"'
        To = '                                    <TextBlock Text=""'
        Test = 'Bws.Gui.Tests.ScopeBarGuards.Every_position_draws_its_count_and_keeps_its_name'
    }
    @{
        # THE WORDS PRESS THE SWITCH. A handler that reads the property instead of writing it is
        # a link that looks like a link and does nothing - the shape a Hyperlink takes the day
        # somebody refactors the model property and leaves the view behind.
        Name = 'notice link: the words press the switch'
        File = 'src\Bws.Gui\StatusRow.xaml.cs'
        From = '            model.ShowingEveryInstance = true;'
        To = '            _ = model.ShowingEveryInstance;'
        Test = 'Bws.Gui.Tests.FoldingGuards.Pressing_the_words_in_the_sentence_unfolds_the_copies'
    }
    @{
        # THE LINK IS THE SWITCH'S OWN WORDS. A sentence with the words taken out is a sentence
        # that no longer names the way out - the fault point 8(d) was raised against, one step on.
        Name = 'notice link: the link is the words the switch wears'
        File = 'src\Bws.Gui\ViewModels\Sentences.cs'
        From = '                Texts.Of("gui.instances.toggle"),'
        To = '                string.Empty,'
        Test = 'Bws.Gui.Tests.FoldingGuards.The_sentence_about_folded_copies_carries_the_switch_as_a_link_and_reads_as_one_line'
    }
    @{
        # AN EMPTY LINK IS NOT A TAB STOP. Without the binding the Hyperlink stays enabled with no
        # words in it, and Tab lands on nothing between the list and the button beside it.
        Name = 'notice link: an empty link is switched off'
        File = 'src\Bws.Gui\StatusRow.xaml'
        From = '                                                                                                          IsEnabled="{Binding Says.NoticeHasLink}"'
        To = ''
        Test = 'Bws.Gui.Tests.FoldingGuards.Pressing_the_words_in_the_sentence_unfolds_the_copies'
    }
    @{
        # THREE RUNS, THREE NOTIFICATIONS. The line on the window is read after the press, and a
        # piece not announced keeps the sentence of the answer before - position 19 again.
        Name = 'notice link: every piece of the line is announced'
        File = 'src\Bws.Gui\ViewModels\Says.cs'
        From = '        Raise(nameof(NoticeBeforeLink));'
        To = ''
        Test = 'Bws.Gui.Tests.FoldingGuards.Pressing_the_words_in_the_sentence_unfolds_the_copies'
    }

    # ---------------------------------------------------------------------------------------
    # Completions under the search box - point 9 of the UX review, backlog 15, 2026-09-15. The
    # core half: what the language offers where the caret stands. Each entry takes ONE rule of
    # `docs/PROJEKT-PODPOWIEDZI-20260915.md` section 4.4 away and names the row of the state
    # table that notices.
    # ---------------------------------------------------------------------------------------
    @{
        # PREFIX, NOT CONTAINMENT, FOR FIELDS. With containment `on` grows a list of two fields
        # under every free search for a word with those letters in it.
        Name = 'completion: a field name is matched by its prefix and not by containment'
        File = 'src\Bws.Core\Querying\QueryCompletions.cs'
        From = 'if (!spelled.StartsWith(prefix, StringComparison.Ordinal))'
        To = 'if (!spelled.Contains(prefix, StringComparison.Ordinal))'
        Test = 'QueryCompletionTests.A_field_name_is_matched_by_its_prefix_and_never_by_containment'
    }
    @{
        # AN EMPTY FIELD PREFIX IS QUIET WHILE TYPING AND LOUD ON REQUEST. Flipped, the box that
        # was just clicked into grows twenty-one rows and Down on it grows none.
        Name = 'completion: an empty field prefix offers nothing while typing'
        File = 'src\Bws.Core\Querying\QueryCompletions.cs'
        From = 'if (prefix.Length == 0 && !asked)'
        To = 'if (prefix.Length == 0 && asked)'
        Test = 'QueryCompletionTests.An_empty_box_offers_nothing_while_typing_and_every_field_on_request'
    }
    @{
        # WRITTEN IN FULL IS HIDDEN WHILE TYPING. Flipped, accepting `running` reopens the list
        # with `running` alone, and Down on it offers nothing - both halves of the same rule.
        # Anchored on the comment beside it, because the condition itself stands twice in the file.
        Name = 'completion: a value written in full is hidden while typing and offered on request'
        File = 'src\Bws.Core\Querying\QueryCompletions.cs'
        From = 'if (!asked && spelled.Length == prefix.Length)
                {
                    // Already there in full'
        To = 'if (asked && spelled.Length == prefix.Length)
                {
                    // Already there in full'
        Test = 'QueryCompletionTests.A_value_written_in_full_is_hidden_while_typing_and_offered_on_request'
    }
    @{
        # THE SECOND MATCH FOR VALUES: the start of a later part of a compound word. Without it
        # `pend` finds `pending` and not the four that end in it - decision 10 of the design.
        Name = 'completion: a later part of a compound value matches'
        File = 'src\Bws.Core\Querying\QueryCompletions.cs'
        From = 'else if (prefix.Length > 0 && BeginsALaterPart(word, prefix))'
        To = 'else if (prefix.Length > 0 && BeginsALaterPart(word, prefix) && false)'
        Test = 'QueryCompletionTests.A_later_part_of_a_compound_value_matches_too_and_comes_after_the_whole_word_matches'
    }
    @{
        # THE SPACE AFTER A VALUE THAT ENDS THE LINE, so the next keystroke starts a new member.
        # Without it `status:running` accepted and `s` typed is `status:runnings`.
        Name = 'completion: a value ending the line gets a space after it'
        File = 'src\Bws.Core\Querying\QueryCompletions.cs'
        From = 'var written = closesTheLine ? word + " " : word;'
        To = 'var written = word;'
        Test = 'QueryCompletionTests.A_value_that_ends_the_line_gets_a_space_after_it_and_one_that_does_not_gets_nothing'
    }
    @{
        # ONLY A MEMBER THAT READS AS IT IS WRITTEN IS COMPLETED. Without the check a quoted
        # member gets values written into a range that includes its quotes.
        Name = 'completion: a member in quotes or with an escape gets nothing'
        File = 'src\Bws.Core\Querying\QueryCompletions.cs'
        From = 'if (!string.Equals(members[at].Text, written, StringComparison.Ordinal))
        {
            return [];
        }'
        To = ''
        Test = 'QueryCompletionTests.Nothing_is_offered_inside_quotes_or_after_a_quote_that_never_closes'
    }
    @{
        # THE CARET IN WHITESPACE IS AN EMPTY MEMBER, which is what Down after a finished member
        # asks about. Without it there is no way to be offered a field name after a space.
        Name = 'completion: the caret in whitespace is an empty member'
        File = 'src\Bws.Core\Querying\QueryCompletions.cs'
        From = 'return Fields(typed: string.Empty, replaces: caret..caret, colonFollows: false, asked);'
        To = 'return [];'
        Test = 'QueryCompletionTests.The_caret_in_whitespace_is_an_empty_member'
    }
    @{
        # THE VALUE UNDER THE CARET STARTS AFTER THE LAST COMMA BEFORE IT. Without that the
        # prefix for `status:running,st` is `running,st` and nothing fits.
        Name = 'completion: the value under the caret is the one after the last comma'
        File = 'src\Bws.Core\Querying\QueryCompletions.cs'
        From = 'var valueStart = lastComma < 0 ? from : lastComma + 1;'
        To = 'var valueStart = from;'
        Test = 'QueryCompletionTests.The_value_under_the_caret_is_the_one_between_the_commas_around_it'
    }
    @{
        # A CARET OUTSIDE THE TEXT IS MOVED TO ITS NEAREST END. Without the clamp a caret past the
        # end stands in no member, and a negative one builds a Range that throws.
        Name = 'completion: a caret outside the text is moved to its nearest end'
        File = 'src\Bws.Core\Querying\QueryCompletions.cs'
        From = '        caret = Math.Clamp(caret, 0, text.Length);'
        To = ''
        Test = 'QueryCompletionTests.A_caret_outside_the_text_is_moved_to_its_nearest_end_and_a_null_text_is_empty'
    }
    @{
        # THE THREE RESERVED WORDS FOLLOW A FIELD'S OWN VALUES. Without them a text field offers
        # nothing at all, and `name:?` - the audit question - is never taught.
        Name = 'completion: the reserved words follow the values of every field'
        File = 'src\Bws.Core\Querying\QueryCompletions.cs'
        From = '            .Concat(QueryFields.ReservedWords.Select(word => (word, QueryCompletionKind.Word)));'
        To = '            ;'
        Test = 'QueryCompletionTests.A_text_field_offers_only_the_three_reserved_words'
    }
    @{
        # THE CARET IN THE MIDDLE OF A FIELD NAME REPLACES THE WHOLE NAME, as a shell does.
        # Replacing only what stands before the caret writes `starttus:running`.
        Name = 'completion: the caret in the middle of a field name completes the whole name'
        File = 'src\Bws.Core\Querying\QueryCompletions.cs'
        From = 'return Fields(text[body..Math.Max(caret, body)], body..colon, colonFollows: true, asked);'
        To = 'return Fields(text[body..Math.Max(caret, body)], body..Math.Max(caret, body), colonFollows: true, asked);'
        Test = 'QueryCompletionTests.The_caret_in_the_middle_of_a_word_completes_the_whole_word'
    }
    @{
        # A LEADING EXCLAMATION MARK IS SKIPPED THE WAY THE PARSER SKIPS IT. Without that `!sta`
        # is a prefix no field begins with, and negation cannot be completed at all.
        Name = 'completion: a leading exclamation mark is skipped and stays where it was'
        File = 'src\Bws.Core\Querying\QueryCompletions.cs'
        From = "var body = written.StartsWith('!') ? start + 1 : start;"
        To = 'var body = start;'
        Test = 'QueryCompletionTests.A_leading_exclamation_mark_is_skipped_and_stays_where_it_was'
    }
    @{
        # ONE LIST FOR THE THREE WORDS, in the order they are offered. A consumer lining them up
        # for itself is a copy of a frozen contract, and this is what notices the list shrinking.
        Name = 'completion: the reserved words are one list of three'
        File = 'src\Bws.Core\Querying\QueryFields.cs'
        From = 'public static IReadOnlyList<string> ReservedWords { get; } = [Any, None, Unreadable];'
        To = 'public static IReadOnlyList<string> ReservedWords { get; } = [Any, None];'
        Test = 'QueryCompletionTests.The_reserved_words_are_one_list_in_the_order_they_are_offered'
    }

    # The model half: when the list opens, what it holds, what is chosen. Section 4.5 of the design,
    # and the three rules a window would have got wrong quietly come first.
    @{
        # A CHIP WRITES INTO THE MODEL, THE BINDING WRITES INTO THE BOX, THE BOX SAYS ITS TEXT
        # CHANGED - with the keyboard in the chip. Without this gate a list opens under a box
        # nobody is typing into, and nothing but Escape can close it.
        Name = 'suggesting: typing followed without the keyboard in the box opens nothing'
        # RE-ANCHORED 2026-09-25: the gate in Follow took a third condition, the caret move.
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = 'if (!_keyboardHere || selectionLength > 0 || (sameText && !sameCaret))'
        To = 'if (selectionLength > 0 || (sameText && !sameCaret))'
        Test = 'SuggestingTests.Typing_followed_without_the_keyboard_in_the_box_opens_nothing'
    }
    @{
        # AFTER CTRL+F THE WHOLE TEXT IS SELECTED AND THE CARET STANDS AT ZERO. A prefix counted
        # there is false, and a list of twelve values would open over the selection.
        Name = 'suggesting: a selection is never completed'
        # RE-ANCHORED 2026-09-25: the gate in Follow took a third condition, the caret move.
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = 'if (!_keyboardHere || selectionLength > 0 || (sameText && !sameCaret))'
        To = 'if (!_keyboardHere || (sameText && !sameCaret))'
        Test = 'SuggestingTests.A_selection_is_never_completed'
    }
    @{
        # FOCUS RESTORED BY THE FRAMEWORK IS NOT A PERSON ARRIVING. This is the first version of
        # the design, which opened the examples on every return after Alt+Tab.
        Name = 'suggesting: the keyboard merely being in the box offers nothing'
        # RE-ANCHORED 2026-09-25 (review of PR 22): the keyboard leaving also ends a composition.
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = '            _composing = false;
            Close();
        }'
        To = '            _composing = false;
            Close();
        }
        else
        {
            Offer(Examples(string.Empty));
        }'
        Test = 'SuggestingTests.The_keyboard_merely_being_in_the_box_offers_nothing'
    }

    # THE EXAMPLES FOLLOW THE SCOPE - backlog 358, 2026-09-16. Three entries for the three places
    # the cut can be lost: the declaration itself, the property that reads it, and the
    # notification that tells the box it moved.
    @{
        # Every example on every list, which is the tooltip backlog 358 found: account:LocalSystem
        # offered on Drivers, where a driver has no account and it selects nothing. The field is
        # still read so the mutation compiles with warnings as errors.
        Name = 'examples: a question is offered only on the lists it can narrow'
        File = 'src\Bws.Gui\ViewModels\QueryExamples.cs'
        From = '    public bool Fits(EntryScope scope) => _scopes.Contains(scope);'
        To = '    public bool Fits(EntryScope scope) => _scopes.Count >= 0;'
        Test = 'QueryExampleScopeTests.Every_example_shown_on_a_list_narrows_it_and_every_example_hidden_from_it_would_not'
    }

    @{
        # The declaration is right and nothing reads it: the model hands out all six on every
        # scope, so the tooltip and the list under the box show the hidden ones anyway.
        Name = 'examples: the box reads the questions cut for the list on screen'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Scope.cs'
        From = '    public IReadOnlyList<QueryExample> Examples => QueryExamples.For(Scope);'
        To = '    public IReadOnlyList<QueryExample> Examples => QueryExamples.All;'
        Test = 'QueryExampleScopeTests.The_tooltip_and_the_list_under_the_box_offer_the_same_questions_as_the_scope'
    }

    @{
        # The cut is right and the binding is never told. The tooltip is one string bound on the
        # box, read when the box was built - a scope switch that does not raise it leaves the
        # Services tooltip on the Drivers list for as long as the window stays open.
        Name = 'examples: moving the scope tells the box its questions changed'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Scope.cs'
        From = '            Raise(nameof(Examples));
            Raise(nameof(SearchTip));'
        To = ''
        Test = 'QueryExampleScopeTests.Moving_the_scope_tells_the_box_that_its_questions_and_its_tooltip_changed'
    }

    @{
        # UX-GUI-008, 2026-09-23: the example asked "should be running and is not" as
        # start:auto !status:running while the chip beside the box asked it as mismatch:stopped -
        # 7 services against 1 on the machine the audit was taken on. The mutation puts the old
        # spelling back, which still parses and still narrows every list, so only the test that
        # asks the chip can tell.
        Name = 'examples: the should-be-running question is the one the chip asks'
        File = 'src\Bws.Gui\ViewModels\QueryExamples.cs'
        From = 'new QueryExample("gui.example.shouldBeRunning", "mismatch:stopped",'
        To = 'new QueryExample("gui.example.shouldBeRunning", "start:auto !status:running",'
        Test = 'QueryExampleTests.The_question_about_what_should_be_running_is_the_one_the_chip_asks'
    }

    @{
        # WHAT A SCREEN READER IS TOLD, off by one - backlog 361. The sentence carries the row's
        # place in the list, and a place counted from zero says "0 of 5" for the first row, which
        # a person without the screen has no way to notice is wrong. The window half - raising the
        # notification on the box - has no test in this registry and cannot: it needs a UIA client
        # outside the process, which is tools/gui-probe/announce.ps1.
        Name = 'suggesting: the chosen row is spoken with its place counted from one'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = '            var position = IndexOf(_chosen) + 1;'
        To = '            var position = IndexOf(_chosen);'
        Test = 'SuggestingWordingTests.The_chosen_row_is_spoken_as_its_word_its_sentence_and_its_place_in_the_list'
    }

    @{
        # The same sentence with the words gone: a reader told "2 of 5" and nothing else knows it
        # moved and not to what. The bare form is kept for rows that HAVE no sentence, so the
        # mutation reaches for it where a sentence exists.
        Name = 'suggesting: the chosen row is spoken with its sentence when it has one'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = '            return string.IsNullOrEmpty(_chosen.Meaning)'
        To = '            return true'
        Test = 'SuggestingWordingTests.The_chosen_row_is_spoken_as_its_word_its_sentence_and_its_place_in_the_list'
    }

    @{
        # DECISION 2: THE QUESTIONS ON ARRIVAL AT AN EMPTY BOX - six until backlog 358 cut them to
        # the list on screen. Without this the examples are reachable only through Down, and a
        # person who clicks the box sees nothing.
        Name = 'suggesting: arriving at an empty box offers the questions that fit the list'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = '        if (string.IsNullOrWhiteSpace(text))
        {
            _questions = true;
            Offer(Examples(text ?? string.Empty));
        }'
        To = ''
        Test = 'SuggestingTests.A_person_arriving_at_an_empty_box_is_offered_the_questions_that_fit_the_list'
    }
    @{
        # THE EXAMPLES ARE FOR ARRIVING, NOT FOR EMPTYING. This is the shape that makes Escape,
        # Escape, Escape mean close the list, clear the box, and here is the list again.
        Name = 'suggesting: emptying the box does not bring the questions back'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = '        Offer(Rows(QueryCompletions.WhileTyping(text, caret)));'
        To = '        Offer(string.IsNullOrWhiteSpace(text)
            ? Examples(text ?? string.Empty)
            : Rows(QueryCompletions.WhileTyping(text, caret)));'
        Test = 'SuggestingTests.Emptying_the_box_does_not_bring_the_questions_back'
    }
    @{
        # DOWN, DOWN, A LETTER: the choice stays on its word. Without this every keystroke sends
        # the highlight back to the first row.
        Name = 'suggesting: the choice is kept by word across a replacement of the list'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = 'Chosen = rows.FirstOrDefault(row => string.Equals(row.Word, kept, StringComparison.Ordinal)) ?? rows[0];'
        To = 'Chosen = rows[0];'
        Test = 'SuggestingTests.The_choice_is_kept_by_word_across_a_replacement_of_the_list'
    }
    @{
        # THE ORDER OF TWO ASSIGNMENTS IS THE BEHAVIOUR. A two-way SelectedItem binding clears the
        # choice when the items are replaced, so a choice set first is gone by the time the list
        # arrives - and the window shows a list with nothing highlighted.
        Name = 'suggesting: the choice is set after the list, not before it'
        # RE-ANCHORED 2026-09-25: the sentence about the keys is raised with the caption.
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = '        Offered = rows;
        Raise(nameof(Caption));
        Raise(nameof(Keys));
        Chosen = rows.FirstOrDefault(row => string.Equals(row.Word, kept, StringComparison.Ordinal)) ?? rows[0];'
        To = '        Chosen = rows.FirstOrDefault(row => string.Equals(row.Word, kept, StringComparison.Ordinal)) ?? rows[0];
        Offered = rows;
        Raise(nameof(Caption));
        Raise(nameof(Keys));'
        Test = 'SuggestingTests.The_choice_is_set_after_the_list_so_that_a_two_way_binding_cannot_clear_it'
    }
    @{
        # WRAP AT BOTH ENDS, so Up on the first row reaches the last. A clamp is the same list
        # with two dead keys in it.
        Name = 'suggesting: Down and Up wrap at both ends'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = 'Chosen = _offered[((at + by) % count + count) % count];'
        To = 'Chosen = _offered[Math.Clamp(at + by, 0, count - 1)];'
        Test = 'SuggestingTests.Down_and_Up_move_the_choice_and_wrap_at_both_ends'
    }
    @{
        # ENTER TAKES THE ROW AND THE LIST GOES. Without the close the row is written and the
        # list stays open over it, offering the same row again.
        Name = 'suggesting: taking a row closes the list'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = '        var taken = _chosen ?? _offered[0];

        Close();'
        To = '        var taken = _chosen ?? _offered[0];'
        Test = 'SuggestingTests.Taking_returns_the_chosen_row_and_closes_the_list'
    }
    @{
        # THE POPUP BINDS IsOpen, AND IsOpen HAS NO FIELD. Without the notification the list
        # fills and nothing on screen opens - the same shape as backlog 19, a binding that fails
        # without reddening anything.
        Name = 'suggesting: opening the list is announced'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = '        if (!wasOpen)
        {
            Raise(nameof(IsOpen));
        }'
        To = ''
        Test = 'SuggestingTests.Opening_and_closing_are_announced_so_the_popup_can_follow'
    }
    @{
        # GUI RULE 3: NOTHING JUMPS ON A REFRESH. A second click into an empty box rebuilt the six
        # rows under the pointer and sent the highlight back to the first one.
        Name = 'suggesting: the same list offered again is left alone'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = '        if (rows.SequenceEqual(_offered))
        {
            return;
        }'
        To = ''
        Test = 'SuggestingTests.The_same_list_offered_again_is_left_alone'
    }
    @{
        # DECISION 3: A VALUE WEARS ITS CHIP'S LABEL, the same word somebody sees in the row above.
        # The chip's own value is the word already in the first column, said twice.
        Name = 'suggesting: a value carries the label of its chip'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = '?.Label ?? string.Empty'
        To = '?.Value ?? string.Empty'
        Test = 'SuggestingTests.A_value_carries_the_label_of_its_chip_and_nothing_where_there_is_no_chip'
    }
    @{
        # A CHIP THAT EXCLUDES A VALUE SAYS THE OPPOSITE OF WRITING IT. Its label beside the value
        # would tell somebody that typing `driver` hides drivers.
        Name = 'suggesting: a chip that excludes a value lends it no label'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = '.FirstOrDefault(chip => !chip.Negated'
        To = '.FirstOrDefault(chip => chip.Field.Length >= 0'
        Test = 'SuggestingTests.A_value_carries_the_label_of_its_chip_and_nothing_where_there_is_no_chip'
    }
    @{
        # DECISION 11: A FIELD CARRIES ITS OWN SENTENCE, and four of them say that they cost
        # seconds. This is a list of twenty-one bare names with nothing beside them.
        Name = 'suggesting: a field carries its own sentence'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = 'QueryCompletionKind.Field => Sentence(Fields, completion.Word),'
        To = 'QueryCompletionKind.Field => string.Empty,'
        Test = 'SuggestingTests.A_field_carries_its_own_sentence_and_the_four_that_cost_a_second_reading_say_so'
    }
    @{
        # EVERY FIELD OF THE LANGUAGE HAS A ROW IN THE TABLE. A row taken out is a field whose
        # key would be assembled at run time - or, as here, a field with no sentence at all.
        Name = 'suggesting: every field of the language has a sentence beside it'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = '        new Meaning("mismatch", "gui.suggest.field.mismatch")'
        To = '        new Meaning("mismatch", "gui.suggest.field.mismatch"),
        new Meaning("mismatched", "gui.suggest.field.mismatch")'
        Test = 'SuggestionGuards.Every_field_of_the_language_has_a_sentence_beside_it_and_no_sentence_names_a_field_that_is_not_there'
    }
    @{
        # THE SENTENCE UNDER THE LIST IS THE ONE THE LANGUAGE FILE HOLDS. Empty, the list stands
        # without its keys and the key stands in the language file without a screen.
        #
        # RE-ANCHORED 2026-09-25: two sentences since Tab writes a word - the one for the words
        # names Tab, the one for the questions does not. The mutation says the questions' sentence
        # over a list of words, so Tab is never mentioned where it writes.
        Name = 'suggesting: the sentence about the keys stands under the list'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = 'public string Keys => Texts.Of(_questions ? QuestionsKeys : WordsKeys);'
        To = 'public string Keys => Texts.Of(QuestionsKeys);'
        Test = 'SuggestingTypingTests.The_sentence_about_the_keys_names_Tab_only_where_Tab_writes'
    }

    # The window half: what a key means, where, and what it does. Section 4.6 of the design.
    @{
        # DOWN MEANS THE LIST UNDER THE BOX. Without it the list opens by typing alone and there
        # is no way to ask for every field on an empty member - decision 4.
        Name = 'suggestions: Down means the list under the box'
        File = 'src\Bws.Gui\Shortcuts.cs'
        From = '            Key.Down => Shortcut.NextSuggestion,'
        To = '            Key.Down => Shortcut.None,'
        Test = 'KeyboardTests.Down_and_Up_mean_the_list_under_the_box'
    }
    @{
        # FROM THE GRID, DOWN STAYS THE GRID'S. Without this line the window takes the arrow keys
        # from a list of eight hundred rows and moves nothing.
        Name = 'suggestions: Down from the grid stays the grid''s'
        File = 'src\Bws.Gui\MainWindow.Keyboard.cs'
        From = 'return (theListPress && !inTheGrid) || theBoxPress ? Shortcut.None : wanted;'
        To = 'return (theListPress && !inTheGrid) ? Shortcut.None : wanted;'
        Test = 'KeyboardTests.Down_belongs_to_the_list_under_the_box_from_the_box_and_to_the_grid_from_the_grid'
    }
    @{
        # ENTER IN THE BOX IS NOBODY'S WHILE THE LIST IS CLOSED - decision 8, and what it was before
        # the list existed. Taken unconditionally, a closed list swallows Enter for nothing.
        Name = 'suggestions: Enter in the box takes a row only while the list is open'
        # RE-ANCHORED 2026-09-25 (review of PR 22): CanTake is IsOpen with no input method composing.
        File = 'src\Bws.Gui\MainWindow.Keyboard.cs'
        From = 'Shortcut.OpenDetails => _model.Suggesting.CanTake ? Shortcut.TakeSuggestion : Shortcut.None,'
        To = 'Shortcut.OpenDetails => Shortcut.TakeSuggestion,'
        Test = 'KeyboardTests.Enter_in_the_box_takes_a_row_only_while_the_list_is_open'
    }
    @{
        # DOWN ON A CLOSED LIST OPENS IT. Without the branch Down does nothing until the person
        # has typed a letter that fits, and the empty-member list of every field is unreachable.
        Name = 'suggestions: Down on a closed list opens it'
        File = 'src\Bws.Gui\MainWindow.Suggesting.cs'
        From = 'Shortcut.NextSuggestion => Announced(list.IsOpen ? list.Next() : list.Ask(box.Text, box.CaretIndex, box.SelectionLength)),'
        To = 'Shortcut.NextSuggestion => Announced(list.Next()),'
        Test = 'KeyboardTests.Down_on_a_closed_list_opens_it_and_moves_through_it_once_open'
    }
    @{
        # ESCAPE CLOSES THE LIST BEFORE ANYTHING ELSE. Last in the chain, the first Escape takes
        # the query somebody is still typing.
        Name = 'suggestions: Escape closes the list before it touches the query'
        File = 'src\Bws.Gui\MainWindow.Keyboard.cs'
        # RE-ANCHORED 2026-09-29: the chain went onto five lines and a run that is going now takes the
        # press first (G-1). What is asserted is unchanged.
        # RE-ANCHORED 2026-10-14: a comparison with a snapshot joined the queue beside the panel, and
        # the two went behind one question to the model, PutAwayAView. What is asserted is unchanged.
        From = '                return _model.Suggesting.Close()
                    || _model.Planned.Busy
                    || _model.Planned.Hide()
                    || _model.PutAwayAView()
                    || _model.ClearQuery();'
        To = '                return _model.Planned.Busy
                    || _model.Planned.Hide()
                    || _model.PutAwayAView()
                    || _model.ClearQuery()
                    || _model.Suggesting.Close();'
        Test = 'KeyboardTests.Escape_closes_the_list_first_and_leaves_the_query_alone'
    }
    @{
        # WRITTEN THROUGH THE SELECTION, NOT BY REPLACING THE TEXT - decision 13. This is the
        # one-line version that loses the rest of the line and the undo history with it.
        Name = 'suggestions: Enter writes the row through the selection and keeps the rest of the line'
        File = 'src\Bws.Gui\MainWindow.Suggesting.cs'
        From = '        box.SelectedText = taken.Written;'
        To = '        box.Text = taken.Written;'
        Test = 'KeyboardTests.Enter_writes_the_chosen_row_through_the_selection_and_puts_the_caret_after_it'
    }
    @{
        # THE CARET LANDS AFTER WHAT WAS WRITTEN, with nothing selected. Without the line the
        # written word stays selected and the next keystroke replaces it.
        Name = 'suggestions: the caret lands after what was written'
        File = 'src\Bws.Gui\MainWindow.Suggesting.cs'
        From = '        box.CaretIndex = start + taken.Written.Length;'
        To = ''
        Test = 'KeyboardTests.Enter_writes_the_chosen_row_through_the_selection_and_puts_the_caret_after_it'
    }
    @{
        # CTRL+F IS AN ARRIVAL. A box that already had the keyboard gets no focus event from it,
        # so without this call an empty box reached by Ctrl+F offers nothing.
        Name = 'suggestions: Ctrl+F is an arrival at the box'
        # Re-anchored 2026-09-23: the caret argument went, because Arrived never read it. And 2026-10-20:
        # Ctrl+F's choice of box left MainWindow.Act for Finding.Begin.
        File = 'src\Bws.Gui\Finding.cs'
        From = '            model.Suggesting.Arrived(list.Box.Text);'
        To = ''
        Test = 'KeyboardTests.Control_F_on_an_empty_box_offers_the_questions_that_fit_the_list_and_on_a_full_one_nothing'
    }
    @{
        # NOT ON THE OVERVIEW SCREEN, where the row holding the box is collapsed. Without the
        # guard Ctrl+F on the first-run screen opens six rows hung off a collapsed target, with
        # nothing under them and nothing but Escape to close them.
        Name = 'suggestions: Ctrl+F on the overview screen opens no list'
        # RE-ANCHORED 2026-10-14: asked as "is the list on screen", because a comparison hides it too.
        # And 2026-10-20 into Finding.Begin, with the rest of what Ctrl+F does.
        File = 'src\Bws.Gui\Finding.cs'
        From = '        if (model.ListOnScreen)'
        To = '        if (model.ListOnScreen || !model.ListOnScreen)'
        Test = 'KeyboardTests.Control_F_on_the_overview_screen_opens_no_list'
    }
    @{
        # A PRESS THAT DID NOTHING IS HANDED BACK. Claimed anyway, Up in the box stops working
        # for the box.
        Name = 'suggestions: a press on the list that does nothing is handed back'
        File = 'src\Bws.Gui\MainWindow.Suggesting.cs'
        From = '            Shortcut.PreviousSuggestion => Announced(list.Previous()),'
        To = '            Shortcut.PreviousSuggestion => Announced(list.Previous()) || true,'
        Test = 'KeyboardTests.A_press_on_the_list_that_does_nothing_is_handed_back'
    }
    @{
        # THE BOX IS WATCHED FROM THE WINDOW. Unwired, a row is written and the list does not
        # follow the new caret - `start:` written and no values under it.
        Name = 'suggestions: the box is watched from the window'
        File = 'src\Bws.Gui\MainWindow.xaml.cs'
        From = '        WatchTheBoxForSuggestions();'
        To = ''
        # RE-POINTED 2026-09-28 (backlog 464): the test of writing a row stayed green without this
        # line since PR 22, because writing a row tells the list itself. MISSED on the gate before 0.3.0.
        Test = 'SuggestingWindowTests.Typing_in_the_box_opens_the_list_and_a_caret_moved_alone_closes_it'
    }
    @{
        # THE CARET IS FOLLOWED AS WELL AS THE TEXT. A caret moving through text nobody changed
        # closes the list, and only this event says the caret moved. RE-POINTED 2026-09-28 (backlog
        # 464): it used to name the test of writing a row, whose last step is a caret move - but
        # since PR 22 writing a row tells the list itself, and that test stayed green without this.
        Name = 'suggestions: the caret is followed as well as the text'
        File = 'src\Bws.Gui\MainWindow.Suggesting.cs'
        From = '        box.SelectionChanged += (_, _) => FollowTheBox();'
        To = ''
        Test = 'SuggestingWindowTests.Typing_in_the_box_opens_the_list_and_a_caret_moved_alone_closes_it'
    }

    # The theme half: what the list looks like and what it must never be. Section 4.3.
    @{
        # `docs/10` TRAP 8, ON THE ITEM. A ListBoxItem is focusable by default, and the first
        # click on one would take the keyboard out of the box and close the list under the pointer.
        Name = 'suggestions: a row of the list cannot take the keyboard'
        File = 'src\Bws.Gui\Themes\Suggestions.xaml'
        From = '        <Setter Property="Focusable" Value="False" />
        <Setter Property="IsTabStop" Value="False" />
        <Setter Property="Padding" Value="{StaticResource PadCell}" />'
        To = '        <Setter Property="IsTabStop" Value="False" />
        <Setter Property="Padding" Value="{StaticResource PadCell}" />'
        Test = 'SuggestionGuards.Nothing_in_the_list_under_the_box_can_take_the_keyboard'
    }
    @{
        # THE ROW IS NAMED FOR ITS WORD. Without the setter a reader from outside gets the
        # record's own ToString, which is not what anybody would type.
        Name = 'suggestions: a row is named for its word'
        File = 'src\Bws.Gui\Themes\Suggestions.xaml'
        From = '        <Setter Property="AutomationProperties.Name" Value="{Binding Word}" />'
        To = ''
        Test = 'SuggestionGuards.A_row_is_named_for_its_word_and_keeps_its_sentence_in_the_automation_tree'
    }
    @{
        # THE CHOSEN ROW WEARS THE SELECTED SURFACE, ASKED OF A PIXEL. The hover brush in its
        # place is the shape trap 6 took: two states, one colour, nobody can tell which is chosen.
        Name = 'suggestions: the chosen row is painted in the selected surface'
        File = 'src\Bws.Gui\Themes\Suggestions.xaml'
        From = '                            <Setter TargetName="Face" Property="Background" Value="{StaticResource SurfaceSelected}" />'
        To = '                            <Setter TargetName="Face" Property="Background" Value="{StaticResource SurfaceHover}" />'
        Test = 'SuggestionGuards.The_chosen_row_is_painted_in_the_selected_surface_and_the_others_are_not'
    }
    @{
        # SELECTION IS DECLARED AFTER HOVER SO THAT IT WINS ON THE ROW THAT IS BOTH. The other way
        # round, pointing at the chosen row takes its colour away.
        Name = 'suggestions: selection is declared after hover so that it wins'
        File = 'src\Bws.Gui\Themes\Suggestions.xaml'
        From = '                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Face" Property="Background" Value="{StaticResource SurfaceHoverOnPanel}" />
                        </Trigger>
                        <Trigger Property="IsSelected" Value="True">
                            <Setter TargetName="Face" Property="Background" Value="{StaticResource SurfaceSelected}" />
                        </Trigger>'
        To = '                        <Trigger Property="IsSelected" Value="True">
                            <Setter TargetName="Face" Property="Background" Value="{StaticResource SurfaceSelected}" />
                        </Trigger>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Face" Property="Background" Value="{StaticResource SurfaceHoverOnPanel}" />
                        </Trigger>'
        Test = 'SuggestionGuards.A_row_under_the_pointer_wears_the_hover_surface_by_its_own_trigger_and_selection_wins_over_it'
    }
    @{
        # `docs/10` TRAP 11: A TRANSPARENT BRUSH TAKES THE POINTER AND NO BRUSH DOES NOT. Without
        # it hover works over the letters only, and a click between two words lands on nothing.
        Name = 'suggestions: the face of a row takes the pointer'
        File = 'src\Bws.Gui\Themes\Suggestions.xaml'
        From = '                            Background="{x:Static media:Brushes.Transparent}"'
        To = ''
        Test = 'SuggestionGuards.The_list_is_capped_at_the_height_of_a_menu_and_the_row_face_takes_the_pointer'
    }
    @{
        # ONE DECISION ABOUT HOW TALL A FLOATING LIST MAY BE, written once in HeightMenu - decision
        # 6. Without the cap twenty-one rows stand as tall as they like.
        Name = 'suggestions: the list is capped at the height of a menu'
        File = 'src\Bws.Gui\Themes\Suggestions.xaml'
        From = '        <Setter Property="MaxHeight" Value="{StaticResource HeightMenu}" />'
        To = ''
        Test = 'SuggestionGuards.The_list_is_capped_at_the_height_of_a_menu_and_the_row_face_takes_the_pointer'
    }
    @{
        # THE LIST HAS A NAME OF ITS OWN FOR AUTOMATION, so a probe reads it without reading the
        # box. The box keeps QueryBox, which four probes look for and ten write to.
        Name = 'suggestions: the list has an automation id of its own'
        File = 'src\Bws.Gui\SearchRow.xaml'
        From = '                                 AutomationProperties.AutomationId="Suggestions"'
        To = ''
        Test = 'SuggestionGuards.The_box_keeps_the_automation_id_every_probe_looks_for_and_the_list_has_one_of_its_own'
    }

    # ---- The details panel drawn again, 2026-09-16 - docs/PROJEKT-SZCZEGOLY-20260916.md ----
    #
    # The panel's values were black on #202020 for thirty-four days, because their only style was
    # written in the view, based on the implicit TextBlock style, and named no colour - and the
    # guard for exactly that read the theme, not the views. Six entries: the two guards that now
    # cover both doors, and the four claims the redesign added to the model.
    @{
        # THE GUARD THAT COVERS THE THEME DOOR. A text style in a theme file without a Foreground is
        # drawn in whatever contains it. Taking the colour off the value style is the fault the
        # owner reported, moved one file over.
        Name = 'details: the value style in the theme names its colour'
        File = 'src\Bws.Gui\Themes\Details.xaml'
        # Since 2026-09-16 the colour comes from BodyText through BasedOn, so the fault is the
        # BasedOn taken off - and the guard follows BasedOn since the same day, or the style
        # would have read as colourless before and after alike.
        From = '<Style x:Key="DetailValueText" TargetType="TextBlock" BasedOn="{StaticResource BodyText}">'
        To = '<Style x:Key="DetailValueText" TargetType="TextBlock">'
        Test = 'ContrastGuards.Every_text_style_in_the_theme_names_the_colour_it_is_drawn_in'
    }
    @{
        # THE GUARD THAT COVERS THE VIEW DOOR - the one the fault actually came through. This
        # puts back, in the panel's head, the exact shape the values had for thirty-four days: a
        # style on the implicit one with a trigger and no colour.
        Name = 'details: no view draws text on the implicit style without a colour'
        File = 'src\Bws.Gui\DetailsView.xaml'
        From = '                    <TextBlock AutomationProperties.AutomationId="detailsName"'
        To = '                    <TextBlock><TextBlock.Style><Style TargetType="TextBlock" BasedOn="{StaticResource {x:Type TextBlock}}"><Style.Triggers><DataTrigger Binding="{Binding Gone}" Value="True"><Setter Property="FontFamily" Value="{StaticResource TextFaceFixed}" /></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock>' + [char]10 + '                    <TextBlock AutomationProperties.AutomationId="detailsName"'
        Test = 'ContrastGuards.No_view_draws_text_on_the_implicit_style_without_naming_a_colour'
    }
    @{
        # Nothing is a word, not a blank - backlog 367. Mutated, the line draws the cell's blank
        # and the panel is back to "Account" followed by nothing.
        Name = 'details: a field with nothing in it says none'
        File = 'src\Bws.Gui\ViewModels\Details.cs'
        # Re-anchored 2026-09-24: a line being read says so first, and the word for nothing after it.
        From = '        Shown = reading ?? (value.Length == 0 ? Texts.Of("gui.details.none") : value);'
        To = '        Shown = reading ?? value;'
        Test = 'DetailsGuards.A_field_with_genuinely_nothing_in_it_says_none_rather_than_showing_a_blank'
    }
    @{
        # A column needing a family without an outcome draws its "not read" as an answer. One of
        # the six loses it; the guard must name it.
        Name = 'details: every second-phase column says how its reading went'
        File = 'src\Bws.Gui\ViewModels\Columns.cs'
        From = '            Reads = entry => CellFaces.Say(entry.FileVersion, value => value),' + [char]10 + '            Outcome = entry => entry.FileVersion.Outcome'
        To = '            Reads = entry => CellFaces.Say(entry.FileVersion, value => value)'
        Test = 'ColumnFamilyGuards.Every_column_fed_by_the_second_phase_says_how_its_reading_went'
    }
    @{
        # The opening is what scrolls the panel to the top. Not raised, the view keeps the offset
        # of the previous entry - the owner's own capture of 2026-09-16.
        Name = 'details: opening the panel says so'
        File = 'src\Bws.Gui\ViewModels\Chosen.cs'
        # Read rather than deleted, because an event nobody raises is CS0067 and warnings are
        # errors here - the entry came back BROKEN the first time, and BROKEN proves nothing.
        From = '        Opened?.Invoke(this, EventArgs.Empty);'
        To = '        _ = Opened;'
        Test = 'ChosenGuards.Opening_the_panel_says_so_and_a_refresh_of_the_row_does_not'
    }
    @{
        # The ceiling under the title is two lines of the heading size. At 30 it is one line and
        # the second is trimmed away - which is what 48 did on the first photograph, by a fraction
        # of a point.
        Name = 'details: the title ceiling is two lines of the heading size'
        File = 'src\Bws.Gui\Themes\Values.xaml'
        From = '    <system:Double x:Key="HeightDetailTitle">56</system:Double>'
        To = '    <system:Double x:Key="HeightDetailTitle">30</system:Double>'
        Test = 'TypeScaleGuards.The_details_title_ceiling_is_two_lines_of_the_heading_size_and_not_three'
    },
    @{
        # THE OWNER'S FIVE REMARKS OF 2026-09-16, docs/PROJEKT-PIEC-UWAG-20260916.md. The first:
        # nothing typed into the box of seconds arrived, on a start plan and on a restart plan
        # alike. The progress sentence shares the box's grid cell, is declared after it, and a
        # TextBlock with nothing in it still takes the pointer over its whole rectangle - so every
        # click landed on an empty sentence and the box never got the keyboard. The fix is the
        # sentence leaving when it has nothing to say. This entry puts it back over the box.
        Name = 'waiting: an empty progress sentence lying over the box of seconds is refused'
        # RE-ANCHORED 2026-09-30, AND THE MUTANT GREW: the sentence moved to the row under the
        # buttons (backlog 497), so two things now keep it off the box - its row and its leaving when
        # empty. Either one alone would leave this mutant alive, so it puts back the whole old shape.
        File = 'src\Bws.Gui\PlanFooter.xaml'
        From = '                <TextBlock Grid.Column="0"
                           Grid.ColumnSpan="4"
                           Grid.Row="1"
                           x:Name="PlanProgress"
                           AutomationProperties.AutomationId="planProgress"
                           Text="{Binding Underway.Progress}"
                           TextWrapping="Wrap"
                           Visibility="{Binding Underway.HasProgress, Converter={StaticResource WhenTrue}}"
                           Style="{StaticResource SubduedText}" />'
        To = '                <TextBlock Grid.Column="0"
                           Grid.Row="0"
                           x:Name="PlanProgress"
                           AutomationProperties.AutomationId="planProgress"
                           Text="{Binding Underway.Progress}"
                           TextWrapping="Wrap"
                           Style="{StaticResource SubduedText}" />'
        Test = 'WaitingBoxGuards.The_pointer_lands_on_the_box_of_seconds_and_not_on_something_lying_over_it'
    },
    @{
        # The second remark: the sentence saying a stop takes the machine down stood in "Worth
        # knowing", white and ordinary, three sections above the box asking for the name. Since
        # 2026-09-16 it stands in the footer over the box, red and semibold, and has left "Worth
        # knowing" - one red block, not two reports of one danger. Inverting the ask makes the
        # block and the box appear exactly when there is nothing heavy on the plan.
        Name = 'critical entry: the box and its reason appear only when something is heavy'
        File = 'src\Bws.Gui\ViewModels\Planned.Forcing.cs'
        From = '        && plan.Warnings.Any(Heavy);'
        To = '        && !plan.Warnings.Any(Heavy);'
        Test = 'ForcedStopViewGuards.The_reason_for_the_name_stands_over_the_box_in_the_danger_style_and_nowhere_else'
    },
    @{
        # And the same sentence left in "Worth knowing" as well as in the footer. The guard reads
        # both places and asks that the heavy sentence is in exactly one of them.
        Name = 'critical entry: the heavy sentence is said in the footer and not twice'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        # RE-ANCHORED 2026-09-24: the warnings became lines that can carry the offer to stop.
        # Re-anchored 2026-09-28: the lines are built once per plan and state of the offer (G-7).
        # Re-anchored 2026-09-30: PlanWarningLine became PlanSentence, which asks the plan's kind (W-4).
        From = 'PlanSentence.Of([.. plan.Warnings.Where(warning => !Heavy(warning))], plan.Action.Kind, offering)'
        To = 'PlanSentence.Of([.. plan.Warnings], plan.Action.Kind, offering)'
        Test = 'ForcedStopViewGuards.The_reason_for_the_name_stands_over_the_box_in_the_danger_style_and_nowhere_else'
    },
    @{
        # The third remark: five commands, five copy buttons, and no way to take them all at
        # once. "Copy all" stands over the section when there are several - over one it would be
        # two buttons for one thing.
        Name = 'plan panel: copy all stands over one command as well'
        # Re-anchored 2026-09-29: the question moved to CommandBlock, for both blocks at once (W6).
        File = 'src\Bws.Gui\ViewModels\CommandBlock.cs'
        From = '    public bool Several => Lines.Count > 1;'
        To = '    public bool Several => Lines.Count > 0;'
        Test = 'PlanViewGuards.Copy_all_stands_over_several_commands_with_all_of_them_on_it_and_not_over_one'
    },
    @{
        # What "Copy all" carries is one command per line, so a paste into a terminal runs them
        # one after another. Joined with a space they would be one line the terminal cannot run.
        Name = 'plan panel: copy all joins the commands on one line'
        # Re-anchored 2026-09-29: the join moved to CommandBlock, made once per block (W6).
        File = 'src\Bws.Gui\ViewModels\CommandBlock.cs'
        From = '        All = string.Join(Environment.NewLine, lines);'
        To = '        All = string.Join(" ", lines);'
        Test = 'PlanViewGuards.Copy_all_stands_over_several_commands_with_all_of_them_on_it_and_not_over_one'
    },
    @{
        # THE SECOND ROUND OF THE SAME DAY, four remarks over the plan sheet. "Copy all" back out of
        # the box the lines' buttons stand in - a right margin found by eye would put the two edges
        # apart at the first change of the box, and this is what says so.
        Name = 'plan panel: copy all stands outside the box the lines stand in'
        File = 'src\Bws.Gui\Themes\PlanLines.xaml'
        From = '    <Style x:Key="PlanCommandGhost" TargetType="Border" BasedOn="{StaticResource PlanCommandBox}">
        <Setter Property="Background" Value="{x:Null}" />'
        To = '    <Style x:Key="PlanCommandGhost" TargetType="Border" BasedOn="{StaticResource PlanCommandBox}">
        <Setter Property="Padding" Value="{StaticResource MarginNone}" />
        <Setter Property="Background" Value="{x:Null}" />'
        Test = 'PlanBodyGuards.Copy_all_ends_where_the_copy_buttons_of_the_lines_end'
    },
    @{
        # A command's own viewer keeping the wheel, which is what every ScrollViewer does unless told
        # not to - the sheet under twenty boxes stops scrolling and nothing else notices.
        Name = 'plan panel: the wheel over a command stays in the command'
        File = 'src\Bws.Gui\PlanView.xaml'
        From = '                          PreviewMouseWheel="WheelOverTheBody"
'
        To = ''
        Test = 'PlanBodyGuards.The_wheel_over_a_command_scrolls_the_sheet_and_not_the_command'
    },
    @{
        # The error adorner drawing one side instead of four: the bottom edge goes back under the
        # library's own line, which is the picture the owner sent. MOVED WITH THE STYLE 2026-09-23 -
        # the template is ReportingBox's in Controls.xaml, and the search box wears it too.
        Name = 'waiting: the problem edge stops short of the bottom of the box'
        File = 'src\Bws.Gui\Themes\Controls.xaml'
        From = '                    <Border BorderBrush="{StaticResource MeaningRejected}"
                            BorderThickness="{StaticResource BorderAllSides}"
                            CornerRadius="{DynamicResource ControlCornerRadius}"'
        To = '                    <Border BorderBrush="{StaticResource MeaningRejected}"
                            BorderThickness="{StaticResource BorderPanelEdge}"
                            CornerRadius="{DynamicResource ControlCornerRadius}"'
        Test = 'WaitingBoxGuards.The_wrong_edge_is_the_error_template_on_all_four_sides'
    },
    @{
        # THE FIFTH REMARK, the list under the search box, section 5 of the same document. The
        # examples on an empty box lead with their sentence, a completion with its word - D1.
        Name = 'suggestions: a question leads with its word like a completion'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = '0..text.Length, example.Query, IsQuestion: true))];'
        To = '0..text.Length, example.Query, IsQuestion: false))];'
        Test = 'SuggestingWordingTests.A_question_leads_with_its_sentence_and_a_completion_leads_with_its_word'
    },
    @{
        # The caption over the examples on an empty box saying "What can go here" - the sentence
        # for completions - because the model forgot which list it opened.
        Name = 'suggestions: the caption over the questions says they are completions'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = '            _questions = true;
            Offer(Examples(text ?? string.Empty));'
        To = '            _questions = false;
            Offer(Examples(text ?? string.Empty));'
        Test = 'SuggestingWordingTests.The_caption_says_which_list_is_open_and_nothing_while_none_is'
    },
    @{
        # The fourth remark: the Filters button answered neither the pointer nor the finger while
        # the filters were open - only its arrow moved. The whole fault was ORDER: a style trigger
        # on IsChecked painted the open button in its resting brushes and outranked the library's
        # template triggers for hover and press, and the filters open by default. The named style
        # puts hover and press AFTER the checked trigger, because the later trigger wins. Swapping
        # the first two back is the fault, and the build, the catalogue and every other guard are
        # blind to it.
        Name = 'filters: the open button no longer answers the pointer'
        File = 'src\Bws.Gui\Themes\Chips.xaml'
        From = '            <Trigger Property="IsChecked" Value="True">
                <Setter Property="Background" Value="{DynamicResource ToggleButtonBackground}" />
                <Setter Property="BorderBrush" Value="{DynamicResource ControlElevationBorderBrush}" />
                <Setter Property="Foreground" Value="{DynamicResource ToggleButtonForeground}" />
            </Trigger>

            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="{DynamicResource ToggleButtonBackgroundPointerOver}" />
            </Trigger>'
        To = '            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="{DynamicResource ToggleButtonBackgroundPointerOver}" />
            </Trigger>

            <Trigger Property="IsChecked" Value="True">
                <Setter Property="Background" Value="{DynamicResource ToggleButtonBackground}" />
                <Setter Property="BorderBrush" Value="{DynamicResource ControlElevationBorderBrush}" />
                <Setter Property="Foreground" Value="{DynamicResource ToggleButtonForeground}" />
            </Trigger>'
        Test = 'ChipGuards.The_filters_toggle_answers_the_pointer_after_it_has_answered_being_open'
    },

    # THE FORCING VERBS IN THE WINDOW, 2026-09-16 - backlog 374, the owner's decision. Force stop and
    # Force restart in the action bar and the row menu, opening the plan `bws kill` builds. Seven
    # entries: the one rule the two verbs have that the others do not (one entry at a time, `docs/
    # ANALIZA-FORCE` 15.6), on both doors and on the count the bar is told - and the core repair the
    # verb uncovered, a run verdict that counted steps rather than entries.
    @{
        # The bar lights the two forcing buttons over exactly one entry. Over any would let Ctrl+A
        # and a press open a plan ending one process twice, which the bulk plan cannot see.
        Name = 'forcing verbs: the bar lights them over one entry and not over any'
        File = 'src\Bws.Gui\ActionBar.xaml.cs'
        From = 'var one = entries == 1 && !onlyDrivers;'
        To = 'var one = entries > 0 && !onlyDrivers;'
        Test = 'ForcingVerbGuards.Several_entries_leave_the_forcing_verbs_off_and_one_entry_turns_them_on'
    },
    @{
        # The refusal itself, where both doors meet. The greyed button is a courtesy; a menu item
        # has no button to grey, so a Preview that built the plan anyway would open the door the
        # bar shut, one right click away.
        Name = 'forcing verbs: an ask over several entries is refused before a plan exists'
        File = 'src\Bws.Gui\MainWindow.Menu.cs'
        From = 'if (kind is ActionKind.ForceStop or ActionKind.ForceRestart && names.Count > 1)'
        To = 'if (kind is ActionKind.ForceStop or ActionKind.ForceRestart && names.Count > 1000)'
        Test = 'ForcingVerbGuards.A_forcing_ask_over_two_entries_is_refused_before_a_plan_exists'
    },
    @{
        # The bar is told ENTRIES, counted by the method that builds every plan. Told rows, a
        # folded per-user family of twenty-four lights the forcing verbs as one, and the sheet
        # behind the press is a plan over all twenty-four.
        Name = 'forcing verbs: the bar is told entries rather than rows'
        File = 'src\Bws.Gui\MainWindow.Menu.cs'
        From = 'private int PickedEntries() => Everything(PickedRows()).Count;'
        To = 'private int PickedEntries() => PickedRows().Count;'
        Test = 'ForcingVerbGuards.A_folded_family_counts_as_many_entries_to_the_bar'
    },
    @{
        # A button wearing the forcing word and wired to the ordinary stop opens a plan that will
        # never end the process, under a heading that says it would.
        Name = 'forcing verbs: the force stop button asks for a forced stop'
        File = 'src\Bws.Gui\ActionBar.xaml.cs'
        From = 'PreviewRequest?.Invoke(this, new PreviewAsked(ActionKind.ForceStop));'
        To = 'PreviewRequest?.Invoke(this, new PreviewAsked(ActionKind.Stop));'
        Test = 'ForcingVerbGuards.Pressing_a_forcing_verb_opens_a_plan_that_ends_the_process_and_nothing_else'
    },
    @{
        # The same fault on the other door. The shape test sees the key and nothing behind it.
        Name = 'row menu: the force stop item asks for a forced stop'
        File = 'src\Bws.Gui\RowMenu.cs'
        From = '() => window.Preview(ActionKind.ForceStop))'
        To = '() => window.Preview(ActionKind.Stop))'
        Test = 'RowMenuGuards.The_forcing_items_open_their_own_plan_over_one_row_and_refuse_over_two'
    },
    @{
        # The core repair the verb uncovered. Counted by step, the shape the forcing plan is built
        # for - polite stop timed out, process ended, entry in Stopped - read as incomplete: exit
        # code 3 and "not where you asked", measured on the throwaway machine before the change.
        # Counting every step of the group again is the old verdict in the new clothes.
        Name = 'forced stop: a run whose escalation arrived is a run that worked'
        File = 'src\Bws.Core\Planning\PlanRun.cs'
        From = '.All(same => same.Last().Arrived);'
        To = '.All(same => same.All(result => result.Arrived));'
        Test = 'EscalationRunTests.A_run_whose_escalation_arrived_is_a_run_that_worked'
    },
    @{
        # The document takes its verdict from the run rather than working one out of the steps.
        # A document counting steps on its own would say false over the same run the core calls
        # complete, and a script reading it would never learn the entry had arrived.
        Name = 'forced stop: the document says completed when the kill arrived'
        File = 'src\Bws.Cli\PlanJson.cs'
        From = 'Completed = run?.Completed,'
        To = 'Completed = run?.Results.All(result => result.Arrived),'
        Test = 'PlanDocumentGuards.A_forced_stop_whose_kill_arrived_is_written_as_completed'
    },
    @{
        # The sheet's third cell for the bar is the one over several. A cell that showed "over one"
        # again would pass every count the catalogue keeps and show a state the window never has.
        Name = 'catalogue: the action bar is shown over several as well as over one and none'
        File = 'src\Bws.Gui\ViewModels\Catalogue.Views.cs'
        From = 'data: Bar(1, needsRights: true), empty: Bar(0), wrong: Bar(1, onlyDrivers: true), extreme: Bar(2)),'
        To = 'data: Bar(1, needsRights: true), empty: Bar(0), wrong: Bar(1, onlyDrivers: true), extreme: Bar(1)),'
        Test = 'CatalogueViewGuards.The_action_bar_is_shown_over_one_entry_over_none_and_over_several'
    },

    # ---- UX-GUI-003, 2026-09-23: a write that cannot work says so before the plan ----
    @{
        # Drivers alone leave nothing to do - the core refuses a driver every ask. The mutation
        # forgets them, and the bar lights six buttons over a driver again, as the audit measured.
        Name = 'drivers: drivers alone turn the write buttons off'
        File = 'src\Bws.Gui\ActionBar.xaml.cs'
        From = 'var anything = entries > 0 && !onlyDrivers;'
        To = 'var anything = entries > 0;'
        Test = 'DriverPickGuards.Drivers_alone_turn_the_write_buttons_off_and_say_why'
    },
    @{
        # The door the row menu shares with the bar. The mutation asks about nothing, so a right
        # click over a driver opens the plan of nothing the bar no longer offers.
        Name = 'drivers: a plan over drivers alone is refused before it exists'
        File = 'src\Bws.Gui\MainWindow.Menu.cs'
        From = '        if (OnlyDrivers(picked))'
        To = '        if (OnlyDrivers([]))'
        Test = 'DriverPickGuards.Asking_for_a_plan_over_drivers_alone_opens_none_and_says_why'
    },
    @{
        # The rights sentence only over a plan that could run. The mutation asks the session alone,
        # and a plan of refusals says in red that a restart would carry it out.
        Name = 'plan sheet: the rights sentence stands only over something to carry out'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        From = '    public string Blocked => Showing && !Elevated && _plan is { IsRunnable: true }'
        To = '    public string Blocked => Showing && !Elevated'
        Test = 'RefusedPlanTests.A_plan_with_nothing_to_carry_out_does_not_advise_a_restart_as_administrator'
    },
    @{
        # Nothing to run is named on the grey button before the session's rights. The mutation
        # drops that reason, and the button sends somebody to restart for nothing.
        Name = 'plan sheet: the grey button names nothing to run before rights'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        From = '        : _plan is not { IsRunnable: true } ? Texts.Of("gui.plan.blocked.nothingToRun")'
        To = '        : false ? Texts.Of("gui.plan.blocked.nothingToRun")'
        Test = 'RefusedPlanTests.A_plan_with_nothing_to_carry_out_does_not_advise_a_restart_as_administrator'
    },
    @{
        # The steps block goes with its steps. The mutation points the trigger at a count that
        # never happens, and a plan of refusals opens on "In this order" over nothing again.
        Name = 'plan sheet: the block of steps goes when there are none'
        File = 'src\Bws.Gui\Themes\Plan.xaml'
        From = '<DataTrigger Binding="{Binding Steps.Count}" Value="0">'
        To = '<DataTrigger Binding="{Binding Steps.Count}" Value="-1">'
        Test = 'RefusedPlanTests.The_sheet_opens_on_the_refusals_when_nothing_can_be_carried_out'
    },
    @{
        # And the sheet wears that style - a block on the plain one keeps its heading over nothing.
        Name = 'plan sheet: the block of steps wears the style that hides it'
        File = 'src\Bws.Gui\PlanView.xaml'
        From = '<Border Style="{StaticResource PlanStepsBlock}">'
        To = '<Border Style="{StaticResource PanelBlock}">'
        Test = 'RefusedPlanTests.The_sheet_opens_on_the_refusals_when_nothing_can_be_carried_out'
    },
    @{
        # UX-GUI-004 (a): the way out beside the sentence on the sheet, since the one at the foot of
        # the window is under the dimming. The mutation hides it and leaves the sentence alone.
        # NOT GUARDED, said here: the Click wiring in MainWindow - a press is a UAC prompt.
        Name = 'rights: the way out stands beside the rights sentence on the plan sheet'
        File = 'src\Bws.Gui\PlanFooter.xaml'
        From = 'Style="{StaticResource ElevateOffer}" />'
        To = 'Style="{StaticResource ElevateOffer}" Visibility="Collapsed" />'
        Test = 'RefusedPlanTests.The_way_out_stands_beside_the_sentence_about_rights_on_the_sheet'
    },
    @{
        # UX-GUI-004 (b): a shield on the six write buttons while the session has no rights, so
        # the window says before the plan what it said only under it. The mutation never shows it.
        Name = 'rights: the shield shows on the write buttons of a session without rights'
        File = 'src\Bws.Gui\Themes\Actions.xaml'
        From = '<Setter Property="Visibility" Value="Visible" />'
        To = '<Setter Property="Visibility" Value="Collapsed" />'
        Test = 'RightsMarkGuards.The_six_write_buttons_wear_a_shield_while_the_session_cannot_carry_their_plans_out'
    },
    @{
        # The shield eats the button's left padding rather than adding its width - without that the
        # nine buttons that cannot wrap push Overview out of the narrowest window (measured 60 over).
        Name = 'rights: the shield takes the left padding so the bar still fits'
        File = 'src\Bws.Gui\Themes\Actions.xaml'
        From = '<Setter Property="Padding" Value="{StaticResource PadButtonWithMark}" />'
        To = '<Setter Property="Padding" Value="11,5,11,6" />'
        Test = 'ActionBarGuards.No_action_button_stands_outside_the_bar_at_the_narrowest_window'
    },
    @{
        # The window tells the bar. The mutation tells it nothing, and no session ever sees a shield.
        Name = 'rights: the window tells the bar whether the session has rights'
        File = 'src\Bws.Gui\MainWindow.xaml.cs'
        From = '        Actions.NeedsRights = _model.Says.NotElevated;'
        To = '        Actions.NeedsRights = false;'
        Test = 'RightsMarkGuards.The_window_tells_the_bar_whether_this_session_has_rights'
    },
    @{
        # The shield in words on a live button, for a screen reader that gets no node for the mark.
        Name = 'rights: a live write button says in words that carrying it out needs rights'
        File = 'src\Bws.Gui\ActionBar.xaml.cs'
        From = '        button.ToolTip = on && NeedsRights ? saying + " " + Texts.Of("gui.action.needsRights") : saying;'
        To = '        button.ToolTip = saying;'
        Test = 'RightsMarkGuards.The_six_write_buttons_wear_a_shield_while_the_session_cannot_carry_their_plans_out'
    },
    # ---- UX-GUI-004 (c), 2026-09-23: the restart as administrator opens where the old window was ----
    #
    # NOT GUARDED, said here: that Elevation.Restart passes the argument and that the window's Loaded
    # reaches TakeOverAsync through App - a press is a UAC prompt, and the test host is not App.
    @{
        # An undefined number parses and prints as itself, so only IsDefined refuses it.
        Name = 'hand-over: an enum value that is not a member is refused'
        File = 'src\Bws.Gui\ViewModels\HandOver.cs'
        From = '            && Enum.IsDefined(value)'
        To = '            && true'
        Test = 'HandOverTests.A_hand_over_this_program_could_not_have_written_is_refused_whole'
    },
    @{
        # "Services, Drivers" parses to a member on an enum without flags - only the round trip refuses it.
        Name = 'hand-over: an enum value has to be written exactly as its member'
        File = 'src\Bws.Gui\ViewModels\HandOver.cs'
        From = '            && string.Equals(value.ToString(), text, StringComparison.Ordinal);'
        To = '            && true;'
        Test = 'HandOverTests.A_hand_over_this_program_could_not_have_written_is_refused_whole'
    },
    @{
        # The cap before decoding - a hostile argument of any length would otherwise be decoded whole.
        Name = 'hand-over: an argument longer than this program writes is refused unread'
        File = 'src\Bws.Gui\ViewModels\HandOver.cs'
        From = '        if (text.Length is 0 or > LongestArgument || !Base64Url.IsValid(text))'
        To = '        if (text.Length is 0 || !Base64Url.IsValid(text))'
        Test = 'HandOverTests.A_value_longer_than_this_program_writes_is_refused_before_it_is_decoded'
    },
    @{
        # Too many picked to fit: the writer leaves them behind and says so, rather than writing an
        # argument its own reader refuses.
        Name = 'hand-over: a selection too large is left behind rather than overflowing'
        File = 'src\Bws.Gui\ViewModels\HandOver.cs'
        # Re-anchored 2026-10-05 (G-7): every candidate goes through one budget check now.
        From = 'Write(handOver) is { Length: <= LongestArgument } written ? written : null;'
        To = 'Write(handOver);'
        Test = 'HandOverTests.A_selection_too_large_to_carry_is_left_behind_and_says_so'
    },
    @{
        # The open plan is asked again in the new window. The mutation asks nothing.
        Name = 'hand-over: the plan that was open is asked for again'
        File = 'src\Bws.Gui\MainWindow.HandOver.cs'
        # RE-ANCHORED 2026-09-24: the stop riding on a setting crosses the restart too.
        # Re-anchored 2026-09-30: the dependants cross too (W-4).
        From = '            await Preview(kind, carried.To, carried.AlsoStop, carried.Dependents).ConfigureAwait(true);'
        To = '            await Task.CompletedTask.ConfigureAwait(true);'
        Test = 'HandOverGuards.The_list_the_search_the_picked_rows_and_the_open_plan_come_back'
    },
    @{
        # Picked entries no longer in the list are said, not dropped in silence (rule 8).
        Name = 'hand-over: picked entries that did not come back are said'
        File = 'src\Bws.Gui\MainWindow.HandOver.cs'
        From = '        if (gone > 0)'
        To = '        if (gone > 1000)'
        Test = 'HandOverGuards.The_list_the_search_the_picked_rows_and_the_open_plan_come_back'
    },
    @{
        # A refused hand-over is said - the person restarted expecting their work.
        Name = 'hand-over: a refused hand-over is said'
        File = 'src\Bws.Gui\MainWindow.HandOver.cs'
        From = '            _model.Says.AboutTheHandOver(Texts.Of("gui.handOver.refused"));'
        To = '            _ = Texts.Of("gui.handOver.refused");'
        Test = 'HandOverGuards.A_refused_hand_over_is_said_and_changes_nothing'
    },
    @{
        # The window remembers which plan it opened, or it cannot hand the question over.
        Name = 'hand-over: the window remembers the plan it opened'
        File = 'src\Bws.Gui\MainWindow.Menu.cs'
        # RE-ANCHORED 2026-09-24: what the window remembers carries whether the plan also stops.
        # Re-anchored 2026-09-30: and whether it also stops the dependants (W-4).
        # Re-anchored 2026-10-05: and, from a failure, the entry the sheet is about (backlog 504).
        From = '            _asked = (kind, to, alsoStop, dependents, null);'
        To = '            _asked = null;'
        Test = 'HandOverGuards.What_the_window_hands_over_is_what_is_on_its_screen'
    },
    @{
        # Both buttons wear the one named look. The mutation puts the sheet's back on the bare
        # outline, and it loses the words and the tooltip the foot of the window keeps.
        Name = 'rights: the way out on the sheet is the same button as at the foot of the window'
        File = 'src\Bws.Gui\PlanFooter.xaml'
        From = 'Style="{StaticResource ElevateOffer}" />'
        To = 'Style="{StaticResource ElevateAction}" />'
        Test = 'RefusedPlanTests.The_way_out_stands_beside_the_sentence_about_rights_on_the_sheet'
    },

    # ---- Outbound: the ways out of this machine that a System.Net check cannot see ----
    #
    # THE FIRST OF THESE IS THE ONE THAT MATTERS, AND IT IS NOT A MADE-UP BREAK - it puts a real
    # HTTP call into the product. Measured 2026-09-22 with exactly this mutation applied: the
    # build succeeds, CsWin32 emits the P/Invoke, Bws.Core binds winhttp.dll, three of the new
    # guards go red - and LayeringGuards.No_shipped_assembly_reaches_for_the_network, which is
    # the guard that held ADR-19 before that day, passes on all three projects. A WinHTTP call
    # carries no managed type reference at all, so the check that reads type references sees an
    # ordinary native call and says nothing. That measurement is the whole reason these guards
    # exist, and this entry is what stops it quietly ceasing to be true.

    @{
        Name = 'outbound: an HTTP call declared in the product reddens the build'
        File = 'src\Bws.Core\NativeMethods.txt'
        From = 'WIN32_ERROR'
        To = 'WIN32_ERROR' + [char]10 + 'WinHttpOpen'
        Test = 'OutboundGuards.No_shipped_assembly_binds_a_networking_module'
    },
    @{
        # The same line, judged by the guard that fires at the DECLARATION rather than at the
        # built module. Registered separately because the two answer for different halves: this
        # one still speaks when somebody writes the inventory entry and has not built yet.
        Name = 'outbound: the win32 inventory refuses an HTTP name'
        File = 'src\Bws.Core\NativeMethods.txt'
        From = 'WIN32_ERROR'
        To = 'WIN32_ERROR' + [char]10 + 'WinHttpOpen'
        Test = 'OutboundGuards.The_win32_inventory_names_nothing_that_reaches_the_network'
    },
    @{
        # The window has one native call and the same rule applies to it. A second inventory file
        # is exactly the place a check reading only the first would stop covering.
        Name = 'outbound: the window is held to the same inventory rule as the core'
        File = 'src\Bws.Gui\NativeMethods.txt'
        From = 'DWMWINDOWATTRIBUTE'
        To = 'DWMWINDOWATTRIBUTE' + [char]10 + 'WSAStartup'
        Test = 'OutboundGuards.The_win32_inventory_names_nothing_that_reaches_the_network'
    },
    @{
        # The reader, not the register. A metadata walk that stopped collecting modules would
        # leave every assertion above passing over an empty set, which is this project's worst
        # shape of failure: a thing that never ran looks exactly like a thing that found nothing.
        Name = 'outbound: the native module reader is reading something'
        File = 'tests\Bws.Architecture.Tests\AssemblyFacts.cs'
        From = 'if (!import.Module.IsNil)'
        To = 'if (import.Module.IsNil && !import.Module.IsNil)'
        Test = 'OutboundGuards.The_module_reader_is_reading_something'
    },
    @{
        # The list every assembly-reading guard in this project walks. A fourth project under src
        # would be read by none of them and would redden nothing at all.
        Name = 'outbound: the list of shipped projects still matches what is under src'
        File = 'tests\Bws.Architecture.Tests\GuardedAssemblies.cs'
        From = '["Bws.Core", "Bws.Cli", "Bws.Gui"]'
        To = '["Bws.Core", "Bws.Cli"]'
        Test = 'OutboundGuards.The_list_of_shipped_projects_still_matches_what_is_under_src'
    },
    @{
        # The half that reads somebody else's libraries. Pointed at a pattern that matches no file,
        # it would find nothing wrong with four libraries it never opened.
        Name = 'outbound: the scan of what ships beside us finds the libraries'
        File = 'tests\Bws.Architecture.Tests\OutboundRegisters.cs'
        From = 'Directory.EnumerateFiles(folder, "*.dll", SearchOption.TopDirectoryOnly)'
        To = 'Directory.EnumerateFiles(folder, "*.absent", SearchOption.TopDirectoryOnly)'
        Test = 'OutboundGuards.The_scan_of_what_ships_beside_us_found_the_libraries_it_is_meant_to_read'
    },
    @{
        # Back to the substring match that reddened the first run of this guard on SC_MANAGER_CONNECT.
        # The canary holds the register to whole-name matching, because "connect" and "socket" are
        # ordinary words and this repository has already paid once for a mechanical match on one.
        Name = 'outbound: the inventory register matches a name, not a fragment of one'
        File = 'tests\Bws.Architecture.Tests\OutboundRegisters.cs'
        From = 'name => declaration.StartsWith(name, StringComparison.OrdinalIgnoreCase));'
        To = 'name => declaration.Contains(name, StringComparison.OrdinalIgnoreCase));'
        Test = 'OutboundGuards.The_win32_inventory_names_nothing_that_reaches_the_network'
    },

    # ---- Signatures without the network ----
    #
    # The quiet mode stops Windows fetching a certificate mid-listing, and the four entries
    # below hold the part that makes that safe rather than merely silent. A file that IS
    # properly signed can come back non-zero once the chain engine may not go and look, and
    # nothing may turn that into a sentence about somebody's certificate.

    @{
        Name = 'signature: a verdict about the chain is not stated when we refused to look'
        File = 'src\Bws.Core\WindowsBinaryInspector.Verdicts.cs'
        From = '        unchecked((int)0x800B0101) => true,'
        To = '        unchecked((int)0x800B0101) => true,' + [char]10 + '        unchecked((int)0x800B0109) => true,'
        Test = 'SignatureWithoutTheNetworkTests.A_verdict_about_the_chain_becomes_a_refusal_in_the_quiet_mode'
    },
    @{
        # The mode itself. Inverted rather than deleted, so both halves are exercised at once:
        # the quiet mode starts stating chain verdicts and the loud one starts hiding them.
        Name = 'signature: the quiet mode is the one that withholds a chain verdict'
        File = 'src\Bws.Core\WindowsBinaryInspector.Verdicts.cs'
        From = 'if (networkPaths == NetworkPaths.Skip && !SurvivesWithoutTheNetwork(result))'
        To = 'if (networkPaths == NetworkPaths.Follow && !SurvivesWithoutTheNetwork(result))'
        Test = 'SignatureWithoutTheNetworkTests.A_verdict_about_the_chain_becomes_a_refusal_in_the_quiet_mode'
    },
    @{
        # A refusal carries no value, so reading the publisher for one means opening the file a
        # second time to decorate an answer this code is declining to give.
        Name = 'signature: nothing is read in order to be thrown away with a refusal'
        File = 'src\Bws.Core\WindowsBinaryInspector.Verdicts.cs'
        # ANCHOR MOVED 2026-09-23, AND stale-check.ps1 IS WHAT FOUND IT. The line this entry
        # pointed at was one statement on one line until the review on PR #4 split it across
        # three and replaced string.Empty with ManagerTerms.Describe - the repair for a refusal
        # that carried no sentence. The mutation went stale in that same commit and nothing said
        # so until the registry was evaluated against the code.
        From = 'return Reading<BinarySignature>.Denied(' + [char]10 + '                result,' + [char]10 + '                Marshal.GetExceptionForHR(result)?.Message ?? ManagerTerms.Describe(result));'
        To = 'var ignored = publisher(); return Reading<BinarySignature>.Denied(' + [char]10 + '                result,' + [char]10 + '                Marshal.GetExceptionForHR(result)?.Message ?? ignored ?? ManagerTerms.Describe(result));'
        Test = 'SignatureWithoutTheNetworkTests.Nothing_is_read_about_a_signature_this_code_is_refusing_to_report'
    },
    @{
        # THE ONE THAT WOULD BE A DISASTER. Refusing to reach the network may cost an answer and
        # may never produce one - a tool calling an unverified binary trusted would be worse
        # than a tool that said nothing at all. The mutation puts a chain failure on the Trusted
        # arm, which is the shape a hurried fix for a red build would take.
        Name = 'signature: nothing but a zero is ever reported as trusted'
        File = 'src\Bws.Core\WindowsBinaryInspector.Verdicts.cs'
        From = '        unchecked((int)0x800B0109) => SignatureStatus.UntrustedRoot,'
        To = '        unchecked((int)0x800B010A) => SignatureStatus.Trusted,' + [char]10 + '        unchecked((int)0x800B0109) => SignatureStatus.UntrustedRoot,'
        Test = 'SignatureWithoutTheNetworkTests.No_result_but_zero_is_ever_reported_as_trusted'
    },
    @{
        # THE DIRECTION THAT FINDS THINGS. A component we chose is in a project file and hard to
        # forget. One that arrives as somebody else's dependency arrives with no prompt at all,
        # ships, and creates the same licence obligation - WPF-UI.Abstractions is in the register
        # because a person noticed. The mutation is that person not noticing.
        Name = 'register: a package that ships and is not in the register is refused'
        File = 'packaging\components.json'
        From = '      "name": "WPF-UI.Abstractions",'
        To = '      "name": "WPF-UI.Abstractions.NotTheOneThatShips",'
        Test = 'ComponentRegisterGuards.Every_package_that_ships_is_in_the_register'
    },
    @{
        # A pin that stops matching is the whole point of having one. One digit, because a
        # mutation that rewrote the whole hash could be mistaken for a deliberate update.
        Name = 'register: a shipped binary that stops hashing to its pin is refused'
        File = 'packaging\components.json'
        From = '"sha256": "966176695819e39752211df271a53cee7df91f55a5e20ef9bf61fc7220fcaa1a"'
        To = '"sha256": "066176695819e39752211df271a53cee7df91f55a5e20ef9bf61fc7220fcaa1a"'
        Test = 'ComponentRegisterGuards.Every_pinned_binary_still_hashes_to_what_the_register_pins'
    },
    @{
        # THE ORDERING TRAP, AS A MUTATION. Immediate answers before what somebody typed is
        # judged, because help and version are questions about the tool. A verb answered in that
        # position swallows every switch beside it: `bws license --json` prints the notice, exits
        # zero and says nothing about the flag it ignored. Taking NothingWrong out is exactly the
        # shape a hurried reading of this file would produce.
        Name = 'licence: a switch on the wrong verb is not swallowed by the licence answer'
        File = 'src\Bws.Cli\Immediate.cs'
        From = 'if (options.Kind == CommandKind.License && options.NothingWrong)'
        To = 'if (options.Kind == CommandKind.License)'
        Test = 'LicenceCommandTests.No_option_belonging_to_another_verb_can_be_swallowed_by_it'
    },
    @{
        # The one failure the whole file is about: a confident inventory of the OTHER program.
        # There is nothing at runtime to work this out from, so it is a literal - and a literal
        # is exactly what a mutation can move without anything else noticing.
        Name = 'licence: the executable lists ITS OWN components rather than the other package'
        File = 'src\Bws.Cli\Licence.cs'
        From = 'private const string ThisPackage = "cli";'
        To = 'private const string ThisPackage = "gui";'
        Test = 'LicenceCommandTests.It_names_every_component_this_package_ships_and_none_of_the_other_package'
    },
    @{
        # PACKAGE 5 OF THE UX AUDIT, 2026-09-24 - UX-GUI-006 with backlog 231 and 232, and every
        # entry from here to the line saying so is one of its guards. The design is
        # docs/PROJEKT-TYP-STARTU-20260924.md. The first four hold the table the writer and the way
        # back share: if Written and Of stop being each other's inverse, a way back names a setting
        # that does not put the entry back. Which ORDER the writer makes its two calls in is not
        # here and cannot be - WindowsScmControl calls the system directly - and tools/plan-journey
        # holds that half on the throwaway machine.
        Name = 'start setting: manual writes the late flag false, as sc.exe does'
        File = 'src\Bws.Core\Planning\StartSetting.cs'
        From = '        StartSetting.Manual => (StartType.Manual, false),'
        To = '        StartSetting.Manual => (StartType.Manual, true),'
        Test = 'StartSettingTests.Every_setting_writes_the_late_flag_and_only_one_writes_it_true'
    },
    @{
        Name = 'start setting: automatic clears the late flag rather than keeping it'
        File = 'src\Bws.Core\Planning\StartSetting.cs'
        From = '        StartSetting.Automatic => (StartType.Automatic, false),'
        To = '        StartSetting.Automatic => (StartType.Automatic, true),'
        Test = 'StartSettingTests.Every_setting_reads_back_as_itself_through_the_table_the_writer_uses'
    },
    @{
        Name = 'start setting: the late flag on a manual entry is no setting this tool writes'
        File = 'src\Bws.Core\Planning\StartSetting.cs'
        From = '            (StartType.Manual, false) => StartSetting.Manual,'
        To = '            (StartType.Manual, _) => StartSetting.Manual,'
        Test = 'StartSettingTests.Nothing_is_named_for_a_setting_this_tool_cannot_write'
    },
    @{
        Name = 'start setting: an absent late flag is known, and it is not late'
        File = 'src\Bws.Core\Planning\StartSetting.cs'
        From = '|| entry.DelayedAuto.Outcome is not (ReadOutcome.Present or ReadOutcome.Absent))'
        To = '|| entry.DelayedAuto.Outcome is not ReadOutcome.Present)'
        Test = 'StartSettingTests.Nothing_is_named_for_a_setting_this_tool_cannot_write'
    },
    @{
        Name = 'way back: a delayed automatic entry is put back by name (backlog 232)'
        File = 'src\Bws.Core\Planning\PlanSteps.cs'
        From = 'internal static StartSetting? WayBackTo(ScmEntry entry) => StartSettings.Of(entry);'
        To = 'internal static StartSetting? WayBackTo(ScmEntry entry) => StartSettings.Of(entry) is StartSetting.AutomaticDelayed ? null : StartSettings.Of(entry);'
        Test = 'StartSettingTests.A_delayed_automatic_entry_gets_its_way_back_by_name'
    },
    @{
        # The old answer, restored: comparing the type alone. With a writer that clears the flag,
        # automatic on a delayed entry is a real change, and "already there" would be a lie.
        Name = 'already there: automatic on a delayed entry is a change now'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = '            ActionKind.SetStartType => StartSettings.Of(target) == action.To,'
        To = '            ActionKind.SetStartType => target.StartType is { IsPresent: true } kept && kept.Value == StartSettings.Written(action.To!.Value).Type,'
        Test = 'StartSettingTests.Automatic_on_a_delayed_entry_is_a_change_and_the_late_setting_is_not'
    },
    @{
        Name = 'late start: an entry in a load order group is refused before planning'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = '        if (action.To == StartSetting.AutomaticDelayed && target.LoadOrderGroup.IsPresent)'
        To = '        if (action.To == StartSetting.AutomaticDelayed && target.LoadOrderGroup.IsPresent && false)'
        Test = 'StartSettingTests.A_late_start_in_a_load_order_group_is_refused_before_anything_is_planned'
    },
    @{
        Name = 'keeps running: a running entry set to disabled is said to keep running'
        File = 'src\Bws.Core\Planning\StartTypeWarnings.cs'
        From = '        if (setting == StartSetting.Disabled
            && target.Status != EntryStatus.Stopped'
        To = '        if (setting == StartSetting.Manual
            && target.Status != EntryStatus.Stopped'
        Test = 'StartSettingTests.Disabling_a_running_entry_says_it_keeps_running'
    },
    @{
        Name = 'keeps running: only where the state was read'
        File = 'src\Bws.Core\Planning\StartTypeWarnings.cs'
        From = '            || target.Status == EntryStatus.Unknown)'
        To = '            || (target.Status == EntryStatus.Unknown && false))'
        Test = 'StartSettingTests.Only_a_running_entry_set_to_disabled_is_said_to_keep_running'
    },
    @{
        Name = 'keeps running: not said on a plan that already stops the entry'
        File = 'src\Bws.Core\Planning\StartTypeWarnings.cs'
        From = '            && !PlanBuilder.StopsAlong(action, target))'
        To = '            && PlanBuilder.StopsAlong(action, target) == PlanBuilder.StopsAlong(action, target))'
        Test = 'StartSettingTests.Taking_the_offer_puts_the_stop_after_the_setting_and_drops_the_sentence'
    },
    @{
        Name = 'starts at next boot: said only of a stopped entry'
        File = 'src\Bws.Core\Planning\StartTypeWarnings.cs'
        From = 'if (StartSettings.StartsAtBoot(setting) && target.Status == EntryStatus.Stopped)'
        To = 'if (StartSettings.StartsAtBoot(setting) && target.Status != EntryStatus.Unknown)'
        Test = 'StartSettingTests.A_stopped_entry_set_to_start_at_boot_is_said_to_start_at_the_next_restart'
    },
    @{
        Name = 'also stop: taking the offer adds the stop step after the setting'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = '                if (StopsAlong(action, target))
                {
                    AddStops(steps, cascade, target);'
        To = '                if (StopsAlong(action, target) && false)
                {
                    AddStops(steps, cascade, target);'
        Test = 'StartSettingTests.Taking_the_offer_puts_the_stop_after_the_setting_and_drops_the_sentence'
    },
    @{
        Name = 'also stop: no stop step for an entry already stopped'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = 'action is { Kind: ActionKind.SetStartType, AlsoStop: true } && target.Status != EntryStatus.Stopped;'
        To = 'action is { Kind: ActionKind.SetStartType, AlsoStop: true } && target.Status != EntryStatus.Unknown;'
        Test = 'StartSettingTests.The_offer_adds_no_stop_to_an_entry_already_stopped'
    },
    @{
        Name = 'also stop: a stop rides only on disabled'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = '        if (action.AlsoStop && action is not { Kind: ActionKind.SetStartType, To: StartSetting.Disabled })'
        To = '        if (action.AlsoStop && action is not { Kind: ActionKind.SetStartType })'
        Test = 'StartSettingTests.A_stop_rides_only_on_disabled'
    },
    @{
        Name = 'also stop: the way back sets the entry back before it starts it'
        File = 'src\Bws.Core\Planning\NetEffect.cs'
        From = '            .ThenByDescending(one => one.Setting)'
        To = '            .ThenBy(one => one.Setting)'
        Test = 'StartSettingTests.The_way_back_from_disabling_and_stopping_sets_it_back_before_starting_it'
    },
    @{
        Name = 'also stop: the line to paste carries --stop'
        File = 'src\Bws.Core\Planning\EquivalentCommand.cs'
        # RE-ANCHORED 2026-10-05 - the line moved into Line (stability report W-12).
        From = 'return Line(Verb(action.Kind), action.ServiceName, word, action.AlsoStop ? [AlsoStop] : []);'
        To = 'return Line(Verb(action.Kind), action.ServiceName, word, action.AlsoStop && false ? [AlsoStop] : []);'
        Test = 'StartSettingTests.The_line_for_the_offer_carries_the_stop'
    },
    @{
        Name = 'also stop: a selection carrying the stop is ordered like a stop'
        File = 'src\Bws.Core\Planning\BulkPlanBuilder.cs'
        From = '(action.Kind == ActionKind.SetStartType && !action.AlsoStop)'
        To = '(action.Kind == ActionKind.SetStartType)'
        Test = 'StartSettingTests.A_selection_that_also_stops_is_ordered_like_a_stop'
    },
    @{
        Name = 'also stop: the stop reaches every entry of a selection'
        File = 'src\Bws.Core\Planning\BulkPlanBuilder.cs'
        From = 'action.Kind, name, action.IncludeDependents, action.To, AlsoStop: action.AlsoStop));'
        To = 'action.Kind, name, action.IncludeDependents, action.To, AlsoStop: false));'
        Test = 'AlsoStopGuards.Taking_the_offer_rebuilds_the_sheet_with_the_stop_after_the_setting'
    },
    @{
        Name = 'writer: a setting nobody defined is refused before a handle is opened'
        File = 'src\Bws.Core\WindowsScmControl.cs'
        From = '        if (!Enum.IsDefined(wanted))'
        To = '        if (!Enum.IsDefined(wanted) && false)'
        Test = 'StartTypeTests.A_start_type_the_manager_cannot_be_told_is_refused_before_anything_is_opened'
    },
    @{
        Name = 'start-type: --stop beside a word other than disabled is refused'
        File = 'src\Bws.Cli\Refusals.cs'
        From = '        if (asked.AlsoStop && setting != StartSetting.Disabled)'
        To = '        if (asked.AlsoStop && setting is null)'
        Test = 'StartSettingCommandTests.A_stop_beside_any_word_but_disabled_is_refused_before_anything_is_read'
    },
    @{
        Name = 'start-type: --stop is a switch of that verb and no other'
        File = 'src\Bws.Cli\OptionSurface.cs'
        From = '        ("--stop", [CommandKind.SetStartType]),'
        To = '        ("--stop", [CommandKind.SetStartType, CommandKind.Stop]),'
        Test = 'StartSettingCommandTests.A_stop_switch_on_another_verb_is_an_option_that_verb_does_not_take'
    },
    @{
        Name = 'plan document: a late step carries delayedAuto true beside startType Automatic'
        File = 'src\Bws.Cli\PlanJson.cs'
        From = '        step.To is { } setting ? StartSettings.Written(setting).Delayed : null;'
        To = '        step.To is { } setting ? StartSettings.Written(setting).Delayed && false : null;'
        Test = 'StartSettingCommandTests.The_plan_document_says_a_late_setting_the_way_the_listing_does'
    },
    @{
        Name = 'plan document: a stop riding on a setting says alsoStop'
        File = 'src\Bws.Cli\PlanJson.cs'
        From = '            AlsoStop = plan.Action.AlsoStop,'
        To = '            AlsoStop = false,'
        Test = 'StartSettingCommandTests.A_stop_riding_on_a_setting_is_asked_for_and_its_step_carries_no_setting'
    },
    @{
        Name = 'offer: none under a sheet that has already run'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        # Re-anchored 2026-09-28: the offer is worked out once, before the lines are kept (G-7).
        From = '            var offering = _run is null && !Busy;'
        To = '            var offering = !Busy;'
        Test = 'AlsoStopGuards.A_sheet_that_has_run_offers_nothing'
    },
    @{
        Name = 'offer: one for the whole selection, under one sentence'
        # RE-ANCHORED 2026-09-30: PlanWarningLine became PlanSentence (W-4). The mutant puts the offer
        # under every sentence saying an entry keeps running rather than under the last one.
        File = 'src\Bws.Gui\ViewModels\PlanSentence.cs'
        From = '                index == lastRunning ? new PlanSentence('
        To = '                warning.Kind == PlanWarningKind.KeepsRunning && offering ? new PlanSentence('
        Test = 'AlsoStopGuards.Disabling_running_entries_says_so_and_offers_one_stop_for_all_of_them'
    },
    @{
        Name = 'hand-over: the stop crosses a restart as administrator'
        File = 'src\Bws.Gui\ViewModels\HandOver.cs'
        From = '            AlsoStop = picked.Length > 0 && wire.Stop,'
        To = '            AlsoStop = picked.Length > 0 && wire.Stop && false,'
        Test = 'AlsoStopGuards.The_stop_crosses_a_restart_as_administrator_with_the_setting'
    },
    @{
        Name = 'hand-over: a stop beside anything but disabled is refused whole'
        File = 'src\Bws.Gui\ViewModels\HandOver.cs'
        From = '|| (wire.Stop && setting != StartSetting.Disabled))'
        To = '|| (wire.Stop && setting == StartSetting.Disabled && false))'
        Test = 'HandOverTests.A_hand_over_this_program_could_not_have_written_is_refused_whole'
    },
    @{
        Name = 'menu: the four settings are offered, the late one among them'
        # RE-ANCHORED 2026-09-24: both menus are filled by StartSettingChoice.Offer since UX-GUI-018.
        File = 'src\Bws.Gui\StartSettingChoice.cs'
        From = '        foreach (var setting in Enum.GetValues<StartSetting>())'
        To = '        foreach (var setting in Enum.GetValues<StartSetting>().Where(one => one != StartSetting.AutomaticDelayed))'
        Test = 'ActionBarGuards.The_start_type_menu_offers_four_settings_and_one_opens_a_plan_naming_it'
    },
    @{
        # The last of package 5's entries as built. Three from the review of PR #17 follow.
        Name = 'label: the late setting reads as the cell of a late entry reads'
        File = 'src\Bws.Gui\ViewModels\CellFaces.cs'
        From = '        return delayed
            ? Texts.Of("gui.cell.start.delayed", TypeLabel(type))'
        To = '        return delayed && false
            ? Texts.Of("gui.cell.start.delayed", TypeLabel(type))'
        Test = 'ActionBarGuards.The_start_type_menu_offers_four_settings_and_one_opens_a_plan_naming_it'
    },
    @{
        # Three more from the review of PR #17, and these ARE the last of package 5's. The offer
        # under "keeps running" stayed live through a run, because the lines were told to look again
        # only when the run ended, and the door behind the button checked the ask and not the sheet.
        # Multi-line anchors, because Replace takes every match and the raise alone appears twice.
        Name = 'offer: gone from the screen the moment a run starts'
        File = 'src\Bws.Gui\ViewModels\Planned.Running.cs'
        # Re-anchored 2026-09-30: the door is TakeTheOffer, and the refusals raise beside the warnings.
        From = '        // plan under a run still going. MainWindow.TakeTheOffer refuses on the same answer.
        Raise(nameof(Warnings));'
        To = '        // plan under a run still going. MainWindow.TakeTheOffer refuses on the same answer.'
        Test = 'AlsoStopGuards.A_sheet_that_is_running_offers_nothing_and_takes_no_press'
    },
    @{
        Name = 'offer: back on the screen when a run is abandoned'
        File = 'src\Bws.Gui\ViewModels\Planned.Running.cs'
        # Re-anchored 2026-09-30: the comment above the raise now speaks of both offers.
        From = '        // come back with the button.
        Raise(nameof(Warnings));'
        To = '        // come back with the button.'
        Test = 'AlsoStopGuards.A_sheet_that_is_running_offers_nothing_and_takes_no_press'
    },
    @{
        Name = 'offer: the door refuses while the sheet offers nothing'
        File = 'src\Bws.Gui\MainWindow.Menu.cs'
        # Re-anchored 2026-09-30: one door for both offers, TakeTheOffer, asking both lists (W-4).
        # AND ITS TEST MOVED THE SAME DAY, BECAUSE IT WAS MISSED: during a run Preview refuses the
        # press by itself (G-1, 2026-09-29), so the running sheet no longer tests this condition at
        # all. On a sheet that has already run nothing else refuses - the record is where the door's
        # own question is the only thing between a press and a rebuilt plan.
        From = '            || !_model.Planned.Warnings.Concat(_model.Planned.Problems).Any(line => line.HasOffer && line.Offer == offer))'
        To = '            || !_model.Planned.Warnings.Concat(_model.Planned.Problems).Any(line => (line.HasOffer && line.Offer == offer) || true))'
        Test = 'AlsoStopGuards.A_sheet_that_has_run_offers_nothing'
    },
    @{
        # PACKAGE 6 OF THE UX AUDIT, 2026-09-24 - UX-GUI-007, 010, 013 and 014, and every entry from
        # here to the end of the list is one of its guards. The design is
        # docs/PROJEKT-UKLAD-OGNISKO-POMOC-20260924.md. TWO BEHAVIOURS HAVE NO ENTRY HERE, said
        # rather than left out: the keyboard in the search box when the list appears (a window the
        # test host builds is never shown and has no keyboard focus to read - its guard is
        # tools/gui-probe/focus-at-start.ps1, proved able to fail on the build before), and the
        # description last among the columns shown at the start (a reorder is a block moved, not a
        # line replaced - ColumnGuards writes the whole order out, so any reorder goes red there).
        Name = 'filters: the fold is written to the file'
        File = 'src\Bws.Gui\ViewModels\ColumnLayout.cs'
        From = '            tree["filtersFolded"] = true;'
        To = '            tree["filtersFolded"] = false;'
        Test = 'FoldedFiltersGuards.Folding_the_filters_is_written_down_and_read_back'
    },
    @{
        Name = 'filters: the fold is read back from the file'
        File = 'src\Bws.Gui\ViewModels\ColumnLayout.cs'
        From = 'fold.TryGetValue<bool>(out var folded) && folded;'
        To = 'fold.TryGetValue<bool>(out var folded) && folded && false;'
        Test = 'FoldedFiltersGuards.Folding_the_filters_is_written_down_and_read_back'
    },
    @{
        Name = 'filters: the window opens the way the profile left them'
        File = 'src\Bws.Gui\MainWindow.Filters.cs'
        From = '        Filters.Switch.IsChecked = !kept.FiltersFolded;'
        To = '        Filters.Switch.IsChecked = !kept.FiltersFolded || true;'
        Test = 'FoldedFiltersGuards.A_window_opens_folded_when_it_was_left_folded_and_writes_the_next_press'
    },
    @{
        Name = 'filters: opening them again is written down'
        File = 'src\Bws.Gui\MainWindow.Filters.cs'
        From = 'Filters.Switch.Checked += (_, _) => kept.TheFiltersWere(folded: false, _model.Says);'
        To = 'Filters.Switch.Checked += (_, _) => kept.TheFiltersWere(folded: true, _model.Says);'
        Test = 'FoldedFiltersGuards.A_window_opens_folded_when_it_was_left_folded_and_writes_the_next_press'
    },
    @{
        Name = 'help: F1 opens the help menu'
        File = 'src\Bws.Gui\Shortcuts.cs'
        From = '            Key.F1 => Shortcut.Help,'
        To = '            Key.F1 => Shortcut.None,'
        Test = 'HelpMenuGuards.F1_opens_the_help_menu'
    },
    @{
        Name = 'help: the menu hangs under its button'
        File = 'src\Bws.Gui\MainWindow.Help.cs'
        From = 'Scope.Help.ContextMenu = RowMenu.Build('
        To = 'Scope.Help.Tag = RowMenu.Build('
        Test = 'HelpMenuGuards.The_help_menu_holds_the_query_language_page_and_nothing_else'
    },
    # Three entries left here on 2026-09-24 with the code they broke - select every entry, a shortcut
    # item doing what its key does, and the version dropping what the SDK appends. The Help menu
    # was cut to the query language page that day, and src\Bws.Gui\Release.cs went with the version.
    @{
        Name = 'help: the menu offers the query language page'
        File = 'src\Bws.Gui\HelpMenu.cs'
        From = 'new RowMenuEntry("gui.help.queryLanguage", null,'
        To = 'new RowMenuEntry("gui.help.button", null,'
        Test = 'HelpMenuGuards.The_help_menu_holds_the_query_language_page_and_nothing_else'
    },
    @{
        Name = 'row menu: Ctrl+C is written beside copy everything'
        File = 'src\Bws.Gui\RowMenu.cs'
        From = 'new RowMenuEntry("gui.menu.copyAll", "gui.menu.copyAll.gesture",'
        To = 'new RowMenuEntry("gui.menu.copyAll", null,'
        Test = 'RowMenuGuards.The_menu_starts_with_show_details_and_writes_its_key_beside_it'
    },
    @{
        # PACKAGE 7 OF THE UX AUDIT, 2026-09-24 - UX-GUI-015, 016, 017 and 018, the last package, and
        # every entry from here to the end of the list is one of its guards. The design is
        # docs/PROJEKT-TEKSTY-EKSPORT-CHIPY-NAZWY-20260924.md. WHAT HAS NO ENTRY, said rather than left
        # out: the new sentences themselves (TextKeyGuards holds that each key reaches a screen), and
        # the rules moving onto the Filters switch (the chip rule they state already has one -
        # 'chips: a group asks about one field').
        Name = 'overview: show the list clears the question the last number left'
        File = 'src\Bws.Gui\OverviewView.xaml.cs'
        From = '        model.ClearQuery();'
        To = ''
        Test = 'OverviewGuards.Show_the_list_clears_the_question_the_last_number_left_in_the_box'
    },
    @{
        Name = 'overview: the second orphan number counts what is not automatic'
        File = 'src\Bws.Gui\ViewModels\Overview.cs'
        From = '$"file:missing !start:auto {Services}"'
        To = '$"file:missing {Services}"'
        Test = 'OverviewGuards.An_orphan_is_told_apart_from_an_entry_that_merely_lost_its_file'
    },
    @{
        Name = 'export: a written file is said'
        File = 'src\Bws.Gui\MainWindow.Exporting.cs'
        # Re-anchored 2026-10-05: the sentence also counts the folded copies (G-8).
        From = '            _model.Says.Did(Exporting.Done(rows, Path.GetFileName(path)));'
        To = '            _ = rows.Count;'
        Test = 'ExportingGuards.A_written_file_is_said_with_its_rows_and_its_name_until_somebody_moves_on'
    },
    @{
        Name = 'export: the name offered follows the tab'
        File = 'src\Bws.Gui\ViewModels\Exporting.cs'
        From = 'EntryScope.Drivers => Texts.Of("gui.export.name.drivers"),'
        To = 'EntryScope.Drivers => Texts.Of("gui.export.name.services"),'
        Test = 'ExportingGuards.The_name_offered_is_the_list_somebody_is_looking_at'
    },
    @{
        # Added 2026-09-24 from the CodeRabbit review of PR #19: the test asked only that the three
        # names differ and that the first was services.csv, so two tabs trading names passed.
        Name = 'export: drivers and everything keep their own names'
        File = 'src\Bws.Gui\ViewModels\Exporting.cs'
        From = '        EntryScope.Drivers => Texts.Of("gui.export.name.drivers"),
        EntryScope.Everything => Texts.Of("gui.export.name.all"),'
        To = '        EntryScope.Drivers => Texts.Of("gui.export.name.all"),
        EntryScope.Everything => Texts.Of("gui.export.name.drivers"),'
        Test = 'ExportingGuards.The_name_offered_is_the_list_somebody_is_looking_at'
    },
    @{
        # Added 2026-09-24 from the CodeRabbit review of PR #19: the test exported ONE row, so a
        # sentence that always said one row passed.
        Name = 'export: the sentence counts the rows written'
        File = 'src\Bws.Gui\MainWindow.Exporting.cs'
        # Re-anchored 2026-10-05: the sentence also counts the folded copies (G-8).
        From = '            _model.Says.Did(Exporting.Done(rows, Path.GetFileName(path)));'
        To = '            _model.Says.Did(Exporting.Done([.. rows.Take(1)], Path.GetFileName(path)));'
        Test = 'ExportingGuards.A_written_file_is_said_with_its_rows_and_its_name_until_somebody_moves_on'
    },
    @{
        # The three ways the second line of the foot of the window keeps to one piece of news.
        Name = 'says: a refusal puts away what finished before it'
        File = 'src\Bws.Gui\ViewModels\Says.cs'
        From = '        // a line rather than stand one over the other.
        _done = string.Empty;'
        To = '        // a line rather than stand one over the other.
        _done += string.Empty;'
        Test = 'ExportingGuards.A_finished_action_and_a_refused_one_put_each_other_away'
    },
    @{
        Name = 'says: a finished action puts away the refusal before it'
        File = 'src\Bws.Gui\ViewModels\Says.cs'
        From = '        _refusal = string.Empty;
        _layout = string.Empty;
        _done = sentence;'
        To = '        _layout = string.Empty;
        _done = sentence;'
        Test = 'ExportingGuards.A_finished_action_and_a_refused_one_put_each_other_away'
    },
    @{
        Name = 'says: what finished goes when somebody asks something else'
        File = 'src\Bws.Gui\ViewModels\Says.cs'
        From = '        // be a sentence about a list nobody is looking at any more.
        _done = string.Empty;'
        To = '        // be a sentence about a list nobody is looking at any more.
        _done += string.Empty;'
        Test = 'ExportingGuards.A_written_file_is_said_with_its_rows_and_its_name_until_somebody_moves_on'
    },
    @{
        Name = 'row menu: set startup type holds the four settings'
        File = 'src\Bws.Gui\RowMenu.cs'
        From = '            StartSettingChoice.Offer(made, item, chosen);'
        To = '            _ = chosen;'
        Test = 'RowMenuGuards.The_start_type_item_holds_the_bar_s_four_settings_and_one_opens_its_plan'
    },
    @{
        Name = 'row menu: a setting chosen on a row opens its plan'
        File = 'src\Bws.Gui\RowMenu.cs'
        From = 'window.Preview(ActionKind.SetStartType, setting))'
        To = 'window.Preview(ActionKind.Stop))'
        Test = 'RowMenuGuards.The_start_type_item_holds_the_bar_s_four_settings_and_one_opens_its_plan'
    },
    @{
        # Added 2026-09-24 from the CodeRabbit review of PR #19: the test asked for a startup type
        # plan and never asked WHICH setting, so any one of the four passed.
        Name = 'row menu: the setting chosen is the setting planned'
        File = 'src\Bws.Gui\RowMenu.cs'
        From = 'window.Preview(ActionKind.SetStartType, setting))'
        To = 'window.Preview(ActionKind.SetStartType, StartSetting.AutomaticDelayed))'
        Test = 'RowMenuGuards.The_start_type_item_holds_the_bar_s_four_settings_and_one_opens_its_plan'
    },
    @{
        Name = 'menu: the late setting says what late means'
        File = 'src\Bws.Gui\StartSettingChoice.cs'
        From = 'StartSetting.AutomaticDelayed => Texts.Of("gui.start.mark.delayed"),'
        To = 'StartSetting.AutomaticDelayed => Texts.Of("gui.start.mark.automatic"),'
        Test = 'RowMenuGuards.The_start_type_item_holds_the_bar_s_four_settings_and_one_opens_its_plan'
    },
    @{
        # PACKAGE 8, 2026-09-24 - the owner's four requests after the audit: the way off the first
        # screen, the entry's name set apart in the plan panel, two copies off the row menu and
        # Help cut to one page. docs/PROJEKT-WYJSCIE-NAZWY-MENU-20260924.md. The Help entry is
        # beside the other Help entries above.
        Name = 'overview: the way off wears the primary action'
        File = 'src\Bws.Gui\OverviewView.xaml'
        From = '                            Style="{StaticResource PrimaryAction}"'
        To = '                            Style="{StaticResource {x:Type Button}}"'
        Test = 'ButtonGuards.The_way_off_the_first_screen_wears_the_colour_that_means_press_this'
    },
    @{
        Name = 'row menu: two copies and no more'
        File = 'src\Bws.Gui\RowMenu.cs'
        From = 'new RowMenuEntry("gui.menu.copyName", null,'
        To = 'new RowMenuEntry("gui.menu.copyAll", null, () => Task.FromResult(window.Copy(Copying.Name))), new RowMenuEntry("gui.menu.copyName", null,'
        Test = 'RowMenuGuards.The_menu_starts_with_show_details_and_writes_its_key_beside_it'
    },
    @{
        Name = 'plan name: a sentence is cut where the name stands'
        File = 'src\Bws.Gui\ViewModels\NamedSentence.cs'
        From = ': new NamedSentence(formatted[..at], name, after);'
        To = ': Unnamed(formatted.Replace(Slot, name, StringComparison.Ordinal));'
        Test = 'PlanNameGuards.A_sentence_is_cut_where_the_name_stands'
    },
    @{
        Name = 'plan name: two marks leave the sentence whole'
        File = 'src\Bws.Gui\ViewModels\NamedSentence.cs'
        From = '? Unnamed(formatted.Replace(Slot, name, StringComparison.Ordinal))'
        To = '? new NamedSentence(formatted[..at], name, after)'
        Test = 'PlanNameGuards.A_sentence_without_exactly_one_mark_comes_back_whole'
    },
    @{
        Name = 'plan name: a step draws the name heavier'
        File = 'src\Bws.Gui\Themes\PlanLines.xaml'
        From = 'Style="{StaticResource NameInSentence}" /><Run Text="{Binding Sentence.After'
        To = '/><Run Text="{Binding Sentence.After'
        Test = 'PlanNameGuards.The_panel_draws_the_name_heavier_and_every_word_as_the_model_says'
    },
    @{
        Name = 'plan name: the title draws the name heavier'
        File = 'src\Bws.Gui\PlanView.xaml'
        From = 'Style="{StaticResource NameInSentence}" /><Run Text="{Binding Heading.After'
        To = '/><Run Text="{Binding Heading.After'
        Test = 'PlanNameGuards.The_panel_draws_the_name_heavier_and_every_word_as_the_model_says'
    },
    @{
        Name = 'plan name: the words around the name in the title are plain'
        File = 'src\Bws.Gui\Themes\Text.xaml'
        From = '    <Style x:Key="PlanTitleText" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource TextFace}" />
        <Setter Property="FontSize" Value="{StaticResource TextSizeHeader}" />
        <Setter Property="FontWeight" Value="{StaticResource TextWeightNormal}" />'
        To = '    <Style x:Key="PlanTitleText" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource TextFace}" />
        <Setter Property="FontSize" Value="{StaticResource TextSizeHeader}" />
        <Setter Property="FontWeight" Value="{StaticResource TextWeightHeader}" />'
        Test = 'PlanNameGuards.The_panel_draws_the_name_heavier_and_every_word_as_the_model_says'
    },
    @{
        Name = 'plan name: a screen reader hears the whole title'
        File = 'src\Bws.Gui\PlanView.xaml'
        From = 'AutomationProperties.Name="{Binding Heading.Text}"'
        To = 'AutomationProperties.Name="{Binding Heading.Name}"'
        Test = 'PlanNameGuards.The_panel_draws_the_name_heavier_and_every_word_as_the_model_says'
    },
    @{
        Name = 'plan name: a screen reader hears the whole step'
        File = 'src\Bws.Gui\Themes\PlanLines.xaml'
        From = '                   AutomationProperties.Name="{Binding Text}"'
        To = '                   AutomationProperties.Name="{Binding Sentence.Name}"'
        Test = 'PlanNameGuards.The_panel_draws_the_name_heavier_and_every_word_as_the_model_says'
    },
    @{
        # REVIEW OF PR 20, 2026-09-25. Every test of "Copy everything" computed its wanted text with
        # Copying.Everything itself, so a copy that dropped the display name and the description -
        # the two fields whose own items went on 2026-09-24 because this one carries them - agreed
        # with itself. SelectionGuards asks only for the names. MISSED before the menu test asked the
        # fixture's words, caught after.
        Name = 'copy: "Copy everything" carries the display name and the description'
        File = 'src\Bws.Gui\ViewModels\Copying.cs'
        From = 'rows.Count == 0 ? null : Details.AsText(rows.Select(row => row.Entry));'
        To = 'rows.Count == 0 ? null : Name(rows);'
        Test = 'CopyingAndPanelGuards.The_context_menu_copies_what_each_item_promises'
    },
    @{
        # REVIEW OF PR 20, 2026-09-25. The panel's contrast guard read TextBlock.Text, which is EMPTY
        # for a block built from runs - and since 2026-09-24 the title and every step are. A title in
        # a colour reading 5.69 on the window and 4.35 on the panel passed, because the guard never
        # saw the title. MISSED before the guard read the runs, caught after.
        Name = 'plan panel: the title built from runs is measured for contrast'
        File = 'src\Bws.Gui\PlanView.xaml'
        From = 'Style="{StaticResource PlanTitleText}"><Run Text="{Binding Heading.Before, Mode=OneWay}" />'
        To = 'Foreground="{StaticResource MeaningUntrusted}" Style="{StaticResource PlanTitleText}"><Run Text="{Binding Heading.Before, Mode=OneWay}" />'
        Test = 'PlanViewGuards.Every_word_on_the_panel_can_be_read_on_the_surface_it_is_drawn_on'
    },
    @{
        # THE OWNER'S COMPLAINT OF 2026-09-25: the window jumped while he typed, 31 device pixels at
        # the first character. The sentence in the field keeps to one line - wrapped, a mistake that
        # lists every value a field accepts would make the field three lines tall and move everything
        # under it, which is the same fault by another road.
        Name = 'search field: the sentence in the field keeps to one line'
        File = 'src\Bws.Gui\Themes\SearchField.xaml'
        From = '        <Setter Property="TextWrapping" Value="NoWrap" />'
        To = '        <Setter Property="TextWrapping" Value="Wrap" />'
        Test = 'AnswerLineGuards.The_search_row_keeps_its_height_whatever_the_answer_says'
    },
    @{
        # REVIEW OF PR 21, 2026-09-25. CI went red on the edge's threshold: more than 20 red pixels,
        # 22 here and 18 on the runner. It is half the field's height now. The entry above turns the
        # trigger round and is caught on the FINE query, so it never asks the threshold - this one
        # takes the red off the edge altogether, so only the threshold can catch it.
        Name = 'answer line: a field with no red edge fails the half-height threshold'
        File = 'src\Bws.Gui\Themes\SearchField.xaml'
        From = @'
            <DataTrigger Binding="{Binding Says.AnswerLineIsProblem}" Value="True">
                <Setter Property="BorderBrush" Value="{StaticResource MeaningRejected}" />
'@
        To = @'
            <DataTrigger Binding="{Binding Says.AnswerLineIsProblem}" Value="True">
                <Setter Property="BorderBrush" Value="{DynamicResource TextControlElevationBorderBrush}" />
'@
        Test = 'AnswerLineGuards.The_search_box_wears_the_problem_colour_on_its_edge_while_its_query_is_wrong'
    },
    @{
        # REVIEW OF PR 21, 2026-09-25. The search row went up a layer so the panel under the field
        # lies over the filters, and the plan - painting last by document order - fell under it: the
        # field took clicks and keys through the dimming. Put back on the default layer, the plan is
        # under the search field again. (Not LayerOverTheRows: on an equal layer document order
        # decides, and the plan is declared last, so that mutant would be a correct window.)
        Name = 'plan layer: the plan lies over the search field'
        File = 'src\Bws.Gui\MainWindow.xaml'
        From = 'Panel.ZIndex="{StaticResource LayerPlan}"'
        To = 'Panel.ZIndex="0"'
        Test = 'WindowLayerGuards.The_plan_lies_over_the_whole_window_the_search_field_included'
    },
    @{
        # REVIEW OF PR 21, 2026-09-25. The library's key carries FocusLine's colour written a second
        # time, since an alias does not reach its template. Drifted, every text box but the search
        # field draws a line nobody measured.
        Name = 'focus line: the library key carries the same colour as FocusLine'
        File = 'src\Bws.Gui\Themes\Surfaces.xaml'
        From = '<SolidColorBrush x:Key="TextControlFocusedBorderBrush" Color="#65A3D8" />'
        To = '<SolidColorBrush x:Key="TextControlFocusedBorderBrush" Color="#3F7099" />'
        Test = 'ContrastGuards.The_focus_line_clears_its_ratio_against_every_fill_a_text_box_takes'
    },
    @{
        # The same test's ratio rather than its equality: a brighter fill under the pointer and the
        # line reads 2.10 on the plan sheet.
        Name = 'focus line: clears 3:1 against every text box fill on the plan sheet'
        File = 'src\Bws.Gui\Themes\Surfaces.xaml'
        From = '<SolidColorBrush x:Key="TextControlBackgroundPointerOver" Color="#1AFFFFFF" />'
        To = '<SolidColorBrush x:Key="TextControlBackgroundPointerOver" Color="#40FFFFFF" />'
        Test = 'ContrastGuards.The_focus_line_clears_its_ratio_against_every_fill_a_text_box_takes'
    },
    @{
        # OWNER'S DECISION OF 2026-09-25, T1 of docs/PROJEKT-PODPOWIEDZI-UX-20260925.md: Tab writes
        # the chosen word. Without its meaning Tab walks out of the box as it did before.
        Name = 'suggestions: Tab means the word under the box'
        File = 'src\Bws.Gui\Shortcuts.cs'
        From = '            Key.Tab => Shortcut.CompleteWord,'
        To = '            Key.Tab => Shortcut.None,'
        Test = 'KeyboardTests.Tab_alone_means_the_word_under_the_box_and_Shift_Tab_never_does'
    },
    @{
        # And only for a list of WORDS. Tied to any open list, Tab writes the first question into
        # the box of anybody who arrived at it by Tab - and stops them walking through the window.
        Name = 'suggestions: Tab walks past the questions'
        File = 'src\Bws.Gui\MainWindow.Keyboard.cs'
        From = 'Shortcut.CompleteWord => _model.Suggesting.TabWrites ? Shortcut.TakeSuggestion : Shortcut.None,'
        To = 'Shortcut.CompleteWord => _model.Suggesting.IsOpen ? Shortcut.TakeSuggestion : Shortcut.None,'
        Test = 'KeyboardTests.Tab_in_the_box_writes_a_word_and_walks_on_everywhere_else'
    },
    @{
        # The model's half of the same rule, asked without a window.
        Name = 'suggestions: the model says Tab writes only a word'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = '    public bool TabWrites => CanTake && !_questions;'
        To = '    public bool TabWrites => CanTake;'
        Test = 'SuggestingTypingTests.Tab_writes_a_word_and_walks_past_the_questions_and_past_nothing'
    },
    @{
        # P2 of the same design: a caret moving through unchanged text closes the list. Without the
        # rule, one Left arrow in status:running opens a list of one row saying running.
        Name = 'suggestions: a caret move through unchanged text closes the list'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = '        if (!_keyboardHere || selectionLength > 0 || (sameText && !sameCaret))'
        To = '        if (!_keyboardHere || selectionLength > 0)'
        Test = 'SuggestingTypingTests.A_caret_moving_through_unchanged_text_opens_nothing_and_closes_what_was_open'
    },
    @{
        # The window's half that P2 would otherwise break: a row just written is followed as typing,
        # or the values after a field never open and sta Tab Tab stops at status:.
        Name = 'suggestions: a row just written is followed as typing'
        File = 'src\Bws.Gui\MainWindow.Suggesting.cs'
        From = '        _model.Suggesting.Wrote(box.Text, box.CaretIndex);'
        To = '        _ = box.CaretIndex;'
        Test = 'KeyboardTests.Tab_twice_writes_a_field_and_then_its_first_value'
    },
    @{
        # The sentence under the list changes with the kind of list, so the window has to be told -
        # or it keeps saying the questions' keys over a list of words.
        Name = 'suggestions: the sentence about the keys is announced when the list changes kind'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = @'
        Offered = rows;
        Raise(nameof(Caption));
        Raise(nameof(Keys));
'@
        To = @'
        Offered = rows;
        Raise(nameof(Caption));
'@
        Test = 'SuggestingTypingTests.The_sentence_about_the_keys_names_Tab_only_where_Tab_writes'
    },
    @{
        # REVIEW OF PR 22: nothing is written over a character an input method is still composing.
        # Without the condition Tab and Enter write the row into the middle of a composition.
        Name = 'suggestions: nothing is taken while an input method is composing'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = '    public bool CanTake => IsOpen && !_composing;'
        To = '    public bool CanTake => IsOpen;'
        Test = 'SuggestingTypingTests.Nothing_is_taken_while_an_input_method_is_composing'
    },
    @{
        # The keyboard leaving ends a composition, or one abandoned without an end keeps Tab and
        # Enter from writing until the next character.
        Name = 'suggestions: the keyboard leaving ends a composition'
        File = 'src\Bws.Gui\ViewModels\Suggesting.cs'
        From = @'
            _composing = false;
            Close();
'@
        To = @'
            Close();
'@
        Test = 'SuggestingTypingTests.Nothing_is_taken_while_an_input_method_is_composing'
    },
    @{
        # The window's half: the start of a composition in the box is heard. Turned round, a
        # composition opening says it closed.
        Name = 'suggestions: the window hears a composition open in the box'
        File = 'src\Bws.Gui\MainWindow.Suggesting.cs'
        From = 'new TextCompositionEventHandler((_, _) => list.Composing(open: true)),'
        To = 'new TextCompositionEventHandler((_, _) => list.Composing(open: false)),'
        Test = 'SuggestingTypingTests.The_window_hears_a_composition_open_in_the_box_and_close'
    }

    # W2 of the performance series, 2026-09-28 - the external report's window findings (G-4, G-5,
    # G-7, G-8, G-11). docs/ANALIZA-RAPORTU-WYDAJNOSCI-20260928.md, section 2.
    @{
        # G-5, backlog 467. WPF leaves IsVisible true on a minimised window, so without this event
        # the timer went on asking the manager once a second for a window nobody could see.
        Name = 'window: a minimised window stops asking the machine'
        File = 'src\Bws.Gui\MainWindow.xaml.cs'
        From = '        StateChanged += async (_, _) => await WatchWhileSeen().ConfigureAwait(true);'
        To = ''
        Test = 'MinimisedWindowGuards.A_minimised_window_stops_asking_and_asks_at_once_when_it_comes_back'
    },
    @{
        # Coming back asks at once, not a second later - the list on screen is what the machine
        # looked like when the window went away.
        Name = 'window: coming back from minimised asks at once'
        File = 'src\Bws.Gui\MainWindow.xaml.cs'
        From = '        await Tick().ConfigureAwait(true);'
        To = '        await Task.CompletedTask.ConfigureAwait(true);'
        Test = 'MinimisedWindowGuards.A_minimised_window_stops_asking_and_asks_at_once_when_it_comes_back'
    },
    @{
        # A window opened minimised: the first look starts the timer itself, so it asks too.
        Name = 'window: the first look leaves a minimised window without the timer'
        File = 'src\Bws.Gui\MainWindow.HandOver.cs'
        From = '        if (Seen)'
        To = '        if (Seen || _looked)'
        Test = 'MinimisedWindowGuards.A_window_opened_minimised_reads_once_and_waits_to_be_looked_at'
    },
    @{
        # G-4. The query as typed is kept beside its text and made again only when the text
        # differs - without that, every question about the same text parses it again.
        Name = 'query: one parse for one text'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.cs'
        From = @'
            ? _asTyped
            : _asTyped = QueryAsTyped.Of(_queryText);
'@
        To = @'
            ? QueryAsTyped.Of(_queryText)
            : _asTyped = QueryAsTyped.Of(_queryText);
'@
        Test = 'QueryParsedOnceGuards.One_keystroke_parses_the_query_once_and_a_tick_does_not_parse_it_again'
    },
    @{
        # G-4, the chips' half - sixteen of the eighteen parses one keystroke used to cost.
        Name = 'query: a chip reads the parse it is handed'
        File = 'src\Bws.Gui\ViewModels\FilterChips.cs'
        From = '        get => _read().Carries(Field, Value, Negated);'
        To = '        get => QueryMembers.Carries(_read().Text, Field, Value, Negated);'
        Test = 'QueryParsedOnceGuards.One_keystroke_parses_the_query_once_and_a_tick_does_not_parse_it_again'
    },
    @{
        # G-8. The details panel rebuilds for the word for every cell and the name, not for the
        # shapes and the highlight a row raises beside them.
        Name = 'details panel: rebuilt only for what it shows'
        File = 'src\Bws.Gui\ViewModels\Chosen.cs'
        From = '            && e.PropertyName is not (EntryRow.EveryCell or nameof(EntryRow.DisplayName)))'
        To = '            && e.PropertyName is "never raised")'
        Test = 'ChosenGuards.A_status_moving_rebuilds_the_panel_once_and_the_highlight_going_out_does_not'
    },
    @{
        # G-11. The second pass setting out changes one sentence. Doing the whole reread as well
        # runs the query a third time over entries just shown.
        Name = 'second pass: setting out says the sentence and nothing else'
        File = 'src\Bws.Gui\ViewModels\Readings.SecondPhase.cs'
        From = '        _announce();'
        To = @'
        _announce();
        _settled();
'@
        Test = 'SecondPassSettingOutTests.Setting_out_says_the_signatures_are_being_read_and_runs_the_query_only_around_the_pass'
    },
    @{
        # G-11, the other half: setting out still says the signatures are being read.
        Name = 'second pass: setting out says the signatures are being read'
        File = 'src\Bws.Gui\ViewModels\Readings.SecondPhase.cs'
        From = '        _announce();'
        To = ''
        Test = 'SecondPassSettingOutTests.Setting_out_says_the_signatures_are_being_read_and_runs_the_query_only_around_the_pass'
    },
    @{
        # G-7. The warnings are built once for a plan and a state of the offer, not once per read.
        Name = 'plan sheet: the warnings are one list however often they are read'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        From = '            return kept.Lines;'
        To = '            return [.. kept.Lines];'
        Test = 'PlanSheetListsTests.The_commands_and_the_warnings_are_the_same_lists_however_often_they_are_read'
    },
    @{
        # G-7. The commands are rendered when a plan is shown - the risk of keeping them is a new
        # plan still showing the old plan's commands.
        Name = 'plan sheet: a new plan renders its own commands'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        # Re-anchored 2026-09-29: the commands are held in a CommandBlock since W6.
        From = '        _commandBlock = new CommandBlock(EquivalentCommand.For(plan));'
        To = '        _commandBlock = _commandBlock.Any ? _commandBlock : new CommandBlock(EquivalentCommand.For(plan));'
        Test = 'PlanSheetListsTests.The_commands_and_the_warnings_are_the_same_lists_however_often_they_are_read'
    },
    @{
        # G-7. Putting the sheet away drops its commands with the plan.
        Name = 'plan sheet: putting it away drops the commands'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        # Re-anchored 2026-09-29: the commands are held in a CommandBlock since W6.
        From = '        _commandBlock = CommandBlock.None;'
        To = ''
        Test = 'PlanSheetListsTests.The_commands_and_the_warnings_are_the_same_lists_however_often_they_are_read'
    },

    # W3 / S-6, 2026-09-29: the pauses between looks at a plan step double from 10 ms up to the
    # 250 ms cadence, and the entry's own promise stays rounded up to a whole cadence as it always
    # was in effect. Two entries hold the speed, four hold what the speed must not cost.
    @{
        # The whole point of S-6. Back to a flat cadence, a 5 ms arrival is reported at 250.
        Name = 'plan cadence: the second look comes soon after the first'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '        var pause = FirstLook;'
        To = '        var pause = Cadence;'
        Test = 'PlanRunCadenceTests.An_entry_is_seen_soon_after_it_arrives_rather_than_a_whole_cadence_later'
    },
    @{
        # The first look goes straight after the request, and a stop over in 2 ms is reported then.
        Name = 'plan cadence: the first look goes straight after the request'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '            var answer = control.Read(serviceName);'
        To = '            clock.Wait(FirstLook); var answer = control.Read(serviceName);'
        Test = 'PlanRunCadenceTests.An_entry_is_seen_soon_after_it_arrives_rather_than_a_whole_cadence_later'
    },
    @{
        # PACKAGE C OF THE STABILITY REPORT, 2026-09-30 (W-1, owner's decision of 2026-09-29): the
        # limit counts from the LAST PROGRESS. Without the reset a service stopping honestly past the
        # limit is given up on at the limit - and a restart of it used to leave it stopped.
        Name = 'plan limit: the limit counts from the last progress'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '                movedAt = now;'
        To = '                _ = now;'
        Test = 'PlanRunProgressTests.A_stop_that_keeps_making_progress_past_the_limit_is_watched_to_its_end'
    },
    @{
        # W-7: a stop to an entry already stopping is refused by the manager, so it is waited for.
        Name = 'plan wait: an entry already on its way is waited for rather than asked'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '            return OnItsWay(target, status) ? WaitFor(step, target, timeout, started, Unasked, asked: false) : null;'
        To = '            return null;'
        Test = 'PlanRunProgressTests.A_stop_that_meets_an_entry_already_stopping_asks_nothing_and_waits_for_it'
    },
    @{
        # W-1 itself: the start that puts a service back waits for it to finish stopping.
        Name = 'plan wait: a start waits for the entry to finish stopping before it asks'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '        var watched = Watch(step.ServiceName, now => now != status, timeout, Unasked);'
        To = '        var watched = Watch(step.ServiceName, now => true, timeout, Unasked);'
        Test = 'PlanRunProgressTests.A_restart_whose_stop_ran_out_starts_the_entry_once_it_has_finished_stopping'
    },
    @{
        # Nothing sent, so a failure in our own words - never a request the manager refuses.
        Name = 'plan wait: an entry that never leaves the other way is never asked'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '            _ => Refused(step, ControlAnswer.Refused(0, NeverAsked(target)), Where(seen), Holding(seen), started)'
        To = '            Settled.GaveUp => null, _ => Refused(step, ControlAnswer.Refused(0, NeverAsked(target)), Where(seen), Holding(seen), started)'
        Test = 'PlanRunProgressTests.An_entry_that_never_leaves_the_other_way_is_never_asked'
    },
    @{
        # W-7: a start the service did not survive is over at once, not a minute later.
        Name = 'plan wait: a start that falls back to stopped fails at once'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '        target == EntryStatus.Running && status == EntryStatus.Stopped;'
        To = '        target == EntryStatus.Running && status == EntryStatus.Unknown;'
        Test = 'StartFellOverTests.A_start_that_falls_back_to_stopped_fails_at_once_with_the_service_exit_code'
    },
    @{
        # The mirror is deliberately not a failure - Running just after a stop is ordinary.
        Name = 'plan wait: a stop that reads running after the request is not a failure'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '        target == EntryStatus.Running && status == EntryStatus.Stopped;'
        To = '        status != target && status is EntryStatus.Stopped or EntryStatus.Running;'
        Test = 'StartFellOverTests.A_stop_that_still_reads_running_just_after_the_request_is_watched_rather_than_failed'
    },
    @{
        # The owner's decision of 2026-09-30: the exit code travels as errorCode.
        Name = 'plan wait: the number of a start that fell back is its exit code'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '            ErrorCode = code,'
        To = '            ErrorCode = 0,'
        Test = 'StartFellOverTests.A_start_that_falls_back_to_stopped_fails_at_once_with_the_service_exit_code'
    },
    @{
        # And the service's own code in the words only, never where a Windows number is expected.
        Name = 'plan wait: a service specific code travels in the words'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '                ? string.Create(CultureInfo.InvariantCulture, $"{words} ({progress.ServiceExitCode})")'
        To = '                ? string.Create(CultureInfo.InvariantCulture, $"{words}")'
        Test = 'StartFellOverTests.A_service_specific_code_travels_in_the_words_and_never_as_the_number'
    },
    @{
        # W-11: the second Ctrl+C ends the watching instead of waiting out a step in flight.
        Name = 'plan halt: abandonment ends a watch after the request'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '        internal bool Asked => Abandonment.IsCancellationRequested;'
        To = '        internal bool Asked => false;'
        Test = 'PlanRunHaltTests.Abandoning_during_the_watch_ends_it_rather_than_waiting_out_the_limit'
    },
    @{
        # A step not yet asked stops on an interruption exactly as one not yet reached.
        Name = 'plan halt: an interruption reaches a forward step still waiting to be asked'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '            Interruption.IsCancellationRequested,'
        To = '            false,'
        Test = 'PlanRunHaltTests.An_interruption_skips_a_step_going_forward_that_is_still_waiting_to_be_asked'
    },
    @{
        # A halted watch that asked nothing is a step never attempted, which the way back skips.
        Name = 'plan halt: a halted watch before any request is a step never attempted'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '            Settled.Halted when !asked =>'
        To = '            Settled.Halted when asked =>'
        Test = 'PlanRunHaltTests.Abandoning_while_an_entry_already_on_its_way_is_watched_reports_a_step_never_attempted'
    },
    @{
        # The time inside the request is what the sentence about the manager is read off.
        Name = 'plan: the manager answer is timed apart from the watching'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = '        var answered = Elapsed(asking);'
        To = '        var answered = Elapsed(asking) * 0;'
        Test = 'PlanRunProgressTests.The_time_the_manager_sits_on_a_request_is_what_outruns_the_limit'
    },
    @{
        # An ending exists for the entry stuck in StopPending, so it is never waited for as on its way.
        Name = 'plan: an ending is never waited for as though it were already on its way'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = '        if (step.Operation != StepOperation.Terminate'
        To = '        if (true'
        Test = 'EscalationRunTests.The_process_is_ended_when_the_stop_before_it_ran_out_of_time'
    },
    @{
        # The old measure, read off the whole step, would name a stop that kept making progress.
        Name = 'plan: a step that kept making progress past the ceiling is not named'
        File = 'src\Bws.Core\Planning\PlanRun.cs'
        From = '        [.. Results.Where(result => result.Answered > Ceiling.TotalMilliseconds)];'
        To = '        [.. Results.Where(result => result.Milliseconds > Ceiling.TotalMilliseconds)];'
        Test = 'PlanRunCeilingTests.A_step_that_kept_making_progress_past_the_ceiling_is_not_named'
    },
    @{
        # W-8, owner's decision of 2026-09-30: a warning, not a refusal.
        Name = 'plan: starting a disabled stopped entry is warned about'
        File = 'src\Bws.Core\Planning\RefusedStartWarnings.cs'
        From = '            && target.Status is EntryStatus.Stopped or EntryStatus.StopPending)'
        To = '            && target.Status is EntryStatus.Running)'
        Test = 'RefusedStartWarningTests.Starting_a_disabled_entry_that_is_stopped_says_Windows_will_refuse_it'
    },
    @{
        Name = 'plan: starting a paused entry is warned about'
        File = 'src\Bws.Core\Planning\RefusedStartWarnings.cs'
        From = '        if (target.Status is EntryStatus.Paused or EntryStatus.PausePending)'
        To = '        if (target.Status is EntryStatus.PausePending)'
        Test = 'RefusedStartWarningTests.Starting_a_paused_entry_says_a_start_does_not_resume_it'
    },
    @{
        # A paused service takes a stop, so only a start meets the refusal.
        Name = 'plan: only a start meets the refused start warnings'
        File = 'src\Bws.Core\Planning\RefusedStartWarnings.cs'
        From = '        if (action.Kind != ActionKind.Start)'
        To = '        if (action.Kind == ActionKind.SetStartType)'
        Test = 'RefusedStartWarningTests.Stopping_a_paused_entry_is_not_warned_about'
    },
    @{
        # Nothing refused a start the service did not survive, so the terminal does not say refused.
        Name = 'command line: a start that fell over is not called refused'
        File = 'src\Bws.Cli\PlanText.cs'
        From = '        StepOutcome.Failed => result.StoppedWhileStarting'
        To = '        StepOutcome.Failed => false'
        Test = 'StartFellOverSentenceTests.A_service_that_stopped_while_starting_is_not_called_refused'
    },
    @{
        # Nor does the window say "would not start".
        Name = 'plan panel: a start that fell over is not called would not start'
        File = 'src\Bws.Gui\ViewModels\PlanWords.cs'
        From = '        { StoppedWhileStarting: true } => Texts.Of('
        To = '        { StoppedWhileStarting: true, Outcome: StepOutcome.Skipped } => Texts.Of('
        Test = 'StartFellOverWordsGuards.A_service_that_stopped_while_starting_says_so_with_its_exit_code'
    },
    @{
        # Pauses that never grow turn a thirty second wait into three thousand questions.
        Name = 'plan cadence: the pauses grow'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '            pause = pause * 2 < Cadence ? pause * 2 : Cadence;'
        To = '            pause = FirstLook;'
        Test = 'PlanRunCadenceTests.A_long_wait_asks_the_manager_about_as_often_as_it_always_did'
    },
    @{
        # And pauses that never stop growing give up seconds after the deadline.
        Name = 'plan cadence: the pauses stop growing at the cadence'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '            pause = pause * 2 < Cadence ? pause * 2 : Cadence;'
        To = '            pause = pause * 2;'
        Test = 'PlanRunCadenceTests.A_long_wait_asks_the_manager_about_as_often_as_it_always_did'
    },
    @{
        # The costly mistake looking sooner could make: an entry that promised 50 ms and arrived
        # at 240 without raising its check point was always seen arriving. At the raw promise it
        # is reported as timed out.
        Name = 'plan cadence: a short promise is honoured to a whole cadence'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = 'now + Honoured(progress.WaitHint) : null;'
        To = 'now + progress.WaitHint : null;'
        Test = 'PlanRunCadenceTests.An_entry_that_promised_less_than_it_needed_is_given_the_patience_it_always_had'
    },
    @{
        # A floor of one cadence is not the same thing: an entry that promised 300 ms always had
        # 500, and a floor gives it 300.
        Name = 'plan cadence: a promise is rounded up rather than only floored'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = 'now + Honoured(progress.WaitHint) : null;'
        To = 'now + (progress.WaitHint > Cadence ? progress.WaitHint : Cadence) : null;'
        Test = 'PlanRunCadenceTests.An_entry_that_promised_less_than_it_needed_is_given_the_patience_it_always_had'
    },

    # W3 / S-5, 2026-09-29: RequiredByPass asks several entries at once, each call with its own
    # manager handle. What a parallel loop gets wrong without a sound is held by these two.
    @{
        # One at a time again: the answers are the same and the pass is ten times slower.
        Name = 'required by: the entries are asked about at the same time'
        File = 'src\Bws.Core\RequiredByPass.cs'
        From = '            new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism },'
        To = '            new ParallelOptions { MaxDegreeOfParallelism = 1 },'
        Test = 'RequiredByPassParallelTests.The_entries_are_asked_about_at_the_same_time_rather_than_one_after_another'
    },
    @{
        # Every entry is asked about and every answer lands - in somebody else's entry.
        Name = 'required by: each answer lands in its own entry'
        File = 'src\Bws.Core\RequiredByPass.cs'
        From = 'RequiredBy = catalog.ReadDependents(entries[index].ServiceName)'
        To = 'RequiredBy = catalog.ReadDependents(entries[entries.Count - 1 - index].ServiceName)'
        Test = 'RequiredByPassParallelTests.Asked_several_at_once_every_entry_keeps_its_own_answer_and_the_order_it_came_in'
    },

    # W4 / S-1, 2026-09-29, the owner's decision in ADR-13: a full reading after a plan, after a
    # change to what is installed and for a new family in the question KEEPS what the window knows
    # about files, and F5 verifies everything. Plus 468: every pass asks through a fresh inspector.
    @{
        Name = 'kept files: the reading after a plan keeps what is known'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.cs'
        From = 'public Task LoadKeepingAsync() => ReadMachineAsync(Relisting.Keeping);'
        To = 'public Task LoadKeepingAsync() => ReadMachineAsync(Relisting.Afresh);'
        Test = 'KeptFileAnswersTests.A_plan_keeps_what_the_window_knows_about_files'
    },
    @{
        # The other half: whatever went stale since the last F5 goes away at the next one.
        Name = 'kept files: F5 keeps nothing'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.cs'
        From = 'Task.WhenAll(ReadMachineAsync(Relisting.Afresh), History.Fresh());'
        To = 'Task.WhenAll(ReadMachineAsync(Relisting.Keeping), History.Fresh());'
        Test = 'KeptFileAnswersTests.F5_verifies_every_file_again'
    },
    @{
        Name = 'kept files: a keeping reading takes the answers the rows hold'
        File = 'src\Bws.Gui\ViewModels\Readings.cs'
        From = '        if (how == Relisting.Keeping)'
        To = '        if (how == Relisting.Afresh)'
        Test = 'KeptFileAnswersTests.A_plan_keeps_what_the_window_knows_about_files'
    },
    @{
        # Multi-line, because the same call stands twice in the file.
        Name = 'kept files: a change to what is installed keeps'
        File = 'src\Bws.Gui\ViewModels\Readings.cs'
        From = '                // installed or removed brings its own path, and that one is verified.
                await LoadEverything(Relisting.Keeping).ConfigureAwait(true);'
        To = '                // installed or removed brings its own path, and that one is verified.
                await LoadEverything(Relisting.Afresh).ConfigureAwait(true);'
        Test = 'KeptFileAnswersTests.After_something_is_installed_only_its_file_is_verified'
    },
    @{
        Name = 'kept files: a new family in the question keeps the signatures'
        File = 'src\Bws.Gui\ViewModels\Readings.cs'
        From = '            // signatures already on screen were verified since the last one.
            await LoadEverything(Relisting.Keeping).ConfigureAwait(true);'
        To = '            // signatures already on screen were verified since the last one.
            await LoadEverything(Relisting.Afresh).ConfigureAwait(true);'
        Test = 'KeptFileAnswersTests.A_column_that_needs_another_family_does_not_verify_signatures_again'
    },
    @{
        Name = 'kept files: an entry already holding an answer is not asked about again'
        File = 'src\Bws.Core\SecondPass.cs'
        From = '                && !Answered(entry)'
        To = ''
        Test = 'SecondPassKeepTests.A_file_already_answered_is_kept_and_not_asked_about_again'
    },
    @{
        # Keyed by the FILE: a moved binary is a different key and has to be verified.
        Name = 'kept files: a changed path is not kept'
        File = 'src\Bws.Core\SecondPass.cs'
        From = 'kept.Add(entry.BinaryFile.Outcome == ReadOutcome.Present && known.TryGetValue(entry.BinaryFile.Value!, out var answer)'
        To = 'kept.Add(entry.BinaryFile.Outcome == ReadOutcome.Present && known.TryGetValue(known.Keys.First(), out var answer)'
        Test = 'SecondPassKeepTests.A_new_path_and_a_changed_path_are_verified_and_nothing_else_is'
    },
    @{
        # 468. The window's own inspector, whose publisher memory outlived every F5.
        Name = 'one pass: the second phase asks through a fresh inspector'
        File = 'src\Bws.Core\SecondPass.cs'
        From = 'var answers = Ask(files, inspector.ForOnePass(), degreeOfParallelism);'
        To = 'var answers = Ask(files, inspector, degreeOfParallelism);'
        Test = 'SecondPassKeepTests.Every_pass_asks_through_an_inspector_that_remembers_nothing_from_the_last'
    },
    @{
        Name = 'one pass: the Windows inspector hands out an empty one'
        File = 'src\Bws.Core\WindowsBinaryInspector.cs'
        From = 'public IBinaryInspector ForOnePass() => new WindowsBinaryInspector(networkPaths);'
        To = 'public IBinaryInspector ForOnePass() => this;'
        Test = 'SecondPassKeepTests.The_windows_inspector_starts_every_pass_empty'
    },

    # W6, 2026-09-29: the plan sheet. G-6 - the shadow lies under the sheet rather than on it, so
    # a caret blink stops re-blurring the sheet and its text gets ClearType back. G-7 - past twenty
    # lines a block of commands stands in one read-only field. And the footer names the entries the
    # machine does not work without in one sentence per kind. Six entries above were re-anchored the
    # same day onto CommandBlock and still break the same behaviour.
    @{
        Name = 'plan sheet: past twenty lines the commands stand at once'
        File = 'src\Bws.Gui\ViewModels\CommandBlock.cs'
        From = '    public bool AtOnce => Lines.Count > ListedUpTo;'
        To = '    public bool AtOnce => Lines.Count > ListedUpTo + 1;'
        Test = 'PlanSheetCostGuards.Past_twenty_commands_the_sheet_holds_them_in_one_field_and_builds_no_box_per_line'
    },
    @{
        # The whole saving: a list that still holds the lines builds a box per line.
        Name = 'plan sheet: past twenty lines the list holds none'
        File = 'src\Bws.Gui\ViewModels\CommandBlock.cs'
        From = '    public IReadOnlyList<string> Listed => AtOnce ? [] : Lines;'
        To = '    public IReadOnlyList<string> Listed => Lines;'
        Test = 'PlanSheetCostGuards.Past_twenty_commands_the_sheet_holds_them_in_one_field_and_builds_no_box_per_line'
    },
    @{
        # The field shows only past the threshold - turned round, it stands under twenty boxes.
        Name = 'plan sheet: the field of commands shows only past twenty lines'
        File = 'src\Bws.Gui\Themes\Plan.xaml'
        From = '<DataTrigger Binding="{Binding AtOnce}" Value="False">'
        To = '<DataTrigger Binding="{Binding AtOnce}" Value="True">'
        Test = 'PlanSheetCostGuards.Past_twenty_commands_the_sheet_holds_them_in_one_field_and_builds_no_box_per_line'
    },
    @{
        # "Copy all" goes over one command - the trigger lives in the button's style since W6.
        Name = 'plan sheet: copy all goes over a single command, from its style'
        File = 'src\Bws.Gui\Themes\PlanLines.xaml'
        From = '<DataTrigger Binding="{Binding Several}" Value="False">'
        To = '<DataTrigger Binding="{Binding Several}" Value="True">'
        Test = 'PlanViewGuards.Copy_all_stands_over_several_commands_with_all_of_them_on_it_and_not_over_one'
    },
    @{
        Name = 'plan sheet: the way back is made once per run and kept'
        File = 'src\Bws.Gui\ViewModels\Planned.Sections.cs'
        From = '            if (!ReferenceEquals(_wayBack.Of, run))'
        To = '            if (!ReferenceEquals(_wayBack.Block, run))'
        Test = 'PlanSheetListsTests.The_way_back_is_made_once_per_run_and_the_commands_go_once_something_was_asked'
    },
    @{
        # G-6. Without the sibling the sheet stands flat on the window.
        Name = 'plan sheet: a shadow lies under the sheet'
        File = 'src\Bws.Gui\PlanView.xaml'
        From = '        <Border Style="{StaticResource PlanSheetShadow}" />'
        To = ''
        Test = 'PlanSheetCostGuards.The_sheet_wears_no_effect_and_its_shadow_lies_under_it_in_the_same_rectangle'
    },
    @{
        # G-6. The effect back ON the sheet - a blur of the whole sheet on every caret blink.
        Name = 'plan sheet: the sheet itself wears no effect'
        File = 'src\Bws.Gui\PlanView.xaml'
        From = '<Border x:Name="Sheet" Style="{StaticResource PlanSheet}">'
        To = '<Border x:Name="Sheet" Style="{StaticResource PlanSheetShadow}">'
        Test = 'PlanSheetCostGuards.The_sheet_wears_no_effect_and_its_shadow_lies_under_it_in_the_same_rectangle'
    },
    @{
        # The search box keeps its library chrome invisible through BasedOn since W6 split it.
        Name = 'search field: the box finds its chrome answered transparent through its base'
        File = 'src\Bws.Gui\Themes\SearchField.xaml'
        From = '<Style x:Key="SearchFieldBox" TargetType="TextBox" BasedOn="{StaticResource ChromelessBox}">'
        To = '<Style x:Key="SearchFieldBox" TargetType="TextBox" BasedOn="{StaticResource {x:Type TextBox}}">'
        Test = 'PlanSheetCostGuards.Both_boxes_in_a_frame_of_their_own_find_the_library_chrome_answered_transparent'
    },
    @{
        Name = 'plan sheet: the field of commands finds its chrome answered transparent'
        File = 'src\Bws.Gui\Themes\Plan.xaml'
        From = '<Style x:Key="PlanCommandsField" TargetType="TextBox" BasedOn="{StaticResource ChromelessBox}">'
        To = '<Style x:Key="PlanCommandsField" TargetType="TextBox" BasedOn="{StaticResource {x:Type TextBox}}">'
        Test = 'PlanSheetCostGuards.Both_boxes_in_a_frame_of_their_own_find_the_library_chrome_answered_transparent'
    },
    @{
        # The footer's sentences: one per plan again, and a scope stop hides its steps.
        Name = 'plan sheet: entries the machine cannot lose are named in one sentence'
        File = 'src\Bws.Gui\ViewModels\PlanWords.cs'
        From = '            if (kind.Key is PlanWarningKind.CriticalService or PlanWarningKind.CriticalStartType)'
        To = '            if (kind.Key is PlanWarningKind.CriticalStartType)'
        Test = 'CriticalEntryWindowGuards.Entries_the_machine_does_not_work_without_are_named_in_one_sentence_per_kind'
    },
    @{
        Name = 'plan sheet: an entry named by two plans is named once'
        File = 'src\Bws.Gui\ViewModels\PlanWords.cs'
        From = 'kind.SelectMany(warning => warning.Related).Distinct(StringComparer.OrdinalIgnoreCase)'
        To = 'kind.SelectMany(warning => warning.Related)'
        Test = 'CriticalEntryWindowGuards.Entries_the_machine_does_not_work_without_are_named_in_one_sentence_per_kind'
    },
    @{
        # Gathered, the process warnings would pin every entry's processes on the first one.
        Name = 'plan sheet: a process that takes others with it is not gathered'
        File = 'src\Bws.Gui\ViewModels\PlanWords.cs'
        From = '            if (kind.Key is PlanWarningKind.CriticalService or PlanWarningKind.CriticalStartType)'
        To = '            if (kind.Key is PlanWarningKind.CriticalService or PlanWarningKind.CriticalStartType or PlanWarningKind.TerminationTakesWithIt)'
        Test = 'CriticalEntryWindowGuards.A_process_that_takes_others_with_it_keeps_a_sentence_of_its_own'
    },

    # PACKAGE A OF THE EXTERNAL STABILITY REPORT, 2026-09-29 (docs/ANALIZA-RAPORTU-STABILNOSCI-20260929.md,
    # backlog 486). A run that is going keeps its sheet, its report and the window's hold on it - G-1 and
    # G-2 - and readings that were lost or left stale come back - G-5, G-6, X-1. NOT HERE, AND SAID: the
    # refresh after a run moved out of the catch meant for a refused plan (G-2) has no entry, because
    # nothing in the test host can make that refresh throw an InvalidOperationException now that X-1
    # keeps a name to one row. The move is one statement and the comment beside it says why.
    @{
        Name = 'run holds the sheet: Escape during a run is taken and does nothing'
        File = 'src\Bws.Gui\MainWindow.Keyboard.cs'
        From = '                    || _model.Planned.Busy'
        To = '                    || false'
        Test = 'RunHoldsTheSheetGuards.Escape_and_the_close_mark_leave_a_running_sheet_where_it_is'
    },
    @{
        Name = 'run holds the sheet: the close mark is grey while a run is going'
        File = 'src\Bws.Gui\ViewModels\Planned.Running.cs'
        From = 'public bool CanClose => !Busy;'
        To = 'public bool CanClose => true;'
        Test = 'RunHoldsTheSheetGuards.Escape_and_the_close_mark_leave_a_running_sheet_where_it_is'
    },
    @{
        Name = 'run holds the sheet: the sheet will not be put away mid-run'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        From = 'if (!Showing || Busy)'
        To = 'if (!Showing)'
        Test = 'RunHoldsTheSheetGuards.Nothing_else_takes_the_sheet_or_starts_a_second_run_while_one_is_going'
    },
    @{
        Name = 'run holds the sheet: no new plan is put over a running one'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        From = 'if (Busy || (!plan.IsRunnable && plan.Problems.Count == 0))'
        To = 'if (!plan.IsRunnable && plan.Problems.Count == 0)'
        Test = 'RunHoldsTheSheetGuards.Nothing_else_takes_the_sheet_or_starts_a_second_run_while_one_is_going'
    },
    @{
        # Without it the sheet still refuses the plan - silently, which is what the test asks about.
        Name = 'run holds the sheet: a preview asked mid-run says why it was refused'
        File = 'src\Bws.Gui\MainWindow.Menu.cs'
        From = '        if (_model.Planned.Busy)
        {
            _model.Says.CouldNotDo(Texts.Of("gui.plan.blocked.running"));'
        To = '        if (false)
        {
            _model.Says.CouldNotDo(Texts.Of("gui.plan.blocked.running"));'
        Test = 'RunHoldsTheSheetGuards.Nothing_else_takes_the_sheet_or_starts_a_second_run_while_one_is_going'
    },
    @{
        Name = 'run holds the sheet: the details do not open beside a running sheet'
        File = 'src\Bws.Gui\MainWindow.Details.cs'
        From = '        if (_model.Planned.Busy)'
        To = '        if (false)'
        Test = 'RunHoldsTheSheetGuards.Nothing_else_takes_the_sheet_or_starts_a_second_run_while_one_is_going'
    },
    @{
        Name = 'run holds the sheet: the window asks the run it holds, not only the sheet'
        File = 'src\Bws.Gui\MainWindow.Carrying.cs'
        From = 'if (_running is { IsCompleted: false } || !_model.Planned.CanCarryOut'
        To = 'if (!_model.Planned.CanCarryOut'
        Test = 'RunHoldsTheSheetGuards.A_press_while_the_window_holds_a_run_starts_nothing'
    },
    @{
        Name = 'run holds the sheet: a run of nothing does not say done'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        From = 'run.Runs.Count == 0 ? Texts.Of("gui.plan.notice.nothingRun")'
        To = 'run.Runs.Count == -1 ? Texts.Of("gui.plan.notice.nothingRun")'
        Test = 'RunHoldsTheSheetGuards.A_run_that_carried_nothing_out_does_not_say_done'
    },
    @{
        Name = 'owed reading: a full reading asked for while another is out is owed'
        File = 'src\Bws.Gui\ViewModels\Readings.cs'
        From = '_owed = how == Relisting.Afresh || _owed == Relisting.Afresh ? Relisting.Afresh : Relisting.Keeping;'
        To = '_owed = null;'
        Test = 'OwedReadingTests.A_full_reading_asked_for_while_a_tick_is_out_is_carried_out_after_it'
    },
    @{
        # Two calls of Owed in the file, so the anchor carries the comment above the tick's one.
        Name = 'owed reading: the tick pays what was owed'
        File = 'src\Bws.Gui\ViewModels\Readings.cs'
        From = '        // see _owed. After the flag is down, so it goes through the same door as any other.
        await Owed().ConfigureAwait(true);'
        To = '        // see _owed. After the flag is down, so it goes through the same door as any other.
        await Task.CompletedTask.ConfigureAwait(true);'
        Test = 'OwedReadingTests.A_full_reading_asked_for_while_a_tick_is_out_is_carried_out_after_it'
    },
    @{
        Name = 'owed reading: a tick that works lowers the failed flag'
        File = 'src\Bws.Gui\ViewModels\Readings.cs'
        From = 'var recovered = _failed;
        _failed = false;'
        To = 'var recovered = _failed;'
        Test = 'OwedReadingTests.A_tick_that_works_after_one_that_failed_brings_the_window_back'
    },
    @{
        Name = 'owed reading: a recovered tick says so even when nothing moved'
        File = 'src\Bws.Gui\ViewModels\Readings.cs'
        From = 'var recovered = _failed;'
        To = 'var recovered = false;'
        Test = 'OwedReadingTests.A_tick_that_works_after_one_that_failed_brings_the_window_back'
    },
    @{
        Name = 'one row per name: a name handed over twice is one row'
        File = 'src\Bws.Gui\ViewModels\RowIndex.cs'
        From = '            if (!seen.Add(entry.ServiceName))
            {
                continue;
            }'
        To = '            seen.Add(entry.ServiceName);'
        Test = 'OwedReadingTests.A_name_handed_over_twice_is_one_row'
    },

    # Package B2 of the stability report, 2026-09-30 (W-3, W-6): what ending a process sets off, read
    # when the plan is built, and who lives in the process read again just before it is ended.
    @{
        # Re-anchored 2026-09-30 (backlog 501): the restart warning is built with its delays in
        # AddWarnings rather than through Warn.
        Name = 'aftermath: a restart in the recovery list is said'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = '        if (restarts.Count > 0)'
        To = '        if (restarts.Count > 99)'
        Test = 'AftermathTests.A_restart_in_the_recovery_list_is_said_before_anything_is_ended'
    },
    @{
        Name = 'aftermath: a program in the recovery list is said'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = '        Warn(warnings, target, Having(recovery, RecoveryAction.RunProgram), PlanWarningKind.RecoveryRunsProgram);'
        To = ''
        Test = 'AftermathTests.A_neighbour_that_sets_a_program_off_is_named_and_the_entry_is_asked_first'
    },
    @{
        Name = 'aftermath: an item with no name is said rather than dropped'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = '        Warn(warnings, target, Having(recovery, RecoveryAction.Unnamed), PlanWarningKind.RecoveryUnnamed);'
        To = ''
        Test = 'AftermathTests.An_item_this_tool_has_no_name_for_is_said_rather_than_guessed_at'
    },
    @{
        Name = 'aftermath: a computer restart is a refusal'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = '        if (restarting.Count > 0)'
        To = '        if (restarting.Count > 99)'
        Test = 'AftermathTests.A_computer_restart_anywhere_in_a_neighbours_list_is_a_refusal'
    },
    @{
        # First only: the third item of a list is the one this refusal exists for, because nobody can
        # read which failure the manager is on.
        # Re-anchored 2026-09-30 (backlog 501): the list holds items with a delay, not bare kinds.
        # Re-anchored 2026-10-07 (backlog 541): the reading carries the reset period beside the items.
        Name = 'aftermath: an item counts anywhere in the list, not only first'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = 'one.Actions.Value!.Items.Any(item => item.Action == action)'
        To = 'one.Actions.Value!.Items.FirstOrDefault().Action == action'
        Test = 'AftermathTests.A_computer_restart_anywhere_in_a_neighbours_list_is_a_refusal'
    },
    @{
        Name = 'aftermath: a recovery list the manager will not show is a refusal'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = '.Where(one => one.Actions.Outcome == ReadOutcome.Denied)'
        To = '.Where(one => one.Actions.Outcome == ReadOutcome.NotRead)'
        Test = 'AftermathTests.A_recovery_list_the_manager_will_not_show_is_a_refusal'
    },
    @{
        Name = 'aftermath: a critical process is a refusal'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = 'ReadOutcome.Present when facts.Critical.Value => new PlanProblem(PlanProblemKind.ProcessIsCritical'
        To = 'ReadOutcome.Present when !facts.Critical.Value && facts.Critical.ErrorCode == -1 => new PlanProblem(PlanProblemKind.ProcessIsCritical'
        Test = 'AftermathTests.A_critical_process_is_refused_before_anything_else_is_asked'
    },
    @{
        Name = 'aftermath: not knowing whether the process is critical is a refusal'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = '        ReadOutcome.Denied => new PlanProblem(PlanProblemKind.AftermathUnreadable, target.ServiceName, []),'
        To = '        ReadOutcome.Denied => null,'
        Test = 'AftermathTests.Not_knowing_whether_the_process_is_critical_is_a_refusal'
    },
    @{
        # The wiring rather than the predicate: the question asked, and asked where the right to end
        # the process is asked - before the road is worked out.
        Name = 'aftermath: the critical question is asked beside the right to end'
        File = 'src\Bws.Core\Planning\ForcedStop.cs'
        From = '            return Aftermath.Critical(facts, target);'
        To = '            return null;'
        Test = 'AftermathTests.A_critical_process_is_refused_before_anything_else_is_asked'
    },
    @{
        # The cascade gets a polite stop, and one that fails holds the ending - so nothing in it dies
        # with the process, and asking about it would warn about restarts that never come.
        Name = 'aftermath: the cascade is never asked'
        File = 'src\Bws.Core\Planning\ForcedStop.cs'
        From = '        return Aftermath.Weigh(processes, facts, target, InOrder(asked, sharing), action, processId);'
        To = '        return Aftermath.Weigh(processes, facts, target, [.. cascade, .. InOrder(asked, sharing)], action, processId);'
        Test = 'AftermathTests.The_cascade_is_never_asked_because_nothing_in_it_dies_with_the_process'
    },
    @{
        Name = 'ending: a service that moved into the process holds the ending'
        File = 'src\Bws.Core\Planning\PlanRunner.Ending.cs'
        From = '        return newcomers.Count > 0'
        To = '        return newcomers.Count > 99'
        Test = 'EndingCompanyRunTests.A_service_that_moved_into_the_process_after_the_preview_holds_the_ending'
    },
    @{
        Name = 'ending: a neighbour the plan named is not a stranger'
        File = 'src\Bws.Core\Planning\PlanRunner.Ending.cs'
        From = 'var named = new HashSet<string>(step.TakesWithIt ?? [], StringComparer.OrdinalIgnoreCase) { step.ServiceName };'
        To = 'var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { step.ServiceName };'
        Test = 'EndingCompanyRunTests.A_neighbour_the_plan_named_is_not_a_stranger'
    },
    @{
        Name = 'ending: a running dependant outside the process holds the ending'
        File = 'src\Bws.Core\Planning\PlanRunner.Ending.cs'
        From = 'running.Contains(dependent) && !inside.Contains(dependent)'
        To = 'running.Contains(dependent) && inside.Contains(dependent)'
        Test = 'EndingCompanyRunTests.A_dependant_that_started_outside_the_process_after_the_preview_holds_the_ending'
    },
    @{
        Name = 'ending: a stopped dependant needs nothing from the process'
        File = 'src\Bws.Core\Planning\PlanRunner.Ending.cs'
        From = 'running.Contains(dependent) && !inside.Contains(dependent)'
        To = '!inside.Contains(dependent)'
        Test = 'EndingCompanyRunTests.A_stopped_dependant_and_one_inside_the_process_hold_nothing_back'
    },
    @{
        Name = 'ending: a dependant inside the process dies with it rather than holding it'
        File = 'src\Bws.Core\Planning\PlanRunner.Ending.cs'
        From = 'running.Contains(dependent) && !inside.Contains(dependent)'
        To = 'running.Contains(dependent)'
        Test = 'EndingCompanyRunTests.A_stopped_dependant_and_one_inside_the_process_hold_nothing_back'
    },
    @{
        Name = 'ending: a company that cannot be listed ends nothing'
        File = 'src\Bws.Core\Planning\PlanRunner.Ending.cs'
        From = '        if (!statuses.IsPresent)'
        To = '        if (!statuses.IsPresent && statuses.ErrorCode == -1)'
        Test = 'EndingCompanyRunTests.A_process_whose_company_cannot_be_read_is_not_ended'
    },
    @{
        Name = 'ending: dependants that cannot be read end nothing'
        File = 'src\Bws.Core\Planning\PlanRunner.Ending.cs'
        From = '            if (dependents.Outcome == ReadOutcome.Denied)'
        To = '            if (dependents.Outcome == ReadOutcome.NotRead)'
        Test = 'EndingCompanyRunTests.A_process_whose_company_cannot_be_read_is_not_ended'
    },
    @{
        # Watching for Stopped alone is how a step waited the whole limit over a service running again.
        Name = 'ending: an entry back in another process ends the watch'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '            ended: step.Operation == StepOperation.Terminate ? step.ProcessId : null);'
        To = '            ended: null);'
        Test = 'EndingCompanyRunTests.An_entry_back_in_a_new_process_is_said_at_once_rather_than_after_the_limit'
    },
    @{
        # Measured: the manager marks a restarting entry start pending with no process for 31-45 ms.
        Name = 'ending: no process yet is not the entry back'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = 'ended is { } gone && progress.ProcessId != 0 && progress.ProcessId != (uint)gone;'
        To = 'ended is { } gone && progress.ProcessId != (uint)gone;'
        Test = 'EndingCompanyRunTests.An_entry_being_started_with_no_process_yet_is_watched_on'
    },
    @{
        Name = 'ending: an entry back says it came back rather than that it fell over'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '            Settled.Over when step.Operation == StepOperation.Terminate =>'
        To = '            Settled.Over when step.Operation == StepOperation.SetStartType =>'
        Test = 'EndingCompanyRunTests.An_entry_back_in_a_new_process_is_said_at_once_rather_than_after_the_limit'
    },
    @{
        Name = 'aftermath words: the terminal reaches the recovery warnings'
        File = 'src\Bws.Cli\PlanText.Starting.cs'
        From = '        _ => Aftermath(warning)'
        To = '        _ => throw new ArgumentOutOfRangeException(nameof(warning))'
        Test = 'AftermathSentenceTests.Each_warning_has_its_own_sentence_in_both_numbers'
    },
    @{
        Name = 'aftermath words: the terminal does not call an ending undone at once refused'
        File = 'src\Bws.Cli\PlanText.cs'
        From = '        StepOutcome.Failed when result.StartedAgain => Texts.Of('
        To = '        StepOutcome.Failed when result.StartedAgain && result.ErrorCode == -1 => Texts.Of('
        Test = 'AftermathSentenceTests.An_ending_the_manager_undid_at_once_is_not_called_refused'
    },
    @{
        Name = 'aftermath words: the window reaches the recovery warnings'
        File = 'src\Bws.Gui\ViewModels\PlanWords.Starting.cs'
        From = '        _ => Aftermath(warning)'
        To = '        _ => throw new ArgumentOutOfRangeException(nameof(warning))'
        Test = 'AftermathWordsGuards.Each_warning_has_its_own_sentence_in_both_numbers'
    },
    @{
        Name = 'aftermath words: the window names the new process of an ending undone at once'
        File = 'src\Bws.Gui\ViewModels\PlanWords.cs'
        From = '        { StartedAgain: true } => Texts.Of('
        To = '        { StartedAgain: true, ErrorCode: -1 } => Texts.Of('
        Test = 'AftermathWordsGuards.An_ending_the_manager_undid_at_once_names_the_new_process'
    },
    @{
        # Package D, W-5 (2026-09-30): a step putting something back gives back only what the run took.
        Name = 'put back: nothing is given back that the run never took down'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = 'StepReason.Restore when !NetEffect.TookDown(results, step.ServiceName) => SkipReason.NothingToPutBack,'
        To = 'StepReason.Restore when NetEffect.TookDown(results, step.ServiceName) => SkipReason.NothingToPutBack,'
        Test = 'PutBackTests.Stop_pressed_before_a_restart_gives_nothing_back'
    },
    @{
        Name = 'put back: a dependant somebody else stopped is left stopped'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = 'StepReason.Restore when !NetEffect.TookDown(results, step.ServiceName) => SkipReason.NothingToPutBack,'
        To = 'StepReason.Restore when !NetEffect.TookDown(results, step.ServiceName) && step.ServiceName.Length < 0 => SkipReason.NothingToPutBack,'
        Test = 'PutBackTests.A_dependant_somebody_else_stopped_before_the_run_is_left_stopped'
    },
    @{
        Name = 'put back: an interrupted restart of a selection starts nothing it did not stop'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = 'StepReason.Restore when !NetEffect.TookDown(results, step.ServiceName) => SkipReason.NothingToPutBack,'
        To = 'StepReason.Restore when step.ServiceName.Length < 0 => SkipReason.NothingToPutBack,'
        Test = 'PutBackTests.An_interrupted_restart_of_a_selection_starts_nothing_it_did_not_stop'
    },
    @{
        Name = 'put back: an ending counts as taking down what it ended'
        File = 'src\Bws.Core\Planning\NetEffect.cs'
        From = '&& moved.Last is StepOperation.Stop or StepOperation.Terminate;'
        To = '&& moved.Last is StepOperation.Stop;'
        Test = 'PutBackTests.An_entry_its_ending_took_down_is_given_back_although_its_own_stop_was_refused'
    },
    @{
        Name = 'put back: an ending Windows undid at once still took the entry down'
        File = 'src\Bws.Core\Planning\NetEffect.cs'
        From = '                && !(endingCounts && result.StartedAgain))'
        To = '                && true)'
        Test = 'PutBackTests.An_ending_Windows_answered_by_starting_the_entry_again_still_took_it_down'
    },
    @{
        Name = 'put back: a neighbour found stopped was not in the process the ending took'
        File = 'src\Bws.Core\Planning\NetEffect.cs'
        From = 'if (!moves.ContainsKey(name) && !foundStopped.Contains(name))'
        To = 'if (!moves.ContainsKey(name))'
        Test = 'PutBackTests.A_neighbour_found_stopped_is_not_started_after_the_process_ends'
    },
    @{
        Name = 'restart: a stopped entry is planned as its start'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = 'action.Kind == ActionKind.Restart && target.Status == EntryStatus.Stopped'
        To = 'action.Kind == ActionKind.Restart && target.Status == EntryStatus.Running'
        Test = 'RestartOfStoppedTests.A_restart_of_a_stopped_entry_is_one_start_asked_for'
    },
    @{
        Name = 'restart: a running entry is still a stop and a start put back'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = 'action.Kind == ActionKind.Restart && target.Status == EntryStatus.Stopped'
        To = 'action.Kind == ActionKind.Restart'
        Test = 'RestartOfStoppedTests.A_restart_of_a_running_entry_is_still_a_stop_and_a_start_put_back'
    },
    @{
        Name = 'restart: the plan says a restart of a stopped entry only starts it'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = 'if (asked != action)'
        To = 'if (asked == action)'
        Test = 'RestartOfStoppedTests.A_restart_of_a_stopped_entry_is_one_start_asked_for'
    },
    @{
        Name = 'restart: that sentence comes first'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = 'warnings.Insert(0, new PlanWarning(PlanWarningKind.RestartOnlyStarts, target.ServiceName));'
        To = 'warnings.Add(new PlanWarning(PlanWarningKind.RestartOnlyStarts, target.ServiceName));'
        Test = 'RestartOfStoppedTests.A_restart_of_a_stopped_disabled_entry_warns_the_way_its_start_does'
    },
    @{
        Name = 'restart: a stopped entry takes no dependant down'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = 'var blocking = asked.Kind is ActionKind.Start'
        To = 'var blocking = action.Kind is ActionKind.Start'
        Test = 'RestartOfStoppedTests.A_restart_of_a_stopped_entry_takes_no_dependant_down'
    },
    @{
        Name = 'restart: a stopped disabled entry is warned about rather than refused'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = 'if (StuckDown(asked.Kind, target,'
        To = 'if (StuckDown(action.Kind, target,'
        Test = 'RestartOfStoppedTests.A_restart_of_a_stopped_disabled_entry_warns_the_way_its_start_does'
    },
    @{
        Name = 'put back words: the terminal has a sentence for a step with nothing to put back'
        File = 'src\Bws.Cli\Resources\cli.en.json'
        From = '  "cli.run.outcome.nothingToPutBack": "not started, this run never stopped it",'
        To = ''
        Test = 'PutBackOutputTests.Every_reason_a_step_was_skipped_has_a_sentence_of_its_own'
    },
    @{
        Name = 'start words: the terminal reaches the sentences about the start in a plan'
        File = 'src\Bws.Cli\PlanText.Warnings.cs'
        From = '        _ => Starting(warning)'
        To = '        _ => Aftermath(warning)'
        Test = 'PlanSentenceGuards.Every_kind_of_warning_has_words_of_its_own'
    },
    @{
        Name = 'start words: the window reaches the sentences about the start in a plan'
        File = 'src\Bws.Gui\ViewModels\PlanWords.Warnings.cs'
        From = '        _ => Starting(warning)'
        To = '        _ => Aftermath(warning)'
        Test = 'PlanSentenceGuards.Every_kind_of_warning_has_words_of_its_own'
    },
    @{
        # PACKAGE GUI OF THE STABILITY REPORT, 2026-09-30 - backlog 497 (the second way of stopping a
        # run in the window) and W-4 / backlog 496 (the offer to stop dependants in the way). The
        # design is docs/PROJEKT-PRZERWANIE-I-ZALEZNI-20260930.md. Every entry from here to the end is
        # one of its guards. What has no entry, said rather than left out: that the wait is measured on
        # the monotonic clock (a mutant would have to change a type - UnderwayTests holds it with a
        # clock whose wall time jumps), and that the offer's count names each dependant once (no guard
        # builds two targets sharing one).
        #
        # The door refuses the second level before the first and before the wait.
        Name = 'abandon: the door refuses before the first level and the wait'
        File = 'src\Bws.Gui\ViewModels\Underway.cs'
        From = '        if (!CanAbandon)'
        To = '        if (!_running)'
        Test = 'AbandonGuards.A_double_press_on_Interrupt_never_reaches_the_second_level'
    },
    @{
        Name = 'abandon: the second level waits three seconds and not none'
        File = 'src\Bws.Gui\ViewModels\Underway.cs'
        From = '_clock().Elapsed - _interruptedAt >= ArmsAfter'
        To = '_clock().Elapsed - _interruptedAt >= TimeSpan.Zero'
        Test = 'UnderwayTests.The_second_level_is_offered_only_after_an_interrupt_and_the_wait'
    },
    @{
        # A double click on Interrupt must not start the wait again from the second press.
        Name = 'abandon: a second interrupt keeps the first one''s time'
        File = 'src\Bws.Gui\ViewModels\Underway.cs'
        From = '        if (!_interrupted)
        {
            _interrupted = true;
            _interruptedAt = _clock().Elapsed;
        }'
        To = '        _interrupted = true;
        _interruptedAt = _clock().Elapsed;'
        Test = 'UnderwayTests.A_second_interrupt_keeps_the_first_one_s_time'
    },
    @{
        # The window cancels the second token only when the sheet agrees - the mutant cancels it on
        # every press, which a press arriving before the offer then reaches.
        Name = 'abandon: the window cancels the second token only when the sheet agrees'
        File = 'src\Bws.Gui\MainWindow.Carrying.cs'
        From = '            if (_model.Planned.Underway.Abandon())
            {
                _abandoning?.Cancel();
            }'
        To = '            _model.Planned.Underway.Abandon();
            _abandoning?.Cancel();'
        Test = 'AbandonGuards.A_double_press_on_Interrupt_never_reaches_the_second_level'
    },
    @{
        Name = 'abandon: the window hands the run its second token'
        File = 'src\Bws.Gui\MainWindow.Carrying.cs'
        From = '                abandoning.Token,'
        To = '                CancellationToken.None,'
        Test = 'AbandonGuards.The_second_level_appears_to_the_left_and_reaches_the_run'
    },
    @{
        # To the LEFT of Interrupt, in a column of its own. The mutant stands it where the button
        # that carries the plan out stands.
        Name = 'abandon: the second button stands left of Interrupt'
        File = 'src\Bws.Gui\PlanFooter.xaml'
        From = '                <Button Grid.Column="1"
                        x:Name="AbandonButton"'
        To = '                <Button Grid.Column="3"
                        x:Name="AbandonButton"'
        Test = 'AbandonGuards.The_second_level_appears_to_the_left_and_reaches_the_run'
    },
    @{
        Name = 'interrupt: a pressed Interrupt goes grey'
        File = 'src\Bws.Gui\PlanFooter.xaml'
        From = '                        IsEnabled="{Binding Underway.CanInterrupt}"
                        Click="InterruptRequested" />'
        To = '                        Click="InterruptRequested" />'
        Test = 'AbandonGuards.A_double_press_on_Interrupt_never_reaches_the_second_level'
    },
    @{
        Name = 'interrupt: the sheet says the run was asked to stop'
        File = 'src\Bws.Gui\ViewModels\Underway.cs'
        From = '                : _interrupted ? Texts.Of("gui.plan.notice.interrupting")'
        To = '                : _interrupted ? Texts.Of("gui.plan.notice.running")'
        Test = 'UnderwayTests.Interrupting_greys_the_first_button_and_says_so'
    },
    @{
        # Same anchor as "window: refusing to close also asks the run to stop", another mutant.
        Name = 'close: reaching for the corner tells the sheet the window waits'
        File = 'src\Bws.Gui\MainWindow.Carrying.cs'
        From = '        _model.Planned.Underway.Interrupt(closing: true);
        _stopping?.Cancel();'
        To = '        _model.Planned.Underway.Interrupt(closing: false);
        _stopping?.Cancel();'
        Test = 'AbandonGuards.Closing_during_a_run_asks_the_first_level_and_says_the_window_waits'
    },
    @{
        Name = 'report: a run held back says it was interrupted'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        From = '        : Underway.Afterwards(run, Arrived(run)) is { Length: > 0 } stopped ? stopped'
        To = '        : Underway.Afterwards(run, Arrived(run)) is { Length: > 100000 } stopped ? stopped'
        Test = 'UnderwayTests.A_finished_run_that_was_held_back_says_so_on_the_sheet'
    },
    @{
        Name = 'report: the second ask is told apart from the first'
        File = 'src\Bws.Gui\ViewModels\Underway.cs'
        From = '        var nothingPutBack = held.Any(result => result.Step.Reason == StepReason.Restore);'
        To = '        var nothingPutBack = held.Any(result => result.Step.Reason == StepReason.Cascade);'
        Test = 'UnderwayTests.A_stopped_run_says_which_ask_it_met'
    },
    @{
        # A startup setting carrying a stop warns about dependants too, and the command line refuses
        # --dependents on start-type.
        Name = 'in the way: offered on a stop and a restart, never on a startup setting'
        File = 'src\Bws.Gui\ViewModels\PlanSentence.cs'
        From = '        var inTheWay = kind is ActionKind.Stop or ActionKind.Restart'
        To = '        var inTheWay = kind is ActionKind.Stop or ActionKind.Restart or ActionKind.SetStartType'
        Test = 'InTheWayGuards.A_startup_setting_that_stops_offers_nothing_for_them'
    },
    @{
        Name = 'in the way: offered under the refusal of a forced stop'
        File = 'src\Bws.Gui\ViewModels\PlanSentence.cs'
        From = '        var offered = inTheWay is null ? null : PlanWords.Describe(inTheWay);'
        To = '        var offered = inTheWay is null ? null : string.Empty;'
        Test = 'InTheWayGuards.A_forced_stop_refused_for_them_offers_the_way_forward'
    },
    @{
        # The plan on the sheet, not the picked rows - a forcing sheet opened from a failure is about
        # the entry that failed. The mutant asks over the picked rows.
        Name = 'in the way: asked over the plan on the sheet, not over the picked rows'
        File = 'src\Bws.Gui\MainWindow.Menu.cs'
        From = '        var again = shown.Action with { IncludeDependents = true };'
        To = '        var again = shown.Action with { IncludeDependents = true, ServiceNames = Everything(PickedRows()) };'
        Test = 'InTheWayGuards.The_offer_asks_over_the_plan_on_the_sheet_not_over_the_picked_rows'
    },
    @{
        Name = 'in the way: the plan is asked again with the dependants'
        File = 'src\Bws.Gui\MainWindow.Menu.cs'
        From = '        var again = shown.Action with { IncludeDependents = true };'
        To = '        var again = shown.Action with { IncludeDependents = false };'
        Test = 'InTheWayGuards.A_stop_offers_to_stop_them_first_and_rebuilds_the_same_sheet'
    },
    @{
        Name = 'hand-over: the window hands the dependants over'
        File = 'src\Bws.Gui\MainWindow.HandOver.cs'
        # Re-anchored 2026-10-05: HandOverNow became a block that commits the box first (G-9).
        From = '            Dependents = showing && _asked?.Dependents == true'
        To = '            Dependents = false'
        Test = 'InTheWayGuards.A_stop_offers_to_stop_them_first_and_rebuilds_the_same_sheet'
    },
    @{
        Name = 'hand-over: the dependants cross a restart as administrator'
        File = 'src\Bws.Gui\ViewModels\HandOver.cs'
        From = '            Dependents = picked.Length > 0 && wire.Deps,'
        To = '            Dependents = picked.Length > 0 && wire.Deps && false,'
        Test = 'InTheWayGuards.Taking_it_crosses_a_restart_as_administrator'
    },
    @{
        Name = 'hand-over: the dependants only beside a kind that takes them'
        File = 'src\Bws.Gui\ViewModels\HandOver.cs'
        From = '                && (!wire.Deps || kind is ActionKind.Stop or ActionKind.Restart or ActionKind.ForceStop or ActionKind.ForceRestart);'
        To = '                && (!wire.Deps || kind is not ActionKind.SetStartType);'
        Test = 'HandOverTests.A_hand_over_this_program_could_not_have_written_is_refused_whole'
    },

    # Backlog 501, 2026-09-30: when the recovery list restarts what was ended - the delays read, said
    # beside every name, and the line after a run naming who comes back.
    @{
        # Integration: the only test holding the delay against the real manager, through sc.exe.
        Name = 'recovery: the delay of every item is read'
        File = 'src\Bws.Core\ScmDetailReader.Recovery.cs'
        From = 'new RecoveryItem(Kind(item.Type), TimeSpan.FromMilliseconds(item.Delay))'
        To = 'new RecoveryItem(Kind(item.Type), TimeSpan.Zero)'
        Test = 'RecoveryContractTests.The_recovery_list_of_RpcSs_reads_as_sc_exe_prints_it'
    },
    @{
        Name = 'aftermath: every delay of a restart is said once'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = '.Select(item => item.Delay).Distinct()'
        To = '.Select(item => item.Delay)'
        Test = 'AftermathTests.The_restart_warning_carries_every_delay_once_in_the_order_of_the_list'
    },
    @{
        # Re-anchored 2026-10-07 (backlog 541): the delays come from the items a failure can reach.
        Name = 'aftermath: a restart anywhere in the list gives its delay'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = 'reached.Where(item => item.Action == RecoveryAction.RestartService)'
        To = 'reached.Take(1).Where(item => item.Action == RecoveryAction.RestartService)'
        Test = 'AftermathTests.The_restart_warning_carries_every_delay_once_in_the_order_of_the_list'
    },
    @{
        # Backlog 541: "Windows starts Spooler again" over a list of restart, restart, nothing was false from
        # the third failure inside the hour - measured on the throwaway machine 2026-10-07.
        Name = 'aftermath: a list that restarts on some failures only says so'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = '        if (items.All(item => item.Action == RecoveryAction.RestartService))'
        To = '        if (items.Any(item => item.Action == RecoveryAction.RestartService))'
        Test = 'SomeFailuresTests.Spoolers_list_restarts_it_on_its_first_two_failures_only'
    },
    @{
        Name = 'aftermath: failures are counted from 1'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = '(item.Action, Number: index + 1)'
        To = '(item.Action, Number: index)'
        Test = 'SomeFailuresTests.Spoolers_list_restarts_it_on_its_first_two_failures_only'
    },
    @{
        # The last item repeats past the end of the list (SERVICE_FAILURE_ACTIONSW) - variant E on the
        # throwaway machine: nothing, then a restart on every later ending.
        Name = 'aftermath: a last item that restarts restarts every later failure'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = '            items[^1].Action == RecoveryAction.RestartService,'
        To = '            false,'
        Test = 'SomeFailuresTests.Nothing_then_a_restart_restarts_it_from_the_second_failure_on'
    },
    @{
        Name = 'aftermath: the reset period travels with the failures'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = '            resetPeriod);'
        To = '            resetPeriod + TimeSpan.FromSeconds(1));'
        Test = 'SomeFailuresTests.Spoolers_list_restarts_it_on_its_first_two_failures_only'
    },
    @{
        # Variant D on the throwaway machine, 2026-10-07: a reset period of zero made every failure the first.
        Name = 'aftermath: a reset period of zero reaches the first item only'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = 'list.ResetPeriod == TimeSpan.Zero ? [.. list.Items.Take(1)] : list.Items;'
        To = 'list.Items;'
        Test = 'SomeFailuresTests.With_a_reset_period_of_zero_every_failure_is_the_first'
    },
    @{
        # The refusal is the side to err on: it asks the whole list whatever the reset period.
        Name = 'aftermath: the refusal over a computer restart asks the whole list'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = 'one.Actions.Value!.Items.Any(item => item.Action == action)'
        To = 'Reached(one.Actions.Value!).Any(item => item.Action == action)'
        Test = 'SomeFailuresTests.The_refusal_over_a_computer_restart_still_asks_the_whole_list'
    },
    @{
        # Read and dropped until 2026-10-07 - the integration test holds it against sc.exe qfailure RpcSs.
        Name = 'recovery: the reset period is read'
        File = 'src\Bws.Core\ScmDetailReader.Recovery.cs'
        From = '        var reset = Reset(header->dwResetPeriod);'
        To = '        var reset = Reset(header->dwResetPeriod + 1);'
        Test = 'RecoveryContractTests.The_reset_period_of_RpcSs_reads_as_sc_exe_prints_it'
    },
    @{
        Name = 'recovery words: a list restarting on some failures gets its own sentence'
        File = 'src\Bws.Cli\PlanText.Aftermath.cs'
        From = '        return Counted(warning.Restarts) is { } counted'
        To = '        return Counted([]) is { } counted'
        Test = 'ComingBackTextTests.A_restart_on_some_failures_only_names_them_and_sends_nobody_to_sc_exe'
    },
    @{
        Name = 'recovery words: the terminal says or later'
        File = 'src\Bws.Cli\PlanText.Aftermath.cs'
        From = '{ AndLater: true } only => Texts.Of("cli.plan.recovery.laterOrLater"'
        To = '{ AndLater: true } only => Texts.Of("cli.plan.recovery.laterOnly"'
        Test = 'ComingBackTextTests.Or_later_a_count_that_never_starts_again_and_two_names_sharing_a_period_have_their_words'
    },
    @{
        # The key stays a TimeSpan, so the mutant compiles - grouping by the name itself changed the key's
        # type and was BROKEN on its first run, 2026-10-07. A tick per letter of the name splits BITS from
        # Spooler and still reads "24 h" in each clause.
        Name = 'recovery words: two names sharing a reset period share one clause'
        File = 'src\Bws.Cli\PlanText.Aftermath.cs'
        From = '.GroupBy(one => one.Only!.ResetPeriod)'
        To = '.GroupBy(one => one.Only!.ResetPeriod + TimeSpan.FromTicks(one.ServiceName.Length))'
        Test = 'ComingBackTextTests.Or_later_a_count_that_never_starts_again_and_two_names_sharing_a_period_have_their_words'
    },
    @{
        Name = 'recovery words: a count that never starts again is said'
        File = 'src\Bws.Cli\PlanText.Aftermath.cs'
        From = '(names.Count == 1, resetPeriod == Timeout.InfiniteTimeSpan) switch'
        To = '(names.Count == 1, resetPeriod == TimeSpan.Zero) switch'
        Test = 'ComingBackTextTests.Or_later_a_count_that_never_starts_again_and_two_names_sharing_a_period_have_their_words'
    },
    @{
        Name = 'recovery words: a reset period is said in the largest unit'
        File = 'src\Bws.Cli\PlanText.Aftermath.cs'
        From = 'var seconds when seconds % 3600 == 0 => Texts.Of("cli.plan.recovery.hours", seconds / 3600),'
        To = 'var seconds when seconds % 3600 == 1 => Texts.Of("cli.plan.recovery.hours", seconds / 3600),'
        Test = 'ComingBackTextTests.A_reset_period_is_said_in_the_largest_unit_that_divides_it'
    },
    @{
        Name = 'coming back: the terminal names the failures after a run'
        File = 'src\Bws.Cli\PlanText.Aftermath.cs'
        From = '        var counted = Counted(back) is { } clauses'
        To = '        var counted = Counted([]) is { } clauses'
        Test = 'ComingBackTextTests.A_run_that_ended_the_process_names_the_failures_of_who_may_come_back'
    },
    @{
        Name = 'recovery words: the window names the failures'
        File = 'src\Bws.Gui\ViewModels\PlanWords.Aftermath.cs'
        From = '        return (warning.Related.Count == 1, Counted(warning.Restarts)) switch'
        To = '        return (warning.Related.Count == 1, Counted([])) switch'
        Test = 'ComingBackWordsGuards.A_restart_on_some_failures_only_names_them'
    },
    @{
        Name = 'recovery words: the window says or later'
        File = 'src\Bws.Gui\ViewModels\PlanWords.Aftermath.cs'
        From = '{ AndLater: true } only => Texts.Of("gui.plan.recovery.laterOrLater"'
        To = '{ AndLater: true } only => Texts.Of("gui.plan.recovery.laterOnly"'
        Test = 'ComingBackWordsGuards.Or_later_and_a_count_that_never_starts_again_have_their_words'
    },
    @{
        Name = 'coming back: the notice names the failures after a run'
        File = 'src\Bws.Gui\ViewModels\PlanWords.Aftermath.cs'
        From = '        return (back.Count == 1, Counted(back)) switch'
        To = '        return (back.Count == 1, Counted([])) switch'
        Test = 'ComingBackWordsGuards.The_notice_after_a_run_names_the_failures_of_who_may_come_back'
    },
    @{
        # Backlog 539 as the window saw it: "not where you asked" over an empty list of what went wrong.
        Name = 'completed: the sheet after a forced restart whose polite stop worked reads done'
        File = 'src\Bws.Core\Planning\PlanRun.cs'
        From = '        SkipReason.NothingToPutBack => Stayed(result.Step.ServiceName),'
        To = '        SkipReason.NothingToPutBack => false,'
        Test = 'ForcedRestartNoticeGuards.A_forced_restart_whose_polite_stop_worked_reads_done'
    },
    @{
        Name = 'coming back: the delays come from the warning the plan made'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = 'warning.Kind == PlanWarningKind.RecoveryRestarts)?.Restarts'
        To = 'warning.Kind == PlanWarningKind.RecoveryRunsProgram)?.Restarts'
        Test = 'ComingBackTests.A_forced_stop_names_everybody_who_died_with_the_process_and_when'
    },
    @{
        # A failed ending counts as one - caught in the terminal, where the report of a refused ending is.
        Name = 'coming back: nothing unless the process was ended'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = '(result.Outcome is StepOutcome.Succeeded or StepOutcome.TimedOut || result.StartedAgain)'
        To = '(result.Outcome is not StepOutcome.Skipped || result.StartedAgain)'
        Test = 'ComingBackTextTests.A_run_that_never_ended_the_process_says_nothing_about_coming_back'
    },
    @{
        Name = 'coming back: an entry Windows started again at once is left to its own line'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = '            ? !ending.StartedAgain'
        To = '            ? true'
        Test = 'ComingBackTests.An_entry_Windows_started_again_at_once_is_left_to_its_own_line'
    },
    @{
        Name = 'coming back: a neighbour whose own stop arrived sets nothing off'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = '(result.Outcome == StepOutcome.Succeeded || result.SkippedBecause == SkipReason.AlreadyThere)'
        To = '(result.SkippedBecause == SkipReason.AlreadyThere)'
        Test = 'ComingBackTests.A_neighbour_whose_own_stop_arrived_sets_nothing_off'
    },
    @{
        # NetEffect's count, which gives a timed out stop the credit - the documented rule says otherwise.
        Name = 'coming back: a neighbour whose stop timed out dies with the process'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = '(result.Outcome == StepOutcome.Succeeded || result.SkippedBecause == SkipReason.AlreadyThere)'
        To = '(result.Outcome is StepOutcome.Succeeded or StepOutcome.TimedOut || result.SkippedBecause == SkipReason.AlreadyThere)'
        Test = 'ComingBackTests.A_neighbour_whose_own_stop_timed_out_dies_with_the_process'
    },
    @{
        Name = 'coming back: an entry this run started again is not announced'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = '            && result.Step.Operation == StepOperation.Start'
        To = '            && result.Step.Operation == StepOperation.Terminate'
        Test = 'ComingBackTests.A_forced_restart_that_put_everything_back_announces_nothing'
    },
    @{
        Name = 'coming back: a start that was refused leaves the entry to its list'
        File = 'src\Bws.Core\Planning\Aftermath.cs'
        From = '(result.Outcome is StepOutcome.Succeeded or StepOutcome.TimedOut
                || result.SkippedBecause'
        To = '(result.Outcome is not StepOutcome.Skipped
                || result.SkippedBecause'
        Test = 'ComingBackTests.An_entry_whose_start_back_was_refused_is_left_to_its_recovery_list'
    },
    @{
        Name = 'coming back: the terminal says who comes back'
        File = 'src\Bws.Cli\PlanText.Aftermath.cs'
        From = '        if (run?.ComingBack is not { Count: > 0 } back)'
        To = '        if (run?.ComingBack is not { Count: > 99 } back)'
        Test = 'ComingBackTextTests.A_run_that_ended_the_process_says_who_comes_back_before_the_warnings'
    },
    @{
        Name = 'coming back: the terminal says when beside the name'
        File = 'src\Bws.Cli\PlanText.Aftermath.cs'
        From = '            is { After.Count: > 0 } restart'
        To = '            is { After.Count: > 99 } restart'
        Test = 'ComingBackTextTests.The_restart_warning_says_when_beside_the_name'
    },
    @{
        Name = 'coming back: the terminal names every delay'
        File = 'src\Bws.Cli\PlanText.Aftermath.cs'
        From = 'string.Join(", ", after.Take(after.Count - 1).Select(Seconds))'
        To = 'Seconds(after[0])'
        Test = 'ComingBackTextTests.Every_delay_is_named_and_a_fraction_of_a_second_stays_in_seconds'
    },
    @{
        Name = 'coming back: the window says when beside the name'
        File = 'src\Bws.Gui\ViewModels\PlanWords.Aftermath.cs'
        From = '                is { After.Count: > 0 } restart'
        To = '                is { After.Count: > 99 } restart'
        Test = 'ComingBackWordsGuards.The_restart_warning_says_when_beside_every_name'
    },
    @{
        Name = 'coming back: the notice says who comes back'
        File = 'src\Bws.Gui\ViewModels\PlanWords.Aftermath.cs'
        From = '        return back.Length == 0'
        To = '        return back.Length >= 0'
        Test = 'ComingBackWordsGuards.The_notice_after_a_run_that_ended_the_process_says_who_comes_back'
    },
    @{
        Name = 'coming back: the sheet asks for the second sentence'
        File = 'src\Bws.Gui\ViewModels\Planned.cs'
        From = '            : PlanWords.ThenComingBack(Reported(run), run);'
        To = '            : Reported(run);'
        Test = 'ComingBackWordsGuards.The_notice_after_a_run_that_ended_the_process_says_who_comes_back'
    },
    @{
        # Package E of the stability report, 2026-09-30 - every entry below is new that day.
        # D-3: a null among the names of fields nobody read ended `snapshot diff` with a null
        # dereference and code 1. The mutation is the check never firing.
        Name = 'snapshot: a null among the fields nobody read is refused, not thrown over'
        File = 'src\Bws.Core\Snapshots\Snapshot.cs'
        From = 'if (entry.NotRead?.Any(field => field is null) == true)'
        To = 'if (entry.NotRead?.Any(field => field is null) == null)'
        Test = 'DamagedListTests.The_reader_refuses_the_file_and_names_the_entry'
    },
    @{
        # D-1: per-user session copies left out by the role the type bits give.
        Name = 'diff: a session copy is left out of the comparison'
        File = 'src\Bws.Core\Snapshots\SnapshotDiff.cs'
        From = '.Where(entry => !string.Equals(entry.PerUserRole, nameof(PerUserRole.Instance), StringComparison.Ordinal))'
        To = '.Where(entry => entry.PerUserRole is not null)'
        Test = 'ComparisonScopeTests.Session_copies_are_left_out_on_both_sides_and_counted'
    },
    @{
        Name = 'diff: the session copies left out are counted'
        File = 'src\Bws.Core\Snapshots\SnapshotDiff.cs'
        From = 'leftOut = entries.Count - kept.Count;'
        To = 'leftOut = 0;'
        Test = 'ComparisonScopeTests.Session_copies_are_left_out_on_both_sides_and_counted'
    },
    @{
        # D-2: drift is configuration. The mutation is the meaning before 2026-09-30, when an entry
        # that only stopped by itself made --exit-code end with 5.
        Name = 'diff: running state alone is not drift'
        File = 'src\Bws.Core\Snapshots\SnapshotDiff.cs'
        From = '|| Changed.Any(entry => entry.Differences.Any(difference => difference.Group == DifferenceGroup.Configuration));'
        To = '|| Changed.Count > 0;'
        Test = 'ComparisonScopeTests.An_entry_that_differs_only_in_running_state_is_reported_and_is_not_drift'
    },
    @{
        # The reader of the old Any that had a second question in it - asking Drifted here would
        # print "No differences." over an entry that differs only in state.
        Name = 'diff text: no differences is said only when there is nothing to show'
        File = 'src\Bws.Cli\DiffText.cs'
        From = 'if (!diff.Reported)'
        To = 'if (!diff.Drifted)'
        Test = 'DiffOutputTests.An_entry_that_differs_only_in_running_state_is_printed_and_does_not_make_the_document_differ'
    },
    @{
        Name = 'diff json: differs answers drift'
        File = 'src\Bws.Cli\DiffJson.cs'
        From = '                diff.Drifted,'
        To = '                diff.Reported,'
        Test = 'DiffOutputTests.An_entry_that_differs_only_in_running_state_is_printed_and_does_not_make_the_document_differ'
    },
    @{
        # Nothing in the suite ran exit code 5 before this package, so the change of its meaning
        # had nothing to redden. The integration test edits one field of a real snapshot.
        Name = 'diff: exit code 5 answers drift'
        File = 'src\Bws.Cli\Program.cs'
        From = 'exit = options.ExitCodeOnDifference && difference.Drifted ? ExitCode.Differences : ExitCode.Ok;'
        To = 'exit = options.ExitCodeOnDifference && difference.Reported ? ExitCode.Differences : ExitCode.Ok;'
        Test = 'DiffContractTests.The_exit_code_answers_about_configuration_and_never_about_running_state_alone'
    },
    @{
        Name = 'diff text: the line about session copies only when there were some'
        File = 'src\Bws.Cli\DiffText.cs'
        From = '(diff.LeftOut.Any, () =>'
        To = '(true, () =>'
        Test = 'DiffOutputTests.No_line_about_session_copies_when_there_were_none'
    },
    @{
        # D-5: two languages leave display names and descriptions out of every entry.
        Name = 'diff: two languages leave the names out of every entry'
        # RE-ANCHORED 2026-10-06, package SB: the fields a caveat takes out moved to ComparisonCaveats.
        File = 'src\Bws.Core\Snapshots\ComparisonCaveats.cs'
        From = '.. LanguageDiffers ? Translated : [],'
        To = '.. LanguageDiffers ? Translated[..0] : [],'
        Test = 'ComparisonCaveatTests.Two_languages_leave_the_names_out_of_every_entry'
    },
    @{
        Name = 'caveats: the monthly update counts when both files know it'
        File = 'src\Bws.Core\Snapshots\ComparisonCaveats.cs'
        From = '|| Disagree(before.OperatingSystemVersion, after.OperatingSystemVersion, StringComparison.Ordinal),'
        To = '|| false,'
        Test = 'ComparisonCaveatTests.Two_updates_of_one_build_are_different_windows'
    },
    @{
        Name = 'caveats: not known is not a disagreement'
        File = 'src\Bws.Core\Snapshots\ComparisonCaveats.cs'
        From = 'before is not null && after is not null && !string.Equals(before, after, comparison);'
        To = '!string.Equals(before, after, comparison);'
        Test = 'ComparisonCaveatTests.An_update_nobody_recorded_is_not_known_rather_than_different'
    },
    @{
        Name = 'caveats: an update one side does not record is named'
        File = 'src\Bws.Core\Snapshots\ComparisonCaveats.cs'
        From = 'if (before.OperatingSystemVersion is null || after.OperatingSystemVersion is null)'
        To = 'if (before.OperatingSystemVersion is null && after.OperatingSystemVersion is null)'
        Test = 'ComparisonCaveatTests.An_update_nobody_recorded_is_not_known_rather_than_different'
    },
    @{
        Name = 'caveats: a language one side does not record is named'
        File = 'src\Bws.Core\Snapshots\ComparisonCaveats.cs'
        From = 'if (before.NamesLanguage is null || after.NamesLanguage is null)'
        To = 'if (before.NamesLanguage is null && after.NamesLanguage is null)'
        Test = 'ComparisonCaveatTests.A_language_nobody_recorded_still_compares_the_names_and_says_it_could_not_check'
    },
    @{
        Name = 'caveats: an account in other capitals is the same account'
        File = 'src\Bws.Core\Snapshots\ComparisonCaveats.cs'
        From = '!string.Equals(before.TakenBy, after.TakenBy, StringComparison.OrdinalIgnoreCase),'
        To = '!string.Equals(before.TakenBy, after.TakenBy, StringComparison.Ordinal),'
        Test = 'ComparisonCaveatTests.A_different_account_is_said_and_different_capitals_are_not_one'
    },
    @{
        # Schema five is the first bump that keeps reading the version before it.
        Name = 'snapshot: a version four file is still read'
        File = 'src\Bws.Core\Snapshots\SnapshotJson.cs'
        From = 'if (snapshot.Metadata.SchemaVersion is < Snapshot.OldestSchemaVersionRead or > Snapshot.CurrentSchemaVersion)'
        To = 'if (snapshot.Metadata.SchemaVersion != Snapshot.CurrentSchemaVersion)'
        Test = 'SnapshotGoldenTests.A_version_four_file_is_read_and_says_what_it_does_not_know'
    },
    @{
        # The ".0" Environment.OSVersion writes where the update belongs - the fault D-5 is about.
        Name = 'system facts: the update number is read, never assumed'
        File = 'src\Bws.Core\Snapshots\SystemFacts.cs'
        From = '$"{version.Major}.{version.Minor}.{version.Build}.{update.Value}");'
        To = '$"{version.Major}.{version.Minor}.{version.Build}.0");'
        Test = 'ComparisonCaveatTests.This_machine_says_its_update_and_the_language_its_manager_names_things_in'
    },
    @{
        # D-6: a folder named as the target is refused before the expensive work.
        Name = 'snapshot: a folder is not a file to write'
        File = 'src\Bws.Core\Snapshots\SnapshotOnDisk.cs'
        From = 'if (Directory.Exists(target))'
        To = 'if (Directory.Exists(target + ":never"))'
        Test = 'SnapshotTargetTests.A_folder_is_refused_in_words_about_the_folder_with_or_without_force'
    },
    @{
        # Round 2 point 8, rule 5 of docs/03: the mutation is the helper this document used until
        # 2026-09-30, lowering the first letter.
        Name = 'plan json: values are spelled as their enumeration spells them'
        File = 'src\Bws.Cli\PlanJson.cs'
        From = '                    result.Outcome.ToString(),'
        To = '                    char.ToLowerInvariant(result.Outcome.ToString()[0]) + result.Outcome.ToString()[1..],'
        Test = 'MachineDocumentValueGuards.Every_enumerated_value_in_a_plan_is_a_member_spelled_as_its_enumeration_spells_it'
    },
    @{
        Name = 'diff json: the group is spelled as its enumeration spells it'
        File = 'src\Bws.Cli\DiffJson.cs'
        From = 'Converters = { new JsonStringEnumConverter() },'
        To = 'Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },'
        Test = 'MachineDocumentValueGuards.The_group_of_every_difference_is_a_member_spelled_as_its_enumeration_spells_it'
    },
    @{
        # Package F of the stability report, 2026-10-05 - Q-1. The scanner forgets which characters
        # came through a backslash, which is how name:/a\?/ used to match all 800 entries.
        Name = 'pattern: an escape inside a pattern reaches the pattern'
        File = 'src\Bws.Core\Querying\QueryScanner.cs'
        From = '                text.Append(query[index + 1], literal: true, escaped: true, at: index + 1);'
        To = '                text.Append(query[index + 1], literal: true, escaped: false, at: index + 1);'
        Test = 'QueryPatternTests.An_escape_inside_a_pattern_reaches_the_pattern'
    },
    @{
        # Q-2. Every bare comma splits again, which cut name:/^w{2,}/ into two searches for text.
        Name = 'pattern: a comma inside a pattern that has not closed belongs to it'
        File = 'src\Bws.Core\Querying\QueryValueList.cs'
        From = '            if (!values.IsSpecial(index, '','') || StillOpen(values, start, index))'
        To = '            if (!values.IsSpecial(index, '',''))'
        Test = 'QueryPatternTests.A_comma_inside_a_pattern_belongs_to_it'
    },
    @{
        # The other side of the same rule: a slash right before the comma closes the pattern, so
        # name:/^a/,/^b/ is still two. The mutation never lets a pattern close.
        Name = 'pattern: a slash right before the comma closes the pattern'
        File = 'src\Bws.Core\Querying\QueryValueList.cs'
        From = '        start < comma && values.IsSpecial(start, ''/'') && !Closes(values, start, comma);'
        To = '        start < comma && values.IsSpecial(start, ''/'');'
        Test = 'QueryPatternTests.Two_patterns_with_a_comma_between_them_are_still_two'
    },
    @{
        # Q-6. The mutation is the rule before 2026-10-05: only a value with a slash at each end is
        # a pattern, so name:/abc falls through to a search for the text, slash included.
        Name = 'pattern: one that never closes is a mistake, not text'
        File = 'src\Bws.Core\Querying\QueryValueReader.cs'
        From = '        if (value.StartsWithSpecial(''/''))'
        To = '        if (value.Length >= 2 && value.IsSpecial(0, ''/'') && value.IsSpecial(value.Length - 1, ''/''))'
        Test = 'QueryPatternTests.A_pattern_that_never_closes_is_a_mistake_once_nobody_is_typing'
    },
    @{
        # Q-6. name:// matched all 800 entries - an empty pattern compiles and matches anything.
        Name = 'pattern: an empty pair of slashes says nothing'
        File = 'src\Bws.Core\Querying\QueryValueReader.cs'
        From = '        if (value.Length == 2)'
        To = '        if (value.Length == -1)'
        Test = 'QueryPatternTests.An_empty_pattern_says_nothing_even_while_typed'
    },
    @{
        # A pattern is unclosed for every keystroke between its slashes, so the window passes the
        # last member over while that is its only problem.
        Name = 'typing tolerance: a pattern still being typed is passed over'
        File = 'src\Bws.Core\Querying\QueryParser.cs'
        From = '        if (problem.Kind == QueryProblemKind.UnclosedPattern)'
        To = '        if (problem.Kind == QueryProblemKind.BadPattern)'
        Test = 'QueryPatternTests.A_pattern_still_being_typed_is_passed_over'
    },
    @{
        # The same anchor as "only the member being typed is tolerated", with a different mutation:
        # the quote condition as it stood before 2026-10-05, ahead of the pattern. A quote inside a
        # pattern keeps a space away from the scanner and closes nothing.
        Name = 'typing tolerance: a quote inside a pattern does not finish it'
        File = 'src\Bws.Core\Querying\QueryParser.cs'
        From = '        if (!last || char.IsWhiteSpace(query[^1]) || problems.Count - before != 1)'
        To = '        if (!last || char.IsWhiteSpace(query[^1]) || query[^1] == ''"'' || problems.Count - before != 1)'
        Test = 'QueryPatternTests.A_pattern_still_being_typed_is_passed_over'
    },
    @{
        # Q-6. A pasted path was a mistake about a field called C.
        Name = 'query: a pasted path is a word, not a field'
        File = 'src\Bws.Core\Querying\QueryParser.cs'
        From = '        if (colon <= 0 || IsDrivePath(body, colon))'
        To = '        if (colon <= 0)'
        Test = 'DrivePathQueryTests.A_pasted_path_is_a_search_rather_than_a_field'
    },
    @{
        # The shape of a drive, not the length of a name: x:abc is still a field.
        Name = 'query: only a colon followed by a separator is a drive'
        File = 'src\Bws.Core\Querying\QueryParser.cs'
        From = '        && body.Text[2] is ''\\'' or ''/'';'
        To = '        && true;'
        Test = 'DrivePathQueryTests.A_letter_and_a_colon_with_no_separator_after_it_still_names_a_field'
    },
    @{
        # Q-3. The mutation is the fold as it stood: pid:>1000 pid:<2000 answered with 121, not 11.
        Name = 'query: two bounds on one number narrow'
        File = 'src\Bws.Core\Querying\QueryParser.cs'
        From = '            if (term.Field is null || term.Negated || term.Values.All(value => value.IsOpenComparison))'
        To = '            if (term.Field is null || term.Negated)'
        Test = 'BoundQueryTests.Two_bounds_written_as_two_members_narrow'
    },
    @{
        # An exact value picks out a value of its own, so two of them are still either.
        Name = 'query: an exact value is not a bound'
        File = 'src\Bws.Core\Querying\QueryValues.cs'
        From = '        operation is NumberOperator.Greater'
        To = '        operation is NumberOperator.Equal or NumberOperator.Greater'
        Test = 'BoundQueryTests.Exact_values_written_as_two_members_are_still_either'
    },
    @{
        # The size field answers the question for itself - the number field's answer is a
        # different line, so it needs the class around it to be found.
        Name = 'query: a bound on a size narrows like one on a number'
        File = 'src\Bws.Core\Querying\QueryValues.cs'
        From = @'
internal sealed class SizeValue(NumberOperator operation, long low, long high) : IQueryValue
{
    public bool IsOpenComparison => NumberOperators.IsOpen(operation);
'@
        To = @'
internal sealed class SizeValue(NumberOperator operation, long low, long high) : IQueryValue
{
    public bool IsOpenComparison => false;
'@
        Test = 'BoundQueryTests.Bounds_on_a_size_narrow_the_same_way'
    },
    @{
        # Q-4. The mutation is ? as it stood: refused only, so an entry nobody read could not be
        # asked for at all.
        Name = 'query: ? finds a field nobody read'
        File = 'src\Bws.Core\Querying\QueryValues.cs'
        From = '        Verdict.Of(QueryValue.Unanswerable(field, entry));'
        To = '        Verdict.Of(field.OutcomeOf(entry) == ReadOutcome.Denied);'
        Test = 'TriggerQueryTests.Nobody_having_looked_is_asked_for_with_the_question_mark'
    },
    @{
        # Q-4. none and any said a confident no about a field nobody could read.
        Name = 'query: none and any do not answer for a field nobody read'
        File = 'src\Bws.Core\Querying\QueryValues.cs'
        From = '            ? Verdict.CouldNotRead'
        To = '            ? Verdict.NoMatch'
        Test = 'TriggerQueryTests.None_and_any_do_not_answer_for_a_field_nobody_read'
    },
    @{
        # Q-5. The mutation is the cut as it stood: the whole member, every time.
        Name = 'filters: a chip takes one value out of a list'
        File = 'src\Bws.Core\Querying\QueryMembers.cs'
        From = '        if (!parts.Where((part, at) => !going[at]).Any(part => part.End.Value > part.Start.Value))'
        To = '        if (parts.Count > 0)'
        Test = 'QueryMemberValueTests.Only_the_value_asked_about_leaves_the_list'
    },
    @{
        # An empty value is not something left to say - status:stopped, goes whole rather than
        # leaving status: behind.
        Name = 'filters: a list with nothing else in it goes whole'
        File = 'src\Bws.Core\Querying\QueryMembers.cs'
        From = '        if (!parts.Where((part, at) => !going[at]).Any(part => part.End.Value > part.Start.Value))'
        To = '        if (!parts.Where((part, at) => !going[at]).Any())'
        Test = 'QueryMemberValueTests.A_list_with_nothing_else_in_it_goes_whole'
    },
    @{
        # The values after the last one kept take the comma before them, or a trailing comma stays.
        Name = 'filters: the last values out take the comma in front of them'
        File = 'src\Bws.Core\Querying\QueryMembers.cs'
        From = '                cuts.Add(at < lastKept'
        To = '                cuts.Add(at < going.Length'
        Test = 'QueryMemberValueTests.Only_the_value_asked_about_leaves_the_list'
    },
    @{
        # The cut is computed from where the scanner says each character stood.
        Name = 'scanner: each character remembers where it was written'
        File = 'src\Bws.Core\Querying\QueryScanner.cs'
        From = '            text.Append(character, literal: quoting, escaped: false, at: index);'
        To = '            text.Append(character, literal: quoting, escaped: false, at: index + 1);'
        Test = 'QueryMemberValueTests.Only_the_value_asked_about_leaves_the_list'
    },

    # Package G of the stability report, 2026-10-05: reading does not mistake a file or a network
    # path. Four entries above were re-anchored, not duplicated - the refused stop bit, the network
    # rule and the percent expansion in the resolver, and the appended .exe. THE SHA-1 CATALOGUE
    # QUESTION (R-6) HAS NO ENTRY ON PURPOSE: no test can hand the inspector a file that only a SHA-1
    # catalogue lists, and the owner's machine has none (tools/signature-probe/catalogue-algorithms.ps1),
    # so a mutant removing it would survive and say nothing about the rule.
    @{
        # R-2, owner's decision: the device namespace is local only before a drive or a volume. The
        # mutant is the rule that stood until that day - local unless \\?\UNC\ - so \\.\UNC\ and
        # GLOBALROOT walk through to a share again.
        Name = 'network: the device namespace is local only before a drive or a volume'
        File = 'src\Bws.Core\NetworkPath.cs'
        From = 'return !deviceNamespace || !NamesALocalVolume(value.AsSpan(4));'
        To = 'return !deviceNamespace || (value.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase) && !NamesALocalVolume(value.AsSpan(4)));'
        Test = 'BinaryPathResolverTests.What_counts_as_leaving_this_machine'
    },
    @{
        # R-3. Puts back the command line rules for a driver, which is what let a planted
        # C:\Program.exe stand in for a driver under C:\Program Files.
        Name = 'path: a driver path is one file name, not a command line'
        File = 'src\Bws.Core\BinaryPathResolver.cs'
        From = 'return isDriver ? asking.Settle(trimmed) : asking.Walk(trimmed);'
        To = 'return asking.Walk(trimmed);'
        Test = 'LaunchPathReadingTests.A_driver_path_with_a_space_is_one_file_name_and_a_planted_program_is_not_it'
    },
    @{
        Name = 'path: a driver gets no exe appended'
        File = 'src\Bws.Core\BinaryPathResolver.cs'
        From = 'if (!IsDriver && Path.GetExtension(place).Length == 0)'
        To = 'if (Path.GetExtension(place).Length == 0)'
        Test = 'LaunchPathReadingTests.A_driver_gets_no_exe_appended_to_a_name_without_an_extension'
    },
    @{
        # R-3: \Device\... for a driver went to C:\Device\... until that day.
        Name = 'path: a driver in the kernel namespace is not looked for on drive C'
        File = 'src\Bws.Core\BinaryPathResolver.cs'
        From = '? Win32GlobalRoot + value'
        To = '? Path.Combine(Path.GetPathRoot(WindowsDirectory) ?? @"C:\", value[1..])'
        Test = 'LaunchPathReadingTests.A_driver_named_in_the_kernel_namespace_is_not_looked_for_on_drive_c'
    },
    @{
        # R-2: \??\UNC\ became a relative UNC\... joined to the Windows directory.
        Name = 'path: the kernel prefix keeps a share a share'
        File = 'src\Bws.Core\BinaryPathResolver.cs'
        From = 'return @"\\" + rest[ObjectShare.Length..];'
        To = 'return rest;'
        Test = 'LaunchPathReadingTests.The_kernel_prefix_keeps_a_share_a_share'
    },
    @{
        # R-3: a bare service name joined to the Windows directory, which is CreateProcess's fifth
        # place to look rather than its first.
        Name = 'path: a bare name is searched for where CreateProcess looks'
        File = 'src\Bws.Core\BinaryPathResolver.cs'
        From = ': SearchedFor(value);'
        To = ': [Path.Combine(WindowsDirectory, value)];'
        Test = 'LaunchPathReadingTests.A_bare_name_is_found_where_CreateProcess_looks_first'
    },
    @{
        # R-1 in the walk: a refusal is an answer, and walking past it names a file Windows does
        # not run.
        Name = 'path: a refused candidate ends the walk'
        File = 'src\Bws.Core\BinaryPathResolver.cs'
        From = 'if (answer.Outcome != ReadOutcome.Present || answer.Value)'
        To = 'if (answer.Outcome == ReadOutcome.Present && answer.Value)'
        Test = 'LaunchPathReadingTests.A_refused_shorter_name_ends_the_walk_before_a_longer_one_that_is_there'
    },
    @{
        # R-1 at the source: a refusal read as "not there", which is what File.Exists did.
        Name = 'disk: a refusal is not a missing file'
        File = 'src\Bws.Core\FileOnDisk.cs'
        From = 'return code is NotFound or PathNotFound or NoSuchDrive or BadName or BadPathName'
        To = 'return code is NotFound or PathNotFound or NoSuchDrive or BadName or BadPathName or 5'
        Test = 'LaunchPathReadingTests.Every_other_code_is_a_refusal_carrying_its_number'
    },
    @{
        # R-1 in the inspector: the signature, version and hash of a refused file said "absent".
        Name = 'signatures: a refused file is refused, not absent'
        File = 'src\Bws.Core\WindowsBinaryInspector.cs'
        From = '        if (onDisk.Outcome == ReadOutcome.Denied)'
        To = '        if (onDisk.Outcome == ReadOutcome.NotRead)'
        Test = 'LaunchPathReadingTests.The_inspector_answers_a_refused_file_with_a_refusal_and_a_missing_one_with_absence'
    },
    @{
        # R-4: a configuration field pointing outside the block. Mutated where the answer is
        # chosen rather than by putting the unbounded read back - that would read a page below a
        # pinned array, which fails usually and not always (see the dependent name entry above).
        Name = 'blocks: a configuration field outside the block is refused'
        File = 'src\Bws.Core\ManagerBlocks.cs'
        From = '            return OutsideTheBlock<string>();'
        To = '            return Reading<string>.Absent();'
        Test = 'ManagerBlockTests.A_configuration_field_pointing_outside_the_block_is_refused_and_its_neighbours_are_read'
    },
    @{
        # R-5, the half rule 8 is about: a stalled first turn ended the listing empty.
        Name = 'listing: a turn without progress is asked again before the listing can end'
        File = 'src\Bws.Core\WindowsScmCatalog.Enumerating.cs'
        From = '        if (stalled > 0)'
        To = '        if (stalled > 99)'
        Test = 'ListingTurnTests.A_first_turn_that_hands_over_nothing_is_asked_again_rather_than_ending_the_listing_empty'
    },
    @{
        # R-5, the half backlog 304 is about: the ceiling on asking again.
        Name = 'listing: the fourth turn without progress gives up'
        File = 'src\Bws.Core\WindowsScmCatalog.Enumerating.cs'
        From = '        if (stalled > StalledTurnsAskedAgain)'
        To = '        if (stalled > StalledTurnsAskedAgain + 1)'
        Test = 'ListingTurnTests.Three_turns_without_progress_are_asked_again_and_the_fourth_gives_up'
    },
    @{
        # R-7: a recognizer driver read as Unknown and therefore as no driver at all.
        Name = 'recognizer: the reading counts a recognizer as a driver'
        File = 'src\Bws.Core\ScmBuffers.cs'
        From = 'EntryType is Core.EntryType.KernelDriver or Core.EntryType.FileSystemDriver || RecognizerDriver;'
        To = 'EntryType is Core.EntryType.KernelDriver or Core.EntryType.FileSystemDriver;'
        Test = 'LaunchPathReadingTests.A_recognizer_driver_naming_no_file_runs_the_one_assumed_for_a_driver'
    },
    @{
        Name = 'recognizer: the entry counts a recognizer as a driver'
        File = 'src\Bws.Core\ScmEntry.cs'
        From = 'public bool IsDriver => EntryType is EntryType.KernelDriver or EntryType.FileSystemDriver || RecognizerDriver;'
        To = 'public bool IsDriver => EntryType is EntryType.KernelDriver or EntryType.FileSystemDriver;'
        Test = 'ScmEntryTests.Drivers_are_recognised_as_drivers'
    },
    @{
        # R-7, owner's decision: "no access" only for a refusal of access - three places say it.
        Name = 'words: the report says no access only for access'
        File = 'src\Bws.Cli\EntryReport.cs'
        From = 'ReadOutcome.Denied => told.Code == AccessDenied.Code'
        To = 'ReadOutcome.Denied => told.Code != 0'
        Test = 'EntryReportTests.Only_a_refusal_of_access_is_called_no_access'
    },
    @{
        Name = 'words: the listing says no access only for access'
        File = 'src\Bws.Cli\ListingTable.cs'
        From = 'ReadOutcome.Denied => reading.ErrorCode == AccessDenied.Code'
        To = 'ReadOutcome.Denied => reading.ErrorCode != 0'
        Test = 'EntryReportTests.Only_a_refusal_of_access_is_called_no_access'
    },
    @{
        Name = 'words: the window says no access only for access'
        File = 'src\Bws.Gui\ViewModels\CellFaces.cs'
        From = 'errorCode == AccessDenied.Code ? Texts.Of("gui.cell.noAccess")'
        To = 'errorCode != 0 ? Texts.Of("gui.cell.noAccess")'
        Test = 'SystemAccountTests.A_reading_that_failed_for_any_reason_but_access_does_not_say_no_access'
    },
    @{
        # Package H of the stability report, 2026-10-05: the command line in front of a console, a
        # pipe and a shell. C-1 every --json is ASCII, C-4 nothing from the machine moves the terminal,
        # `--` and the verb after "snapshot", the second pass timed on its own, W-12 the name quoted for
        # a shell, C-3 the admissions after an early refusal. Every entry from here to the end is one of
        # its guards. WHAT HAS NO ENTRY, said rather than left out: W-11 - a closed window interrupting
        # the run (WindowClose) - because no test can close the console it runs in. Its proof is
        # tools/close-probe/sweep.ps1 against a real service, the build before beside the build after
        # (10 of 13 left stopped against 0 of 13), and its self-test can fail (close.ps1 -SelfTest).
        Name = 'ascii json: a letter outside ASCII leaves as an escape'
        File = 'src\Bws.Cli\AsciiJson.cs'
        From = 'var first = document.AsSpan().IndexOfAnyExceptInRange(''\0'', Last);'
        To = 'var first = document.Length < 0 ? 0 : -1;'
        Test = 'AsciiDocumentTests.A_listing_is_ascii_and_says_the_name_the_machine_gave'
    },
    @{
        # WAS 'the boundary is the tilde, so DEL is escaped too', moving the boundary to U+007F, and it
        # came back MISSED on its first run - rightly. The relaxed encoder escapes DEL, the C1 controls
        # and U+00A0 itself, so no character between U+007E and U+00A1 ever reaches this loop and the
        # boundary cannot be seen from outside (measured, and said beside Last in the code). What the
        # loop does that nothing else does is escape the letters, so that is what this breaks.
        Name = 'ascii json: a letter reaching the loop is escaped, not copied'
        File = 'src\Bws.Cli\AsciiJson.cs'
        From = 'if (character <= Last)'
        To = 'if (character <= (char)0xFFFE)'
        Test = 'AsciiDocumentTests.A_listing_is_ascii_and_says_the_name_the_machine_gave'
    },
    @{
        Name = 'ascii json: every renderer serialises through the one door'
        File = 'src\Bws.Cli\ListingJson.cs'
        From = 'AsciiJson.Serialize(entries.Select(EntryDocument.From).ToList(), Options);'
        To = 'JsonSerializer.Serialize(entries.Select(EntryDocument.From).ToList(), Options);'
        Test = 'OutputChannelGuards.Every_json_document_of_the_command_line_is_serialised_in_one_place'
    },
    @{
        Name = 'printable: a character from the set is written out'
        File = 'src\Bws.Core\Printable.cs'
        From = 'var first = value.AsSpan().IndexOfAny(Unprintable);'
        To = 'var first = value.Length < 0 ? 0 : -1;'
        Test = 'TerminalTextTests.A_character_is_written_out_exactly_when_it_is_in_the_set'
    },
    @{
        Name = 'printable: the right to left override is in the set'
        File = 'src\Bws.Core\Printable.cs'
        From = '.. Range(0x202A, 0x202E),'
        To = '.. Range(0x202A, 0x202D),'
        Test = 'TerminalTextTests.A_character_is_written_out_exactly_when_it_is_in_the_set'
    },
    @{
        Name = 'printable: a value put into a sentence is written out'
        File = 'src\Bws.Cli\Texts.cs'
        From = 'value is string text ? Printable.Of(text) : value;'
        To = 'value;'
        Test = 'TerminalTextTests.The_report_of_one_entry_moves_nothing'
    },
    @{
        Name = 'printable: a listing cell is written out'
        File = 'src\Bws.Cli\ListingTable.cs'
        From = '.Select(extra => extra!)).Select(Printable.Of)];'
        To = '.Select(extra => extra!))];'
        Test = 'TerminalTextTests.A_listing_row_stays_one_row_and_moves_nothing'
    },
    @{
        Name = 'typed: a name with a space is quoted'
        File = 'src\Bws.Core\Planning\EquivalentCommand.cs'
        From = 'if (serviceName.Length > 0 && serviceName.All(character => char.IsLetterOrDigit(character) || character is ''.'' or ''_'' or ''-''))'
        To = 'if (serviceName.Length > 0 && serviceName.All(character => character != ''"''))'
        Test = 'EquivalentCommandBridgeGuards.A_name_that_needs_quoting_reaches_this_tool_as_itself'
    },
    @{
        # The bridge cannot catch this one and the entry names the test that can: the bridge models
        # how PowerShell cuts a line into words, not what it does with a dollar inside double quotes.
        Name = 'typed: a dollar sends the name to single quotes'
        File = 'src\Bws.Core\Planning\EquivalentCommand.cs'
        From = 'if (serviceName.AsSpan().IndexOfAny(ReadInsideDoubleQuotes) < 0)'
        To = 'if (serviceName.AsSpan().IndexOfAny(ReadInsideDoubleQuotes) < 99)'
        Test = 'EquivalentCommandTests.A_name_is_quoted_only_as_much_as_a_shell_needs'
    },
    @{
        Name = 'typed: a quote mark inside single quotes is doubled'
        File = 'src\Bws.Core\Planning\EquivalentCommand.cs'
        From = 'if (SingleQuoteMarks.Contains(character))'
        To = 'if (SingleQuoteMarks.Contains(character) && character == ''\0'')'
        Test = 'EquivalentCommandTests.A_name_is_quoted_only_as_much_as_a_shell_needs'
    },
    @{
        Name = 'line: a name that begins with a dash comes after the end of the switches'
        File = 'src\Bws.Core\Planning\EquivalentCommand.cs'
        From = 'string[] parts = serviceName.StartsWith(''-'')'
        To = 'string[] parts = serviceName.StartsWith(''\0'')'
        Test = 'EquivalentCommandBridgeGuards.A_name_that_needs_quoting_reaches_this_tool_as_itself'
    },
    @{
        Name = 'end of switches: every word after it is a name'
        File = 'src\Bws.Cli\CommandLine.Reading.cs'
        From = 'if (namesOnly || !argument.StartsWith(''-''))'
        To = 'if (!argument.StartsWith(''-''))'
        Test = 'ArgumentRefusalTests.After_the_end_of_the_switches_a_word_that_begins_with_a_dash_is_a_name'
    },
    @{
        Name = 'snapshot: its verb is the first word that is not a switch'
        File = 'src\Bws.Cli\CommandLine.Reading.cs'
        From = 'snapshotVerbDue = true;'
        To = 'snapshotVerbDue = arguments.Length < 0;'
        Test = 'ArgumentRefusalTests.Snapshot_finds_its_verb_past_a_switch_and_still_names_a_wrong_one'
    },
    @{
        Name = 'timing: asking who depends on what is not counted as filtering'
        File = 'src\Bws.Cli\Execution.cs'
        From = 'spent.Measured - spent.Asked;'
        To = 'spent.Measured;'
        Test = 'ListingContractTests.Asking_who_depends_on_what_is_timed_apart_from_filtering'
    },
    @{
        Name = 'refusal: no such entry still says what the reading admits'
        File = 'src\Bws.Cli\Program.cs'
        From = @'
            Console.Error.WriteLine(Texts.Of("cli.show.noSuchEntry", options.ServiceName));
            Execution.Report(entries, result: null, options, new Spent(read, stopwatch.ElapsedMilliseconds));
'@
        To = @'
            Console.Error.WriteLine(Texts.Of("cli.show.noSuchEntry", options.ServiceName));
'@
        Test = 'PlanContractTests.A_refusal_after_the_reading_still_says_what_the_reading_has_to_admit'
    },
    @{
        Name = 'refusal: a refused plan still says what the reading admits'
        File = 'src\Bws.Cli\Program.cs'
        From = @'
                Console.Error.WriteLine(PlanText.Describe(problem));
            }

            Execution.Report(entries, result: null, options, new Spent(read, stopwatch.ElapsedMilliseconds));
'@
        To = @'
                Console.Error.WriteLine(PlanText.Describe(problem));
            }
'@
        Test = 'PlanContractTests.A_refusal_after_the_reading_still_says_what_the_reading_has_to_admit'
    },

    # ---- Package I of the external stability report, 2026-10-05 (G-3, G-4, G-7, G-8, G-9, 504) ----
    @{
        Name = 'layout: a file left alone is not written over'
        File = 'src\Bws.Gui\KeptColumns.cs'
        From = '        if (_reading.LeftAlone || _left)'
        To = '        if (_left)'
        Test = 'LeftAloneGuards.A_file_from_another_schema_is_not_written_over_by_anything_the_window_does'
    },
    @{
        Name = 'layout: a file nobody could open is left alone'
        File = 'src\Bws.Gui\ViewModels\ColumnLayout.cs'
        From = '        || Unopened is not null'
        To = '        || false'
        Test = 'LeftAloneGuards.A_file_nobody_could_open_is_not_written_over_once_it_frees_up'
    },
    @{
        Name = 'layout: a damaged file that could not be moved is left alone'
        File = 'src\Bws.Gui\ViewModels\ColumnLayout.cs'
        From = '        || (Unreadable is not null && MovedAside is null);'
        To = '        || false;'
        Test = 'LeftAloneGuards.A_damaged_file_that_could_not_be_moved_is_not_written_over_later'
    },
    @{
        # Both catches at once - Replace takes every occurrence, and both are the same claim.
        Name = 'layout: a file that could not be opened is not called unreadable'
        File = 'src\Bws.Gui\PreferencesFile.cs'
        From = 'return new LayoutReading { Unopened = problem.Message };'
        To = 'return new LayoutReading { Unreadable = problem.Message };'
        Test = 'LeftAloneGuards.A_file_nobody_could_open_is_not_written_over_once_it_frees_up'
    },
    @{
        Name = 'layout: a window that handed itself over writes nothing more'
        File = 'src\Bws.Gui\KeptColumns.cs'
        From = 'internal void Leave() => _left = true;'
        To = 'internal void Leave() => _left = false;'
        Test = 'LeftAloneGuards.A_window_that_has_handed_itself_over_writes_nothing_more'
    },
    @{
        Name = 'hand-over: a query longer than the reading side takes stays behind'
        File = 'src\Bws.Gui\ViewModels\HandOver.cs'
        From = 'var carried = Query.Length <= LongestQuery ? this : this with { Query = string.Empty, QueryLeftBehind = true };'
        To = 'var carried = this;'
        Test = 'HandOverTests.A_query_longer_than_the_reading_side_takes_stays_behind_and_the_rest_crosses'
    },
    @{
        # Without the query-only candidate the picks go with it - the Polish query case exactly.
        Name = 'hand-over: a query too long once written goes before the picks'
        File = 'src\Bws.Gui\ViewModels\HandOver.cs'
        From = @'
        return Fitting(carried)
            ?? Fitting(withoutQuery)
'@
        To = @'
        return Fitting(carried)
'@
        Test = 'HandOverTests.A_query_too_long_once_written_goes_before_the_picks_do'
    },
    @{
        Name = 'hand-over: a query both carried and left behind is refused'
        File = 'src\Bws.Gui\ViewModels\HandOver.cs'
        From = @'
            || (wire.QLeft && query.Length > 0)

'@
        To = @'

'@
        Test = 'HandOverTests.A_query_both_carried_and_left_behind_is_refused'
    },
    @{
        Name = 'hand-over: the new window says the query stayed behind'
        File = 'src\Bws.Gui\MainWindow.HandOver.cs'
        From = '            _model.Says.AboutTheHandOver(Texts.Of("gui.handOver.queryLeftBehind"));'
        To = ''
        Test = 'HandOverGuards.A_query_left_behind_is_said'
    },
    @{
        Name = 'hand-over: the take-over does not wait for the pointer'
        File = 'src\Bws.Gui\MainWindow.HandOver.cs'
        From = @'
        _model.Interacting = false;

        _model.ShowingOverview = false;
'@
        To = @'
        _model.ShowingOverview = false;
'@
        Test = 'HandOverGuards.The_hand_over_lands_even_with_the_pointer_resting_on_the_list'
    },
    @{
        Name = 'hand-over: a sheet from a failure hands over the entry it is about'
        File = 'src\Bws.Gui\MainWindow.Menu.cs'
        From = '_asked = (offer.Kind, null, false, false, [offer.ServiceName]);'
        To = '_asked = (offer.Kind, null, false, false, null);'
        Test = 'HandOverGuards.A_sheet_opened_from_a_failure_hands_over_the_entry_it_is_about'
    },
    @{
        Name = 'search box: a restart carries what the box says'
        File = 'src\Bws.Gui\MainWindow.HandOver.cs'
        From = @'
        Search.Commit();

        var showing
'@
        To = @'
        var showing
'@
        Test = 'BoxCommitGuards.A_restart_carries_what_the_box_says_rather_than_what_the_model_heard'
    },
    @{
        Name = 'search box: escape hands the box over first'
        File = 'src\Bws.Gui\MainWindow.Keyboard.cs'
        # Re-anchored 2026-10-20: the searches of the views are handed over right after it.
        From = @'
                Search.Commit();
                ChangeHistory.Choices.Commit();
'@
        To = @'
                ChangeHistory.Choices.Commit();
'@
        Test = 'BoxCommitGuards.Escape_straight_after_typing_empties_the_box'
    },
    @{
        Name = 'search box: losing the keyboard hands the text over'
        File = 'src\Bws.Gui\SearchRow.xaml.cs'
        From = '        QueryBox.LostKeyboardFocus += (_, _) => Commit();'
        To = ''
        Test = 'BoxCommitGuards.The_box_hands_its_text_over_when_it_loses_the_keyboard'
    },
    @{
        # Inside the helper rather than its call - a mutant leaving a private method unused is BROKEN.
        Name = 'menus: a shortcut whose button is put away opens nothing'
        File = 'src\Bws.Gui\ButtonMenu.cs'
        From = 'if (at is UIElement { Visibility: not Visibility.Visible })'
        To = 'if (at is UIElement { Visibility: Visibility.Hidden })'
        Test = 'HelpMenuGuards.F1_on_the_overview_opens_no_menu_under_a_button_that_is_put_away'
    },
    @{
        Name = 'mishaps: a window that has not loaded is nobody to tell'
        File = 'src\Bws.Gui\Mishaps.cs'
        From = 'window is { IsLoaded: true, DataContext: MainViewModel model } ? model : null;'
        To = 'window is { DataContext: MainViewModel model } ? model : null;'
        Test = 'MishapGuards.A_window_that_has_not_loaded_is_nobody_to_tell'
    },
    @{
        Name = 'mishaps: with nobody to tell the last word is said'
        File = 'src\Bws.Gui\Mishaps.cs'
        From = @'
        last(failure);

        return false;
'@
        To = @'
        return false;
'@
        Test = 'MishapGuards.With_nobody_to_tell_the_failure_goes_to_the_last_word_and_only_then'
    },
    @{
        Name = 'export: the separator is the one windows names'
        File = 'src\Bws.Gui\ViewModels\Exporting.cs'
        From = @'
        return listed.Length == 0 || listed.Any(character => character is '"' or '\r' or '\n') ? "," : listed;
'@
        To = @'
        return listed.Length < 0 ? listed : ",";
'@
        Test = 'ExportingGuards.The_file_is_split_the_way_windows_says_and_a_value_holding_the_separator_is_quoted'
    },
    @{
        Name = 'export: a separator the format cannot carry falls back to the comma'
        File = 'src\Bws.Gui\ViewModels\Exporting.cs'
        From = @'
        return listed.Length == 0 || listed.Any(character => character is '"' or '\r' or '\n') ? "," : listed;
'@
        To = @'
        return listed.Length < 0 ? "," : listed;
'@
        Test = 'ExportingGuards.A_separator_the_format_cannot_carry_falls_back_to_the_comma'
    },
    @{
        Name = 'export: the folded copies are said'
        File = 'src\Bws.Gui\ViewModels\Exporting.cs'
        From = '        var folded = rows.Sum(row => row.Instances.Count);'
        To = '        var folded = rows.Count - rows.Count;'
        Test = 'ExportingGuards.The_sentence_says_how_many_copies_went_in_folded'
    },
    @{
        # The old early return put back. One entry for both roads through Apply - the scope and the
        # tick - because the mutant is the same text, and A_typo_in_the_box_does_not_freeze_the_list_
        # against_the_machine holds the second road against the same line.
        Name = 'search: a typo does not freeze the list'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.cs'
        From = '            AboutTheQuery(string.Join(" ", parsed.Problems.Select(QueryMessages.Of)));'
        To = '            AboutTheQuery(string.Join(" ", parsed.Problems.Select(QueryMessages.Of)));' + [char]10 + '            if (Rows.Count >= 0) { _filters.Rethink(); return; }'
        Test = 'ScopeTests.A_typo_in_the_box_does_not_freeze_the_list_against_the_scope'
    },
    @{
        # The old order put back: flag and sentence before the try, so the throw escapes the net.
        Name = 'second pass: a sentence that throws on setting out is inside the net'
        File = 'src\Bws.Gui\ViewModels\Readings.SecondPhase.cs'
        From = '        IReadOnlyList<ScmEntry> filled;'
        To = @'
        _filling = true;
        _announce();
        IReadOnlyList<ScmEntry> filled;
'@
        Test = 'SecondPassSettingOutTests.A_sentence_that_throws_on_setting_out_does_not_leave_the_list_filling'
    },
    @{
        Name = 'overview: counts only once a reading has arrived'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Overview.cs'
        From = 'counted: _readings.EverRead'
        To = 'counted: !_readings.FirstLook'
        Test = 'OverviewEdgeGuards.A_first_reading_that_failed_counts_nothing_rather_than_zero'
    },
    @{
        Name = 'overview: a line asked from drivers lands on a list that can answer it'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Overview.cs'
        From = '            Scope = EntryScope.Everything;'
        To = ''
        Test = 'OverviewEdgeGuards.A_line_asked_from_the_drivers_tab_lands_on_a_list_that_can_answer_it'
    },
    @{
        Name = 'language: a file whose root is not an object leaves english standing'
        File = 'src\Bws.Gui\Texts.cs'
        From = 'if (document.RootElement.ValueKind != JsonValueKind.Object)'
        To = 'if (document.RootElement.ValueKind == JsonValueKind.Undefined)'
        Test = 'LanguageGuards.A_language_file_that_is_not_an_object_leaves_english_standing'
    },
    @{
        # SECURITY REPORT S-2, 2026-10-06. Turning the property back on must redden the guard that
        # asks the RUNTIME, which is the whole point of asking it rather than reading the line.
        Name = 'startup hooks: every process this repository builds is told they are not supported'
        File = 'Directory.Build.props'
        From = '<StartupHookSupport>false</StartupHookSupport>'
        To = '<StartupHookSupport>true</StartupHookSupport>'
        Test = 'StartupHookGuards.The_runtime_is_told_startup_hooks_are_not_supported'
    },
    @{
        # SECURITY REPORT S-3, 2026-10-06, and the next five with it: nothing a launch command names
        # may follow the environment of the process reading it.
        Name = 'environment: a folder the system injects is not read from this process'
        File = 'src\Bws.Core\ManagerEnvironment.cs'
        From = '            ["ProgramFiles"] = _ => Folders.Value.ProgramFiles,'
        To = '            ["ProgramFiles"] = _ => Environment.GetEnvironmentVariable("ProgramFiles"),'
        Test = 'ManagerEnvironmentTests.A_folder_the_system_injects_does_not_follow_this_process'
    },
    @{
        Name = 'environment: ProgramData comes from the profile list rather than this process'
        File = 'src\Bws.Core\ManagerEnvironment.cs'
        From = '            ["ProgramData"] = windows => Profiles("ProgramData", windows),'
        To = '            ["ProgramData"] = _ => Environment.GetEnvironmentVariable("ProgramData"),'
        Test = 'ManagerEnvironmentTests.ProgramData_does_not_follow_this_process_either'
    },
    @{
        # The fallback that stood here until 2026-10-06, put back.
        Name = 'environment: a name only this process has is not borrowed from it'
        File = 'src\Bws.Core\ManagerEnvironment.cs'
        From = '            ? Expand(written, windowsDirectory, depth - 1)'
        To = '            ? Expand(written, windowsDirectory, depth - 1) : Environment.GetEnvironmentVariable(name) is { } borrowed ? borrowed'
        Test = 'ManagerEnvironmentTests.A_variable_only_this_process_has_stays_as_written'
    },
    @{
        # The framework's own reading expands with THIS process's variables - measured 2026-10-06.
        # Caught only in a process where the block is first read with SystemRoot replaced, which is
        # what a filtered run of the one named test is.
        Name = 'environment: the machine block is read as written'
        File = 'src\Bws.Core\ManagerEnvironment.cs'
        From = 'RegistryValueOptions.DoNotExpandEnvironmentNames'
        To = 'RegistryValueOptions.None'
        Test = 'ManagerEnvironmentTests.A_machine_variable_naming_another_is_expanded_without_this_process'
    },
    @{
        Name = 'path: nothing found under an unknown variable is not read rather than missing'
        File = 'src\Bws.Core\BinaryPathResolver.cs'
        From = '                && ManagerEnvironment.Unresolved(written, WindowsDirectory);'
        To = '                && ManagerEnvironment.Unresolved(written, WindowsDirectory) && written.Length < 0;'
        Test = 'ManagerEnvironmentTests.Nothing_found_under_an_unknown_name_is_not_read_rather_than_missing'
    },
    @{
        Name = 'runtime settings: a profiler switched on is named'
        File = 'src\Bws.Core\Session.cs'
        From = '        "CORECLR_ENABLE_PROFILING",'
        To = ''
        Test = 'RuntimeSettingsTests.A_setting_that_is_on_is_named'
    },
    @{
        Name = 'runtime settings: a setting that is 0 is not named'
        File = 'src\Bws.Core\Session.cs'
        From = '!string.IsNullOrWhiteSpace(value) && value.Trim() != "0";'
        To = '!string.IsNullOrWhiteSpace(value);'
        Test = 'RuntimeSettingsTests.A_setting_that_is_off_or_empty_is_not_named'
    },
    @{
        Name = 'runtime settings: the terminal names them only with administrator rights'
        File = 'src\Bws.Cli\Execution.cs'
        From = '        elevated && set.Count > 0'
        To = '        set.Count > 0'
        Test = 'ElevationNoticeGuards.Nothing_is_said_without_rights_or_without_settings'
    },
    @{
        # RE-ANCHORED 2026-10-06 with the three below (security report S-9): Rights became statements.
        Name = 'runtime settings: the window names them only with administrator rights'
        File = 'src\Bws.Gui\ViewModels\Sentences.cs'
        From = '        if (!elevated)'
        To = '        if (!elevated && settings.Count == 0)'
        Test = 'RuntimeSettingsNoticeTests.A_window_without_rights_says_the_list_is_short_and_nothing_about_settings'
    },
    @{
        Name = 'runtime settings: an elevated window names what it carries'
        File = 'src\Bws.Gui\ViewModels\Sentences.cs'
        From = '        if (settings.Count > 0)'
        To = '        if (settings.Count > 99)'
        Test = 'RuntimeSettingsNoticeTests.An_elevated_window_names_the_settings_it_carries'
    },
    @{
        Name = 'runtime settings: the line is built from what the window carries'
        File = 'src\Bws.Gui\ViewModels\Says.cs'
        From = 'Sentences.Rights(Elevated, RuntimeSettings, BesideNotRead)'
        To = 'Sentences.Rights(Elevated, [], BesideNotRead)'
        Test = 'RuntimeSettingsNoticeTests.An_elevated_window_names_the_settings_it_carries'
    },
    # SECURITY REPORT S-6, 2026-10-06 (package SC): a publisher name the signature does not vouch
    # for. Three classes share the test class name across three projects, so these name the tests
    # in full - the runner filters the whole solution with ~.
    @{
        # The one rule. Letting every signed verdict vouch puts the changed Microsoft file back
        # among the Microsoft ones.
        Name = 'vouching: only a trusted verdict vouches for the publisher'
        File = 'src\Bws.Core\BinarySignature.cs'
        From = 'public bool VouchesForPublisher => Status == SignatureStatus.Trusted;'
        To = 'public bool VouchesForPublisher => Status != SignatureStatus.NotSigned;'
        Test = 'Bws.Core.Tests.UnvouchedPublisherTests.Only_a_trusted_verdict_vouches_for_the_name'
    },
    @{
        # Disconnecting the question from the field is the shape this would rot into.
        Name = 'publisher query: the field asks whether the name is vouched for'
        File = 'src\Bws.Core\Querying\QueryFields.cs'
        From = '            Unvouched = QuerySymbols.PublisherUnvouched'
        To = '            Unvouched = _ => false'
        Test = 'Bws.Core.Tests.UnvouchedPublisherTests.Leaving_microsoft_out_keeps_the_microsoft_file_somebody_changed_and_says_so'
    },
    @{
        # The second case the rule was tried against: without the name check every unsigned file
        # turns unsure.
        Name = 'publisher query: an unsigned file is a certain no'
        File = 'src\Bws.Core\Querying\QuerySymbols.cs'
        From = '        && entry.Signature.Value!.Publisher is not null'
        To = '        && (entry.Signature.Value!.Publisher is not null || true)'
        Test = 'Bws.Core.Tests.UnvouchedPublisherTests.A_file_nobody_signed_is_certainly_not_from_microsoft'
    },
    @{
        Name = 'publisher query: alternatives keep the reservation'
        File = 'src\Bws.Core\Querying\QueryValues.cs'
        From = '        Unvouched || other.Unvouched);'
        To = '        Unvouched && other.Unvouched);'
        Test = 'Bws.Core.Tests.UnvouchedPublisherTests.Alternatives_inside_one_member_keep_the_reservation'
    },
    @{
        Name = 'publisher query: a member that doubts reaches the answer'
        File = 'src\Bws.Core\Querying\Query.cs'
        From = '            unvouched |= verdict.Unvouched;'
        To = '            unvouched |= verdict.Unreadable;'
        Test = 'Bws.Core.Tests.UnvouchedPublisherTests.Asking_for_microsoft_leaves_the_changed_file_out_and_says_so'
    },
    @{
        Name = 'publisher query: the answer counts what it doubted'
        File = 'src\Bws.Core\Querying\Query.cs'
        From = '            if (match.Unvouched)'
        To = '            if (match.Unreadable)'
        Test = 'Bws.Core.Tests.UnvouchedPublisherTests.Leaving_microsoft_out_keeps_the_microsoft_file_somebody_changed_and_says_so'
    },
    @{
        Name = 'publisher window: the list counts what it doubted'
        File = 'src\Bws.Gui\ViewModels\Narrowing.cs'
        From = '            if (match.Unvouched)'
        To = '            if (match.Unreadable)'
        Test = 'Bws.Gui.Tests.UnvouchedPublisherTests.Asking_for_microsoft_leaves_a_changed_file_out_and_the_window_says_why'
    },
    @{
        Name = 'publisher window: the middle of an empty list says nothing that could be checked'
        File = 'src\Bws.Gui\ViewModels\Says.cs'
        From = '        _partial = answer.Unreadable > 0 || answer.TooCostly > 0 || answer.Unvouched > 0;'
        To = '        _partial = answer.Unreadable > 0 || answer.TooCostly > 0;'
        Test = 'Bws.Gui.Tests.UnvouchedPublisherTests.Asking_for_microsoft_leaves_a_changed_file_out_and_the_window_says_why'
    },
    @{
        # Held back like the refusal sentence, it would vanish for the whole of the pass.
        Name = 'publisher window: the sentence is not held back while signatures are read'
        File = 'src\Bws.Gui\ViewModels\Sentences.cs'
        From = '        if (answer.Unvouched > 0)'
        To = '        if (answer.Unvouched > 0 && unread == ExtraRead.None)'
        Test = 'Bws.Gui.Tests.UnvouchedPublisherTests.The_line_under_the_box_says_it_even_while_signatures_are_being_read'
    },
    @{
        Name = 'publisher window: the cell marks a name nobody vouches for'
        File = 'src\Bws.Gui\ViewModels\CellFaces.cs'
        From = '            : Say(reading, signature => signature.VouchesForPublisher'
        To = '            : Say(reading, signature => !signature.VouchesForPublisher'
        Test = 'Bws.Gui.Tests.UnvouchedPublisherTests.The_publisher_cell_marks_a_name_nobody_vouches_for'
    },
    @{
        Name = 'publisher terminal: show marks a name nobody vouches for'
        File = 'src\Bws.Cli\EntryReport.cs'
        From = '        return signature.Value.VouchesForPublisher'
        To = '        return !signature.Value.VouchesForPublisher'
        Test = 'Bws.Cli.Tests.UnvouchedPublisherTests.Show_marks_a_publisher_nobody_vouches_for'
    },
    @{
        Name = 'publisher terminal: the listing cell marks it inside the brackets'
        File = 'src\Bws.Cli\ListingTable.cs'
        From = '            : signature.VouchesForPublisher ? Texts.Of("cli.cell.signedBy", status, signature.Publisher)'
        To = '            : !signature.VouchesForPublisher ? Texts.Of("cli.cell.signedBy", status, signature.Publisher)'
        Test = 'Bws.Cli.Tests.UnvouchedPublisherTests.The_listing_cell_says_it_inside_the_brackets'
    },
    @{
        Name = 'publisher terminal: a query says how many entries rest on a doubted name'
        File = 'src\Bws.Cli\Execution.cs'
        From = '        if (result.Unvouched > 0)'
        To = '        if (result.Unvouched > 3)'
        Test = 'Bws.Cli.Tests.UnvouchedPublisherTests.A_query_says_how_many_entries_rest_on_a_publisher_nobody_vouches_for'
    },
    @{
        # BACKLOG 531, found while building S-6: with WTD_SAFER_FLAG a changed file reads as having no
        # signature, so Tampered never arrived for a file with its own. The mutation puts it back.
        Name = 'signatures: a file changed after signing is tampered, not unsigned'
        File = 'src\Bws.Core\WindowsBinaryInspector.cs'
        From = '            : WINTRUST_DATA_PROVIDER_FLAGS.WTD_CACHE_ONLY_URL_RETRIEVAL'
        To = '            : WINTRUST_DATA_PROVIDER_FLAGS.WTD_SAFER_FLAG | WINTRUST_DATA_PROVIDER_FLAGS.WTD_CACHE_ONLY_URL_RETRIEVAL'
        Test = 'Bws.Integration.Tests.TamperedSignatureTests.A_file_changed_after_it_was_signed_is_tampered_and_nobody_vouches_for_its_publisher'
    },
    @{
        # PACKAGE SB, 2026-10-06, security report S-5: one handle for the whole inspection, opened
        # without write or delete sharing. With write sharing the file could change under it.
        Name = 'one opening: the file is opened without write sharing'
        File = 'src\Bws.Core\WindowsBinaryInspector.cs'
        From = 'handle = File.OpenHandle(file, FileMode.Open, FileAccess.Read, FileShare.Read);'
        To = 'handle = File.OpenHandle(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);'
        Test = 'Bws.Core.Tests.OneOpeningTests.A_file_somebody_holds_open_for_writing_is_refused_in_all_three_fields_alike'
    },
    @{
        # MISSED on its first run, 2026-10-06, and rightly: through Inspect the path beside the handle is
        # the handle's own final path, so WinTrust read the same file either way. The guard moved to the
        # signature alone, asked with another file's path.
        Name = 'one opening: WinTrust verifies the handle, not the path beside it'
        File = 'src\Bws.Core\WindowsBinaryInspector.cs'
        From = '                hFile = raw'
        To = '                hFile = default'
        Test = 'Bws.Core.Tests.OneOpeningTests.The_signature_is_the_one_on_the_handle_even_beside_another_file_s_path'
    },
    @{
        Name = 'one opening: the version is asked through the final path of the handle'
        File = 'src\Bws.Core\WindowsBinaryInspector.cs'
        From = '            Version(where),'
        To = '            Version(Reading<string>.Present(file)),'
        Test = 'Bws.Core.Tests.OneOpeningTests.Every_field_is_about_the_file_the_handle_holds_and_none_about_the_path_beside_it'
    },
    @{
        # Reads the first chunk and stops: the offset jumps past the end after one read.
        Name = 'one opening: the hash covers the whole file, not its first chunk'
        File = 'src\Bws.Core\WindowsBinaryInspector.cs'
        From = 'for (long offset = 0; (read = RandomAccess.Read(handle, buffer, offset)) > 0; offset += read)'
        To = 'for (long offset = 0; (read = RandomAccess.Read(handle, buffer, offset)) > 0; offset = long.MaxValue)'
        Test = 'Bws.Core.Tests.OneOpeningTests.Every_field_is_about_the_file_the_handle_holds_and_none_about_the_path_beside_it'
    },
    @{
        # The publisher read only when there is no state to read it from - so never. Keeps the local
        # used, which a mutant leaving it unused would not compile.
        Name = 'one opening: the publisher comes out of the verification that ran'
        File = 'src\Bws.Core\WindowsBinaryInspector.cs'
        From = 'return Settle(result, networkPaths, () => Signer(state));'
        To = 'return Settle(result, networkPaths, () => state == default ? Signer(state) : null);'
        Test = 'Bws.Core.Tests.OneOpeningTests.Every_field_is_about_the_file_the_handle_holds_and_none_about_the_path_beside_it'
    },
    @{
        # Rest of security report S-4: the shape decides before anything is opened.
        Name = 'file shape: a pipe is not opened even when the network may be followed'
        File = 'src\Bws.Core\WindowsBinaryInspector.cs'
        From = 'if (OffLimits(file) || !FileShape.NamesAFile(file))'
        To = 'if (OffLimits(file))'
        Test = 'Bws.Core.Tests.OneOpeningTests.A_pipe_is_not_opened_even_when_the_network_may_be_followed'
    },
    @{
        Name = 'file shape: the pipe namespace of a server is not a share'
        File = 'src\Bws.Core\FileShape.cs'
        From = '            && !share.Equals("pipe", StringComparison.OrdinalIgnoreCase)'
        To = '            && !share.Equals("pipes", StringComparison.OrdinalIgnoreCase)'
        Test = 'FileShapeTests.A_pipe_a_mailslot_a_device_and_an_object_name_do_not'
    },
    @{
        Name = 'file shape: only the question-mark spelling of UNC names a share'
        File = 'src\Bws.Core\FileShape.cs'
        From = '(value[2] == ''?'' && name.StartsWith'
        To = '(value[2] is ''?'' or ''.'' && name.StartsWith'
        Test = 'FileShapeTests.A_pipe_a_mailslot_a_device_and_an_object_name_do_not'
    },
    @{
        # The owner's decision of 2026-10-06: the version in the file's own resource. The mutant asks
        # the way System.Diagnostics.FileVersionInfo asks. Until 2026-10-16 the test asked svchost.exe in
        # System32 and was caught only where a language file disagrees with the binary (the owner's
        # machine, 8737 against 8875) - MISSED twice on GitHub. Since then the test copies the binary
        # and its language files with their version moved, so the disagreement exists on any machine.
        Name = 'version: the file''s own resource, not a language file'
        File = 'src\Bws.Core\WindowsBinaryInspector.Version.cs'
        From = 'private const GET_FILE_VERSION_INFO_FLAGS FromTheFileItself = default;'
        To = 'private const GET_FILE_VERSION_INFO_FLAGS FromTheFileItself = GET_FILE_VERSION_INFO_FLAGS.FILE_VER_GET_LOCALISED;'
        Test = 'OwnVersionTests.The_version_of_a_system_binary_is_the_one_in_its_own_resource'
    },
    @{
        # Schema six, 2026-10-06: a five against a six does not compare file versions.
        Name = 'diff: file versions are not compared across schema six'
        File = 'src\Bws.Core\Snapshots\ComparisonCaveats.cs'
        From = '(before.SchemaVersion >= OwnFileVersionSince) != (after.SchemaVersion >= OwnFileVersionSince),'
        To = 'false,'
        Test = 'SnapshotGoldenTests.A_version_five_file_is_read_and_its_file_versions_are_not_compared'
    },
    @{
        Name = 'diff: the file version caveat takes the field out of every entry'
        File = 'src\Bws.Core\Snapshots\ComparisonCaveats.cs'
        From = '.. FileVersionSourceDiffers ? VersionedDifferently : []'
        To = '.. FileVersionSourceDiffers ? VersionedDifferently[..0] : []'
        Test = 'SnapshotGoldenTests.A_version_five_file_is_read_and_its_file_versions_are_not_compared'
    },
    @{
        Name = 'diff: the file version caveat is said in the terminal'
        File = 'src\Bws.Cli\DiffText.cs'
        From = '(caveats.FileVersionSourceDiffers, () => Texts.Of("cli.diff.caveat.fileVersion")),'
        To = '(false, () => Texts.Of("cli.diff.caveat.fileVersion")),'
        Test = 'Bws.Cli.Tests.DiffOutputTests.The_caveat_of_schema_six_is_said_and_written_last'
    },

    # PACKAGE SD OF THE SECURITY REPORT, 2026-10-06 - S-7 and S-9, owner's decisions that day.
    # docs/PROJEKT-PACZKA-SD-20261006.md carries the measurements each one stands on.
    @{
        # A process under runas /trustlevel:0x20000 answers TokenElevationTypeFull with the role
        # saying no. The mutant lets the type alone make a twin.
        Name = 'session: a twin needs the administrator role as well as the type'
        File = 'src\Bws.Core\Session.cs'
        From = '        elevated && type != TOKEN_ELEVATION_TYPE.TokenElevationTypeDefault;'
        To = '        (elevated || type is not null) && type != TOKEN_ELEVATION_TYPE.TokenElevationTypeDefault;'
        Test = 'ElevationTwinTests.A_twin_needs_the_role_and_a_type_that_is_not_the_one_without_a_linked_token'
    },
    @{
        # The safe side: a type nobody could read counts as a twin, so the window writes less.
        Name = 'session: a type nobody could read counts as a twin'
        File = 'src\Bws.Core\Session.cs'
        From = '        elevated && type != TOKEN_ELEVATION_TYPE.TokenElevationTypeDefault;'
        To = '        elevated && type == TOKEN_ELEVATION_TYPE.TokenElevationTypeFull;'
        Test = 'ElevationTwinTests.A_twin_needs_the_role_and_a_type_that_is_not_the_one_without_a_linked_token'
    },
    @{
        # S-7: an administrator window under UAC creates no folder and writes no file in the profile.
        # A filter that is never true rather than `false`, which the compiler folds and refuses.
        Name = 'layout: an administrator window under UAC writes nothing into the profile'
        File = 'src\Bws.Gui\PreferencesFile.cs'
        From = @'
        if (LeavesTheProfileAlone)
        {
            return Texts.Of("gui.layout.notKeptAsAdministrator");
'@
        To = @'
        if (LeavesTheProfileAlone && _directory.Length < 0)
        {
            return Texts.Of("gui.layout.notKeptAsAdministrator");
'@
        Test = 'ProfileLeftAloneGuards.An_administrator_window_creates_no_folder_and_writes_nothing'
    },
    @{
        # S-7: and moves nothing aside - a move is a write too, of a name somebody else may have put there.
        Name = 'layout: an administrator window under UAC moves nothing aside'
        File = 'src\Bws.Gui\PreferencesFile.cs'
        From = @'
        if (LeavesTheProfileAlone)
        {
            return null;
'@
        To = @'
        if (LeavesTheProfileAlone && _directory.Length < 0)
        {
            return null;
'@
        Test = 'ProfileLeftAloneGuards.An_administrator_window_leaves_a_damaged_layout_where_it_is_and_says_so'
    },
    @{
        Name = 'layout: the window asks the session whether to leave the profile alone'
        File = 'src\Bws.Gui\PreferencesFile.cs'
        From = '        : this(InTheProfile(), Session.IsElevatedWithTwin())'
        To = '        : this(InTheProfile(), !Session.IsElevatedWithTwin())'
        Test = 'ProfileLeftAloneGuards.The_window_asks_the_session_whether_to_leave_the_profile_alone'
    },
    @{
        # The sentence for a move that failed says "could not be moved" - untrue when none was tried.
        Name = 'layout: a file left alone by rule is not said to have failed a move'
        File = 'src\Bws.Gui\KeptColumns.cs'
        From = '        : _file.LeavesTheProfileAlone ? Texts.Of("gui.layout.unreadableLeftAsAdministrator", why)'
        To = '        : _file.LeavesTheProfileAlone && why.Length < 0 ? Texts.Of("gui.layout.unreadableLeftAsAdministrator", why)'
        Test = 'ProfileLeftAloneGuards.An_administrator_window_leaves_a_damaged_layout_where_it_is_and_says_so'
    },
    @{
        # S-9: a session with administrator rights reads no language file beside the program.
        Name = 'language: an administrator session does not read the file beside the program'
        File = 'src\Bws.Gui\Texts.cs'
        From = '        if (elevated)'
        To = '        if (!elevated)'
        Test = 'BesideLanguageGuards.An_administrator_session_does_not_read_the_file_beside_the_program'
    },
    @{
        Name = 'language: the file not read is named only when it is there'
        File = 'src\Bws.Gui\Texts.cs'
        From = '        return exists(file) ? file : null;'
        To = '        return file;'
        Test = 'BesideLanguageGuards.The_file_not_read_is_named_only_when_rights_kept_it_out'
    },
    @{
        Name = 'status: the translation left out is said in the line under the list'
        File = 'src\Bws.Gui\ViewModels\Sentences.cs'
        From = '        if (besideNotRead is not null)'
        To = '        if (besideNotRead is null)'
        Test = 'BesideLanguageGuards.Both_facts_about_an_administrator_session_stand_in_one_line'
    },
    @{
        Name = 'status: the window hands the translation left out to the sentence'
        File = 'src\Bws.Gui\ViewModels\Says.cs'
        From = 'Sentences.Rights(Elevated, RuntimeSettings, BesideNotRead),'
        To = 'Sentences.Rights(Elevated, RuntimeSettings, null),'
        Test = 'BesideLanguageGuards.An_administrator_window_says_which_translation_it_left_out'
    },
    @{
        # S-1, 2026-10-06: the elevated window's native libraries come only from a folder only
        # administrators can change. The rule is pure (Unpacking), the folder is examined on disk
        # (AdministratorsFolder), and Main plus the restart in Elevation.cs are not run by any test.
        Name = 'unpacking: a window without rights or outside a bundle stays and asks nothing of the disk'
        File = 'src\Bws.Core\Unpacking.cs'
        From = '        if (!facts.Elevated || !facts.Bundled)'
        To = '        if (!facts.Elevated && !facts.Bundled)'
        Test = 'Bws.Core.Tests.UnpackingTests.Without_rights_or_outside_a_bundle_nothing_is_asked_of_the_disk'
    },
    @{
        Name = 'unpacking: a bundle whose runtime names no folder refuses'
        File = 'src\Bws.Core\Unpacking.cs'
        From = '        if (facts.SearchDirectories is null || !Path.IsPathFullyQualified(facts.Home))'
        To = '        if (facts.SearchDirectories is null && !Path.IsPathFullyQualified(facts.Home))'
        Test = 'Bws.Core.Tests.UnpackingTests.A_bundle_whose_runtime_names_no_folder_refuses'
    },
    @{
        # The loop guard: told where and still outside, a third start would only be a third start.
        Name = 'unpacking: libraries still outside after the variable was set refuse'
        File = 'src\Bws.Core\Unpacking.cs'
        From = '            return Same(facts.BaseVariable, facts.Home)'
        To = '            return Same(facts.BaseVariable, facts.Home) && facts.Home.Length < 0'
        Test = 'Bws.Core.Tests.UnpackingTests.Libraries_still_outside_after_the_variable_was_set_refuse_rather_than_start_again'
    },
    @{
        Name = 'unpacking: a folder inside that fails its examination refuses'
        File = 'src\Bws.Core\Unpacking.cs'
        From = '            .FirstOrDefault(check => check.Fault is not null) is { } failed'
        To = '            .FirstOrDefault(check => check.Fault is null) is { } failed'
        Test = 'Bws.Core.Tests.UnpackingTests.A_folder_inside_that_fails_its_examination_refuses_with_that_folder'
    },
    @{
        Name = 'unpacking: libraries beside the program are trusted as the program is'
        File = 'src\Bws.Core\Unpacking.cs'
        From = '                .Where(folder => !string.Equals(folder, program, StringComparison.OrdinalIgnoreCase))'
        To = '                .Where(folder => !string.Equals(folder, program + "x", StringComparison.OrdinalIgnoreCase))'
        Test = 'Bws.Core.Tests.UnpackingTests.Libraries_beside_the_program_are_trusted_as_the_program_is'
    },
    @{
        Name = 'unpacking: inside means under a separator, not a name that begins the same'
        File = 'src\Bws.Core\Unpacking.cs'
        From = '            || whole.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);'
        To = '            || whole.StartsWith(root, StringComparison.OrdinalIgnoreCase);'
        Test = 'Bws.Core.Tests.UnpackingTests.Inside_means_the_folder_or_under_it_judged_by_where_the_path_leads'
    },
    @{
        Name = 'unpacking: a path is judged by where it leads, not by how it starts'
        File = 'src\Bws.Core\Unpacking.cs'
        From = '            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));'
        To = '            : Path.TrimEndingDirectorySeparator(path);'
        Test = 'Bws.Core.Tests.UnpackingTests.Inside_means_the_folder_or_under_it_judged_by_where_the_path_leads'
    },
    @{
        Name = 'unpacking: an administrators folder that is not ready refuses instead of moving'
        File = 'src\Bws.Core\Unpacking.cs'
        From = '        return new(prepared.Fault is null ? UnpackingVerdict.Elsewhere : UnpackingVerdict.Refused, prepared);'
        To = '        return new(UnpackingVerdict.Elsewhere, prepared);'
        Test = 'Bws.Core.Tests.UnpackingTests.An_administrators_folder_that_cannot_be_made_ready_refuses_instead_of_moving'
    },
    @{
        Name = 'admins folder: no list at all means everybody may do everything'
        File = 'src\Bws.Core\AdministratorsFolder.cs'
        From = '            return false;'
        To = '            return true;'
        Test = 'Bws.Core.Tests.AdministratorsFolderTests.No_list_at_all_means_everybody_may_do_everything'
    },
    @{
        Name = 'admins folder: an owner outside the three must be held back by owner rights'
        File = 'src\Bws.Core\AdministratorsFolder.cs'
        From = '        return (ownerTrusted || ownerHeldBack) && list.Cast<GenericAce>().All(entry => Harmless(entry, ownerTrusted));'
        To = '        return list.Cast<GenericAce>().All(entry => Harmless(entry, ownerTrusted));'
        Test = 'Bws.Core.Tests.AdministratorsFolderTests.An_owner_outside_the_three_passes_only_when_owner_rights_hold_it_back'
    },
    @{
        Name = 'admins folder: owner rights hold the owner back only when they apply to the object'
        File = 'src\Bws.Core\AdministratorsFolder.cs'
        From = '            entry.SecurityIdentifier == OwnerRights && !entry.AceFlags.HasFlag(AceFlags.InheritOnly));'
        To = '            entry.SecurityIdentifier == OwnerRights);'
        Test = 'Bws.Core.Tests.AdministratorsFolderTests.An_owner_outside_the_three_passes_only_when_owner_rights_hold_it_back'
    },
    @{
        Name = 'admins folder: generic write given to others is a right to change'
        File = 'src\Bws.Core\AdministratorsFolder.cs'
        From = '        | 0x40000000 // GENERIC_WRITE'
        To = '        | 0x00000000 // GENERIC_WRITE'
        Test = 'Bws.Core.Tests.AdministratorsFolderTests.Every_right_that_changes_something_given_to_others_fails'
    },
    @{
        Name = 'admins folder: an entry of a kind the rule does not read is not harmless'
        File = 'src\Bws.Core\AdministratorsFolder.cs'
        From = '        _ => false'
        To = '        _ => true'
        Test = 'Bws.Core.Tests.AdministratorsFolderTests.Write_for_everybody_for_a_future_creator_for_one_account_or_an_entry_of_another_kind_fails'
    },
    @{
        Name = 'admins folder: the folder is made with a list nothing above it hands down to'
        File = 'src\Bws.Core\AdministratorsFolder.cs'
        From = '        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);'
        To = '        security.SetAccessRuleProtection(isProtected: false, preserveInheritance: false);'
        Test = 'Bws.Core.Tests.AdministratorsFolderTests.The_list_the_folder_is_made_with_passes_its_own_rule'
    },
    @{
        # Somebody may make the folder first, and Create then passes without giving it the list.
        Name = 'admins folder: what is there after making it is examined, made by us or not'
        File = 'src\Bws.Core\AdministratorsFolder.cs'
        From = '        return Inspect(home);'
        To = '        return FolderCheck.Passed(home);'
        Test = 'Bws.Core.Tests.AdministratorsFolderTests.A_folder_somebody_made_first_is_judged_as_it_is_and_not_given_the_list'
    },
    @{
        Name = 'admins folder: the folder above is never made'
        File = 'src\Bws.Core\AdministratorsFolder.cs'
        From = '            if (folder.Parent is not { Exists: true })'
        To = '            if (folder.Parent is null)'
        Test = 'Bws.Core.Tests.AdministratorsFolderTests.A_folder_whose_parent_is_missing_is_not_made_and_its_parent_is_not_made_either'
    },
    @{
        Name = 'admins folder: a link is refused as a link'
        File = 'src\Bws.Core\AdministratorsFolder.cs'
        From = '            if (attributes.HasFlag(FileAttributes.ReparsePoint))'
        To = '            if (attributes.HasFlag(FileAttributes.Offline))'
        Test = 'Bws.Core.Tests.AdministratorsFolderTests.A_link_is_refused_as_a_link_and_never_followed'
    },
    @{
        Name = 'admins folder: every level from the administrators folder down is examined'
        File = 'src\Bws.Core\AdministratorsFolder.cs'
        From = '            if (Inspect(level) is { Fault: not null } failed)'
        To = '            if (Inspect(level) is { Fault: not null } failed && level.Length < 0)'
        Test = 'Bws.Core.Tests.AdministratorsFolderTests.A_level_between_the_administrators_folder_and_the_libraries_that_others_can_change_fails'
    },
    @{
        Name = 'admins folder: everything inside the libraries is examined'
        File = 'src\Bws.Core\AdministratorsFolder.cs'
        From = '        return Contents(folder);'
        To = '        return FolderCheck.Passed(folder);'
        Test = 'Bws.Core.Tests.AdministratorsFolderTests.Everything_under_the_libraries_is_examined_and_one_file_others_can_change_fails'
    },
    @{
        Name = 'admins folder: folders inside the libraries are walked too'
        File = 'src\Bws.Core\AdministratorsFolder.cs'
        From = '                waiting.Enqueue(inner);'
        To = '                _ = inner;'
        Test = 'Bws.Core.Tests.AdministratorsFolderTests.Everything_under_the_libraries_is_examined_and_one_file_others_can_change_fails'
    },
    @{
        # The opposite mutant - never a bundle, so never protected - cannot be caught by a test host,
        # which is not a bundle either. The published window is its only witness (docs/04).
        Name = 'window: a test host is not taken for a bundle'
        File = 'src\Bws.Gui\Program.cs'
        From = '    private static bool Bundled() => typeof(Program).Assembly.Location.Length == 0;'
        To = '    private static bool Bundled() => typeof(Program).Assembly.Location.Length >= 0;'
        Test = 'UnpackingRefusalTests.A_test_host_is_not_a_bundle_so_the_window_stays_where_it_is'
    },
    @{
        Name = 'unpacking refusal: deleting the folder is advised only where deleting it helps'
        File = 'src\Bws.Gui\Program.cs'
        From = '            UnpackingFault.OthersCanChange or UnpackingFault.Link or UnpackingFault.Unreadable =>'
        To = '            UnpackingFault.OthersCanChange or UnpackingFault.Link or UnpackingFault.Unreadable or UnpackingFault.StillOutside =>'
        Test = 'UnpackingRefusalTests.Deleting_the_folder_is_advised_only_where_deleting_it_helps'
    },
    @{
        Name = 'unpacking refusal: what the system said is carried into the box'
        File = 'src\Bws.Gui\Program.cs'
        From = 'string?[] lines = [Texts.Of("gui.unpacking.lead"), reason, check.Cause, advice,'
        To = 'string?[] lines = [Texts.Of("gui.unpacking.lead"), reason, advice,'
        Test = 'UnpackingRefusalTests.What_the_system_said_is_carried_and_a_folder_nobody_named_still_gets_a_reason'
    }
    # Backlog 545, 2026-10-13: the progress line goes out before the step reads the entry, so an
    # ending behind a stop is announced as conditional. Each mutant puts back the bare announcement
    # for the step behind a stop (Cascade never reaches an ending, so the condition is false there).
    @{
        Name = 'progress: the terminal announces an ending behind a stop as conditional'
        File = 'src\Bws.Cli\PlanText.cs'
        From = 'internal static string Progress(PlanStep step, int number, int count) => step.Reason == StepReason.Escalation'
        To = 'internal static string Progress(PlanStep step, int number, int count) => step.Reason == StepReason.Cascade'
        Test = 'ProgressLineTests.An_ending_behind_a_stop_is_announced_as_only_if_the_stop_did_not_work'
    }
    @{
        Name = 'progress: the window announces an ending behind a stop as conditional'
        File = 'src\Bws.Gui\ViewModels\Underway.cs'
        From = '_step = step.Reason == StepReason.Escalation'
        To = '_step = step.Reason == StepReason.Cascade'
        Test = 'UnderwayProgressTests.An_ending_behind_a_stop_is_announced_as_only_if_the_stop_did_not_work'
    }
    # 2026-10-14, after the review of PR #60: both tests took the expected line from the same key
    # the product reads, so a sentence that lost its condition passed them. These mutants change the
    # SENTENCE, not the choice of key - the first two drop the condition, the last two give it to the
    # ordinary line - and only the literal words in the tests can catch them.
    @{
        Name = 'progress: the terminal line behind a stop says the condition in words'
        File = 'src\Bws.Cli\Resources\cli.en.json'
        From = '"cli.run.progress.escalation": "[{0}/{1}] {2} {3}, if the stop did not work ...",'
        To = '"cli.run.progress.escalation": "[{0}/{1}] {2} {3} ...",'
        Test = 'ProgressLineTests.An_ending_behind_a_stop_is_announced_as_only_if_the_stop_did_not_work'
    }
    @{
        Name = 'progress: the window line behind a stop says the condition in words'
        File = 'src\Bws.Gui\Resources\gui.en.json'
        From = '"gui.plan.progress.escalation": "Step {0} of {1}: {2} {3}, if the stop did not work",'
        To = '"gui.plan.progress.escalation": "Step {0} of {1}: {2} {3}",'
        Test = 'UnderwayProgressTests.An_ending_behind_a_stop_is_announced_as_only_if_the_stop_did_not_work'
    }
    @{
        Name = 'progress: the ordinary terminal line carries no condition'
        File = 'src\Bws.Cli\Resources\cli.en.json'
        From = '"cli.run.progress": "[{0}/{1}] {2} {3} ...",'
        To = '"cli.run.progress": "[{0}/{1}] {2} {3}, if the stop did not work ...",'
        Test = 'ProgressLineTests.An_ending_with_no_stop_in_front_of_it_is_announced_as_it_is'
    }
    @{
        Name = 'progress: the ordinary window line carries no condition'
        File = 'src\Bws.Gui\Resources\gui.en.json'
        From = '"gui.plan.progress": "Step {0} of {1}: {2} {3}",'
        To = '"gui.plan.progress": "Step {0} of {1}: {2} {3}, if the stop did not work",'
        Test = 'UnderwayProgressTests.An_ending_with_no_stop_in_front_of_it_is_announced_as_it_is'
    }

    # SNAPSHOTS IN THE WINDOW AND BACKLOG 559, 2026-10-14 - W1 of docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md.
    @{
        Name = 'diff: a field refused to both sides is said once, not against the entry'
        File = 'src\Bws.Core\Snapshots\SnapshotDiff.cs'
        From = '            if (refusedByBoth.Contains(field))'
        To = '            if (refusedByBoth.Contains(field) && false)'
        Test = 'SnapshotDiffTests.The_same_refusal_in_two_languages_is_not_a_difference'
    }
    @{
        Name = 'diff: a field refused on one side only stays against its entry'
        File = 'src\Bws.Core\Snapshots\SnapshotDiff.cs'
        From = '        var refusedByBoth = Refused(before).Intersect(Refused(after), StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);'
        To = '        var refusedByBoth = Refused(before).Union(Refused(after), StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);'
        Test = 'SnapshotDiffTests.A_field_refused_on_one_side_only_stays_against_its_entry_beside_one_refused_on_both'
    }
    @{
        Name = 'diff text: a field refused to both sides is one line among the caveats'
        File = 'src\Bws.Cli\DiffText.cs'
        From = '            .Concat(diff.RefusedByBoth.Select(Refused))'
        To = '            .Concat(diff.RefusedByBoth.Take(0).Select(Refused))'
        Test = 'DiffOutputTests.A_field_refused_to_both_snapshots_is_one_line_naming_the_entries_and_nothing_else_differs'
    }
    @{
        Name = 'diff text: one entry refused a field on both sides is said in the singular'
        File = 'src\Bws.Cli\DiffText.cs'
        From = '        field.Entries.Count == 1'
        To = '        field.Entries.Count == 2'
        Test = 'DiffOutputTests.One_entry_refused_a_field_on_both_sides_is_said_in_the_singular'
    }
    @{
        Name = 'diff json: a field refused to both sides is written with its entries'
        File = 'src\Bws.Cli\DiffJson.cs'
        From = '                [.. diff.RefusedByBoth.Select(field => new RefusedDocument(field.Field, field.Entries))]),'
        To = '                [.. diff.RefusedByBoth.Take(0).Select(field => new RefusedDocument(field.Field, field.Entries))]),'
        Test = 'DiffOutputTests.A_field_refused_to_both_snapshots_is_written_last_with_its_entries'
    }
    @{
        Name = 'window diff: nothing differing is a sentence rather than an empty table'
        File = 'src\Bws.Gui\ViewModels\DiffSheet.cs'
        From = '            Same = diff.Reported ? string.Empty : Texts.Of("gui.diff.same")'
        To = '            Same = !diff.Reported ? string.Empty : Texts.Of("gui.diff.same")'
        Test = 'DiffSheetTests.A_snapshot_compared_with_the_same_machine_is_a_sentence_and_no_rows'
    }
    @{
        Name = 'window diff: a field is named by the heading the list puts over it'
        File = 'src\Bws.Gui\ViewModels\DiffLines.cs'
        From = '        Columns.Of(field) is { } column ? Texts.Of(column.LabelKey) : Printable.Of(field);'
        To = '        Columns.Of(field + "?") is { } column ? Texts.Of(column.LabelKey) : Printable.Of(field);'
        Test = 'DiffSheetTests.A_changed_field_is_one_row_named_by_the_heading_the_list_puts_over_it'
    }
    @{
        # PR #61 review, 2026-10-14: a field neither side read is named by the files, and two files decide it.
        Name = 'window diff: a field name two files wrote cannot reorder the text around it'
        File = 'src\Bws.Gui\ViewModels\DiffLines.cs'
        From = '        Columns.Of(field) is { } column ? Texts.Of(column.LabelKey) : Printable.Of(field);'
        To = '        Columns.Of(field) is { } column ? Texts.Of(column.LabelKey) : field;'
        Test = 'DiffSheetTests.A_field_name_two_files_wrote_cannot_reorder_the_text_around_it_either'
    }
    @{
        # Re-anchored 2026-10-15 (W2): the row now carries its kind for the filter.
        Name = 'window diff: a value from a foreign file cannot reorder the text around it'
        File = 'src\Bws.Gui\ViewModels\DiffLines.cs'
        From = '        new(Printable.Of(entry.ServiceName), Printable.Of(entry.DisplayName), what, before, after) { Kind = kind };'
        To = '        new(Printable.Of(entry.ServiceName), entry.DisplayName, what, before, after) { Kind = kind };'
        Test = 'DiffSheetTests.A_character_in_a_foreign_file_cannot_reorder_the_text_around_it'
    }
    @{
        Name = 'window diff: a field about what was running says so'
        File = 'src\Bws.Gui\ViewModels\DiffLines.cs'
        From = '        difference.Group == DifferenceGroup.RunningState'
        To = '        difference.Group != DifferenceGroup.RunningState'
        Test = 'DiffSheetTests.A_field_about_what_was_running_says_so_beside_its_name'
    }
    @{
        Name = 'window diff: a field refused to both sides is a caveat above the table'
        File = 'src\Bws.Gui\ViewModels\DiffCaveats.cs'
        From = '        return [.. lines.Where(line => line.Says).Select(line => line.Line()), .. diff.RefusedByBoth.Select(Refused)];'
        To = '        return [.. lines.Where(line => line.Says).Select(line => line.Line()), .. diff.RefusedByBoth.Take(0).Select(Refused)];'
        Test = 'DiffSheetTests.A_field_refused_to_both_sides_is_one_sentence_above_the_table_and_no_row'
    }
    @{
        Name = 'window snapshot: a file that is not a snapshot is never written over'
        File = 'src\Bws.Gui\ViewModels\Snapshotting.cs'
        From = '    private static DiskRefusal? Refused(string path) => SnapshotOnDisk.WhyNotWritten(path, force: true);'
        To = '    private static DiskRefusal? Refused(string path) => SnapshotOnDisk.WhyNotWritten(path + ".elsewhere", force: true);'
        Test = 'SnapshottingTests.A_file_that_is_not_a_snapshot_is_never_written_over_and_is_left_as_it_was'
    }
    @{
        Name = 'window snapshot: what the window writes is a snapshot the command line reads'
        File = 'src\Bws.Gui\ViewModels\Snapshotting.cs'
        From = '        AtomicFile.Write(path, text);'
        To = '        AtomicFile.Write(path, text + "x");'
        Test = 'SnapshottingTests.A_snapshot_taken_in_the_window_is_one_the_command_line_reads'
    }
    @{
        # PR #61 review, 2026-10-14: the target is asked about before the second the machine takes.
        Name = 'window snapshot: a target that will be refused costs no reading of the machine'
        File = 'src\Bws.Gui\ViewModels\Snapshotting.cs'
        From = '        if (Refused(path) is { } early)'
        To = '        if (Refused(path + ".elsewhere") is { } early)'
        Test = 'SnapshottingTests.A_target_that_will_be_refused_costs_no_reading_of_the_machine'
    }
    @{
        # PR #61 review, 2026-10-14: and again right before the write, which is the ask S-8 stands on.
        Name = 'window snapshot: a file that appears while the machine is read is never written over'
        File = 'src\Bws.Gui\ViewModels\Snapshotting.cs'
        From = '        if (Refused(path) is { } late)'
        To = '        if (Refused(path + ".elsewhere") is { } late)'
        Test = 'SnapshottingTests.A_file_that_appears_while_the_machine_is_read_is_still_never_written_over'
    }
    @{
        # PR #61 review, 2026-10-14: the reason quotes the path, so it is escaped like the path.
        Name = 'window snapshot words: an unreadable file''s reason cannot reorder the line'
        File = 'src\Bws.Gui\ViewModels\SnapshotWords.cs'
        From = '            _ => Texts.Of("gui.snapshot.fault.unreadable", path, Printable.Of(refusal.Detail))'
        To = '            _ => Texts.Of("gui.snapshot.fault.unreadable", path, refusal.Detail)'
        Test = 'SnapshottingTests.A_reason_a_file_could_not_be_read_cannot_reorder_the_line_either'
    }
    @{
        # Re-anchored 2026-10-15 (W2): the form before a snapshot hides the list too, asked as one question.
        Name = 'window snapshot: a comparison hides the list'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Snapshots.cs'
        From = '    public bool ListOnScreen => !_showingOverview && !Snapshotting.InTheMiddle && !History.InTheMiddle;'
        To = '    public bool ListOnScreen => !_showingOverview && !History.InTheMiddle;'
        Test = 'SnapshottingTests.Putting_the_comparison_away_answers_once_and_gives_the_list_back'
    }
    @{
        Name = 'window snapshot: Escape puts the comparison away before the query'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Snapshots.cs'
        # Re-anchored 2026-10-20: the search over the view's table goes first now (AS-a).
        From = '    internal bool PutAwayAView() => MiddleSearchEmptied() || Snapshotting.PutAway() || History.PutAway() || Chosen.Hide();'
        To = '    internal bool PutAwayAView() => MiddleSearchEmptied() || History.PutAway() || Chosen.Hide();'
        Test = 'SnapshotWindowGuards.Escape_puts_the_comparison_away_before_it_touches_the_query'
    }
    @{
        Name = 'window snapshot: a comparison takes the middle of the window'
        File = 'src\Bws.Gui\MainWindow.Overview.cs'
        From = '        Comparison.Visibility = comparing ? Visibility.Visible : Visibility.Collapsed;'
        To = '        Comparison.Visibility = Visibility.Collapsed;'
        Test = 'SnapshotWindowGuards.A_comparison_takes_the_middle_and_puts_the_controls_of_the_list_away'
    }
    @{
        Name = 'window snapshot: the way back gives the list back'
        File = 'src\Bws.Gui\SnapshotDoors.cs'
        From = '        comparison.Finished += (_, _) => model.Snapshotting.PutAway();'
        To = '        comparison.Finished += (_, _) => { };'
        Test = 'SnapshotWindowGuards.The_way_back_gives_the_list_and_its_controls_back'
    }
    @{
        Name = 'window snapshot: the button is off while the machine is read'
        File = 'src\Bws.Gui\ActionBar.xaml.cs'
        From = '        AuditButton.IsEnabled = !busy;'
        To = '        AuditButton.IsEnabled = true;'
        Test = 'SnapshotWindowGuards.The_button_is_off_while_the_machine_is_read_and_says_why_on_itself'
    }
    @{
        # BACKLOG 562, 2026-10-14. A dialog with no default folder starts wherever the process
        # stands, and Explorer starts programs in System32.
        Name = 'dialogs: every file dialog starts in Documents when Windows remembers no folder'
        File = 'src\Bws.Gui\FileDialogs.cs'
        From = '        dialog.DefaultDirectory = StartingFolder();'
        To = '        _ = StartingFolder();'
        Test = 'FileDialogGuards.Every_dialog_starts_in_Documents_unless_Windows_remembers_a_folder'
    }
    @{
        # The owner's decision was a default, not an initial folder - an initial folder would send
        # a person away from the folder Windows remembers on every press.
        Name = 'dialogs: the starting folder never overrides the folder Windows remembers'
        File = 'src\Bws.Gui\FileDialogs.cs'
        From = '        dialog.DefaultDirectory = StartingFolder();'
        To = '        dialog.DefaultDirectory = StartingFolder();
        dialog.InitialDirectory = StartingFolder();'
        Test = 'FileDialogGuards.Every_dialog_starts_in_Documents_unless_Windows_remembers_a_folder'
    }
    @{
        # A fourth dialog written out by hand would start in System32 again without a word.
        # Re-anchored 2026-10-15 (W2): the open dialog says what it is choosing for.
        Name = 'dialogs: no file dialog is built outside the one file'
        File = 'src\Bws.Gui\SnapshotDoors.cs'
        From = '        var dialog = FileDialogs.Open(".json", Texts.Of("gui.snapshot.kind"), Texts.Of("gui.snapshot.chooseForMachine"));'
        To = '        _ = new Microsoft.Win32.OpenFileDialog();
        var dialog = FileDialogs.Open(".json", Texts.Of("gui.snapshot.kind"), Texts.Of("gui.snapshot.chooseForMachine"));'
        Test = 'FileDialogGuards.No_file_dialog_is_built_outside_the_one_file'
    }
    # ---- W2, 2026-10-15: two snapshots, "Show", the comparison's export, the form with a note ----
    @{
        # W2-a: chosen the other way round, the same pair would turn every addition into a removal.
        Name = 'window two snapshots: the one taken first is the earlier side'
        File = 'src\Bws.Gui\ViewModels\Snapshotting.Files.cs'
        From = '        if (two.Metadata.TakenAt < one.Metadata.TakenAt)'
        To = '        if (two.Metadata.TakenAt > one.Metadata.TakenAt)'
        Test = 'TwoSnapshotsTests.The_one_taken_first_is_the_earlier_side_whichever_was_chosen_first'
    }
    @{
        Name = 'window two snapshots: the heading says two snapshots'
        File = 'src\Bws.Gui\ViewModels\DiffSheet.cs'
        From = '            Title = Texts.Of("gui.diff.files.title"),'
        To = '            Title = Texts.Of("gui.diff.title"),'
        Test = 'TwoSnapshotsTests.Two_snapshots_are_said_as_two_snapshots_and_never_as_a_snapshot_and_this_machine'
    }
    @{
        Name = 'window two snapshots: the caveats name two snapshots'
        File = 'src\Bws.Gui\ViewModels\DiffCaveats.cs'
        From = '        var files = sides == DiffSides.TwoSnapshots;'
        To = '        var files = sides != DiffSides.TwoSnapshots;'
        Test = 'TwoSnapshotsTests.Two_snapshots_are_said_as_two_snapshots_and_never_as_a_snapshot_and_this_machine'
    }
    @{
        Name = 'window two snapshots: an entry that came is said for two snapshots'
        File = 'src\Bws.Gui\ViewModels\DiffLines.cs'
        From = '        sides == DiffSides.TwoSnapshots ? Texts.Of("gui.diff.files.row.added") : Texts.Of("gui.diff.row.added");'
        To = '        sides != DiffSides.TwoSnapshots ? Texts.Of("gui.diff.files.row.added") : Texts.Of("gui.diff.row.added");'
        Test = 'TwoSnapshotsTests.Two_snapshots_are_said_as_two_snapshots_and_never_as_a_snapshot_and_this_machine'
    }
    @{
        Name = 'window two snapshots: the later side keeps its note'
        File = 'src\Bws.Gui\ViewModels\DiffSheet.cs'
        From = 'Described(later.Metadata)), Noted(later.Metadata))'
        To = 'Described(later.Metadata)), string.Empty)'
        Test = 'TwoSnapshotsTests.Both_notes_are_shown_and_both_sides_say_their_rights'
    }
    @{
        # Two files play no part in the machine, so reading it for them is a second for nothing.
        Name = 'window two snapshots: the machine is not read for two files'
        File = 'src\Bws.Gui\ViewModels\Snapshotting.cs'
        From = '            var (earlier, later, diff, trouble) = await Task.Run(() => FilesCompared(first, second)).ConfigureAwait(true);'
        To = '            _ = _catalog.ReadAll();
            var (earlier, later, diff, trouble) = await Task.Run(() => FilesCompared(first, second)).ConfigureAwait(true);'
        Test = 'TwoSnapshotsTests.Two_files_are_compared_without_reading_the_machine'
    }
    @{
        Name = 'window two snapshots: the headings over the values name the sides'
        File = 'src\Bws.Gui\DiffTable.xaml.cs'
        From = '            BeforeColumn.Header = sheet.BeforeHeading;'
        To = '            BeforeColumn.Header = sheet.AfterHeading;'
        Test = 'TwoSnapshotWindowGuards.Two_snapshots_head_their_columns_earlier_and_later'
    }
    @{
        Name = 'window two snapshots: the bar offers comparing two'
        File = 'src\Bws.Gui\ActionBar.xaml.cs'
        From = '"snapshotCompareTwo", () => CompareTwoRequest'
        To = '"snapshotCompare2", () => CompareTwoRequest'
        Test = 'SnapshotWindowGuards.The_bar_offers_taking_comparing_comparing_two_and_the_history_under_one_button'
    }
    @{
        # Two dialogs come one after the other, and "Open" alone said nothing about which was which.
        Name = 'dialogs: an open dialog says what it is choosing for'
        File = 'src\Bws.Gui\FileDialogs.cs'
        From = '            Title = title'
        To = '            Title = title.Length > 0 ? string.Empty : title'
        Test = 'FileDialogGuards.The_dialogs_keep_the_name_extension_and_filter_they_always_had'
    }
    @{
        # W2-b. Rows that stayed would make "Differences only" a label rather than a filter.
        # Re-anchored 2026-10-15 after the PR #69 review: the rows are replaced at once now.
        Name = 'window diff filter: a choice takes out the rows it does not show'
        File = 'src\Bws.Gui\ViewModels\DiffFilter.cs'
        # And 2026-10-20: the tick and the search narrow together (AS-a), and this breaks the tick alone.
        From = '        Rows.ShowOnly(_all.Where((line, i) => takes(line) && RowSearch.Finds(_said[i], _words)));'
        To = '        Rows.ShowOnly(_all.Where((line, i) => RowSearch.Finds(_said[i], _words)));'
        Test = 'DiffFilterTests.A_choice_narrows_the_rows_in_place_and_says_how_many_are_hidden'
    }
    @{
        Name = 'window show choices: a choice that shows nothing is not offered'
        File = 'src\Bws.Gui\ViewModels\RowChoices.cs'
        From = '        if (count > 0 && count < all.Count)'
        To = '        if (count >= 0 && count < all.Count)'
        Test = 'DiffFilterTests.Each_choice_carries_its_count_and_only_choices_that_narrow_anything_are_offered'
    }
    @{
        Name = 'window show choices: a choice that shows everything is not offered'
        File = 'src\Bws.Gui\ViewModels\RowChoices.cs'
        From = '        if (count > 0 && count < all.Count)'
        To = '        if (count > 0 && count <= all.Count)'
        Test = 'TwoSnapshotWindowGuards.The_menu_under_show_holds_every_choice_and_a_tick_narrows_the_table'
    }
    @{
        # Rule 8: a narrowed table that does not say so reads as the whole comparison.
        Name = 'window diff filter: the line says how many rows are hidden'
        File = 'src\Bws.Gui\ViewModels\DiffFilter.cs'
        # Re-anchored 2026-10-20: a search that finds nothing is asked about first now.
        From = '        : Rows.Count == _all.Count ? string.Empty'
        To = '        : Rows.Count != -1 ? string.Empty'
        Test = 'DiffFilterTests.A_choice_narrows_the_rows_in_place_and_says_how_many_are_hidden'
    }
    @{
        Name = 'window diff filter: the button names what is shown'
        File = 'src\Bws.Gui\ViewModels\DiffFilter.cs'
        From = '    public string Button => Texts.Of("gui.diff.show", _chosen.Name);'
        To = '    public string Button => Texts.Of("gui.diff.show", Choices[0].Name);'
        Test = 'DiffFilterTests.A_choice_narrows_the_rows_in_place_and_says_how_many_are_hidden'
    }
    @{
        Name = 'window show choices: the tick on what is shown cannot be taken off'
        File = 'src\Bws.Gui\ViewModels\RowChoices.cs'
        From = '                Raise(nameof(IsChosen));'
        To = '                Mark(chosen: false);'
        Test = 'DiffFilterTests.The_tick_on_what_is_shown_cannot_be_taken_off'
    }
    @{
        Name = 'window show row: each item of the menu is bound to its choice'
        File = 'src\Bws.Gui\ShowRow.xaml.cs'
        From = '                ShowMenu.Items.Add(new MenuItem { DataContext = choice, Style = style });'
        To = '                ShowMenu.Items.Add(new MenuItem { Style = style });'
        Test = 'TwoSnapshotWindowGuards.The_menu_under_show_holds_every_choice_and_a_tick_narrows_the_table'
    }
    @{
        # W2-c, rule 8 in a file: the caveats travel with the rows or the file claims more than it knows.
        Name = 'window diff export: the sentences go in before the table'
        File = 'src\Bws.Gui\ViewModels\DiffExporting.cs'
        From = '            Exporting.Line(text, [sentence], separator);'
        To = '            _ = sentence;'
        Test = 'DiffExportingTests.The_file_says_everything_said_above_the_table_before_the_table'
    }
    @{
        Name = 'window diff export: a narrowed table is said in the file'
        File = 'src\Bws.Gui\ViewModels\DiffExporting.cs'
        From = '        if (shown < sheet.Lines.Count)'
        To = '        if (shown < 0)'
        Test = 'DiffExportingTests.A_narrowed_table_is_said_in_the_file_and_only_its_rows_go_in'
    }
    @{
        # The same code the list export uses, and this entry names the comparison's own test.
        Name = 'window diff export: a value a spreadsheet would run is made inert'
        File = 'src\Bws.Gui\ViewModels\Exporting.cs'
        From = 'value.Length > 0 && Dangerous.Contains(value[0])'
        To = 'value.Length > 0 && Dangerous.Contains(value[0]) && false'
        Test = 'DiffExportingTests.A_value_from_a_snapshot_that_a_spreadsheet_would_run_arrives_inert'
    }
    @{
        # Re-anchored 2026-10-15 after the PR #69 review: the write went off the window's thread.
        Name = 'window diff export: the file carries the mark a spreadsheet needs'
        File = 'src\Bws.Gui\ViewModels\Snapshotting.Files.cs'
        From = '            await WrittenOffTheWindow(path, Exporting.Mark + text).ConfigureAwait(true);'
        To = '            await WrittenOffTheWindow(path, text).ConfigureAwait(true);'
        Test = 'DiffExportingTests.The_window_writes_it_with_the_mark_a_spreadsheet_needs_and_says_how_many_rows'
    }
    @{
        # PR #69 review, 2026-10-15: a notification per row is a notification the grid answers per row.
        Name = 'window shown rows: a choice tells the table once'
        File = 'src\Bws.Gui\ViewModels\RowChoices.cs'
        From = '        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));'
        To = '        _ = NotifyCollectionChangedAction.Reset;'
        Test = 'DiffFilterTests.A_choice_tells_the_table_once_rather_than_once_a_row'
    }
    @{
        # W2-d, backlog 561.
        Name = 'window snapshot form: the note typed goes into the snapshot'
        File = 'src\Bws.Gui\ViewModels\Snapshotting.cs'
        From = '            var note = Form?.Note;'
        To = '            var note = (string?)null;'
        Test = 'SnapshotFormTests.The_note_typed_in_the_form_is_kept_inside_the_snapshot_and_the_form_goes'
    }
    @{
        # MISSED on its first run, 2026-10-15, and rightly: it mutated a copy of this rule the form
        # carried, while the core settled the note again on the way into the file. The copy went, and
        # the entry points at the one rule both front ends write through - which had no guard at all.
        Name = 'window snapshot form: a note of spaces is no note'
        File = 'src\Bws.Core\Snapshots\Snapshot.cs'
        From = '            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),'
        To = '            Note = string.IsNullOrWhiteSpace(note) ? note : note.Trim(),'
        Test = 'SnapshotFormTests.A_note_of_spaces_is_no_note_at_all'
    }
    @{
        Name = 'window snapshot form: the form goes once the snapshot is written'
        File = 'src\Bws.Gui\ViewModels\Snapshotting.cs'
        From = '            // with the note still in it, so another folder is one more press rather than a retyping.
            Form = null;'
        To = '            // with the note still in it, so another folder is one more press rather than a retyping.'
        Test = 'SnapshotFormTests.The_note_typed_in_the_form_is_kept_inside_the_snapshot_and_the_form_goes'
    }
    @{
        Name = 'window snapshot form: a refused target keeps the form'
        File = 'src\Bws.Gui\ViewModels\Snapshotting.cs'
        From = '            // The same one answer told two ways as in CompareAsync - Written fills one of them, never both.
            if (snapshot is null)
            {'
        To = '            // The same one answer told two ways as in CompareAsync - Written fills one of them, never both.
            if (snapshot is null)
            {
                Form = null;'
        Test = 'SnapshotFormTests.A_refused_target_keeps_the_form_with_its_note_for_another_folder'
    }
    @{
        Name = 'window snapshot form: the save button is off while the machine is read'
        File = 'src\Bws.Gui\ViewModels\Snapshotting.cs'
        From = '                _form?.Reading(value);'
        To = '                _ = value;'
        Test = 'SnapshotFormTests.The_save_button_is_off_while_the_machine_is_read_for_the_snapshot'
    }
    @{
        Name = 'window snapshot form: Escape and the way back put the form away'
        File = 'src\Bws.Gui\ViewModels\Snapshotting.cs'
        From = '        if (Shown is null && Form is null)'
        To = '        if (Shown is null)'
        Test = 'TwoSnapshotWindowGuards.Escape_pressed_on_the_form_puts_it_away_and_leaves_the_query'
    }
    @{
        Name = 'window snapshot form: the form takes the middle of the window'
        File = 'src\Bws.Gui\MainWindow.Overview.cs'
        From = '        TakeForm.Visibility = preparing ? Visibility.Visible : Visibility.Collapsed;'
        To = '        TakeForm.Visibility = Visibility.Collapsed;'
        Test = 'TwoSnapshotWindowGuards.Take_snapshot_puts_the_form_in_the_middle_and_the_controls_of_the_list_away'
    }
    @{
        Name = 'window snapshot form: take snapshot opens the form'
        File = 'src\Bws.Gui\SnapshotDoors.cs'
        From = '        actions.TakeSnapshotRequest += (_, _) => model.Snapshotting.Prepare();'
        To = '        actions.TakeSnapshotRequest += (_, _) => { };'
        Test = 'TwoSnapshotWindowGuards.Take_snapshot_puts_the_form_in_the_middle_and_the_controls_of_the_list_away'
    }
    @{
        # BACKLOG 563, 2026-10-14. Every keyboard test called Act straight, so a press that never
        # reached it - or reached it as another key - was green everywhere.
        Name = 'keyboard: the key a person presses is the key the window acts on'
        File = 'src\Bws.Gui\MainWindow.Keyboard.cs'
        From = '            Wanted(e.Key, e.KeyboardDevice.Modifiers,'
        To = '            Wanted(Key.None, e.KeyboardDevice.Modifiers,'
        Test = 'PressedKeyGuards.Escape_pressed_in_the_box_clears_the_query_and_a_second_press_is_handed_back'
    }
    @{
        # The other half of the same handler: a press that did nothing goes back to the framework,
        # or the key silently stops working for whatever needed it next.
        Name = 'keyboard: a press that did nothing is handed back to whatever needs it'
        File = 'src\Bws.Gui\MainWindow.Keyboard.cs'
        From = '        e.Handled = Act('
        To = '        e.Handled = true | Act('
        Test = 'PressedKeyGuards.Escape_pressed_in_the_box_clears_the_query_and_a_second_press_is_handed_back'
    }
    @{
        # From the CodeRabbit review of PR 67: a handler that put the comparison away and emptied
        # the query on the way passed while the routed test started from an empty query.
        Name = 'keyboard: a press that puts the comparison away leaves the query alone'
        File = 'src\Bws.Gui\MainWindow.Keyboard.cs'
        From = '        await work.ConfigureAwait(true);'
        To = '        if (e.Key == Key.Escape) { _model.ClearQuery(); }
        await work.ConfigureAwait(true);'
        Test = 'SnapshotWindowGuards.Escape_pressed_on_the_window_puts_the_comparison_away_and_leaves_the_query'
    }
    @{
        # BACKLOG 568, 2026-10-14. Which page a press asks for, through a hand-over that opens
        # nothing. NO ENTRY BYPASSES PagesOpenedBy, deliberately: that mutant would hand three real
        # pages to the browser of whoever runs the registry.
        Name = 'link presses: GitHub asks for the repository'
        File = 'src\Bws.Gui\MainWindow.xaml.cs'
        From = 'OpenPage(ExternalLinks.Destination.Repository)'
        To = 'OpenPage(ExternalLinks.Destination.Support)'
        Test = 'LinkPressGuards.Each_press_asks_for_its_own_page_and_no_other'
    }
    @{
        Name = 'link presses: Donate asks for the support page'
        File = 'src\Bws.Gui\MainWindow.xaml.cs'
        From = 'OpenPage(ExternalLinks.Destination.Support)'
        To = 'OpenPage(ExternalLinks.Destination.Repository)'
        Test = 'LinkPressGuards.Each_press_asks_for_its_own_page_and_no_other'
    }
    @{
        Name = 'link presses: the help menu asks for the query language page'
        File = 'src\Bws.Gui\HelpMenu.cs'
        From = 'window.OpenPage(ExternalLinks.Destination.QueryLanguage)'
        To = 'window.OpenPage(ExternalLinks.Destination.Support)'
        Test = 'LinkPressGuards.Each_press_asks_for_its_own_page_and_no_other'
    }
    @{
        Name = 'link presses: a page that did not open says why'
        File = 'src\Bws.Gui\MainWindow.Help.cs'
        From = '            _model.Says.CouldNotDo(trouble);'
        To = '            _ = trouble;'
        Test = 'LinkPressGuards.A_page_that_did_not_open_says_why_in_the_status_line'
    }
    # THE WINDOWS INSTALLER, 2026-10-14 (docs/PROJEKT-MSI-20261014.md). Every line below changed would
    # still build a working installer, so each one needs a guard that can fail - these are that proof.
    # The installer's shape is asked of what build-msi.ps1 RENDERS, so the template mutants reach the
    # guards through the script and no WiX is needed.
    @{
        # A new UpgradeCode leaves every old install beside the new one, on every machine, for good.
        Name = 'installer: the upgrade code is the product for good'
        File = 'packaging\build-msi.ps1'
        From = "`$UpgradeCode = 'FD038501-7A2F-49C9-A587-E8DF3EFB9718'"
        To = "`$UpgradeCode = 'FD038501-7A2F-49C9-A587-E8DF3EFB9719'"
        Test = 'InstallerGuards.The_installer_is_the_product_for_good_and_ends_nothing_that_runs'
    }
    @{
        # With the Restart Manager on, an upgrade ends the window in the middle of a plan.
        Name = 'installer: nothing that runs is closed'
        File = 'packaging\msi\BetterWindowsServices.wxs'
        From = '<Property Id="MSIRESTARTMANAGERCONTROL" Value="Disable" />'
        To = '<Property Id="MSIRESTARTMANAGERCONTROL" Value="DisableShutdown" />'
        Test = 'InstallerGuards.The_installer_is_the_product_for_good_and_ends_nothing_that_runs'
    }
    @{
        # afterInstallExecute keeps the old files of a rebuild under the same number and reports success.
        Name = 'installer: the old product goes first'
        File = 'packaging\msi\BetterWindowsServices.wxs'
        From = 'Schedule="afterInstallInitialize"'
        To = 'Schedule="afterInstallExecute"'
        Test = 'InstallerGuards.The_installer_is_the_product_for_good_and_ends_nothing_that_runs'
    }
    @{
        Name = 'installer: it installs for the machine'
        File = 'packaging\msi\BetterWindowsServices.wxs'
        From = 'Scope="perMachine">'
        To = 'Scope="perUser">'
        Test = 'InstallerGuards.The_installer_is_the_product_for_good_and_ends_nothing_that_runs'
    }
    @{
        # SetProperty is a custom action under another name - the shape a template grows without anybody
        # writing the word CustomAction.
        Name = 'installer: it runs no action of its own'
        File = 'packaging\msi\BetterWindowsServices.wxs'
        From = '<MediaTemplate EmbedCab="yes" />'
        To = '<MediaTemplate EmbedCab="yes" /><SetProperty Id="BWSPROBE" Value="1" After="CostFinalize" Sequence="execute" />'
        Test = 'InstallerGuards.The_installer_carries_nothing_but_windows_installer_tables'
    }
    @{
        Name = 'installer: the PATH entry comes off at uninstall'
        File = 'packaging\msi\BetterWindowsServices.wxs'
        From = 'Part="last" Action="set" System="yes" Permanent="no" />'
        To = 'Part="last" Action="set" System="yes" Permanent="yes" />'
        Test = 'InstallerGuards.The_installer_puts_one_folder_on_the_path_and_the_window_in_every_start_menu'
    }
    @{
        Name = 'installer: the Start menu entry starts the window'
        File = 'packaging\msi\BetterWindowsServices.wxs'
        From = 'Target="[INSTALLFOLDER]{{WINDOW_EXE}}"'
        To = 'Target="[INSTALLFOLDER]bws.exe"'
        Test = 'InstallerGuards.The_installer_puts_one_folder_on_the_path_and_the_window_in_every_start_menu'
    }
    @{
        # A candidate's installer takes the release's own three numbers and blocks the release.
        Name = 'installer: a release candidate gets none'
        File = 'packaging\build-msi.ps1'
        From = "    if (`$releaseTag.Contains('-')) {"
        To = "    if (`$false) {"
        Test = 'InstallerGuards.A_tag_the_installer_cannot_carry_is_refused_rather_than_cut'
    }
    @{
        Name = 'installer: a version it cannot hold is refused rather than cut'
        File = 'packaging\build-msi.ps1'
        From = "if (`$major -gt 255 -or `$minor -gt 255 -or `$patch -gt 65535) {"
        To = "if (`$major -gt 255 -or `$minor -gt 255) {"
        Test = 'InstallerGuards.A_tag_the_installer_cannot_carry_is_refused_rather_than_cut'
    }
    @{
        Name = 'installer signing: the installer is held to the pin'
        File = 'packaging\sign-release.ps1'
        From = 'Assert-SignedByPin $installerPath $INSTALLER $pin.certificate_sha256'
        To = 'Write-Host $installerPath'
        Test = 'InstallerGuards.The_signing_asks_first_builds_from_the_signed_packages_and_holds_the_installer_to_the_pin'
    }
    @{
        Name = 'installer signing: one rule for which release gets an installer'
        File = 'packaging\sign-release.ps1'
        From = "return [bool]`$INSTALLER -and -not `$releaseTag.Contains('-')"
        To = "return [bool]`$INSTALLER"
        Test = 'InstallerGuards.Every_phase_asks_the_same_question_of_a_candidate_and_of_an_older_tree'
    }
    @{
        Name = 'installer phase D: the published page is asked for the installer'
        File = '.github\workflows\verify-release.yml'
        From = '$expected += @($installer, "$installer.spdx.json", "$installer.sigstore.json")'
        To = '$expected += @()'
        Test = 'InstallerGuards.Every_phase_asks_the_same_question_of_a_candidate_and_of_an_older_tree'
    }
    @{
        # Without it a run started by hand reads today's README and register and holds an old release
        # to them - "the installer is missing" for every release made before there was one.
        Name = 'installer phase D: an old release is held to its own tree'
        File = '.github\workflows\verify-release.yml'
        From = 'ref: refs/tags/${{ env.TAG }}'
        To = 'ref: ${{ github.ref }}'
        Test = 'InstallerGuards.Every_phase_asks_the_same_question_of_a_candidate_and_of_an_older_tree'
    }
    @{
        Name = 'installer names: the README checks the installer against the sums'
        File = 'README.md'
        From = "(Get-FileHash BetterWindowsServices-win-x64.msi -Algorithm SHA256)"
        To = "(Get-FileHash BetterWindowsServices-win-x64.zip -Algorithm SHA256)"
        Test = 'InstallerGuards.Every_place_a_release_names_the_installer_names_it_the_same_way'
    }
    @{
        Name = 'installer CI: the installer is asked of a clean runner'
        File = '.github\workflows\installer.yml'
        From = 'run: ./packaging/test-installer.ps1 -Window'
        To = 'run: echo skipped ./packaging/test-installer.ps1 -Window'
        Test = 'InstallerGuards.The_installer_is_installed_on_a_clean_runner'
    }
    @{
        # Review of #68: a candidate publishes no installer, so the README's commands naming it would
        # turn phase D red on every candidate.
        Name = 'installer phase D: a candidate leaves out only the README commands naming the installer'
        File = '.github\workflows\verify-release.yml'
        From = '$commands = @($commands | Where-Object { -not $_.Contains($installer) })'
        To = '$commands = @($commands)'
        Test = 'InstallerGuards.Every_phase_asks_the_same_question_of_a_candidate_and_of_an_older_tree'
    }
    @{
        # Review of #68: a second literal of the name would attest a renamed installer under the old one.
        Name = 'installer phase C: the installer is attested under the one registered name'
        File = '.github\workflows\attest-signed.yml'
        From = 'subject-path: assets/${{ env.INSTALLER }}'
        To = 'subject-path: assets/BetterWindowsServices-win-x64.msi'
        Test = 'InstallerGuards.Every_place_a_release_names_the_installer_names_it_the_same_way'
    }
    @{
        # J1, 2026-10-15. Event 104 is written into System for clearing ANY channel - five on the owner's
        # machine in one afternoon - so only the one naming this log may say it was emptied.
        Name = 'history: clearing another log is not clearing this one'
        File = 'src\Bws.Core\History\ChangeEvents.cs'
        From = '&& string.Equals(rendered.ClearedChannel, channel, StringComparison.OrdinalIgnoreCase)'
        To = '&& rendered.ClearedChannel is not null'
        Test = 'ChangeEventsTests.Clearing_the_log_being_read_is_a_clearing_and_clearing_another_one_is_not'
    }
    @{
        # J1-b: the field 7045 calls ServiceName holds the display name, measured on the throwaway machine.
        Name = 'history: an installation carries no service name'
        File = 'src\Bws.Core\History\ChangeEvents.cs'
        From = 'ServiceName = null,'
        To = 'ServiceName = rendered.Data("ServiceName"),'
        Test = 'ChangeEventsTests.An_installation_carries_the_display_name_and_never_a_service_name'
    }
    @{
        # J1-b: one entry's history is by identity, never by display name - rule 3.
        Name = 'history: one entry never gets an installation by its display name'
        File = 'src\Bws.Core\History\ChangeHistory.cs'
        From = 'string.Equals(change.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase)'
        To = 'string.Equals(change.ServiceName ?? change.DisplayName, serviceName, StringComparison.OrdinalIgnoreCase)'
        Test = 'ChangeHistoryTests.An_installation_is_never_given_to_an_entry_even_under_its_own_display_name'
    }
    @{
        # F25: the last chunk that does not fit is the limit - the overwriting Security log was header plus limit.
        Name = 'history: a log with less than a chunk of room is at its limit'
        File = 'src\Bws.Core\History\ChangeHistory.cs'
        From = 'FileSize + Chunk > MaximumSize'
        To = 'FileSize > MaximumSize'
        Test = 'ChangeHistoryTests.A_log_with_less_than_one_chunk_of_room_is_already_at_its_limit'
    }
    @{
        Name = 'history: a full log is not called overwritten'
        File = 'src\Bws.Core\History\ChangeHistory.cs'
        From = 'Overwritten = !facts.Full && facts.AtSizeLimit,'
        To = 'Overwritten = facts.AtSizeLimit,'
        Test = 'ChangeHistoryTests.A_full_log_is_not_called_overwritten'
    }
    @{
        # J1-c: naming an account of another domain would make Windows ask a domain controller.
        Name = 'history: an account of another domain gets no name'
        File = 'src\Bws.Core\History\AccountNames.cs'
        From = 'return domain is null || (localDomain is not null && domain == localDomain);'
        To = 'return domain is null || localDomain is not null;'
        Test = 'ChangeHistoryTests.Only_accounts_this_machine_can_name_on_its_own_are_named'
    }
    @{
        # J1-c: outside the NT authority sit the accounts of Microsoft Entra ID (S-1-12).
        Name = 'history: only the NT authority is named'
        File = 'src\Bws.Core\History\AccountNames.cs'
        From = 'if (!sid.Value.StartsWith("S-1-5-", StringComparison.Ordinal))'
        To = 'if (sid.Value.Length < 0)'
        Test = 'ChangeHistoryTests.Only_accounts_this_machine_can_name_on_its_own_are_named'
    }
    @{
        Name = 'history: a SID not shaped like one never reaches the constructor'
        File = 'src\Bws.Core\History\AccountNames.cs'
        From = '.Where(text => text is not null && SidShape().IsMatch(text))'
        To = '.Where(text => text is not null)'
        Test = 'ChangeHistoryTests.A_sid_that_is_not_shaped_like_one_gets_no_name_and_throws_nothing'
    }
    @{
        # Caught only on a machine whose time zone is not UTC - the owner's is +02:00. In UTC moving a
        # time into the machine's zone changes nothing, and the first two runs on GitHub came back MISSED
        # for that reason, so since 2026-10-16 mutation.yml sets each part's machine to the owner's zone
        # (a test cannot choose its own zone on Windows, measured that day). Run by hand in UTC, this
        # entry is MISSED and says nothing about the test. The first version of this entry took
        # AdjustToUniversal away and came back MISSED: the trailing Z already keeps the offset at zero,
        # so it removed nothing - the comment it stood on was wrong, and is corrected.
        Name = 'history: the time is never moved to this machine zone'
        File = 'src\Bws.Core\History\ChangeEvents.cs'
        From = '? at'
        To = '? at.ToLocalTime()'
        Test = 'ChangeEventsTests.The_time_is_the_instant_windows_wrote_in_utc_to_the_full_precision'
    }
    @{
        Name = 'history text: a log that lost something never says it lost nothing'
        File = 'src\Bws.Cli\HistoryText.cs'
        From = 'return log.Overwritten || log.Full'
        To = 'return log.Full'
        Test = 'HistoryOutputTests.A_log_that_lost_nothing_says_so_and_one_that_did_does_not'
    }
    @{
        # A misspelt name must not read as "nobody touched it".
        Name = 'history: a name nothing answers to is code 2'
        File = 'src\Bws.Cli\HistoryCommand.cs'
        From = 'if (name is not null && shown.Count == 0'
        To = 'if (name is not null && shown.Count < 0'
        Test = 'HistoryOutputTests.A_name_nothing_answers_to_is_a_mistake_and_the_manager_is_asked_only_then'
    }
    @{
        # J1-d: the words Windows wrote never stand where a value of this tool would be compared.
        Name = 'history json: the words Windows wrote stay under windowsWords'
        File = 'src\Bws.Cli\HistoryJson.cs'
        From = 'WordsDocument WindowsWords);'
        To = 'WordsDocument StartType);'
        Test = 'HistoryOutputTests.The_words_windows_wrote_are_never_written_where_a_value_of_this_tool_would_be'
    }
    @{
        # Runs against the live System log - the integration suite, read only.
        Name = 'history reader: changes come newest first'
        File = 'src\Bws.Core\History\WindowsChangeHistoryReader.cs'
        From = 'var direction = newestFirst ? EVT_QUERY_FLAGS.EvtQueryReverseDirection : EVT_QUERY_FLAGS.EvtQueryForwardDirection;'
        To = 'var direction = newestFirst ? EVT_QUERY_FLAGS.EvtQueryForwardDirection : EVT_QUERY_FLAGS.EvtQueryForwardDirection;'
        Test = 'HistoryContractTests.Changes_come_newest_first_and_a_start_type_change_always_names_its_entry'
    }
    @{
        # Review of #70: what is parsed has a bound, one record in a 64 KB chunk. Zero means no limit.
        Name = 'history: an event bigger than one event can be is refused'
        File = 'src\Bws.Core\History\ChangeEvents.cs'
        From = 'MaxCharactersInDocument = LargestRendering / sizeof(char)'
        To = 'MaxCharactersInDocument = 0'
        Test = 'ChangeEventsTests.An_event_bigger_than_one_event_can_be_is_refused_rather_than_read'
    }
    @{
        # The weekly search lists properties by their trait. One left without it is never searched,
        # and the Sunday run is green and smaller without a word.
        Name = 'search: a property left out of the weekly list is named'
        File = 'tests\Bws.Core.Tests\ParserPropertyTests.cs'
        From = '[Trait("search", "weekly")]' + [char]10 + '    public void Reading_a_snapshot_never_fails_in_a_way_it_did_not_plan_for()'
        To = 'public void Reading_a_snapshot_never_fails_in_a_way_it_did_not_plan_for()'
        Test = 'SearchGuards.Every_property_through_the_door_is_in_the_weekly_search'
    }
    @{
        # The runner filters on search=weekly exactly. A capital letter leaves a property out.
        Name = 'search: the weekly trait has one spelling'
        File = 'tests\Bws.Core.Tests\DiffPropertyTests.cs'
        From = '[Trait("search", "weekly")]' + [char]10 + '    public void Turning_the_comparison_round_swaps_what_it_found()'
        To = '[Trait("search", "Weekly")]' + [char]10 + '    public void Turning_the_comparison_round_swaps_what_it_found()'
        Test = 'SearchGuards.The_trait_is_spelled_the_one_way_the_runner_lists'
    }
    @{
        # A property sampling CsCheck past the door is searched and can never say how much.
        Name = 'search: only the door samples random input'
        File = 'tests\Bws.Core.Tests\ParserPropertyTests.cs'
        From = 'var entries = Specimens.All;'
        To = 'var entries = Specimens.All; Awkward.Sample(_ => true, iter: 1);'
        Test = 'SearchGuards.Only_the_door_samples_random_input'
    }
    @{
        # A trait on a test that samples nothing would be a red Sunday about a label.
        Name = 'search: a test that searches nothing is not in the weekly list'
        File = 'tests\Bws.Core.Tests\QueryPropertyTests.cs'
        From = '[Fact]' + [char]10 + '    public void Repeating_a_field_never_narrows_the_answer()'
        To = '[Fact]' + [char]10 + '    [Trait("search", "weekly")]' + [char]10 + '    public void Repeating_a_field_never_narrows_the_answer()'
        Test = 'SearchGuards.Nothing_is_in_the_weekly_search_that_does_not_search'
    }
    @{
        # An exception naming a test that is gone reads as an argument still being made.
        Name = 'search: an exception that names nothing is named'
        File = 'tests\Bws.Architecture.Tests\SearchGuards.cs'
        From = '["SearchCanaryTests.A_property_that_fails_is_red_and_says_how_to_reproduce_it"] ='
        To = '["SearchCanaryTests.A_property_that_is_gone"] ='
        Test = 'SearchGuards.The_exceptions_still_name_tests_that_use_CsCheck'
    }
    @{
        # Every assertion of the guard passes on zero methods. This is the canary for that.
        Name = 'search: a guard that reads no test is refused'
        File = 'tests\Bws.Architecture.Tests\SearchGuards.cs'
        From = 'Sources.Testing()' + [char]10 + '            .SelectMany(Read)'
        To = 'Sources.Testing().Take(0)' + [char]10 + '            .SelectMany(Read)'
        Test = 'SearchGuards.The_reading_finds_the_properties_it_exists_for'
    }
    @{
        # Review of PR #73: CsCheck.Gen... written out in full, with no using directive, sampled past
        # the door unseen while only the directive counted.
        Name = 'search: a sample written with the full name is still seen'
        File = 'tests\Bws.Architecture.Tests\SearchGuards.cs'
        From = '.Any(name => name.Identifier.Text == "CsCheck");'
        To = '.Any(name => name.Identifier.Text == "CsCheck" && name.Parent is UsingDirectiveSyntax);'
        Test = 'SearchGuards.The_reading_sees_a_sample_however_CsCheck_is_named'
    }
    @{
        # The weekly run tells a property that searched from one that did not by this count. A door
        # that stopped counting turns every Sunday NOT SEARCHED, red and silent about why.
        Name = 'search: the door counts every call'
        File = 'tests\Searching.cs'
        From = 'System.Threading.Interlocked.Increment(ref calls);'
        To = '_ = calls;'
        Test = 'SearchCanaryTests.A_property_that_fails_is_red_and_says_how_to_reproduce_it'
    }
    @{
        # The fault this property found on its first run (backlog 588), put back. The example test
        # beside the fix holds one path, and this entry holds the property to finding it again on
        # whatever line the generator writes.
        Name = 'search: a chip is carried after any line somebody typed'
        File = 'src\Bws.Core\Querying\QueryMembers.cs'
        From = '(backslashes % 2 == 1 ? "\\ " : " ")'
        To = '(backslashes < 0 ? "\\ " : " ")'
        Test = 'QueryShapePropertyTests.A_chip_turned_on_is_carried_and_turned_off_is_not_whatever_was_typed_before'
    }
    @{
        # A size the reader cannot read has to come back as a reason. Parsing it instead throws on
        # 1.5MB, and the random properties before the shaped generator never reached this file
        # (0 per cent of it on 2026-10-16), so only the shaped one can be the guard here.
        Name = 'search: a size that does not read is refused rather than thrown'
        File = 'src\Bws.Core\Querying\QuerySizes.cs'
        From = 'if (!long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity))'
        To = 'var quantity = long.Parse(number, NumberStyles.None, CultureInfo.InvariantCulture);' + [char]10 + '            if (quantity < 0)'
        Test = 'QueryShapePropertyTests.Shaped_text_is_read_or_refused_in_both_readings_and_never_both'
    }
    @{
        # A stopped entry has no process and so no memory. A comparison that reads the missing
        # value instead of saying "no match" throws on the first stopped entry it judges, and only
        # a query that reads - memory:>5MB, which the old alphabet never spelled - gets that far.
        Name = 'search: an entry with no memory is judged rather than thrown over'
        File = 'src\Bws.Core\Querying\QueryValues.cs'
        From = 'var value = field.SizeOf!(entry);'
        To = 'var value = (long?)field.SizeOf!(entry)!.Value;'
        Test = 'QueryShapePropertyTests.Shaped_text_that_reads_judges_every_entry_without_failing'
    }
    @{
        # A value row replaces the value under the caret and nothing past the end of the line. One
        # character too many reaches outside the text whenever the value ends the line.
        Name = 'search: a value row replaces only text that is there'
        File = 'src\Bws.Core\Querying\QueryCompletions.cs'
        From = 'replaces: valueStart..valueEnd,'
        To = 'replaces: valueStart..(valueEnd + 1),'
        Test = 'QueryShapePropertyTests.Asking_at_any_caret_in_shaped_text_never_throws_and_every_row_points_into_the_text'
    }
    @{
        # Written in full is hidden while typing, so accepting a row does not reopen the list with
        # that row alone. Anchored on the comment beside it, because the condition stands twice.
        Name = 'search: a value written in full is not offered again while typing'
        File = 'src\Bws.Core\Querying\QueryCompletions.cs'
        From = 'if (!asked && spelled.Length == prefix.Length)' + [char]10 + '                {' + [char]10 + '                    // Already there in full'
        To = 'if (!asked && spelled.Length == prefix.Length && false)' + [char]10 + '                {' + [char]10 + '                    // Already there in full'
        Test = 'QueryShapePropertyTests.Nothing_offers_itself_again_in_shaped_text_once_it_has_been_written'
    }
    @{
        # PowerShell reads a single quote mark inside single quotes as the end of the word unless it is
        # doubled. A name holding one and a dollar - the only names that go in single quotes - would
        # reach this tool cut short, and the plan would hand out a line that names another entry.
        Name = 'search: a quote mark in a name is doubled inside single quotes'
        File = 'src\Bws.Core\Planning\EquivalentCommand.cs'
        From = 'if (SingleQuoteMarks.Contains(character))'
        To = 'if (SingleQuoteMarks.Contains(character) && false)'
        Test = 'EquivalentCommandPropertyTests.Any_name_typed_the_way_the_plan_writes_it_reaches_this_tool_through_PowerShell_as_the_same_ask'
    }
    @{
        # The double quoted form is the one cmd reads too, and the Windows parser is the oracle for it.
        # A quote left open runs to the end of the line, so the switches become part of the name.
        Name = 'search: a name in double quotes has its closing quote'
        File = 'src\Bws.Core\Planning\EquivalentCommand.cs'
        From = 'return $"\"{serviceName}\"";'
        To = 'return $"\"{serviceName}";'
        Test = 'EquivalentCommandPropertyTests.Any_name_the_plan_writes_for_cmd_too_reaches_this_tool_through_the_Windows_parser_as_the_same_ask'
    }
    @{
        # Program.cs builds the plan on the promise that a start type is never missing where the verb
        # needs one, because a refusal turned it back. Without the refusal a word that names no start
        # type walks through in silence.
        Name = 'search: a start type nobody has is refused before anything runs'
        File = 'src\Bws.Cli\Refusals.cs'
        From = 'if (setting is null)'
        To = 'if (setting is null && false)'
        Test = 'CommandLinePropertyTests.Any_words_end_in_an_answer_that_says_something_or_a_command_whole_enough_to_run'
    }
    @{
        # Everything before the first character outside ASCII is copied as it is, so moving that
        # boundary past the Latin letters prints them as themselves - the bytes a console on another
        # code page turns into a file no UTF-8 reader accepts.
        Name = 'search: every document leaves as ASCII'
        File = 'src\Bws.Cli\AsciiJson.cs'
        From = "var first = document.AsSpan().IndexOfAnyExceptInRange('\0', Last);"
        To = "var first = document.AsSpan().IndexOfAnyExceptInRange('\0', (char)0x017F);"
        Test = 'AsciiJsonPropertyTests.Any_text_an_entry_carries_leaves_as_ascii_and_reads_back_as_the_machine_said_it'
    }
    @{
        # The property's verbs are a copy, because the tool reads them in a switch nothing can list. A
        # verb dropped from the copy is a kind of command nobody draws, and the guard beside it says so.
        Name = 'search: the words a property draws reach every kind of command'
        File = 'tests\Bws.Cli.Tests\CommandLinePropertyTests.cs'
        From = '"history", "license", "snapshot"]'
        To = '"history", "snapshot"]'
        Test = 'CommandLinePropertyTests.The_words_drawn_above_reach_every_kind_of_command'
    }
    @{
        # Review of #75: a pipe left bare is syntax to both shells, and the models used to read it as
        # part of a word. A name of letters and one pipe would go out bare, and the line would hand
        # the rest of the name to another command.
        Name = 'search: a shell operator in a name never goes bare'
        File = 'src\Bws.Core\Planning\EquivalentCommand.cs'
        From = "character is '.' or '_' or '-')"
        To = "character is '.' or '_' or '-' or '|')"
        Test = 'EquivalentCommandPropertyTests.Any_name_typed_the_way_the_plan_writes_it_reaches_this_tool_through_PowerShell_as_the_same_ask'
    }
    @{
        # Backlog 591, step 3b of the fuzzing project: a layout file naming a key twice threw
        # ArgumentException out of the reader and kept the window from opening. Duplicates allowed
        # again is the fault back, and the property damaging a real file found it by a piece written twice.
        Name = 'search: a layout file naming a key twice is refused while reading'
        File = 'src\Bws.Gui\ViewModels\ColumnLayout.cs'
        From = 'new() { AllowDuplicateProperties = false };'
        To = 'new() { AllowDuplicateProperties = true };'
        Test = 'LayoutFilePropertyTests.A_damaged_layout_file_is_read_or_named_and_what_is_read_is_whole'
    }
    @{
        # The same fault as a person meets it, and the entry that measures what the changelog says of it:
        # a real window over such a file. With the repeat allowed again the constructor throws, and the
        # file is never moved aside, so every start after that one fails the same way.
        Name = 'layout: a window whose file names a key twice still opens'
        File = 'src\Bws.Gui\ViewModels\ColumnLayout.cs'
        From = 'new() { AllowDuplicateProperties = false };'
        To = 'new() { AllowDuplicateProperties = true };'
        Test = 'LayoutFilePropertyTests.A_window_whose_layout_file_names_a_key_twice_opens_and_moves_the_file_aside'
    }
    @{
        # The plan promises the grid every column exactly once. A file listing one column twice - which
        # the hand-shaped layouts write often - would hand the grid that column twice.
        Name = 'search: a column a file lists twice reaches the grid once'
        File = 'src\Bws.Gui\ViewModels\ColumnPlan.cs'
        From = 'if (Columns.Of(column.Id) is null || !seen.Add(column.Id))'
        To = 'if (Columns.Of(column.Id) is null)'
        Test = 'LayoutFilePropertyTests.A_layout_file_written_by_hand_is_read_or_named_and_what_is_read_is_whole'
    }
    @{
        # The history refuses an event as XML and in no other way. A number read with Parse throws a
        # FormatException on a damaged event identifier, and nothing in bws history plans for that.
        Name = 'search: a damaged event is refused only as xml'
        File = 'src\Bws.Core\History\ChangeEvents.cs'
        From = 'int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;'
        To = 'int.Parse(text!, NumberStyles.None, CultureInfo.InvariantCulture);'
        Test = 'ChangeEventPropertyTests.A_damaged_event_is_read_or_refused_as_xml_and_never_otherwise'
    }
    @{
        # An empty field is no value, so a driver installed with no account says none rather than
        # an empty string beside a null - and the events written from any values draw empty ones often.
        Name = 'search: an empty field in an event is no value'
        File = 'src\Bws.Core\History\ChangeEvents.cs'
        From = 'Values.TryGetValue(name, out var value) && value.Length > 0 ? value : null;'
        To = 'Values.TryGetValue(name, out var value) ? value : null;'
        Test = 'ChangeEventPropertyTests.An_event_written_from_any_values_gives_those_values_back'
    }
    @{
        # A query that does not fit is left behind AND said. Without the flag the new window opens with
        # an empty box and no sentence, which is the hand-over quietly showing less than it was given.
        Name = 'search: a query left behind to make room is said'
        File = 'src\Bws.Gui\ViewModels\HandOver.cs'
        From = 'QueryLeftBehind = carried.QueryLeftBehind || carried.Query.Length > 0 };'
        To = 'QueryLeftBehind = carried.QueryLeftBehind };'
        Test = 'HandOverPropertyTests.What_a_window_hands_over_comes_back_whole_or_says_what_was_left'
    }
    @{
        # A damaged argument is refused whole and quietly. Decoded without asking whether it is base64
        # first, it throws out of the reader in a process with administrator rights.
        Name = 'search: a damaged hand-over is refused rather than thrown over'
        File = 'src\Bws.Gui\ViewModels\HandOver.cs'
        From = '        if (text.Length is 0 or > LongestArgument || !Base64Url.IsValid(text))'
        To = '        if (text.Length is 0 or > LongestArgument)'
        Test = 'HandOverPropertyTests.A_damaged_hand_over_is_taken_or_refused_whole_and_taken_only_in_a_shape_the_window_writes'
    }
    @{
        # A carriage return inside a value has to be quoted, or the reader takes it for the end of a
        # line. The export property reads every file back with a reader that refuses a bare one.
        Name = 'search: a value holding a carriage return is quoted'
        File = 'src\Bws.Gui\ViewModels\Exporting.cs'
        From = "            && !value.Contains('\r', StringComparison.Ordinal))"
        To = "            )"
        Test = 'CsvPropertyTests.Every_value_exported_reads_back_as_itself_and_none_starts_a_formula'
    }
    @{
        # The converter throws ArgumentException for a width it cannot hold, Infinity and NaN among
        # them. Not caught, a hand-edited width takes the window down while the columns are built.
        Name = 'search: a width the converter cannot hold is left to the theme'
        File = 'src\Bws.Gui\ListColumns.cs'
        From = '        catch (ArgumentException)'
        To = '        catch (ArgumentNullException)'
        Test = 'KeptWidthPropertyTests.A_kept_width_of_any_text_is_drawable_or_left_to_the_theme_and_named'
    }
    @{
        # Review of #76: the property took the refused list from the product as its oracle, so a width
        # replaced by the theme's WITHOUT the column being named passed. The oracle is now WPF's own
        # converter and the drawable bound, and a fallback said to nobody is the silence rule 8 forbids.
        Name = 'search: a width refused is named rather than replaced in silence'
        File = 'src\Bws.Gui\ListColumns.cs'
        From = '            refused.Add(column.Id);'
        To = '            refused.AddRange([]);'
        Test = 'KeptWidthPropertyTests.A_kept_width_of_any_text_is_drawable_or_left_to_the_theme_and_named'
    }
    @{
        # The other half the same review found: a good width ignored for the theme's and named as if it
        # were bad would also have passed, because a named column was all the old oracle asked about.
        Name = 'search: a width that reads is the one the column gets'
        File = 'src\Bws.Gui\ListColumns.cs'
        From = '            if (Parsed(kept) is { } saved)'
        To = '            if (Parsed(kept) is { } saved && false)'
        Test = 'KeptWidthPropertyTests.A_kept_width_of_any_text_is_drawable_or_left_to_the_theme_and_named'
    }
    @{
        # Fuzzing finding M1, 2026-10-17: over a cycle through the target the manager's answer reaches
        # back round to it, and the target was a member of its own cascade - stopped twice, and named
        # in the cascade warning among the things that come down with it.
        Name = 'search: a target the manager names among its own dependants is not in its cascade'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = '                && !target.ServiceName.Equals(entry.ServiceName, StringComparison.OrdinalIgnoreCase))'
        To = '                && true)'
        Test = 'PlanGraphPropertyTests.A_plan_over_any_dependency_graph_does_each_thing_once_and_nothing_before_what_needs_it'
    }
    @{
        # The same mutant against the example written out from what the search found.
        Name = 'plan: a target the manager names among its own dependants is stopped once'
        File = 'src\Bws.Core\Planning\PlanBuilder.cs'
        From = '                && !target.ServiceName.Equals(entry.ServiceName, StringComparison.OrdinalIgnoreCase))'
        To = '                && true)'
        Test = 'DependencyCycleTests.A_target_the_manager_names_among_its_own_dependants_is_stopped_once_and_started_once'
    }
    @{
        # Fuzzing finding C: every member of a cycle names itself in an answer that reaches past the
        # first hop, so waited on, no member was ever ready and the fallback took an entry the cycle
        # still needed - which the manager refuses to stop while the cycle runs.
        Name = 'search: a block both ways is a cycle and is not waited on'
        File = 'src\Bws.Core\Planning\DependentsFirst.cs'
        From = 'blockedBy[name].All(other => done.Contains(other) || blockedBy[other].Contains(name)));'
        To = 'blockedBy[name].All(other => done.Contains(other)));'
        Test = 'PlanGraphPropertyTests.A_plan_over_any_dependency_graph_does_each_thing_once_and_nothing_before_what_needs_it'
    }
    @{
        Name = 'plan: a cycle among the dependants is stopped before the entry it needs'
        File = 'src\Bws.Core\Planning\DependentsFirst.cs'
        From = 'blockedBy[name].All(other => done.Contains(other) || blockedBy[other].Contains(name)));'
        To = 'blockedBy[name].All(other => done.Contains(other)));'
        Test = 'DependencyCycleTests.A_cycle_among_the_dependants_is_stopped_before_the_entry_it_needs'
    }
    @{
        # The cycle test asks the answers about a name as the listing spells it, and the manager may
        # spell it otherwise - kept with case, the sets would miss the cycle.
        Name = 'plan: a cycle the manager spells in another case is still a cycle'
        File = 'src\Bws.Core\Planning\DependentsFirst.cs'
        From = '? new HashSet<string>(theirs.Value!.Where(inside.Contains), StringComparer.OrdinalIgnoreCase)'
        To = '? new HashSet<string>(theirs.Value!.Where(inside.Contains), StringComparer.Ordinal)'
        Test = 'DependencyCycleTests.A_cycle_the_manager_spells_in_another_case_is_still_a_cycle'
    }
    @{
        # Fuzzing finding B: housemates stood in the order of the listing, so one needed by another was
        # asked to stop first, refused (1051), and died with the process instead. A real shape: mpssvc
        # needs bfe in one process on the machine this was built on.
        Name = 'search: housemates go down with what needs them first'
        File = 'src\Bws.Core\Planning\ForcedStop.cs'
        From = 'return Aftermath.Weigh(processes, facts, target, InOrder(asked, sharing), action, processId);'
        To = 'return Aftermath.Weigh(processes, facts, target, sharing, action, processId);'
        Test = 'PlanGraphPropertyTests.A_plan_over_any_dependency_graph_does_each_thing_once_and_nothing_before_what_needs_it'
    }
    @{
        Name = 'forced stop: housemates go down in the order the manager lets them'
        File = 'src\Bws.Core\Planning\ForcedStop.cs'
        From = 'return Aftermath.Weigh(processes, facts, target, InOrder(asked, sharing), action, processId);'
        To = 'return Aftermath.Weigh(processes, facts, target, sharing, action, processId);'
        Test = 'ForcedStopTests.Housemates_go_down_in_the_order_the_manager_lets_them_rather_than_the_order_listed'
    }
    @{
        # Rule 5 as a property: once a step going forward has failed, nothing later going forward is
        # asked. Without the hold, a refused stop of a dependant was followed by the stop it protected.
        Name = 'search: a run asks nothing forward past a step that failed'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = 'return (forwardFailed && !putsBack && !behind) || (cascadeFailed && behind)'
        To = 'return (false && !putsBack && !behind) || (cascadeFailed && behind)'
        Test = 'PlanRunPropertyTests.A_run_asks_only_what_the_preview_showed_and_puts_back_what_it_took_down'
    }
    @{
        # A dependant that refused to stop is still running on the process, so ending the process under
        # it is exactly what the refusal protected (stability report W-2).
        Name = 'search: a run ends no process past a cascade step that failed'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = 'return (forwardFailed && !putsBack && !behind) || (cascadeFailed && behind)'
        To = 'return (forwardFailed && !putsBack && !behind) || (false && behind)'
        Test = 'PlanRunPropertyTests.A_run_asks_only_what_the_preview_showed_and_puts_back_what_it_took_down'
    }
    @{
        # Stop goes no further forward and still puts back what the run took down - the Ctrl+C on a
        # virtual machine in 2026-08 that left a restarted service stopped.
        Name = 'search: a run stopped still puts back what it took down'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = 'if (abandoned || (cancelled && !putsBack))'
        To = 'if (abandoned || cancelled)'
        Test = 'PlanRunPropertyTests.A_run_asks_only_what_the_preview_showed_and_puts_back_what_it_took_down'
    }
    @{
        Name = 'search: a run abandoned asks nothing more'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = 'if (abandoned || (cancelled && !putsBack))'
        To = 'if (cancelled && !putsBack)'
        Test = 'PlanRunPropertyTests.A_run_asks_only_what_the_preview_showed_and_puts_back_what_it_took_down'
    }
    @{
        # What was never taken down is not given back (stability report W-5): a restart pressed Stop on
        # before its first step started a service that had been stopped all along.
        Name = 'search: a run puts back only what it took down'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = 'StepReason.Restore when !NetEffect.TookDown(results, step.ServiceName) => SkipReason.NothingToPutBack,'
        To = 'StepReason.Restore when false => SkipReason.NothingToPutBack,'
        Test = 'PlanRunPropertyTests.A_run_asks_only_what_the_preview_showed_and_puts_back_what_it_took_down'
    }
    @{
        # The number in the preview can belong to a process nobody has heard of by the time the step
        # runs, and ending it would be a step the preview never showed.
        Name = 'search: a run ends only the process the preview named'
        File = 'src\Bws.Core\Planning\PlanRunner.cs'
        From = 'return holding.IsPresent && holding.Value == step.ProcessId'
        To = 'return holding.IsPresent'
        Test = 'PlanRunPropertyTests.A_run_asks_only_what_the_preview_showed_and_puts_back_what_it_took_down'
    }
    @{
        # Fuzzing finding D, 2026-10-17: the measurement was taken out of "notRead" and left in
        # "unreadable", so a refused one came through as a refusal of a field the file never claims.
        Name = 'search: a refused measurement is not in the snapshot file'
        File = 'src\Bws.Core\Snapshots\SnapshotJson.cs'
        From = 'if (entry["unreadable"] is JsonObject unreadable && unreadable.Remove(property) && unreadable.Count == 0)'
        To = 'if (entry["unreadable"] is JsonObject unreadable && false)'
        Test = 'SnapshotRoundTripPropertyTests.A_machine_written_to_a_snapshot_and_read_back_is_the_same_machine_with_nothing_unread_lost'
    }
    @{
        Name = 'snapshot: a measurement the machine refused is not in the file either'
        File = 'src\Bws.Core\Snapshots\SnapshotJson.cs'
        From = 'if (entry["unreadable"] is JsonObject unreadable && unreadable.Remove(property) && unreadable.Count == 0)'
        To = 'if (entry["unreadable"] is JsonObject unreadable && false)'
        Test = 'SnapshotTests.A_measurement_the_machine_refused_is_not_in_the_file_either'
    }
    @{
        # The review of #77: the example held only the absence, so a writer dropping every refusal of an
        # entry once the measurement was among them kept it green. The refusal of the start type in the
        # same entries is its positive control.
        Name = 'snapshot: a refused measurement takes no other refusal with it'
        File = 'src\Bws.Core\Snapshots\SnapshotJson.cs'
        From = 'unreadable.Remove(property) && unreadable.Count == 0)'
        To = 'unreadable.Remove(property) && unreadable.Count >= 0)'
        Test = 'SnapshotTests.A_measurement_the_machine_refused_is_not_in_the_file_either'
    }
    @{
        # Rule 8 across a file: a refusal written as "never asked" is a snapshot claiming the machine was
        # merely not asked, when it said no. The oracle counts the fields from the entry's own readings,
        # so this is caught although both sides of the comparison pass through the mutated line.
        Name = 'search: a refused field stays refused across a snapshot file'
        File = 'src\Bws.Core\Snapshots\EntryDocument.cs'
        From = 'unreadable[Camel(field)] = new UnreadableDocument(reading.ErrorCode, reading.Reason!);'
        To = 'notRead.Add(Camel(field));'
        Test = 'SnapshotRoundTripPropertyTests.A_machine_written_to_a_snapshot_and_read_back_is_the_same_machine_with_nothing_unread_lost'
    }
    @{
        Name = 'search: a field never asked stays never asked across a snapshot file'
        File = 'src\Bws.Core\Snapshots\EntryDocument.cs'
        From = 'notRead.Add(Camel(field));'
        To = '_ = field;'
        Test = 'SnapshotRoundTripPropertyTests.A_machine_written_to_a_snapshot_and_read_back_is_the_same_machine_with_nothing_unread_lost'
    }
    @{
        # The manager refusing to say who depends on an entry is a refusal in the entry, never an
        # entry nobody depends on - the second would reach the snapshot as a fact.
        Name = 'search: a refused answer about dependants reaches the entry as a refusal'
        File = 'src\Bws.Core\RequiredByPass.cs'
        From = 'RequiredBy = catalog.ReadDependents(entries[index].ServiceName)'
        To = 'RequiredBy = catalog.ReadDependents(entries[index].ServiceName) is { Outcome: ReadOutcome.Denied } ? Reading<IReadOnlyList<string>>.Absent() : catalog.ReadDependents(entries[index].ServiceName)'
        Test = 'SnapshotRoundTripPropertyTests.A_machine_written_to_a_snapshot_and_read_back_is_the_same_machine_with_nothing_unread_lost'
    }
    @{
        # Fuzzing finding E, 2026-10-17: a watch that ran out before anything was asked came back as
        # TimedOut, which the way back counts as a move of this run - so a restart started a housemate
        # somebody else was stopping, though this tool never asked it to stop.
        Name = 'plan run: a watch that runs out before anything is asked puts nothing back'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '_ when !asked => Refused(step, ControlAnswer.Refused(0, NeverArrived(target)), Where(seen), Holding(seen), started),'
        To = '_ when false => Refused(step, ControlAnswer.Refused(0, NeverArrived(target)), Where(seen), Holding(seen), started),'
        Test = 'PlanRunProgressTests.A_restart_that_only_watched_an_entry_stopping_and_ran_out_puts_nothing_back'
    }
    @{
        # The same mutant against the seed the minute-long search found, which is the procedure for a
        # finding: the input it failed on replayed on every pull request.
        Name = 'search: the run the first minute-long search found puts back nothing it never asked to stop'
        File = 'src\Bws.Core\Planning\PlanRunner.Waiting.cs'
        From = '_ when !asked => Refused(step, ControlAnswer.Refused(0, NeverArrived(target)), Where(seen), Holding(seen), started),'
        To = '_ when false => Refused(step, ControlAnswer.Refused(0, NeverArrived(target)), Where(seen), Holding(seen), started),'
        Test = 'PlanRunPropertyTests.The_run_the_first_minute_long_search_found_puts_back_nothing_it_never_asked_to_stop'
    }
    @{
        # J2, 2026-10-20: the change history in the window. Identical changes share a line (J2-b), and
        # identical is decided by identity - the internal name without regard to case, the account by SID.
        Name = 'history groups: the internal name compares without regard to case'
        File = 'src\Bws.Core\History\ChangeGroup.cs'
        From = '            change.ServiceName?.ToUpperInvariant(),'
        To = '            change.ServiceName,'
        Test = 'ChangeGroupTests.The_internal_name_compares_the_way_the_manager_compares_it'
    }
    @{
        Name = 'history groups: the account is its SID, not its name'
        File = 'src\Bws.Core\History\ChangeGroup.cs'
        From = '            change.By?.Sid,'
        To = '            change.By?.Name,'
        Test = 'ChangeGroupTests.The_account_is_its_sid_and_its_name_does_not_split_a_group'
    }
    @{
        Name = 'window history: identical changes share a line until somebody chooses otherwise'
        File = 'src\Bws.Gui\ViewModels\HistoryFilter.cs'
        From = '        _arranged = _together;'
        To = '        _arranged = each;'
        Test = 'HistoryTests.Identical_changes_share_a_line_by_default_and_the_line_under_show_says_so'
    }
    @{
        Name = 'window history: not by SYSTEM leaves SYSTEM out'
        File = 'src\Bws.Gui\ViewModels\HistoryFilter.cs'
        From = 'change => !string.Equals(change.By?.Sid, SystemSid, StringComparison.Ordinal)'
        To = 'change => string.Equals(change.By?.Sid, SystemSid, StringComparison.Ordinal)'
        Test = 'HistoryTests.The_second_group_counts_each_choice_and_not_by_system_says_how_many_of_how_many'
    }
    @{
        # GUI rule 3: a refresh keeps the tick somebody set.
        Name = 'window history: a new reading keeps the tick of the same name'
        File = 'src\Bws.Gui\ViewModels\HistoryFilter.cs'
        From = '        _chosen = whose.FirstOrDefault(choice => string.Equals(choice.Name, name, StringComparison.Ordinal)) ?? whose[0];'
        To = '        _chosen = whose.LastOrDefault(choice => !string.Equals(choice.Name, name, StringComparison.Ordinal)) ?? whose[0];'
        Test = 'HistoryTests.A_new_reading_keeps_the_table_and_both_ticks'
    }
    @{
        Name = 'window history: filtered first, then grouped'
        File = 'src\Bws.Gui\ViewModels\HistoryFilter.cs'
        # Re-anchored 2026-10-20: the tick and the search are asked of each change in one loop (AS-a).
        From = '            if (takes(_all[i]) && RowSearch.Finds(_said[i], _words))'
        To = '            if (RowSearch.Finds(_said[i], _words))'
        Test = 'HistoryTests.The_second_group_counts_each_choice_and_not_by_system_says_how_many_of_how_many'
    }
    @{
        Name = 'window history: a second ask while a reading is out waits for it'
        File = 'src\Bws.Gui\ViewModels\HistoryReading.cs'
        From = '        if (Out.IsCompleted)'
        To = '        if (!Out.IsFaulted)'
        Test = 'HistoryTests.A_second_ask_while_a_reading_is_out_waits_for_it_rather_than_reading_again'
    }
    @{
        Name = 'window history: F5 reads the log only once somebody asked for it'
        File = 'src\Bws.Gui\ViewModels\HistoryReading.cs'
        From = '    internal Task Fresh() => _asked ? Again() : Task.CompletedTask;'
        To = '    internal Task Fresh() => Again();'
        Test = 'HistoryTests.F5_reads_the_log_again_only_once_somebody_has_asked_for_it'
    }
    @{
        # Rule 8: rows from an earlier reading under a new failure would read as the answer.
        Name = 'window history: a failure leaves nothing of the reading before it'
        File = 'src\Bws.Gui\ViewModels\HistoryReading.cs'
        From = '        _latest = null;'
        To = '        _ = _latest;'
        Test = 'HistoryTests.A_failure_after_a_good_reading_leaves_nothing_of_the_good_one_on_screen'
    }
    @{
        Name = 'window history: the panel gathers identical changes of its entry'
        File = 'src\Bws.Gui\ViewModels\HistoryReading.cs'
        From = '        var lines = ChangeGroup.Of(changes).Select(Line).ToList();'
        To = '        var lines = ChangeGroup.Each(changes).Select(Line).ToList();'
        Test = 'HistoryTests.The_panel_ends_with_the_entrys_history_identical_changes_together'
    }
    @{
        Name = 'window history: the panel ends with the history section'
        File = 'src\Bws.Gui\ViewModels\Chosen.cs'
        From = '        Sections = _history is { } history ? [.. sections, history.SectionFor(row.ServiceName)] : sections;'
        To = '        Sections = sections;'
        Test = 'HistoryTests.The_panel_ends_with_the_entrys_history_identical_changes_together'
    }
    @{
        Name = 'window history: opening a panel reads the log'
        File = 'src\Bws.Gui\ViewModels\Chosen.cs'
        From = '        _history?.Need();'
        To = '        _ = _history;'
        Test = 'HistoryTests.While_the_log_is_read_the_panel_section_says_so_and_fills_when_it_is_back'
    }
    @{
        Name = 'window history: the list is not on screen while the history is'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Snapshots.cs'
        From = ' && !History.InTheMiddle;'
        To = ';'
        Test = 'HistoryTests.Opening_the_view_reads_the_log_and_puts_it_where_the_list_was'
    }
    @{
        Name = 'window history: Escape puts the history away'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Snapshots.cs'
        From = 'Snapshotting.PutAway() || History.PutAway() || Chosen.Hide();'
        To = 'Snapshotting.PutAway() || Chosen.Hide();'
        Test = 'HistoryWindowGuards.Escape_puts_the_history_away_before_it_touches_the_query'
    }
    @{
        Name = 'window history: F5 reads the log again with the machine'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.cs'
        From = '    public Task LoadAsync() => Task.WhenAll(ReadMachineAsync(Relisting.Afresh), History.Fresh());'
        To = '    public Task LoadAsync() => ReadMachineAsync(Relisting.Afresh);'
        Test = 'HistoryTests.F5_reads_the_log_again_only_once_somebody_has_asked_for_it'
    }
    @{
        Name = 'window history: the bar offers the change history under audit'
        File = 'src\Bws.Gui\ActionBar.xaml.cs'
        From = '"historyOpen", () => HistoryRequest'
        To = '"historyOpened", () => HistoryRequest'
        Test = 'SnapshotWindowGuards.The_bar_offers_taking_comparing_comparing_two_and_the_history_under_one_button'
    }
    @{
        Name = 'window show row: a line goes between two groups of choices'
        File = 'src\Bws.Gui\ShowRow.xaml.cs'
        From = '                ShowMenu.Items.Add(new Separator());'
        To = '                _ = ShowMenu;'
        Test = 'HistoryWindowGuards.Show_over_the_history_offers_its_two_groups_with_a_line_between_them'
    }
    @{
        Name = 'window history: a snapshot form arriving takes the middle from the history'
        File = 'src\Bws.Gui\SnapshotDoors.cs'
        From = '            _model.History.PutAway();'
        To = '            _ = _model.History;'
        Test = 'HistoryWindowGuards.A_snapshot_form_arriving_takes_the_middle_from_the_history'
    }
    @{
        Name = 'window history: the view is drawn where the list was'
        File = 'src\Bws.Gui\MainWindow.Overview.cs'
        From = '        ChangeHistory.Visibility = history ? Visibility.Visible : Visibility.Collapsed;'
        To = '        ChangeHistory.Visibility = Visibility.Collapsed;'
        Test = 'HistoryWindowGuards.The_item_under_audit_puts_the_history_in_the_middle_and_the_controls_of_the_list_away'
    }
    @{
        # J1-b in the window: one entry never claims an installation, and says why.
        Name = 'window history: the panel says why no installation is listed for one entry'
        File = 'src\Bws.Gui\ViewModels\HistoryWords.cs'
        From = '                lines.Add(Texts.Of("gui.history.installationsNotByName"));'
        To = '                _ = lines;'
        Test = 'HistoryTests.The_panel_ends_with_the_entrys_history_identical_changes_together'
    }
    @{
        # Review of #81: the panel said the log could not be read while the view said it was reading.
        Name = 'window history: a new reading is not the last one''s failure'
        File = 'src\Bws.Gui\ViewModels\HistoryReading.cs'
        From = '        _failure = string.Empty;'
        To = '        _ = _failure;'
        Test = 'HistoryTests.A_new_reading_after_a_failure_says_reading_in_the_panel_rather_than_the_old_failure'
    }
    @{
        # Review of #81: the panel rebuilds once a second while its row moves, and the section with it.
        Name = 'window history: one reading and one entry make the section once'
        File = 'src\Bws.Gui\ViewModels\HistoryReading.cs'
        From = '            return made.Section;'
        To = '            _ = made.Section;'
        Test = 'HistoryTests.One_reading_and_one_entry_make_the_section_once_and_a_new_reading_makes_it_again'
    }

    # THE SEARCH, THE FIRST SORT AND THE EXPORT IN THE VIEWS OF THE AUDIT - owner's decisions AS-a to AS-d
    # of 2026-10-20 (docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md section 11).
    @{
        # Every word, not any word - "bits spooler" must find nothing.
        Name = 'audit search: every word has to be in the row'
        File = 'src\Bws.Gui\ViewModels\RowSearch.cs'
        From = @'
            if (!said.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
'@
        To = @'
            if (said.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
'@
        Test = 'RowSearchTests.Every_word_has_to_be_somewhere_in_the_row_in_any_case'
    }
    @{
        # Rule 26: under Turkish rules a culture's comparison misses BITS for "bits".
        Name = 'audit search: case is the same in every culture'
        File = 'src\Bws.Gui\ViewModels\RowSearch.cs'
        From = '            if (!said.Contains(word, StringComparison.OrdinalIgnoreCase))'
        To = '            if (!said.Contains(word, StringComparison.CurrentCultureIgnoreCase))'
        Test = 'RowSearchTests.Case_is_the_same_whatever_the_culture_of_the_machine'
    }
    @{
        Name = 'audit search: a word never spans two values'
        File = 'src\Bws.Gui\ViewModels\RowSearch.cs'
        From = "        string.Join('\n', values.Where(value => !string.IsNullOrEmpty(value)));"
        To = '        string.Join(string.Empty, values.Where(value => !string.IsNullOrEmpty(value)));'
        Test = 'RowSearchTests.A_word_never_matches_across_the_edge_of_two_values'
    }
    @{
        Name = 'audit search: the field hands what is typed to the table'
        File = 'src\Bws.Gui\ShowRow.xaml'
        From = '                             Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged, Delay=400}" />'
        To = '                             Text="{Binding SearchText, Mode=OneWay}" />'
        Test = 'HistoryWindowGuards.Escape_empties_the_search_first_and_puts_the_history_away_on_the_next_press'
    }
    @{
        Name = 'audit search: the field has a name a probe can ask for'
        File = 'src\Bws.Gui\ShowRow.xaml.cs'
        From = '            AutomationProperties.SetAutomationId(SearchBox, value + "Search");'
        To = ''
        Test = 'HistoryWindowGuards.Control_F_under_the_history_takes_its_own_search_and_leaves_the_list_box_alone'
    }
    @{
        Name = 'window history: the search narrows the changes'
        File = 'src\Bws.Gui\ViewModels\HistoryFilter.cs'
        From = '            _words = RowSearch.Words(_searchText);'
        To = '            _words = [];'
        Test = 'AuditSearchTests.A_search_narrows_the_changes_before_they_are_grouped'
    }
    @{
        # docs/11 3.8 - a search that leaves nothing is a sentence, not an empty rectangle.
        Name = 'window history: a search that finds nothing says so'
        File = 'src\Bws.Gui\ViewModels\HistoryFilter.cs'
        From = '            if (_words.Length > 0 && _shown == 0 && _all.Count > 0)'
        To = '            if (_words.Length > 0 && _shown == 0 && _all.Count < 0)'
        Test = 'AuditSearchTests.A_search_and_a_tick_narrow_together_and_a_search_that_finds_nothing_says_so'
    }
    @{
        # Over a log just cleared the sentences above the table say it is empty - no second sentence.
        Name = 'window history: no sentence about a search over an empty log'
        File = 'src\Bws.Gui\ViewModels\HistoryFilter.cs'
        From = '            if (_words.Length > 0 && _shown == 0 && _all.Count > 0)'
        To = '            if (_words.Length > 0 && _shown == 0)'
        Test = 'AuditSearchTests.The_search_stays_when_the_log_is_read_again_and_says_nothing_over_an_empty_log'
    }
    @{
        # Rule 3: the account is its SID, and the search reads it though the table shows the name.
        Name = 'window history: the search reads the sid'
        File = 'src\Bws.Gui\ViewModels\HistoryFilter.cs'
        From = '            change.By?.Sid))'
        To = '            null))'
        Test = 'AuditSearchTests.The_search_reads_the_display_name_and_the_sid_the_table_leaves_out'
    }
    @{
        Name = 'window diff filter: the search narrows the comparison'
        File = 'src\Bws.Gui\ViewModels\DiffFilter.cs'
        From = '            _words = RowSearch.Words(_searchText);'
        To = '            _words = [];'
        Test = 'AuditSearchTests.A_search_over_a_comparison_narrows_with_its_choice_and_says_when_nothing_is_left'
    }
    @{
        Name = 'window diff filter: a search that finds nothing says so'
        File = 'src\Bws.Gui\ViewModels\DiffFilter.cs'
        From = '        _words.Length > 0 && Rows.Count == 0 && _all.Count > 0 ? Texts.Of("gui.diff.search.nothing", RowSearch.Typed(_words))'
        To = '        _words.Length < 0 && Rows.Count == 0 && _all.Count > 0 ? Texts.Of("gui.diff.search.nothing", RowSearch.Typed(_words))'
        Test = 'AuditSearchTests.A_search_over_a_comparison_narrows_with_its_choice_and_says_when_nothing_is_left'
    }
    @{
        Name = 'window diff table: gives way when the search leaves no row'
        File = 'src\Bws.Gui\DiffTable.xaml'
        From = '                        <DataTrigger Binding="{Binding Filter.Rows.Count}" Value="0">'
        To = '                        <DataTrigger Binding="{Binding Lines.Count}" Value="0">'
        Test = 'AuditSearchTests.The_comparison_opens_sorted_by_entry_and_gives_way_when_the_search_leaves_nothing'
    }
    @{
        # AS-d: the owner never pressed a heading, because nothing said one sorts.
        Name = 'audit tables: the first sort is a real sort'
        File = 'src\Bws.Gui\FirstSort.cs'
        From = '        grid.Items.SortDescriptions.Add(new SortDescription(path, direction));'
        To = ''
        Test = 'HistoryWindowGuards.The_history_opens_newest_first_with_the_mark_on_when_and_again_on_the_next_opening'
    }
    @{
        Name = 'audit tables: the first sort is marked on its heading'
        File = 'src\Bws.Gui\FirstSort.cs'
        From = '            column.SortDirection = string.Equals(column.SortMemberPath, path, StringComparison.Ordinal) ? direction : null;'
        To = '            column.SortDirection = null;'
        Test = 'AuditSearchTests.The_comparison_opens_sorted_by_entry_and_gives_way_when_the_search_leaves_nothing'
    }
    @{
        # The cheaper loss first: a view's search does not outlive the view.
        Name = 'window escape: the search over a view goes before the view'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Snapshots.cs'
        From = '    internal bool PutAwayAView() => MiddleSearchEmptied() || Snapshotting.PutAway() || History.PutAway() || Chosen.Hide();'
        To = '    internal bool PutAwayAView() => Snapshotting.PutAway() || History.PutAway() || Chosen.Hide();'
        Test = 'HistoryWindowGuards.Escape_empties_the_search_first_and_puts_the_history_away_on_the_next_press'
    }
    @{
        # A review of #82: a history read again and found empty keeps the text with the box gone.
        Name = 'window escape: only a search somebody can see is emptied'
        File = 'src\Bws.Gui\ViewModels\MainViewModel.Snapshots.cs'
        From = '        if (MiddleChoices is not { Offered: true, SearchText.Length: > 0 } choices)'
        To = '        if (MiddleChoices is not { SearchText.Length: > 0 } choices)'
        Test = 'HistoryWindowGuards.Escape_over_a_history_read_again_and_found_empty_puts_it_away_at_once'
    }
    @{
        # G-9 for the view's search: Escape inside the 400 ms must find the text that is there.
        Name = 'window escape: the search over a view is handed over first'
        File = 'src\Bws.Gui\MainWindow.Keyboard.cs'
        From = '                ChangeHistory.Choices.Commit();'
        To = ''
        Test = 'HistoryWindowGuards.Escape_empties_the_search_first_and_puts_the_history_away_on_the_next_press'
    }
    @{
        # Under a view the list's box is collapsed - Ctrl+F put the caret nowhere.
        Name = 'window ctrl+f: under a view, the view''s own search'
        File = 'src\Bws.Gui\Finding.cs'
        From = '        if (model.MiddleChoices is { Offered: true })'
        To = '        if (model.MiddleChoices is { Offered: false })'
        Test = 'HistoryWindowGuards.Control_F_under_the_history_takes_its_own_search_and_leaves_the_list_box_alone'
    }
    @{
        Name = 'window history export: nothing to write before a reading'
        File = 'src\Bws.Gui\ViewModels\HistorySheet.cs'
        From = '    public bool Exportable => Admissions.Count > 0;'
        To = '    public bool Exportable => true;'
        Test = 'HistoryExportingTests.There_is_nothing_to_write_until_a_reading_has_come_back_and_nothing_after_a_failure'
    }
    @{
        Name = 'window history export: the button waits for a reading'
        File = 'src\Bws.Gui\HistoryView.xaml'
        From = '                        IsEnabled="{Binding Exportable}"'
        To = ''
        Test = 'HistoryWindowGuards.The_export_waits_for_a_reading'
    }
    @{
        Name = 'window history export: the file is written'
        File = 'src\Bws.Gui\ViewModels\HistoryReading.cs'
        From = '            await Task.Run(() => AtomicFile.Write(path, text)).ConfigureAwait(true);'
        To = '            await Task.CompletedTask.ConfigureAwait(true);'
        Test = 'HistoryExportingTests.The_window_writes_it_with_the_mark_a_spreadsheet_needs_and_says_how_many_lines'
    }
    @{
        # A spreadsheet sums numbers, and a blank is not one.
        Name = 'window history export: times is a number on every line'
        File = 'src\Bws.Gui\ViewModels\HistoryExporting.cs'
        From = '                [row.When, row.Count.ToString(CultureInfo.InvariantCulture), row.Since, row.Entry, row.Change, row.By, row.Group.Latest.By?.Sid ?? string.Empty],'
        To = '                [row.When, row.Times, row.Since, row.Entry, row.Change, row.By, row.Group.Latest.By?.Sid ?? string.Empty],'
        Test = 'HistoryExportingTests.A_line_carries_its_count_as_a_number_and_the_account_by_sid'
    }
    @{
        # AS-c and rule 3: the account in the file is its SID beside its name.
        Name = 'window history export: the account goes in by sid'
        File = 'src\Bws.Gui\ViewModels\HistoryExporting.cs'
        From = '                [row.When, row.Count.ToString(CultureInfo.InvariantCulture), row.Since, row.Entry, row.Change, row.By, row.Group.Latest.By?.Sid ?? string.Empty],'
        To = '                [row.When, row.Count.ToString(CultureInfo.InvariantCulture), row.Since, row.Entry, row.Change, row.By, string.Empty],'
        Test = 'HistoryExportingTests.A_line_carries_its_count_as_a_number_and_the_account_by_sid'
    }
    @{
        Name = 'window history export: the search is said in the file'
        File = 'src\Bws.Gui\ViewModels\HistoryExporting.cs'
        From = '            yield return Texts.Of("gui.history.export.searched", RowSearch.Typed(words));'
        To = '            _ = words;'
        Test = 'HistoryExportingTests.A_search_is_said_in_the_file_and_only_the_lines_it_leaves_go_in'
    }
    @{
        Name = 'window diff export: the search is said in the file'
        File = 'src\Bws.Gui\ViewModels\DiffExporting.cs'
        From = '            yield return Texts.Of("gui.diff.export.searched", RowSearch.Typed(words));'
        To = '            _ = words;'
        Test = 'DiffExportingTests.A_search_over_the_comparison_is_said_in_the_file'
    }
)

$mutations
