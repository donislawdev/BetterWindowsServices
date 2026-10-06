namespace Bws.Core;

/// <summary>
/// Where the window's native libraries are unpacked when it runs with administrator rights, and what
/// the window does about it - security report S-1, the owner's decisions of 2026-10-06.
///
/// <para>
/// <b>Why it matters.</b> The window ships as one file, and the .NET host unpacks five native libraries
/// of WPF out of it into a folder on disk before <c>Main</c>, then loads them from there. By default that
/// folder is under the temporary directory, which on a typical machine any program of the account can
/// write to - and on this machine falls back to <c>C:\Windows\Temp</c>, which other accounts can write
/// to as well. The host does not set who may change the folder, and once a folder exists it is reused
/// without its files being compared to anything (the host's sources, tag <c>v10.0.12</c>, read
/// 2026-10-06). So for a process with administrator rights the folder has to be one only administrators
/// can change, and it has to be one from the moment it is made. Microsoft's own page on single-file
/// deployment says the same: these folders should not be writable by anybody with different privileges.
/// </para>
/// <para>
/// <b>Why the decision can be taken in <c>Main</c> at all - measured, not assumed.</b>
/// <c>tools/security-probe/main-modules.ps1</c>: at the first line of <c>Main</c> no module from that
/// folder is loaded, on a fresh unpacking and on a reused one, and the folder is the one entry of the
/// runtime's <c>NATIVE_DLL_SEARCH_DIRECTORIES</c>. The first library from it arrives with
/// <c>new Application()</c>. So everything here runs before anything from the folder has.
/// </para>
/// <para>
/// <b>The only lever is the host's variable</b> - there is no build setting for the place (H5 in
/// <c>docs/PROJEKT-S1-20261006.md</c>). An elevated window whose libraries are anywhere but the
/// administrators' folder therefore starts itself once more with the variable pointing there. That
/// costs 64-67 ms to the second <c>Main</c> on the machine it was measured on, and only with elevation.
/// </para>
/// </summary>
public static class Unpacking
{
    /// <summary>The variable the .NET host reads for the base of the folder it unpacks into.</summary>
    public const string BaseVariable = "DOTNET_BUNDLE_EXTRACT_BASE_DIR";

    /// <summary>
    /// What an elevated window does about the folder its native libraries came from.
    ///
    /// <para>
    /// <b>No rights, or not one file - nothing to do, and no file is touched.</b> Without elevation
    /// there is nobody with fewer rights to protect against. A window that is not a single-file
    /// bundle - run from <c>bin</c>, a test host, a publish with the libraries beside it - loads them
    /// from where the program lies, and they are trusted exactly as much as the program itself.
    /// </para>
    /// <para>
    /// <b>A folder outside the administrators' folder sends the window elsewhere</b>, once the
    /// administrators' folder is ready. When the variable already points there and the libraries are
    /// still outside it, the host did not do what it was told, and starting again would only start
    /// again - so that refuses instead of looping.
    /// </para>
    /// <para>
    /// <b>Every folder inside it is examined, every time</b>, from the administrators' folder down
    /// to the libraries themselves. A folder that cannot be made, cannot be read, can be changed by
    /// others or is a link refuses the start, with a box saying which and why - never a start with a
    /// sentence, because a start with a sentence would be the way round all of this.
    /// </para>
    /// </summary>
    /// <param name="facts">What this process is and where its libraries came from.</param>
    /// <param name="prepare">Makes the administrators' folder when it is missing, then examines it.</param>
    /// <param name="examine">Examines one folder of libraries inside the administrators' folder.</param>
    public static UnpackingDecision Decide(
        UnpackingFacts facts,
        Func<string, FolderCheck> prepare,
        Func<string, string, FolderCheck> examine)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(prepare);
        ArgumentNullException.ThrowIfNull(examine);

        if (!facts.Elevated || !facts.Bundled)
        {
            return UnpackingDecision.Stay;
        }

        if (facts.SearchDirectories is null || !Path.IsPathFullyQualified(facts.Home))
        {
            // Nothing to examine is not the same as nothing wrong: the runtime always names where it
            // looks for libraries, so a bundle that does not say has changed underneath this code.
            return UnpackingDecision.Refuse(FolderCheck.Failed(facts.Home, UnpackingFault.Unnamed));
        }

        var folders = Folders(facts.SearchDirectories, facts.ProgramFolder);

        if (folders.FirstOrDefault(folder => !Within(folder, facts.Home)) is { } outside)
        {
            return Same(facts.BaseVariable, facts.Home)
                ? UnpackingDecision.Refuse(FolderCheck.Failed(outside, UnpackingFault.StillOutside))
                : UnpackingDecision.Moved(prepare(facts.Home));
        }

        return folders
            .Select(folder => examine(facts.Home, folder))
            .FirstOrDefault(check => check.Fault is not null) is { } failed
            ? UnpackingDecision.Refuse(failed)
            : UnpackingDecision.Stay;
    }

    /// <summary>
    /// The folders the runtime loads native libraries from, other than the program's own - the folder
    /// of unpacking, under this name in the dictionary, <c>docs/03</c>.
    ///
    /// <b>A list, though one entry was measured</b>: the runtime names the property in the plural and
    /// separates it with semicolons, and a second entry from a later runtime must be examined rather
    /// than missed. Each entry is made whole before it is compared, so a base written with ".." in it
    /// is judged by where it leads and not by how it starts.
    /// </summary>
    public static IReadOnlyList<string> Folders(string searchDirectories, string programFolder)
    {
        ArgumentNullException.ThrowIfNull(searchDirectories);

        var program = Whole(programFolder);

        return
        [
            .. searchDirectories
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Whole)
                .Where(folder => !string.Equals(folder, program, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <summary>
    /// Whether a folder is the administrators' folder or lies under it. Compared whole and without
    /// regard to case, the way the file system compares them, and on a separator - so a sibling whose
    /// name only begins with the same letters is outside.
    /// </summary>
    public static bool Within(string folder, string home)
    {
        var whole = Whole(folder);
        var root = Whole(home);

        return string.Equals(whole, root, StringComparison.OrdinalIgnoreCase)
            || whole.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether the host's variable already names the administrators' folder - which is both how a
    /// second start recognises itself and what the window takes back out of its own environment.
    /// </summary>
    public static bool Same(string? variable, string home) =>
        !string.IsNullOrWhiteSpace(variable)
        && string.Equals(Whole(variable), Whole(home), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A path made absolute and without a closing separator. A path with a NUL in it cannot name
    /// anything on disk, and comes back as itself so that it never compares equal to a real folder -
    /// checked here rather than caught, because it is the one thing GetFullPath refuses on Windows.
    /// </summary>
    private static string Whole(string path) =>
        path.Contains('\0', StringComparison.Ordinal) || !Path.IsPathFullyQualified(path)
            ? path
            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}

/// <summary>What an elevated window does about where its libraries came from.</summary>
public enum UnpackingVerdict
{
    /// <summary>Run here: no elevation, not one file, or every folder examined and only administrators can change it.</summary>
    Stay,

    /// <summary>Start once more with the libraries unpacked into the administrators' folder.</summary>
    Elsewhere,

    /// <summary>Do not start - the box says which folder and why.</summary>
    Refused
}

/// <summary>Why a folder of libraries was not good enough to load anything from with administrator rights.</summary>
public enum UnpackingFault
{
    /// <summary>The administrators' folder could not be made, or the folder it lives in is missing.</summary>
    NotMade,

    /// <summary>Somebody who is not SYSTEM, an administrator or TrustedInstaller can change it.</summary>
    OthersCanChange,

    /// <summary>It is a link, so what is loaded is decided by wherever the link points.</summary>
    Link,

    /// <summary>Who may change it could not be read, so it cannot be said to be safe.</summary>
    Unreadable,

    /// <summary>The libraries were unpacked outside the folder the host had been told to use.</summary>
    StillOutside,

    /// <summary>The runtime did not say where it unpacked the libraries.</summary>
    Unnamed,

    /// <summary>The window could not start itself once more.</summary>
    NotStarted
}

/// <summary>
/// One folder examined: which, and what is wrong with it - nothing, when <see cref="Fault"/> is null.
/// <see cref="Cause"/> is what the system said, when it said anything.
/// </summary>
public sealed record FolderCheck(string Path, UnpackingFault? Fault, string? Cause = null)
{
    public static FolderCheck Passed(string path) => new(path, null);

    public static FolderCheck Failed(string path, UnpackingFault fault, string? cause = null) => new(path, fault, cause);
}

/// <summary>The verdict, with the folder it is about - the administrators' folder when the window moves.</summary>
public sealed record UnpackingDecision(UnpackingVerdict Verdict, FolderCheck? Check)
{
    public static UnpackingDecision Stay { get; } = new(UnpackingVerdict.Stay, null);

    public static UnpackingDecision Refuse(FolderCheck check) => new(UnpackingVerdict.Refused, check);

    /// <summary>Elsewhere when the administrators' folder is ready, refused when it is not.</summary>
    public static UnpackingDecision Moved(FolderCheck prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);

        return new(prepared.Fault is null ? UnpackingVerdict.Elsewhere : UnpackingVerdict.Refused, prepared);
    }
}

/// <summary>
/// What <see cref="Unpacking.Decide"/> is handed. <see cref="SearchDirectories"/> is the runtime's
/// <c>NATIVE_DLL_SEARCH_DIRECTORIES</c>, <see cref="BaseVariable"/> this process's own
/// <see cref="Unpacking.BaseVariable"/>.
/// </summary>
public sealed record UnpackingFacts(
    bool Elevated,
    bool Bundled,
    string? SearchDirectories,
    string ProgramFolder,
    string Home,
    string? BaseVariable);
