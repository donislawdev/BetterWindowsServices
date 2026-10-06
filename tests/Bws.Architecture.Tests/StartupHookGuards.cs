using System.Xml.Linq;

namespace Bws.Architecture.Tests;

/// <summary>
/// That no process this repository builds runs the assemblies named in DOTNET_STARTUP_HOOKS -
/// security report S-2, 2026-10-06.
///
/// <b>WHAT THIS ASSERTS IS THE RUNTIME, NOT THE LINE.</b> The property lives in Directory.Build.props
/// and every project inherits it, this one too - so the switch the runtime reads from this test
/// host's own runtimeconfig.json is the switch the shipped programs get from the same file. Asking
/// the running process catches the property being renamed, misspelt or losing its meaning in a later
/// SDK, which reading it back from the props file would not.
///
/// <b>What it does not catch, said rather than left to be assumed:</b> a shipped project overriding
/// the value for itself. That is the second assertion, read from the project files, because no
/// shipped program runs inside this suite to be asked.
/// </summary>
public sealed class StartupHookGuards
{
    private const string Switch = "System.StartupHookProvider.IsSupported";

    [Fact]
    public void The_runtime_is_told_startup_hooks_are_not_supported()
    {
        Assert.True(
            AppContext.TryGetSwitch(Switch, out var supported),
            $"The runtime of this test host was given no value for {Switch}. Directory.Build.props sets "
            + "StartupHookSupport to false for every project, so the property is gone or no longer "
            + "becomes this switch - and the shipped programs would then run DOTNET_STARTUP_HOOKS again.");

        Assert.False(supported, $"{Switch} is on. StartupHookSupport has to stay false - security report S-2.");
    }

    [Fact]
    public void No_project_sets_startup_hook_support_for_itself()
    {
        var root = SourceTree.Root();

        var overriding = Directory
            .EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(file => XDocument.Load(file).Descendants().Any(node => node.Name.LocalName == "StartupHookSupport"))
            .Select(file => Path.GetRelativePath(root, file))
            .ToList();

        Assert.True(
            overriding.Count == 0,
            "These project files set StartupHookSupport themselves, and a local value wins over "
            + "Directory.Build.props: " + string.Join(", ", overriding));
    }
}
