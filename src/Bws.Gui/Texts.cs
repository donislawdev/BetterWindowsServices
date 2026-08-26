using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Bws.Gui;

/// <summary>
/// Every string the window shows, in one place, and never a literal in code.
///
/// Rule 13 of the project notes: user-facing text is a key, and the keys are English too.
/// The window is the half of this product that gets translated - the command line stays
/// English by decision, and its own copy of this lives in Bws.Cli.
///
/// <b>Nowhere here is there a list of languages, and that is `ADR-21` rather than an
/// oversight.</b> Languages are discovered: the ones built in come from the assembly's own
/// resources, and anything in a <c>languages</c> folder beside the executable is picked up
/// as well. The moment a pair like "pl" and "en" appears in code as a pair, adding a third
/// becomes a code change and that decision is quietly dead.
/// </summary>
internal static class Texts
{
    private const string Prefix = "Bws.Gui.Resources.gui.";
    private const string Suffix = ".json";

    /// <summary>
    /// What to fall back to, and the only language code this file may name.
    ///
    /// It is not a choice among languages - it is the one the product is written in, so a
    /// missing translation shows English rather than a key.
    /// </summary>
    private const string Fallback = "en";

    private static readonly Dictionary<string, string> Strings = Load();

    /// <summary>
    /// The text for a key. An unknown key comes back as itself rather than throwing: a
    /// missing string should look wrong on screen, not take the window down while somebody
    /// is using it.
    /// </summary>
    internal static string Of(string key) =>
        Strings.TryGetValue(key, out var text) ? text : key;

    internal static string Of(string key, params object[] values) => Formatted(Of(key), values);

    /// <summary>
    /// One sentence with its values in it, or the sentence untouched when it cannot hold them.
    ///
    /// <b>Separated from <see cref="Of(string, object[])"/> on 2026-08-26 for the reason
    /// <see cref="Assemble"/> was separated from <c>Load</c>: it could not otherwise be reached by
    /// a test at all.</b> The strings come from a dictionary built in a static initialiser, and
    /// every sentence this build ships is held to its arguments by a guard - so there is no key
    /// that can be asked for to produce the case below. Handing the template in is the only way to
    /// exercise the file a translator wrote rather than the file we ship.
    /// </summary>
    internal static string Formatted(string template, params object[] values)
    {
        try
        {
            // THE MACHINE'S CULTURE, AND THE COMMAND LINE USES THE INVARIANT ONE. That difference
            // is deliberate and was nowhere written down until 2026-08-03, which is how an audit
            // came to ask about it.
            //
            // A window shows text to the person sitting at it, on their machine, so a count reads
            // better as they would write it. A terminal writes into pipes, where a thousands
            // separator that appears on one install and not on another is something a script has
            // to cope with. Same product, two audiences, two right answers.
            return string.Format(CultureInfo.CurrentCulture, template, values);
        }
        catch (FormatException)
        {
            // A TRANSLATION WITH A BROKEN HOLE SHOULD LOOK WRONG, NOT TAKE THE WINDOW DOWN - and
            // this is the rule two lines above, applied to the other way the same file can be
            // wrong. A missing key already comes back as itself for exactly this reason.
            //
            // The file this reads can be dropped beside the program by hand - that is `ADR-21`
            // rather than an accident - so "{0" or a {3} where two values are handed over is a
            // thing a translator can write. It throws where it is used, which is inside a handler
            // or a binding, in the middle of somebody's session.
            //
            // The unformatted sentence is what comes back: it still says what it is about, and
            // the braces standing in it are the report that something is wrong with the file.
            return template;
        }
    }

    /// <summary>Every key that was loaded, for a guard that wants to check the markup against them.</summary>
    internal static IReadOnlyCollection<string> Keys => Strings.Keys;

    /// <summary>
    /// Puts every string into a resource dictionary, keyed by its own key.
    ///
    /// This is how text reaches the markup, and it is not the first thing tried. A column
    /// header cannot bind to the view model: columns live outside the visual tree and
    /// inherit no data context, so the obvious binding silently shows nothing. Resources
    /// have no such problem and are resolved by name, which is what a key is.
    /// </summary>
    internal static void PutInto(System.Windows.ResourceDictionary resources)
    {
        foreach (var (key, text) in Strings)
        {
            resources[key] = text;
        }
    }

    private static Dictionary<string, string> Load() =>
        Assemble(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, Embedded, Beside);

    /// <summary>
    /// Builds the strings for one language code, from whatever the two lookups can find.
    ///
    /// <b>Separated from <see cref="Load"/> on 2026-08-10 so that it could be tested at all, and
    /// the reason it had to be is worth keeping.</b> Everything below was decided in `ADR-21`,
    /// written at S6a and never once executed by a test: the language a machine asks for, an
    /// unfinished translation, a file dropped beside the program. The whole mechanism was a
    /// claim in a comment - in a product where a binding has compiled and painted nothing five
    /// times.
    ///
    /// It could not be reached before because the choice lived inside a static initialiser
    /// reading the machine's own culture, so a test could only ever exercise the one language
    /// the machine happened to be set to.
    ///
    /// The two lookups are passed in rather than called directly for the same reason, and their
    /// ORDER IS THE BEHAVIOUR: embedded wins, and a file beside the program is consulted only
    /// when nothing is built in for that code. `ADR-21` left that precedence open and the code
    /// answered it here - so a shipped translation cannot be overridden from outside.
    /// </summary>
    internal static Dictionary<string, string> Assemble(
        string wanted,
        Func<string, Stream?> embedded,
        Func<string, Stream?> beside)
    {
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);

        // English first, then the chosen language on top of it. A translation that is missing
        // a key falls back to a sentence rather than showing the key - an unfinished
        // translation should be usable, not a punishment.
        using (var fallback = embedded(Fallback))
        {
            Merge(strings, fallback);
        }

        if (!string.Equals(wanted, Fallback, StringComparison.OrdinalIgnoreCase))
        {
            using var chosen = embedded(wanted) ?? beside(wanted);

            Merge(strings, chosen);
        }

        return strings;
    }

    /// <summary>A language built into the assembly, or null when there is none.</summary>
    private static Stream? Embedded(string code) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(Prefix + code + Suffix);

    /// <summary>
    /// A language somebody dropped beside the executable. This is the half of `ADR-21` that
    /// lets a translation arrive without anybody compiling anything.
    /// </summary>
    private static Stream? Beside(string code)
    {
        var file = Path.Combine(
            AppContext.BaseDirectory, "languages", "gui." + code + Suffix);

        try
        {
            return File.Exists(file) ? File.OpenRead(file) : null;
        }
        catch (IOException)
        {
            // A FILE THAT CANNOT BE OPENED IS ONE THIS BUILD DOES NOT HAVE A TRANSLATION IN, and
            // until 2026-08-26 it was the window not starting. This is read from a static
            // initialiser, so anything thrown here arrives as a TypeInitializationException at the
            // first mention of Texts - which is in OnStartup, before there is a window to say
            // anything in. What a person got was the runtime's own crash box.
            //
            // Narrow rather than broad, and the two named are the whole list: the file is locked
            // or on a share that went away, or the account may not read it. Both are ordinary
            // facts about somebody's machine, and neither is a reason to have no program.
            //
            // NOTHING IS SAID ABOUT IT, which is a silence this project would normally refuse. The
            // window opens in English and that is the only report there is. Named in the backlog
            // rather than left to be discovered.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads one language file into the dictionary. Does not own the stream.
    ///
    /// Ownership sits with the caller, which is the reverse of how this was first written, and
    /// the change is not cosmetic. Disposing something handed in means every call site has to
    /// know that this one does - and the site that mattered picked between two streams with a
    /// null coalescing operator, where an analyser could no longer tell which of them was going
    /// to be closed. Owning it at the point where it is chosen is the version a reader can
    /// check in one line.
    /// </summary>
    private static void Merge(Dictionary<string, string> into, Stream? source)
    {
        if (source is null)
        {
            return;
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(source);
        }
        catch (JsonException)
        {
            // A LANGUAGE FILE THAT IS NOT ONE LEAVES THE PROGRAM IN ENGLISH RATHER THAN LEAVING
            // NOBODY A PROGRAM - 2026-08-26. `ADR-21` lets anybody drop a file beside the
            // executable, which means a half written or truncated one is a thing that happens, and
            // this ran inside a static initialiser: the throw arrived as a TypeInitializationException
            // at the first mention of Texts, in OnStartup, and the window never appeared.
            //
            // Merging what it read so far is not the answer either. The dictionary starts with
            // English already in it, so returning here leaves every key answered - by the sentence
            // this build ships rather than by half a translation.
            return;
        }

        using (document)
        {
            foreach (var property in document.RootElement.EnumerateObject())
            {
                // Anything that is not a plain string describes the file rather than being
                // one of its strings.
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    into[property.Name] = property.Value.GetString()!;
                }
            }
        }
    }
}
