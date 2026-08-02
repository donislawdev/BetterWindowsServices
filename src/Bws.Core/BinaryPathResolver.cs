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
/// </summary>
public static class BinaryPathResolver
{
    private const string ObjectPrefix = @"\??\";
    private const string SystemRootPrefix = @"\SystemRoot\";

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
    /// are now three answers, not two: it is there, it is not, and <b>nobody looked</b> - the
    /// last of those for a path on another machine when the caller did not ask to go off this
    /// one. Collapsing "did not look" into "not there" would be rule 8 broken in the field
    /// where it costs most: an audit tool reporting a file as missing when it never checked.
    /// </param>
    public readonly record struct ResolvedBinary(string? File, Reading<bool> OnDisk);

    /// <param name="command">The launch command exactly as the manager returns it.</param>
    /// <param name="serviceName">Needed only for a driver that names no file of its own.</param>
    /// <param name="isDriver">Drivers have a default the manager applies for them.</param>
    /// <param name="windowsDirectory">What a relative path and <c>\SystemRoot\</c> are relative to.</param>
    /// <param name="exists">
    /// Asks whether a candidate is on disk. Handed in rather than called directly, so the
    /// rules above can be tested without a file system arranged to suit them.
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
        Func<string, bool> exists,
        NetworkPaths networkPaths)
    {
        // No default value on the parameter above, on purpose. A default would let a new call
        // site reach off the machine by saying nothing, which is exactly how the behaviour
        // this replaces went unnoticed for six slices.
        bool OffLimits(string candidate) =>
            networkPaths == NetworkPaths.Skip && NetworkPath.LeavesThisMachine(candidate);

        if (string.IsNullOrWhiteSpace(command))
        {
            // A driver that names no file runs the one the manager assumes for it. Anything
            // else naming no file leaves us with nothing to resolve, and saying so is better
            // than inventing a path in order to report it missing.
            if (!isDriver)
            {
                return new ResolvedBinary(null, Reading<bool>.Absent());
            }

            var assumed = Path.Combine(windowsDirectory, "System32", "drivers", serviceName + ".sys");

            return new ResolvedBinary(assumed, Look(assumed, exists, OffLimits));
        }

        var trimmed = command.Trim();

        if (trimmed[0] == '"')
        {
            // Quotes settle it. Whoever wrote them said where the file name ends, which is
            // the entire reason quoting an image path is worth doing.
            var close = trimmed.IndexOf('"', 1);
            var quoted = Absolute(close > 0 ? trimmed[1..close] : trimmed[1..], windowsDirectory);

            return new ResolvedBinary(quoted, Look(quoted, exists, OffLimits));
        }

        // No quotes and possibly spaces, so where the file name ends is genuinely ambiguous.
        // Every prefix is tried, shortest first, and the first one on disk is the answer.
        //
        // Cutting at the first space is the obvious alternative and it is wrong: it turned
        // both of the entries on the machine with this shape into "C:\Program" and reported
        // files that are there as missing.
        //
        // NOT MEASURED: whether the manager resolves such a command in exactly this order.
        // It only decides anything when two prefixes both exist, which no entry on the
        // machine does, and finding out would mean planting a file and starting a service.
        // Naming the ambiguity as a finding of its own is C5 of the specification, and is
        // deliberately not done here.
        string? executableLooking = null;
        var lookedAway = false;

        // Ordinal by construction: searching for a character has no cultural reading, and
        // the overload taking a start index has no comparison parameter to pass one to.
        for (var space = trimmed.IndexOf(' ', StringComparison.Ordinal); ;
             space = trimmed.IndexOf(' ', space + 1))
        {
            var candidate = Absolute(space < 0 ? trimmed : trimmed[..space], windowsDirectory);

            // Off this machine, and nobody asked to go there. Every prefix of one command
            // shares a root, so this is the same answer each time round - but it is asked per
            // candidate rather than once, because Absolute can turn a candidate into
            // something else entirely, and a rule that holds "by construction" is the kind
            // that stops holding when somebody adds a seventh path shape.
            if (OffLimits(candidate))
            {
                lookedAway = true;
            }
            else if (exists(candidate))
            {
                return new ResolvedBinary(candidate, Reading<bool>.Present(true));
            }

            executableLooking ??= HasExecutableExtension(candidate) ? candidate : null;

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
            executableLooking ?? Absolute(trimmed, windowsDirectory),
            lookedAway ? Reading<bool>.NotRead() : Reading<bool>.Present(false));
    }

    /// <summary>
    /// One question about the disk, asked only when it is allowed to be asked.
    /// </summary>
    private static Reading<bool> Look(string candidate, Func<string, bool> exists, Func<string, bool> offLimits) =>
        offLimits(candidate)
            ? Reading<bool>.NotRead()
            : Reading<bool>.Present(exists(candidate));

    /// <summary>
    /// One candidate, turned into a path that can be looked for.
    ///
    /// The order of these tests is not interchangeable. A path beginning with a single
    /// backslash is rooted as far as the platform is concerned, so asking "is it rooted"
    /// first would let <c>\SystemRoot\System32\drivers\x.sys</c> through untouched and it
    /// would be looked for on a drive it is not on.
    /// </summary>
    private static string Absolute(string candidate, string windowsDirectory)
    {
        var value = candidate.Trim();

        if (value.Contains('%', StringComparison.Ordinal))
        {
            value = Environment.ExpandEnvironmentVariables(value);
        }

        if (value.StartsWith(ObjectPrefix, StringComparison.Ordinal))
        {
            // The kernel's own way of naming a path. What follows is an ordinary one.
            return value[ObjectPrefix.Length..];
        }

        if (value.StartsWith(SystemRootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(windowsDirectory, value[SystemRootPrefix.Length..]);
        }

        if (value.StartsWith(@"\\", StringComparison.Ordinal))
        {
            // A share. Left alone: it is already absolute, and joining it to anything would
            // produce a path that names nothing.
            return value;
        }

        if (value.StartsWith('\\'))
        {
            // Rooted, but on no stated drive. The one it means is the one Windows is on.
            return Path.Combine(Path.GetPathRoot(windowsDirectory) ?? @"C:\", value[1..]);
        }

        return value.Length > 1 && value[1] == ':'
            ? value
            : Path.Combine(windowsDirectory, value);
    }

    private static bool HasExecutableExtension(string path) =>
        ExecutableExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
