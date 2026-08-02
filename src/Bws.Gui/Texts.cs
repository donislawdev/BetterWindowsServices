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

    internal static string Of(string key, params object[] values) =>
        string.Format(CultureInfo.CurrentCulture, Of(key), values);

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

    private static Dictionary<string, string> Load()
    {
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);

        // English first, then the chosen language on top of it. A translation that is missing
        // a key falls back to a sentence rather than showing the key - an unfinished
        // translation should be usable, not a punishment.
        using (var fallback = Embedded(Fallback))
        {
            Merge(strings, fallback);
        }

        var wanted = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        if (!string.Equals(wanted, Fallback, StringComparison.OrdinalIgnoreCase))
        {
            using var chosen = Embedded(wanted) ?? Beside(wanted);

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

        return File.Exists(file) ? File.OpenRead(file) : null;
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

        using var document = JsonDocument.Parse(source);

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
