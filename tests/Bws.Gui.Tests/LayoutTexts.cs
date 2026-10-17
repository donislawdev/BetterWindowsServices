using System.Globalization;
using System.Text.Json;
using Bws.Gui.ViewModels;
using CsCheck;

namespace Bws.Gui.Tests;

/// <summary>
/// Layout files as somebody editing one by hand might leave them - every key the file knows, in any
/// order and any of them twice, each holding the value it should or a value of the wrong type.
///
/// <b>Why a generator beside the damaged real file.</b> Damage to a real layout (Damaged) is good at
/// files that stop parsing and almost never builds one that parses and is wrong in a particular
/// place: a width that is a number, a column entry that is a string, a sort naming no column, a
/// schema version written in quotes, the same column twice. Those are the files a person writes, and
/// they reach ColumnLayouts.Read past the JSON parser and ColumnPlan.Of past the reader.
///
/// <b>The keys are the reader's own words, copied, not read from it.</b> ColumnLayouts.Read names
/// them as literals and nothing in the product lists them, so a key added there and not here is a
/// key this generator never writes - the price of a list kept by hand, said rather than hidden.
/// </summary>
internal static class LayoutTexts
{
    private static readonly string[] Ids = [.. Columns.All.Select(column => column.Id)];

    private static readonly Gen<string> Anything = Gen.String;

    /// <summary>A value of whatever type is wrong where it stands, written as JSON.</summary>
    private static readonly Gen<string> Odd = Gen.OneOfConst(
        "true", "false", "null", "0", "1", "-1", "1.5", "1e400", "2147483648", "[]", "{}", "\"\"", "\"5\"");

    private static readonly Gen<string> Flag = Gen.OneOf(Gen.OneOfConst("true", "false"), Odd);

    private static readonly Gen<string> Id = Gen.Frequency(
        (8, Gen.OneOfConst(Ids)),
        (1, Gen.OneOfConst("", "Status", "status ", "nothing")),
        (1, Anything));

    /// <summary>
    /// The forms the markup uses, the ones ListColumns refuses, and anything at all. Internal for
    /// KeptWidthPropertyTests, which puts the same texts through the grid rather than the reader.
    /// </summary>
    internal static readonly Gen<string> Width = Gen.Frequency(
        (4, Gen.OneOfConst("220", "2*", "Auto", "2.5*", "0", "", "SizeToCells")),
        (2, Gen.OneOfConst("1e300", "1e300*", "-40", "-1*", "NaN", "Infinity", "5px5", "1,5", "1in", "-0")),
        (1, Anything));

    private static readonly Gen<string> Entry = Gen.Frequency(
        (8, Object(
            Gen.Frequency(
                (4, Member("id", Id.Select(Quoted))),
                (2, Member("shown", Flag)),
                (2, Member("width", Gen.OneOf(Width.Select(Quoted), Odd))),
                (1, Member("id", Odd)),
                (1, Member("x", Odd))),
            most: 4)),
        (1, Odd));

    /// <summary>
    /// Every column this build has, each one hidden - the one list that makes ColumnPlan refuse a file
    /// rather than obey it. Thirty random entries never hide all of them, and the first measurement of
    /// these properties (2026-10-17) showed the refusal never ran, so "at least one shown" was being
    /// held only where it held by itself.
    /// </summary>
    private static readonly Gen<string> AllHidden = Width.Select(Quoted).Array[Ids.Length]
        .Select(widths => "[" + string.Join(",", Ids.Zip(widths, (id, width) => "{\"id\":" + Quoted(id) + ",\"shown\":false,\"width\":" + width + "}")) + "]");

    private static readonly Gen<string> ColumnList = Gen.Frequency(
        (8, Entry.Array[0, 30].Select(entries => "[" + string.Join(",", entries) + "]")),
        (1, AllHidden),
        (1, Odd));

    private static readonly Gen<string> Sort = Gen.Frequency(
        (6, Object(
            Gen.Frequency(
                (3, Member("id", Id.Select(Quoted))),
                (2, Member("descending", Flag)),
                (1, Member("id", Odd))),
            most: 3)),
        (1, Odd));

    private static readonly Gen<string> Version = Gen.Frequency(
        (8, Gen.Int[ColumnLayout.OldestSchemaVersionRead, ColumnLayout.CurrentSchemaVersion]
            .Select(version => version.ToString(CultureInfo.InvariantCulture))),
        (1, Gen.OneOfConst("0", "6", "99", "-1", "5.0", "\"5\"", "null", "2147483648")));

    /// <summary>
    /// Each key with the value it should hold. A table rather than a switch, because the switch put
    /// the method among the ones standing near the ceiling of forks, every <c>or</c> counted apart.
    /// </summary>
    private static readonly (string Key, Gen<string> Value)[] Members =
    [
        ("columns", ColumnList), ("driverColumns", ColumnList), ("everythingColumns", ColumnList),
        ("sort", Sort), ("driverSort", Sort), ("everythingSort", Sort),
        ("overviewSeen", Flag), ("filtersFolded", Flag), ("schemaVersion", Version), ("somethingElse", Odd)
    ];

    private static readonly Gen<string> AnyMember = Gen.OneOf([.. Members.Select(member => Member(member.Key, member.Value))]);

    /// <summary>
    /// A whole file: nearly always a version and a list of columns, since without them the reader
    /// stops at its first question, and then up to six members more, which may repeat either. Now
    /// and then not an object at all, which is the reader's own first refusal.
    /// </summary>
    internal static readonly Gen<string> Shaped = Gen.Frequency(
        (12, Gen.Select(
            Gen.Frequency((9, Member("schemaVersion", Version)), (1, Gen.Const(string.Empty))),
            Gen.Frequency((9, Member("columns", ColumnList)), (1, Gen.Const(string.Empty))),
            AnyMember.Array[0, 6],
            (version, columns, more) =>
                "{" + string.Join(",", new[] { version, columns }.Concat(more).Where(member => member.Length > 0)) + "}")),
        (1, Odd));

    private static string Quoted(string text) => JsonSerializer.Serialize(text);

    private static Gen<string> Member(string key, Gen<string> value) =>
        value.Select(written => Quoted(key) + ":" + written);

    private static Gen<string> Object(Gen<string> member, int most) =>
        member.Array[0, most].Select(members => "{" + string.Join(",", members) + "}");
}
