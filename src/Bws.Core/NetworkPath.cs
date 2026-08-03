namespace Bws.Core;

/// <summary>
/// Whether this tool may look at a file that lives on another machine.
///
/// It exists because of a measurement rather than a principle. A service can register a
/// launch path on a share, and asking whether that file is there is an ordinary
/// <c>File.Exists</c> - which on an unreachable host <b>blocks for 21 053 ms</b>, measured
/// 2026-08-02, against 1.23 ms for a local path and a budget of one second. One such entry
/// is enough to make a listing look hung.
///
/// The other half is worse and quieter. An SMB connection authenticates with the token of
/// whoever is running the tool, and this tool is meant to run elevated on production
/// machines - so a service entry pointing at somebody else's share turns an audit into a
/// credential disclosure. The precondition is administrative rights on the machine, which
/// is exactly the situation somebody reaches for this tool to investigate.
///
/// <b>`ADR-19` was already broken by this and its guard could not see it.</b> The rule says
/// zero outbound connections, and the guard that holds it checks for references to
/// <c>System.Net</c>. SMB arrives through <c>System.IO</c> and walked straight past.
/// </summary>
public enum NetworkPaths
{
    /// <summary>
    /// Do not ask. Anything that would have required reaching off this machine comes back as
    /// <b>not read</b> rather than as absent - nobody looked, which is not the same as
    /// finding nothing, and the difference is the whole of rule 8.
    /// </summary>
    Skip,

    /// <summary>
    /// Ask, like any other path. For the administrator who genuinely runs services from a
    /// share and wants the same answers about them, having read what it costs.
    /// </summary>
    Follow
}

/// <summary>
/// Telling a path on another machine from one that merely looks like it.
/// </summary>
public static class NetworkPath
{
    private const string DeviceNamespace = @"\\?\";
    private const string LocalDeviceNamespace = @"\\.\";
    private const string DeviceUnc = @"\\?\UNC\";

    /// <summary>
    /// Whether reaching this path means reaching off this machine.
    ///
    /// <b>Two shapes start with two backslashes and are local</b>, which is why this is a
    /// method rather than a <c>StartsWith</c> at each call site. <c>\\?\C:\x</c> and
    /// <c>\\.\PhysicalDrive0</c> are the Win32 device namespace - a way of naming things on
    /// this machine that skips path parsing - and treating them as remote would stop the
    /// tool answering about files that are right here.
    ///
    /// <b>What this cannot see, stated rather than left to be discovered:</b> a drive letter
    /// mapped to a share. <c>Z:\service.exe</c> is indistinguishable from a local path
    /// without asking the system, and asking is the thing being avoided. An entry launched
    /// from a mapped drive is therefore still followed. Mapped drives are per user session
    /// and services do not ordinarily have one, which is why this is a documented edge and
    /// not a hole - but it is an edge, and it is written here rather than nowhere.
    /// </summary>
    public static bool LeavesThisMachine(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Separators folded before anything is compared, and that is a repair rather than
        // tidiness. Windows treats a forward slash as a separator everywhere a path is parsed,
        // so `//server/share/x.exe` reaches the same host as `\\server\share\x.exe` - and until
        // 2026-08-03 this looked only for two backslashes and let the first shape past as local.
        // Everything this class exists to prevent then happened: an existence check that blocks
        // for twenty one seconds on an unreachable host, and an SMB connection carrying the token
        // of whoever ran the tool, which is meant to be an administrator on a production machine.
        //
        // `ADR-19` has now been broken twice by the same class of thing and neither time by
        // anything resembling a network client - once through System.IO, once through a
        // separator. Both times the guard was looking at the wrong layer.
        var value = path.TrimStart().Replace('/', '\\');

        if (!value.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return false;
        }

        if (value.StartsWith(DeviceUnc, StringComparison.OrdinalIgnoreCase))
        {
            // The device namespace spelling of a share. Same destination, longer name.
            return true;
        }

        return !value.StartsWith(DeviceNamespace, StringComparison.Ordinal)
            && !value.StartsWith(LocalDeviceNamespace, StringComparison.Ordinal);
    }
}
