using System.Text.Json;
using Bws.Tests;

namespace Bws.Cli.Tests;

/// <summary>
/// The licence command answers about THIS program, and cannot swallow a switch on the way.
///
/// <b>Two different worries, and the second one is why this file exists at all.</b> The first is
/// ordinary: the notice has to name what this executable carries and nothing it does not. The
/// second is an ordering trap - <c>Immediate</c> answers BEFORE what somebody typed is judged,
/// because help and version are questions about the tool rather than about a command. A verb
/// answered in that position would print its answer and drop every switch beside it, which is
/// precisely the silence the belonging table in <c>OptionSurface</c> exists to end.
///
/// So the guard below walks the WHOLE option surface rather than naming a switch or two. A tenth
/// option added next year is covered on the day it is added, without anybody remembering this
/// file - and that is the difference between a guard and a note.
/// </summary>
public sealed class LicenceCommandTests
{
    private const string Register = "packaging/components.json";

    [Fact]
    public void The_notice_names_the_licence_and_where_the_full_text_is()
    {
        var notice = Licence.Answer(components: false);

        Assert.Contains("GPL-3.0-or-later", notice, StringComparison.Ordinal);

        // The warranty disclaimer is not decoration. Sections 15 and 16 of the GPL ask for it to
        // be shown, and a tool asking for administrator rights is exactly where somebody should
        // read it.
        Assert.Contains("NO WARRANTY", notice, StringComparison.Ordinal);
        Assert.Contains("LICENSE", notice, StringComparison.Ordinal);
        Assert.Contains("THIRD-PARTY-NOTICES.md", notice, StringComparison.Ordinal);
    }

    [Fact]
    public void It_names_every_component_this_package_ships_and_none_of_the_other_package()
    {
        var full = Licence.Answer(components: true);
        var (mine, theirs) = Components();

        // THE CONSTANT THIS CHECKS IS THE ONE FAILURE THE WHOLE FILE IS ABOUT. Licence carries a
        // literal saying which package of the register this executable is, because there is
        // nothing at runtime to work it out from. Name the wrong one and the answer is a
        // confident inventory of the other program - so both directions are asserted here.
        Assert.NotEmpty(mine);

        foreach (var component in mine)
        {
            Assert.Contains(component, full, StringComparison.Ordinal);
        }

        foreach (var component in theirs)
        {
            Assert.DoesNotContain(component, full, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_component_list_states_a_version_for_every_one_of_them()
    {
        var full = Licence.Answer(components: true);
        var (mine, _) = Components();

        // One "version" line per component, whether the number is a literal from the register or
        // read off the runtime. A component listed without one would be a row somebody has to go
        // and ask about, which is the whole thing this command exists to save them.
        var stated = full.Split('\n').Count(line => line.TrimStart().StartsWith("version", StringComparison.Ordinal));

        Assert.Equal(mine.Count, stated);
    }

    [Fact]
    public void Asking_for_the_licence_is_answered_with_a_code_of_zero()
    {
        var options = CommandLine.Read(["license"]);

        Assert.Equal(CommandKind.License, options.Kind);
        Assert.True(options.NothingWrong);
        Assert.Equal(ExitCode.Ok, Immediate.Answer(options));
    }

    [Fact]
    public void The_components_switch_belongs_to_it()
    {
        var options = CommandLine.Read(["license", "--components"]);

        Assert.True(options.Components);
        Assert.True(options.NothingWrong);
        Assert.Equal(ExitCode.Ok, Immediate.Answer(options));
    }

    [Fact]
    public void No_option_belonging_to_another_verb_can_be_swallowed_by_it()
    {
        // Every switch this tool has, asked of this verb. The ones that belong to it are expected
        // to be taken - every other one has to leave a complaint behind, because Immediate hands
        // the line back to Refusals the moment there is one - and Refusals is where the sentence
        // naming the verbs it DOES work with is written.
        var swallowed = new List<string>();

        foreach (var (option, verbs) in OptionSurface.Surface)
        {
            if (verbs.Contains(CommandKind.License))
            {
                continue;
            }

            var options = CommandLine.Read(["license", option]);

            if (options.NothingWrong || Immediate.Answer(options) is not null)
            {
                swallowed.Add(option);
            }
        }

        Assert.True(
            swallowed.Count == 0,
            "These options were accepted on `license` and did nothing, which is a switch somebody "
            + "typed being dropped in silence: " + string.Join(", ", swallowed));
    }

    [Fact]
    public void A_word_it_has_no_room_for_is_not_quietly_ignored()
    {
        var options = CommandLine.Read(["license", "GPL"]);

        Assert.False(options.NothingWrong);
        Assert.Null(Immediate.Answer(options));
        Assert.Contains("GPL", options.Extra, StringComparer.Ordinal);
    }

    /// <summary>
    /// The component names the register puts in this package, and the ones it puts only in the
    /// other. Read from the file rather than listed here: a list in a test is a second register.
    /// </summary>
    private static (IReadOnlyList<string> Mine, IReadOnlyList<string> Theirs) Components()
    {
        var path = Path.Combine(SourceTree.Root(), Register.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(path), $"There is no {Register}, which is what this program answers from.");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var mine = new List<string>();
        var theirs = new List<string>();

        foreach (var entry in document.RootElement.GetProperty("components").EnumerateArray())
        {
            var packages = entry.GetProperty("in").EnumerateArray().Select(value => value.GetString()).ToList();
            var name = entry.GetProperty("name").GetString()!;

            if (packages.Contains("cli", StringComparer.Ordinal))
            {
                mine.Add(name);
            }
            else
            {
                theirs.Add(name);
            }
        }

        return (mine, theirs);
    }
}
