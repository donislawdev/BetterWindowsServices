using System.IO;
using System.Reflection;
using System.Text;
using Bws.Gui;

namespace Bws.Gui.Tests;

/// <summary>
/// How the window picks a language, and what happens when the one it picks is incomplete or absent.
///
/// <b>Written 2026-08-10, and until that day none of this had ever been executed by anything.</b>
/// `ADR-21` decided the whole mechanism at S6a - languages discovered rather than listed, English
/// underneath, a file droppable beside the program - and every word of it lived in a comment.
/// `TextKeyGuards` next door checks that keys exist and are used, which is a different question
/// entirely: it would stay green while the window showed English to somebody who asked for Polish.
///
/// <b>The most valuable assertion here is the dullest one</b> - that the English file is really
/// embedded under the name the loader asks for. A file called <c>gui.&lt;code&gt;.json</c> is taken
/// by MSBuild for a satellite resource and compiled into a separate assembly with the code stripped
/// out of the name. The build stays green and the window comes up showing keys instead of words.
/// Two attributes in the project file prevent it, and every language added later needs them too.
/// </summary>
public sealed class LanguageGuards
{
    [Fact]
    public void The_english_file_is_embedded_under_the_name_the_loader_asks_for()
    {
        // The MSBuild trap, guarded at its narrowest point. Nothing else in this suite notices
        // when WithCulture goes missing, because every other test reads Texts through a key and
        // an unknown key comes back AS ITSELF - so the window full of keys and the test suite
        // full of keys agree with each other perfectly.
        var names = typeof(Texts).Assembly.GetManifestResourceNames();

        Assert.Contains("Bws.Gui.Resources.gui.en.json", names);
    }

    [Fact]
    public void The_window_says_words_rather_than_keys()
    {
        // The other half of the same trap, from the outside. A key that comes back as itself is
        // what a broken embed looks like on screen.
        var title = Texts.Of("gui.window.title");

        Assert.NotEqual("gui.window.title", title);
    }

    [Fact]
    public void A_language_nobody_ships_falls_back_to_english()
    {
        var strings = Texts.Assemble("xx", Only("en", @"{""a"": ""English""}"), None);

        Assert.Equal("English", strings["a"]);
    }

    [Fact]
    public void An_unfinished_translation_shows_a_sentence_rather_than_a_key()
    {
        // The rule `ADR-21` calls out by name: an unfinished translation should be usable, not a
        // punishment. Two keys in English, one of them translated.
        var strings = Texts.Assemble(
            "pl",
            Pair("en", @"{""a"": ""English A"", ""b"": ""English B""}", "pl", @"{""a"": ""Polskie A""}"),
            None);

        Assert.Equal("Polskie A", strings["a"]);
        Assert.Equal("English B", strings["b"]);
    }

    [Fact]
    public void A_file_dropped_beside_the_program_is_read()
    {
        // The half of `ADR-21` that lets a translation arrive without anybody compiling anything.
        // Nothing is built in for this code, so the outside file is the only thing there is.
        var strings = Texts.Assemble("pl", Only("en", @"{""a"": ""English""}"), Only("pl", @"{""a"": ""Polskie""}"));

        Assert.Equal("Polskie", strings["a"]);
    }

    [Fact]
    public void A_built_in_language_wins_over_a_file_dropped_beside_the_program()
    {
        // `ADR-21` left this precedence open and the code answered it. Asserted rather than left
        // to be discovered, because the consequence is not obvious and somebody will hit it: a
        // shipped translation CANNOT be corrected by dropping a file next to the executable.
        var strings = Texts.Assemble(
            "pl",
            Pair("en", @"{""a"": ""English""}", "pl", @"{""a"": ""Wbudowane""}"),
            Only("pl", @"{""a"": ""Z katalogu obok""}"));

        Assert.Equal("Wbudowane", strings["a"]);
    }

    [Fact]
    public void English_is_never_asked_for_twice()
    {
        // A machine set to English must not go looking for an "en" translation to lay on top of
        // the English it already has. Cheap to get wrong, invisible when wrong, and it would put
        // a pointless file read on the path of every start on an English machine.
        var asked = new List<string>();

        Texts.Assemble(
            "en",
            code => { asked.Add(code); return Text(@"{""a"": ""English""}"); },
            code => { asked.Add(code); return null; });

        Assert.Equal(["en"], asked);
    }

    [Fact]
    public void A_translation_file_that_is_not_there_leaves_english_standing()
    {
        // The degenerate case, and the one a real machine reaches most often: somebody's Windows
        // is set to a language nobody has translated yet, and neither lookup finds anything.
        var strings = Texts.Assemble("de", Only("en", @"{""a"": ""English""}"), None);

        Assert.Equal("English", strings["a"]);
    }

    private static Func<string, Stream?> None => _ => null;

    private static Func<string, Stream?> Only(string code, string json) =>
        asked => string.Equals(asked, code, StringComparison.Ordinal) ? Text(json) : null;

    private static Func<string, Stream?> Pair(string first, string firstJson, string second, string secondJson) =>
        asked => string.Equals(asked, first, StringComparison.Ordinal) ? Text(firstJson)
            : string.Equals(asked, second, StringComparison.Ordinal) ? Text(secondJson)
            : null;

    private static Stream Text(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));
}
