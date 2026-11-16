using System.Security.Principal;

namespace Bws.Core;

/// <summary>
/// What is true about the session this process is running in.
///
/// <b>One reader of this fact, not two.</b> Elevation was answered inside the snapshot's own
/// metadata and nowhere else, so the window had no way to say what a snapshot says plainly -
/// that a session without administrator rights is handed fewer entries than the machine has.
/// Two readers of the same fact drift at the first edit, and this one is measured rather than
/// obvious.
/// </summary>
public static class Session
{
    /// <summary>
    /// Whether this session has administrator rights.
    ///
    /// <b>Measured, and the number is the reason this is worth saying out loud:</b> without
    /// elevation the manager enumerates 807 entries where an elevated session sees 810 on the
    /// same machine, and the security descriptor is refused for five more. A listing taken
    /// without elevation is not a shorter listing - it is a different document.
    ///
    /// <b>Asked through the built-in role, which compares the well-known identifier.</b> Never
    /// by the name of the group: that comparison reads "Administrators" and answers no on a
    /// machine where the group is called something else, which is how a set of measurements in
    /// this project came to be recorded under the wrong heading.
    /// </summary>
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();

        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// The settings in this process's environment that tell the .NET runtime to load code into it
    /// or to write files from it - named so that an elevated process can say they are there.
    ///
    /// <para>
    /// <b>Named rather than switched off, because they cannot be switched off from inside.</b>
    /// Microsoft's page of runtime settings for debugging and profiling lists no runtimeconfig.json
    /// equivalent for any of them - read 2026-10-06. Startup hooks are the one that can be, and are,
    /// in Directory.Build.props. An elevated process inherits the environment of the account that
    /// started it, measured the same day, so whatever that account carries arrives here. Security
    /// report S-2.
    /// </para>
    /// <para>
    /// <b>"Set" means present, not empty and not 0</b>, and the sentences built from this say "set"
    /// rather than "loaded". The runtime's own parsing of each value is not reproduced, so a value
    /// it would read as off can still be named - an over-statement of what is in the environment,
    /// never an under-statement of it.
    /// </para>
    /// <para>
    /// Both spellings of each knob are asked about: the profiler's variables take a DOTNET prefix as
    /// well as CORECLR from .NET 11 onward, and the runtime's own knobs still answer to the COMPlus
    /// prefix they had before DOTNET. Naming one that this runtime ignores costs a line, and missing
    /// one that a later runtime honours costs the point of the list.
    /// </para>
    /// </summary>
    /// <param name="extractsNatives">
    /// Whether this program unpacks native libraries before it runs, which the window does and the
    /// terminal does not. Only then does the variable choosing where they are unpacked matter.
    /// </param>
    public static IReadOnlyList<string> RuntimeSettingsFromEnvironment(bool extractsNatives) =>
        RuntimeSettingsFrom(Environment.GetEnvironmentVariable, extractsNatives);

    /// <summary>The same question asked of any environment, which is what a test can hand it.</summary>
    internal static IReadOnlyList<string> RuntimeSettingsFrom(Func<string, string?> read, bool extractsNatives) =>
        [.. (extractsNatives ? WhenUnpacking : Always).Where(name => IsSet(read(name)))];

    private static bool IsSet(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim() != "0";

    private static readonly string[] Always =
    [
        "CORECLR_ENABLE_PROFILING",
        "DOTNET_ENABLE_PROFILING",
        "DOTNET_EnableEventPipe",
        "COMPlus_EnableEventPipe",
        "DOTNET_DbgEnableMiniDump",
        "COMPlus_DbgEnableMiniDump",
        "DOTNET_DiagnosticPorts",
        "COMPlus_DiagnosticPorts",
    ];

    private static readonly string[] WhenUnpacking = [.. Always, "DOTNET_BUNDLE_EXTRACT_BASE_DIR"];
}
