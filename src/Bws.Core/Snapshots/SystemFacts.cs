using System.Globalization;
using System.Security;
using Microsoft.Win32;
using Windows.Win32;

namespace Bws.Core.Snapshots;

/// <summary>
/// Two facts about the machine a snapshot needs and the rest of the product does not: which update
/// of Windows it runs, and which language the service manager names things in.
///
/// <b>Both since schema five, 2026-09-30 - stability report D-5, package E, owner's decisions of
/// that day.</b> Without the first, two snapshots either side of a monthly update compared as
/// taken on the same Windows, because <c>Environment.OSVersion</c> ends in ".0" where the update
/// number belongs - measured that day as "Microsoft Windows NT 10.0.26200.0" on a machine whose
/// update number is 9550. Without the second, two machines naming services in different languages
/// compared every translated display name as drift.
///
/// <b>Null is "not known", never a guess.</b> Either fact can be refused, and a snapshot that wrote
/// ".0" for an update it could not read, or the install language for a system language it could
/// not ask, would be carrying something that looks like a reading and is not one - rule 8. A
/// comparison meeting null says it could not check, which is a different sentence from "the same".
/// </summary>
internal static class SystemFacts
{
    /// <summary>
    /// Major, minor, build and update, as in "10.0.26200.9550", or null when the update is not known.
    ///
    /// <b>The update number comes from the registry, owner's decision of 2026-09-30, and only after
    /// looking for anything else.</b> Microsoft documents that value as the place the revision is
    /// kept ("OEM deployment of Windows desktop editions", and the Intune page on requirement rules
    /// reads the same value). The one other road, the WinRT <c>AnalyticsInfo</c> version, would bring
    /// the Windows SDK projection in as a new dependency and its page does not say it carries the
    /// update at all. The rule against reading the registry on a short cut is about services, where
    /// the manager has an API - this is not a fact about a service, and nothing else answers it.
    ///
    /// <b>The first three parts come from the runtime rather than from the registry beside them.</b>
    /// <c>Environment.OSVersion</c> is what this format has always written as "operatingSystem", so
    /// the two fields cannot disagree about the build.
    /// </summary>
    internal static string? OperatingSystemVersion()
    {
        var update = UpdateNumber();

        if (update is null)
        {
            return null;
        }

        var version = Environment.OSVersion.Version;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{version.Major}.{version.Minor}.{version.Build}.{update.Value}");
    }

    /// <summary>
    /// The first of the system's preferred UI languages, as in "pl-PL", or null when it cannot be asked.
    ///
    /// <b>The system's rather than this session's, and that was a measurement before it was a
    /// decision.</b> tools/scm-probe/language-probe.ps1 switched the calling thread between en-US
    /// and pl-PL on a machine holding both: the system's own error text followed the switch, and the
    /// manager handed back the same Polish names and descriptions under both. What it could NOT tell
    /// apart is the manager following the caller's USER language against following the system's -
    /// both are pl-PL there. Two different accounts on one machine are named by a caveat of their
    /// own (<see cref="ComparisonCaveats.AccountDiffers"/>), which is where that case lands.
    /// </summary>
    internal static unsafe string? NamesLanguage()
    {
        uint count;
        uint size = 0;

        // Asked for the size first rather than guessing one, so a machine with many languages is
        // never cut short into a first name that is only half of itself.
        if (!PInvoke.GetSystemPreferredUILanguages(PInvoke.MUI_LANGUAGE_NAME, &count, default, &size) || size == 0)
        {
            return null;
        }

        var buffer = new char[size];

        fixed (char* start = buffer)
        {
            if (!PInvoke.GetSystemPreferredUILanguages(PInvoke.MUI_LANGUAGE_NAME, &count, start, &size))
            {
                return null;
            }
        }

        // A list of names each ended by a null, the whole ended by a second one. The first is the
        // language the system uses when nothing else is asked for.
        var end = Array.IndexOf(buffer, '\0');
        var first = end < 0 ? new string(buffer) : new string(buffer, 0, end);

        return first.Length == 0 ? null : first;
    }

    private static int? UpdateNumber()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");

            return key?.GetValue("UBR") is int update ? update : null;
        }
        catch (Exception refused) when (refused is SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
