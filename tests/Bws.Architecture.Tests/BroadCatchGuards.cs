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
            "One per file it opens - signature, version, hash and publisher. This runs over " +
            "every binary a machine happens to have, none of which we chose, and the ways one " +
            "file can be malformed are not a list anybody finishes. One bad file must cost its " +
            "own answer rather than the other eight hundred.",

        ["MainViewModel.cs"] =
            "The window's first reading. A failure has to arrive as a line under an empty list " +
            "rather than as a dialog nobody can act on, or a window that disappears."
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
            var matches = Regex.Matches(File.ReadAllText(file), @"#pragma warning disable CA1031");

            if (matches.Count > 0)
            {
                counts[Path.GetFileName(file)] = matches.Count;
            }
        }

        return counts;
    }
}
