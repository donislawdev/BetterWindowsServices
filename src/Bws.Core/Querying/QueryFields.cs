namespace Bws.Core.Querying;

/// <summary>
/// Every field the language knows, and every spelling it accepts.
///
/// This file is a public contract twice over. The field names and the value spellings go
/// into saved queries that have to keep working years later, so the rule is that this
/// list may grow and may not change meaning. Value spellings deliberately match what the
/// JSON output prints, so anything a person sees in a listing can be pasted into a query.
///
/// Spelling is normalised before lookup, which is why <c>startPending</c>, <c>start-pending</c>
/// and <c>start_pending</c> all arrive at the same place without three entries below.
/// </summary>
public static class QueryFields
{
    /// <summary>
    /// Asks whether the value is genuinely absent. Reserved in every field, so it is not a
    /// value any single enumeration can claim.
    /// </summary>
    public const string None = "none";

    /// <summary>Asks whether there is any value at all.</summary>
    public const string Any = "any";

    /// <summary>
    /// Asks for what could not be read. Odd-looking, and necessary: in an audit tool "show
    /// me what you failed to read" is a question in its own right, and without it a person
    /// can see that a result is partial with no way to ask what is missing.
    ///
    /// Only the bare, unquoted form means this. <c>name:"?"</c> is a literal question mark
    /// and <c>name:ab?</c> is a wildcard.
    /// </summary>
    public const string Unreadable = "?";

    // Declaration order matters here and is not cosmetic: static initialisers run top to
    // bottom, and the two below are built from All.

    /// <summary>Every field, in the order they are offered when a field name is wrong.</summary>
    internal static IReadOnlyList<QueryField> All { get; } = BuildAll();

    /// <summary>
    /// The fields a bare word searches. Grows as the tool starts reading more, which is why
    /// a saved query records the syntax version it was written against: the same text has to
    /// keep meaning what it meant, and silently widening the search would change audit
    /// results without changing the query.
    /// </summary>
    internal static IReadOnlyList<QueryField> FreeSearch { get; } = BuildFreeSearch();

    /// <summary>Every field name, for anything that offers them to a person.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. All.Select(field => field.Name)];

    private static readonly Dictionary<string, QueryField> ByName = BuildIndex();

    /// <summary>The field for a spelling, or null when nobody knows it.</summary>
    internal static QueryField? Find(string name) =>
        ByName.GetValueOrDefault(Normalise(name));

    /// <summary>
    /// One spelling, one form. Case is folded because Windows treats service names that way,
    /// and the separators go because a two-word value has three plausible spellings and
    /// making a person guess which one we chose is a poor use of their time.
    /// </summary>
    internal static string Normalise(string text)
    {
        Span<char> folded = text.Length <= 64 ? stackalloc char[text.Length] : new char[text.Length];
        var length = 0;

        foreach (var character in text)
        {
            if (character is '-' or '_')
            {
                continue;
            }

            folded[length++] = char.ToLowerInvariant(character);
        }

        return new string(folded[..length]);
    }

    private static QueryField[] BuildAll() =>
    [
        new QueryField
        {
            Name = "name",
            Kind = QueryFieldKind.Text,
            OutcomeOf = _ => ReadOutcome.Present,
            TextOf = entry => entry.ServiceName
        },

        new QueryField
        {
            Name = "display",
            Kind = QueryFieldKind.Text,
            OutcomeOf = _ => ReadOutcome.Present,
            TextOf = entry => entry.DisplayName
        },

        new QueryField
        {
            Name = "type",
            Kind = QueryFieldKind.Enumeration,
            OutcomeOf = _ => ReadOutcome.Present,
            SymbolsOf = entry => FieldSymbols.Of(Normalise(entry.EntryType.ToString())),
            Values =
            [
                // "driver" covers both driver kinds. Windows keeps drivers and services in
                // one place, so telling them apart is the first thing anyone asks for, and
                // spelling out two values for it would be a poor answer to a common question.
                new QueryValueName("driver", "kerneldriver", "filesystemdriver"),
                new QueryValueName("kernelDriver", "kerneldriver"),
                new QueryValueName("fileSystemDriver", "filesystemdriver"),
                new QueryValueName("ownProcess", "ownprocess"),
                new QueryValueName("sharedProcess", "sharedprocess"),
                new QueryValueName("unknown", "unknown")
            ]
        },

        new QueryField
        {
            Name = "status",
            Kind = QueryFieldKind.Enumeration,
            OutcomeOf = _ => ReadOutcome.Present,
            SymbolsOf = entry => FieldSymbols.Of(Normalise(entry.Status.ToString())),
            Values =
            [
                new QueryValueName("running", "running"),
                new QueryValueName("stopped", "stopped"),
                new QueryValueName("paused", "paused"),
                new QueryValueName("startPending", "startpending"),
                new QueryValueName("stopPending", "stoppending"),
                new QueryValueName("pausePending", "pausepending"),
                new QueryValueName("continuePending", "continuepending"),
                new QueryValueName("unknown", "unknown")
            ]
        },

        new QueryField
        {
            Name = "start",
            Kind = QueryFieldKind.Enumeration,
            OutcomeOf = entry => entry.StartType.Outcome,
            SymbolsOf = StartSymbols,
            Values =
            [
                new QueryValueName("automatic", "automatic"),

                // An alias means the same thing as the word it stands for. "auto" therefore
                // covers delayed entries too, because "automatic" does. Somebody who wants
                // the distinction asks for "delayed", and somebody who wants automatic
                // without delayed writes: start:auto !start:delayed
                new QueryValueName("auto", "automatic"),

                // Not a start type Windows reports. The manager returns the same number, 2,
                // for both, and the delay is a separate piece of configuration. It is a
                // value here because that is how a person thinks about it and how
                // services.msc shows it.
                new QueryValueName("delayed", "delayed"),

                new QueryValueName("manual", "manual"),
                new QueryValueName("disabled", "disabled"),
                new QueryValueName("boot", "boot"),
                new QueryValueName("system", "system"),
                new QueryValueName("unknown", "unknown")
            ]
        },

        new QueryField
        {
            Name = "account",
            Kind = QueryFieldKind.Text,
            OutcomeOf = entry => entry.Account.Outcome,
            TextOf = entry => entry.Account.ValueOr(null)
        },

        new QueryField
        {
            Name = "pid",
            Kind = QueryFieldKind.Number,
            OutcomeOf = entry => entry.ProcessId.Outcome,
            NumberOf = entry => entry.ProcessId.IsPresent ? entry.ProcessId.Value : null
        },

        new QueryField
        {
            // Reserved in the query language document from the start, for the moment the
            // data existed. It does now.
            //
            // The question worth asking most often is not which kind but whether there is
            // one at all, and that is already covered by the words every field has:
            // trigger:any and trigger:none. Measured on a real machine on 2026-08-01, that
            // is the difference between a stopped service that is broken and one that is
            // waiting to be asked for.
            Name = "trigger",
            Kind = QueryFieldKind.Enumeration,
            OutcomeOf = entry => entry.Triggers.Outcome,
            SymbolsOf = TriggerSymbols,
            Values =
            [
                new QueryValueName("device", "devicearrival"),
                new QueryValueName("ip", "ipaddress"),
                new QueryValueName("domain", "domainjoin"),
                new QueryValueName("firewall", "firewallport"),
                new QueryValueName("policy", "grouppolicy"),
                new QueryValueName("network", "networkendpoint"),
                new QueryValueName("custom", "custom"),
                new QueryValueName("state", "customsystemstatechange"),
                new QueryValueName("unknown", "unknown"),

                // Not a kind but an action, and worth asking about on its own: a trigger
                // that stops a service is a very different fact from one that starts it.
                new QueryValueName("start", "start"),
                new QueryValueName("stop", "stop")
            ]
        },

        new QueryField
        {
            // The launch command whole, arguments and all, because that is what a person
            // sees in the listing and what they will paste a fragment of. Matching the
            // resolved file instead would answer "where did this come from" with a path
            // nobody showed them.
            Name = "path",
            Kind = QueryFieldKind.Text,
            OutcomeOf = entry => entry.BinaryPath.Outcome,
            TextOf = entry => entry.BinaryPath.ValueOr(null)
        },

        new QueryField
        {
            // Its own field rather than a value inside "path", because it answers a
            // different question. "path" is text and takes wildcards and expressions. This
            // one is about the thing that text points at.
            //
            // An orphan in the glossary's sense is then written out of parts that already
            // exist - file:missing start:auto - the same way automatic-without-delay is
            // start:auto !start:delayed rather than a value of its own. Measured on a real
            // machine on 2026-08-01: five entries name a file that is not there and none of
            // them is automatic, so a single "orphan" answer would have been an empty list
            // and the five would have had no way to be asked about.
            Name = "file",
            Kind = QueryFieldKind.Enumeration,
            OutcomeOf = entry => entry.BinaryOnDisk.Outcome,
            SymbolsOf = FileSymbols,
            Values =
            [
                new QueryValueName("present", "present"),
                new QueryValueName("missing", "missing")
            ]
        }
    ];

    /// <summary>
    /// Whether the file the entry runs is on disk.
    ///
    /// An entry naming no file at all reports neither symbol and is not incomplete: there
    /// is genuinely nothing to be present or missing, which is what <c>file:none</c> asks.
    /// </summary>
    private static FieldSymbols FileSymbols(ScmEntry entry) => entry.BinaryOnDisk.Outcome switch
    {
        ReadOutcome.Present => FieldSymbols.Of(entry.BinaryOnDisk.Value ? "present" : "missing"),
        ReadOutcome.Absent => FieldSymbols.Of(),
        _ => FieldSymbols.Nothing
    };

    /// <summary>
    /// Every kind an entry's triggers carry, plus the actions they take.
    ///
    /// Kinds and actions share one list of symbols on purpose. They are two questions about
    /// the same thing and a person asking "what starts this by itself" should not have to
    /// learn which of the two words they need. The names cannot collide - the kinds are
    /// conditions and the actions are the two verbs this tool already uses everywhere.
    /// </summary>
    private static FieldSymbols TriggerSymbols(ScmEntry entry)
    {
        if (!entry.Triggers.IsPresent)
        {
            return entry.Triggers.Outcome == ReadOutcome.Absent
                ? FieldSymbols.Of()
                : FieldSymbols.Nothing;
        }

        return FieldSymbols.Of(
        [
            .. entry.Triggers.Value!
                .SelectMany(trigger => new[] { Normalise(trigger.Kind.ToString()), Normalise(trigger.Action.ToString()) })
                .Distinct(StringComparer.Ordinal)
        ]);
    }

    /// <summary>
    /// The start type, plus "delayed" when the entry carries the delay flag.
    ///
    /// An automatic entry whose delay flag could not be read reports "automatic" and admits
    /// the gap. Reporting only "automatic" would answer <c>start:delayed</c> with a
    /// confident no, and a confident no about something we did not read is the failure
    /// this tool exists to avoid.
    /// </summary>
    private static FieldSymbols StartSymbols(ScmEntry entry)
    {
        if (!entry.StartType.IsPresent)
        {
            return FieldSymbols.Nothing;
        }

        var startType = Normalise(entry.StartType.Value.ToString());

        if (entry.StartType.Value != StartType.Automatic)
        {
            return FieldSymbols.Of(startType);
        }

        return entry.DelayedAuto.Outcome switch
        {
            ReadOutcome.Present when entry.DelayedAuto.Value => FieldSymbols.Of(startType, "delayed"),
            ReadOutcome.Present or ReadOutcome.Absent => FieldSymbols.Of(startType),
            _ => FieldSymbols.Partial(startType)
        };
    }

    /// <summary>
    /// What a bare word searches.
    ///
    /// The launch command joined here the moment the tool started reading it, which is what
    /// the query language document promised would happen. Typing a vendor's name into an
    /// empty box and finding the services that came with them is the whole point of a free
    /// search, and a path column somebody can see but not search reads as a bug.
    ///
    /// This widens what an existing bare word matches, so it is a change to the language and
    /// not only an addition. Harmless today because a query has nowhere to be saved until
    /// phase four - and the version stamp exists precisely so that stops being true then.
    /// </summary>
    private static QueryField[] BuildFreeSearch() =>
    [
        Required("name"),
        Required("display"),
        Required("account"),
        Required("path")
    ];

    private static QueryField Required(string name) =>
        All.First(field => field.Name == name);

    private static Dictionary<string, QueryField> BuildIndex()
    {
        var index = new Dictionary<string, QueryField>(StringComparer.Ordinal);

        foreach (var field in All)
        {
            index[Normalise(field.Name)] = field;

            foreach (var alias in field.Aliases)
            {
                index[Normalise(alias)] = field;
            }
        }

        return index;
    }
}
