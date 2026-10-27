using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security.Authentication.Identity;
using Windows.Win32.System.EventLog;

namespace Bws.Core.History;

/// <summary>
/// Reads the change history out of this machine's System log.
///
/// <b>Through wevtapi rather than System.Diagnostics.Eventing.Reader</b> - NativeMethods.txt carries
/// the measurement: that namespace is not in the terminal's shared framework, so it would have been a
/// new package for bws under ADR-15.
///
/// <b>Readable without administrator rights, and measured so rather than assumed:</b> under a
/// restricted token on 2026-10-15 (tools/restricted/run.ps1) the System log answered in full - the
/// same 102 events 7040 and 7045 an elevated session saw. The Security log refused, which is one of
/// the reasons event 4697 is not read here.
///
/// <b>A query handle may only be used on the thread that made it</b> (the documentation of EvtQuery),
/// so one <see cref="Read"/> runs start to finish on whatever thread called it. The window, when it
/// reads this, has to call it from one background thread and not hop.
///
/// <b>Every failure to read is code 1, said with the number Windows gave</b>, the way a refused
/// service manager is in <see cref="WindowsScmCatalog"/>: a history that could not be read is not an
/// empty history, and printing nothing would say there were no changes.
/// </summary>
public sealed class WindowsChangeHistoryReader : IChangeHistoryReader
{
    private const string Channel = "System";

    /// <summary>
    /// Both events, from the service manager only - the query names the provider as well as the
    /// numbers, because event numbers belong to providers and another one may reuse 7040.
    /// </summary>
    private const string Changes =
        "*[System[Provider[@Name='Service Control Manager'] and (EventID=7040 or EventID=7045)]]";

    /// <summary>How many event handles one EvtNext hands over. Measured: 102 events read in 4 ms.</summary>
    private const int Batch = 64;

    public ChangeHistory Read()
    {
        var facts = Facts();

        // The oldest record says how far back the log reaches, and - when it is the event Windows
        // writes after emptying a log - who emptied it.
        var oldest = Rendered("*", newestFirst: false, limit: 1, xml => xml);

        // EACH EVENT PARSED AS IT IS RENDERED, since 2026-10-27 - logic report C02, owner's decision "parse
        // on the fly". Until then every event was kept as its XML in one list and parsed afterwards, so a
        // large log held all of its text and all of its changes at once. The range and the absence of a
        // limit are unchanged, by the same decision: only the text goes once it has been read.
        var changes = Rendered(Changes, newestFirst: true, limit: int.MaxValue, xml => ChangeEvents.Change(xml)
            ?? throw new InvalidDataException(
                "The System log answered a query for events 7040 and 7045 with an event that is neither."));

        var cleared = oldest.Count == 0 ? null : ChangeEvents.ClearingOf(oldest[0], Channel);
        var reach = oldest.Count == 0 ? null : ChangeEvents.TimeOf(oldest[0]);

        var names = AccountNames.Of(
            changes.Select(change => change.By?.Sid).Append(cleared?.By?.Sid), LocalDomain());

        return new ChangeHistory(
            ChangeHistory.Cover(Channel, facts, reach, cleared is null ? null : cleared with { By = Named(cleared.By, names) }),
            [.. changes.Select(change => change with { By = Named(change.By, names) })]);
    }

    private static Actor? Named(Actor? by, IReadOnlyDictionary<string, string> names) =>
        by is null ? null : by with { Name = names.GetValueOrDefault(by.Sid) };

    private static LogFacts Facts()
    {
        using var log = PInvoke.EvtOpenLog(null, Channel, (uint)EVT_OPEN_LOG_FLAGS.EvtOpenChannelPath);

        if (log.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the System event log.");
        }

        var size = Property(log, EVT_LOG_PROPERTY_ID.EvtLogFileSize, EVT_VARIANT_TYPE.EvtVarTypeUInt64).UInt64Val;
        var full = Property(log, EVT_LOG_PROPERTY_ID.EvtLogFull, EVT_VARIANT_TYPE.EvtVarTypeBoolean).BooleanVal;

        using var channel = PInvoke.EvtOpenChannelConfig(null, Channel, 0);

        if (channel.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not read how the System event log is configured.");
        }

        if (!PInvoke.EvtGetChannelConfigProperty(
                channel, EVT_CHANNEL_CONFIG_PROPERTY_ID.EvtChannelLoggingConfigMaxSize, 0, out EVT_VARIANT maximum, out _))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not read the size limit of the System event log.");
        }

        Expect(maximum, EVT_VARIANT_TYPE.EvtVarTypeUInt64, "size limit");

        return new LogFacts(size, maximum.UInt64Val, full, Mode(channel));
    }

    /// <summary>
    /// What the channel does at its size limit - H02, 2026-10-27. Microsoft's channel schema: retention true
    /// is a sequential log, false a circular one, and autoBackup may only be true with retention, when the
    /// full file is put aside as an archive. Autobackup without retention is not a shape Windows accepts, so
    /// such an answer is read as what retention alone says - circular.
    /// </summary>
    private static LogMode Mode(SafeHandle channel)
    {
        var retention = Configured(channel, EVT_CHANNEL_CONFIG_PROPERTY_ID.EvtChannelLoggingConfigRetention, "retention");
        var autoBackup = Configured(channel, EVT_CHANNEL_CONFIG_PROPERTY_ID.EvtChannelLoggingConfigAutoBackup, "automatic backup");

        return !retention ? LogMode.Circular
            : autoBackup ? LogMode.AutoBackup
            : LogMode.Retain;
    }

    private static bool Configured(SafeHandle channel, EVT_CHANNEL_CONFIG_PROPERTY_ID property, string what)
    {
        if (!PInvoke.EvtGetChannelConfigProperty(channel, property, 0, out EVT_VARIANT value, out _))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not read the {what} setting of the System event log.");
        }

        Expect(value, EVT_VARIANT_TYPE.EvtVarTypeBoolean, what);

        return value.BooleanVal;
    }

    private static EVT_VARIANT Property(SafeHandle log, EVT_LOG_PROPERTY_ID property, EVT_VARIANT_TYPE type)
    {
        if (!PInvoke.EvtGetLogInfo(log, property, out EVT_VARIANT value, out _))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not read {property} of the System event log.");
        }

        Expect(value, type, property.ToString());

        return value;
    }

    /// <summary>
    /// A property of a kind other than the one asked for is refused rather than read through the
    /// wrong member of the union - which would hand back a size made of a boolean's bytes.
    /// </summary>
    private static void Expect(EVT_VARIANT value, EVT_VARIANT_TYPE type, string what)
    {
        if (value.Type != (uint)type)
        {
            throw new InvalidDataException(
                $"The System event log answered its {what} as variant type {value.Type}, not {type}.");
        }
    }

    /// <summary>
    /// The events a query matches, newest or oldest first, each rendered as XML and handed to
    /// <paramref name="parse"/> at once - the text of one event is all that is held at a time.
    /// </summary>
    private static List<T> Rendered<T>(string query, bool newestFirst, int limit, Func<string, T> parse)
    {
        var direction = newestFirst ? EVT_QUERY_FLAGS.EvtQueryReverseDirection : EVT_QUERY_FLAGS.EvtQueryForwardDirection;

        using var results = PInvoke.EvtQuery(null, Channel, query, (uint)(EVT_QUERY_FLAGS.EvtQueryChannelPath | direction));

        if (results.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not query the System event log.");
        }

        var found = new List<T>();
        var handles = new nint[Batch];
        var renderer = new Renderer();

        while (found.Count < limit && Next(results, handles) is var returned && returned > 0)
        {
            // Every handle owned before any is rendered, so one that fails cannot leave the rest
            // of the batch open.
            var events = handles.Take((int)returned).Select(handle => new EvtCloseSafeHandle(handle, ownsHandle: true)).ToArray();

            try
            {
                found.AddRange(events.Take(limit - found.Count).Select(one => parse(renderer.Xml(one))));
            }
            finally
            {
                Array.ForEach(events, one => one.Dispose());
            }
        }

        return found;
    }

    /// <summary>
    /// The next batch of event handles, and how many - zero at the end of the results.
    ///
    /// Asked through the return value first, and the last error only on a failure - the rule
    /// WindowsScmCatalog.Enumerate sets out, because a call that worked leaves whatever the previous
    /// one put there. The end of the results is a failure with its own number.
    /// </summary>
    private static uint Next(SafeHandle results, nint[] handles)
    {
        uint returned = 0;

        // How many handles to hand over is the length of the span - the generated overload passes it
        // as the count. The number here is the TIMEOUT, INFINITE, named so it cannot be read as a
        // count of events (it was, in the review of #70).
        if (PInvoke.EvtNext(results, handles, Timeout: uint.MaxValue, Flags: 0, ref returned))
        {
            return returned;
        }

        var error = Marshal.GetLastWin32Error();

        return error == (int)WIN32_ERROR.ERROR_NO_MORE_ITEMS
            ? 0u
            : throw new Win32Exception(error, "Could not read the System event log.");
    }

    /// <summary>
    /// Renders events as XML into a buffer it keeps between them, grown when one needs more room.
    /// </summary>
    private sealed class Renderer
    {
        private byte[] _buffer = new byte[16 * 1024];

        internal string Xml(SafeHandle one)
        {
            while (true)
            {
                if (PInvoke.EvtRender(null, one, (uint)EVT_RENDER_FLAGS.EvtRenderEventXml, _buffer, out var used, out _))
                {
                    return Encoding.Unicode.GetString(_buffer, 0, (int)used).TrimEnd('\0');
                }

                var error = Marshal.GetLastWin32Error();

                if (error != (int)WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER)
                {
                    throw new Win32Exception(error, "Could not render an event from the System event log.");
                }

                // Never past what one event can render to - ChangeEvents.LargestRendering says why that
                // is a bound and not a guess. Asked in the review of #70: the room an event asks for is
                // a number from the log, and this would have handed it to the allocator unread.
                if (used > ChangeEvents.LargestRendering)
                {
                    throw new InvalidDataException(
                        $"An event in the System log asks for {used} bytes to render, more than one event can hold.");
                }

                // The call says how much room it wanted. Grown and kept for the events after this one.
                _buffer = new byte[used];
            }
        }
    }

    /// <summary>
    /// The SID of this machine's own account domain, from the local security authority - or null
    /// when it would not say, and then only the accounts that belong to no domain get a name. Nothing
    /// is hidden by that: a SID without a name is printed as itself.
    /// </summary>
    private static unsafe SecurityIdentifier? LocalDomain()
    {
        var attributes = default(LSA_OBJECT_ATTRIBUTES);

        // Zero is STATUS_SUCCESS. The handle is owned before the answer is looked at, so a refusal
        // that still handed one back cannot leave it open.
        var opened = PInvoke.LsaOpenPolicy(null, in attributes, PInvoke.POLICY_VIEW_LOCAL_INFORMATION, out var policy);

        using (policy)
        {
            if (opened.Value != 0
                || PInvoke.LsaQueryInformationPolicy(policy, POLICY_INFORMATION_CLASS.PolicyAccountDomainInformation, out var buffer).Value != 0)
            {
                return null;
            }

            try
            {
                var domain = ((POLICY_ACCOUNT_DOMAIN_INFO*)buffer)->DomainSid;

                return domain.Value is null ? null : new SecurityIdentifier((nint)domain.Value);
            }
            finally
            {
                // Handing the memory back. A failure to free has nothing a caller could act on, and
                // turning it into an answer would replace the domain with a complaint about tidying.
                _ = PInvoke.LsaFreeMemory(buffer);
            }
        }
    }
}
