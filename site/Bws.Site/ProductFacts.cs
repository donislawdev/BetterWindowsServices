using System.Globalization;
using System.Text.RegularExpressions;
using Bws.Core.Querying;

namespace Bws.Site;

/// <summary>
/// What the site says about the program, taken from the program.
///
/// <b>Nothing here is a number somebody typed into a page.</b> This project has paid for a
/// hand-copied figure more than a dozen times, and a website is the worst place for one: it is
/// read by people who cannot check it, and nothing in a build compares prose with code. So the
/// version comes out of Directory.Build.props, the exit codes out of ExitCode.cs, the switches
/// out of OptionSurface.cs, and the query fields and value spellings out of Bws.Core itself.
///
/// <b>Two of those four are read as TEXT and the other two are called, and the difference is
/// not laziness.</b> QueryFields.Names and QueryFields.ValuesOf are public API, meant for
/// "anything that offers them to a person", so the site calls them and cannot drift. ExitCode
/// and OptionSurface are internal to the command line tool and stay internal - a website is not
/// a reason to widen a shipped assembly's surface - so they are read the way
/// tools/audit/audit.ps1 reads them. That way of reading has a known failure: when the
/// declaration shape changes, the regular expression finds nothing and reports nothing wrong.
/// It happened once in this repository, to that script, when the value names moved to another
/// file. Every reader below therefore FAILS when it finds nothing, rather than returning empty.
///
/// What each code and each switch MEANS is not here: meanings are prose, prose is translated,
/// and translations live in site/i18n. The check runs both ways - a code without a sentence is
/// a problem, and a sentence about a code that no longer exists is a problem too.
/// </summary>
internal sealed class ProductFacts
{
    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(2);

    private ProductFacts(string version, IReadOnlyList<ExitCodeFact> exitCodes, IReadOnlyList<SwitchFact> switches, IReadOnlyList<QueryFieldFact> fields)
    {
        Version = version;
        ExitCodes = exitCodes;
        Switches = switches;
        Fields = fields;
    }

    internal string Version { get; }

    internal IReadOnlyList<ExitCodeFact> ExitCodes { get; }

    internal IReadOnlyList<SwitchFact> Switches { get; }

    internal IReadOnlyList<QueryFieldFact> Fields { get; }

    internal static ProductFacts Read(string root) =>
        new(ReadVersion(root), ReadExitCodes(root), ReadSwitches(root), ReadFields());

    private static string ReadVersion(string root)
    {
        var path = Path.Combine(root, "Directory.Build.props");
        var match = Regex.Match(File.ReadAllText(path), @"<Version>([^<]+)</Version>", RegexOptions.None, Ceiling);
        return match.Success
            ? match.Groups[1].Value.Trim()
            : throw new InvalidOperationException($"No <Version> in {path}. The shape of that file changed.");
    }

    /// <summary>
    /// The exit codes, as constants rather than as a list. The names are the ones the code uses -
    /// <c>Ok</c>, <c>Runtime</c>, <c>Usage</c> - and they key the sentences in the language files.
    /// </summary>
    private static IReadOnlyList<ExitCodeFact> ReadExitCodes(string root)
    {
        var path = Path.Combine(root, "src", "Bws.Cli", "ExitCode.cs");
        var found = Regex
            .Matches(File.ReadAllText(path), @"internal const int (?<name>\w+)\s*=\s*(?<value>\d+)\s*;", RegexOptions.ExplicitCapture, Ceiling)
            .Select(match => new ExitCodeFact(
                int.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture),
                match.Groups["name"].Value))
            .OrderBy(code => code.Value)
            .ToList();

        return found.Count > 0
            ? found
            : throw new InvalidOperationException($"No exit codes found in {path}. The declaration shape changed.");
    }

    /// <summary>
    /// The switches and the verbs each one belongs to, from the table the parser itself uses.
    /// The verb names are the enumeration's, mapped to what somebody types - the two differ for
    /// three of them, and the mapping is repeated here because it lives in an internal method.
    /// </summary>
    private static IReadOnlyList<SwitchFact> ReadSwitches(string root)
    {
        var path = Path.Combine(root, "src", "Bws.Cli", "OptionSurface.cs");
        var text = File.ReadAllText(path);

        var found = Regex
            .Matches(text, @"\(""(?<name>--[a-z-]+)"",\s*\[(?<verbs>[^\]]*)\]\)", RegexOptions.ExplicitCapture, Ceiling)
            .Select(match => new SwitchFact(
                match.Groups["name"].Value,
                Regex
                    .Matches(match.Groups["verbs"].Value, @"CommandKind\.(\w+)", RegexOptions.None, Ceiling)
                    .Select(verb => Spelling(verb.Groups[1].Value))
                    .ToList()))
            .ToList();

        return found.Count > 0
            ? found
            : throw new InvalidOperationException($"No switches found in {path}. The declaration shape changed.");
    }

    /// <summary>
    /// The verb as somebody types it. Three of the eight are not the enumeration name lower-cased,
    /// and printing "setstarttype" on a page would be printing a word nobody can type.
    /// </summary>
    private static string Spelling(string kind) => kind switch
    {
        "SnapshotCreate" => "snapshot create",
        "SnapshotDiff" => "snapshot diff",
        "SetStartType" => "start-type",
        _ => kind.ToLowerInvariant(),
    };

    /// <summary>
    /// Every field of the query language and every spelling it accepts, called rather than read.
    /// The three words every field takes - <c>any</c>, <c>none</c> and <c>?</c> - are deliberately
    /// not inside each field's list, so they are carried once, beside the table.
    /// </summary>
    private static IReadOnlyList<QueryFieldFact> ReadFields() =>
        QueryFields.Names
            .Select(name => new QueryFieldFact(name, QueryFields.ValuesOf(name)))
            .ToList();

    internal static IReadOnlyList<string> ReservedWords => QueryFields.ReservedWords;
}

internal sealed record ExitCodeFact(int Value, string Name);

internal sealed record SwitchFact(string Name, IReadOnlyList<string> Verbs);

internal sealed record QueryFieldFact(string Name, IReadOnlyList<string> Values);
