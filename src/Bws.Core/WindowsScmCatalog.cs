using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.Services;

namespace Bws.Core;

/// <summary>
/// Reads the real service control manager.
///
/// Two calls per listing: one enumeration that returns every entry with its name,
/// display name, type, state and process, and one configuration query per entry for
/// the start type and the account.
///
/// The second call is the one that gets refused, and rarely: measured on 2026-08-01
/// under a restricted token, opening 3 of 810 entries is denied, and with elevation
/// none are. Rare is not never, and a refused entry still comes back in the listing
/// carrying <see cref="ReadOutcome.Denied"/> - dropping it would produce a listing that
/// looks complete and is not.
///
/// An earlier version of this comment claimed 203 of 869 read and the rest refused. That
/// number was a count of registry keys, most of which hold no security value at all, and
/// it never described this call. See <see cref="ReadOutcome"/>.
/// </summary>
/// <param name="networkPaths">
/// Whether a launch path on another machine may be asked about - <see cref="NetworkPaths"/>
/// holds the whole argument. Optional so that every caller gets the safe answer by saying
/// nothing, and only the one that wants the other has to say so.
/// </param>
public sealed partial class WindowsScmCatalog(NetworkPaths networkPaths = NetworkPaths.Skip) : IScmCatalog
{
    // What a relative image path and \SystemRoot\ are relative to. Read once: it cannot
    // change while the process runs, and it is asked for on every entry of every listing.
    private static readonly string WindowsDirectory =
        Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    // Everything the manager holds. services.msc shows only part of this, which is why
    // our count is larger, and that difference is deliberate rather than a discrepancy.
    //
    // Getting this wrong is quiet. The first attempt asked only for drivers plus the two
    // plain Win32 kinds and came back 81 entries short, with nothing to indicate anything
    // was missing. The absent ones were per-user services, whose type carries extra bits
    // (0x40 for a template, 0x80 for a per-session instance) on top of the Win32 kind, so
    // a mask built from the obvious four never matches them.
    //
    // (A copy of the security descriptor parts used to sit here too, word for word with the
    // one in ScmDetailReader, and went unused when the reading moved there on 2026-08-02. An
    // unused private constant raises no warning at all, so it sat until an audit found it.)
    private const ENUM_SERVICE_TYPE AllEntryTypes =
        ENUM_SERVICE_TYPE.SERVICE_DRIVER               // kernel, file system, recogniser
        | ENUM_SERVICE_TYPE.SERVICE_ADAPTER
        | ENUM_SERVICE_TYPE.SERVICE_WIN32              // own process and shared process
        | ENUM_SERVICE_TYPE.SERVICE_USER_OWN_PROCESS   // per-user, own process
        | ENUM_SERVICE_TYPE.SERVICE_USER_SHARE_PROCESS // per-user, shared process
        | (ENUM_SERVICE_TYPE)0x80;                     // per-session instance of the above

    /// <summary>
    /// How many entries are described at once when nobody says otherwise.
    ///
    /// The processor count, the same answer and the same reason as <see cref="SecondPass"/>,
    /// and arrived at the same way - by sweeping rather than by choosing. See `ADR-22`.
    /// </summary>
    public static int DefaultDegreeOfParallelism => Environment.ProcessorCount;

    public IReadOnlyList<ScmEntry> ReadAll() => ReadAll(DefaultDegreeOfParallelism);

    /// <summary>
    /// Every entry, with the number described at once given rather than defaulted.
    ///
    /// <b>It is a parameter because the guard needs it.</b> "Threads changed no answer" can
    /// only be checked by running both ways and comparing, and a guard with no way to run the
    /// old way has nothing to compare against. Nothing in the product passes it.
    ///
    /// <b>Why this is parallel at all, measured rather than assumed.</b> The same loop over
    /// the same 810 entries costs 13-22 ms without opening a handle per entry and 455-475 ms
    /// with - so about 440 ms of a listing is spent waiting on the manager, one entry at a
    /// time, while fifteen processors do nothing. It is the same shape `ADR-22` already found
    /// in signature verification, one layer down.
    ///
    /// <b>Order is held by index, not by collecting and sorting afterwards.</b> Each entry
    /// writes into the slot it came from, so the answer is identical to the sequential one by
    /// construction rather than by a comparison somebody has to remember to make.
    ///
    /// <b>One manager handle, used from every thread.</b> That is the part worth knowing:
    /// <c>OpenService</c> is called concurrently against a single <c>OpenSCManager</c> handle.
    /// Nothing in the documentation forbids it and the entry-by-entry comparison in the tests
    /// says the answers do not change - but it is an assumption, it is written here rather
    /// than left in the code, and the guard that would catch it going wrong compares a
    /// parallel reading against a sequential one field by field.
    /// </summary>
    public IReadOnlyList<ScmEntry> ReadAll(int degreeOfParallelism)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(degreeOfParallelism, 1);

        using var manager = PInvoke.OpenSCManager(
            lpMachineName: null!,
            lpDatabaseName: null!,
            dwDesiredAccess: PInvoke.SC_MANAGER_CONNECT | PInvoke.SC_MANAGER_ENUMERATE_SERVICE);

        if (manager.IsInvalid)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not open the service control manager for enumeration.");
        }

        var enumerated = Enumerate(manager);
        var entries = new ScmEntry[enumerated.Count];

        // WORTH KNOWING BEFORE ANYBODY CATCHES SOMETHING FROM HERE BY TYPE: the two branches
        // below fail differently. Below, an exception from Describe travels as itself. Through
        // Parallel.For it arrives wrapped in an AggregateException.
        //
        // Nothing catches either by type today, so the difference is cosmetic - and it is worth
        // saying because the single-threaded branch exists only so the guard can compare one
        // reading against the other, which means the guard runs a branch that behaves differently
        // under failure from the one that ships.

        if (degreeOfParallelism == 1)
        {
            for (var index = 0; index < enumerated.Count; index++)
            {
                entries[index] = Describe(manager, enumerated[index], networkPaths);
            }

            return entries;
        }

        Parallel.For(
            0,
            enumerated.Count,
            new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism },
            index => entries[index] = Describe(manager, enumerated[index], networkPaths));

        return entries;
    }

    public IReadOnlyList<ScmStatus> ReadStatuses()
    {
        using var manager = PInvoke.OpenSCManager(
            lpMachineName: null!,
            lpDatabaseName: null!,
            dwDesiredAccess: PInvoke.SC_MANAGER_CONNECT | PInvoke.SC_MANAGER_ENUMERATE_SERVICE);

        if (manager.IsInvalid)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not open the service control manager for enumeration.");
        }

        var statuses = new List<ScmStatus>(capacity: 1024);

        // The same enumeration a full reading starts with, and then nothing. What makes a
        // full reading expensive is the handle opened for every entry afterwards, so leaving
        // that out is the whole saving rather than an optimisation of it.
        foreach (var enumerated in Enumerate(manager))
        {
            statuses.Add(new ScmStatus(
                enumerated.ServiceName,
                enumerated.Status,
                enumerated.ProcessId == 0
                    ? Reading<int>.Absent()
                    : Reading<int>.Present((int)enumerated.ProcessId)));
        }

        return statuses;
    }

    public unsafe Reading<IReadOnlyList<string>> ReadDependents(string serviceName)
    {
        using var manager = PInvoke.OpenSCManager(
            lpMachineName: null!,
            lpDatabaseName: null!,
            dwDesiredAccess: PInvoke.SC_MANAGER_CONNECT);

        if (manager.IsInvalid)
        {
            return Refused<IReadOnlyList<string>>(Marshal.GetLastWin32Error());
        }

        using var service = PInvoke.OpenService(manager, serviceName, PInvoke.SERVICE_ENUMERATE_DEPENDENTS);

        if (service.IsInvalid)
        {
            return Refused<IReadOnlyList<string>>(Marshal.GetLastWin32Error());
        }

        // Ask with an empty buffer first and let the call report how much room it wants.
        // Skipping this is exactly what sc.exe does, and it is why sc.exe reports three
        // dependents for a service that has a hundred and sixty two.
        var probed = PInvoke.EnumDependentServices(
            service, ENUM_SERVICE_STATE.SERVICE_STATE_ALL, default, out var needed, out _);

        var probeError = Marshal.GetLastWin32Error();

        // ASKED THROUGH THE RETURN VALUE SINCE 2026-08-26, AND THROUGH THE ERROR CODE ALONE BEFORE
        // THAT. The enumeration below carries the argument in full; the short version is that
        // Windows does not clear the last error on success, so a call with nothing to hand over can
        // leave whatever the previous call in this thread put there - and a check reading only the
        // number turned an ordinary service with no dependents into a refusal.
        //
        // A false refusal here is not quiet. PlanBuilder answers it with the CascadeUnreadable
        // warning, which a person reads on the screen where they decide whether to change the
        // machine, and DependentsFirst treats the entry as unordered. False alarms on that screen
        // are the thing this project argues against everywhere else - they teach people to click
        // past warnings, which is worse than not having warned at all.
        if (!probed && probeError != (int)WIN32_ERROR.ERROR_MORE_DATA)
        {
            return Refused<IReadOnlyList<string>>(probeError);
        }

        if (needed == 0)
        {
            // Nothing depends on it. The call reports no room needed and succeeds, which
            // is a fact about the service rather than a failure to read one.
            return Reading<IReadOnlyList<string>>.Absent();
        }

        var buffer = new byte[needed];
        List<string> names;

        // Pinned across the call and the reading - the argument is at ReadConfiguration. Each
        // record here names a service through a pointer into this block.
        fixed (byte* pinned = buffer)
        {
            if (!PInvoke.EnumDependentServices(
                    service, ENUM_SERVICE_STATE.SERVICE_STATE_ALL,
                    new Span<byte>(pinned, buffer.Length), out _, out var returned))
            {
                return Refused<IReadOnlyList<string>>(Marshal.GetLastWin32Error());
            }

            names = ManagerBlocks.ReadDependentNames(buffer, returned);
        }

        return names.Count == 0
            ? Reading<IReadOnlyList<string>>.Absent()
            : Reading<IReadOnlyList<string>>.Present(names);
    }

    private static ScmEntry Describe(SafeHandle manager, EnumeratedEntry enumerated, NetworkPaths networkPaths)
    {
        var configuration = ReadConfiguration(manager, enumerated, networkPaths);

        return new ScmEntry
        {
            ServiceName = enumerated.ServiceName,
            DisplayName = enumerated.DisplayName,
            EntryType = enumerated.EntryType,
            PerUserRole = enumerated.PerUserRole,
            Status = enumerated.Status,

            // A stopped entry has no process. Zero is a value, "not running" is not,
            // so it is reported as absent rather than as process zero.
            ProcessId = enumerated.ProcessId == 0
                ? Reading<int>.Absent()
                : Reading<int>.Present((int)enumerated.ProcessId),

            StartType = configuration.StartType,
            DelayedAuto = configuration.DelayedAuto,
            Account = configuration.Account,
            DependsOn = configuration.DependsOn,
            Triggers = configuration.Triggers,
            BinaryPath = configuration.BinaryPath,
            BinaryFile = configuration.BinaryFile,
            BinaryOnDisk = configuration.BinaryOnDisk,
            RequiredPrivileges = configuration.RequiredPrivileges,
            SidType = configuration.SidType,
            ErrorControl = configuration.ErrorControl,
            LoadOrderGroup = configuration.LoadOrderGroup,
            Description = configuration.Description,

            // Read outside the configuration, and not because of tidiness. It needs a
            // different right on a different handle, so an entry whose configuration was
            // refused can still have a readable descriptor and the other way round. Folding
            // it in would have made one refusal answer for two questions nobody asked
            // together.
            SecurityDescriptor = ScmDetailReader.ReadSecurityDescriptor(manager, enumerated),

            // The second pass fills these, and only when somebody asks for them. Not read
            // is the honest state here and it is the ordinary one: a listing that verified
            // every signature would take six times its budget, so most runs never will.
            Signature = Reading<BinarySignature>.NotRead(),
            FileVersion = Reading<string>.NotRead(),
            BinaryHash = Reading<string>.NotRead(),

            // Filled in by MemoryPass, and only when asked. Not read is honest here and it
            // is the ordinary state: a listing describes configuration, and this is the one
            // field that is a reading off a running machine instead.
            Memory = Reading<ProcessMemory>.NotRead()
        };
    }

    private static unsafe ScmConfiguration ReadConfiguration(
        SafeHandle manager, EnumeratedEntry enumerated, NetworkPaths networkPaths)
    {
        // SERVICE_QUERY_CONFIG alone, and adding READ_CONTROL here would be the quiet
        // mistake this family invites - see ReadSecurityDescriptor for the five entries it
        // costs. No test on this machine would catch it: an elevated session refuses
        // nothing, so the whole suite stays green with that one word added.
        using var service = PInvoke.OpenService(manager, enumerated.ServiceName, PInvoke.SERVICE_QUERY_CONFIG);

        if (service.IsInvalid)
        {
            return ScmConfiguration.Refused(Marshal.GetLastWin32Error());
        }

        // ASKED THROUGH THE RETURN VALUE, the rule Enumerate below sets out in full and the one
        // this call did not follow until 2026-09-02. Backlog 303. Deciding on the size alone reads
        // whatever the previous call on this thread left in the last error as though it were this
        // call's refusal.
        var probed = PInvoke.QueryServiceConfig(service, default, out var needed);
        var probeError = Marshal.GetLastWin32Error();

        if (!probed && probeError != (int)WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER)
        {
            return ScmConfiguration.Refused(probeError);
        }

        if (needed == 0)
        {
            // The call answered and asked for no room. Every entry has a configuration, so this
            // is not a shape the manager produces - measured over 799 entries under both tokens
            // on 2026-09-02 and never seen once. Refused with a definite code rather than with a
            // stale one, because there is no true thing to say about an answer that cannot happen.
            return ScmConfiguration.Refused((int)WIN32_ERROR.ERROR_INVALID_DATA);
        }

        var buffer = new byte[needed];
        ScmConfiguration configuration;

        // PINNED ACROSS THE CALL AND THE READING, NOT ONLY ACROSS THE CALL, and this is the whole
        // of backlog 297. The interop wrapper pins for exactly as long as the native call runs -
        // Windows.Win32.PInvoke.ADVAPI32.dll.g.cs puts its `fixed` INSIDE the method - so the array
        // is loose again the moment it returns. The structure the manager wrote into it carries
        // ABSOLUTE POINTERS into that same array, taken at the moment of the call, and the reading
        // below follows them. A collection between the two moves the bytes to a new address and
        // leaves those pointers aimed at where the array used to be.
        //
        // What that costs is the thing this whole file is careful about: an account or a launch
        // path read from freed memory is either an access violation, which ends the process on
        // somebody's server without a word, or worse - plausible rubbish in an audit.
        //
        // THE SPAN IS BUILT FROM THE PINNED POINTER RATHER THAN FROM THE ARRAY on purpose. Passing
        // `buffer` would work identically, and would leave nothing on the page saying why the block
        // exists - the next person would see a `fixed` whose variable nobody uses and take it out.
        fixed (byte* pinned = buffer)
        {
            if (!PInvoke.QueryServiceConfig(service, new Span<byte>(pinned, buffer.Length), out _))
            {
                return ScmConfiguration.Refused(Marshal.GetLastWin32Error());
            }

            configuration = ManagerBlocks.ReadConfigurationBuffer(buffer);
        }

        var withOwnCalls = configuration with
        {
            DelayedAuto = ScmDetailReader.ReadDelayedAuto(service, enumerated),
            Triggers = ScmDetailReader.ReadTriggers(service),
            RequiredPrivileges = ScmDetailReader.ReadRequiredPrivileges(service),
            SidType = ScmDetailReader.ReadSidType(service),

            // On this handle rather than in the second pass, and that is a measurement rather
            // than a convenience: the whole description family costs 212-223 ms over 819 entries,
            // which is the privileges' order of magnitude and not the signatures' 4620-7656 ms.
            Description = ScmDetailReader.ReadDescription(service)
        };

        return withOwnCalls.WithBinary(enumerated, WindowsDirectory, networkPaths);
    }


    /// <summary>
    /// A refusal, carrying both halves: the system's number for a script and the system's
    /// sentence for a person. Reading the last error once, here, so that no caller has to
    /// remember that the next call would overwrite it.
    /// </summary>
    private static Reading<T> Refused<T>(int code) => Reading<T>.Denied(code, ManagerTerms.Describe(code));

    // Shared with the half of the manager that writes, because two copies of the same
    // mapping drift.

}
