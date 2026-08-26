using System.Runtime.InteropServices;
using Bws.Core;

namespace Bws.Core.Tests;

/// <summary>
/// Blocks the manager could hand back, handed over by a test instead.
///
/// <b>This is the whole reason those walks stopped taking a service handle.</b> A machine will not
/// produce a malformed answer on request - the bytes that matter here are the ones that arrive when
/// something has already gone wrong, on somebody's server, once. A method that takes a block can be
/// given them.
///
/// <b>What each of these is worth is worth saying, because it is not "the manager does this".</b>
/// Nothing below claims Windows ever writes a count of five into a block with room for one. The
/// claim is narrower and it is the one that matters at three in the morning: if it did, this reads
/// one record and stops. Before 2026-08-26 it read five, out of a fifty six byte array, which on an
/// elevated process is a memory fault that ends the program with no report at all.
/// </summary>
public sealed class ManagerBlockTests
{
    /// <summary>Where each field of SERVICE_TRIGGER_INFO sits on a 64 bit machine.</summary>
    private const int TriggerCount = 0;
    private const int TriggerPointer = 8;
    private const int HeaderLength = 24;

    /// <summary>SERVICE_TRIGGER: two words, a pointer, a count, a pointer.</summary>
    private const int RecordLength = 32;

    [Fact]
    public void A_block_too_short_to_hold_its_own_header_is_nothing_read()
    {
        Assert.False(ManagerBlocks.ReadTriggerBuffer(new byte[4]).IsPresent);
    }

    [Fact]
    public void A_block_declaring_no_triggers_is_a_fact_about_the_service()
    {
        // Absent rather than an empty list, and this is the ordinary case: most entries on a real
        // machine declare none.
        var reading = ManagerBlocks.ReadTriggerBuffer(new byte[HeaderLength]);

        Assert.False(reading.IsPresent);
        Assert.Equal(ReadOutcome.Absent, reading.Outcome);
    }

    [Fact]
    public void A_count_with_a_pointer_to_nowhere_is_refused_rather_than_followed()
    {
        // The block says there are a thousand records and does not say where. Following that
        // dereferences nothing at address zero, which is where a program ends.
        var buffer = new byte[HeaderLength];

        BitConverter.TryWriteBytes(buffer.AsSpan(TriggerCount), 1000u);

        Assert.False(ManagerBlocks.ReadTriggerBuffer(buffer).IsPresent);
    }

    [Fact]
    public void A_count_larger_than_the_block_is_held_down_to_what_the_block_holds()
    {
        // Room for one record and a header that claims five. The one that fits is read and the
        // four that do not are not gone looking for.
        var buffer = new byte[HeaderLength + RecordLength];
        var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);

        try
        {
            // Pinned for the whole call, so the pointer written into the block stays true. This is
            // what the manager does: it hands back a pointer into the very block it filled.
            var address = pinned.AddrOfPinnedObject().ToInt64();

            BitConverter.TryWriteBytes(buffer.AsSpan(TriggerCount), 5u);
            BitConverter.TryWriteBytes(buffer.AsSpan(TriggerPointer), address + HeaderLength);

            // Device arrival, and the action is a start. The two words a record begins with.
            BitConverter.TryWriteBytes(buffer.AsSpan(HeaderLength), 1u);
            BitConverter.TryWriteBytes(buffer.AsSpan(HeaderLength + 4), 1u);

            var reading = ManagerBlocks.ReadTriggerBuffer(buffer);

            Assert.True(reading.IsPresent);
            Assert.Single(reading.Value!);
            Assert.Equal(TriggerKind.DeviceArrival, reading.Value![0].Kind);
            Assert.Equal(TriggerAction.Start, reading.Value![0].Action);
        }
        finally
        {
            pinned.Free();
        }
    }
}
