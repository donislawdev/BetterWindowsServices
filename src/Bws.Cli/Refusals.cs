using Bws.Core.Planning;

namespace Bws.Cli;

/// <summary>
/// Everything wrong with what somebody typed, answered before the service manager is opened.
///
/// <b>Split out of Program.cs on 2026-08-25 when the size ratchet asked that file for a seam, which
/// is the second time it has asked and the second time the seam was already described by a comment
/// in it.</b> <see cref="Immediate"/> was the first. The subject here is one sentence: none of these
/// answers needs a machine, a query or a plan - they are all about the words, and they all end the
/// run with the code this tool reserves for what somebody typed wrongly.
///
/// <b>THE ORDER OF THE CHECKS IS PART OF WHAT THEY MEAN, AND IT IS NOT ALPHABETICAL.</b> A half
/// typed subcommand is asked about before an unknown option, because "snapshot" on its own would
/// otherwise be reported as an option nobody knows - and it is neither an option nor unknown. Each
/// one moved here in the order it stood, and moving one is a change to an answer rather than a tidy
/// up.
///
/// <b>Not one of these opens a handle or reads an entry</b>, which is the property that makes the
/// whole block worth having in front of the expensive work. Refusing a typo after eight hundred
/// entries have been read is half a second spent to say "you made a typo".
/// </summary>
internal static class Refusals
{
    /// <summary>
    /// Answers if there is something wrong to answer, and says nothing otherwise.
    /// </summary>
    /// <returns>The code to end on, or null when nothing about the words is wrong.</returns>

    // Long, and long WITH a list of unrelated mistakes, each three lines and a paragraph - splitting
    // the list would put the order above into two files where nothing holds it. It needed a
    // suppression while method length was counted in raw lines. Counted in lines of code it is 67,
    // one under the longest method in the product, so it stands under the shape ceiling with no
    // exemption, and the next line added here asks for the seam. Backlog 24 is where the wider
    // question lives.
    internal static int? Answer(CommandLine options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.BadVerb is not null)
        {
            // Named as a command rather than an option, and offered the nearest one. "Unknown option:
            // lst" was wrong twice over: lst is not an option, and the answer helped with nothing.
            var nearest = Suggestions.Nearest(options.BadVerb, OptionSurface.Verbs);

            Console.Error.WriteLine(nearest is null
                ? Texts.Of("cli.unknownCommand", options.BadVerb, string.Join(", ", OptionSurface.Verbs))
                : Texts.Of("cli.unknownCommandDidYouMean", options.BadVerb, nearest));

            Console.Error.WriteLine(Texts.Of("cli.usage"));
            return ExitCode.Usage;
        }

        if (options.BadSubcommand is not null)
        {
            // Ahead of the unknown-option check, because "snapshot" on its own would otherwise be
            // reported as an option nobody knows - and it is neither an option nor unknown.
            //
            // Two sentences rather than one, because the two cases are different and one wording
            // has to lie about one of them. Snapshot on its own is a command that is half typed.
            // Snapshot followed by a word we do not know is a command that does not exist.
            var available = string.Join(", ", OptionSurface.Subcommands);

            Console.Error.WriteLine(options.BadSubcommand.Length == 0
                ? Texts.Of("cli.subcommandMissing", available)
                : Texts.Of("cli.unknownSubcommand", options.BadSubcommand, available));

            Console.Error.WriteLine(Texts.Of("cli.usage"));
            return ExitCode.Usage;
        }

        if (options.Rejected.Count > 0)
        {
            // Diagnostics go to the error channel even when the run fails. The data channel stays
            // clean so a failed run never drops a stray line into somebody's pipe.
            Console.Error.WriteLine(Texts.Of("cli.unknownOption", string.Join(", ", options.Rejected)));
            Console.Error.WriteLine(Texts.Of("cli.usage"));
            return ExitCode.Usage;
        }

        if (options.Repeated.Count > 0)
        {
            // Accepted twice and honoured once is the same silence as accepted and ignored, which
            // this tool refuses everywhere else. The last one used to win without a word.
            Console.Error.WriteLine(Texts.Of("cli.optionGivenTwice", string.Join(", ", options.Repeated)));

            return ExitCode.Usage;
        }

        if (options.Incomplete.Count > 0)
        {
            // A different mistake from an unknown option, and it used to be reported as one -
            // sending somebody to hunt for a typo in a word they had spelled correctly.
            Console.Error.WriteLine(Texts.Of("cli.optionNeedsValue", string.Join(", ", options.Incomplete)));
            Console.Error.WriteLine(Texts.Of("cli.usage"));
            return ExitCode.Usage;
        }

        if (options.Kind == CommandKind.None)
        {
            Console.Error.WriteLine(Texts.Of("cli.usage"));
            return ExitCode.Usage;
        }

        if (options.Extra.Count > 0)
        {
            // AFTER THE VERB IS KNOWN, AND THAT IS WHY IT SITS HERE RATHER THAN BESIDE THE UNKNOWN
            // OPTION ABOVE. The sentence is about how many names THIS command has room for, so it
            // cannot be said until there is a command - and an unknown option stays the first
            // answer because it is the sharper mistake of the two.
            Console.Error.WriteLine(Texts.Of(
                "cli.wordsNotTaken",
                OptionSurface.Spelling(options.Kind),
                Texts.Of(OptionSurface.TakesWhat(options.Kind)),
                string.Join(", ", options.Extra)));

            Console.Error.WriteLine(Texts.Of("cli.usage"));
            return ExitCode.Usage;
        }

        if (options.Misplaced.Count > 0)
        {
            // An option that exists but not here. Refused rather than ignored: a switch that
            // quietly does nothing turns a runbook line into something that looks right and behaves
            // differently, and nobody finds out until it matters.
            foreach (var option in options.Misplaced)
            {
                Console.Error.WriteLine(Texts.Of(
                    "cli.optionNotForCommand",
                    option,
                    OptionSurface.Spelling(options.Kind),
                    string.Join(", ", OptionSurface.Accepts(option))));
            }

            return ExitCode.Usage;
        }

        return AboutTheAsk(options);
    }

    /// <summary>
    /// What is wrong with the words a write verb needs beside it.
    ///
    /// <b>Its own method because these are the only checks here that know which verb they are
    /// looking at.</b> Everything above is about the shape of the line - an option nobody knows, a
    /// value nobody gave - and could be asked of any command. These are about arguments a
    /// particular verb needs: a NAME and a TYPE for the write verbs, a name for <c>show</c>, and
    /// two sides for <c>snapshot diff</c>.
    ///
    /// <b>It stopped being only about write verbs on 2026-08-26</b>, when the two read verbs that
    /// were asking the same kind of question inside <c>Program</c> moved here - after the manager
    /// had been read, which is exactly what this class is in front of it to avoid.
    /// </summary>
    private static int? AboutTheAsk(CommandLine options)
    {
        if (options.IsWrite && options.ServiceName.Length == 0)
        {
            // SPELLED THE WAY IT IS TYPED, and it used to be the name of the action value
            // lower-cased. That worked for exactly as long as every verb was one word: the fourth
            // is SetStartType, which reads "setstarttype" - a word nobody can type, going out in
            // the sentence that teaches somebody how to type it. The same mistake
            // OptionSurface.Spelling exists for, met again one file over.
            //
            // Two keys, because the example has to be a line that works. The shared sentence ends
            // "bws {0} Spooler --dry-run", which for this verb is itself an incomplete command - so
            // answering a missing name would have handed somebody their next mistake.
            Console.Error.WriteLine(Texts.Of(
                WriteCommands.NeedsAStartType(options.Kind)
                    ? "cli.missingServiceName.startType"
                    : "cli.missingServiceName",
                OptionSurface.Spelling(options.Kind)));

            return ExitCode.Usage;
        }

        // THESE TWO STOOD INSIDE THEIR OWN BRANCHES IN Program UNTIL 2026-08-26, WHICH MEANT THEY
        // WERE ASKED AFTER THE MACHINE HAD BEEN READ. That is the one property this whole class
        // exists for: nothing here opens a handle, so a typo is answered before eight hundred
        // entries are enumerated. Both of these commands read the manager first and then said
        // "you left out an argument" - about half a second spent to report a missing word.
        //
        // Nothing about the answers changed. Same two sentences, same code, and the order they are
        // asked in is the order they stood in.
        if (options.Kind == CommandKind.Show && options.ServiceName.Length == 0)
        {
            // The name is required, and its absence is a usage mistake rather than an empty answer -
            // OptionSurface.TakesAName carries the whole of that argument.
            Console.Error.WriteLine(Texts.Of("cli.show.nameMissing"));

            return ExitCode.Usage;
        }

        if (options.Kind == CommandKind.SnapshotDiff)
        {
            // Two files, or one file and the machine. Never one file on its own: that would have
            // to be guessed into meaning something, and the only thing it could mean is the
            // expensive one. --live says it in a word, which is how E1 writes it.
            if (options.Path.Length == 0 || (options.Against.Length == 0 && !options.Live))
            {
                Console.Error.WriteLine(Texts.Of("cli.diff.needsTwoSides"));

                return ExitCode.Usage;
            }

            if (options.Live && options.Against.Length > 0)
            {
                // Three sides to a comparison with two. Refused rather than resolved by picking
                // one, because either choice would silently ignore something the person typed.
                Console.Error.WriteLine(Texts.Of("cli.diff.liveTakesOneFile"));

                return ExitCode.Usage;
            }
        }

        if (WriteCommands.NeedsAStartType(options.Kind))
        {
            // Named as its own mistake rather than folded into "unknown option", because a value
            // left off is not a word nobody knows. That distinction has been paid for twice in this
            // tool already - once for a switch given without its value, once for a subcommand half
            // typed - and this is the third shape of it.
            if (options.StartTypeWord.Length == 0)
            {
                Console.Error.WriteLine(Texts.Of(
                    "cli.missingStartType", options.ServiceName, string.Join(", ", StartTypeWords.All)));

                return ExitCode.Usage;
            }

            if (WriteCommands.Named(options.StartTypeWord) is null)
            {
                // Says the word they wrote rather than "that is not a start type". Somebody who
                // typed "automatik" has read their own line twice by now, and the sentence that
                // repeats it back to them is the one that ends the search.
                Console.Error.WriteLine(Texts.Of(
                    "cli.badStartType", options.StartTypeWord, string.Join(", ", StartTypeWords.All)));

                return ExitCode.Usage;
            }
        }

        if (options.BadTimeout is not null)
        {
            Console.Error.WriteLine(Texts.Of("cli.badTimeout", options.BadTimeout));
            return ExitCode.Usage;
        }

        return null;
    }
}
