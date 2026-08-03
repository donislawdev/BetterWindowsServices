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
public sealed class WindowsScmCatalog(NetworkPaths networkPaths = NetworkPaths.Skip) : IScmCatalog
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

    public Reading<IReadOnlyList<string>> ReadDependents(string serviceName)
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
        PInvoke.EnumDependentServices(
            service, ENUM_SERVICE_STATE.SERVICE_STATE_ALL, default, out var needed, out _);

        if (needed == 0)
        {
            var error = Marshal.GetLastWin32Error();

            // Nothing depends on it. The call reports no room needed and succeeds, which
            // is a fact about the service rather than a failure to read one.
            return error is 0 or (int)WIN32_ERROR.ERROR_SUCCESS
                ? Reading<IReadOnlyList<string>>.Absent()
                : Refused<IReadOnlyList<string>>(error);
        }

        var buffer = new byte[needed];

        if (!PInvoke.EnumDependentServices(
                service, ENUM_SERVICE_STATE.SERVICE_STATE_ALL, buffer, out _, out var returned))
        {
            return Refused<IReadOnlyList<string>>(Marshal.GetLastWin32Error());
        }

        var names = ReadDependentNames(buffer, returned);

        return names.Count == 0
            ? Reading<IReadOnlyList<string>>.Absent()
            : Reading<IReadOnlyList<string>>.Present(names);
    }

    private static unsafe List<string> ReadDependentNames(byte[] buffer, uint count)
    {
        count = Math.Min(count, (uint)(buffer.Length / sizeof(ENUM_SERVICE_STATUSW)));

        var names = new List<string>((int)count);

        fixed (byte* start = buffer)
        {
            var records = (ENUM_SERVICE_STATUSW*)start;

            for (uint index = 0; index < count; index++)
            {
                names.Add(records[index].lpServiceName.ToString());
            }
        }

        return names;
    }

    // A list rather than a sequence, because the caller needs a count before it starts and an
    // index while it runs. It was already building one internally - the sequence was hiding
    // that behind a type that promised less than it delivered.
    private static List<EnumeratedEntry> Enumerate(SafeHandle manager)
    {
        uint resume = 0;
        var results = new List<EnumeratedEntry>(capacity: 1024);

        while (true)
        {
            // Ask with an empty buffer first. The call is expected to fail and to report
            // how much room it wants, which is the documented way to size this.
            var probed = PInvoke.EnumServicesStatusEx(
                manager, SC_ENUM_TYPE.SC_ENUM_PROCESS_INFO, AllEntryTypes,
                ENUM_SERVICE_STATE.SERVICE_STATE_ALL, default,
                out var needed, out _, ref resume, null!);

            var probeError = Marshal.GetLastWin32Error();

            // NOTHING LEFT AND REFUSED LOOKED THE SAME FROM HERE UNTIL 2026-08-03, and this
            // took both to mean the first. Any failure that does not set the size - a refusal,
            // a manager shutting down between two turns of this loop, resources running out -
            // came back reporting no room needed, the loop broke, and ReadAll returned however
            // many entries it happened to have collected by then. With a code of success, and a
            // listing that looks exactly like a complete one.
            //
            // That is rule 8 of CLAUDE.md, in the one place in this file that had no guard
            // against it. ReadDependents below has had the same buffer shape and the right
            // check since it was written, and so has ReadConfiguration - this was the odd one
            // out rather than the pattern.
            //
            // ASKED THROUGH THE RETURN VALUE, NOT THROUGH THE ERROR CODE ALONE, and the
            // difference is not style. Windows does not clear the last error on success, so a
            // call that genuinely has nothing left to hand over can leave whatever the previous
            // call in this thread put there - and a check reading only the number would throw
            // on a stale one, turning a working listing into a failure. The return value is the
            // only thing that says whether this call worked.
            if (!probed && probeError != (int)WIN32_ERROR.ERROR_MORE_DATA)
            {
                throw new Win32Exception(probeError, "Enumerating the service control manager failed.");
            }

            if (needed == 0)
            {
                break;
            }

            var buffer = new byte[needed];
            var read = PInvoke.EnumServicesStatusEx(
                manager, SC_ENUM_TYPE.SC_ENUM_PROCESS_INFO, AllEntryTypes,
                ENUM_SERVICE_STATE.SERVICE_STATE_ALL, buffer,
                out _, out var returned, ref resume, null!);

            var error = Marshal.GetLastWin32Error();

            if (!read && error != (int)WIN32_ERROR.ERROR_MORE_DATA)
            {
                throw new Win32Exception(error, "Enumerating the service control manager failed.");
            }

            results.AddRange(ReadEnumerationBuffer(buffer, returned));

            // A resume handle of zero means the manager has nothing left to hand over.
            if (resume == 0)
            {
                break;
            }
        }

        return results;
    }

    private static unsafe List<EnumeratedEntry> ReadEnumerationBuffer(byte[] buffer, uint count)
    {
        // However many records the manager says it wrote, never more than the room it was given.
        // Trusting the count alone is the shape this project used everywhere and wrote down
        // nowhere - see ReadConfigurationBuffer for the argument.
        count = Math.Min(count, (uint)(buffer.Length / sizeof(ENUM_SERVICE_STATUS_PROCESSW)));

        var entries = new List<EnumeratedEntry>((int)count);

        fixed (byte* start = buffer)
        {
            var records = (ENUM_SERVICE_STATUS_PROCESSW*)start;

            for (uint index = 0; index < count; index++)
            {
                var record = records[index];
                var status = record.ServiceStatusProcess;

                entries.Add(new EnumeratedEntry(
                    ServiceName: record.lpServiceName.ToString(),
                    DisplayName: record.lpDisplayName.ToString(),
                    EntryType: ManagerTerms.EntryType(status.dwServiceType),
                    Status: ManagerTerms.Status(status.dwCurrentState),
                    ProcessId: status.dwProcessId));
            }
        }

        return entries;
    }

    private static ScmEntry Describe(SafeHandle manager, EnumeratedEntry enumerated, NetworkPaths networkPaths)
    {
        var configuration = ReadConfiguration(manager, enumerated, networkPaths);

        return new ScmEntry
        {
            ServiceName = enumerated.ServiceName,
            DisplayName = enumerated.DisplayName,
            EntryType = enumerated.EntryType,
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

    private static ScmConfiguration ReadConfiguration(
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

        PInvoke.QueryServiceConfig(service, default, out var needed);

        if (needed == 0)
        {
            return ScmConfiguration.Refused(Marshal.GetLastWin32Error());
        }

        var buffer = new byte[needed];

        if (!PInvoke.QueryServiceConfig(service, buffer, out _))
        {
            return ScmConfiguration.Refused(Marshal.GetLastWin32Error());
        }

        var configuration = ReadConfigurationBuffer(buffer);

        var withOwnCalls = configuration with
        {
            DelayedAuto = ScmDetailReader.ReadDelayedAuto(service, enumerated),
            Triggers = ScmDetailReader.ReadTriggers(service),
            RequiredPrivileges = ScmDetailReader.ReadRequiredPrivileges(service),
            SidType = ScmDetailReader.ReadSidType(service)
        };

        return withOwnCalls.WithBinary(enumerated, WindowsDirectory, networkPaths);
    }

    /// <remarks>
    /// The size is checked before the cast, and it was not until 2026-08-03. The manager reports
    /// how much room it wants and this asks for exactly that, so a buffer too small for the
    /// structure cannot happen - which is a fact about the manager rather than about this code,
    /// and it was nowhere written down. Owner's decision, 2026-08-03: the three places in this
    /// project that read a reported size now check it, because the tool runs elevated on
    /// production servers and a memory fault there is indistinguishable from a bug of ours.
    /// </remarks>
    private static unsafe ScmConfiguration ReadConfigurationBuffer(byte[] buffer)
    {
        if (buffer.Length < sizeof(QUERY_SERVICE_CONFIGW))
        {
            return ScmConfiguration.Refused((int)WIN32_ERROR.ERROR_INVALID_DATA);
        }

        fixed (byte* start = buffer)
        {
            var configuration = *(QUERY_SERVICE_CONFIGW*)start;

            var account = configuration.lpServiceStartName.ToString();
            var dependencies = ScmDetailReader.ReadMultiString(
                configuration.lpDependencies, start, buffer.Length);
            var binaryPath = configuration.lpBinaryPathName.ToString();
            var loadOrderGroup = configuration.lpLoadOrderGroup.ToString();

            return new ScmConfiguration(
                StartType: Reading<StartType>.Present(ManagerTerms.StartType(configuration.dwStartType)),
                DelayedAuto: Reading<bool>.Absent(),
                Account: string.IsNullOrEmpty(account)
                    ? Reading<string>.Absent()
                    : Reading<string>.Present(account),

                // Declaring nothing is ordinary rather than missing information: 129 of 339
                // services on the machine this was measured on declare no dependency at all.
                DependsOn: dependencies.Count == 0
                    ? Reading<IReadOnlyList<string>>.Absent()
                    : Reading<IReadOnlyList<string>>.Present(dependencies),

                // Filled in by its own call. Not read yet is the honest state here, not absent.
                Triggers: Reading<IReadOnlyList<ServiceTrigger>>.NotRead(),

                // 29 of 825 entries name nothing at all, all of them drivers, and for those
                // the manager applies a default of its own. Absent says that, and the file
                // question is answered from the default rather than left blank.
                BinaryPath: string.IsNullOrWhiteSpace(binaryPath)
                    ? Reading<string>.Absent()
                    : Reading<string>.Present(binaryPath),

                // Both worked out from the value above, once it is known which entry it is.
                BinaryFile: Reading<string>.NotRead(),
                BinaryOnDisk: Reading<bool>.NotRead(),

                // Two more levels of the same call as the triggers, filled in by their own
                // calls for the same reason: this buffer holds none of them.
                RequiredPrivileges: Reading<IReadOnlyList<string>>.NotRead(),
                SidType: Reading<ServiceSidType>.NotRead(),

                ErrorControl: Reading<ErrorControl>.Present(ManagerTerms.ErrorControl(configuration.dwErrorControl)),

                // Most entries belong to no group, which is a fact about them rather than
                // something we failed to read.
                LoadOrderGroup: string.IsNullOrEmpty(loadOrderGroup)
                    ? Reading<string>.Absent()
                    : Reading<string>.Present(loadOrderGroup));
        }
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
