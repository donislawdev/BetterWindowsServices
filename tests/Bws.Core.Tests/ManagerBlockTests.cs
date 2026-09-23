using System.Runtime.InteropServices;

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

    /// <summary>
    /// ENUM_SERVICE_STATUSW on a 64 bit machine: two pointers and a SERVICE_STATUS of seven words,
    /// which is 44 rounded up to 48. Written out here rather than asked of the type, which is
    /// interop metadata this assembly cannot see - and the tests below assert the record COUNT so
    /// that a wrong number here fails loudly instead of quietly clamping to nothing.
    /// </summary>
    private const int DependentRecordLength = 48;

    /// <summary>
    /// <b>Backlog 297.</b> Until 2026-09-02 this walk called <c>PWSTR.ToString()</c>, which reads
    /// from an address until it meets a null wherever that happens to be - past the block, past the
    /// array, past anything. Every other reading in that file is bounded by the block, and these
    /// two record walks were the ones nobody had come back to.
    ///
    /// The name comes back empty rather than as whatever was at that address. That is a compromise
    /// and it is written down as one where the method lives - what matters here is that the read
    /// stops, on a process that runs elevated on other people's servers.
    /// </summary>
    [Fact]
    public void A_dependent_name_pointing_outside_the_block_is_not_followed()
    {
        var buffer = new byte[DependentRecordLength];
        var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);

        try
        {
            // A page below the block, which is inside this process and not inside this array -
            // the shape that used to be read from and would have handed back whatever was there.
            var address = pinned.AddrOfPinnedObject().ToInt64();

            BitConverter.TryWriteBytes(buffer.AsSpan(0), address - 4096);

            var names = ManagerBlocks.ReadDependentNames(buffer, 1);

            Assert.Single(names);
            Assert.Equal(string.Empty, names[0]);
        }
        finally
        {
            pinned.Free();
        }
    }

    /// <summary>
    /// A record whose name pointer is zero. The manager does not write one, and a walk that
    /// dereferences it anyway ends the process rather than reporting anything.
    /// </summary>
    [Fact]
    public void A_dependent_name_that_is_null_is_not_followed()
    {
        var names = ManagerBlocks.ReadDependentNames(new byte[DependentRecordLength], 1);

        Assert.Single(names);
        Assert.Equal(string.Empty, names[0]);
    }

    /// <summary>
    /// A name that ends where the block ends, with no terminator after it. The manager terminates
    /// what it writes - this asks what happens when it did not, which is the case the bound is for.
    /// </summary>
    [Fact]
    public void A_dependent_name_running_to_the_end_of_the_block_stops_there()
    {
        var buffer = new byte[DependentRecordLength];
        var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);

        try
        {
            var address = pinned.AddrOfPinnedObject().ToInt64();

            // The name starts two characters before the end of the block and neither of them is a
            // null, so nothing inside the block closes it.
            BitConverter.TryWriteBytes(buffer.AsSpan(0), address + DependentRecordLength - 4);
            BitConverter.TryWriteBytes(buffer.AsSpan(DependentRecordLength - 4), (char)'h');
            BitConverter.TryWriteBytes(buffer.AsSpan(DependentRecordLength - 2), (char)'i');

            var names = ManagerBlocks.ReadDependentNames(buffer, 1);

            Assert.Single(names);
            Assert.Equal("hi", names[0]);
        }
        finally
        {
            pinned.Free();
        }
    }

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
