using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// The first of the project's untouchable rules, as a test rather than as a sentence: every write
/// goes through a plan - `ADR-11`.
///
/// <b>Backlog 34, built 2026-08-18 as the precondition for bundle 2.</b> Until now the rule held on
/// an accident of arithmetic: there was exactly ONE construction of the writer in the whole product
/// and the window did not know the interface existed. Nothing said so. The bundle that puts
/// operations in the window is precisely the slice where that erodes, one "just for now" at a time,
/// and the notes name this rule as the one that erodes by a single exception per session.
///
/// <b>A guard BEFORE the code, which is the whole reason it is worth writing today.</b> Written
/// after the window can already stop a service, it would be a test asserting whatever happened to
/// be built. Written now it is a constraint on what gets built, and the difference is not
/// decoration: the first version of the operations slice will want a direct call and this is what
/// refuses it.
///
/// <b>Why the source rather than the assembly.</b> LayeringGuards reads compiled metadata because
/// its question is about references between assemblies, which is exactly what metadata records. The
/// question here is narrower than a reference - the window may perfectly well reference the core,
/// and what must not happen is one particular constructor being called in one particular place. In
/// IL that is a call instruction inside a method body, and the reader those guards share does not
/// go that deep. The text does answer it, and the two guards below are written so that the ways of
/// evading them are the ways somebody would have to be trying.
/// </summary>
public sealed class PlanOnlyGuards
{
    /// <summary>
    /// Where the writer may be built, and why, one line each.
    ///
    /// <b>Adding a file here is the deliberate act</b>, and it is a conversation with the owner
    /// rather than an edit - rule 1 is on the untouchable list. The legitimate case is narrow:
    /// handing the writer to the thing that carries out a plan.
    /// </summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.Ordinal)
    {
        ["Execution.cs"] =
            "The command line's one composition point. It hands the writer straight to a " +
            "PlanRunner and keeps no reference of its own, which is what the second guard below " +
            "checks rather than takes on trust.",

        ["Carrying.cs"] =
            "The window's one composition point, added 2026-08-18 on the owner's decision when " +
            "the window learned to carry a plan out. Same shape as the line above: built and " +
            "handed straight to a runner, no reference kept. Named Carrying rather than " +
            "Execution BECAUSE OF THIS LIST - the keys below are bare file names, so a second " +
            "Execution.cs would overwrite the command line's entry and leave one permission " +
            "standing for two files."
    };

    /// <summary>
    /// The writer is built in one place and handed straight to the thing that runs a plan.
    ///
    /// Matched on the constructor rather than on the type name, because naming the type is
    /// ordinary and harmless - a comment, a using, a test double's declaration - while calling its
    /// constructor is the act that produces something able to change the machine.
    /// </summary>
    [Fact]
    public void The_writer_is_only_ever_built_where_a_plan_will_carry_it()
    {
        var found = Builders();
        var strangers = found.Keys.Where(file => !Allowed.ContainsKey(file)).ToArray();

        Assert.True(
            strangers.Length == 0,
            "Something builds WindowsScmControl outside the one place that hands it to a plan. " +
            "Rule 1 of the project's untouchable rules: every write produces a plan, which can be " +
            "shown, turned into a command line, reversed or carried out. A write that skips it " +
            "has no dry run and no undo, and --dry-run showing something other than what runs is " +
            "the worst failure this product has:" +
            Environment.NewLine + string.Join(Environment.NewLine, strangers));
    }

    /// <summary>
    /// The other direction, and the one that lets a list rot into a wish - the same shape
    /// <see cref="BroadCatchGuards"/> uses for the same reason.
    ///
    /// A file listed as a place the writer is built, that no longer builds one, reads as an
    /// argument still being made. Somebody moving the composition point would leave the old
    /// permission standing and the guard would go on allowing a file that had stopped needing it.
    /// </summary>
    [Fact]
    public void The_list_does_not_name_places_that_stopped_building_one()
    {
        var found = Builders();
        var gone = Allowed.Keys.Where(file => !found.ContainsKey(file)).ToArray();

        Assert.True(
            gone.Length == 0,
            "These files are listed as building the writer and no longer do. Remove them, or the " +
            "permission outlives the reason for it:" +
            Environment.NewLine + string.Join(Environment.NewLine, gone));
    }

    /// <summary>
    /// AND EVERY CONSTRUCTION IS HANDED STRAIGHT TO A PLAN RUNNER, which is the half that actually
    /// says "inside the plan".
    ///
    /// <b>The guard above only says WHERE, and where is not the rule.</b> A file on the list could
    /// keep the writer in a field and call it directly, and everything above would stay green - so
    /// on its own that list would be a guard about file names rather than about `ADR-11`.
    ///
    /// What is asserted is that the STATEMENT building the writer also builds a PlanRunner. The
    /// first version demanded the text immediately before it be "new PlanRunner(" and that was too
    /// tight to be right: naming the arguments, or wrapping the line, would have reddened correct
    /// code - a guard that cries at ordinary edits gets suppressed, and then it is not a guard.
    ///
    /// It is a shape rather than a proof, and the limit is said rather than left to be discovered:
    /// somebody determined to write around this can put the construction in a variable on the line
    /// above. The point is not to make it impossible - it is to make it impossible to do BY
    /// ACCIDENT, and to leave a red test in front of anybody doing it on purpose.
    /// </summary>
    [Fact]
    public void Every_writer_that_is_built_goes_straight_into_a_plan_runner()
    {
        var loose = new List<string>();

        foreach (var file in Sources.Shipped())
        {
            var text = File.ReadAllText(file);

            foreach (Match match in Regex.Matches(
                text, @"new\s+WindowsScmControl\s*\(", RegexOptions.None, Sources.Ceiling))
            {
                // The statement around it, which is where a plan runner would have to be built too.
                // Bounded by the punctuation that ends one, so a construction two statements away
                // from a plan is as visible here as one on the other side of the file.
                var opens = text.LastIndexOfAny([';', '{', '}'], match.Index) + 1;
                var closes = text.IndexOfAny([';', '{'], match.Index);
                var statement = text[opens..(closes < 0 ? text.Length : closes)];

                if (!statement.Contains("new PlanRunner", StringComparison.Ordinal))
                {
                    loose.Add($"{Path.GetFileName(file)} at offset {match.Index}");
                }
            }
        }

        Assert.True(
            loose.Count == 0,
            "A WindowsScmControl is built somewhere that does not hand it straight to a PlanRunner. " +
            "Rule 1 says every write goes through a plan - a writer held anywhere else is a write " +
            "waiting to skip one:" +
            Environment.NewLine + string.Join(Environment.NewLine, loose));
    }

    private static Dictionary<string, int> Builders()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var file in Sources.Shipped())
        {
            var matches = Regex.Matches(
                File.ReadAllText(file), @"new\s+WindowsScmControl\s*\(", RegexOptions.None, Sources.Ceiling);

            if (matches.Count > 0)
            {
                counts[Path.GetFileName(file)] = matches.Count;
            }
        }

        return counts;
    }
}
