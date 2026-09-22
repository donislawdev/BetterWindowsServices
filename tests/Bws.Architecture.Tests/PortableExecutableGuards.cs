using System.Xml.Linq;

namespace Bws.Architecture.Tests;

/// <summary>
/// That a publish of the window really produces one file.
///
/// <b>IT DID NOT, FROM THE DAY THE WINDOW EXISTED UNTIL 2026-09-09, AND EVERY BUILD WAS GREEN
/// THROUGHOUT.</b> <c>PublishSingleFile</c> was passed, the publish succeeded, and it wrote
/// the window's executable beside five native halves of WPF - D3DCompiler, wpfgfx, PresentationNative,
/// PenImc and the C runtime, about eight megabytes. The single-file bundler leaves native
/// libraries out unless told otherwise, because they have to be loaded from a path on disk.
///
/// <b>Phase 1 of the specification promises a portable executable</b>, and <c>PreferencesFile</c>
/// names the person it is for: the administrator who copies the executable to the twentieth
/// server. That person copies one file. Found by the pre-release audit of 2026-09-09 listing a
/// publish directory - not by anything in this suite, because nothing here had ever looked at
/// what a publish produces.
///
/// <b>WHAT THIS GUARD IS AND IS NOT.</b> It reads the declaration, not the output: asserting on
/// the output would mean running a publish inside the suite, which takes minutes and needs a
/// runtime identifier the test host has no business choosing. So this catches the property being
/// deleted or renamed - which is how it would actually be lost - and does not catch a future
/// runtime changing what the property means. That second one is named here rather than left for
/// somebody to assume covered, and the thing that would catch it is a publish in the release
/// pipeline followed by counting the files.
/// </summary>
public sealed class PortableExecutableGuards
{
    /// <summary>
    /// The window says its natives belong inside the file.
    /// </summary>
    [Fact]
    public void The_window_publishes_as_one_file_including_the_native_halves_of_wpf() =>
        Assert.True(
            Declares("Bws.Gui", "IncludeNativeLibrariesForSelfExtract"),
            "Bws.Gui.csproj no longer declares IncludeNativeLibrariesForSelfExtract, so a "
            + "single-file publish will write the executable beside five native DLLs again. "
            + "Phase 1 promises a portable executable and that is the promise this holds.");

    /// <summary>
    /// <b>And the terminal deliberately does not, which is the half that keeps the first assertion
    /// meaning something.</b> That project has no native libraries of its own - its publish was
    /// already one file - so the property would change nothing there, and a property that changes
    /// nothing is one somebody later has to work out the purpose of. Written as a guard rather
    /// than a comment because "copy it to the other project too" is the obvious next edit.
    /// </summary>
    [Fact]
    public void The_terminal_does_not_carry_a_setting_that_would_do_nothing_for_it() =>
        Assert.False(
            Declares("Bws.Cli", "IncludeNativeLibrariesForSelfExtract"),
            "Bws.Cli.csproj declares IncludeNativeLibrariesForSelfExtract. That project has no "
            + "native libraries, so the setting does nothing there - and a setting that does "
            + "nothing is one the next reader has to spend time disproving.");

    private static bool Declares(string projectName, string property)
    {
        var projectFile = Path.Combine(
            SourceTree.Root(), "src", projectName, projectName + ".csproj");

        if (!File.Exists(projectFile))
        {
            throw new InvalidOperationException($"Project file not found: '{projectFile}'.");
        }

        return XDocument.Load(projectFile)
            .Descendants()
            .Any(node =>
                node.Name.LocalName == property
                && string.Equals(node.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase));
    }
}
