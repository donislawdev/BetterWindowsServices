using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// Counts the places that catch everything, because a comment cannot.
///
/// This guard exists because the same sentence rotted three times. A comment in the command
/// line entry point said "exactly two broad catches in the project", then was corrected to
/// five when three more had appeared, and by the time the window was built it was six. **A
/// number written in prose has nothing counting it**, which is the rule this project states
/// about comments, demonstrated on itself.
///
/// The point is not that broad catches are forbidden. Each one here is deliberate and
/// argued: they exist where the alternative is one bad input costing a whole run, and every
/// one turns the failure into something the person sees. The point is that a **new** one
/// should be a decision somebody makes on purpose, not something that arrives and joins a
/// count nobody maintains.
///
/// <b>ONE BLIND SPOT, NAMED HERE RATHER THAN LEFT FOR SOMEBODY TO TRUST THIS COUNT WITHOUT IT -
/// 2026-08-26.</b> This finds a catch-all by looking for the analyser suppression one needs. The
/// window's dispatcher handler in <c>Mishaps</c> is not a catch block, needs no suppression, and
/// catches strictly MORE than any file listed below - everything thrown on the interface thread
/// that nothing else caught. It is deliberate and argued in its own file, exactly like these are,
/// and it is invisible here.
///
/// <b>Not fixed by widening the pattern, and that is a decision.</b> A guard looking for
/// <c>DispatcherUnhandledException</c> as well would be two shapes in one test, and the second has
/// exactly one legitimate site in a product with one application. Naming it is what a reader needs.
/// </summary>
public sealed class BroadCatchGuards
{
    /// <summary>
    /// Where a broad catch is allowed, and why, one line each.
    ///
    /// Adding a file here is the deliberate act. Failing this test is the question being
    /// asked, and the answer belongs in this list rather than in a number in a comment.
    /// </summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.Ordinal)
    {
        ["Program.cs"] =
            "The entry point of the command line tool. Without it a person gets a stack trace " +
            "instead of a sentence, and the process still ends with a failing code.",

        ["WindowsBinaryInspector.cs"] =
            "One per file it opens - signature, version and hash. This runs over every binary a " +
            "machine happens to have, none of which we chose, and the ways one file can be " +
            "malformed are not a list anybody finishes. One bad file must cost its own answer " +
            "rather than the other eight hundred.",

        ["WindowsBinaryInspector.Publisher.cs"] =
            "The publisher half of the same argument, and it moved here on 2026-09-02 when the " +
            "size ratchet split that file - backlog 303. Reading a name out of a certificate " +
            "meets everything a malformed certificate can be, and the answer to one unreadable " +
            "one is no publisher for that file rather than no listing at all. " +
            "This guard caught the split with the argument left behind, which is the second time " +
            "it has done that: see Readings.cs below.",

        ["Readings.cs"] =
            "The window's first reading. A failure has to arrive as a line under an empty list " +
            "rather than as a dialog nobody can act on, or a window that disappears. " +
            "It was MainViewModel.cs until 2026-08-18, when the state machine was cut out into " +
            "its own file - backlog 198. The argument moved with the code, unchanged.",

        ["Readings.SecondPhase.cs"] =
            "The expensive pass, which moved out of the file above on 2026-09-03 when the size " +
            "ratchet asked - and its catch is BROADER IN PLACE AND NARROWER IN REASON than the " +
            "two it left behind. Every file that pass opens answers for itself: a refusal or a " +
            "malformed binary comes back as a Reading rather than as a throw, so anything " +
            "arriving at the catch is the pass itself failing while the list on screen is already " +
            "good. It must not take the window down, and it must not be reported as the reading " +
            "having failed either, because the reading succeeded. THE THIRD TIME THIS GUARD HAS " +
            "CAUGHT A SEAM WITH THE ARGUMENT LEFT BEHIND, and the second in two days.",

        ["Readings.OneEntry.cs"] =
            "The details panel's own reading of one entry, 2026-09-24, UX-GUI-005 - the same " +
            "argument as the second phase, over one row. Every file answers for itself, so what " +
            "arrives here is the reading failing as a whole. It must not take the window down, and " +
            "it must not leave the lines saying 'not read' as though nobody had tried: the failure " +
            "is kept against the entry it happened to and said by the panel whenever that entry is " +
            "shown. It stood in Chosen.cs for one commit - the review of PR #16 found the failure " +
            "following the panel to the next entry, and it moved here, beside the claims it lives with."
    };

    [Fact]
    public void Every_broad_catch_is_in_a_place_that_argued_for_one()
    {
        var found = Occurrences();
        var strangers = found.Keys.Where(file => !Allowed.ContainsKey(file)).ToArray();

        Assert.True(
            strangers.Length == 0,
            "A broad catch appeared somewhere that has not argued for one. Either the failure " +
            "still reaches the person and this file belongs in the list with its reason, or it " +
            "does not and this is the silence rule 8 forbids:" +
            Environment.NewLine + string.Join(Environment.NewLine, strangers));
    }

    [Fact]
    public void The_list_does_not_name_places_that_stopped_having_one()
    {
        // The other direction, and the one that lets a list rot into a wish. An entry naming a
        // file that no longer catches anything reads as an argument still being made.
        var found = Occurrences();
        var gone = Allowed.Keys.Where(file => !found.ContainsKey(file)).ToArray();

        Assert.True(
            gone.Length == 0,
            "These files are listed as having a broad catch and no longer do. Remove them, so " +
            "the list keeps meaning what it says:" +
            Environment.NewLine + string.Join(Environment.NewLine, gone));
    }

    private static Dictionary<string, int> Occurrences()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var file in Sources.Shipped())
        {
            var matches = Regex.Matches(
                File.ReadAllText(file),
                @"#pragma warning disable CA1031",
                RegexOptions.None,
                Sources.Ceiling);

            if (matches.Count > 0)
            {
                counts[Path.GetFileName(file)] = matches.Count;
            }
        }

        return counts;
    }
}
