using System.Diagnostics;
using Bws.Cli;
using Bws.Core;
using Bws.Core.Planning;
using Bws.Core.Querying;
using Bws.Core.Snapshots;

// Slices S1 to S3: read every entry the manager knows about, narrow the listing with a
// query, and work out what a stop, a start or a restart would do - then carry it out and
// say what came of every step.

// THE WHOLE PROGRAM IS INSIDE THIS, AND UNTIL 2026-08-03 THE FIRST THIRTY LINES WERE NOT.
//
// The catch at the bottom describes itself as the entry point's, which is the right place for a
// broad one - and it started after the arguments had been read, after help and version had been
// answered, and after the query had been parsed. Those three are exactly the places that handle
// what a person typed, so the one part of the run most likely to meet something unexpected was
// the part with nothing behind it.
//
// MEASURED 2026-08-03: `bws list --query "name:*a*a..."` with about a thousand repetitions ended
// with an unhandled exception, a stack trace, and exit code 0xE0434352 - a number that is not in
// the table of exit codes and that no script can be expected to know. The parser has since been
// fixed to report that as a problem, which is the real repair, and this is the net underneath it:
// the next surprise in the same region ends with a sentence and code 1 rather than a crash.
//
// The line in the regression surface of docs/04 reading "every ending has a code from the table -
// PARTIAL, an unforeseen failure still falls into code 1" was not true here. It is now.
try
{
    var options = CommandLine.Read(args);

    // Version and help first, because neither depends on a verb, on options making sense, or on
    // the tool being able to reach the service manager. Why they answer the way they do lives in
    // Immediate, together with what this tool used to do instead.
    if (Immediate.Answer(options) is { } answered)
    {
        return answered;
    }

    // EVERYTHING WRONG WITH WHAT SOMEBODY TYPED, IN ONE PLACE AND BEFORE THE MANAGER IS OPENED.
    // Ten checks in an order that carries meaning, none of which needs a machine - the reasoning
    // for each is beside it in Refusals, where they moved on 2026-08-25 when the size ratchet
    // asked this file for a seam a second time. Immediate above was the first.
    if (Refusals.Answer(options) is { } mistyped)
    {
        return mistyped;
    }

    // Never null where the verb needs one: Refusals has already turned back both a missing word
    // and one that names no start type, so what is left here is a value. Read again rather than
    // carried out of that block, because a refusal handing back a value as well as a code would
    // be two answers from one call, and the reading itself is a table lookup.
    var wanted = WriteCommands.NeedsAStartType(options.Kind)
        ? WriteCommands.Named(options.StartTypeWord)
        : null;

    // Read the query before touching the system. A typo costs nothing this way, and the
    // alternative is enumerating hundreds of entries in order to throw them away.
    var parsed = QueryParser.Parse(options.Query);

    if (!parsed.IsValid)
    {
        foreach (var problem in parsed.Problems)
        {
            Console.Error.WriteLine(QueryMessages.Of(problem));
        }

        // A query with a mistake in it filters nothing and says what is wrong. In a window
        // that means the listing stays as it was, and here it means no listing at all: a
        // terminal writes into pipes, and answering a typo with the unfiltered listing would
        // hand every entry to whatever comes next in the pipeline.
        return ExitCode.Usage;
    }

    var stopwatch = Stopwatch.StartNew();

    // Comparing two files never opens the service control manager, and it matters that it
    // does not. A pipeline step comparing two snapshots on a build agent has no business
    // needing rights over that agent's own services, and reading eight hundred entries this
    // branch never looks at would spend half a second saying nothing.
    var offline = options.Kind == CommandKind.SnapshotDiff && !options.Live;

    // Skip unless somebody said otherwise, everywhere, including the branches that never see
    // the switch. `snapshot diff --live` is the one that never sees it: the switch belongs to
    // `list` and `snapshot create`, because offering it on the diff verb would also accept it
    // on the two-file form where it does nothing, and a switch that does nothing is the
    // silence the belonging table exists to end. The cost of that choice is honest rather
    // than hidden - a live comparison reports "one side did not read this" for an entry on a
    // share, which is an admission about the comparison and never a false difference.
    var networkPaths = options.FollowNetwork ? NetworkPaths.Follow : NetworkPaths.Skip;

    var catalog = offline ? null : new WindowsScmCatalog(networkPaths);
    IReadOnlyList<ScmEntry> entries = offline ? [] : catalog!.ReadAll();
    var read = offline ? 0 : stopwatch.ElapsedMilliseconds;

    // Its own number, and not folded into the time spent filtering. The second pass is by
    // far the most expensive thing this tool does - measured at around five seconds against
    // a third of one for the read - and reporting it under the word "filtered" would put a
    // true number next to a sentence about something else.
    long inspected = 0;

    // Its own number for the same reason, and it turned out to need one: the pass is cheap
    // in calls and not free in work, so it showed up inside the figure labelled "filtered"
    // until it was pulled out.
    long measured = 0;

    // Every command produces its text, and exactly one place puts text on the data channel.
    // Not tidiness: it is what makes "could anything else have reached standard output"
    // answerable by looking, and a guard in the architecture tests holds it to one.
    string data;
    var exit = ExitCode.Ok;

    if (options.Kind == CommandKind.SnapshotDiff)
    {
        // Two files, or one file and the machine. Never one file on its own: that would have
        // to be guessed into meaning something, and the only thing it could mean is the
        // expensive one. --live says it in a word, which is how E1 writes it.
        if (options.Path.Length == 0 || (options.Against.Length == 0 && !options.Live))
        {
            stopwatch.Stop();
            Console.Error.WriteLine(Texts.Of("cli.diff.needsTwoSides"));

            return ExitCode.Usage;
        }

        if (options.Live && options.Against.Length > 0)
        {
            // Three sides to a comparison with two. Refused rather than resolved by picking
            // one, because either choice would silently ignore something the person typed.
            stopwatch.Stop();
            Console.Error.WriteLine(Texts.Of("cli.diff.liveTakesOneFile"));

            return ExitCode.Usage;
        }

        if (!SnapshotFiles.Load(options.Path, out var before, out var earlierFailed))
        {
            stopwatch.Stop();

            return earlierFailed;
        }

        Snapshot? after;

        if (options.Live)
        {
            // Signatures and hashes are read here for the same reason snapshot create reads
            // them: the file on the other side has them. Comparing against a reading that
            // skipped them would mark every entry as "one side never read this", which is
            // 810 admissions and no answer.
            var before2 = stopwatch.ElapsedMilliseconds;
            entries = SecondPass.Fill(entries, new WindowsBinaryInspector(networkPaths));
            inspected = stopwatch.ElapsedMilliseconds - before2;

            after = Snapshot.Of(entries, note: null, new SystemClock());
        }
        else if (!SnapshotFiles.Load(options.Against, out after, out var laterFailed))
        {
            stopwatch.Stop();

            return laterFailed;
        }

        if (!SnapshotFiles.Compare(before!, after!, out var difference, out var refused))
        {
            stopwatch.Stop();

            return refused;
        }

        stopwatch.Stop();

        data = options.Json ? DiffJson.Render(difference) : DiffText.Render(difference);

        if (options.Timing)
        {
            if (options.Live)
            {
                // Its own line, like everywhere else the second pass runs. Folded into the
                // comparison figure it would put seconds of file reading under a word about
                // comparing two documents in memory.
                Console.Error.WriteLine(Texts.Of("cli.info.timingRead", entries.Count, read));
                Console.Error.WriteLine(Texts.Of("cli.info.timingInspected", inspected));
            }

            Console.Error.WriteLine(Texts.Of("cli.info.timingCompared", stopwatch.ElapsedMilliseconds));
        }

        // Only when asked. Every other code in the table answers "did the tool work", and
        // this is the one place where a code can also answer "what did it find" - which is a
        // different question and a script has to opt into being told that way.
        exit = options.ExitCodeOnDifference && difference.Any ? ExitCode.Differences : ExitCode.Ok;
    }
    else if (options.Kind == CommandKind.SnapshotCreate)
    {
        // No plan, and that is worth saying rather than leaving as an absence. ADR-11 puts
        // every write behind a plan, and it means writes to the machine: a plan exists so
        // that stopping a service can be previewed, reversed and turned into a command.
        // Writing a file the person named changes nothing about any service, and there is
        // nothing to preview that the file itself does not already say.
        //
        // Refused before the expensive work rather than after it, and the order is the whole
        // courtesy: verifying signatures takes about a second, and spending it to then say
        // "there is already a file there" would be a second nobody got anything for.
        //
        // Found 2026-08-02 by reading a security document from another project, and confirmed
        // by doing it: a file holding the words "this is not a snapshot" was replaced without
        // a word and the command ended with code 0. Nothing else this tool does overwrites
        // anything - a snapshot was the one place, and it was the place somebody keeps for
        // months.
        //
        // Asked here as well as after the name is worked out, and both go through the same
        // method rather than repeating the condition. This one is the courtesy - it saves the
        // second the signatures cost when the answer is already known - and it can only be asked
        // when a name was given, because otherwise there is no name yet to ask about.
        if (options.Path.Length > 0 && !SnapshotFiles.MayWrite(options.Path, options.Force, out var taken))
        {
            stopwatch.Stop();
            Console.Error.WriteLine(taken);

            return ExitCode.Usage;
        }

        // Signatures and the hash are read every time here, unlike in a listing. A snapshot
        // is taken deliberately and kept for months, so being comparable against the next
        // one matters more than the several seconds it costs - and a snapshot missing them
        // would compare against one that has them as though the machine had changed.
        var before = stopwatch.ElapsedMilliseconds;
        entries = SecondPass.Fill(entries, new WindowsBinaryInspector(networkPaths));
        inspected = stopwatch.ElapsedMilliseconds - before;

        var snapshot = Snapshot.Of(entries, options.Note, new SystemClock());
        var target = SnapshotFiles.Target(options.Path, snapshot.Metadata);

        // THE SAME QUESTION ABOUT THE FILE THAT WILL ACTUALLY BE WRITTEN, which until 2026-08-03
        // was never asked when nobody named one. The comment here read "without a name the name
        // carries a timestamp to the second and collides with nothing", and that is true of one
        // person running the command twice and false of two runs started together by a script,
        // of a file restored from a backup, and of a machine fast enough to finish twice inside
        // a second. So the one command in this tool that overwrites anything had a way round its
        // own guard, on the path a person takes when they have not thought about the file name.
        if (!SnapshotFiles.MayWrite(target, options.Force, out var refusal)
            || !SnapshotFiles.KeepWhatCannotBeRead(target, out var quarantined, out refusal))
        {
            stopwatch.Stop();
            Console.Error.WriteLine(refusal);

            return ExitCode.Usage;
        }

        // Said before the write rather than after, because it is the sentence somebody needs in
        // order to find the file again if this run is not what they meant.
        if (quarantined is not null)
        {
            Console.Error.WriteLine(Texts.Of("cli.snapshot.quarantined", target, quarantined));
        }

        try
        {
            AtomicFile.Write(target, SnapshotJson.Render(snapshot));
        }
        catch (DirectoryNotFoundException missing)
        {
            // What somebody typed, not something that went wrong inside. ADR-18 forbids
            // inventing directories, so the honest answer is the one the usage code carries.
            stopwatch.Stop();
            Console.Error.WriteLine(missing.Message);
            return ExitCode.Usage;
        }

        stopwatch.Stop();

        data = options.Json
            ? SnapshotText.Render(snapshot, target, asJson: true)
            : SnapshotText.Render(snapshot, target, asJson: false);

        Execution.Report(entries, result: null, options, read, stopwatch.ElapsedMilliseconds, inspected, measured: 0);
    }
    else if (options.Kind == CommandKind.Show)
    {
        // The name is required, and its absence is a usage mistake rather than an empty answer -
        // OptionSurface.TakesAName carries the whole of that argument.
        if (options.ServiceName.Length == 0)
        {
            stopwatch.Stop();
            Console.Error.WriteLine(Texts.Of("cli.show.nameMissing"));

            return ExitCode.Usage;
        }

        var found = entries.FirstOrDefault(entry =>
            string.Equals(entry.ServiceName, options.ServiceName, StringComparison.OrdinalIgnoreCase));

        // Case-insensitively, because that is how the manager compares names, and by the internal
        // name only. A display name is translated into the language of the machine - rule 3 of
        // CLAUDE.md - so accepting one here would make a runbook work on one Windows and not on
        // another, quietly.
        if (found is null)
        {
            stopwatch.Stop();
            Console.Error.WriteLine(Texts.Of("cli.show.noSuchEntry", options.ServiceName));

            return ExitCode.Usage;
        }

        // MEMORY FIRST AND OVER THE WHOLE LISTING, WHICH LOOKS LIKE THE WRONG WAY ROUND AND IS
        // NOT. The pass counts how many entries share each process, so run over one entry it would
        // report a service sitting in a shared svchost as having it to itself. It costs 4-6 ms
        // over 798 entries, so there is nothing to save by narrowing it.
        //
        // Doing it before the entry is picked also keeps the two passes from having to be sewn
        // back together: pick once, from the list that already has the memory in it.
        var before = stopwatch.ElapsedMilliseconds;
        entries = MemoryPass.Fill(entries, new WindowsProcessMemoryReader());
        measured = stopwatch.ElapsedMilliseconds - before;

        found = entries.First(entry =>
            string.Equals(entry.ServiceName, found.ServiceName, StringComparison.Ordinal));

        // THE EXPENSIVE READING, OVER ONE ENTRY RATHER THAN OVER THE MACHINE, AND THAT IS THE
        // WHOLE REASON THIS VERB HAS ITS OWN BRANCH. The listing runs the second pass over
        // everything before it filters, because a query about signatures has to have them read
        // first - measured at 947-999 ms over 798 entries, and 911-1031 ms even when the query
        // matched one. Here there is no query and one entry, so the same reading is one file.
        before = stopwatch.ElapsedMilliseconds;
        found = SecondPass.Fill([found], new WindowsBinaryInspector(networkPaths))[0];
        inspected = stopwatch.ElapsedMilliseconds - before;

        stopwatch.Stop();

        // The same document the listing emits, on its own rather than inside an array of one.
        // Anything already reading `bws list --json` needs nothing new to read this, and a second
        // shape for the same facts is the second answer this project keeps paying for.
        //
        // --full has no meaning here and the switch table does not pretend otherwise: the document
        // carries every field in every state, because that is what makes it comparable.
        data = options.Json
            ? ListingJson.One(found)
            : EntryReport.Render(found, options.Full);

        // The same reporting every other command gets. Accepting --timing on this verb and then
        // printing nothing would be the exact silence the belonging table for that switch was
        // built to end, from the other side - and it was doing precisely that until a run on a
        // real machine printed six empty lines.
        //
        // result: null, like the write commands: there is no query here, so there is nothing to
        // say about how many of how many matched.
        Execution.Report(entries, result: null, options, read, stopwatch.ElapsedMilliseconds, inspected, measured);
    }
    else if (options.IsWrite)
    {
        // Never null here: the only command that leaves it unbuilt is the file comparison,
        // which is not a write and never reaches this branch.
        var plan = new PlanBuilder(entries, catalog!)
            .Build(new ServiceAction(options.Action, options.ServiceName, options.Dependents, wanted));

        if (!plan.IsRunnable)
        {
            stopwatch.Stop();

            foreach (var problem in plan.Problems)
            {
                Console.Error.WriteLine(PlanText.Describe(problem));
            }

            return ExitCode.Usage;
        }

        if (options.DryRun)
        {
            stopwatch.Stop();
            data = options.Json ? PlanJson.Render(plan) : PlanText.Render(plan);
        }
        else
        {
            var run = Execution.Carry(plan, options.Timeout);
            stopwatch.Stop();

            data = options.Json ? PlanJson.Render(run) : PlanText.Render(run);

            // A plan that did not finish is neither a broken tool nor a mistyped command,
            // so it is neither of the codes those two already have. Monitoring needs to
            // tell "the stop was refused" from "bws itself fell over".
            exit = run.Cancelled
                ? ExitCode.Interrupted
                : run.Completed ? ExitCode.Ok : ExitCode.Incomplete;
        }
    }
    else
    {
        // The second pass, and the first thing in this tool that is asked for rather than
        // simply done. Measured at 4620-7656 ms over 810 entries and 544 distinct files
        // against 476-551 ms for everything above, so a listing does not verify signatures
        // unless somebody wants them.
        //
        // A query about them counts as wanting them. Answering "signed:no" with an empty
        // list because nobody had looked would be a correct query returning what reads
        // exactly like "there are none" - which is the one failure this whole language is
        // arranged to avoid.
        var needs = parsed.Query!.Needs;

        if (options.Signatures || needs.HasFlag(ExtraRead.Signatures))
        {
            var before = stopwatch.ElapsedMilliseconds;
            entries = SecondPass.Fill(entries, new WindowsBinaryInspector(networkPaths));
            inspected = stopwatch.ElapsedMilliseconds - before;
        }

        // Asked for separately, because the two families are nothing alike - seconds against
        // single milliseconds - and answering a question about memory by verifying every
        // signature would cost a thousand times what was asked for.
        //
        // Over every entry, before the filter. MemoryPass counts how many entries share each
        // process, and counting that over a filtered list would report a service in a shared
        // process as having it to itself.
        //
        // Timed on its own line for the same reason the signatures are. The calls themselves
        // measure well under a millisecond, and the pass around them does not: it builds a
        // new record for all 810 entries. Folding that into the figure called "filtered"
        // would put a true number next to a sentence about something else, which is the
        // mistake this line was split off to avoid - and it was measured doing exactly that
        // before it was split.
        if (options.Memory || needs.HasFlag(ExtraRead.Memory))
        {
            var before = stopwatch.ElapsedMilliseconds;
            entries = MemoryPass.Fill(entries, new WindowsProcessMemoryReader());
            measured = stopwatch.ElapsedMilliseconds - before;
        }

        var result = parsed.Query!.Filter(entries);
        stopwatch.Stop();

        data = options.Json
            ? ListingJson.Render(result.Entries)
            : ListingTable.Render(result.Entries);

        Execution.Report(entries, result, options, read, stopwatch.ElapsedMilliseconds, inspected, measured);
    }

    if (options.IsWrite)
    {
        // Every command reads the manager first, so everything the read has to admit to is
        // owed on every command. It used to be said only when listing, which meant a plan
        // built on entries whose configuration was refused looked exactly like one built on
        // a complete reading.
        Execution.Report(entries, result: null, options, read, stopwatch.ElapsedMilliseconds, inspected: 0, measured: 0);
    }

    Output.Data(data);

    return exit;
}
#pragma warning disable CA1031
// The entry point of a command line tool is the one place a broad catch is right.
// It does not swallow anything: the message goes to the error channel and the process
// ends with a failing code. The alternative is a stack trace in the user's face, which
// tells them less and looks like a crash.
//
// Where the others are, and why, is a list in BroadCatchGuards rather than a number here.
// This sentence used to carry the count and it rotted three times - "exactly two", then
// five, then six - which is what a number in a comment does, since nothing counts it. Now
// something does, and a broad catch appearing anywhere new fails a test instead of joining
// a tally nobody maintains.
catch (Exception failure)
{
    // The whole chain, not just the top message. A wrapper such as
    // TypeInitializationException says only "something threw", and the sentence that
    // actually explains the failure sits underneath it. Printing one line and dropping
    // the rest is the quiet kind of silence rule 8 forbids.
    for (Exception? level = failure; level is not null; level = level.InnerException)
    {
        Console.Error.WriteLine(level.Message);
    }

    return ExitCode.Runtime;
}
#pragma warning restore CA1031
