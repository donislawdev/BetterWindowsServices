using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bws.Gui.ViewModels;

/// <summary>
/// One column as a file remembers it.
///
/// <b>The identifier is the whole reason this is a contract.</b> It is the glossary's own spelling
/// of the field - `docs/03` part 4 - and it is what ends up in a file on somebody's machine, which
/// is why renaming a column identifier stopped being free the day this record was written. A
/// heading would have been the obvious thing to write instead and would be wrong twice over: it is
/// translated, so a layout saved on a Polish machine and opened on an English one would name no
/// column at all, and it is prose somebody may improve.
///
/// <b>The width is TEXT, and null means the theme decides.</b> A number could not tell 220 pixels
/// from two shares of what is left, and eleven of the eighteen columns are the second kind. What
/// goes in here is the same form the markup uses - <c>220</c>, <c>2*</c>, <c>Auto</c> - so a person
/// reading the file sees what they would have written, and a column nobody has dragged carries
/// nothing at all rather than a copy of the theme's own number. That last part is `ADR-23`
/// surviving contact with a saved file: the theme stays the starting width for every column until
/// somebody moves that one.
/// </summary>
internal sealed record KeptColumn(string Id, bool Shown, string? Width);

/// <summary>
/// Which column the list was sorted by, and which way round.
///
/// <b>The identifier again rather than a heading, and for the reason above it</b> - a heading is
/// translated, so a layout saved on a Polish machine would name no column at all on an English one.
///
/// <b>A direction rather than a WPF ListSortDirection.</b> Nothing in this file knows what a
/// DataGrid is, which is what lets the whole of it be checked without a window - and a bool written
/// as <c>descending</c> is what somebody opening the file would have written themselves.
/// </summary>
internal sealed record KeptSort(string Id, bool Descending);

/// <summary>
/// Which columns the list shows, in what order and how wide - the first thing this product keeps
/// on disk between sessions.
///
/// <b>The order of <see cref="Columns"/> IS the display order.</b> A position written on each
/// entry was the alternative, and it can disagree with itself: two columns claiming position four,
/// a file jumping from three to nine. An array cannot be inconsistent about its own order, so the
/// whole family of faults never arrives.
///
/// <b>Nothing here knows what a DataGrid is</b>, which is what lets the whole of it be checked
/// without a window - the same split <see cref="Column"/> and <c>ListColumns</c> already make.
/// </summary>
internal sealed record ColumnLayout(IReadOnlyList<KeptColumn> Columns, KeptSort? Sort = null)
{
    /// <summary>
    /// The version of the file format, which is a frozen contract - `docs/02`.
    ///
    /// Spelled the same as the snapshot's own, because they answer the same question and a reader
    /// who has met one should not have to learn a second word for it.
    /// </summary>
    /// <summary>
    /// TWO SINCE 2026-08-19, because the file now keeps one layout per scope.
    ///
    /// <b>The shape grew rather than changed, which is what makes the older file readable.</b> The
    /// <c>columns</c> array is still there, still means exactly what it meant, and is still the
    /// first thing in the file - what joined it are two siblings. A version 1 file IS a version 2
    /// file that says nothing about drivers or about both at once.
    /// </summary>
    /// <summary>
    /// THREE SINCE 2026-08-25, AND THE SHAPE GREW AGAIN RATHER THAN CHANGING. Three more siblings
    /// joined - which column each list was sorted by, and which way round. Every array that was
    /// there is still there, still means what it meant, and a version 2 file IS a version 3 file
    /// that says nothing about sorting.
    /// </summary>
    /// <summary>
    /// FOUR SINCE 2026-08-25, LATER THE SAME DAY, AND ONE SIBLING JOINED - whether the machine
    /// overview has been dismissed. A version 3 file IS a version 4 file that says nothing about it,
    /// and saying nothing means "not yet", which is what an older file honestly means.
    ///
    /// <b>IT IS HERE RATHER THAN IN A SIGNAL THE PROBES ALREADY OWN, and that is the whole reason
    /// this version exists</b> - owner's decision, 2026-08-25. `G` asks for the overview "at start
    /// without saved configuration", and the obvious reading of that is the absence of this file.
    /// But eleven probes in tools/ move this file aside ON PURPOSE, to measure the theme rather than
    /// somebody's arrangement - <c>tools/gui-probe/window-helpers.ps1</c> says so in as many words -
    /// so its absence already means "this person has not arranged their columns". Overloading it
    /// with "and has never seen this tool" would make every probe open on the overview instead of
    /// the list it exists to measure, and would conflate two facts that are genuinely different.
    ///
    /// <b>The alternative and its price, said rather than left out:</b> the probes could lay down a
    /// default layout after moving the person's aside, which needs no schema bump - and needs a copy
    /// of which six columns are default, knowledge that lives only in <see cref="Columns"/> today
    /// and would drift the first time that list changes, silently.
    /// </summary>
    /// <summary>
    /// FIVE SINCE 2026-09-24, AND ONE MORE SIBLING - whether the filter chips were folded away when
    /// the window last closed. UX-GUI-007, owner's decision: the chips took nearly half the height of
    /// the window above the list, and the toggle folding them was forgotten at every start. A version
    /// 4 file IS a version 5 file that says nothing about it, and saying nothing means "open", which
    /// is what the window has always done and what somebody who has never folded them should see.
    /// </summary>
    internal const int CurrentSchemaVersion = 5;

    /// <summary>The oldest schema this build reads and carries forward. See <see cref="ColumnLayouts.Read"/>.</summary>
    internal const int OldestSchemaVersionRead = 1;

    /// <summary>
    /// What a scope shows before anybody has ever chosen anything.
    ///
    /// <b>Per scope since 2026-08-19</b>, because a column that is empty in one list is not empty
    /// in the other - <see cref="Column.OffAtFirstIn"/> carries the measurement.
    ///
    /// <b>AND IT ARRIVES SORTED SINCE 2026-09-02, WHICH IT NEVER DID BEFORE.</b> The list used to
    /// come in whatever order the manager handed it over, which looks alphabetical by internal name
    /// and is not quite - <c>AdobeARMservice</c> lands before <c>ADPSvc</c> because a capital sorts
    /// before a lowercase letter, and nothing on screen said why. services.msc opens sorted by the
    /// name it shows, ascending, and this is the same promise made deliberately rather than
    /// inherited from an enumeration order nobody chose.
    ///
    /// <b>It is a DEFAULT and not a rule.</b> Anybody who has ever clicked a heading has a sort in
    /// their profile, and that one still wins - this is only what a machine nobody has touched does.
    /// </summary>
    internal static ColumnLayout DefaultFor(EntryScope scope) =>
        new(
            [.. ViewModels.Columns.All.Select(column =>
                new KeptColumn(column.Id, column.ShownAtFirstIn(scope), Width: null))],
            Sort: new KeptSort("displayName", Descending: false));

    /// <summary>
    /// The text that goes in the file.
    ///
    /// Keys are written in a fixed order and it happens to be the ordinal one, which is what
    /// `ADR-6` asks of a snapshot for the same reason: a file that reorders its own keys diffs
    /// against yesterday's copy on nothing.
    ///
    /// A newline at the end, because a file without one shows its last line as changed the first
    /// time anybody appends to it.
    /// </summary>
    internal JsonArray Render()
    {
        var columns = new JsonArray();

        foreach (var column in Columns)
        {
            var entry = new JsonObject
            {
                ["id"] = column.Id,
                ["shown"] = column.Shown
            };

            if (column.Width is { } width)
            {
                entry["width"] = width;
            }

            columns.Add(entry);
        }

        return columns;
    }

    /// <summary>
    /// Reads a layout back, or says what is wrong with the file.
    ///
    /// <b>Returns rather than throws, for the same reason <c>SnapshotJson.TryRead</c> does.</b>
    /// A file on disk answers to nobody: it can be hand edited, truncated by a full disk, merged
    /// by somebody's tooling, or written by a build that does not exist yet. None of those is the
    /// window falling over, and all of them have to end with the person being told rather than
    /// with a layout quietly not being the one they left.
    ///
    /// <b>Every value is asked for rather than assumed</b>, which is why this parses nodes instead
    /// of deserialising into the record. <c>GetValue&lt;int&gt;</c> on a string throws, and a
    /// layout file is exactly the document where a string where a number belongs is ordinary.
    /// </summary>
    internal static ColumnLayout From(JsonArray listed) => new([.. Listed(listed)]);

    /// <summary>
    /// Every entry the file holds that says anything at all.
    ///
    /// An entry with no usable identifier is dropped here and named later, when the catalogue is
    /// consulted - it is the same kind of fault as an identifier this build has never heard of,
    /// and a person is owed one sentence about their file rather than two.
    ///
    /// <b>A missing <c>shown</c> reads as the column's usual state rather than as hidden.</b> An
    /// entry holding nothing but an identifier is what somebody writes by hand when they mean
    /// "this column, as it comes" - and defaulting to hidden would answer that by silently taking
    /// a column away.
    /// </summary>
    private static IEnumerable<KeptColumn> Listed(JsonArray listed)
    {
        foreach (var node in listed)
        {
            if (node is not JsonObject entry
                || entry["id"] is not JsonValue named
                || !named.TryGetValue<string>(out var id)
                || string.IsNullOrEmpty(id))
            {
                continue;
            }

            var shown = entry["shown"] is JsonValue flag && flag.TryGetValue<bool>(out var wanted)
                ? wanted
                : ViewModels.Columns.Of(id)?.ShownAtFirst ?? false;

            var width = entry["width"] is JsonValue measured && measured.TryGetValue<string>(out var text)
                ? text
                : null;

            yield return new KeptColumn(id, shown, width);
        }
    }
}

/// <summary>
/// The whole of what the preferences file keeps about columns - one layout per scope.
///
/// <b>Built 2026-08-19, when services and drivers became separate lists.</b> A driver has no
/// process identifier and almost never an account, so a single layout serving both spends three
/// columns of a 472 row list on emptiness - the numbers are at <see cref="Column.OffAtFirstIn"/>.
///
/// <b>THE FILE GREW A SIBLING RATHER THAN CHANGING A FIELD, and that is what makes an older file
/// survive.</b> <c>columns</c> is untouched, still first, still the same shape - so a build that
/// only knows version 1 still finds a layout it understands, and this build reads a version 1 file
/// as "the services layout, and nothing said about the other two". Nobody loses the widths they
/// dragged.
/// </summary>
internal sealed record ColumnLayouts(ColumnLayout Services, ColumnLayout Drivers, ColumnLayout Everything)
{
    /// <summary>What every scope shows before anybody has chosen anything.</summary>
    internal static ColumnLayouts Default { get; } = new(
        ColumnLayout.DefaultFor(EntryScope.Services),
        ColumnLayout.DefaultFor(EntryScope.Drivers),
        ColumnLayout.DefaultFor(EntryScope.Everything));

    /// <summary>
    /// Whether the machine overview has been dismissed on this profile - `G`, schema 4.
    ///
    /// <b>An init-only property rather than a fourth positional parameter, and that is not a style
    /// choice.</b> This record is named for the three column layouts and every call site constructs
    /// it with those three. A fourth position would make every one of them state an answer to a
    /// question they are not about, and the honest default - false, meaning "nobody has seen it" -
    /// is exactly what an absent key in an older file means.
    ///
    /// <b>False is the answer for every file this build has never written</b>, including a version 3
    /// one carried forward. That is deliberate: somebody upgrading has not seen this screen either,
    /// and showing it once to a person who has already arranged their columns is a smaller mistake
    /// than never showing it at all.
    /// </summary>
    internal bool OverviewSeen { get; init; }

    /// <summary>
    /// Whether the filter chips were folded away - UX-GUI-007, schema 5. Init-only for the reason
    /// <see cref="OverviewSeen"/> is, and false - open - for every file this build did not write,
    /// because open is what complaint 7 of `docs/11` asked for and what a first run shows.
    /// </summary>
    internal bool FiltersFolded { get; init; }

    /// <summary>The layout for one scope.</summary>
    internal ColumnLayout For(EntryScope scope) => scope switch
    {
        EntryScope.Services => Services,
        EntryScope.Drivers => Drivers,
        EntryScope.Everything => Everything,

        // Not a default arm that picks one, for the reason Scopes.QueryFor gives: a fourth scope
        // must fail here loudly rather than silently share a layout with one of the three.
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "No layout is kept for this scope.")
    };

    /// <summary>The same three with one of them replaced.</summary>
    internal ColumnLayouts With(EntryScope scope, ColumnLayout layout) => scope switch
    {
        EntryScope.Services => this with { Services = layout },
        EntryScope.Drivers => this with { Drivers = layout },
        EntryScope.Everything => this with { Everything = layout },
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "No layout is kept for this scope.")
    };

    /// <summary>
    /// The text that goes in the file.
    ///
    /// Keys are written in a fixed order and it happens to be the ordinal one, which is what
    /// `ADR-6` asks of a snapshot for the same reason: a file that reorders its own keys diffs
    /// against yesterday's copy on nothing. <c>columns</c>, <c>driverColumns</c>,
    /// <c>everythingColumns</c>, <c>schemaVersion</c> - c, d, e, s.
    ///
    /// A newline at the end, because a file without one shows its last line as changed the first
    /// time anybody appends to it.
    /// </summary>
    internal string Render()
    {
        var tree = new JsonObject
        {
            ["columns"] = Services.Render(),
            ["driverColumns"] = Drivers.Render()
        };

        // A SCOPE NOBODY HAS SORTED WRITES NOTHING RATHER THAN A NULL, which is the rule the width
        // already follows: what is not in the file is what the program does on its own. The keys
        // stay in ordinal order as the paragraph above promises - driverSort after driverColumns,
        // everythingSort after everythingColumns, and sort last of all because s-o beats s-c.
        Sorted(tree, "driverSort", Drivers.Sort);

        tree["everythingColumns"] = Everything.Render();

        Sorted(tree, "everythingSort", Everything.Sort);

        // The same rule for the folded filters, schema 5 - written only when folded. Ordinal order
        // holds: f comes after everythingSort and before overviewSeen.
        if (FiltersFolded)
        {
            tree["filtersFolded"] = true;
        }

        // WRITTEN ONLY WHEN IT IS TRUE, which is the rule every optional key in this file follows:
        // what is not there is what the program does on its own. Ordinal order holds - o beats s,
        // and everythingSort is already past.
        if (OverviewSeen)
        {
            tree["overviewSeen"] = true;
        }

        tree["schemaVersion"] = ColumnLayout.CurrentSchemaVersion;

        Sorted(tree, "sort", Services.Sort);

        return tree.ToJsonString(Shape) + "\n";
    }

    /// <summary>
    /// Reads the file back, or says what is wrong with it.
    ///
    /// <b>Returns rather than throws, for the same reason <c>SnapshotJson.TryRead</c> does.</b> A
    /// file on disk answers to nobody: it can be hand edited, truncated by a full disk, merged by
    /// somebody's tooling, or written by a build that does not exist yet. None of those is the
    /// window falling over, and all of them have to end with the person being told rather than with
    /// a layout quietly not being the one they left.
    ///
    /// <b>OLDER AND NEWER STOPPED MEANING THE SAME THING ON 2026-08-19, and until that day they
    /// did.</b> The rule was "a different schema version leaves the file untouched", with a reason
    /// that is right for exactly one direction: a file from a build that does not exist yet holds
    /// fields this one would have to guess at, and overwriting it throws away settings somebody
    /// gets back the moment they open the other build. <b>None of that is true of our own past.</b>
    /// A version 1 file was written by this product, its every field is known here, and treating it
    /// as foreign would have made the scope switch a slice that silently forgot the column widths
    /// of everybody who had ever dragged one.
    ///
    /// So newer is still left alone, and older is carried forward. `docs/02` says so too - that
    /// table row was split rather than left disagreeing with this method.
    /// </summary>
    internal static LayoutReading Read(string content)
    {
        JsonNode? tree;

        try
        {
            tree = JsonNode.Parse(content);
        }
        catch (JsonException problem)
        {
            return new LayoutReading { Unreadable = problem.Message };
        }

        if (tree is not JsonObject root)
        {
            return new LayoutReading { Unreadable = "The file does not hold a layout." };
        }

        if (root["schemaVersion"] is not JsonValue stated || !stated.TryGetValue<int>(out var version))
        {
            return new LayoutReading { Unreadable = "The file does not say which schema it uses." };
        }

        if (version > ColumnLayout.CurrentSchemaVersion || version < ColumnLayout.OldestSchemaVersionRead)
        {
            return new LayoutReading { OtherSchemaVersion = version };
        }

        if (root["columns"] is not JsonArray listed)
        {
            return new LayoutReading { Unreadable = "The file names no columns." };
        }

        var services = ColumnLayout.From(listed) with { Sort = SortFrom(root["sort"]) };

        // A SECTION THAT IS NOT THERE IS THE DEFAULT FOR THAT SCOPE, not an empty layout - which is
        // both what a version 1 file means and what a version 2 file written by hand means. It is
        // the same rule the columns array already follows for a column it never mentions.
        var drivers = (root["driverColumns"] is JsonArray forDrivers
            ? ColumnLayout.From(forDrivers)
            : ColumnLayout.DefaultFor(EntryScope.Drivers)) with { Sort = SortFrom(root["driverSort"]) };

        var everything = (root["everythingColumns"] is JsonArray forEverything
            ? ColumnLayout.From(forEverything)
            : ColumnLayout.DefaultFor(EntryScope.Everything)) with { Sort = SortFrom(root["everythingSort"]) };

        // ASKED FOR AS A BOOLEAN AND NOT COERCED FROM ANYTHING ELSE. A key holding a string or a
        // number is a file somebody edited by hand into a shape this does not know, and answering
        // "not seen" is the safe half of that: the worst it costs is one screen somebody has
        // already read, where the other way round costs the screen entirely.
        var overviewSeen = root["overviewSeen"] is JsonValue seen && seen.TryGetValue<bool>(out var yes) && yes;

        // The same strictness for the folded filters, and the safe half is "open" for the same
        // reason: a hand-edited key costs at most a row of chips somebody folds again.
        var filtersFolded = root["filtersFolded"] is JsonValue fold && fold.TryGetValue<bool>(out var folded) && folded;

        return new LayoutReading
        {
            Layouts = new ColumnLayouts(services, drivers, everything) { OverviewSeen = overviewSeen, FiltersFolded = filtersFolded },
            CarriedForwardFrom = version < ColumnLayout.CurrentSchemaVersion ? version : null
        };
    }

    /// <summary>One scope's sort, written only when there is one.</summary>
    private static void Sorted(JsonObject tree, string key, KeptSort? sort)
    {
        if (sort is not { } kept)
        {
            return;
        }

        tree[key] = new JsonObject
        {
            ["id"] = kept.Id,
            ["descending"] = kept.Descending
        };
    }

    /// <summary>
    /// One scope's sort, read back, or nothing at all.
    ///
    /// <b>Every value is asked for rather than assumed</b>, the rule the columns array already
    /// follows: a file on disk can be hand edited, so a string where a bool belongs is ordinary.
    /// An entry naming no column is nothing rather than a fault, because the list then comes up
    /// unsorted and there is no half state to explain.
    /// </summary>
    private static KeptSort? SortFrom(JsonNode? node)
    {
        if (node is not JsonObject entry
            || entry["id"] is not JsonValue named
            || !named.TryGetValue<string>(out var id)
            || string.IsNullOrEmpty(id))
        {
            return null;
        }

        var descending = entry["descending"] is JsonValue way
            && way.TryGetValue<bool>(out var down)
            && down;

        return new KeptSort(id, descending);
    }

    private static readonly JsonSerializerOptions Shape = new()
    {
        WriteIndented = true,

        // Characters as themselves rather than as escapes, for the reason SnapshotJson gives at
        // length: a file nobody can read in a diff is a file this product had no business writing
        // as text. Nothing in here is outside ASCII today - every identifier is one of eighteen
        // English words - so this is the habit rather than a fix, and it costs nothing.
        //
        // This comment stood on a second copy of these options in ColumnLayout, which nothing
        // read, until the unread-member rule found it on 2026-09-23. The copy that writes the file
        // is this one, and it had no comment at all.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}

/// <summary>
/// What came back from a layout file, in the three states a read can end in.
///
/// Nothing here is a null pretending to be an answer - `docs/06` part 3, one layer up from a
/// field of an entry. All three properties empty is the ordinary first run: there is no file, so
/// there is nothing to say and nothing went wrong.
/// </summary>
internal sealed record LayoutReading
{
    /// <summary>What the file said, or nothing when it said nothing usable.</summary>
    internal ColumnLayouts? Layouts { get; init; }

    /// <summary>Why the file is not a layout at all. The file is moved aside when this is set.</summary>
    internal string? Unreadable { get; init; }

    /// <summary>Where the unreadable file went, when it could be moved at all.</summary>
    internal string? MovedAside { get; init; }

    /// <summary>The schema the file claims, when this build will not read it.</summary>
    internal int? OtherSchemaVersion { get; init; }

    /// <summary>
    /// The older schema this file was written in, when it was read and carried forward anyway.
    ///
    /// <b>Set rather than silent, and it is not the same thing as
    /// <see cref="OtherSchemaVersion"/>.</b> That one means the file was left alone. This one means
    /// it was understood, used, and will be written back in the current shape the next time
    /// anything changes - so the person's own file is about to stop being readable by the build
    /// they had yesterday. Rule 8: a window that quietly upgrades somebody's settings has changed
    /// something on their disk without saying so.
    /// </summary>
    internal int? CarriedForwardFrom { get; init; }
}
