namespace Bws.Core;

/// <summary>
/// Whether this tool may reach off this machine at all.
///
/// <b>THE NAME OF THIS TYPE IS NARROWER THAN WHAT IT NOW DECIDES, AND THAT IS SAID HERE RATHER
/// THAN QUIETLY LIVED WITH.</b> Since 2026-09-22 it also decides whether Windows may go and
/// fetch a certificate while verifying a signature - measured that day, reading signatures
/// opened HTTP connections to certificates.intel.com on a listing nobody had told to use the
/// network. Both are the same decision for whoever runs this: one promise, one control, and
/// a second switch would have meant finding two of them before the tool was actually quiet.
/// Renaming the type is a sweep of its own and is a backlog row rather than a change made on
/// the way past.
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
    private const string VolumeName = "Volume{";

    /// <summary>
    /// Whether reaching this path means reaching off this machine - or cannot be told apart
    /// from it without asking the system, which is the question being avoided.
    ///
    /// <b>The Win32 device namespace is LOCAL ONLY BEFORE A DRIVE LETTER OR A VOLUME, since
    /// 2026-10-05</b> - the owner's decision on stability report R-2. <c>\\?\C:\x</c> and
    /// <c>\\?\Volume{...}\x</c> name a file on this machine and skip path parsing, and calling
    /// them remote would stop the tool answering about files that are right here. Until that day
    /// the rule was the other way round - everything after <c>\\?\</c> or <c>\\.\</c> was local
    /// except <c>\\?\UNC\</c> - and three spellings walked through it to a share:
    /// <c>\\.\UNC\host\share</c>, <c>\\?\GLOBALROOT\Device\Mup\host\share</c> and the same with
    /// a dot. Listing the shapes that leave is a list nobody can finish, because the object
    /// manager holds more names than this code will ever know. Listing the two that stay is a
    /// list that is already finished. A name in between - a device, a pipe, GLOBALROOT pointing
    /// at a local volume - comes back as not read, which is honest about a path nobody looked
    /// at, and <see cref="NetworkPaths.Follow"/> reads it.
    ///
    /// The cost, said rather than discovered: <c>\\.\PhysicalDrive0</c> answered "local" until
    /// that day and now does not. No service on the machine this was written on has a launch
    /// path beginning with two backslashes at all - 0 of 787, counted that day with
    /// tools/scm-probe/scm-probe.ps1.
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

        // \\?\ and \\.\ - the device namespace. Anything else after two backslashes is a share.
        var deviceNamespace = value.Length > 3 && (value[2] is '?' or '.') && value[3] == '\\';

        return !deviceNamespace || !NamesALocalVolume(value.AsSpan(4));
    }

    /// <summary>
    /// A drive letter and its colon, or a volume by its identifier - nothing else. Internal since
    /// 2026-10-06 because <see cref="FileShape"/> asks the same question about the same namespace,
    /// and one rule in two places is one rule that will be changed in one of them.
    /// </summary>
    internal static bool NamesALocalVolume(ReadOnlySpan<char> name) =>
        (name.Length >= 2 && char.IsAsciiLetter(name[0]) && name[1] == ':')
        || name.StartsWith(VolumeName, StringComparison.OrdinalIgnoreCase);
}
