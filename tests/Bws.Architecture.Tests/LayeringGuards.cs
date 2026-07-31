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

    [Fact]
    public void The_core_never_references_a_presentation_project()
    {
        var core = AssemblyFacts.Of("Bws.Core");

        Assert.DoesNotContain("Bws.Cli", core.AssemblyReferences);
        Assert.DoesNotContain("Bws.Gui", core.AssemblyReferences);
    }

    [Fact]
    public void The_cli_and_the_gui_never_reference_each_other()
    {
        Assert.DoesNotContain("Bws.Gui", AssemblyFacts.Of("Bws.Cli").AssemblyReferences);
        Assert.DoesNotContain("Bws.Cli", AssemblyFacts.Of("Bws.Gui").AssemblyReferences);
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
}
