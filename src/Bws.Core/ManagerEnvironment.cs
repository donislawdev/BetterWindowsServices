namespace Bws.Core;

/// <summary>
/// The environment a service is started in, which is not the environment of this process.
///
/// <para>
/// <b>Moved out of <see cref="BinaryPathResolver"/> on 2026-10-05</b>, when stability report R-3
/// needed the machine's PATH as well as its variables and the resolver would have crossed the size
/// ratchet with both. It belongs apart anyway: the resolver decides which file a command names, and
/// this answers what the manager would see while it does.
/// </para>
/// </summary>
internal static class ManagerEnvironment
{
    /// <summary>
    /// Expands <c>%NAME%</c> the way the manager does, which is not the way this process does.
    ///
    /// <para>
    /// <c>Environment.ExpandEnvironmentVariables</c>, which stood in the resolver until 2026-09-08,
    /// reads the block this process started with - and that block is the machine's with the
    /// signed-in user's laid over the top. A service gets no such layer. Measured on the machine this
    /// was written against: <c>TEMP</c> is the Windows directory's own for the machine and a folder
    /// inside the signed-in account's profile for the user, so one entry would have resolved to two
    /// different files for two people, and their snapshots would have differed with nothing on the
    /// machine having changed. That is a false drift, which is the one failure this tool exists to
    /// not produce.
    ///
    /// <i>Said in words rather than shown as two paths, and not for brevity:</i> a profile path
    /// written out carries the account name of whoever wrote the line, and
    /// <c>PublicSurfaceGuards.No_shape_that_belongs_to_a_person_is_written_into_a_published_file</c>
    /// refuses that shape in anything the repository publishes. It refused this comment on the day
    /// it was written, with a placeholder standing where the name would go - which is the guard
    /// being right rather than fussy, because the next person to edit the line would have had a
    /// real path in front of them to copy.
    /// </para>
    /// <para>
    /// <b>Reading only the machine's block is not the fix, and this is the part worth keeping.</b>
    /// Measured the same day across 786 entries carrying a launch command: 270 hold a percent sign,
    /// and the names in them are <c>%SystemRoot%</c> 264 times, <c>%windir%</c> 3,
    /// <c>%ProgramFiles%</c> 2 and <c>%ProgramData%</c> 1. Of those four, <b>only windir is in the
    /// machine's block</b> - the system injects the rest into every process instead. So the obvious
    /// correction would have left a third of the machine unresolved, and it would have looked like
    /// the tool had lost the files.
    /// </para>
    /// <para>
    /// Hence three rules rather than one argument changed. The Windows directory answers for itself,
    /// because the caller already hands it in and the <c>\SystemRoot\</c> spelling has always used
    /// it - the two spellings of one idea agree. Any other name prefers the machine's value, which is
    /// what a service would see. Falling back to this process is right for what is left, because a
    /// name absent from the machine's block is one the system gives every process alike. A name that
    /// resolves to nothing stays as written, exactly as the framework left it.
    /// </para>
    /// </summary>
    internal static string Expand(string value, string windowsDirectory)
    {
        var built = new System.Text.StringBuilder(value.Length);
        var at = 0;

        while (at < value.Length)
        {
            var opened = value.IndexOf('%', at);
            var closed = opened < 0 ? -1 : value.IndexOf('%', opened + 1);

            if (closed < 0)
            {
                // An odd percent sign is a character in a file name, not a variable opening.
                built.Append(value, at, value.Length - at);
                break;
            }

            built.Append(value, at, opened - at);

            var name = value[(opened + 1)..closed];
            built.Append(ValueTheManagerWouldSee(name, windowsDirectory) ?? value[opened..(closed + 1)]);

            at = closed + 1;
        }

        return built.ToString();
    }

    /// <summary>
    /// The directories of the machine's PATH, in order, for a command naming no directory at all -
    /// the last place CreateProcess looks (stability report R-3).
    ///
    /// <b>The machine's PATH only, never this process's</b>, for the reason <see cref="Expand"/>
    /// gives: this process carries the signed-in user's PATH laid over the machine's, and a service
    /// does not. A machine block that could not be read therefore gives no directories rather than
    /// falling back - an empty answer here costs a "missing" for a file only PATH would find, and a
    /// fallback would cost a file found where no service would ever look.
    ///
    /// NOT CHECKED: services.exe takes its environment when it starts, so a PATH edited since the
    /// last boot is what this reads and not what the manager would use. The window between the two
    /// is a property of the machine, not something this code can close.
    /// </summary>
    internal static IEnumerable<string> SearchPath(string windowsDirectory)
    {
        if (!MachineBlock.Value.TryGetValue("Path", out var path))
        {
            yield break;
        }

        foreach (var directory in path.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return directory.Contains('%', StringComparison.Ordinal) ? Expand(directory, windowsDirectory) : directory;
        }
    }

    /// <param name="name">
    /// May be empty, from a literal <c>%%</c>. Asking the framework for a variable of no name
    /// throws, so that case is answered here rather than reached.
    /// </param>
    private static string? ValueTheManagerWouldSee(string name, string windowsDirectory)
    {
        if (name.Length == 0)
        {
            return null;
        }

        if (name.Equals("SystemRoot", StringComparison.OrdinalIgnoreCase)
            || name.Equals("windir", StringComparison.OrdinalIgnoreCase))
        {
            return windowsDirectory;
        }

        return MachineBlock.Value.TryGetValue(name, out var onTheMachine)
            ? onTheMachine
            : Environment.GetEnvironmentVariable(name);
    }

    /// <summary>
    /// The machine's own environment, read once for the life of the process.
    ///
    /// <para>
    /// Read once because the alternative is a registry open per variable per entry, and a full
    /// listing resolves several hundred of them from <see cref="System.Threading.Tasks.Parallel"/>
    /// loops. Reading once also makes a listing self-consistent: an environment edited midway
    /// through cannot make two entries in one snapshot disagree about the same variable.
    /// </para>
    /// <para>
    /// An unreadable machine block is not an error to report. Under a restricted token the read
    /// can be refused, and the honest answer there is the process's own block - the same one this
    /// code used before, so a refusal costs the correction and nothing else. It must not cost the
    /// listing.
    /// </para>
    /// </summary>
    private static readonly Lazy<Dictionary<string, string>> MachineBlock = new(() =>
    {
        var read = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var block = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);

            foreach (var key in block.Keys)
            {
                if (key is string name && block[name] is string value)
                {
                    read[name] = value;
                }
            }
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException)
        {
            // Left empty on purpose. Every lookup then falls through to this process, which is
            // where every lookup went before this method existed.
        }

        return read;
    });
}
