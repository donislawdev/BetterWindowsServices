using System.Globalization;
using System.Xml.Linq;
using CsCheck;

namespace Bws.Core.Tests;

/// <summary>
/// Events from the System log as Windows renders them, written from any values a person installing a
/// service can put in them - step 3b of the fuzzing project, point C4.
///
/// <b>Built with XElement, never by gluing strings.</b> ChangeEventsTests writes its examples as
/// text without escaping, which is right for examples somebody reads and wrong here: a value holding
/// an ampersand or an angle bracket would make the example malformed rather than unusual. XElement
/// escapes the way any writer of XML does, so what comes back has to be the value that went in.
///
/// <b>Characters XML 1.0 allows, and no carriage return.</b> The others cannot be written into a
/// document at all (XElement refuses them), and a carriage return would come back as a line feed -
/// the end-of-line rule of every XML parser, applied to OUR writing, not something Windows does to the
/// value. How EvtRender writes a control character it was handed is a separate question, NOT
/// MEASURED: it needs a service installed with one, which is a job for the throwaway machine.
///
/// <b>The shapes come from the provider's manifest</b>, as written at the top of ChangeEvents: 7040
/// has param1 to param4, 7045 five named fields, 104 the channel under UserData.
/// </summary>
internal static class RenderedEvents
{
    internal const string Space = "http://schemas.microsoft.com/win/2004/08/events/event";
    private const string EventLogSpace = "http://manifests.microsoft.com/win/2004/08/windows/eventlog";

    internal static readonly string[] StartTypeFields = ["param1", "param2", "param3", "param4"];
    internal static readonly string[] InstallationFields = ["ServiceName", "ImagePath", "ServiceType", "StartType", "AccountName"];

    /// <summary>One character, or a pair standing for one beyond the first plane, that XML 1.0 allows - never a carriage return.</summary>
    private static readonly Gen<string> Piece = Gen.Frequency(
        (6, Gen.Char[' ', '~'].Select(character => character.ToString())),
        (2, Gen.OneOfConst("<", ">", "&", "\"", "'", "]]>", "\t", "\n", " ", ((char)0x00A0).ToString())),
        (2, Gen.Char[(char)0x00A0, (char)0xD7FF].Select(character => character.ToString())),
        (1, Gen.Char[(char)0xE000, (char)0xFFFD].Select(character => character.ToString())),
        (1, Gen.Int[0x10000, 0x10FFFF].Select(char.ConvertFromUtf32)));

    /// <summary>A value: often empty or blank, sometimes long.</summary>
    internal static readonly Gen<string> Value = Gen.Frequency(
        (1, Gen.OneOfConst(string.Empty, " ", "   ", "\n", "\t")),
        (8, Piece.Array[1, 24].Select(string.Concat)));

    private static readonly Gen<string> Sid = Gen.Frequency(
        (3, Gen.OneOfConst("S-1-5-18", "S-1-5-19", "S-1-5-20")),
        (6, Gen.Select(Gen.UInt, Gen.UInt, Gen.UInt, Gen.Int[500, 70_000], (a, b, c, rid) =>
            string.Create(CultureInfo.InvariantCulture, $"S-1-5-21-{a}-{b}-{c}-{rid}"))),
        (1, Gen.Const(string.Empty)));

    private static readonly Gen<string> Channel = Gen.OneOfConst(
        "System", "system", "SYSTEM", "Application", "Security", " System", "System ", "Microsoft-Windows-Foo/Operational", string.Empty);

    /// <summary>Any instant a DateTime can hold, written the way Windows writes SystemTime - UTC, seven decimals, a trailing Z.</summary>
    private static readonly Gen<DateTime> Instant =
        Gen.Long[DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks].Select(ticks => new DateTime(ticks, DateTimeKind.Utc));

    /// <summary>A start type change, an installation or a log being cleared, each with its fields.</summary>
    internal static readonly Gen<WrittenEvent> Any = Gen.Frequency(
        (4, Gen.Select(Sid, Instant, FieldsOf(StartTypeFields), (sid, at, fields) => new WrittenEvent(7040, sid, at, fields, null))),
        (4, Gen.Select(Sid, Instant, FieldsOf(InstallationFields), (sid, at, fields) => new WrittenEvent(7045, sid, at, fields, null))),
        (2, Gen.Select(Sid, Instant, Channel, (sid, at, channel) => new WrittenEvent(104, sid, at, [], channel))));

    /// <summary>
    /// Every field of the template, each one missing a tenth of the time, and a fifth of the time
    /// one of them named again further down with another value - which the reader answers with the
    /// first, because a template never names a field twice.
    /// </summary>
    private static Gen<(string Name, string Value)[]> FieldsOf(string[] names) =>
        Gen.Select(
            Gen.Frequency((9, Value.Select(value => (string?)value)), (1, Gen.Const((string?)null))).Array[names.Length],
            Gen.Int[0, names.Length - 1],
            Value,
            Gen.Int[0, 4],
            (values, again, other, chance) =>
            {
                var fields = names.Zip(values)
                    .Where(field => field.Second is not null)
                    .Select(field => (field.First, field.Second!))
                    .ToList();

                if (chance == 0)
                {
                    fields.Add((names[again], other));
                }

                return fields.ToArray();
            });

    /// <summary>One event, rendered the way Windows lays it out - no whitespace between elements.</summary>
    internal static string Rendered(WrittenEvent written)
    {
        XNamespace space = Space;
        XNamespace eventLog = EventLogSpace;

        var provider = written.Id == 104 ? "Microsoft-Windows-Eventlog" : "Service Control Manager";

        object payload = written.Id == 104
            ? new XElement(
                space + "UserData",
                new XElement(eventLog + "LogFileCleared", new XElement(eventLog + "SubjectUserName", "someone"), new XElement(eventLog + "Channel", written.Channel)))
            : new XElement(
                space + "EventData",
                written.Fields.Select(field => new XElement(space + "Data", new XAttribute("Name", field.Name), field.Value)));

        var root = new XElement(
            space + "Event",
            new XElement(
                space + "System",
                new XElement(space + "Provider", new XAttribute("Name", provider)),
                new XElement(space + "EventID", new XAttribute("Qualifiers", "16384"), written.Id),
                new XElement(space + "TimeCreated", new XAttribute("SystemTime", written.At.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture))),
                new XElement(space + "EventRecordID", "531"),
                new XElement(space + "Channel", "System"),
                new XElement(space + "Computer", "HOST"),
                new XElement(space + "Security", new XAttribute("UserID", written.Sid))),
            payload);

        return root.ToString(SaveOptions.DisableFormatting);
    }
}

/// <summary>What an event was written from - the values a property expects back.</summary>
internal sealed record WrittenEvent(int Id, string Sid, DateTime At, (string Name, string Value)[] Fields, string? Channel)
{
    /// <summary>The first value written under this name, or null when there is none or it is empty - the reader's own rule.</summary>
    internal string? First(string name) =>
        Fields.Where(field => field.Name == name).Select(field => field.Value).FirstOrDefault() is { Length: > 0 } value ? value : null;

    /// <summary>What a failure prints: the rendered event itself.</summary>
    public override string ToString() => RenderedEvents.Rendered(this);
}
