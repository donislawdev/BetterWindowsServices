// Explicit for the same reason SourceTree.cs says so at the top of itself: these guards read
// files off disk, and the implicit using set is not something to depend on across projects.
using System.IO;
using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// Every action a workflow runs is named by a commit, not by a tag.
///
/// <b>What the difference is, because it does not look like much in a diff.</b>
/// <c>uses: actions/checkout@v4</c> and <c>uses: actions/checkout@11d5960...</c> read almost the
/// same and are not the same promise. A tag is a label somebody else can move. A commit is the
/// bytes. So a tag means "run whatever that project calls v4 at the moment our job starts", and
/// the moment that project's release process is taken over, every repository pinned to the tag
/// runs the new bytes with its own token, on its next push, without anybody changing a line.
/// That has happened to widely used actions more than once, which is why
/// <c>.github/dependabot.yml</c> holds a new release back for a week before proposing it - and
/// a cooldown on versions is worth nothing if the reference does not name a version at all.
///
/// <b>Why it needed a guard rather than care.</b> Every action here was already pinned when this
/// was written, by hand, and nothing said so. Replacing one SHA with <c>@v4</c> passed the build,
/// the tests, the scanner and the licence gate - the whole pipeline - because none of them reads
/// workflow files for this. The one place the difference shows up is a dialog nobody opens.
///
/// <b>It also pays for a skip made somewhere else, and that is the sharper reason it exists.</b>
/// <c>.github/scripts/dependency_gate.py</c> passes over the <c>actions</c> ecosystem when it
/// checks licences, on the grounds that an action is CI machinery that never reaches a user.
/// That is true, and it leaves actions checked by nothing at all unless something else asks a
/// stricter question of them. This is that question.
///
/// <b>What this does NOT check.</b> That the SHA belongs to the version in the comment beside
/// it - a comment is prose and nothing here verifies prose. That the action is trustworthy, or
/// that its code has been read. That a SHA still exists in the upstream repository. And nothing
/// about a workflow's behaviour: this reads text, so a workflow that pins everything perfectly
/// and does something foolish passes here exactly like a good one.
/// </summary>
public sealed class WorkflowGuards
{
    /// <summary>
    /// A SHA-1 commit identifier written in full. Git accepts shorter prefixes and this does
    /// not: an abbreviation is still a commit rather than a label, but it is one that can turn
    /// ambiguous as the upstream repository grows, and there is no reason to accept it here.
    /// </summary>
    private static readonly Regex PinnedToACommit =
        new("^[0-9a-f]{40}$", RegexOptions.None, Sources.Ceiling);

    /// <summary>
    /// The two ways to name an action that lives in THIS repository, which is already exactly
    /// as pinned as the commit being built and therefore has nothing to pin.
    ///
    /// <b><c>$/</c> was missing until a review pointed at it, and the first reaction here was
    /// that it had been invented.</b> It has not: the workflow syntax reference calls it the
    /// self repository reference and presents it as the RECOMMENDED form - "Using an action in
    /// the same repository as the workflow at the running commit (recommended)", written
    /// <c>$/path/to/action</c>. Read there on 2026-09-22 rather than recalled, after nearly
    /// dismissing it. There are no local actions in this repository today, so this branch was
    /// about to be wrong in a way nothing would have caught until the first one was added - and
    /// then it would have looked like the guard working.
    /// </summary>
    private static readonly string[] InThisRepository = ["./", "$/"];

    private static bool IsLocal(string action) =>
        InThisRepository.Any(prefix => action.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>
    /// A `uses:` line, split into what is used and what follows the reference.
    ///
    /// The trailing group is deliberate. A bare SHA says nothing to a person reading the file -
    /// forty hex characters are not a version - so the convention in this repository is a
    /// comment naming the release, and the second test below holds it. Dependabot writes and
    /// updates that comment when it moves the pin, so it is not something anybody maintains by
    /// hand after the first time.
    /// </summary>
    private static readonly Regex UsesLine =
        new(@"^\s*-?\s*uses:\s*(?<action>[^\s#]+)\s*(?<rest>.*)$", RegexOptions.None, Sources.Ceiling);

    [Fact]
    public void Every_action_a_workflow_runs_is_named_by_a_commit_rather_than_a_tag()
    {
        var loose = new List<string>();
        var pinned = 0;

        foreach (var (file, line, action, _) in Uses())
        {
            // A local action is a path into this repository, so it is already exactly as pinned
            // as the commit being built. There are none today; the branch is here so that adding
            // one is not blocked by a guard about somebody else's releases.
            if (IsLocal(action))
            {
                continue;
            }

            var at = action.LastIndexOf('@');
            var reference = at < 0 ? string.Empty : action[(at + 1)..];

            if (!PinnedToACommit.IsMatch(reference))
            {
                loose.Add($"  {file}:{line}  {action}");
            }
            else
            {
                pinned++;
            }
        }

        // A sweep that read nothing finds nothing, and reports it in the same green as a sweep
        // that read everything. The floor is a floor rather than a count, so it survives
        // workflows being added and split - what it refuses is the day the file pattern, the
        // directory or the `uses:` pattern stops matching and this guard quietly becomes a test
        // over an empty list. The same argument PublicSurfaceGuards makes about its own reach.
        Assert.True(
            pinned >= 5,
            $"This guard found only {pinned} pinned action(s), and this repository has more than "
            + "that. It is reading the wrong files or the wrong lines, so its green result means "
            + "nothing. Check .github/workflows and the patterns in this class.");

        Assert.True(
            loose.Count == 0,
            "These actions are named by something a stranger can move. A tag or a branch means "
            + "the job runs whatever that project publishes under that name at the moment it "
            + "starts, with this repository's token - which is exactly the shape the cooldown in "
            + ".github/dependabot.yml exists to slow down, and a cooldown on versions buys "
            + "nothing when the reference names no version. Use the full 40 character commit and "
            + "put the release in a comment beside it:"
            + Environment.NewLine + string.Join(Environment.NewLine, loose));
    }

    [Fact]
    public void A_pinned_action_says_which_release_it_is()
    {
        // Forty hex characters tell a reader nothing. Every pin in this repository carries the
        // version beside it, which is what makes a diff from Dependabot readable as "v4.4.0 to
        // v4.5.0" rather than as two rows of noise - and what lets somebody decide whether a
        // bump is routine without leaving the file.
        var silent = Uses()
            .Where(u => !IsLocal(u.Action))
            .Where(u => u.Trailing.TrimStart().StartsWith('#') is false)
            .Select(u => $"  {u.File}:{u.Line}  {u.Action}")
            .ToList();

        Assert.True(
            silent.Count == 0,
            "These pins carry no comment saying which release they are. The commit is what runs "
            + "and the comment is what a person reads, so a pin without one is a line nobody can "
            + "review. Dependabot writes and maintains this comment once it is there:"
            + Environment.NewLine + string.Join(Environment.NewLine, silent));
    }

    /// <summary>Every `uses:` line in every workflow, with where it was found.</summary>
    private static IEnumerable<(string File, int Line, string Action, string Trailing)> Uses()
    {
        var workflows = Path.Combine(SourceTree.Root(), ".github", "workflows");

        foreach (var file in Directory.EnumerateFiles(workflows, "*.yml").Order(StringComparer.Ordinal))
        {
            var lines = File.ReadAllLines(file);

            for (var index = 0; index < lines.Length; index++)
            {
                var match = UsesLine.Match(lines[index]);

                if (match.Success)
                {
                    yield return (
                        Path.GetFileName(file),
                        index + 1,
                        match.Groups["action"].Value,
                        match.Groups["rest"].Value);
                }
            }
        }
    }
}
