namespace Bws.Architecture.Tests;

/// <summary>
/// Guards for the rules in 06-STRUKTURA-I-KONWENCJE, part 2.
///
/// These exist because a rule written in a document is a suggestion. Over enough
/// sessions it erodes one reasonable-looking exception at a time. A rule that turns
/// a build red is a rule.
///
/// They are written before any behaviour exists, because that is the only thing a
/// guard can protect at this stage, and because a guard added later would find the
/// rules already broken.
/// </summary>
public sealed class LayeringGuards
{
    private static readonly string[] UserInterfaceAssemblies =
    [
        "PresentationFramework",
        "PresentationCore",
        "WindowsBase",
        "System.Windows.Forms",
        "System.Xaml"
    ];

    // THE NAMES ASSERTED AGAINST ARE THE ASSEMBLIES' OWN, NOT THE PROJECTS'. Since 2026-09-22
    // Bws.Cli builds bws.dll and Bws.Gui builds BetterWindowsServices.dll, so a reference from the
    // core to the window would be called "BetterWindowsServices" - and DoesNotContain("Bws.Gui")
    // would stay green while the rule it guards was broken. The name comes from the project file
    // through GuardedAssemblies, and the theory below proves it is the one the compiled file carries.
    private static string Cli => GuardedAssemblies.AssemblyNameOf("Bws.Cli");
    private static string Gui => GuardedAssemblies.AssemblyNameOf("Bws.Gui");

    [Fact]
    public void The_core_never_references_a_presentation_project()
    {
        var core = AssemblyFacts.Of("Bws.Core");

        Assert.DoesNotContain(Cli, core.AssemblyReferences);
        Assert.DoesNotContain(Gui, core.AssemblyReferences);
    }

    [Fact]
    public void The_cli_and_the_gui_never_reference_each_other()
    {
        Assert.DoesNotContain(Gui, AssemblyFacts.Of("Bws.Cli").AssemblyReferences);
        Assert.DoesNotContain(Cli, AssemblyFacts.Of("Bws.Gui").AssemblyReferences);
    }

    /// <summary>
    /// The two guards above assert that a name is absent, and an absent name is the easiest thing
    /// in the world to assert: a name nothing could ever carry is absent everywhere. This ties the
    /// name read from the project file to the name written into the compiled assembly, so that a
    /// renamed project, a mistyped AssemblyName or a stale build turns this red instead of
    /// quietly making the other two vacuous.
    /// </summary>
    [Theory]
    [InlineData("Bws.Core")]
    [InlineData("Bws.Cli")]
    [InlineData("Bws.Gui")]
    public void The_name_a_guard_asks_about_is_the_name_the_assembly_carries(string project)
    {
        var facts = AssemblyFacts.Of(project);

        Assert.Equal(GuardedAssemblies.AssemblyNameOf(project), facts.Name);
    }

    [Fact]
    public void The_core_never_references_a_user_interface_assembly()
    {
        var core = AssemblyFacts.Of("Bws.Core");

        var found = UserInterfaceAssemblies
            .Where(core.AssemblyReferences.Contains)
            .ToArray();

        Assert.True(
            found.Length == 0,
            $"Bws.Core links against user interface assemblies: {string.Join(", ", found)}. " +
            "The core must not know that an interface exists (ADR-3).");
    }

    [Fact]
    public void The_core_never_writes_to_the_console()
    {
        var core = AssemblyFacts.Of("Bws.Core");

        // Not a style preference. The core returning data instead of printing it is what
        // keeps the CLI output contract intact: data on standard output, everything else
        // on standard error. A core that prints breaks that the first time it is piped.
        Assert.False(
            core.TypeReferences.Contains("System.Console"),
            "Bws.Core names System.Console. The core returns results, the layer above decides " +
            "what happens to them (06-STRUKTURA-I-KONWENCJE, part 2, rule 3).");
    }

    [Theory]
    [InlineData("Bws.Core")]
    [InlineData("Bws.Cli")]
    [InlineData("Bws.Gui")]
    public void No_shipped_assembly_reaches_for_the_network(string projectName)
    {
        var assembly = AssemblyFacts.Of(projectName);

        // ADR-19 promises the program never sends anything anywhere. This turns that
        // promise from a sentence in a README into something a build can refuse.
        var networkTypes = assembly.TypeReferences
            .Where(type => type.StartsWith("System.Net.", StringComparison.Ordinal))
            .OrderBy(type => type, StringComparer.Ordinal)
            .ToArray();

        var networkAssemblies = assembly.AssemblyReferences
            .Where(name => name.StartsWith("System.Net.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            networkTypes.Length == 0 && networkAssemblies.Length == 0,
            $"{projectName} reaches for the network. " +
            $"Types: [{string.Join(", ", networkTypes)}]. " +
            $"Assemblies: [{string.Join(", ", networkAssemblies)}]. " +
            "Zero telemetry is a decision, not an aspiration (ADR-19).");
    }

    /// <summary>
    /// Nothing that ships starts a process, loads an assembly by name, or builds a type from
    /// one.
    ///
    /// Written 2026-08-02, after a security review elsewhere put it plainly: <b>a tool must not
    /// execute anything, and the guard for that is the same shape as the guard for the
    /// network.</b> This one holds today - checked by reading the
    /// whole of <c>src</c> before writing it, and nothing in the product names any of these -
    /// so it costs nothing now and exists for what comes next.
    ///
    /// <b>What comes next is the point.</b> S7 brings an event log panel and Faza 2 brings a
    /// binary path editor, and both are exactly the slice where somebody reaches for
    /// <c>Process.Start</c> to open a viewer or test a command. A tool that runs elevated on
    /// somebody else's production machine, and whose whole subject is which programs the
    /// machine launches, is the last place a quiet process start belongs.
    ///
    /// <b>What it does not prove, and this is the same caveat the network guard carries:</b>
    /// a reference graph shows what our own code names. A dependency could reach the same
    /// place without us naming it. Three packages are declared and none of them ships, so the
    /// exposure today is small - but the guard is PARTIAL and is described that way in the
    /// regression surface rather than being allowed to look complete.
    /// </summary>
    [Theory]
    [InlineData("Bws.Core")]
    [InlineData("Bws.Cli")]
    [InlineData("Bws.Gui")]
    public void No_shipped_assembly_runs_anything(string projectName)
    {
        var assembly = AssemblyFacts.Of(projectName);

        // Named types rather than a namespace prefix, because System.Diagnostics is where
        // Stopwatch lives and the command line times itself with one. Forbidding the
        // namespace would forbid a clock, which is the sort of guard people switch off.
        //
        // System.Reflection.Assembly is DELIBERATELY NOT HERE, and the first version of this
        // guard had it and went red on all three projects. The reason is good: every one of
        // them reads its own embedded language file, which is what `ADR-21` prescribes, and
        // that goes through Assembly. Forbidding the type would forbid the translations.
        //
        // The cost of leaving it out is stated rather than hidden: this guard sees types, not
        // the members called on them, so Assembly.Load would walk past it. What it does hold
        // is the two shapes that cannot be mistaken for anything innocent - starting a process
        // and building a type from a name.
        // THE WINDOW MAY NAME A PROCESS SINCE 2026-08-25, AND ONLY THE WINDOW - the owner's
        // decision, taken with the alternatives beside it. The button that restarts this tool with
        // administrator rights has no other mechanism: elevation IS a new process, and the sentence
        // this window has said since 2026-08-05 about a short list had no way out at all.
        //
        // <b>The exception is narrow in three ways rather than one.</b> It is one assembly, not
        // three. Activator and AppDomain stay forbidden here, so the shape that catches a stale
        // generated file in obj still catches it. And
        // <see cref="Only_one_file_in_the_window_may_start_a_process"/> reads the sources, so the
        // name may appear in exactly one file and any second one reddens the build.
        string[] forbidden = string.Equals(projectName, "Bws.Gui", StringComparison.Ordinal)
            ?
            [
                "System.Activator",
                "System.AppDomain"
            ]
            :
            [
                "System.Diagnostics.Process",
                "System.Diagnostics.ProcessStartInfo",
                "System.Activator",
                "System.AppDomain"
            ];

        var found = assembly.TypeReferences
            .Where(type => forbidden.Contains(type, StringComparer.Ordinal))
            .OrderBy(type => type, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            found.Length == 0,
            $"{projectName} names something that runs code: [{string.Join(", ", found)}]. " +
            "This tool reads a machine and changes services through a plan. It does not start " +
            "programs, and it does not build types a file asked for. If a slice genuinely " +
            "needs one of these, that is a conversation and an entry here with its reason - " +
            "not a reference that arrives while somebody is doing something else.");
    }
    /// <summary>
    /// A process may be started from one file in the window, and that file is named here.
    ///
    /// <b>This is the other half of the exception the theory above describes</b>, and without it
    /// that exception would be an assembly-wide door: anything in the window could reach for
    /// Process.Start and nothing would say a word.
    ///
    /// <b>It reads the SOURCES rather than the assembly</b>, because that is the only place the
    /// question "which file" can be asked at all - a compiled assembly knows which types it names
    /// and not which of its files named them. The cost is the usual one for a text scan and is
    /// stated rather than hidden: a file could reach the same place through a name this does not
    /// look for, exactly as the theory above says about a dependency.
    ///
    /// <b>The allowed file is spelled out rather than pattern matched</b>, the same shape
    /// <c>PlanOnlyGuards</c> uses for the two files allowed to build a writer. A list somebody has
    /// to add a line to is a list somebody has to think about.
    /// </summary>
    [Fact]
    public void Only_one_file_in_the_window_may_start_a_process()
    {
        const string Allowed = "Elevation.cs";

        var window = Path.Combine(SourceTree.Root(), "src", "Bws.Gui");

        var named = Directory
            .EnumerateFiles(window, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => Names(File.ReadAllText(file)))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            named.Count == 1 && string.Equals(named[0], Allowed, StringComparison.Ordinal),
            "Starting a process is allowed in " + Allowed + " and nowhere else in this window - "
            + "the owner decided that on 2026-08-25 for one button, with the reason written at the "
            + "head of that file. These files name one: "
            + string.Join(", ", named));
    }

    /// <summary>Whether a source file names the two types that start a program.</summary>
    private static bool Names(string source) =>
        source.Contains("Process.Start", StringComparison.Ordinal)
        || source.Contains("ProcessStartInfo", StringComparison.Ordinal);
}
