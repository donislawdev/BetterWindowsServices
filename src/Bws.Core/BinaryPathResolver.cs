namespace Bws.Core;

/// <summary>
/// Works out which file a launch command actually runs.
///
/// The manager hands back one string holding the executable and its arguments together,
/// written in whichever of several shapes the author of the service happened to use.
/// Measured on a real machine on 2026-08-01, across 825 entries: 294 start with a drive
/// letter, 237 with <c>\SystemRoot\</c>, 216 are relative to the Windows directory, 45 are
/// quoted, 29 are empty and 4 start with <c>\??\</c>.
///
/// Getting this wrong is quiet and expensive. Checking the raw string for a file on disk,
/// which is the obvious first attempt, reported 785 of those 825 as missing - the arguments
/// and the relative paths see to that. Anything built on top would have called almost the
/// whole machine broken.
///
/// <b>DRIVERS AND SERVICES ARE READ BY DIFFERENT CODE, AND SINCE 2026-10-05 THIS SAYS SO</b>
/// (stability report R-3). The manager hands a service's command to CreateProcess, which splits it
/// at spaces and searches for a bare name. The kernel takes a driver's path as one file name, with
/// no arguments, relative to <c>\SystemRoot\</c> when it is not rooted - knowledge about Windows,
/// not a measurement and not a Microsoft page. Until that day a driver went through the command
/// line rules too, and the one driver on the owner's machine with an unquoted space in its path
/// (<c>\??\C:\Program Files\...\x.sys</c>, counted with tools/scm-probe/scm-probe.ps1) resolved
/// correctly only because no <c>C:\Program.exe</c> existed. Planting one would have made the
/// signature and the hash describe that file instead - in an audit tool.
/// </summary>
public static class BinaryPathResolver
{
    private const string ObjectPrefix = @"\??\";
    private const string SystemRootPrefix = @"\SystemRoot\";
    private const string ObjectShare = @"UNC\";
    private const string Win32Device = @"\\?\";
    private const string Win32GlobalRoot = @"\\?\GLOBALROOT";

    private static readonly string[] ExecutableExtensions =
        [".exe", ".sys", ".dll", ".com", ".bat", ".cmd"];

    /// <summary>
    /// The file a launch command runs, and whether it is on disk.
    ///
    /// The two come back together because working out which file it is already has to look
    /// at the disk - an unquoted path with spaces cannot be split any other way. Answering
    /// both at once keeps the caller from asking a second time about a file we just looked
    /// at, which on a full listing is several hundred needless questions.
    /// </summary>
    /// <param name="File">Absolute path, or null when the entry names nothing to resolve.</param>
    /// <param name="OnDisk">
    /// Whether that file is there. A <see cref="Reading{T}"/> rather than a bool because there
    /// are four answers, not two: it is there, it is not, <b>nobody looked</b> - for a path on
    /// another machine when the caller did not ask to go off this one - and, since 2026-10-05,
    /// <b>the system would not say</b>, for a file this token may not read (stability report
    /// R-1, <see cref="FileOnDisk"/>). Collapsing either of the last two into "not there" would
    /// be rule 8 broken in the field where it costs most: an audit tool reporting a file as
    /// missing when it never found out.
    /// </param>
    public readonly record struct ResolvedBinary(string? File, Reading<bool> OnDisk);

    /// <param name="command">The launch command exactly as the manager returns it.</param>
    /// <param name="serviceName">Needed only for a driver that names no file of its own.</param>
    /// <param name="isDriver">
    /// Drivers have a default the manager applies for them, and their path is one file name
    /// rather than a command line.
    /// </param>
    /// <param name="windowsDirectory">What a relative path and <c>\SystemRoot\</c> are relative to.</param>
    /// <param name="exists">
    /// Asks whether a candidate is on disk, with a refusal as the third answer. Handed in rather
    /// than called directly, so the rules above can be tested without a file system arranged to
    /// suit them.
    /// </param>
    /// <param name="networkPaths">
    /// Whether a path on another machine may be asked about at all. Skipping is the default
    /// everywhere, and the reason is measured rather than cautious - see
    /// <see cref="NetworkPaths"/> for the 21 seconds and the credential half.
    /// </param>
    public static ResolvedBinary Resolve(
        string? command,
        string serviceName,
        bool isDriver,
        string windowsDirectory,
        Func<string, Reading<bool>> exists,
        NetworkPaths networkPaths)
    {
        // No default value on the parameter above, on purpose. A default would let a new call
        // site reach off the machine by saying nothing, which is exactly how the behaviour
        // this replaces went unnoticed for six slices.
        var asking = new Asking(exists, networkPaths, isDriver, windowsDirectory);

        if (string.IsNullOrWhiteSpace(command))
        {
            // A driver that names no file runs the one the manager assumes for it. Anything
            // else naming no file leaves us with nothing to resolve, and saying so is better
            // than inventing a path in order to report it missing.
            return isDriver
                ? asking.Settle(Path.Combine(windowsDirectory, "System32", "drivers", serviceName + ".sys"))
                : new ResolvedBinary(null, Reading<bool>.Absent());
        }

        var trimmed = command.Trim();

        if (trimmed[0] == '"')
        {
            // Quotes settle it. Whoever wrote them said where the file name ends, which is
            // the entire reason quoting an image path is worth doing.
            var close = trimmed.IndexOf('"', 1);

            return asking.Settle(close > 0 ? trimmed[1..close] : trimmed[1..]);
        }

        // A driver's path is the whole string - see the class summary. Only a service's command
        // has arguments to cut off and a name to search for.
        return isDriver ? asking.Settle(trimmed) : asking.Walk(trimmed);
    }

    /// <summary>
    /// One question about one entry, carried through every candidate it produces. A struct rather
    /// than five parameters on every method below, because the five are the same for all of them.
    /// </summary>
    private readonly record struct Asking(
        Func<string, Reading<bool>> Exists,
        NetworkPaths NetworkPaths,
        bool IsDriver,
        string WindowsDirectory)
    {
        /// <summary>
        /// A name with no doubt about where it ends: the places it can mean, tried in order.
        /// </summary>
        internal ResolvedBinary Settle(string written)
        {
            var found = Look(written, out var lookedAway);

            return found ?? new ResolvedBinary(
                Places(written).First(),
                lookedAway ? Reading<bool>.NotRead() : Reading<bool>.Present(false));
        }

        /// <summary>
        /// No quotes and possibly spaces, so where the file name ends is genuinely ambiguous.
        /// Every prefix is tried, shortest first, and the first one that answers is the answer.
        ///
        /// Cutting at the first space is the obvious alternative and it is wrong: it turned both
        /// of the entries on the machine with this shape into "C:\Program" and reported files
        /// that are there as missing. The order is CreateProcess's own, from its documentation:
        /// <c>c:\program.exe</c> first, the whole name last.
        ///
        /// <b>A REFUSED CANDIDATE ENDS THE WALK, since 2026-10-05</b> (stability report R-1). A
        /// refusal comes back only for a name that exists - measured that day, see
        /// <see cref="FileOnDisk"/> - so it is the file CreateProcess would reach first, and
        /// walking past it to a longer one this token can read would name a file Windows does not
        /// run.
        /// </summary>
        internal ResolvedBinary Walk(string trimmed)
        {
            string? executableLooking = null;
            var lookedAway = false;

            // Ordinal by construction: searching for a character has no cultural reading, and
            // the overload taking a start index has no comparison parameter to pass one to.
            for (var space = trimmed.IndexOf(' ', StringComparison.Ordinal); ;
                 space = trimmed.IndexOf(' ', space + 1))
            {
                var written = space < 0 ? trimmed : trimmed[..space];

                if (Look(written, out var away) is { } found)
                {
                    return found;
                }

                lookedAway |= away;
                executableLooking ??= HasExecutableExtension(written) ? Places(written).First() : null;

                if (space < 0)
                {
                    break;
                }
            }

            // Nothing on disk, so the answer is going to be "missing" whatever we pick, and the
            // point of picking well is that a person can check it. The prefix that ends in an
            // executable extension is the one they meant. Falling back to the whole string keeps
            // us from returning a truncated path, which would read as a different mistake.
            //
            // Unless nobody looked, in which case "missing" would be a claim about a disk this
            // process never touched. Which prefix is the file cannot be settled without looking
            // either, so the readable guess comes back with the disk question unanswered.
            return new ResolvedBinary(
                executableLooking ?? Places(trimmed).First(),
                lookedAway ? Reading<bool>.NotRead() : Reading<bool>.Present(false));
        }

        /// <summary>
        /// Every name one written candidate can mean, asked in order. Null when none of them is
        /// there or refuses, with <paramref name="lookedAway"/> saying whether one was skipped for
        /// being off this machine - every place is asked about on its own, because a bare name
        /// searched along PATH can meet a share among local directories.
        /// </summary>
        private ResolvedBinary? Look(string written, out bool lookedAway)
        {
            lookedAway = false;

            foreach (var place in Places(written))
            {
                var offLimits = NetworkPaths == NetworkPaths.Skip && NetworkPath.LeavesThisMachine(place);
                lookedAway |= offLimits;

                if (!offLimits && Answered(place) is { } found)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// The first name for one place that is there or refuses - a refusal is an answer, see
        /// <see cref="Walk"/> - or null when every name Windows would try is absent.
        /// </summary>
        private ResolvedBinary? Answered(string place)
        {
            foreach (var name in AsWindowsWouldTryIt(place))
            {
                var answer = Exists(name);

                if (answer.Outcome != ReadOutcome.Present || answer.Value)
                {
                    return new ResolvedBinary(name, answer);
                }
            }

            return null;
        }

        /// <summary>
        /// The names Windows will actually try for one candidate.
        ///
        /// <b>CreateProcess appends .exe when the name it is handed carries no extension</b>, and
        /// services are launched through it. So <c>C:\WINDOWS\system32\svchost -k TSLicensing</c>
        /// runs <c>svchost.exe</c>, and every prefix of that command is a name with no extension.
        ///
        /// Found 2026-08-04 on Windows Server 2025, which has exactly one entry written this way -
        /// <c>TermServLicensing</c>, and it is <b>Running</b> while we called its file missing.
        /// That is a false audit finding, not a cosmetic one. <b>The CreateProcess documentation
        /// says .exe is not appended when the name contains a path</b> - read 2026-10-05 - and that
        /// running entry says it is. The measurement wins.
        ///
        /// Only .exe, and only when there is no extension at all, because that is the whole of
        /// what CreateProcess does. A name ending .bat or .com is taken as written, and a name
        /// ending in something that is not an extension at all - a version number, say - counts
        /// as having one and gets nothing appended. <b>Never for a driver</b>: the kernel loads the
        /// name it is given.
        /// </summary>
        private IEnumerable<string> AsWindowsWouldTryIt(string place)
        {
            yield return place;

            if (!IsDriver && Path.GetExtension(place).Length == 0)
            {
                yield return place + ".exe";
            }
        }

        /// <summary>
        /// One written candidate, turned into the paths that can be looked for - one, except for
        /// a service naming no directory at all, which CreateProcess searches for.
        ///
        /// The order of these tests is not interchangeable. A path beginning with a single
        /// backslash is rooted as far as the platform is concerned, so asking "is it rooted"
        /// first would let <c>\SystemRoot\System32\drivers\x.sys</c> through untouched and it
        /// would be looked for on a drive it is not on.
        /// </summary>
        private IEnumerable<string> Places(string written)
        {
            var value = written.Trim();

            if (value.Contains('%', StringComparison.Ordinal))
            {
                value = ManagerEnvironment.Expand(value, WindowsDirectory);
            }

            if (value.StartsWith(ObjectPrefix, StringComparison.Ordinal))
            {
                return [FromTheObjectNamespace(value[ObjectPrefix.Length..])];
            }

            if (value.StartsWith(SystemRootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return [Path.Combine(WindowsDirectory, value[SystemRootPrefix.Length..])];
            }

            if (value.StartsWith('\\'))
            {
                return [Rooted(value)];
            }

            if (value.Length > 1 && value[1] == ':')
            {
                return [value];
            }

            // Relative. A driver's path is relative to \SystemRoot\, and so, as far as anything
            // measured says, is a service's that names a directory - 0 of the second on the
            // owner's machine, and services.exe's current directory, which CreateProcess would
            // use, is NOT CHECKED.
            return IsDriver || value.Contains('\\', StringComparison.Ordinal) || value.Contains('/', StringComparison.Ordinal)
                ? [Path.Combine(WindowsDirectory, value)]
                : SearchedFor(value);
        }

        /// <summary>
        /// A path starting with a backslash and not one of the two prefixes above.
        ///
        /// Two backslashes is a share or the device namespace, already absolute. One is rooted on
        /// no stated drive: for a service that is the drive CreateProcess's caller is on, which is
        /// the one Windows is on. <b>For a driver it is a name in the kernel's object namespace</b>
        /// - <c>\Device\HarddiskVolume3\x.sys</c> - and until 2026-10-05 became
        /// <c>C:\Device\...</c>, a confident "missing" about a file that may well be there. Its
        /// Win32 spelling is the same name under <c>\\?\GLOBALROOT</c>, which
        /// <see cref="NetworkPath"/> will not follow without being asked, because GLOBALROOT can
        /// lead to a share as easily as to a disk.
        /// </summary>
        private string Rooted(string value)
        {
            if (value.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return value;
            }

            return IsDriver
                ? Win32GlobalRoot + value
                : Path.Combine(Path.GetPathRoot(WindowsDirectory) ?? @"C:\", value[1..]);
        }

        /// <summary>
        /// Where CreateProcess looks for a name with no directory, in its documented order
        /// (learn.microsoft.com, CreateProcessW, read 2026-10-05): the directory the parent was
        /// loaded from, the parent's current directory, System32, System, the Windows directory,
        /// then PATH. The parent is services.exe, loaded from System32. Its current directory is
        /// NOT CHECKED, and believed to be System32 too - which would leave this order as written.
        /// 0 entries on the owner's machine have this shape, counted that day.
        /// </summary>
        private IEnumerable<string> SearchedFor(string name)
        {
            yield return Path.Combine(WindowsDirectory, "System32", name);
            yield return Path.Combine(WindowsDirectory, "System", name);
            yield return Path.Combine(WindowsDirectory, name);

            foreach (var directory in ManagerEnvironment.SearchPath(WindowsDirectory))
            {
                yield return Path.Combine(directory, name);
            }
        }
    }

    /// <summary>
    /// What follows <c>\??\</c> - the kernel's own prefix for the names a Win32 path can reach.
    ///
    /// A drive letter is returned as an ordinary path, exactly as before 2026-10-05, so the
    /// <c>binaryFile</c> of the four entries on the owner's machine written this way is what older
    /// snapshots hold. <b>Anything else used to be cut loose and joined to the Windows directory</b>
    /// - stability report R-2: <c>\??\UNC\host\share\x.sys</c> became a relative
    /// <c>UNC\host\...</c>, looked for under C:\WINDOWS and reported missing. Now a share is a share,
    /// and every other name keeps its meaning under the Win32 device prefix, where
    /// <see cref="NetworkPath"/> decides whether it may be read.
    /// </summary>
    private static string FromTheObjectNamespace(string rest)
    {
        if (rest.StartsWith(ObjectShare, StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + rest[ObjectShare.Length..];
        }

        return rest.Length > 1 && rest[1] == ':' ? rest : Win32Device + rest;
    }

    private static bool HasExecutableExtension(string path) =>
        ExecutableExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
