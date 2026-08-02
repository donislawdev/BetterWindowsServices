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
    /// <param name="Found">Whether that file is there. False whenever <paramref name="File"/> is null.</param>
    public readonly record struct ResolvedBinary(string? File, bool Found);

    /// <param name="command">The launch command exactly as the manager returns it.</param>
    /// <param name="serviceName">Needed only for a driver that names no file of its own.</param>
    /// <param name="isDriver">Drivers have a default the manager applies for them.</param>
    /// <param name="windowsDirectory">What a relative path and <c>\SystemRoot\</c> are relative to.</param>
    /// <param name="exists">
    /// Asks whether a candidate is on disk. Handed in rather than called directly, so the
    /// rules above can be tested without a file system arranged to suit them.
    /// </param>
    public static ResolvedBinary Resolve(
        string? command,
        string serviceName,
        bool isDriver,
        string windowsDirectory,
        Func<string, bool> exists)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            // A driver that names no file runs the one the manager assumes for it. Anything
            // else naming no file leaves us with nothing to resolve, and saying so is better
            // than inventing a path in order to report it missing.
            if (!isDriver)
            {
                return new ResolvedBinary(null, Found: false);
            }

            var assumed = Path.Combine(windowsDirectory, "System32", "drivers", serviceName + ".sys");

            return new ResolvedBinary(assumed, exists(assumed));
        }

        var trimmed = command.Trim();

        if (trimmed[0] == '"')
        {
            // Quotes settle it. Whoever wrote them said where the file name ends, which is
            // the entire reason quoting an image path is worth doing.
            var close = trimmed.IndexOf('"', 1);
            var quoted = Absolute(close > 0 ? trimmed[1..close] : trimmed[1..], windowsDirectory);

            return new ResolvedBinary(quoted, exists(quoted));
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

        // Ordinal by construction: searching for a character has no cultural reading, and
        // the overload taking a start index has no comparison parameter to pass one to.
        for (var space = trimmed.IndexOf(' ', StringComparison.Ordinal); ;
             space = trimmed.IndexOf(' ', space + 1))
        {
            var candidate = Absolute(space < 0 ? trimmed : trimmed[..space], windowsDirectory);

            if (exists(candidate))
            {
                return new ResolvedBinary(candidate, Found: true);
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
        return new ResolvedBinary(executableLooking ?? Absolute(trimmed, windowsDirectory), Found: false);
    }

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
