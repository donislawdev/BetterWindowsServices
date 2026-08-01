using System.Text;

namespace Bws.Core.Snapshots;

/// <summary>
/// Writes a file the user cares about, or leaves what was there alone.
///
/// `ADR-18`, and the reason it is a decision rather than a habit: a snapshot interrupted
/// halfway is **worse than no snapshot at all**. It looks like data, it is not data, and in
/// an audit tool it goes into a comparison as a false truth. The same goes for
/// configuration - somebody who lost their tags because a process died mid-write does not
/// come back to the tool.
///
/// So the content is written beside the target and then moved over it in one step. A
/// process that dies at any point leaves either the old file or a temporary one, never a
/// half-written target. On success nothing temporary remains.
///
/// One path for every file the tool writes, built once here rather than separately in each
/// place that writes something. `ADR-18` says that too, and the reason is that the second
/// implementation is the one that forgets a step.
///
/// **Directories are not invented.** Writing into a directory that does not exist is an
/// error and stays one, because a tool that creates a tree from a mistyped path has
/// scattered somebody's files somewhere they will not look.
/// </summary>
public static class AtomicFile
{
    /// <summary>
    /// UTF-8 without a byte order mark, and Unix line endings.
    ///
    /// Both so that the file diffs cleanly, which is what `ADR-6` promises. A mark at the
    /// front makes the first line differ from every tool's idea of the first line, and
    /// carriage returns make a file written here differ from the same file written on
    /// anything else - neither of which is a fact about any service.
    /// </summary>
    private static readonly UTF8Encoding Encoding = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Puts the text at the path, atomically.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    public static void Write(string path, string content)
    {
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full);

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                $"There is no directory at '{directory}', so nothing was written to '{full}'.");
        }

        // Beside the target rather than in the system temporary directory, because the move
        // at the end has to be a rename within one volume to be a single step. Across
        // volumes it becomes a copy and a delete, which is exactly the halfway state this
        // exists to prevent.
        var beside = Path.Combine(directory, $".{Path.GetFileName(full)}.{Guid.NewGuid():N}.tmp");

        // Written beside and moved over, never straight into the target. No test in this
        // project can tell the difference - verified by replacing these two lines with a
        // direct write, which left all 383 of them green - because the gap only exists while
        // the process is inside the write, and that is microseconds wide. What is guarded is
        // the observable half: that nothing temporary survives a successful write.
        try
        {
            File.WriteAllText(beside, content, Encoding);
            File.Move(beside, full, overwrite: true);
        }
        catch
        {
            // The target is untouched either way. What must not be left behind is the
            // temporary file, which would otherwise accumulate beside somebody's snapshots
            // with a name they cannot explain.
            Delete(beside);
            throw;
        }
    }

    /// <summary>
    /// Moves a file aside instead of deleting it, and says where it went.
    ///
    /// For the file that cannot be read back. `ADR-18` asks for quarantine rather than
    /// deletion, and the reason is that a corrupt snapshot is sometimes the only remaining
    /// trace of what was there - overwriting it destroys evidence in a tool whose whole
    /// purpose is keeping evidence.
    /// </summary>
    public static string Quarantine(string path, IClock clock)
    {
        var full = Path.GetFullPath(path);
        var stamp = clock.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var aside = $"{full}.unreadable-{stamp}";

        // A second failure in the same second would otherwise overwrite the first file set
        // aside, which is the one thing this must never do.
        for (var attempt = 2; File.Exists(aside); attempt++)
        {
            aside = $"{full}.unreadable-{stamp}-{attempt}";
        }

        File.Move(full, aside);

        return aside;
    }

    private static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Already gone, or held by something. Either way the original failure is what
            // the caller needs to hear about, and losing it behind a tidying-up error would
            // replace a real reason with a meaningless one.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
