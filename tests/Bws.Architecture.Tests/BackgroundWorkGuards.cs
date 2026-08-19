using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// Keeps the two ways of losing an exception on a background path down to a list somebody
/// wrote on purpose.
///
/// The same shape as <see cref="BroadCatchGuards"/>, and for the same reason: this is a rule
/// that cannot be kept by remembering it. Both patterns below are legal C#, neither produces
/// a warning, and both are the ordinary way of getting a compiler to stop complaining.
///
/// <b>What they cost when they are wrong.</b> An exception thrown out of an <c>async void</c>
/// method has nowhere to go - it is raised on whatever context the method started on, and in
/// a window that ends the process. There is no catch to add higher up, because there is no
/// higher up. A task started and never awaited is the same failure with a delay: nobody looks
/// at it, so the work either silently did not happen or its failure silently did not happen.
///
/// This project is now exactly the shape where that bites. The window runs a reading every
/// second on a timer, unattended, while its owner is somewhere else - which is the difference
/// between a crash somebody sees and a program that was open this morning and is not now.
///
/// <b>False alarm estimate, as 04-PLAN-PRAC requires before building a guard:</b> at the
/// moment it was written the whole product held one <c>async void</c> - an overridden event
/// handler, which is the one case the language has no alternative for - and no unawaited
/// task at all. So one entry on the list and nothing else. Every hit from here on is a real
/// question until somebody shows otherwise.
/// </summary>
public sealed class BackgroundWorkGuards
{
    /// <summary>
    /// Where an <c>async void</c> is allowed, and why, one line each.
    ///
    /// Adding a file here is the deliberate act. The list is short because the legitimate
    /// case is narrow: overriding a handler the framework declares as returning nothing.
    /// </summary>
    private static readonly Dictionary<string, string> AllowedVoid = new(StringComparer.Ordinal)
    {
        ["MainWindow.xaml.cs"] =
            "The overridden key handler, and the language has no alternative: the framework " +
            "declares it as returning nothing, so there is no task to hand back. Everything it " +
            "awaits reports its own failures into the window. It shared this entry with " +
            "OnClosing until 2026-08-19, when the size ratchet moved that one to its own file - " +
            "and the reason it is now TWO entries rather than one is that this list is keyed by " +
            "file name, so a split silently leaves the moved code covered by a permission " +
            "written about a file it no longer lives in.",

        ["MainWindow.Carrying.cs"] =
            "OnClosing, which the framework also declares as returning nothing. It has to await " +
            "a run in progress before letting the window go - without it, closing the window " +
            "mid-run ends the process and leaves a cascade switched off with nothing printed, " +
            "which is the failure the command line pays three levels of Ctrl+C for. Guarded " +
            "since 2026-08-19 by CarryingGuards, which until then had no way to put a run into " +
            "this window without starting a real one."
    };

    /// <summary>
    /// An <c>async void</c> that is not an event handler override. Matches the declaration
    /// rather than a call, because that is where the decision is made.
    /// </summary>
    private static readonly Regex AsyncVoid = new(
        @"\basync\s+void\s+\w+\s*\(", RegexOptions.Compiled, Sources.Ceiling);

    /// <summary>
    /// Work started and thrown away: a discard on something awaitable, or a call to a method
    /// whose name ends in Async standing alone as a statement.
    ///
    /// The second half is the one that matters. <c>_ = SomethingAsync();</c> at least says out
    /// loud that a result is being dropped. A bare <c>SomethingAsync();</c> looks exactly like
    /// calling a method, and the compiler says nothing.
    /// </summary>
    private static readonly Regex Abandoned = new(
        @"(^\s*_\s*=\s*\w[\w\.]*Async\s*\()|(^\s*(?!return\b|await\b)\w[\w\.]*Async\s*\([^;]*\)\s*;)",
        RegexOptions.Compiled | RegexOptions.Multiline,
        Sources.Ceiling);

    [Fact]
    public void Every_async_void_is_somewhere_that_argued_for_one()
    {
        var found = Matching(AsyncVoid);
        var strangers = found.Where(file => !AllowedVoid.ContainsKey(file)).ToArray();

        Assert.True(
            strangers.Length == 0,
            "An async void appeared somewhere that has not argued for one. An exception thrown " +
            "out of it has no catch to reach and ends the process, so either this is a handler " +
            "the framework forces and it belongs in the list with its reason, or it should " +
            "return a task:" +
            Environment.NewLine + string.Join(Environment.NewLine, strangers));
    }

    [Fact]
    public void The_list_does_not_name_places_that_stopped_having_one()
    {
        // The other direction, and the one that lets a list rot into a wish. An entry naming a
        // file that no longer has one reads as an argument still being made.
        var found = Matching(AsyncVoid);
        var gone = AllowedVoid.Keys.Where(file => !found.Contains(file)).ToArray();

        Assert.True(
            gone.Length == 0,
            "These files are listed as needing an async void and no longer have one. Remove " +
            "them, so the list keeps meaning what it says:" +
            Environment.NewLine + string.Join(Environment.NewLine, gone));
    }

    [Fact]
    public void No_shipped_code_starts_work_and_walks_away_from_it()
    {
        // No allowed list at all, because there is no case for it here. Everything this
        // product does in the background is a reading it then shows, so there is always
        // somebody to hand the result to - and a reading nobody waits for is a reading whose
        // failure nobody sees.
        var found = Matching(Abandoned);

        Assert.True(
            found.Count == 0,
            "Work is being started and abandoned. A task nobody awaits carries its failure " +
            "away with it, and the work either did not happen or did not finish, silently " +
            "either way:" +
            Environment.NewLine + string.Join(Environment.NewLine, found));
    }

    private static HashSet<string> Matching(Regex pattern)
    {
        var files = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Sources.Shipped())
        {
            if (pattern.IsMatch(File.ReadAllText(file)))
            {
                files.Add(Path.GetFileName(file));
            }
        }

        return files;
    }
}
