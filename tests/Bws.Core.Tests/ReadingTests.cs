namespace Bws.Core.Tests;

/// <summary>
/// The point of <see cref="Reading{T}"/> is that four situations stay four situations.
/// These tests exist to keep them from quietly collapsing into two, which is the failure
/// that would turn a partial audit into one that looks complete.
/// </summary>
public sealed class ReadingTests
{
    [Fact]
    public void Absent_and_denied_are_not_the_same_thing()
    {
        var absent = Reading<string>.Absent();
        var denied = Reading<string>.Denied(Entries.AccessDenied, "access denied");

        // Both have no value, and that is exactly why they are easy to confuse.
        Assert.False(absent.IsPresent);
        Assert.False(denied.IsPresent);

        // One is a fact about the service, the other a fact about our permissions.
        Assert.NotEqual(absent.Outcome, denied.Outcome);
        Assert.Null(absent.Reason);
        Assert.Equal("access denied", denied.Reason);
    }

    [Fact]
    public void A_refusal_carries_a_number_as_well_as_a_sentence()
    {
        // The sentence comes from Windows and comes in the language of the machine, so it
        // is the half that cannot be relied on anywhere else. Measured on 2026-08-01: the
        // same refusal reads in English on one install and in Polish on another. Anything
        // deciding on a refusal has to have something stable to decide on.
        var denied = Reading<string>.Denied(Entries.AccessDenied, "whatever Windows said");

        Assert.Equal(Entries.AccessDenied, denied.ErrorCode);

        // And a reading that did not fail carries no number to be mistaken for one.
        Assert.Equal(0, Reading<string>.Absent().ErrorCode);
        Assert.Equal(0, Reading<string>.Present("x").ErrorCode);
    }

    [Fact]
    public void Not_read_yet_is_not_the_same_as_nothing_there()
    {
        Assert.NotEqual(Reading<int>.NotRead().Outcome, Reading<int>.Absent().Outcome);
    }

    [Fact]
    public void One_refusal_carries_one_number_whichever_way_it_arrived()
    {
        // The field is a frozen contract and its own documentation calls it "the system's own
        // number", singular. Two numberings reached it: most refusals come from
        // GetLastWin32Error and are Win32 codes, and some come from the HResult of a managed
        // exception, because by the time one has been built the thread's last error has usually
        // been overwritten.
        //
        // So access denied was 5 out of the listing and -2147024891 out of a signature or a
        // security descriptor - the same refusal, two numbers, and nothing anywhere said so. A
        // script keying on 5 worked for one and silently missed the other. Owner's decision,
        // 2026-08-03: one numbering.
        Assert.Equal(
            Reading<string>.Denied(Entries.AccessDenied, "from the manager").ErrorCode,
            Reading<string>.Denied(unchecked((int)0x80070005), "from an exception").ErrorCode);
    }

    [Fact]
    public void A_number_that_is_not_a_wrapped_win32_code_is_left_alone()
    {
        // The other side of the line. 0x800B0100 is "this file carries no signature" and has no
        // Win32 equivalent at all, so unwrapping anything that merely looks like an HResult
        // would invent a number - which is worse than carrying two numbering schemes knowingly.
        const int noSignature = unchecked((int)0x800B0100);

        Assert.Equal(noSignature, Reading<string>.Denied(noSignature, "no signature").ErrorCode);

        // And an ordinary Win32 code passes through untouched, which is the case that must not
        // regress: it is what almost every refusal in this product is.
        Assert.Equal(1060, Reading<string>.Denied(1060, "the service does not exist").ErrorCode);
    }

    [Fact]
    public void A_present_reading_carries_its_value()
    {
        var reading = Reading<int>.Present(1234);

        Assert.True(reading.IsPresent);
        Assert.Equal(1234, reading.Value);
        Assert.Equal(1234, reading.ValueOr(-1));
    }

    [Theory]
    [InlineData(ReadOutcome.NotRead)]
    [InlineData(ReadOutcome.Absent)]
    [InlineData(ReadOutcome.Denied)]
    public void Everything_that_is_not_present_falls_back(ReadOutcome outcome)
    {
        var reading = outcome switch
        {
            ReadOutcome.NotRead => Reading<int>.NotRead(),
            ReadOutcome.Absent => Reading<int>.Absent(),
            _ => Reading<int>.Denied(Entries.AccessDenied, "no")
        };

        Assert.Equal(-1, reading.ValueOr(-1));
    }
}
