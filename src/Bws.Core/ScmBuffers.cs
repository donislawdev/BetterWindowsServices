namespace Bws.Core;

/// <summary>
/// The two shapes a reading of the manager passes around before it becomes an
/// <see cref="ScmEntry"/>.
///
/// <b>Moved out of <c>WindowsScmCatalog</c> on 2026-08-02 because the size ratchet said so</b>,
/// and that is worth recording rather than tidying away. Adding the network rule pushed that
/// file nine lines past a ceiling that may only ever go down, and the guard's own message is
/// "split it, or move a piece of it somewhere it belongs". These two are the piece: everything
/// else in that file is calls into the manager and the parsing of what comes back, while these
/// are the values being carried between the two.
///
/// <b>Deliberately not a partial class.</b> Continuing the same type in a second file would
/// have satisfied a ratchet that counts files while leaving the type exactly as large - which
/// is gaming a guard rather than answering it, and the next session would inherit the habit
/// rather than the reasoning.
///
/// Internal rather than public: these are how this assembly talks to itself while reading, and
/// the contract the rest of the world sees is <see cref="ScmEntry"/>.
/// </summary>
internal readonly record struct EnumeratedEntry(
    string ServiceName,
    string DisplayName,
    EntryType EntryType,
    EntryStatus Status,
    uint ProcessId)
{
    internal bool IsDriver =>
        EntryType is Core.EntryType.KernelDriver or Core.EntryType.FileSystemDriver;
}

/// <summary>
/// Everything a configuration reading found, before it is folded into an entry.
///
/// Every field is a <see cref="Reading{T}"/> rather than a value, and that is the whole point
/// of the type: a configuration can be refused as a whole, refused per field, or genuinely
/// empty, and those three are different answers about a service.
/// </summary>
internal readonly record struct ScmConfiguration(
    Reading<StartType> StartType,
    Reading<bool> DelayedAuto,
    Reading<string> Account,
    Reading<IReadOnlyList<string>> DependsOn,
    Reading<IReadOnlyList<ServiceTrigger>> Triggers,
    Reading<string> BinaryPath,
    Reading<string> BinaryFile,
    Reading<bool> BinaryOnDisk,
    Reading<IReadOnlyList<string>> RequiredPrivileges,
    Reading<ServiceSidType> SidType,
    Reading<ErrorControl> ErrorControl,
    Reading<string> LoadOrderGroup)
{
    /// <summary>
    /// A refusal, carrying both halves: the system's number for a script and the system's
    /// sentence for a person.
    /// </summary>
    internal static ScmConfiguration Refused(int code) => new(
        Denied<StartType>(code),
        Denied<bool>(code),
        Denied<string>(code),
        Denied<IReadOnlyList<string>>(code),
        Denied<IReadOnlyList<ServiceTrigger>>(code),
        Denied<string>(code),
        Denied<string>(code),
        Denied<bool>(code),
        Denied<IReadOnlyList<string>>(code),
        Denied<ServiceSidType>(code),
        Denied<ErrorControl>(code),
        Denied<string>(code));

    /// <summary>
    /// Which file the launch command runs, and whether it is there.
    ///
    /// Kept apart from the buffer reading because it needs to know the entry, and because it
    /// is the one part of a listing that touches the file system rather than the manager. That
    /// makes it the first place a slow or disconnected disk shows up - and, since 2026-08-02,
    /// the place where a disk on somebody else's machine is not touched at all unless asked.
    /// </summary>
    internal ScmConfiguration WithBinary(
        EnumeratedEntry enumerated, string windowsDirectory, NetworkPaths networkPaths)
    {
        var resolved = BinaryPathResolver.Resolve(
            BinaryPath.ValueOr(null),
            enumerated.ServiceName,
            enumerated.IsDriver,
            windowsDirectory,
            File.Exists,
            networkPaths);

        return resolved.File is null
            // Nothing named and no default that applies. A fact about the entry, so the
            // question of whether the file is there has no subject and is absent too.
            ? this with { BinaryFile = Reading<string>.Absent(), BinaryOnDisk = Reading<bool>.Absent() }
            : this with
            {
                // Named even when nobody looked: which file a command runs comes out of its
                // text, and only the disk question needs the disk.
                BinaryFile = Reading<string>.Present(resolved.File),
                BinaryOnDisk = resolved.OnDisk
            };
    }

    private static Reading<T> Denied<T>(int code) => Reading<T>.Denied(code, ManagerTerms.Describe(code));
}
