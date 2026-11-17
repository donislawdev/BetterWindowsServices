using System.IO;
using System.Reflection;
using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// Which languages this session can open in, and which one it does - `ADR-21` and the settings page,
/// `docs/PROJEKT-USTAWIENIA-20261117.md` (decisions D1-D3 of 2026-11-17).
///
/// <b>Out of Texts, which keeps the words.</b> Texts asks this once, in its static initialiser, before the
/// first sentence exists - so nothing here may ask Texts for one. Only Texts' constants are read, and a
/// constant is compiled in rather than reached through the type.
///
/// <b>Found, never listed</b> - the rule `ADR-21` sets and LanguageGuards holds: a pair like "pl" and "en"
/// written here would make a third language a change in code.
/// </summary>
internal static class Languages
{
    /// <summary>
    /// The name under which a host can pin the language before the first sentence, through the runtime's
    /// configuration - and then the preferences file is not read at all.
    ///
    /// <b>For the test host, and that is the whole reason it exists.</b> Tests assert English sentences, and
    /// they would otherwise follow whatever the person running them chose on the settings page and the
    /// Windows they run on - Polish on the machine this was written on (measured 2026-11-17, collision 1 of
    /// the project). The value still has to be a code of the right shape, so nothing reaches a resource
    /// name or a path through it unchecked.
    /// </summary>
    internal const string Pinned = "Bws.Gui.Texts.Language";

    /// <summary>What <see cref="Pick"/> decided: the code shown, the choice honoured, the choice that could not be.</summary>
    internal sealed record LanguagePick(string Showing, string? Chosen, string? Missing);

    /// <summary>The two-letter code of the language Windows asks this window for.</summary>
    internal static string WindowsAsks => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    /// <summary>
    /// Decides the language from what is known before the first sentence: a pinned code, the code the
    /// preferences file keeps, the code Windows asks for, and the codes that were found.
    ///
    /// <b>A kept code counts only when it was found in this session</b> - decision D3 and rule 25: it comes
    /// from a file anybody running as this account can edit, and it becomes a resource name and a path.
    /// Anything not on the list falls to Windows, and is reported as missing - unless it was not even a code,
    /// which is a hand-edited file and is dropped the way the window's own reader drops it.
    /// </summary>
    internal static LanguagePick Pick(string? pinned, string? kept, string windowsAsks, IReadOnlyList<string> found)
    {
        ArgumentNullException.ThrowIfNull(found);

        if (KeptChoices.Shaped(pinned) is { } pin)
        {
            return new LanguagePick(pin, null, null);
        }

        bool Found(string? code) => code is not null && found.Contains(code, StringComparer.OrdinalIgnoreCase);

        var choice = KeptChoices.Shaped(kept);

        if (Found(choice))
        {
            return new LanguagePick(choice!, choice, null);
        }

        var windows = KeptChoices.Shaped(windowsAsks);

        return new LanguagePick(Found(windows) ? windows! : Texts.Fallback, null, choice);
    }

    /// <summary>
    /// The language codes this session could open in. Every language built into the assembly, and the files
    /// beside the program when this session may read them (Texts.BesideFor - not with administrator rights,
    /// S-9). English is always among them, because it is what the window falls back to. The settings page
    /// offers exactly these.
    /// </summary>
    internal static IReadOnlyList<string> Found() =>
        Found(
            Assembly.GetExecutingAssembly().GetManifestResourceNames(),
            Session.IsElevated() ? [] : BesideFiles());

    /// <summary>The same question with both lists handed in, which is what a test can do.</summary>
    internal static IReadOnlyList<string> Found(IEnumerable<string> resources, IEnumerable<string> besideFiles)
    {
        var codes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { Texts.Fallback };

        // ONLY CODES OF THE SHAPE A CHOICE CAN BE KEPT IN (KeptChoices.Shaped) - a file called gui.pt-BR.json
        // beside the program is one this window never asks for, because Windows names a language by two
        // letters, and offering it would offer a choice the file could not keep.
        foreach (var name in resources)
        {
            if (Between(name, Texts.Prefix, StringComparison.Ordinal) is { } code)
            {
                codes.Add(code);
            }
        }

        foreach (var file in besideFiles)
        {
            if (Between(Path.GetFileName(file), "gui.", StringComparison.OrdinalIgnoreCase) is { } code)
            {
                codes.Add(code);
            }
        }

        return [.. codes];
    }

    /// <summary>
    /// The code between a prefix and ".json", in the shape a choice can be kept in, or nothing. LONGER THAN
    /// BOTH OR NOTHING - "gui.json" starts with "gui." and ends with ".json", and slicing between them throws.
    /// </summary>
    private static string? Between(string name, string prefix, StringComparison comparison) =>
        name.Length > prefix.Length + Texts.Suffix.Length && name.StartsWith(prefix, comparison) && name.EndsWith(Texts.Suffix, comparison)
            ? KeptChoices.Shaped(name[prefix.Length..^Texts.Suffix.Length])
            : null;

    /// <summary>
    /// The language files beside the program, or none when the folder cannot be listed.
    ///
    /// <b>A folder that cannot be listed offers nothing and says nothing here</b>, and that is not the
    /// silence rule 8 forbids: nothing is read from it either, so the window shows what it shows anyway -
    /// the languages built in - and the settings page lists exactly those. The one case worth a sentence, a
    /// translation beside the program an administrator's window refused, is Texts.BesideNotRead's.
    /// </summary>
    private static string[] BesideFiles() => BesideFiles(Path.Combine(AppContext.BaseDirectory, "languages"));

    /// <summary>
    /// The language files in one folder - the half of <see cref="BesideFiles()"/> a test can reach. Asked of
    /// the folder beside the program only in a session without administrator rights (S-9), and the machines
    /// that run the suites have them, so without this seam the listing would run in none of them.
    /// </summary>
    internal static string[] BesideFiles(string folder)
    {
        try
        {
            return Directory.Exists(folder) ? Directory.GetFiles(folder, "gui.*" + Texts.Suffix) : [];
        }
        catch (Exception refused) when (refused is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
