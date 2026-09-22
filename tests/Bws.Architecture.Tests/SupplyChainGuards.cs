// Explicit for the same reason SourceTree.cs says so at the top of itself: these guards read
// files off disk, and the implicit using set is not something to depend on across projects.
using System.IO;
using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// The build refuses a package that has a published advisory, and keeps refusing it.
///
/// <b>Why this needs a guard when nothing is broken.</b> Because nothing being broken is the
/// whole problem. Measured on 2026-09-22 by asking MSBuild for the effective values rather than
/// by recalling the documentation, this project already audited its packages: <c>NuGetAudit</c>
/// was true, <c>NuGetAuditMode</c> was <c>all</c> and <c>NuGetAuditLevel</c> was <c>low</c> on
/// every project asked. Not one of those was written anywhere in this repository. They were the
/// SDK's defaults - which is to say somebody else's decision, arriving with the toolchain, and
/// one of them has already moved once: the mode was direct-only before .NET 9. A default that
/// moves back takes a gate away and reports nothing, because there is nothing to report.
///
/// <b>What makes the setting bite, and it is not obvious.</b> NuGet reports an advisory as
/// NU1901 to NU1904, and those are restore WARNINGS. <c>TreatWarningsAsErrors</c> covers NU
/// codes, so the two settings together are what stops a restore. Measured the same day with a
/// throwaway project carrying a package with a known high-severity advisory:
///
///   with TreatWarningsAsErrors: <c>error NU1903</c>, restore exits 1
///   without it:                 <c>warning NU1903</c>, restore exits 0
///
/// So these two settings are one gate wearing two names, and either one removed on its own
/// leaves a build that looks identical and checks nothing. That is why the second test below
/// reads a property that has nothing to do with supply chains at first glance.
///
/// <b>What this does NOT check, so that a green run is not read as more than it is.</b> It
/// reads the file and asks what it SAYS. It does not ask what MSBuild evaluated, it does not
/// restore anything, and it therefore cannot see an SDK that starts ignoring these properties
/// or a feed that stops carrying vulnerability data. Measuring the effect needs a real restore
/// against a real advisory and a network, which is <c>tools/supply-chain/audit-blocks.ps1</c> -
/// it builds the throwaway project described above, runs restore twice and reports both exit
/// codes. Run that after an SDK upgrade. This class is what runs on every push.
///
/// <b>And it says nothing about the rest of the supply chain.</b> Whether a dependency may be
/// distributed at all is a licence question, answered on pull requests by
/// <c>.github/scripts/dependency_gate.py</c>. Whether the source itself holds a dangerous shape
/// is answered by <c>.github/workflows/security.yml</c>. Whether a package is merely old is
/// <c>.github/dependabot.yml</c>. Four different questions, and this one is the narrowest.
/// </summary>
public sealed class SupplyChainGuards
{
    /// <summary>
    /// One file, above every project, because a local copy would quietly win and there would be
    /// two sources for one setting - the sentence that file already makes about everything else
    /// in it.
    /// </summary>
    private const string Shared = "Directory.Build.props";

    /// <summary>
    /// What has to be stated, and the value it has to be stated as.
    ///
    /// <c>all</c> rather than <c>direct</c> is the entry worth defending. Seven of this
    /// project's packages are named in a project file and the resolved graph behind them is an
    /// order of magnitude larger, so direct-only would audit the short list and skip the long
    /// one - which is where an advisory is actually likely to sit.
    ///
    /// <c>low</c> rather than a higher floor because this tool runs as an administrator on
    /// somebody's production machine and stops their services. There is no severity of
    /// dependency problem that is beneath asking a question about here.
    /// </summary>
    private static readonly (string Property, string Value)[] Required =
    [
        ("NuGetAudit", "true"),
        ("NuGetAuditMode", "all"),
        ("NuGetAuditLevel", "low"),

        // Not a supply chain setting by its name, and half of this gate by its effect. See the
        // measurement in the summary above.
        ("TreatWarningsAsErrors", "true")
    ];

    [Fact]
    public void The_build_is_told_to_refuse_a_package_with_a_published_advisory()
    {
        var text = File.ReadAllText(Path.Combine(SourceTree.Root(), Shared));
        var missing = new List<string>();

        foreach (var (property, value) in Required)
        {
            var element = new Regex(
                $"<{property}>\\s*(?<value>[^<]*)</{property}>",
                RegexOptions.IgnoreCase,
                Sources.Ceiling);

            var match = element.Match(text);

            if (!match.Success)
            {
                missing.Add($"  {property} is not stated at all, so its value is the SDK's");
                continue;
            }

            var stated = match.Groups["value"].Value.Trim();

            if (!string.Equals(stated, value, StringComparison.OrdinalIgnoreCase))
            {
                missing.Add($"  {property} says '{stated}' and has to say '{value}'");
            }
        }

        Assert.True(
            missing.Count == 0,
            $"{Shared} no longer tells the build to stop on a package with a published "
            + "advisory. These four settings are one gate: the three NuGetAudit properties "
            + "decide what is looked for, and TreatWarningsAsErrors is what turns NU1901 to "
            + "NU1904 from a line in a log into a restore that exits 1. Removing any one of "
            + "them leaves a build that looks identical and checks nothing:"
            + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void Nothing_quietly_excuses_the_warnings_that_carry_an_advisory()
    {
        // The other direction, and the cheaper way to lose this gate. Nobody will delete the
        // settings above - they are commented and they look deliberate. What somebody will do,
        // on an afternoon when a bump is inconvenient, is put NU1903 into a NoWarn on one
        // project. The build stays green, the advisory stays in the product, and the four
        // settings above still read exactly as they do today.
        var advisoryCodes = new Regex(@"NU19\d\d", RegexOptions.IgnoreCase, Sources.Ceiling);

        var excuses = BuildFiles()
            .Select(file => (File: file, Text: File.ReadAllText(file)))
            .Where(pair => Excusing(pair.Text).Any(advisoryCodes.IsMatch))
            .Select(pair => "  " + Path.GetRelativePath(SourceTree.Root(), pair.File).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            excuses.Count == 0,
            "An advisory warning is excused in these files. NU1901 to NU1905 are how NuGet "
            + "reports a package with a published vulnerability and a feed that cannot answer "
            + "the question at all, and silencing one of them is silencing the gate rather "
            + "than tidying a build. If a particular advisory really has been read and "
            + "accepted, that decision belongs in a document and in the backlog, where a "
            + "person can find it later - not in a NoWarn that looks like housekeeping:"
            + Environment.NewLine + string.Join(Environment.NewLine, excuses));
    }

    /// <summary>
    /// The values of every element that can take a warning code out of the build.
    ///
    /// Three names rather than one, because they are three different ways to the same place:
    /// NoWarn silences the warning, WarningsNotAsErrors demotes it back from an error, and
    /// MSBuildWarningsAsMessages is the blunt one that works on anything.
    /// </summary>
    private static IEnumerable<string> Excusing(string text)
    {
        foreach (var element in new[] { "NoWarn", "WarningsNotAsErrors", "MSBuildWarningsAsMessages" })
        {
            var pattern = new Regex(
                $"<{element}>(?<value>[^<]*)</{element}>",
                RegexOptions.IgnoreCase,
                Sources.Ceiling);

            foreach (Match match in pattern.Matches(text))
            {
                yield return match.Groups["value"].Value;
            }
        }
    }

    /// <summary>
    /// Every file that can carry an MSBuild property for this product.
    ///
    /// The whole tree rather than src/ alone, and that is deliberate: a test project pulling a
    /// vulnerable package in is the same problem wearing a different hat, and this repository
    /// has six of them against three shipped projects.
    /// </summary>
    private static IEnumerable<string> BuildFiles() =>
        new[] { "*.csproj", "*.props", "*.targets" }
            .SelectMany(pattern => Directory.EnumerateFiles(SourceTree.Root(), pattern, SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            // tools/ is outside version control and outside the product, and its probe projects
            // are throwaway. A guard reading them would fail a clone that has no tools directory
            // at all, which is every clone but this one.
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}tools{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
}
