using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// Guards everything AROUND the analysers, which the analysers cannot see: that the rules chosen
/// for this project are still switched on, that nothing turns them off for one project, and that
/// every place the code steps around them is counted.
///
/// Added 2026-09-23 on the owner's decision, after a Rust project of his that guards clippy the
/// same way. Its argument transfers word for word: <b>a check that has vanished cannot fail to
/// announce itself.</b> Deleting a line from .editorconfig turns a red build green and leaves the
/// build looking exactly as guarded as before. So the configuration is recorded twice - there,
/// and here - and the two must agree.
///
/// <b>Growing any list here is free: add the line in both places.</b> Shrinking one reddens, and
/// that is the point. Dropping a rule is a decision, and this is where it gets made on purpose.
///
/// <b>What this does NOT prove, said before anybody leans on it:</b> that an analyser actually
/// runs. It proves nothing configured has been switched off and the package that carries the MA
/// rules is still referenced. A rule that fires on nothing in the tree is indistinguishable here
/// from a rule that is not running, which is the same limit the Rust guard names about itself.
/// </summary>
public sealed class AnalyzerRuleGuards
{
    /// <summary>
    /// Every setting in .editorconfig, exactly: section, key and value, inline comment removed.
    ///
    /// The WHOLE file rather than the sections naming .cs, and the first draft of this guard had that
    /// hole: a severity written under <c>[*]</c>, or under <c>[*.{cs,xaml}]</c>, applies to C# as much as
    /// one under <c>[*.cs]</c> does, and a filter on the section name would have read straight past it.
    ///
    /// MA0051 - method too long - is <c>none</c> since 2026-09-23, and not because it was wrong. It
    /// counted every line of a method body, comments included, which in a project whose sources are
    /// sixty-three per cent explanation made it a ceiling on explaining. Method length is held in
    /// lines of code by <see cref="CodeShapeGuards"/> now, together with the three axes MA0051 never
    /// had. Two rules on one axis would redden for different reasons on the same edit.
    /// </summary>
    private static readonly string[] Recorded =
    [
        "(top) root = true",
        "[*] charset = utf-8",
        "[*] end_of_line = crlf",
        "[*] insert_final_newline = true",
        "[*] trim_trailing_whitespace = true",
        "[*] indent_style = space",
        "[*] indent_size = 4",
        "[*.{md,yml,yaml,json}] indent_size = 2",
        "[*.{md,yml,yaml,json}] trim_trailing_whitespace = false",
        "[*.cs] csharp_style_namespace_declarations = file_scoped:warning",
        "[*.cs] csharp_style_var_when_type_is_apparent = true:suggestion",
        "[*.cs] dotnet_style_readonly_field = true:warning",
        "[*.cs] dotnet_style_require_accessibility_modifiers = for_non_interface_members:warning",
        "[*.cs] dotnet_naming_rule.private_fields_underscore.severity = warning",
        "[*.cs] dotnet_naming_rule.private_fields_underscore.symbols = private_fields",
        "[*.cs] dotnet_naming_rule.private_fields_underscore.style = underscore_camel",
        "[*.cs] dotnet_naming_symbols.private_fields.applicable_kinds = field",
        "[*.cs] dotnet_naming_symbols.private_fields.applicable_accessibilities = private",
        "[*.cs] dotnet_naming_style.underscore_camel.required_prefix = _",
        "[*.cs] dotnet_naming_style.underscore_camel.capitalization = camel_case",
        "[*.cs] dotnet_diagnostic.CA1031.severity = warning",
        "[*.cs] dotnet_diagnostic.CA2000.severity = warning",
        "[*.cs] dotnet_diagnostic.CA1304.severity = warning",
        "[*.cs] dotnet_diagnostic.CA1305.severity = warning",
        "[*.cs] dotnet_diagnostic.CA1307.severity = warning",
        "[*.cs] dotnet_diagnostic.CA1310.severity = warning",
        "[*.cs] dotnet_diagnostic.CA1849.severity = warning",
        "[*.cs] dotnet_diagnostic.CA2007.severity = warning",
        "[*.cs] dotnet_diagnostic.CA2016.severity = warning",
        "[*.cs] dotnet_diagnostic.MA0002.severity = none",
        "[*.cs] dotnet_diagnostic.MA0048.severity = none",
        "[*.cs] dotnet_diagnostic.MA0006.severity = none",
        "[*.cs] dotnet_diagnostic.MA0023.severity = none",
        "[*.cs] dotnet_diagnostic.MA0004.severity = none",
        "[*.cs] dotnet_diagnostic.MA0016.severity = none",
        "[*.cs] dotnet_diagnostic.MA0047.severity = none",
        "[*.cs] dotnet_diagnostic.MA0008.severity = none",
        "[*.cs] dotnet_diagnostic.MA0051.severity = none",
        "[*.cs] dotnet_diagnostic.MA0009.severity = warning",
        "[*.cs] dotnet_diagnostic.MA0011.severity = warning",
        "[*.cs] dotnet_diagnostic.IDE0051.severity = warning",
        "[*.cs] dotnet_diagnostic.IDE0052.severity = warning",
        "[*.cs] dotnet_diagnostic.IDE0060.severity = warning",
        "[*.cs] dotnet_diagnostic.IDE0005.severity = warning",
        "[*.cs] dotnet_diagnostic.CS1591.severity = none",
        "[*.cs] dotnet_diagnostic.CS1573.severity = none",
        "[tests/**.cs] dotnet_diagnostic.CA2007.severity = none",
    ];

    /// <summary>
    /// Every way the code steps around the compiler, counted by rule, measured 2026-09-23. Exact,
    /// like every number the shape guards hold: fewer means lower it, more means somebody decided
    /// to silence something and this is where that decision is written down.
    ///
    /// CA1031 - catching everything - is also held file by file in <see cref="BroadCatchGuards"/>,
    /// which says WHERE each one is allowed. This says HOW MANY, which that guard cannot: a second
    /// broad catch in a file already on its list passes it and moves this count.
    /// </summary>
    private static readonly Dictionary<string, int> Escapes = new(StringComparer.Ordinal)
    {
        ["#pragma warning disable CA1031"] = 10,
        ["#pragma warning disable SYSLIB0057"] = 1,
    };

    /// <summary>
    /// MSBuild properties and items that turn an analyser off, weaken it, move a warning out of the
    /// gate, or load a configuration this guard never reads.
    ///
    /// The last three came from review on 2026-09-23, and each is a way round this guard rather than
    /// round the analyser. <c>CodeAnalysisTreatWarningsAsErrors</c> set to false leaves every CA rule a
    /// plain warning while <c>TreatWarningsAsErrors</c> still says true. The two items load an analyser
    /// configuration from a file of ANY name, so the search for a second .editorconfig by name below
    /// would never see it.
    /// </summary>
    private static readonly string[] Switches =
    [
        "RunAnalyzers", "RunAnalyzersDuringBuild", "EnableNETAnalyzers", "AnalysisLevel", "AnalysisMode",
        "CodeAnalysisRuleSet", "WarningLevel", "EnforceCodeStyleInBuild",
        "CodeAnalysisTreatWarningsAsErrors", "GlobalAnalyzerConfigFiles", "EditorConfigFiles",
        "PublishDocumentationFile", "PublishReferencesDocumentationFiles",
    ];

    /// <summary>
    /// What <see cref="Shared"/> has to say, each with what goes quiet the day it stops saying it.
    /// These are the switches above that one file sets for all of them, and no other file may.
    ///
    /// <b>The two publish lines joined on 2026-09-23</b>, with the documentation file IDE0005 needs:
    /// the file was measured going out with the release the moment it existed, which nobody decided,
    /// and nothing but this would notice it doing so again.
    ///
    /// <b>The documentation file itself is NOT here, and the first draft of this summary said it had
    /// to be.</b> It claimed a project switching the file off would switch IDE0005 off with it,
    /// without a word. The mutation run that day tried exactly that and the build refused -
    /// <c>error EnableGenerateDocumentationFile</c>, the SDK's own - so the SDK already says the
    /// word, louder than a test, and a second guard on the same edit would only redden twice.
    /// </summary>
    private static readonly Dictionary<string, string> SharedMustSay = new(StringComparer.Ordinal)
    {
        ["<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>"] = "the style rules in .editorconfig stop failing the build",
        ["<PublishDocumentationFile>false</PublishDocumentationFile>"] = "each executable's documentation file goes out with the release",
        ["<PublishReferencesDocumentationFiles>false</PublishReferencesDocumentationFiles>"] =
            "Bws.Core.xml goes out with the release beside both executables",
    };

    /// <summary>
    /// A setting and a section header, exactly as the compiler's own parser reads them - Roslyn's
    /// AnalyzerConfig, read in its source on 2026-09-23 rather than recalled. Both <c>=</c> and <c>:</c>
    /// separate a key from its value, and a comment starts at the first <c>#</c> or <c>;</c> after it.
    /// The first draft of this guard split on <c>=</c> alone, so <c>severity: none</c> on a line below
    /// the recorded one would have overridden it in the build with this test still green.
    /// </summary>
    private static readonly Regex Setting = new(@"^\s*([\w\.\-_]+)\s*[=:]\s*(.*?)\s*([#;].*)?$", RegexOptions.None, Sources.Ceiling);

    private static readonly Regex Header = new(@"^\s*\[(([^#;]|\\#|\\;)+)\]\s*([#;].*)?$", RegexOptions.None, Sources.Ceiling);

    private const string Shared = "Directory.Build.props";

    [Fact]
    public void The_analyser_configuration_is_exactly_what_was_chosen()
    {
        var (found, unreadable) = Settings(File.ReadAllLines(Path.Combine(SourceTree.Root(), ".editorconfig")));
        var lost = Recorded.Except(found, StringComparer.Ordinal).ToArray();
        var gained = found.Except(Recorded, StringComparer.Ordinal).ToArray();

        // The same key twice in one section: the later line wins in the build, whatever the first
        // says. Keys compared the way the compiler compares them, which lowercases every key.
        var twice = found
            .GroupBy(setting => setting[..setting.IndexOf(" = ", StringComparison.Ordinal)].ToLowerInvariant(), StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.True(
            lost.Length == 0 && gained.Length == 0 && twice.Length == 0 && unreadable.Length == 0,
            ".editorconfig no longer says what was chosen for C#. Dropping a rule is a decision - make it here as " +
            "well as there. A new one is free, record it here too, that is what makes it stick." +
            Environment.NewLine + "Gone from .editorconfig:" + Environment.NewLine + string.Join(Environment.NewLine, lost) +
            Environment.NewLine + "Not recorded here:" + Environment.NewLine + string.Join(Environment.NewLine, gained) +
            Environment.NewLine + "Set twice, so the later one wins:" + Environment.NewLine + string.Join(Environment.NewLine, twice) +
            Environment.NewLine + "Lines the compiler ignores without a word:" + Environment.NewLine + string.Join(Environment.NewLine, unreadable));
    }

    [Fact]
    public void The_build_file_scan_reads_every_project_in_the_solution()
    {
        // The canary for the list above and for SupplyChainGuards, which read the same one. A scan
        // of build files that finds none passes both, and review found a way it could: a filter on
        // the absolute path turned a checkout beneath any folder called tools into an empty list.
        var solution = File.ReadAllText(Path.Combine(SourceTree.Root(), "BetterWindowsServices.slnx"));
        var projects = Regex.Matches(solution, "Path=\"(?<path>[^\"]+\\.csproj)\"", RegexOptions.None, Sources.Ceiling)
            .Select(match => match.Groups["path"].Value.Replace('\\', '/'))
            .ToArray();
        var read = Sources.BuildFiles().Select(CodeShape.NameOf).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missed = projects.Where(project => !read.Contains(project)).ToArray();

        Assert.True(
            projects.Length >= 9 && missed.Length == 0 && read.Contains(Shared),
            $"The build file scan read {read.Count} files and the solution lists {projects.Length} projects. Missing " +
            $"from the scan: {string.Join(", ", missed)}. {Shared} read: {read.Contains(Shared)}. Every guard reading " +
            "this list passes on an empty one.");
    }

    [Fact]
    public void Nothing_switches_an_analyser_off_for_one_project()
    {
        var problems = Sources.BuildFiles().SelectMany(file => Weakenings(file, File.ReadAllText(file))).ToList();

        var shared = File.ReadAllText(Path.Combine(SourceTree.Root(), Shared));
        problems.AddRange(SharedMustSay
            .Where(said => !Contains(shared, said.Key))
            .Select(said => $"  {Shared} no longer says {said.Key}, so {said.Value}"));

        if (!Contains(shared, "<PackageReference Include=\"Meziantou.Analyzer\""))
        {
            problems.Add($"  {Shared} no longer references Meziantou.Analyzer, so every MA rule in .editorconfig is configured and nothing runs it");
        }

        // A second .editorconfig lower in the tree, or a .globalconfig anywhere, can set a severity
        // this guard never reads - and the nearer file wins.
        problems.AddRange(OtherConfigurations().Select(file => $"  {file}: a second analyser configuration, which can override the one recorded here"));

        Assert.True(problems.Count == 0, "Something weakens the analysers:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Every_way_around_the_compiler_is_counted()
    {
        var found = CodeShape.Shipped.Files.Concat(CodeShape.Testing.Files).SelectMany(file => file.Escapes).ToArray();
        var counted = found.GroupBy(escape => escape.Rule, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var wrong = counted.Keys.Union(Escapes.Keys, StringComparer.Ordinal)
            .Where(rule => counted.GetValueOrDefault(rule) != Escapes.GetValueOrDefault(rule))
            .Select(rule => $"  {rule}: {counted.GetValueOrDefault(rule)} in the code, {Escapes.GetValueOrDefault(rule)} recorded - " +
                string.Join(", ", found.Where(escape => escape.Rule == rule).Select(escape => escape.Where)))
            .ToArray();

        Assert.True(
            wrong.Length == 0,
            "The count of places that step around the compiler has moved. Fewer: lower the number here in the same " +
            "change. More: a warning was silenced, and that is a decision to write down here, with the reason beside " +
            "the directive in the code:" + Environment.NewLine + string.Join(Environment.NewLine, wrong));
    }

    /// <summary>
    /// Every setting in .editorconfig as "[section] key = value", with "(top)" for the lines above the
    /// first section, read with the compiler's own two patterns - so a colon separates like an equals
    /// sign and a trailing comment is dropped. Also every line the compiler would skip in silence:
    /// neither blank, nor a comment, nor a section, nor a setting.
    /// </summary>
    internal static (string[] Settings, string[] Unreadable) Settings(IEnumerable<string> lines)
    {
        var settings = new List<string>();
        var unreadable = new List<string>();
        var section = "(top)";
        foreach (var raw in lines.Select(line => line.Trim()).Where(line => line.Length > 0 && line[0] is not ('#' or ';')))
        {
            if (Header.Match(raw) is { Success: true } header)
            {
                section = $"[{header.Groups[1].Value}]";
            }
            else if (Setting.Match(raw) is { Success: true } setting)
            {
                settings.Add($"{section} {setting.Groups[1].Value} = {setting.Groups[2].Value}");
            }
            else
            {
                unreadable.Add($"  {section} {raw}");
            }
        }

        return ([.. settings], [.. unreadable]);
    }

    private static IEnumerable<string> Weakenings(string file, string text)
    {
        var name = CodeShape.NameOf(file);
        foreach (var property in Switches.Where(property => !(name == Shared && SharedMustSay.Keys.Any(said => said.StartsWith($"<{property}>", StringComparison.Ordinal)))))
        {
            if (Regex.IsMatch(text, $"<{property}\\w*[\\s>]", RegexOptions.IgnoreCase, Sources.Ceiling))
            {
                yield return $"  {name}: sets {property}";
            }
        }

        // NoWarn and WarningsNotAsErrors carrying anything but an advisory code. The advisory codes
        // are SupplyChainGuards' to refuse, and a finding reported by two guards is noise.
        foreach (Match element in Regex.Matches(text, "<(NoWarn|WarningsNotAsErrors)[^>]*>(?<value>[^<]*)</", RegexOptions.IgnoreCase, Sources.Ceiling))
        {
            var codes = element.Groups["value"].Value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (codes.Any(code => !code.StartsWith("NU19", StringComparison.OrdinalIgnoreCase) && !code.StartsWith("$(", StringComparison.Ordinal)))
            {
                yield return $"  {name}: {element.Groups[1].Value} silences {element.Groups["value"].Value.Trim()}";
            }
        }
    }

    /// <summary>
    /// Analyser configurations other than the root .editorconfig. Asked of the file system by name, so
    /// the walk does not open every file in the tree. Build output is left out - the SDK writes its own
    /// generated configuration there on every build - and so are tools/ and the release staging, which
    /// no project in the solution reads.
    /// </summary>
    private static IEnumerable<string> OtherConfigurations()
    {
        // Hidden files are NOT skipped, which is the default: the compiler reads a hidden
        // .editorconfig exactly like a visible one.
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System };
        string[] outside = ["tools/", ".git/", "dist/", "build/", "artifacts/"];

        return new[] { ".editorconfig", "*.globalconfig" }
            .SelectMany(pattern => Directory.EnumerateFiles(SourceTree.Root(), pattern, options))
            .Select(CodeShape.NameOf)
            .Where(name => name != ".editorconfig")
            .Where(name => !outside.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .Where(name => !name.Contains("/obj/", StringComparison.Ordinal) && !name.Contains("/bin/", StringComparison.Ordinal));
    }

    private static bool Contains(string text, string fragment) => text.Contains(fragment, StringComparison.Ordinal);
}
