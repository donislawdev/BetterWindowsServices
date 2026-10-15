using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Bws.Core.History;

/// <summary>
/// One event from the System log, as the XML Windows renders it, turned into what the change
/// history needs - with no call to Windows anywhere in here, so every shape can be tested from text.
///
/// <b>THE SHAPES ARE MEASURED, NOT ASSUMED</b> (docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md section 9,
/// 2026-10-15). The event templates come from the provider's manifest
/// (<c>Get-WinEvent -ListProvider 'Service Control Manager'</c>): 7040 has param1 to param4 and 7045
/// has ServiceName, ImagePath, ServiceType, StartType and AccountName, every one a plain string. Read
/// on a Polish machine and an English one, a change made by a person and one made by SYSTEM:
///
/// <list type="bullet">
///   <item>7040: param1 is the display name, param2 and param3 the start type before and after - in
///   words - and param4 the internal name. The account is the SID in System/Security/@UserID, and it
///   is the person's own SID when a person made the change.</item>
///   <item>7045: ServiceName is the DISPLAY name. There is no internal name in the event at all.</item>
///   <item>104 from Microsoft-Windows-Eventlog: written for clearing ANY channel, with the channel's
///   name in UserData/LogFileCleared/Channel - so only one naming the log being read says that log
///   was cleared.</item>
/// </list>
///
/// <b>Read with a DTD refused and nothing resolved.</b> Windows renders these itself and would never
/// put one in, but the values inside are written by whoever installed a service, and the parser is
/// the one place that should not have to trust that.
/// </summary>
public static class ChangeEvents
{
    private const string Manager = "Service Control Manager";
    private const string EventLog = "Microsoft-Windows-Eventlog";
    private const int StartTypeChanged = 7040;
    private const int Installed = 7045;
    private const int LogCleared = 104;

    /// <summary>
    /// The most one event may take once rendered as XML, in bytes of UTF-16 - and so half that in
    /// characters for the parser.
    ///
    /// <b>A bound with a reason, not a guess.</b> An event record lives inside one 64 KB chunk of the
    /// log file (the evtx format, written down by people who read it rather than by Microsoft - the
    /// same outside source as <see cref="LogFacts.Chunk"/>), so its XML, with the template's markup
    /// and every value doubled into UTF-16, stays far under a megabyte. Anything bigger is not an
    /// event this history should read, and it is refused rather than allocated. Added after the review
    /// of #70.
    /// </summary>
    internal const int LargestRendering = 1024 * 1024;

    /// <summary>
    /// The change one event records, or null when the event is not a change this history reads.
    /// </summary>
    public static ServiceChange? Change(string xml)
    {
        var rendered = Rendered.Of(xml);

        if (!string.Equals(rendered.Provider, Manager, StringComparison.Ordinal))
        {
            return null;
        }

        return rendered.Id switch
        {
            StartTypeChanged => new ServiceChange
            {
                At = rendered.At,
                Kind = ChangeKind.StartTypeChanged,
                ServiceName = rendered.Data("param4"),
                DisplayName = rendered.Data("param1"),
                By = rendered.By,
                Words = new WindowsWords(rendered.Data("param2"), rendered.Data("param3"), null, null)
            },

            Installed => new ServiceChange
            {
                At = rendered.At,
                Kind = ChangeKind.Installed,

                // Null on purpose rather than filled from the field that sounds like it. The field
                // called ServiceName holds the display name - see the type above.
                ServiceName = null,
                DisplayName = rendered.Data("ServiceName"),
                By = rendered.By,
                BinaryPath = rendered.Data("ImagePath"),
                Account = rendered.Data("AccountName"),
                Words = new WindowsWords(null, null, rendered.Data("ServiceType"), rendered.Data("StartType"))
            },

            _ => null
        };
    }

    /// <summary>
    /// Who cleared the log called <paramref name="channel"/>, when this event says so, and null for
    /// any other event - including the clearing of a different log.
    /// </summary>
    public static Clearing? ClearingOf(string xml, string channel)
    {
        var rendered = Rendered.Of(xml);

        return string.Equals(rendered.Provider, EventLog, StringComparison.Ordinal)
               && rendered.Id == LogCleared
               && string.Equals(rendered.ClearedChannel, channel, StringComparison.OrdinalIgnoreCase)
            ? new Clearing(rendered.At, rendered.By)
            : null;
    }

    /// <summary>When any event was written, for how far back a log reaches.</summary>
    public static DateTimeOffset? TimeOf(string xml) => Rendered.Of(xml).At;

    /// <summary>The parts of one rendered event this history reads, and nothing else.</summary>
    private sealed record Rendered(
        string? Provider,
        int? Id,
        DateTimeOffset? At,
        Actor? By,
        IReadOnlyDictionary<string, string> Values,
        string? ClearedChannel)
    {
        internal static Rendered Of(string xml)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = LargestRendering / sizeof(char)
            };

            using var text = new StringReader(xml);
            using var reader = XmlReader.Create(text, settings);

            var root = XDocument.Load(reader).Root
                       ?? throw new XmlException("A rendered event has no root element.");

            var space = root.Name.Namespace;
            var system = root.Element(space + "System");

            return new Rendered(
                system?.Element(space + "Provider")?.Attribute("Name")?.Value,
                Number(system?.Element(space + "EventID")?.Value),
                Time(system?.Element(space + "TimeCreated")?.Attribute("SystemTime")?.Value),
                Who(system?.Element(space + "Security")?.Attribute("UserID")?.Value),
                Fields(root.Element(space + "EventData")?.Elements(space + "Data")),

                // By local name, because the element under UserData carries a namespace of its own
                // (the eventlog manifest's), and the channel is the one child named Channel.
                root.Element(space + "UserData")?.Elements().FirstOrDefault()?.Elements()
                    .FirstOrDefault(element => element.Name.LocalName == "Channel")?.Value);
        }

        /// <summary>
        /// A field the event names, or null when it is missing OR EMPTY. Windows writes an empty
        /// AccountName for a driver, and an empty string beside a null would be two spellings of
        /// "no account" for whoever reads the document.
        /// </summary>
        internal string? Data(string name) =>
            Values.TryGetValue(name, out var value) && value.Length > 0 ? value : null;

        private static Dictionary<string, string> Fields(IEnumerable<XElement>? data)
        {
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var field in data ?? [])
            {
                if (field.Attribute("Name")?.Value is { } name)
                {
                    // The first one wins. A template never names a field twice, and taking the last
                    // would let a repeated name decide what an earlier one meant.
                    fields.TryAdd(name, field.Value);
                }
            }

            return fields;
        }

        private static int? Number(string? text) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;

        /// <summary>
        /// The time Windows wrote, which is in UTC and says so with a trailing Z - kept to the full
        /// precision it carries, because two changes a few milliseconds apart are two changes, and
        /// rounding them into one second would put them in no particular order.
        ///
        /// <b>And kept in UTC, so the document says the same whatever time zone reads the log.</b> The
        /// trailing Z is what does it: measured 2026-10-15, this parse gives an offset of zero with or
        /// without AdjustToUniversal. A comment here claimed for an hour that it did not - from a JSON
        /// document shown through PowerShell 7's ConvertFrom-Json, which turns dates into local time
        /// by itself. The mutation registry found that, by failing to make the claim matter.
        /// </summary>
        private static DateTimeOffset? Time(string? text) =>
            DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
                ? at
                : null;

        private static Actor? Who(string? sid) => string.IsNullOrEmpty(sid) ? null : new Actor(sid, null);
    }
}
