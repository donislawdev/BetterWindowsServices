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
public sealed class WindowsScmCatalog : IScmCatalog
{
    // Everything the manager holds. services.msc shows only part of this, which is why
    // our count is larger, and that difference is deliberate rather than a discrepancy.
    //
    // Getting this wrong is quiet. The first attempt asked only for drivers plus the two
    // plain Win32 kinds and came back 81 entries short, with nothing to indicate anything
    // was missing. The absent ones were per-user services, whose type carries extra bits
    // (0x40 for a template, 0x80 for a per-session instance) on top of the Win32 kind, so
    // a mask built from the obvious four never matches them.
    // What a relative image path and \SystemRoot\ are relative to. Read once: it cannot
    // change while the process runs, and it is asked for on every entry of every listing.
    private static readonly string WindowsDirectory =
        Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    // Owner, group and permissions - the three parts of a descriptor that can be read with
    // READ_CONTROL alone. The fourth, the audit list, is left out on purpose: see
    // ReadSecurityDescriptor.
    private const uint DescriptorParts =
        (uint)(OBJECT_SECURITY_INFORMATION.OWNER_SECURITY_INFORMATION
            | OBJECT_SECURITY_INFORMATION.GROUP_SECURITY_INFORMATION
            | OBJECT_SECURITY_INFORMATION.DACL_SECURITY_INFORMATION);

    private const ENUM_SERVICE_TYPE AllEntryTypes =
        ENUM_SERVICE_TYPE.SERVICE_DRIVER               // kernel, file system, recogniser
        | ENUM_SERVICE_TYPE.SERVICE_ADAPTER
        | ENUM_SERVICE_TYPE.SERVICE_WIN32              // own process and shared process
        | ENUM_SERVICE_TYPE.SERVICE_USER_OWN_PROCESS   // per-user, own process
        | ENUM_SERVICE_TYPE.SERVICE_USER_SHARE_PROCESS // per-user, shared process
        | (ENUM_SERVICE_TYPE)0x80;                     // per-session instance of the above

    public IReadOnlyList<ScmEntry> ReadAll()
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

        var entries = new List<ScmEntry>(capacity: 1024);

        foreach (var enumerated in Enumerate(manager))
        {
            entries.Add(Describe(manager, enumerated));
        }

        return entries;
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

    private static IEnumerable<EnumeratedEntry> Enumerate(SafeHandle manager)
    {
        uint resume = 0;
        var results = new List<EnumeratedEntry>(capacity: 1024);

        while (true)
        {
            // Ask with an empty buffer first. The call is expected to fail and to report
            // how much room it wants, which is the documented way to size this.
            PInvoke.EnumServicesStatusEx(
                manager, SC_ENUM_TYPE.SC_ENUM_PROCESS_INFO, AllEntryTypes,
                ENUM_SERVICE_STATE.SERVICE_STATE_ALL, default,
                out var needed, out _, ref resume, null!);

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
                    EntryType: MapEntryType(status.dwServiceType),
                    Status: MapStatus(status.dwCurrentState),
                    ProcessId: status.dwProcessId));
            }
        }

        return entries;
    }

    private static ScmEntry Describe(SafeHandle manager, EnumeratedEntry enumerated)
    {
        var configuration = ReadConfiguration(manager, enumerated);

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
            SecurityDescriptor = ReadSecurityDescriptor(manager, enumerated),

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

    private static Configuration ReadConfiguration(SafeHandle manager, EnumeratedEntry enumerated)
    {
        // SERVICE_QUERY_CONFIG alone, and adding READ_CONTROL here would be the quiet
        // mistake this family invites - see ReadSecurityDescriptor for the five entries it
        // costs. No test on this machine would catch it: an elevated session refuses
        // nothing, so the whole suite stays green with that one word added.
        using var service = PInvoke.OpenService(manager, enumerated.ServiceName, PInvoke.SERVICE_QUERY_CONFIG);

        if (service.IsInvalid)
        {
            return Configuration.Refused(Marshal.GetLastWin32Error());
        }

        PInvoke.QueryServiceConfig(service, default, out var needed);

        if (needed == 0)
        {
            return Configuration.Refused(Marshal.GetLastWin32Error());
        }

        var buffer = new byte[needed];

        if (!PInvoke.QueryServiceConfig(service, buffer, out _))
        {
            return Configuration.Refused(Marshal.GetLastWin32Error());
        }

        var configuration = ReadConfigurationBuffer(buffer);

        var withOwnCalls = configuration with
        {
            DelayedAuto = ReadDelayedAuto(service, enumerated, configuration.StartType),
            Triggers = ReadTriggers(service),
            RequiredPrivileges = ReadRequiredPrivileges(service),
            SidType = ReadSidType(service)
        };

        return withOwnCalls.WithBinary(enumerated);
    }

    private static unsafe Configuration ReadConfigurationBuffer(byte[] buffer)
    {
        fixed (byte* start = buffer)
        {
            var configuration = *(QUERY_SERVICE_CONFIGW*)start;

            var account = configuration.lpServiceStartName.ToString();
            var dependencies = ReadMultiString(configuration.lpDependencies);
            var binaryPath = configuration.lpBinaryPathName.ToString();
            var loadOrderGroup = configuration.lpLoadOrderGroup.ToString();

            return new Configuration(
                StartType: Reading<StartType>.Present(MapStartType(configuration.dwStartType)),
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

                ErrorControl: Reading<ErrorControl>.Present(MapErrorControl(configuration.dwErrorControl)),

                // Most entries belong to no group, which is a fact about them rather than
                // something we failed to read.
                LoadOrderGroup: string.IsNullOrEmpty(loadOrderGroup)
                    ? Reading<string>.Absent()
                    : Reading<string>.Present(loadOrderGroup));
        }
    }

    /// <summary>
    /// Whether an automatic entry starts late.
    ///
    /// Asked only where it can be true, which keeps the extra call off the great majority
    /// of the listing: drivers do not have the notion, and neither does anything that is
    /// not automatic in the first place. Those come back absent, which says the idea does
    /// not apply here rather than claiming somebody checked and found no delay.
    /// </summary>
    private static unsafe Reading<bool> ReadDelayedAuto(
        SafeHandle service, EnumeratedEntry enumerated, Reading<StartType> startType)
    {
        // Every entry that can carry one, not only the ones where it currently does
        // something. Windows stores this flag on manual and disabled services too, and it
        // sits there doing nothing until somebody sets the entry to automatic - at which
        // point it decides whether the machine waits for it at boot.
        //
        // Read here because a snapshot records how the machine is set up, and a value that
        // is stored and can change belongs in it. Measured on 2026-08-01: eight entries on
        // this machine carry the flag set while not being automatic, among them WinRM, MSDTC
        // and PcaSvc. Under the old rule a snapshot could not see any of them, so flipping
        // one to automatic would show up as a start type change with no hint that the entry
        // had been marked delayed all along.
        //
        // Costs nothing measurable: 451-481 ms before against 455-461 ms after, five counted
        // runs of each build interleaved, over 810 entries. The spread inside each variant is
        // wider than the gap between them, which by this project's own rule means there is no
        // difference. It goes from 82 reads to 339.
        //
        // The listing still only annotates automatic entries - see ListingTable. Reading it
        // and showing it are different questions, and "Manual (delayed)" would be a sentence
        // claiming something the flag does not do.
        var applies = !enumerated.IsDriver;

        if (!applies)
        {
            return Reading<bool>.Absent();
        }

        // Fixed size structure, so asking for the length first would only double the number
        // of calls. That matters here: this runs once per automatic entry on every listing.
        var buffer = new byte[sizeof(SERVICE_DELAYED_AUTO_START_INFO)];

        if (!PInvoke.QueryServiceConfig2W(
                service, SERVICE_CONFIG.SERVICE_CONFIG_DELAYED_AUTO_START_INFO, buffer, out _))
        {
            return Refused<bool>(Marshal.GetLastWin32Error());
        }

        fixed (byte* start = buffer)
        {
            return Reading<bool>.Present(((SERVICE_DELAYED_AUTO_START_INFO*)start)->fDelayedAutostart);
        }
    }

    /// <summary>
    /// The conditions under which the manager starts or stops this entry by itself.
    ///
    /// Variable length, so it takes the same buffer dance as everything else here: ask with
    /// nothing and be told how much room the answer wants. Everything is read inside the
    /// fixed block, because the structure hands back a pointer into that very buffer and
    /// following it afterwards would be reading memory nobody owns any more.
    /// </summary>
    private static unsafe Reading<IReadOnlyList<ServiceTrigger>> ReadTriggers(SafeHandle service)
    {
        PInvoke.QueryServiceConfig2W(
            service, SERVICE_CONFIG.SERVICE_CONFIG_TRIGGER_INFO, default, out var needed);

        if (needed == 0)
        {
            return Refused<IReadOnlyList<ServiceTrigger>>(Marshal.GetLastWin32Error());
        }

        var buffer = new byte[needed];

        if (!PInvoke.QueryServiceConfig2W(
                service, SERVICE_CONFIG.SERVICE_CONFIG_TRIGGER_INFO, buffer, out _))
        {
            return Refused<IReadOnlyList<ServiceTrigger>>(Marshal.GetLastWin32Error());
        }

        fixed (byte* start = buffer)
        {
            var info = *(SERVICE_TRIGGER_INFO*)start;

            if (info.cTriggers == 0)
            {
                // A fact about the service: most entries have none.
                return Reading<IReadOnlyList<ServiceTrigger>>.Absent();
            }

            var triggers = new List<ServiceTrigger>((int)info.cTriggers);

            for (uint index = 0; index < info.cTriggers; index++)
            {
                var trigger = info.pTriggers[index];

                triggers.Add(new ServiceTrigger(
                    ManagerTerms.Trigger(trigger.dwTriggerType),
                    ManagerTerms.TriggerAction(trigger.dwAction)));
            }

            return Reading<IReadOnlyList<ServiceTrigger>>.Present(triggers);
        }
    }

    /// <summary>
    /// The privileges the entry asks the manager to leave in its token.
    ///
    /// Variable length, so the same buffer dance as the triggers, and read on the handle the
    /// configuration already opened - this level needs no right the listing does not have.
    ///
    /// Declaring nothing is a fact about the service and the ordinary case for two thirds of
    /// a machine, so it comes back absent rather than as an empty list. It is also the
    /// permissive case rather than the careful one, which is worth knowing before building
    /// anything on top: a service that names no privileges keeps every privilege its account
    /// has.
    /// </summary>
    private static unsafe Reading<IReadOnlyList<string>> ReadRequiredPrivileges(SafeHandle service)
    {
        PInvoke.QueryServiceConfig2W(
            service, SERVICE_CONFIG.SERVICE_CONFIG_REQUIRED_PRIVILEGES_INFO, default, out var needed);

        if (needed == 0)
        {
            return Refused<IReadOnlyList<string>>(Marshal.GetLastWin32Error());
        }

        var buffer = new byte[needed];

        if (!PInvoke.QueryServiceConfig2W(
                service, SERVICE_CONFIG.SERVICE_CONFIG_REQUIRED_PRIVILEGES_INFO, buffer, out _))
        {
            return Refused<IReadOnlyList<string>>(Marshal.GetLastWin32Error());
        }

        fixed (byte* start = buffer)
        {
            // The structure is one pointer into this very buffer, so the multi-string is read
            // inside the fixed block for the same reason the triggers are.
            var privileges = ReadMultiString(((SERVICE_REQUIRED_PRIVILEGES_INFOW*)start)->pmszRequiredPrivileges);

            return privileges.Count == 0
                ? Reading<IReadOnlyList<string>>.Absent()
                : Reading<IReadOnlyList<string>>.Present(privileges);
        }
    }

    /// <summary>
    /// Whether the entry has an identity of its own.
    ///
    /// Fixed size, so one call rather than two, the same trade the delay flag makes.
    ///
    /// Asked of drivers as well, even though every driver on the machine this was measured
    /// on answers none. Skipping them would save about ten milliseconds across 810 entries
    /// and would turn "471 drivers answered none here" into "drivers cannot have one", which
    /// is a claim about every machine made from one.
    /// </summary>
    private static unsafe Reading<ServiceSidType> ReadSidType(SafeHandle service)
    {
        var buffer = new byte[sizeof(SERVICE_SID_INFO)];

        if (!PInvoke.QueryServiceConfig2W(
                service, SERVICE_CONFIG.SERVICE_CONFIG_SERVICE_SID_INFO, buffer, out _))
        {
            return Refused<ServiceSidType>(Marshal.GetLastWin32Error());
        }

        fixed (byte* start = buffer)
        {
            // SERVICE_SID_TYPE_NONE, _UNRESTRICTED and _RESTRICTED, which this interop
            // metadata does not name - it carries the structure and not the three values that
            // go in it. Written out here rather than as bare numbers at the call site.
            return ((SERVICE_SID_INFO*)start)->dwServiceSidType switch
            {
                // Not a kind of identity but the absence of one, so it belongs in the outcome
                // rather than in the enumeration. See ServiceSidType for what that buys.
                0 => Reading<ServiceSidType>.Absent(),

                1 => Reading<ServiceSidType>.Present(ServiceSidType.Unrestricted),
                3 => Reading<ServiceSidType>.Present(ServiceSidType.Restricted),
                _ => Reading<ServiceSidType>.Present(ServiceSidType.Unknown)
            };
        }
    }

    /// <summary>
    /// Who may do what to this entry, in the text form the system reads and writes.
    ///
    /// The one field in a listing that opens a handle of its own, and the reason is measured
    /// rather than structural. READ_CONTROL is a different right from SERVICE_QUERY_CONFIG,
    /// and under a restricted token on 2026-08-01 five entries of 810 - LSM, NetSetupSvc,
    /// pla, QWAVE and QWAVEdrv - grant the second and refuse the first. Adding READ_CONTROL
    /// to the handle the listing already opens would have taken the start type, the account
    /// and the launch path away from those five in exchange for a field they were never
    /// going to give up. A refusal here costs this field and nothing else.
    ///
    /// The audit list is deliberately not asked for: including SACL_SECURITY_INFORMATION
    /// fails the whole call with error 5 unless SeSecurityPrivilege is enabled, which it is
    /// not even in an elevated session. This is a deliberate difference from sc sdshow,
    /// which shows the audit list and does not show the owner or the group.
    /// </summary>
    private static unsafe Reading<string> ReadSecurityDescriptor(SafeHandle manager, EnumeratedEntry enumerated)
    {
        // READ_CONTROL is a standard right shared by every kind of securable object. The
        // interop metadata happens to file it under the file rights, which is where the name
        // comes from - it is not a file being opened here.
        using var service = PInvoke.OpenService(
            manager, enumerated.ServiceName, (uint)FILE_ACCESS_RIGHTS.READ_CONTROL);

        if (service.IsInvalid)
        {
            return Refused<string>(Marshal.GetLastWin32Error());
        }

        PInvoke.QueryServiceObjectSecurity(service, DescriptorParts, default, 0, out var needed);

        if (needed == 0)
        {
            return Refused<string>(Marshal.GetLastWin32Error());
        }

        var buffer = new byte[needed];

        fixed (byte* start = buffer)
        {
            if (!PInvoke.QueryServiceObjectSecurity(
                    service, DescriptorParts, new PSECURITY_DESCRIPTOR(start), needed, out _))
            {
                return Refused<string>(Marshal.GetLastWin32Error());
            }
        }

        try
        {
            // Everything the descriptor turned out to carry, which can never be more than was
            // asked for above. Naming the three parts again here would go wrong in one
            // direction only - by asking for a part this descriptor does not have.
            return Reading<string>.Present(
                new RawSecurityDescriptor(buffer, 0).GetSddlForm(AccessControlSections.All));
        }
        catch (Exception malformed) when (malformed is ArgumentException or InvalidOperationException)
        {
            // A descriptor the system handed over and nothing here can read. Not a refusal in
            // the ordinary sense, and reported as one anyway, because the four states have no
            // word for "read, and unintelligible" - and of the four, saying we failed to read
            // it is the only one that is not a lie. Narrow on purpose: these two are what
            // building or writing a descriptor is documented to throw, so a third kind of
            // failure here is news and should not be swallowed.
            return Reading<string>.Denied(malformed.HResult, malformed.Message);
        }
    }

    /// <summary>
    /// Reads one of the manager's multi-strings: values back to back, each ending in a
    /// null, the whole run ending in a second one.
    ///
    /// Written out by hand because the marshalling helper for a string stops at the first
    /// null and would hand back only the first dependency. That failure is quiet - a
    /// service declaring five dependencies would report one, and the cascade built on it
    /// would look reasonable and be wrong.
    /// </summary>
    private static unsafe List<string> ReadMultiString(PWSTR start)
    {
        var values = new List<string>();

        if (start.Value is null)
        {
            return values;
        }

        for (var cursor = start.Value; *cursor != '\0';)
        {
            var value = new string(cursor);
            values.Add(value);
            cursor += value.Length + 1;
        }

        return values;
    }

    /// <summary>
    /// A refusal, carrying both halves: the system's number for a script and the system's
    /// sentence for a person. Reading the last error once, here, so that no caller has to
    /// remember that the next call would overwrite it.
    /// </summary>
    private static Reading<T> Refused<T>(int code) => Reading<T>.Denied(code, ManagerTerms.Describe(code));

    // Shared with the half of the manager that writes, because two copies of the same
    // mapping drift.

    private static EntryStatus MapStatus(SERVICE_STATUS_CURRENT_STATE state) => ManagerTerms.Status(state);

    private static EntryType MapEntryType(ENUM_SERVICE_TYPE type)
    {
        if (type.HasFlag(ENUM_SERVICE_TYPE.SERVICE_KERNEL_DRIVER))
        {
            return EntryType.KernelDriver;
        }

        if (type.HasFlag(ENUM_SERVICE_TYPE.SERVICE_FILE_SYSTEM_DRIVER))
        {
            return EntryType.FileSystemDriver;
        }

        if (type.HasFlag(ENUM_SERVICE_TYPE.SERVICE_WIN32_SHARE_PROCESS))
        {
            return EntryType.SharedProcess;
        }

        return type.HasFlag(ENUM_SERVICE_TYPE.SERVICE_WIN32_OWN_PROCESS)
            ? EntryType.OwnProcess
            : EntryType.Unknown;
    }

    /// <summary>
    /// How hard the system takes a failure to start during boot.
    ///
    /// Anything the metadata does not name comes back Unknown rather than being folded into
    /// the nearest neighbour, the same rule the trigger kinds follow. Guessing here would be
    /// a claim about how a machine boots.
    /// </summary>
    private static ErrorControl MapErrorControl(SERVICE_ERROR type) => type switch
    {
        SERVICE_ERROR.SERVICE_ERROR_IGNORE => Core.ErrorControl.Ignore,
        SERVICE_ERROR.SERVICE_ERROR_NORMAL => Core.ErrorControl.Normal,
        SERVICE_ERROR.SERVICE_ERROR_SEVERE => Core.ErrorControl.Severe,
        SERVICE_ERROR.SERVICE_ERROR_CRITICAL => Core.ErrorControl.Critical,
        _ => Core.ErrorControl.Unknown
    };

    private static StartType MapStartType(SERVICE_START_TYPE type) => type switch
    {
        SERVICE_START_TYPE.SERVICE_BOOT_START => Core.StartType.Boot,
        SERVICE_START_TYPE.SERVICE_SYSTEM_START => Core.StartType.System,
        SERVICE_START_TYPE.SERVICE_AUTO_START => Core.StartType.Automatic,
        SERVICE_START_TYPE.SERVICE_DEMAND_START => Core.StartType.Manual,
        SERVICE_START_TYPE.SERVICE_DISABLED => Core.StartType.Disabled,
        _ => Core.StartType.Unknown
    };

    private readonly record struct EnumeratedEntry(
        string ServiceName,
        string DisplayName,
        EntryType EntryType,
        EntryStatus Status,
        uint ProcessId)
    {
        internal bool IsDriver =>
            EntryType is Core.EntryType.KernelDriver or Core.EntryType.FileSystemDriver;
    }

    private readonly record struct Configuration(
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
        internal static Configuration Refused(int code) => new(
            Refused<StartType>(code),
            Refused<bool>(code),
            Refused<string>(code),
            Refused<IReadOnlyList<string>>(code),
            Refused<IReadOnlyList<ServiceTrigger>>(code),
            Refused<string>(code),
            Refused<string>(code),
            Refused<bool>(code),
            Refused<IReadOnlyList<string>>(code),
            Refused<ServiceSidType>(code),
            Refused<ErrorControl>(code),
            Refused<string>(code));

        /// <summary>
        /// Which file the launch command runs, and whether it is there.
        ///
        /// Kept out of the buffer reading above because it needs to know the entry, and
        /// because it is the one part of a listing that touches the file system rather than
        /// the manager. That makes it the first place a slow or disconnected disk could show
        /// up, which is worth knowing when a listing is ever slower than it should be.
        /// </summary>
        internal Configuration WithBinary(EnumeratedEntry enumerated)
        {
            var resolved = BinaryPathResolver.Resolve(
                BinaryPath.ValueOr(null),
                enumerated.ServiceName,
                enumerated.IsDriver,
                WindowsDirectory,
                File.Exists);

            return resolved.File is null
                // Nothing named and no default that applies. A fact about the entry, so the
                // question of whether the file is there has no subject and is absent too.
                ? this with { BinaryFile = Reading<string>.Absent(), BinaryOnDisk = Reading<bool>.Absent() }
                : this with
                {
                    BinaryFile = Reading<string>.Present(resolved.File),
                    BinaryOnDisk = Reading<bool>.Present(resolved.Found)
                };
        }
    }
}
