using System.Xml;
using Bws.Core.History;
using CsCheck;

namespace Bws.Core.Tests;

/// <summary>
/// One event from the System log, read into a change - step 3b of the fuzzing project, point C4
/// (docs/PROJEKT-FUZZING-20261016.md, outside the repository).
///
/// <b>THE ONE WAY OUT IS XmlException, and that is a decision rather than a habit.</b>
/// WindowsChangeHistoryReader catches nothing, so an event that will not parse ends bws history with
/// code 1 through the net in Program.cs - an event this history refuses rather than guesses at. Any
/// other exception from the three readings is a fault: a parse that was meant to say "not XML" and
/// said something else, or a value nobody planned for tripping over code after the parse.
///
/// <b>And what parses gives back what Windows wrote.</b> The values in these events are written by
/// whoever installed a service - an ampersand, an angle bracket, a blank display name, a letter beyond
/// the first plane - so the second property writes events from any of them and asks for each back:
/// an empty one as null, the first of a repeated name, the time in UTC to the tick.
/// </summary>
public sealed class ChangeEventPropertyTests
{
    [Fact]
    [Trait("search", "weekly")]
    public void A_damaged_event_is_read_or_refused_as_xml_and_never_otherwise()
    {
        RenderedEvents.Any
            .SelectMany(written => Damaged.In(RenderedEvents.Rendered(written)).Select(damage => (written, damage)))
            .Searched(
                drawn => Survives(drawn.damage.To(RenderedEvents.Rendered(drawn.written), '<')),
                new(10_000) { Print = drawn => $"{drawn.damage} of {drawn.written}" });
    }

    [Fact]
    [Trait("search", "weekly")]
    public void An_event_written_from_any_values_gives_those_values_back()
    {
        RenderedEvents.Any.Searched(
            written => Wrong(written) is { } wrong ? throw new InvalidOperationException(wrong) : true,
            new(5_000));
    }

    /// <summary>Whether each of the three readings of this text answers or refuses it as XML.</summary>
    private static bool Survives(string xml) =>
        Answers(() => ChangeEvents.Change(xml))
        & Answers(() => ChangeEvents.ClearingOf(xml, "System"))
        & Answers(() => ChangeEvents.TimeOf(xml));

    private static bool Answers<T>(Func<T> read)
    {
        try
        {
            _ = read();
        }
        catch (XmlException)
        {
            // Refused as XML - the one way out the history plans for, see the class note. Anything
            // else is not caught, and CsCheck reports it with the smallest damage that still throws.
        }

        return true;
    }

    /// <summary>What came back differently from what was written, or nothing.</summary>
    private static string? Wrong(WrittenEvent written)
    {
        var xml = RenderedEvents.Rendered(written);
        var at = new DateTimeOffset(written.At);
        var time = ChangeEvents.TimeOf(xml);

        // Equality of two instants ignores their offsets, and the promise is also that the time stays
        // in UTC - so the offset is asked for on its own.
        if (time != at || time?.Offset != TimeSpan.Zero)
        {
            return $"The time came back as {time:o}, written as {at:o}.";
        }

        var change = ChangeEvents.Change(xml);
        var cleared = ChangeEvents.ClearingOf(xml, "System");
        var wanted = Expected(written, at);
        var wantedClearing = Cleared(written, at);

        return change == wanted && cleared == wantedClearing
            ? null
            : $"Came back as {change?.ToString() ?? "no change"} and {cleared?.ToString() ?? "no clearing"}, " +
              $"written as {wanted?.ToString() ?? "no change"} and {wantedClearing?.ToString() ?? "no clearing"}.";
    }

    /// <summary>The change the event records, field by field from the manifest, or none for a clearing.</summary>
    private static ServiceChange? Expected(WrittenEvent written, DateTimeOffset at) => written.Id switch
    {
        7040 => new ServiceChange
        {
            At = at,
            Kind = ChangeKind.StartTypeChanged,
            ServiceName = written.First("param4"),
            DisplayName = written.First("param1"),
            By = By(written),
            Words = new WindowsWords(written.First("param2"), written.First("param3"), null, null)
        },
        7045 => new ServiceChange
        {
            At = at,
            Kind = ChangeKind.Installed,
            ServiceName = null,
            DisplayName = written.First("ServiceName"),
            By = By(written),
            BinaryPath = written.First("ImagePath"),
            Account = written.First("AccountName"),
            Words = new WindowsWords(null, null, written.First("ServiceType"), written.First("StartType"))
        },
        _ => null
    };

    /// <summary>A clearing only of the log asked about, whatever the case it was written in.</summary>
    private static Clearing? Cleared(WrittenEvent written, DateTimeOffset at) =>
        written.Id == 104 && string.Equals(written.Channel, "System", StringComparison.OrdinalIgnoreCase)
            ? new Clearing(at, By(written))
            : null;

    private static Actor? By(WrittenEvent written) => written.Sid.Length > 0 ? new Actor(written.Sid, null) : null;
}
