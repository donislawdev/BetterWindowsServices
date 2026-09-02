using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Services;

namespace Bws.Core;

/// <summary>
/// The questions asked of a single service, beyond the one configuration structure.
///
/// <b>Moved out of <c>WindowsScmCatalog</c> on 2026-08-02 because the size ratchet said so a
/// second time</b>, and the second time is worth recording as much as the first. Making the
/// listing read entries in parallel added fifty one lines to a file already standing at its
/// own ceiling - and the ceiling exists precisely so that adding to the longest file starts
/// with splitting it.
///
/// The split is along a real seam rather than at a convenient line count. Everything here
/// takes a handle somebody else opened and asks it one more question at one more information
/// level: the delayed start flag, the triggers, the declared privileges, the SID type, and -
/// on a handle of its own, for the reason written at it - the security descriptor. What stays
/// behind is opening the manager, enumerating it, and turning one configuration buffer into
/// fields.
///
/// <b>The ratchet asked a third time on 2026-08-26, and the seam it found was the other half of
/// the same sentence.</b> Asking a handle a question is not the same job as walking the block that
/// comes back, and the two go wrong in different ways - one is refused by the manager, the other
/// walks off the end of an array. The walks moved to <see cref="ManagerBlocks"/>, which is also
/// where they can be handed bytes by a test rather than needing a machine.
/// </summary>
internal static class ScmDetailReader
{
    /// <summary>
    /// Whether an entry is marked to start late.
    ///
    /// Asked for everything that can carry the setting, which is every non-driver entry.
    /// Drivers do not have the notion at all, and for them this comes back absent - saying
    /// the idea does not apply rather than claiming somebody checked and found no delay.
    ///
    /// <b>This used to be asked only for automatic entries</b>, on the grounds that Windows
    /// ignores it elsewhere - which confused what the setting does with whether it exists.
    /// The body below has the measurement that changed it.
    /// </summary>
    /// <remarks>
    /// The start type used to be a parameter here and was never read - a leftover from when this
    /// was asked only of automatic entries. A signature that names something the body ignores is
    /// a claim about what the answer depends on, and this one was false.
    /// </remarks>
    internal static unsafe Reading<bool> ReadDelayedAuto(SafeHandle service, EnumeratedEntry enumerated)
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
    /// nothing and be told how much room the answer wants.
    ///
    /// <b>The walk over what came back lives in <see cref="ManagerBlocks.ReadTriggerBuffer"/>.</b>
    /// The structure hands back a pointer into that very buffer, so it has to be followed while
    /// the block is still pinned and it has to be bounded by the block - two rules that belong
    /// with the other bounded walk rather than with the questions asked of a handle. It is also
    /// the half that can be handed a malformed answer by a test.
    /// </summary>
    internal static unsafe Reading<IReadOnlyList<ServiceTrigger>> ReadTriggers(SafeHandle service)
    {
        PInvoke.QueryServiceConfig2W(
            service, SERVICE_CONFIG.SERVICE_CONFIG_TRIGGER_INFO, default, out var needed);

        if (needed == 0)
        {
            return Refused<IReadOnlyList<ServiceTrigger>>(Marshal.GetLastWin32Error());
        }

        var buffer = new byte[needed];

        // PINNED FROM THE CALL TO THE WALK, and until 2026-09-02 the comment above this method
        // described that as a requirement while the code did not meet it. The interop wrapper pins
        // only for the duration of the native call, and SERVICE_TRIGGER_INFO carries an absolute
        // pointer into this block - so a collection between the two left the walk aimed at an
        // address the array had left. Backlog 297.
        fixed (byte* pinned = buffer)
        {
            if (!PInvoke.QueryServiceConfig2W(
                    service, SERVICE_CONFIG.SERVICE_CONFIG_TRIGGER_INFO,
                    new Span<byte>(pinned, buffer.Length), out _))
            {
                return Refused<IReadOnlyList<ServiceTrigger>>(Marshal.GetLastWin32Error());
            }

            return ManagerBlocks.ReadTriggerBuffer(buffer);
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
    internal static unsafe Reading<IReadOnlyList<string>> ReadRequiredPrivileges(SafeHandle service)
    {
        PInvoke.QueryServiceConfig2W(
            service, SERVICE_CONFIG.SERVICE_CONFIG_REQUIRED_PRIVILEGES_INFO, default, out var needed);

        if (needed == 0)
        {
            return Refused<IReadOnlyList<string>>(Marshal.GetLastWin32Error());
        }

        var buffer = new byte[needed];

        // THE CALL IS INSIDE THE BLOCK, NOT ABOVE IT, since 2026-09-02. The pinning was already
        // here and already argued for - it just started one statement too late, so the pointer the
        // manager wrote was taken against an address the array was free to leave before the walk
        // below picked it up. Backlog 297.
        fixed (byte* start = buffer)
        {
            if (!PInvoke.QueryServiceConfig2W(
                    service, SERVICE_CONFIG.SERVICE_CONFIG_REQUIRED_PRIVILEGES_INFO,
                    new Span<byte>(start, buffer.Length), out _))
            {
                return Refused<IReadOnlyList<string>>(Marshal.GetLastWin32Error());
            }

            if (buffer.Length < sizeof(SERVICE_REQUIRED_PRIVILEGES_INFOW))
            {
                // Room reported for less than the structure the call promises. Nothing to read.
                return Reading<IReadOnlyList<string>>.Absent();
            }

            // The structure is one pointer into this very buffer, so the multi-string is read
            // inside the fixed block for the same reason the triggers are.
            var privileges = ManagerBlocks.ReadMultiString(
                ((SERVICE_REQUIRED_PRIVILEGES_INFOW*)start)->pmszRequiredPrivileges, start, buffer.Length);

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
    internal static unsafe Reading<ServiceSidType> ReadSidType(SafeHandle service)
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
    /// The sentence a person reads to find out what this entry is for.
    ///
    /// <b>The manager is asked rather than the registry, and that is measured rather than a
    /// reflex about not taking shortcuts.</b> Over 819 entries on 2026-08-12, <b>398 of the 444
    /// descriptions the registry holds are indirections</b> of the form
    /// <c>@%SystemRoot%\system32\adpsvc.dll,-103</c> rather than sentences - the text lives in a
    /// resource inside a binary, and only the manager resolves it. It resolved <b>388</b> of
    /// them. Reading the key would hand a person the indirection and call it a description.
    ///
    /// <b>The ten it does not resolve come back refused rather than absent</b>, because those are
    /// different answers about a service: absent means the entry has no description, and this
    /// means it has one nobody could turn into words. The manager reports the failure by handing
    /// back the indirection unresolved, so a value still arriving with a leading <c>@</c> is the
    /// shape that has to be caught here - nothing in the return code says so.
    ///
    /// <b>Absent is the ordinary case and not an edge:</b> 384 of 819 entries have no description
    /// at all, which is nearly every driver. An empty string would claim the manager answered
    /// with emptiness.
    ///
    /// Variable length, so the same two-call dance as the triggers, on the handle the
    /// configuration already opened - this level needs no right the listing does not have. The
    /// whole family costs <b>212-223 ms over 819 entries</b>, four warm runs, which puts it in the
    /// cheap pass beside the privileges rather than in the second pass of `ADR-13` beside the
    /// signatures at 4620-7656 ms.
    ///
    /// <b>Translated, like the display name, so it is never an identity</b> - `ADR-14`. The
    /// longest one measured is 1251 characters and two contain a newline, which is a fact for
    /// whatever shows it rather than for this method.
    /// </summary>
    internal static unsafe Reading<string> ReadDescription(SafeHandle service)
    {
        PInvoke.QueryServiceConfig2W(
            service, SERVICE_CONFIG.SERVICE_CONFIG_DESCRIPTION, default, out var needed);

        if (needed == 0)
        {
            return Refused<string>(Marshal.GetLastWin32Error());
        }

        var buffer = new byte[needed];

        // The call is inside the block for the reason set out at ReadRequiredPrivileges above:
        // SERVICE_DESCRIPTIONW is one absolute pointer into this block, taken while the call runs,
        // and nothing kept the block still between then and the reading. Backlog 297.
        fixed (byte* start = buffer)
        {
            if (!PInvoke.QueryServiceConfig2W(
                    service, SERVICE_CONFIG.SERVICE_CONFIG_DESCRIPTION,
                    new Span<byte>(start, buffer.Length), out _))
            {
                return Refused<string>(Marshal.GetLastWin32Error());
            }

            if (buffer.Length < sizeof(SERVICE_DESCRIPTIONW))
            {
                // Room reported for less than the structure the call promises. Nothing to read.
                return Reading<string>.Absent();
            }

            // The structure is one pointer into this very buffer, so the string is read inside
            // the fixed block for the same reason the triggers are.
            //
            // Which of the three answers it is lives in ServiceDescription, public and checkable
            // without a machine - the shape that matters most is the one the system reports by NOT
            // failing, and a rule reachable only through this method could never be tested for the
            // ten entries it applies to.
            return ServiceDescription.Of(((SERVICE_DESCRIPTIONW*)start)->lpDescription.ToString());
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
    internal static unsafe Reading<string> ReadSecurityDescriptor(SafeHandle manager, EnumeratedEntry enumerated)
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
    /// A refusal, carrying both halves: the system's number for a script and the system's
    /// sentence for a person. Reading the last error once, here, so that no caller has to
    /// remember that the next call would overwrite it.
    /// </summary>
    private static Reading<T> Refused<T>(int code) => Reading<T>.Denied(code, ManagerTerms.Describe(code));

    /// <summary>
    /// Owner, group and permissions - the three parts of a descriptor that can be read with
    /// READ_CONTROL alone. The fourth, the audit list, is left out on purpose: see
    /// ReadSecurityDescriptor.
    /// </summary>
    private const uint DescriptorParts =
        (uint)(OBJECT_SECURITY_INFORMATION.OWNER_SECURITY_INFORMATION
            | OBJECT_SECURITY_INFORMATION.GROUP_SECURITY_INFORMATION
            | OBJECT_SECURITY_INFORMATION.DACL_SECURITY_INFORMATION);
}