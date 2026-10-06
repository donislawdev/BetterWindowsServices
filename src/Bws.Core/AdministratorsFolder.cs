using System.Security.AccessControl;
using System.Security.Principal;

namespace Bws.Core;

/// <summary>
/// A folder nobody can change but SYSTEM, the Administrators group and TrustedInstaller - the place an
/// elevated window unpacks its native libraries into, and the rule every level of it is held to.
/// Security report S-1, the owner's decisions of 2026-10-06, and the entry for this name in the
/// dictionary, <c>docs/03</c>.
///
/// <para>
/// <b>Who can change it is the whole question, and it is asked of the security descriptor rather than
/// of the path.</b> The owner must be one of the three - or be held back by an OWNER RIGHTS entry, which
/// takes away the implicit right of an owner to rewrite the list. No entry that allows anything may give
/// anybody else the right to write, append, change attributes, delete, delete what is inside, or change
/// the permissions or the owner. Entries that deny are not counted in its favour, and an entry of a kind
/// this does not read is not counted as harmless. And it must not be a link of any kind.
/// </para>
/// <para>
/// <b>Asked again at every start, never remembered.</b> The host reuses a folder it finds without
/// comparing anything, so a check made once and trusted afterwards would be a check of a folder that
/// may no longer be the one in use.
/// </para>
/// </summary>
public static class AdministratorsFolder
{
    /// <summary>
    /// <c>&lt;drive of Windows&gt;\ProgramData\BetterWindowsServices</c>.
    ///
    /// <b>From the drive of the Windows folder, not from the environment.</b> GetFolderPath for the
    /// common application data follows the process's own environment, which an elevated process inherits
    /// from the account that started it - and GetFolderPath for Windows does not. Both measured with
    /// <c>tools/security-probe/env-sources.ps1</c> on 2026-10-06. ProgramData itself is named rather than
    /// looked up for the same reason, and it is not localised.
    /// </summary>
    public static string Home { get; } = HomeOn(Environment.GetFolderPath(Environment.SpecialFolder.Windows));

    internal static string HomeOn(string windows) =>
        Path.GetPathRoot(windows) is { Length: > 0 } drive
            ? Path.Combine(drive, "ProgramData", "BetterWindowsServices")
            : string.Empty;

    private static readonly SecurityIdentifier System = new(WellKnownSidType.LocalSystemSid, null);
    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly SecurityIdentifier TrustedInstaller = new("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464");

    /// <summary>OWNER RIGHTS. Present on an object, it replaces what its owner may do implicitly.</summary>
    private static readonly SecurityIdentifier OwnerRights = new("S-1-3-4");

    private static readonly SecurityIdentifier[] Trusted = [System, Administrators, TrustedInstaller];

    /// <summary>
    /// Every right that changes a folder, a file or who may change them. The generic bits are here
    /// because an entry can carry them unmapped, and MAXIMUM_ALLOWED because it asks for everything.
    /// </summary>
    private const int Changing =
        (int)(FileSystemRights.WriteData | FileSystemRights.AppendData | FileSystemRights.WriteExtendedAttributes
            | FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.WriteAttributes | FileSystemRights.Delete
            | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership)
        | 0x10000000 // GENERIC_ALL
        | 0x40000000 // GENERIC_WRITE
        | 0x02000000; // MAXIMUM_ALLOWED

    /// <summary>
    /// Whether nobody but SYSTEM, the Administrators group and TrustedInstaller can change what this
    /// descriptor protects. A pure question, so a test can hand it any descriptor written in SDDL.
    /// </summary>
    public static bool OnlyAdministratorsCanChange(RawSecurityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        // No list at all means everybody may do everything.
        if (descriptor.DiscretionaryAcl is not { } list)
        {
            return false;
        }

        var ownerTrusted = descriptor.Owner is { } owner && Trusted.Contains(owner);
        var ownerHeldBack = list.OfType<CommonAce>().Any(entry =>
            entry.SecurityIdentifier == OwnerRights && !entry.AceFlags.HasFlag(AceFlags.InheritOnly));

        return (ownerTrusted || ownerHeldBack) && list.Cast<GenericAce>().All(entry => Harmless(entry, ownerTrusted));
    }

    private static bool Harmless(GenericAce entry, bool ownerTrusted) => entry switch
    {
        CommonAce { AceQualifier: AceQualifier.AccessDenied } => true,
        CommonAce { AceQualifier: AceQualifier.AccessAllowed } allowed =>
            (allowed.AccessMask & Changing) == 0
            || Trusted.Contains(allowed.SecurityIdentifier)
            || (ownerTrusted && allowed.SecurityIdentifier == OwnerRights),
        _ => false
    };

    /// <summary>
    /// The list the administrators' folder is made with: SYSTEM and Administrators may do everything,
    /// whoever owns something inside may only read it, nothing is inherited from the folder above, and
    /// the owner is the Administrators group.
    ///
    /// <b>The OWNER RIGHTS entry carries weight and is not decoration.</b> The host unpacks inside this
    /// folder as the elevated process, and what it creates is owned by whatever that token names as
    /// owner. Were that ever the account rather than the group, the owner's implicit right to rewrite
    /// the list would belong to the same account running without elevation. Which one Windows picks was
    /// not measured - with this entry the rule above passes either way.
    ///
    /// <b>Nothing for the users of the machine, not even reading</b> - a window without rights unpacks
    /// into its own temporary folder and never comes here.
    /// </summary>
    public static DirectorySecurity Closed()
    {
        var security = new DirectorySecurity();

        security.SetOwner(Administrators);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        foreach (var (who, rights) in new[]
                 {
                     (System, FileSystemRights.FullControl),
                     (Administrators, FileSystemRights.FullControl),
                     (OwnerRights, FileSystemRights.ReadAndExecute)
                 })
        {
            security.AddAccessRule(new FileSystemAccessRule(
                who,
                rights,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        return security;
    }

    /// <summary>
    /// Makes the administrators' folder with <see cref="Closed"/> when it is not there, then examines it.
    ///
    /// <b>Examined after making it, always.</b> Anybody may make folders in ProgramData, and a folder
    /// somebody made first is not an error to Create - it simply gets no list. So whatever is there
    /// afterwards is what is judged, made by us a moment ago or not.
    ///
    /// <b>The folder above is never made.</b> ProgramData is part of every Windows. Creating it here would
    /// give it the permissions of the root of the drive, and a machine without it is one to refuse on.
    /// </summary>
    public static FolderCheck Prepare(string home)
    {
        try
        {
            var folder = new DirectoryInfo(home);

            if (folder.Parent is not { Exists: true })
            {
                return FolderCheck.Failed(home, UnpackingFault.NotMade);
            }

            if (!folder.Exists)
            {
                folder.Create(Closed());
            }
        }
        catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
        {
            // Without elevation the owner above cannot be given and this is an IOException
            // (0x8007051B) with no folder left behind - measured 2026-10-06 under a restricted token.
            return FolderCheck.Failed(home, UnpackingFault.NotMade, failed.Message);
        }

        return Inspect(home);
    }

    /// <summary>
    /// Examines one folder of libraries inside the administrators' folder: every level from the
    /// administrators' folder down to it, from the top, then everything inside it. The first failure is
    /// the answer.
    ///
    /// <b>From the top, because a level that passes protects everything under it</b> from anybody but
    /// the three - so what is examined below it cannot be changed by others between the two questions.
    /// </summary>
    public static FolderCheck Examine(string home, string folder)
    {
        foreach (var level in Levels(home, folder))
        {
            if (Inspect(level) is { Fault: not null } failed)
            {
                return failed;
            }
        }

        return Contents(folder);
    }

    /// <summary>The administrators' folder and every folder under it down to the one given.</summary>
    internal static IEnumerable<string> Levels(string home, string folder)
    {
        var level = Path.TrimEndingDirectorySeparator(home);

        yield return level;

        foreach (var name in folder[level.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            level = Path.Combine(level, name);

            yield return level;
        }
    }

    /// <summary>
    /// Everything inside a folder of libraries, folders and files alike and all the way down - walked by
    /// hand rather than by a recursive enumeration, so that a link inside is refused rather than followed.
    /// </summary>
    private static FolderCheck Contents(string folder)
    {
        var waiting = new Queue<string>([folder]);

        while (waiting.TryDequeue(out var current))
        {
            string[] entries;

            try
            {
                entries = Directory.GetFileSystemEntries(current);
            }
            catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
            {
                return FolderCheck.Failed(current, UnpackingFault.Unreadable, failed.Message);
            }

            var verdicts = entries.Select(Inspect).ToArray();

            if (verdicts.FirstOrDefault(verdict => verdict.Fault is not null) is { } failure)
            {
                return failure;
            }

            foreach (var inner in entries.Where(Directory.Exists))
            {
                waiting.Enqueue(inner);
            }
        }

        return FolderCheck.Passed(folder);
    }

    /// <summary>
    /// One folder or file: not a link, and only administrators can change it. The attributes are read
    /// without following anything, so a link answers as the link and not as what it points to.
    /// </summary>
    internal static FolderCheck Inspect(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);

            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return FolderCheck.Failed(path, UnpackingFault.Link);
            }

            FileSystemSecurity security = attributes.HasFlag(FileAttributes.Directory)
                ? new DirectoryInfo(path).GetAccessControl(AccessControlSections.Owner | AccessControlSections.Access)
                : new FileInfo(path).GetAccessControl(AccessControlSections.Owner | AccessControlSections.Access);

            return OnlyAdministratorsCanChange(new RawSecurityDescriptor(security.GetSecurityDescriptorBinaryForm(), 0))
                ? FolderCheck.Passed(path)
                : FolderCheck.Failed(path, UnpackingFault.OthersCanChange);
        }
        catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
        {
            return FolderCheck.Failed(path, UnpackingFault.Unreadable, failed.Message);
        }
    }
}
