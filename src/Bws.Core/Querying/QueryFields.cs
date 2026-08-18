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

    // Long because it is a table, not because it is tangled: one entry per field, each a
    // declaration with no branching in it. Splitting it would put the language's field list in
    // several places, which is the thing this file exists to prevent - and the ceiling is
    // aimed at methods somebody has to hold in their head, which this is not.
#pragma warning disable MA0051
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
            // "Which of these touches the printer" is a question the name and the display name
            // cannot answer and this one can. It is also the only text field where `none` and
            // `?` are worth asking - 384 entries of 819 have none and eight have one nobody
            // could resolve, so both are real populations rather than the empty sets they are
            // for a name. Why it is refused rather than absent is at ServiceDescription.
            Name = "description",
            Kind = QueryFieldKind.Text,
            OutcomeOf = entry => entry.Description.Outcome,
            TextOf = entry => entry.Description.ValueOr(null)
        },

        new QueryField
        {
            Name = "type",
            Kind = QueryFieldKind.Enumeration,
            OutcomeOf = _ => ReadOutcome.Present,
            SymbolsOf = entry => FieldSymbols.Of(Normalise(entry.EntryType.ToString())),
            Values = QueryValueNames.Type
        },

        new QueryField
        {
            Name = "status",
            Kind = QueryFieldKind.Enumeration,
            OutcomeOf = _ => ReadOutcome.Present,
            SymbolsOf = entry => FieldSymbols.Of(Normalise(entry.Status.ToString())),
            Values = QueryValueNames.Status
        },

        new QueryField
        {
            Name = "start",
            Kind = QueryFieldKind.Enumeration,
            OutcomeOf = entry => entry.StartType.Outcome,
            SymbolsOf = QuerySymbols.StartSymbols,
            Values = QueryValueNames.Start
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
            SymbolsOf = QuerySymbols.TriggerSymbols,
            Values = QueryValueNames.Trigger
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
            SymbolsOf = QuerySymbols.FileSymbols,
            Values = QueryValueNames.File
        },

        new QueryField
        {
            // Reserved in the query language document from the start, and in the
            // specification's own example - "path:~\temp\ signed:no" is written there.
            //
            // yes and no are not two of the values but two groups of them, the same way
            // type:driver covers both driver kinds. no means "Windows would not run this
            // quietly", which is the question somebody actually has: an expired signature
            // is a signature, and answering "yes, signed" about one would be true and
            // useless. The individual statuses are askable underneath for drilling in.
            Name = "signed",
            Kind = QueryFieldKind.Enumeration,
            Needs = ExtraRead.Signatures,
            OutcomeOf = entry => entry.Signature.Outcome,
            SymbolsOf = QuerySymbols.SignatureSymbols,
            Values = QueryValueNames.Signed
        },

        new QueryField
        {
            // "Show me everything not signed by Microsoft" is the question this exists for,
            // and it is one of the few that turns a service list into an audit.
            Name = "publisher",
            Kind = QueryFieldKind.Text,
            Needs = ExtraRead.Signatures,
            OutcomeOf = QuerySymbols.PublisherOutcome,
            TextOf = entry => entry.Signature.IsPresent ? entry.Signature.Value!.Publisher : null
        },

        new QueryField
        {
            // "Which services asked to keep the right to debug anything" is an audit
            // question with a one-line answer here and no answer at all in services.msc.
            //
            // Text rather than an enumeration, although Windows has a closed list of
            // privilege names. An enumeration would reject any name this code had not been
            // told about, so a machine carrying a privilege we had never seen would answer a
            // correct query with an error. Text also gives the fragment search that suits a
            // name nobody types in full: privilege:debug finds SeDebugPrivilege.
            //
            // Singular, because a member asks about one of them. The field holds a list and
            // matches when any value in it does.
            Name = "privilege",
            Kind = QueryFieldKind.Text,
            Aliases = ["privileges"],
            OutcomeOf = entry => entry.RequiredPrivileges.Outcome,
            TextsOf = entry => entry.RequiredPrivileges.ValueOr(null)
        },

        new QueryField
        {
            // Two values where Windows has three. The third, NONE, is the absence of an
            // identity rather than a kind of one, so it is read through the reserved word
            // every field has: sidtype:none. See ServiceSidType for why that is not a
            // convenience but the difference between "has none" and "nobody asked".
            Name = "sidtype",
            Kind = QueryFieldKind.Enumeration,
            OutcomeOf = entry => entry.SidType.Outcome,
            SymbolsOf = QuerySymbols.SidTypeSymbols,
            Values = QueryValueNames.Sidtype
        },

        new QueryField
        {
            // Named after the text form rather than after the descriptor, and the distinction
            // is the glossary's own - pitfall P9 keeps the descriptor, the permission list and
            // the text form apart because conflating them guarantees a misunderstanding at the
            // first conversation about a diff. This field searches the text form, which is
            // what the tool holds today. When the permission list is decoded for the window it
            // will want a field of its own, and this name leaves that one free.
            //
            // Blunt on purpose: sddl:"(A;;CCLCSWRPWPDTLOCRRC;;;WD)" is an expert's question
            // and reads like one. The everyday use is sddl:? and sddl:any, which ask which
            // entries would not give up their permissions - the question rule 8 of CLAUDE.md
            // exists for.
            Name = "sddl",
            Kind = QueryFieldKind.Text,
            OutcomeOf = entry => entry.SecurityDescriptor.Outcome,
            TextOf = entry => entry.SecurityDescriptor.ValueOr(null)
        },

        new QueryField
        {
            // Reserved in the query language document from the start, and written out in the
            // specification's own showcase query as memory:>500MB - which is where the unit
            // comes from. It is the reason this language has sizes at all.
            //
            // The working set rather than the commit, because it is the number a person can
            // check us against: it is what Get-Process reports as WorkingSet64 and what Task
            // Manager shows in its working set column. The commit figure is in the machine
            // readable output next to it and deliberately has no field of its own - nobody
            // asked for one, and a second size field would have to be named well enough to
            // be told apart from this one at a glance.
            Name = "memory",
            Kind = QueryFieldKind.Size,
            Needs = ExtraRead.Memory,
            OutcomeOf = entry => entry.Memory.Outcome,
            SizeOf = entry => entry.Memory.IsPresent ? entry.Memory.Value!.WorkingSet : null
        },

        new QueryField
        {
            // BOTH DIRECTIONS UNDER ONE NAME, AND THE VALUES KEEP THEM APART - backlog 170 and 172.
            // The window has carried the first of these as a column since backlog 165 and nobody
            // could ask about it, which is the asymmetry this closes: a fact you can see and cannot
            // filter on is half a feature.
            //
            // ONE FIELD RATHER THAN TWO BOOLEANS, for three reasons that are all checkable here. A
            // chip has to be able to write itself as a member somebody can read, and
            // mismatch:running reads like trigger:device. One field is one addition to a surface
            // people keep in scripts rather than two, and a surface does not narrow again. And the
            // enumeration keeps the DIRECTION that backlog 170 refused to collapse - any and none
            // arrive free from the reserved words, so the convenience costs no information.
            Name = "mismatch",
            Kind = QueryFieldKind.Enumeration,
            OutcomeOf = QuerySymbols.MismatchOutcome,
            SymbolsOf = QuerySymbols.MismatchSymbols,
            Values = QueryValueNames.Mismatch
        }
    ];

#pragma warning restore MA0051

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
    /// <remarks>
    /// <b>The description is deliberately NOT here, and that is a question for the owner rather
    /// than a decision taken quietly.</b> The argument above applies to it word for word - a
    /// column somebody can see but not search reads as a bug - and it is a searchable field, so
    /// <c>description:printer</c> works today. What is withheld is the BARE word.
    ///
    /// The reason to withhold it is that a description is prose while the four fields above are
    /// labels: a bare word would start matching a sentence somewhere inside 819 paragraphs, and a
    /// common word would widen a free search from a few entries to dozens. That is a change
    /// somebody would notice in the box they already use, so it belongs to whoever owns what the
    /// window feels like. Backlog 175.
    /// </remarks>
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
