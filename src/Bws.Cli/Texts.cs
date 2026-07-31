using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Bws.Cli;

/// <summary>
/// Every string a person reads, in one place.
///
/// The command line tool ships in English only, by decision: translating it would cost
/// upkeep with nobody to serve. The strings still live outside the code, because text
/// scattered through source cannot be reviewed, and reviewing it is the whole point.
/// Part 5 of the glossary says what good text looks like, and that judgement needs
/// somewhere to look.
///
/// The file is embedded rather than shipped alongside. It removes a way for the tool to
/// arrive broken, and it keeps working when the CLI is published as a single native file.
/// </summary>
internal static class Texts
{
    private const string ResourceName = "Bws.Cli.Resources.cli.en.json";

    private static readonly Dictionary<string, string> Strings = Load();

    /// <summary>
    /// The text for a key. An unknown key returns the key itself rather than throwing:
    /// a missing string should look wrong on screen, not take the tool down mid-run.
    /// </summary>
    internal static string Of(string key) =>
        Strings.TryGetValue(key, out var text) ? text : key;

    internal static string Of(string key, params object[] values) =>
        string.Format(CultureInfo.InvariantCulture, Of(key), values);

    private static Dictionary<string, string> Load()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Naming the alternatives turns "it is missing" into "it is called something else",
        // which is the difference between a puzzle and a fix. Learned the hard way: the
        // first version said only that the resource was absent, and the build was green.
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded text resource '{ResourceName}' is missing. " +
                $"The assembly carries: [{string.Join(", ", assembly.GetManifestResourceNames())}].");

        using var document = JsonDocument.Parse(stream);

        var strings = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            // Anything that is not a plain string is metadata about the file itself.
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                strings[property.Name] = property.Value.GetString()!;
            }
        }

        return strings;
    }
}
