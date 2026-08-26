using Windows.Win32.Foundation;
using Windows.Win32.System.Services;

namespace Bws.Core;

/// <summary>
/// Blocks the manager filled, walked without believing what they say about themselves.
///
/// <b>Moved out of <c>ScmDetailReader</c> on 2026-08-26 because the size ratchet said so, and the
/// seam it pointed at was already there.</b> Everything left in that file ASKS the manager
/// something - it opens a handle, calls one more information level, and hands the answer back.
/// These do not ask anything. They are given a block that a call has already filled and they walk
/// it, which is a different job with a different way of going wrong.
///
/// <b>The way it goes wrong is the reason these belong together.</b> A block carries its own
/// description - a count, or a pointer into itself - and nothing checks that description against
/// the block. Believing it walks off the end of a managed array, and this tool runs elevated on
/// production servers, where a memory fault is indistinguishable from a bug of ours and ends the
/// process without a word. Both of these are therefore bounded by the block rather than by what
/// the block claims, and putting them in one file is what makes that a family rather than three
/// separate careful moments. Owner's decision, 2026-08-03, applied to the last of them on
/// 2026-08-26.
///
/// <b>Taking a block rather than a handle is also what makes them checkable.</b> The half that
/// asks the manager needs a machine with a service on it and cannot be handed a malformed answer
/// on purpose. This half needs bytes, and a test can write the bytes that a machine will not
/// produce on request.
/// </summary>
internal static class ManagerBlocks
{
    /// <summary>
    /// The triggers inside a block the manager has already filled.
    ///
    /// <b>Its own method so that a test can hand it a block, which is the only way anything below
    /// can be checked at all.</b> The half above needs a service handle and therefore a machine
    /// with a service on it - this half needs bytes, and the bytes that matter are the ones a
    /// machine does not produce on demand. Same shape as <see cref="ReadDependentNames"/> beside it
    /// and as the two buffer readers left in <c>WindowsScmCatalog</c>, for the same reason.
    ///
    /// <b>BOUNDED BY THE BLOCK SINCE 2026-08-26, and until then this walked a count and a pointer
    /// that both came from the manager, with neither checked against the block they describe.</b>
    /// Every other buffer here limits itself - <see cref="ReadMultiString"/> by the pointer landing
    /// inside, the enumeration and configuration buffers by their own length. This was the one
    /// left, and it is the one where being wrong costs the most: a record past the end is an access
    /// violation, which ends the process outright with no report, possibly in the middle of listing
    /// eight hundred entries on somebody else's server. Owner's decision of 2026-08-03, applied in
    /// the last place that had not had it.
    ///
    /// <b>The pointer is checked as well as the count</b>, because either can be wrong on its own.
    /// It closes a second way this could have gone wrong as well: the structure points into the
    /// very block the call filled, so an array that moved between that call and the fixed statement
    /// below would leave an offset nowhere near the buffer - refused here rather than followed.
    ///
    /// <b>A block that does not describe itself comes back as nothing read rather than as no
    /// triggers.</b> An empty list would be a claim about the service - that it declares none -
    /// and that claim is exactly what could not be established.
    /// </summary>
    internal static unsafe Reading<IReadOnlyList<ServiceTrigger>> ReadTriggerBuffer(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (buffer.Length < sizeof(SERVICE_TRIGGER_INFO))
        {
            return Reading<IReadOnlyList<ServiceTrigger>>.Absent();
        }

        fixed (byte* start = buffer)
        {
            var info = *(SERVICE_TRIGGER_INFO*)start;

            if (info.cTriggers == 0)
            {
                // A fact about the service: most entries have none.
                return Reading<IReadOnlyList<ServiceTrigger>>.Absent();
            }

            var offset = (byte*)info.pTriggers - start;

            if (info.pTriggers is null || offset < 0 || offset >= buffer.Length)
            {
                return Reading<IReadOnlyList<ServiceTrigger>>.Absent();
            }

            var room = (uint)((buffer.Length - offset) / sizeof(SERVICE_TRIGGER));
            var count = Math.Min(info.cTriggers, room);

            if (count == 0)
            {
                return Reading<IReadOnlyList<ServiceTrigger>>.Absent();
            }

            var triggers = new List<ServiceTrigger>((int)count);

            for (uint index = 0; index < count; index++)
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
    /// Reads one of the manager's multi-strings: values back to back, each ending in a
    /// null, the whole run ending in a second one.
    ///
    /// Written out by hand because the marshalling helper for a string stops at the first
    /// null and would hand back only the first dependency. That failure is quiet - a
    /// service declaring five dependencies would report one, and the cascade built on it
    /// would look reasonable and be wrong.
    /// </summary>
    /// <param name="buffer">The block this string was read out of.</param>
    /// <param name="length">How long that block is, in bytes.</param>
    /// <remarks>
    /// <b>Bounded by the buffer since 2026-08-03, and before that by nothing at all.</b> The walk
    /// ran until it met two nulls in a row, so a multi-string the manager did not terminate -
    /// truncated, or simply not what this code believes it is - would have carried the loop
    /// straight out of a managed array and into whatever follows it. Every other buffer here is
    /// at least described by a count. This one had neither a count nor a limit.
    ///
    /// The trust that made that acceptable is real: the manager and this process are the same
    /// machine and the same kernel. It was also unwritten, which is the part that was wrong -
    /// and this tool runs elevated on production servers, which is a poor place to keep an
    /// unwritten assumption about memory. Owner's decision, 2026-08-03.
    ///
    /// Reading is bounded too, not just the walk. Asking for a string at a pointer reads until
    /// a null wherever that null happens to be, so the terminator is found inside the remaining
    /// span first and the string is built from that.
    /// </remarks>
    internal static unsafe List<string> ReadMultiString(PWSTR start, byte* buffer, int length)
    {
        var values = new List<string>();
        var cursor = start.Value;

        if (cursor is null)
        {
            return values;
        }

        // The structure hands back a pointer into the very block it came from. One that does not
        // land there is not something to make the best of - it is a reading nobody can trust.
        var offset = (byte*)cursor - buffer;

        if (offset < 0 || offset >= length)
        {
            return values;
        }

        var end = (char*)(buffer + length);

        while (cursor < end && *cursor != '\0')
        {
            var remaining = new ReadOnlySpan<char>(cursor, (int)(end - cursor));
            var terminator = remaining.IndexOf('\0');

            if (terminator < 0)
            {
                // The run reaches the end of the block without closing. What is there is what
                // there is, and it stops here rather than reading on.
                values.Add(new string(remaining));

                break;
            }

            values.Add(new string(remaining[..terminator]));
            cursor += terminator + 1;
        }

        return values;
    }

    /// <summary>
    /// The names inside a block of dependent records the manager has already filled.
    ///
    /// <b>Moved here from <c>WindowsScmCatalog</c> on 2026-08-26, and the move is the point rather
    /// than a side effect.</b> It was already the shape this file is about - a count the manager
    /// declared, held down to what the block can actually hold - and it sat in a file whose subject
    /// is opening handles and asking questions. Standing beside the other two bounded walks is what
    /// makes them one rule instead of three separate careful moments.
    ///
    /// The count is clamped rather than trusted, for the reason written at the top of this file.
    /// </summary>
    internal static unsafe List<string> ReadDependentNames(byte[] buffer, uint count)
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

    internal static unsafe List<EnumeratedEntry> ReadEnumerationBuffer(byte[] buffer, uint count)
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
                    PerUserRole: ManagerTerms.PerUserRole(status.dwServiceType),
                    Status: ManagerTerms.Status(status.dwCurrentState),
                    ProcessId: status.dwProcessId));
            }
        }

        return entries;
    }
}
