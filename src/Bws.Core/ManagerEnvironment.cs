using Microsoft.Win32;

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
/// <para>
/// <b>NOTHING HERE READS THIS PROCESS'S ENVIRONMENT, SINCE 2026-10-06 - security report S-3.</b> That
/// environment is laid out by the account running the tool, and an elevated process inherits it -
/// measured that day: every value in the account's own environment was present in an elevated
/// process. A variable named ProgramFiles set for that account changed which file this tool
/// described, signed and hashed, while the manager went on starting the other one. That is a false
/// audit answer chosen by whoever can write to the account's environment, which is a process with no
/// administrator rights at all.
/// </para>
/// </summary>
internal static class ManagerEnvironment
{
    /// <summary>
    /// How many machine variables may name one another before the innermost stays as written. The
    /// machine's own block nests one level (TEMP is <c>%SystemRoot%\TEMP</c>), so eight is generous,
    /// and a finite number is what ends two variables that name each other.
    /// </summary>
    private const int Nesting = 8;

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
    /// refuses that shape in anything the repository publishes.
    /// </para>
    /// <para>
    /// <b>Measured 2026-09-08 across 786 entries carrying a launch command:</b> 270 hold a percent
    /// sign, and the names in them are <c>%SystemRoot%</c> 264 times, <c>%windir%</c> 3,
    /// <c>%ProgramFiles%</c> 2 and <c>%ProgramData%</c> 1. Only windir of those four is in the
    /// machine's block - the system injects the rest into every process instead. Until 2026-10-06
    /// those injected names were read from this process, and that was the hole S-3 is about.
    /// </para>
    /// <para>
    /// <b>Hence a table of trusted sources, measured rather than assumed.</b> A child process with
    /// windir, SystemRoot, ProgramFiles, ProgramData, ALLUSERSPROFILE and SystemDrive replaced in its
    /// own environment, 2026-10-06: <c>Environment.GetFolderPath</c> for the Windows, ProgramFiles and
    /// CommonProgramFiles folders answered the real folders, and for CommonApplicationData it answered
    /// the replaced one. So the first three come from that call and ProgramData does not.
    /// </para>
    /// </summary>
    internal static string Expand(string value, string windowsDirectory) =>
        Expand(value, windowsDirectory, Nesting);

    /// <summary>
    /// Whether a written command names a variable this code cannot answer for.
    ///
    /// <para>
    /// <b>Left as written, exactly as the manager leaves a name it does not know</b> - and then the
    /// file is looked for under that literal name, because a service can genuinely be launched from
    /// one. What changes is the answer when nothing is there: the resolver says NOT READ rather than
    /// missing, because a variable this table does not know may still be one the manager does, and
    /// "missing" would then be a false finding about a file this code never located. A name the
    /// manager also does not know is the price - its file reads as not read rather than missing.
    /// </para>
    /// <para>
    /// Asked of the written command, with the same pairing <see cref="Expand(string, string)"/>
    /// uses, so the two cannot disagree about where a name starts. An empty name - a literal
    /// <c>%%</c> - is not a variable and does not count.
    /// </para>
    /// </summary>
    internal static bool Unresolved(string value, string windowsDirectory) =>
        Pairs(value).Any(pair =>
            pair.Closed > pair.Opened + 1
            && ValueTheManagerWouldSee(value[(pair.Opened + 1)..pair.Closed], windowsDirectory, Nesting) is null);

    /// <summary>
    /// The directories of the machine's PATH, in order, for a command naming no directory at all -
    /// the last place CreateProcess looks (stability report R-3).
    ///
    /// <b>The machine's PATH only, never this process's</b>, for the reason <see cref="Expand(string, string)"/>
    /// gives: this process carries the signed-in user's PATH laid over the machine's, and a service
    /// does not. <b>And read as written since 2026-10-06</b> - the framework's own reading of the
    /// machine block expands what it reads with THIS process's variables, measured that day with
    /// SystemRoot replaced in a child process, so the PATH a service would search was being laid out
    /// by the account running the tool. A machine block that could not be read gives no directories
    /// rather than falling back - an empty answer here costs a "missing" for a file only PATH would
    /// find, and a fallback would cost a file found where no service would ever look.
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

    private static string Expand(string value, string windowsDirectory, int depth)
    {
        var built = new System.Text.StringBuilder(value.Length);
        var at = 0;

        foreach (var (opened, closed) in Pairs(value))
        {
            built.Append(value, at, opened - at);

            var name = value[(opened + 1)..closed];
            built.Append(ValueTheManagerWouldSee(name, windowsDirectory, depth) ?? value[opened..(closed + 1)]);

            at = closed + 1;
        }

        // An odd percent sign after the last pair is a character in a file name, not a variable
        // opening, and it is carried over with the rest.
        return built.Append(value, at, value.Length - at).ToString();
    }

    /// <summary>Where each <c>%NAME%</c> opens and closes, left to right, the way the manager pairs them.</summary>
    private static IEnumerable<(int Opened, int Closed)> Pairs(string value)
    {
        var at = 0;

        while (at < value.Length)
        {
            var opened = value.IndexOf('%', at);
            var closed = opened < 0 ? -1 : value.IndexOf('%', opened + 1);

            if (closed < 0)
            {
                yield break;
            }

            yield return (opened, closed);
            at = closed + 1;
        }
    }

    /// <param name="name">
    /// May be empty, from a literal <c>%%</c>, which names nothing and stays as written.
    /// </param>
    /// <param name="depth">
    /// How many more machine variables may be followed - see <see cref="Nesting"/>.
    /// </param>
    private static string? ValueTheManagerWouldSee(string name, string windowsDirectory, int depth)
    {
        if (name.Length == 0)
        {
            return null;
        }

        if (Injected.TryGetValue(name, out var injected))
        {
            return injected(windowsDirectory);
        }

        return depth > 0 && MachineBlock.Value.TryGetValue(name, out var written)
            ? Expand(written, windowsDirectory, depth - 1)
            : null;
    }

    private sealed record KnownFolders(string? ProgramFiles, string? ProgramFilesX86, string? CommonFiles, string? CommonFilesX86);

    /// <summary>
    /// Declared before <see cref="Injected"/> rather than beside its one reader, because a static
    /// initializer sees the fields declared after it as not yet set - the build refused the other
    /// order with a possible null on every line that reads this.
    /// </summary>
    private static readonly Lazy<KnownFolders> Folders = new(() => new KnownFolders(
        Folder(Environment.SpecialFolder.ProgramFiles),
        Folder(Environment.SpecialFolder.ProgramFilesX86),
        Folder(Environment.SpecialFolder.CommonProgramFiles),
        Folder(Environment.SpecialFolder.CommonProgramFilesX86)));

    /// <summary>A folder, or null where the system has none - an empty answer names no place.</summary>
    private static string? Folder(Environment.SpecialFolder folder) =>
        Environment.GetFolderPath(folder) is { Length: > 0 } path ? path : null;

    /// <summary>
    /// The names the system gives every process rather than keeping in the machine's block, each
    /// from a source that does not follow this process's environment - see
    /// <see cref="Expand(string, string)"/> for the measurement behind each choice.
    ///
    /// <b>ProgramData and PUBLIC are read from the registry, where an API exists</b>, and that is a
    /// deliberate exception to reading the system through its API: the API for ProgramData was
    /// measured following this process's environment, which is the thing being avoided. The
    /// registry holds them as written, with <c>%SystemDrive%</c> inside, and that name is answered
    /// here from the Windows directory, never from the environment.
    ///
    /// The two x86 folders were measured the same day as the rest and do not follow it either.
    /// tools/security-probe/env-sources.ps1 takes the whole measurement again, against this code.
    /// </summary>
    private static readonly Dictionary<string, Func<string, string?>> Injected =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = windows => windows,
            ["windir"] = windows => windows,
            ["SystemDrive"] = windows => Path.GetPathRoot(windows)?.TrimEnd('\\'),
            ["ProgramFiles"] = _ => Folders.Value.ProgramFiles,
            ["ProgramW6432"] = _ => Folders.Value.ProgramFiles,
            ["ProgramFiles(x86)"] = _ => Folders.Value.ProgramFilesX86,
            ["CommonProgramFiles"] = _ => Folders.Value.CommonFiles,
            ["CommonProgramW6432"] = _ => Folders.Value.CommonFiles,
            ["CommonProgramFiles(x86)"] = _ => Folders.Value.CommonFilesX86,
            ["ProgramData"] = windows => Profiles("ProgramData", windows),
            ["ALLUSERSPROFILE"] = windows => Profiles("ProgramData", windows),
            ["PUBLIC"] = windows => Profiles("Public", windows),
        };

    /// <summary>
    /// A folder the profile list holds as written, expanded with the injected names only - depth 0,
    /// so no machine variable is followed from here.
    /// </summary>
    private static string? Profiles(string name, string windowsDirectory) =>
        ProfileList.Value.TryGetValue(name, out var written)
            ? Expand(written, windowsDirectory, depth: 0)
            : null;

    /// <summary>
    /// The machine's own environment, AS WRITTEN, read once for the life of the process.
    ///
    /// <para>
    /// Read once because the alternative is a registry open per variable per entry, and a full
    /// listing resolves several hundred of them from <see cref="System.Threading.Tasks.Parallel"/>
    /// loops. Reading once also makes a listing self-consistent: an environment edited midway
    /// through cannot make two entries in one snapshot disagree about the same variable.
    /// </para>
    /// <para>
    /// <b>As written, and that is the S-3 repair as much as anything above.</b> Until 2026-10-06 this
    /// was <c>Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine)</c>, which
    /// expands every expandable value it reads with this process's own variables - measured that
    /// day: with SystemRoot replaced in a child process, the machine's TEMP and windir came back
    /// under the replacement. Read raw, each value is expanded by <see cref="Expand(string, string)"/>
    /// like any other.
    /// </para>
    /// </summary>
    private static readonly Lazy<Dictionary<string, string>> MachineBlock =
        new(() => AsWritten(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment"));

    private static readonly Lazy<Dictionary<string, string>> ProfileList =
        new(() => AsWritten(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList"));

    /// <summary>
    /// The text values of one key of the machine's registry, none of them expanded.
    ///
    /// <para>
    /// <b>A refusal is not an error to report, and since 2026-10-06 it no longer falls back.</b>
    /// Under a restricted token the read can be refused, and until that day the honest answer here
    /// was taken to be this process's own block. It is not honest once that block is somebody else's
    /// to write. An unreadable key now answers nothing, the names it would have held stay as
    /// written, and <see cref="Unresolved"/> turns a file that is then not found into not read rather
    /// than missing - so the refusal costs the answer, visibly, and never borrows one.
    /// </para>
    /// </summary>
    private static Dictionary<string, string> AsWritten(string keyPath)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);

            return (key?.GetValueNames() ?? [])
                .Select(name => (Name: name, Value: key!.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string))
                .Where(pair => pair.Value is not null)
                .ToDictionary(pair => pair.Name, pair => pair.Value!, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException)
        {
            // Left empty on purpose - the summary says why a refusal must not borrow this process's
            // environment, and what it costs instead.
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
