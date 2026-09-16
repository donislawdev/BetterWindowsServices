using System.IO;
using System.Text.RegularExpressions;

namespace Bws.Gui.Tests;

/// <summary>
/// The words the two interfaces use for one thing, held to the dictionary's decision.
///
/// <b>Point 6 of the review of 2026-09-15, `docs/11` 2.14, and the owner's decision the same day.</b>
/// The window said "Start" on the column, "Start type" on the filter group and "Start type..." on
/// the button - three names for one thing, and `docs/03` part 4 names a fourth: "Startup type",
/// the word services.msc uses. The decision was the window to the dictionary everywhere and the
/// terminal left alone, because its verb is <c>start-type</c> - a contract - so its prose says
/// "start type" the way <c>sc</c> and <c>Get-Service</c> do.
///
/// <b>Prose has no guard, which is why this file exists.</b> A session that finds "start type" in
/// one language file and "startup type" in the other will read it as an inconsistency to repair,
/// and repair it in whichever direction it happens to be looking. Both halves are held here so
/// that the repair has to come through this file and its reason.
/// </summary>
public sealed class DictionaryWordGuards
{
    private static readonly Regex StartType =
        new(@"\bstart type\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(5));

    private static readonly Regex StartupType =
        new(@"\bstartup type\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(5));

    [Fact]
    public void The_window_says_startup_type_the_way_the_dictionary_and_services_msc_do()
    {
        var window = File.ReadAllText(Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "Resources", "gui.en.json"));

        var wrong = window.Split('\n').Where(line => StartType.IsMatch(line)).Select(line => line.Trim()).ToArray();

        Assert.True(
            wrong.Length == 0,
            "The window's language file says \"start type\" where the dictionary says \"Startup type\":"
            + Environment.NewLine + string.Join(Environment.NewLine, wrong));

        // A guard satisfied by a file that never mentions the thing is the absence-shaped pass
        // this project keeps meeting. The column header alone is one mention.
        Assert.True(StartupType.Matches(window).Count >= 5, "the window's language file barely mentions the startup type at all");
    }

    /// <summary>
    /// And the terminal keeps its own word, on purpose - its verb is <c>start-type</c>.
    /// </summary>
    [Fact]
    public void The_terminal_keeps_saying_start_type_because_its_verb_is_start_type()
    {
        var terminal = File.ReadAllText(Path.Combine(SourceTree.Root(), "src", "Bws.Cli", "Resources", "cli.en.json"));

        Assert.True(StartType.IsMatch(terminal), "the terminal stopped saying \"start type\" beside its start-type verb");
        Assert.False(StartupType.IsMatch(terminal), "the terminal says \"startup type\" beside a verb spelled start-type");
    }
}
