using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace Bws.Core.Tests;

/// <summary>
/// What a trigger record points at - its subtype and its data items - handed over as a block.
///
/// <b>Written 2026-10-27 for logic report C01</b>, when the walk went from one array to three levels
/// of pointers into one block. Each level is a count or a pointer the manager wrote, so each gets the
/// question <see cref="ManagerBlockTests"/> asks of the first: what happens when it is wrong. The answer
/// differs from the record count's on purpose - a data item outside the block refuses the field rather
/// than being dropped, because a trigger missing one of its items waits for something else.
/// </summary>
public sealed class TriggerBlockTests
{
    /// <summary>SERVICE_TRIGGER_INFO on a 64 bit machine: the count at 0, the records pointer at 8.</summary>
    private const int HeaderLength = 24;

    /// <summary>
    /// SERVICE_TRIGGER: the kind at 0, the action at 4, the subtype pointer at 8, the item count at 16
    /// and the items pointer at 24 - thirty two bytes.
    /// </summary>
    private const int RecordLength = 32;

    private const int SubtypePointer = 8;
    private const int ItemCount = 16;
    private const int ItemsPointer = 24;

    /// <summary>SERVICE_TRIGGER_SPECIFIC_DATA_ITEM: the type at 0, the size at 4, the data pointer at 8.</summary>
    private const int ItemLength = 16;

    private const int GuidLength = 16;

    private const uint Binary = 1;
    private const uint Text = 2;
    private const uint Level = 3;
    private const uint KeywordAny = 4;
    private const uint KeywordAll = 5;

    private static readonly Guid Provider = new("f5528ada-be5f-4f14-8aef-a95de7281161");

    [Fact]
    public void A_trigger_is_read_whole_with_its_subtype_and_every_type_of_data()
    {
        var reading = Walked(
            Provider,
            [
                (Binary, [0x75, 0x28, 0xBC]),
                (Text, Encoding.Unicode.GetBytes("500\0UDP\0\0")),
                (Level, [4]),
                (KeywordAny, Little(1)),
                (KeywordAll, Little(0x8000000000000010)),
                (9, [0xAB])
            ]);

        var trigger = Assert.Single(reading.Value!);

        Assert.Equal(TriggerKind.Custom, trigger.Kind);
        Assert.Equal(Provider, trigger.Subtype);

        Assert.Equal(
            [
                (TriggerDataType.Binary, "7528bc"),
                (TriggerDataType.String, "500|UDP"),
                (TriggerDataType.Level, "4"),
                (TriggerDataType.KeywordAny, "0x0000000000000001"),
                (TriggerDataType.KeywordAll, "0x8000000000000010"),
                (TriggerDataType.Unknown, "ab")
            ],
            trigger.Data.Select(item => (item.Type, string.Join('|', item.Values))));
    }

    /// <summary>
    /// The run has its own length, so an empty string in the middle is something the manager holds and
    /// is kept, and an item with no text at all still has one value to read.
    /// </summary>
    [Fact]
    public void A_string_item_keeps_an_empty_string_inside_it_and_always_has_a_value()
    {
        var reading = Walked(Provider, [(Text, Encoding.Unicode.GetBytes("a\0\0b\0\0")), (Text, [])]);

        var data = Assert.Single(reading.Value!).Data;

        Assert.Equal(["a", "", "b"], data[0].Values);
        Assert.Equal([""], data[1].Values);
    }

    [Fact]
    public void A_trigger_pointing_at_no_subtype_and_no_data_is_read_as_such()
    {
        var trigger = Assert.Single(Walked(subtype: null, []).Value!);

        Assert.Null(trigger.Subtype);
        Assert.Empty(trigger.Data);
    }

    [Fact]
    public void A_subtype_pointing_outside_the_block_refuses_the_field()
    {
        var reading = Walked(Provider, [], (buffer, address) =>
            BitConverter.TryWriteBytes(buffer.AsSpan(HeaderLength + SubtypePointer), address - 4096));

        Refused(reading);
    }

    /// <summary>
    /// Eight bytes declared, two of them inside the block. Reading the six beyond would be a read past
    /// the end of a managed array on an elevated process.
    /// </summary>
    [Fact]
    public void A_data_item_running_past_the_end_of_the_block_refuses_the_field()
    {
        var reading = Walked(Provider, [(Binary, [1, 2, 3, 4, 5, 6, 7, 8])], (buffer, address) =>
            BitConverter.TryWriteBytes(buffer.AsSpan(ItemsAt + 8), address + buffer.Length - 2));

        Refused(reading);
    }

    [Fact]
    public void A_count_of_data_items_larger_than_the_block_refuses_the_field()
    {
        var reading = Walked(Provider, [(Level, [4])], (buffer, _) =>
            BitConverter.TryWriteBytes(buffer.AsSpan(HeaderLength + ItemCount), 1000u));

        Refused(reading);
    }

    /// <summary>
    /// A level is one byte, a keyword eight and a string whole characters - anything else is a block that
    /// does not describe itself, said as a refusal rather than read as far as it happens to go.
    /// </summary>
    [Theory]
    [InlineData(Level, 2)]
    [InlineData(KeywordAny, 4)]
    [InlineData(KeywordAll, 9)]
    [InlineData(Text, 3)]
    public void A_data_item_of_a_size_its_type_does_not_allow_refuses_the_field(uint type, int length)
    {
        Refused(Walked(Provider, [(type, new byte[length])]));
    }

    private static void Refused(Reading<IReadOnlyList<ServiceTrigger>> reading)
    {
        Assert.Equal(ReadOutcome.Denied, reading.Outcome);
        Assert.Equal(13, reading.ErrorCode);
    }

    /// <summary>Where the items array starts in a block <see cref="Walked"/> builds.</summary>
    private const int ItemsAt = HeaderLength + RecordLength + GuidLength;

    private static byte[] Little(ulong value)
    {
        var bytes = new byte[sizeof(ulong)];

        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);

        return bytes;
    }

    /// <summary>
    /// One custom start trigger laid out the way the manager lays it out - the header, the record, the
    /// subtype, the items and then their bytes, every pointer into the same pinned block - with a chance
    /// to spoil the block before it is read.
    /// </summary>
    private static Reading<IReadOnlyList<ServiceTrigger>> Walked(
        Guid? subtype, (uint Type, byte[] Bytes)[] items, Action<byte[], long>? spoil = null)
    {
        var dataAt = ItemsAt + items.Length * ItemLength;
        var buffer = new byte[dataAt + items.Sum(item => item.Bytes.Length)];
        var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);

        try
        {
            var address = pinned.AddrOfPinnedObject().ToInt64();

            BitConverter.TryWriteBytes(buffer.AsSpan(0), 1u);
            BitConverter.TryWriteBytes(buffer.AsSpan(8), address + HeaderLength);

            // A custom trigger that starts the entry: kind twenty, action one.
            BitConverter.TryWriteBytes(buffer.AsSpan(HeaderLength), 20u);
            BitConverter.TryWriteBytes(buffer.AsSpan(HeaderLength + 4), 1u);

            if (subtype is { } guid)
            {
                guid.TryWriteBytes(buffer.AsSpan(HeaderLength + RecordLength));
                BitConverter.TryWriteBytes(buffer.AsSpan(HeaderLength + SubtypePointer), address + HeaderLength + RecordLength);
            }

            BitConverter.TryWriteBytes(buffer.AsSpan(HeaderLength + ItemCount), (uint)items.Length);

            if (items.Length > 0)
            {
                BitConverter.TryWriteBytes(buffer.AsSpan(HeaderLength + ItemsPointer), address + ItemsAt);
            }

            for (var index = 0; index < items.Length; index++)
            {
                var at = ItemsAt + index * ItemLength;
                var (type, bytes) = items[index];

                BitConverter.TryWriteBytes(buffer.AsSpan(at), type);
                BitConverter.TryWriteBytes(buffer.AsSpan(at + 4), (uint)bytes.Length);

                if (bytes.Length > 0)
                {
                    BitConverter.TryWriteBytes(buffer.AsSpan(at + 8), address + dataAt);
                    bytes.CopyTo(buffer, dataAt);
                    dataAt += bytes.Length;
                }
            }

            spoil?.Invoke(buffer, address);

            return ManagerBlocks.ReadTriggerBuffer(buffer);
        }
        finally
        {
            pinned.Free();
        }
    }
}
