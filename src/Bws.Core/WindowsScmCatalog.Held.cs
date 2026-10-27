using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;

namespace Bws.Core;

/// <summary>
/// Which names the manager still holds - since 2026-10-27, logic report S03.
///
/// <b>The manager is asked by name and the registry is not read</b>, unlike the question R04 asks before a
/// process is ended (WindowsEndingFactsReader.Unlisted.cs). There the names are what is missing, and only the
/// registry hands them out. Here a snapshot already names them, and the services API answers a question
/// about a name - so the rule to ask that API wherever it answers holds.
/// </summary>
public sealed partial class WindowsScmCatalog
{
    /// <inheritdoc />
    public IReadOnlyList<string> ReadHeld(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        // Nothing missing is the ordinary answer, and it opens nothing.
        if (names.Count == 0)
        {
            return [];
        }

        using var manager = PInvoke.OpenSCManager(
            lpMachineName: null!,
            lpDatabaseName: null!,
            dwDesiredAccess: PInvoke.SC_MANAGER_CONNECT);

        if (manager.IsInvalid)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not open the service control manager to ask about entries missing from its list.");
        }

        return [.. names.Where(name => Held(manager, name))];
    }

    /// <summary>
    /// Whether the manager holds this name: it opened, or it refused for any reason but "no such entry" -
    /// the same reading of a refusal as <see cref="WindowsEndingFactsReader.Withholds"/>, which this asks.
    ///
    /// <b>A name holding a NUL is not asked</b>: the names come from a file, and passed to Windows such a
    /// name ends at the NUL - so "Spooler" followed by anything would be answered about Spooler.
    /// </summary>
    private static bool Held(SafeHandle manager, string name)
    {
        if (name.Contains('\0', StringComparison.Ordinal))
        {
            return false;
        }

        using var service = PInvoke.OpenService(manager, name, PInvoke.SERVICE_QUERY_STATUS);

        return !service.IsInvalid || WindowsEndingFactsReader.Withholds(Marshal.GetLastWin32Error());
    }
}
