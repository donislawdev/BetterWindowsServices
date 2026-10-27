using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using Windows.Win32.System.Services;

namespace Bws.Core;

/// <summary>
/// The trigger walk: the records of a trigger block, and since 2026-10-27 everything each record points
/// at - its subtype and its data items (logic report C01, snapshot schema seven).
///
/// <b>Its own file since that day</b>, because the walk grew from one array to three levels of pointers
/// into one block, and it is one subject with one way of going wrong: every level is a count or a pointer
/// the manager wrote, and none of them is believed until it is checked against the block.
/// </summary>
internal static partial class ManagerBlocks
{
    /// <summary>
    /// The triggers inside a block the manager has already filled.
    ///
    /// <b>Its own method so that a test can hand it a block, which is the only way anything below
    /// can be checked at all.</b> The half above needs a service handle and therefore a machine
    /// with a service on it - this half needs bytes, and the bytes that matter are the ones a
    /// machine does not produce on demand. Same shape as <see cref="ReadDependentNames"/> and
    /// <see cref="ReadConfigurationBuffer"/>, for the same reason. <b>That sentence named those
    /// readers as "left in WindowsScmCatalog" until 2026-09-02, and the last of them arrived in this
    /// class that day</b> - the seam was named in this header long before it was taken, and what
    /// finally took it was the size ratchet on the file they were leaving.
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
    ///
    /// <b>What a record points at is refused rather than held down, unlike the record count</b>
    /// (2026-10-27). A count of records larger than the block keeps the records that fit, as it has
    /// since 2026-08-26. A subtype or a data item outside the block refuses the whole field with
    /// ERROR_INVALID_DATA, the way a configuration field outside its block is refused since R-4 -
    /// because a trigger with one of its data items quietly dropped is a different trigger: a device
    /// trigger fires on any of its hardware IDs and an ETW trigger on its filters, so one item fewer
    /// widens or narrows what the entry waits for, and the reading would say so as fact.
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

            // READ THROUGH THE OFFSET THAT WAS CHECKED, NOT THROUGH THE POINTER THAT WAS NOT, and
            // until 2026-09-02 this loop did the second. The check above establishes that the
            // offset lands inside the block, and then the reading went back to info.pTriggers -
            // so in the one case the check exists for, a pointer landing in range by accident, it
            // validated one address and dereferenced another. A guard that looks like a guard and
            // is not is worse than no guard, because the next reader stops looking. Backlog 297.
            var records = (SERVICE_TRIGGER*)(start + offset);

            return Records(records, count, start, buffer.Length, triggers)
                ? Reading<IReadOnlyList<ServiceTrigger>>.Present(triggers)
                : OutsideTheBlock<IReadOnlyList<ServiceTrigger>>();
        }
    }

    /// <summary>Every record whole into <paramref name="triggers"/>, or false at the first that is not.</summary>
    private static unsafe bool Records(
        SERVICE_TRIGGER* records, uint count, byte* block, int length, List<ServiceTrigger> triggers)
    {
        for (uint index = 0; index < count; index++)
        {
            if (Whole(records[index], block, length) is not { } trigger)
            {
                return false;
            }

            triggers.Add(trigger);
        }

        return true;
    }

    /// <summary>
    /// One record with its subtype and its data items, or null when anything it points at is outside
    /// the block or is not the size its type says.
    /// </summary>
    private static unsafe ServiceTrigger? Whole(SERVICE_TRIGGER record, byte* block, int length)
    {
        Guid? subtype = null;

        if (record.pTriggerSubtype is not null)
        {
            if (Within(record.pTriggerSubtype, sizeof(Guid), block, length) is not { } at)
            {
                return null;
            }

            subtype = *(Guid*)(block + at);
        }

        var data = Items(record.cDataItems, record.pDataItems, block, length);

        return data is null
            ? null
            : new ServiceTrigger(
                ManagerTerms.Trigger(record.dwTriggerType), ManagerTerms.TriggerAction(record.dwAction), subtype, data);
    }

    /// <summary>The data items of one record, in the manager's order, or null when one cannot be read whole.</summary>
    private static unsafe List<TriggerData>? Items(
        uint count, SERVICE_TRIGGER_SPECIFIC_DATA_ITEM* items, byte* block, int length)
    {
        if (count == 0)
        {
            return [];
        }

        // The whole array inside the block before any of it is read - a count that does not fit is a
        // block that does not describe itself, and the list is not started on a guess.
        if (Within(items, (long)count * sizeof(SERVICE_TRIGGER_SPECIFIC_DATA_ITEM), block, length) is not { } at)
        {
            return null;
        }

        var records = (SERVICE_TRIGGER_SPECIFIC_DATA_ITEM*)(block + at);
        var data = new List<TriggerData>((int)count);

        for (uint index = 0; index < count; index++)
        {
            var item = records[index];

            // An item of no bytes points at nothing worth checking - the manager may leave it null.
            var from = item.cbData == 0 ? 0 : Within(item.pData, item.cbData, block, length);

            if (from is null
                || Value(item.dwDataType, new ReadOnlySpan<byte>(block + from.Value, (int)item.cbData)) is not { } value)
            {
                return null;
            }

            data.Add(value);
        }

        return data;
    }

    /// <summary>
    /// Where a run of bytes the manager pointed at starts inside the block, or null when any byte of
    /// it lies outside - a null pointer included, which lands below the block.
    /// </summary>
    private static unsafe long? Within(void* pointer, long size, byte* block, int length)
    {
        var offset = (byte*)pointer - block;

        return offset >= 0 && offset <= length && size <= length - offset ? offset : null;
    }

    /// <summary>
    /// One data item as text, in the form <see cref="TriggerData"/> describes - or null for a level
    /// that is not one byte, a keyword that is not eight, or a string of an odd number of bytes. None
    /// of those is a shape the documentation allows, so each is a block that does not describe itself.
    /// </summary>
    private static TriggerData? Value(SERVICE_TRIGGER_SPECIFIC_DATA_ITEM_DATA_TYPE type, ReadOnlySpan<byte> bytes) => type switch
    {
        SERVICE_TRIGGER_SPECIFIC_DATA_ITEM_DATA_TYPE.SERVICE_TRIGGER_DATA_TYPE_BINARY =>
            new TriggerData(TriggerDataType.Binary, [Convert.ToHexStringLower(bytes)]),

        SERVICE_TRIGGER_SPECIFIC_DATA_ITEM_DATA_TYPE.SERVICE_TRIGGER_DATA_TYPE_STRING =>
            bytes.Length % sizeof(char) == 0 ? new TriggerData(TriggerDataType.String, Strings(bytes)) : null,

        SERVICE_TRIGGER_SPECIFIC_DATA_ITEM_DATA_TYPE.SERVICE_TRIGGER_DATA_TYPE_LEVEL =>
            bytes.Length == 1
                ? new TriggerData(TriggerDataType.Level, [bytes[0].ToString(CultureInfo.InvariantCulture)])
                : null,

        SERVICE_TRIGGER_SPECIFIC_DATA_ITEM_DATA_TYPE.SERVICE_TRIGGER_DATA_TYPE_KEYWORD_ANY => Keyword(TriggerDataType.KeywordAny, bytes),
        SERVICE_TRIGGER_SPECIFIC_DATA_ITEM_DATA_TYPE.SERVICE_TRIGGER_DATA_TYPE_KEYWORD_ALL => Keyword(TriggerDataType.KeywordAll, bytes),

        // A type the documentation does not name: what is there is kept, and the type says it is not ours.
        _ => new TriggerData(TriggerDataType.Unknown, [Convert.ToHexStringLower(bytes)])
    };

    private static TriggerData? Keyword(TriggerDataType type, ReadOnlySpan<byte> bytes) =>
        bytes.Length == sizeof(ulong)
            ? new TriggerData(type, ["0x" + BinaryPrimitives.ReadUInt64LittleEndian(bytes).ToString("x16", CultureInfo.InvariantCulture)])
            : null;

    /// <summary>
    /// The strings of one item: split at every null, the nulls that close the run taken off the end, and
    /// one empty string when nothing is left - so an item always has a value to read.
    ///
    /// <b>Split at every null rather than stopped at the first pair</b>, the way <see cref="ReadMultiString"/>
    /// stops: this run has its own length, so an empty string in the middle is something the manager holds
    /// rather than the end of the run, and keeping it is what keeps the item whole.
    /// </summary>
    private static string[] Strings(ReadOnlySpan<byte> bytes)
    {
        var values = new string(MemoryMarshal.Cast<byte, char>(bytes)).Split('\0');
        var kept = values.Length;

        while (kept > 1 && values[kept - 1].Length == 0)
        {
            kept--;
        }

        return values[..kept];
    }
}
