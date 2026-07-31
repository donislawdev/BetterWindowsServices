using Bws.Core;

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
        var denied = Reading<string>.Denied("access denied");

        // Both have no value, and that is exactly why they are easy to confuse.
        Assert.False(absent.IsPresent);
        Assert.False(denied.IsPresent);

        // One is a fact about the service, the other a fact about our permissions.
        Assert.NotEqual(absent.Outcome, denied.Outcome);
        Assert.Null(absent.Reason);
        Assert.Equal("access denied", denied.Reason);
    }

    [Fact]
    public void Not_read_yet_is_not_the_same_as_nothing_there()
    {
        Assert.NotEqual(Reading<int>.NotRead().Outcome, Reading<int>.Absent().Outcome);
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
            _ => Reading<int>.Denied("no")
        };

        Assert.Equal(-1, reading.ValueOr(-1));
    }
}
