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
        // on an afternoon when a bump is inconvenient, is reach for one of the several
        // documented ways to make the warning go away on one project. The build stays green,
        // the advisory stays in the product, and the four settings above still read exactly as
        // they do today.
        var excuses = BuildFiles()
            .SelectMany(file => Excuses(file, File.ReadAllText(file)))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            excuses.Count == 0,
            "An advisory is excused in these files. Every entry below is a documented way to "
            + "make NuGet's audit stop failing this build, and silencing one is silencing the "
            + "gate rather than tidying a build. If a particular advisory really has been read "
            + "and accepted, that decision belongs in a document and in the backlog, where a "
            + "person can find it later - not in a line that looks like housekeeping:"
            + Environment.NewLine + string.Join(Environment.NewLine, excuses));
    }

    /// <summary>
    /// Every documented way, in a build file, to stop an advisory from failing the build.
    ///
    /// <b>THREE OF THESE FIVE WERE MISSING UNTIL A REVIEW POINTED AT ONE OF THEM</b>, and the
    /// review's own suggestion was narrower than what the documentation turned out to say. Read
    /// on 2026-09-22 from NuGet's own page on auditing packages rather than recalled:
    ///
    ///   <c>NuGetAuditSuppress</c> is an ITEM, not a property, and it names one advisory by
    ///   URL: <c>&lt;NuGetAuditSuppress Include="https://github.com/advisories/GHSA-..." /&gt;</c>.
    ///   It suppresses that advisory completely, for every package that shares it, and NuGet's
    ///   own documentation calls it "a last resort". It carries no NU code at all, so the three
    ///   patterns that were here could never have seen it.
    ///
    ///   <c>NuGetAudit</c>, <c>NuGetAuditMode</c> and <c>NuGetAuditLevel</c> can be set in ANY
    ///   project, and a project's own value wins over the one in Directory.Build.props. So the
    ///   first test in this class - which reads that one shared file - could stay green while a
    ///   single csproj carried <c>&lt;NuGetAudit&gt;false&lt;/NuGetAudit&gt;</c> and audited
    ///   nothing. That hole was nobody's suggestion: it came from reading the documentation
    ///   that the suggestion cited.
    ///
    /// The three warning-code elements now allow attributes, which the earlier patterns did
    /// not: <c>&lt;NoWarn Condition="..."&gt;NU1903&lt;/NoWarn&gt;</c> went straight past them.
    ///
    /// <b>What this still cannot see, and it is worth saying rather than leaving to be found.</b>
    /// NuGet reads <c>NuGetAudit</c> from an ENVIRONMENT VARIABLE as well, which its
    /// documentation suggests outright as a way to turn auditing off on a build server. That
    /// value lives in a workflow file or in a runner's configuration, not in a build file, so
    /// no amount of reading csproj and props files will find it. Backlog row 409.
    /// </summary>
    private static IEnumerable<string> Excuses(string file, string text)
    {
        var name = Path.GetRelativePath(SourceTree.Root(), file).Replace('\\', '/');
        var advisoryCodes = new Regex(@"NU19\d\d", RegexOptions.IgnoreCase, Sources.Ceiling);

        // Silencing the warning, demoting it back from an error, or turning it into a message.
        // `[^>]*` after the element name is what lets an attribute through to the value.
        foreach (var element in new[] { "NoWarn", "WarningsNotAsErrors", "MSBuildWarningsAsMessages" })
        {
            var pattern = new Regex(
                $"<{element}[^>]*>(?<value>[^<]*)</{element}>",
                RegexOptions.IgnoreCase,
                Sources.Ceiling);

            foreach (Match match in pattern.Matches(text))
            {
                var value = match.Groups["value"].Value;

                if (advisoryCodes.IsMatch(value))
                {
                    yield return $"  {name}: {element} carries an advisory code";
                }

                // AND THE VERSION THIS CANNOT READ THROUGH, which a second review pointed at.
                // MSBuild expands properties and items before any of this matters, so
                // <NoWarn>$(AuditWarnings)</NoWarn> silences NU1903 whenever some other line
                // defines AuditWarnings as NU1903 - and a scan for the literal code sees
                // nothing at all. Evaluating MSBuild here is not an option in a unit test, so
                // the indirection itself is refused: in a repository that has never needed one,
                // a property reference inside a warning-control element is either a mistake or
                // the exact thing this guard exists to stop.
                if (value.Contains("$(", StringComparison.Ordinal)
                    || value.Contains("@(", StringComparison.Ordinal))
                {
                    yield return $"  {name}: {element} is built from an MSBuild expression, so what it "
                        + "silences cannot be read from this file";
                }
            }
        }

        // Suppressing one advisory by its URL. No NU code appears anywhere on the line.
        var suppression = new Regex("<NuGetAuditSuppress[\\s>]", RegexOptions.IgnoreCase, Sources.Ceiling);

        if (suppression.IsMatch(text))
        {
            yield return $"  {name}: NuGetAuditSuppress, which excuses one advisory outright";
        }

        // A project overriding what Directory.Build.props states. The shared file is where these
        // three belong and the only place they are allowed, which is what makes the first test
        // in this class worth anything.
        // TreatWarningsAsErrors is on this list and was missing from it until a second review
        // asked why. The class summary says in as many words that these four settings are ONE
        // GATE and that removing any of them leaves a build that looks identical and checks
        // nothing - and then the override check covered three of the four. A project setting
        // <TreatWarningsAsErrors>false</TreatWarningsAsErrors> leaves NU1901 to NU1904 as
        // warnings, restore exits zero, and both tests in this class stay green. That is the
        // same prose-ahead-of-code gap the semgrep gate had, in the file that argues against it.
        if (!string.Equals(name, Shared, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var property in new[] { "NuGetAudit", "NuGetAuditMode", "NuGetAuditLevel", "TreatWarningsAsErrors" })
            {
                var local = new Regex(
                    $"<{property}[^>]*>[^<]*</{property}>",
                    RegexOptions.IgnoreCase,
                    Sources.Ceiling);

                if (local.IsMatch(text))
                {
                    yield return $"  {name}: sets {property} itself, and a project's value wins over {Shared}";
                }
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
