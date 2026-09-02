// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.IO;
using System.Text.RegularExpressions;

namespace Bws.Gui.Tests;

/// <summary>
/// Every sentence the window can say has somewhere to be said, and every place asks for a
/// sentence that exists.
///
/// <b>This guard was promised by a comment and did not exist.</b> `Texts.Keys` carries the words
/// "for a guard that wants to check the markup against them" - which is the shape this project
/// calls out elsewhere as worse than saying nothing: a document promising protection that is not
/// there, exactly where somebody relies on it.
///
/// <b>What it caught the day it was written, and this is why the rule is not tidiness.</b> Two
/// declared strings reached no screen. One of them was
/// <c>"Running without administrator rights, so Windows hands over fewer entries than it has."</c>
/// - a sentence about the list being INCOMPLETE, which is the one thing rule 8 forbids a window
/// to keep quiet about. It had been written, translated and shipped, and never rendered.
///
/// <b>Unreachable text is not dead weight - it is pressure on the neighbouring text to say it
/// instead.</b> The remedy is to connect it or remove it, never to leave it.
///
/// <b>False alarm estimate, as `docs/06` requires before building a guard:</b> measured on the
/// day it was written - 59 declared keys, 2 unreferenced, both real. Zero referenced-but-missing.
/// So the expected noise is nil and every hit from here on is a real one until somebody shows
/// otherwise.
/// </summary>
public sealed class TextKeyGuards
{
    /// <summary>
    /// The two shapes in which a key is actually asked for: through the loader in code, and
    /// through a resource reference in markup.
    ///
    /// <b>Narrowed after the first version cried wolf on its own first run</b>, which is the
    /// thing `docs/06` asks to be estimated before a guard is built rather than discovered
    /// after. A pattern matching anything key-shaped also matches the language file's own NAME
    /// where a comment mentions it - so the guard reported `gui.en.json` as a missing string.
    /// Asking for the two real shapes cannot be fooled by prose.
    ///
    /// <b>What it therefore does not catch, said rather than left to be found:</b> a key
    /// travelling as a variable rather than sitting inside the call. When this guard was first run
    /// it reported the six keys of <see cref="ListState"/>, across its four calls to Say - written
    /// an hour earlier, in this same session, by the same hand that had just written "every call
    /// passes a literal" into this comment. The keys were moved into their calls rather than the
    /// pattern being widened, because a key visible where it is chosen is better for a reader too.
    ///
    /// So the guard fails in the safe direction: an indirect key is reported as unused, which is
    /// a red test rather than a silent gap.
    /// </summary>
    /// <remarks>
    /// <b>A THIRD SHAPE ARRIVED WITH THE CLICKABLE FILTERS, 2026-08-11, AND IT IS A SHAPE RATHER
    /// THAN A WIDENING.</b> A chip is built from a key, and the key is a literal sitting where
    /// somebody chooses it - <c>new FilterChip("gui.filter.stopped", ...)</c> - but it reaches
    /// the loader through a field, so the first pattern cannot see it.
    ///
    /// The precedent above was to move keys into their calls rather than widen the pattern, and
    /// that was right for <c>ListState</c>, where the key could simply be written at the call.
    /// Here it cannot: the chip needs the key before it needs the words, because the words are
    /// read again whenever the language changes. So the constructor is named exactly, which is
    /// still a real place a key is asked for and still cannot be fooled by prose mentioning one.
    ///
    /// <b>A FOURTH SHAPE ARRIVED WITH THE CONFIGURABLE COLUMNS, 2026-08-11, AND IT IS THE SAME
    /// SHAPE AS THE THIRD.</b> A column carries the key for its heading, which is also what the
    /// picker calls it, and it reaches the loader through a property rather than at a call site -
    /// for the same reason a chip does: the identifier is fixed and the words are read again in
    /// whatever language the machine is set to.
    ///
    /// It anchors on the PROPERTY NAME rather than on a constructor, and that is the better of the
    /// two anchors: <c>LabelKey = "gui.column.status"</c> can only be written where a label key is
    /// being assigned, so no sentence mentioning a key can be mistaken for one.
    ///
    /// <b>A FIFTH SHAPE ARRIVED WITH THE GROUPED FILTERS, 2026-08-12, AND THE PRECEDENT DECIDED
    /// IT.</b> A facet carries the key for its own name and reaches the loader through a field,
    /// exactly as a chip does and for the same reason - the grouping is fixed and the words are
    /// read again whenever the language changes. So it gets a named anchor rather than a wider
    /// pattern, which is what this file has done every time and why it still cannot be fooled by a
    /// comment that mentions a key.
    /// </remarks>
    private static readonly Regex[] Mentioned =
    [
        new(@"Texts\.Of\(\s*""(gui\.[^""]+)""", RegexOptions.Compiled, TimeSpan.FromSeconds(5)),
        new(@"\{\s*DynamicResource\s+(gui\.[^}\s]+)\s*\}", RegexOptions.Compiled, TimeSpan.FromSeconds(5)),
        new(@"new FilterChip\(\s*""(gui\.[^""]+)""", RegexOptions.Compiled, TimeSpan.FromSeconds(5)),
        new(@"new FilterGroup\(\s*""(gui\.[^""]+)""", RegexOptions.Compiled, TimeSpan.FromSeconds(5)),
        new(@"LabelKey\s*=\s*""(gui\.[^""]+)""", RegexOptions.Compiled, TimeSpan.FromSeconds(5)),

        // A SIXTH SHAPE, 2026-08-12: a key held as a NAMED CONSTANT. The column picker's four
        // headings are chosen once, in a map from column to heading, and reach the loader through
        // that map - so no call site carries the text. The constant's declaration is where somebody
        // chooses the key, which is the same thing this file has anchored on five times already,
        // and a constant declaration cannot be a sentence mentioning a key.
        new(@"const string \w+ = ""(gui\.[^""]+)""", RegexOptions.Compiled, TimeSpan.FromSeconds(5)),

        // A SEVENTH, 2026-08-12: an example query carries the key for its own question, exactly as
        // a chip carries the key for its label and for exactly the same reason. Named rather than
        // widened, which is now the seventh time this file has answered the question that way.
        new(@"new QueryExample\(\s*""(gui\.[^""]+)""", RegexOptions.Compiled, TimeSpan.FromSeconds(5)),

        // AN EIGHTH, 2026-08-19, AND IT IS THE THIRD SHAPE THAT IS REALLY THE SAME SHAPE. A scope
        // switch position carries the key for its own label and reaches the loader through a field,
        // for the reason a chip and a facet both do: which positions exist is fixed, and the words
        // are read again in whatever language the machine is set to.
        //
        // The eighth time this file has been asked to widen the pattern and the eighth time it has
        // not. A pattern loose enough to see any string starting with "gui." would also see one
        // inside a comment, and this file's whole worth is that a sentence MENTIONING a key is not
        // a screen showing it.
        new(@"new ScopeChoice\(\s*""(gui\.[^""]+)""", RegexOptions.Compiled, TimeSpan.FromSeconds(5)),

        // A NINTH, 2026-09-02, AND IT IS THE SECOND ARGUMENT OF THE EIGHTH RATHER THAN A NEW PLACE.
        // Backlog 273: the three scope positions were the only pressable controls in the window
        // carrying no tooltip of their own, so each one gained a key for what it LEAVES OUT beside
        // the key for what it says.
        //
        // THE FIRST ATTEMPT DERIVED IT - Texts.Of(labelKey + ".hint") - AND THIS GUARD REPORTED ALL
        // THREE, CORRECTLY. A key assembled at run time is a key nothing can find by reading, which
        // is the failure this file exists to make loud. The repair was to write it where somebody
        // chooses it, which is the same answer given eight times above, and this pattern reads that
        // second literal rather than widening the first.
        new(
            @"new ScopeChoice\(\s*""gui\.[^""]+""\s*,\s*""(gui\.[^""]+)""",
            RegexOptions.Compiled,
            TimeSpan.FromSeconds(5))
    ];

    [Fact]
    public void Every_sentence_the_window_can_say_is_said_somewhere()
    {
        var orphans = Declared().Where(key => !Used().Contains(key)).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            orphans.Count == 0,
            "These strings are declared and never reach a screen. Text with no audience is not "
            + "harmless - it is pressure on the sentence beside it to say what this one was for. "
            + "Connect it or take it out:"
            + Environment.NewLine + string.Join(Environment.NewLine, orphans));
    }

    /// <summary>
    /// The other direction, and it fails differently: a missing key does not throw, it renders as
    /// the key itself. So the window shows <c>gui.status.read</c> to a person, and nothing on the
    /// way there objects.
    /// </summary>
    [Fact]
    public void Every_place_that_asks_for_a_sentence_asks_for_one_that_exists()
    {
        var declared = Declared();
        var missing = Used().Where(key => !declared.Contains(key)).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            missing.Count == 0,
            "These keys are asked for and not declared. A missing key is shown to the person as "
            + "the key, which is a fault nothing else in this product would report:"
            + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    /// <summary>
    /// Without this the two guards above pass perfectly on an empty language file and an empty
    /// source tree - which is the state every guard starts in.
    /// </summary>
    [Fact]
    public void Both_sides_were_actually_read()
    {
        Assert.True(Declared().Count > 20, $"Only {Declared().Count} strings were found in the language file.");
        Assert.True(Used().Count > 20, $"Only {Used().Count} keys were found in the code and markup.");
    }

    /// <summary>Every key the language file declares, read out of the file rather than from the loader.</summary>
    private static HashSet<string> Declared()
    {
        var path = Path.Combine(SourceTree.Root(), "src", "Bws.Gui", "Resources", "gui.en.json");

        return Regex
            .Matches(File.ReadAllText(path), @"""(gui\.[^""]+)""\s*:", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Every key anything in the product mentions.
    ///
    /// The language file itself is left out for the obvious reason, and so is anything under obj
    /// or bin - a stale generated copy of the markup would answer for a file nobody edits.
    /// </summary>
    private static HashSet<string> Used()
    {
        var source = Path.Combine(SourceTree.Root(), "src");

        var files = Directory
            .EnumerateFiles(source, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);

            foreach (var shape in Mentioned)
            {
                foreach (Match match in shape.Matches(text))
                {
                    used.Add(match.Groups[1].Value);
                }
            }
        }

        return used;
    }
}
