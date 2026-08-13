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
internal sealed record ColumnLayout(IReadOnlyList<KeptColumn> Columns)
{
    /// <summary>
    /// The version of the file format, which is a frozen contract - `docs/02`.
    ///
    /// Spelled the same as the snapshot's own, because they answer the same question and a reader
    /// who has met one should not have to learn a second word for it.
    /// </summary>
    internal const int CurrentSchemaVersion = 1;

    /// <summary>What the window shows before anybody has ever chosen anything.</summary>
    internal static ColumnLayout Default { get; } =
        new([.. ViewModels.Columns.All.Select(column =>
            new KeptColumn(column.Id, column.ShownAtFirst, Width: null))]);

    private static readonly JsonSerializerOptions Shape = new()
    {
        WriteIndented = true,

        // Characters as themselves rather than as escapes, for the reason SnapshotJson gives at
        // length: a file nobody can read in a diff is a file this product had no business writing
        // as text. Nothing in here is outside ASCII today - every identifier is one of eighteen
        // English words - so this is the habit rather than a fix, and it costs nothing.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

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
    internal string Render()
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

        var tree = new JsonObject
        {
            ["columns"] = columns,
            ["schemaVersion"] = CurrentSchemaVersion
        };

        return tree.ToJsonString(Shape) + "\n";
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

        if (version != CurrentSchemaVersion)
        {
            // NOT AN ERROR AND NOT QUARANTINED, and the difference matters: a file this build does
            // not understand is somebody's settings written by another build, so the answer is to
            // leave it exactly where it is. Reading it anyway would guess at what a field means
            // there, and overwriting it would throw away the layout they get back the moment they
            // open the other build again.
            return new LayoutReading { OtherSchemaVersion = version };
        }

        if (root["columns"] is not JsonArray listed)
        {
            return new LayoutReading { Unreadable = "The file names no columns." };
        }

        return new LayoutReading { Layout = new ColumnLayout([.. Listed(listed)]) };
    }

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
/// What came back from a layout file, in the three states a read can end in.
///
/// Nothing here is a null pretending to be an answer - `docs/06` part 3, one layer up from a
/// field of an entry. All three properties empty is the ordinary first run: there is no file, so
/// there is nothing to say and nothing went wrong.
/// </summary>
internal sealed record LayoutReading
{
    /// <summary>What the file said, or nothing when it said nothing usable.</summary>
    internal ColumnLayout? Layout { get; init; }

    /// <summary>Why the file is not a layout at all. The file is moved aside when this is set.</summary>
    internal string? Unreadable { get; init; }

    /// <summary>Where the unreadable file went, when it could be moved at all.</summary>
    internal string? MovedAside { get; init; }

    /// <summary>The schema the file claims, when it is not the one this build writes.</summary>
    internal int? OtherSchemaVersion { get; init; }

    /// <summary>Whether the window has to say something about this before anybody asks.</summary>
    internal bool WorthSaying => Unreadable is not null || OtherSchemaVersion is not null;
}

/// <summary>
/// A layout file reconciled with the columns this build actually has.
///
/// <b>Four degenerate files, all of them ordinary, and each answered here rather than at the
/// grid.</b> `docs/04` names them at `S6d`: a layout holding a column that has since been taken
/// away, a layout written before a column was added, a layout with every column hidden, and a
/// layout from a schema this build does not read. The last one never reaches this class - it is
/// refused while reading, because guessing at its fields is the fault it exists to prevent.
///
/// <b>What comes out is always complete and always usable</b>: every column this build has,
/// exactly once, in a display order, with at least one of them shown. The grid is then handed
/// something it cannot be broken by, and everything that had to be left out is named.
/// </summary>
internal sealed record ColumnPlan(ColumnLayout Layout, IReadOnlyList<string> Ignored, bool NoneWasShown)
{
    /// <summary>
    /// Works out what to show, from a layout that may be missing, stale or contradictory.
    ///
    /// <b>A column the file never mentioned joins at the end with the state it would have had if
    /// there were no file.</b> That is the case of a build that added a column since the layout
    /// was written, and the alternative - dropping it - is a feature that silently does not exist
    /// for everybody who used the previous version.
    ///
    /// <b>A file with every column hidden is refused rather than obeyed.</b> Eight hundred rows of
    /// nothing above a count line still saying eight hundred is the empty rectangle complaint 9 of
    /// `docs/11` is about, and <see cref="ColumnChoice.MayHide"/> already refuses to let anybody
    /// reach it through the window - so a file that asks for it was hand edited, and the honest
    /// answer is the usual six and a sentence saying so.
    /// </summary>
    internal static ColumnPlan Of(ColumnLayout? layout)
    {
        if (layout is null)
        {
            return new ColumnPlan(ColumnLayout.Default, [], NoneWasShown: false);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var kept = new List<KeptColumn>();
        var ignored = new List<string>();

        foreach (var column in layout.Columns)
        {
            // A name this build does not have, or the same column twice. Both are files somebody
            // edited or files older than a rename, and both leave a column out of the order -
            // which is a thing the person is told about rather than a thing they notice later.
            if (Columns.Of(column.Id) is null || !seen.Add(column.Id))
            {
                ignored.Add(column.Id);

                continue;
            }

            kept.Add(column);
        }

        foreach (var column in Columns.All.Where(column => seen.Add(column.Id)))
        {
            kept.Add(new KeptColumn(column.Id, column.ShownAtFirst, Width: null));
        }

        var noneWasShown = !kept.Exists(column => column.Shown);

        if (noneWasShown)
        {
            kept = [.. kept.Select(column =>
                column with { Shown = Columns.Of(column.Id)!.ShownAtFirst })];
        }

        return new ColumnPlan(new ColumnLayout(kept), ignored, noneWasShown);
    }
}
