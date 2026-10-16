using System.Globalization;
using Bws.Core.Querying;
using CsCheck;

namespace Bws.Core.Tests;

/// <summary>
/// Query text shaped like the language, for the properties that need to reach past its first
/// character.
///
/// <b>Why a second generator beside the alphabet of awkward characters in ParserPropertyTests.</b>
/// Measured 2026-10-16 with tools\coverage-probe\uncovered.ps1: the random properties on their own
/// ran 63 per cent of the query language's lines, against 98 for every test together - and the
/// reader of a field's VALUE, where <c>status:running</c>, <c>pid:&gt;100</c> and <c>memory:1GB</c>
/// are taken apart, at 28 per cent. Thirty-five characters in strings of up to forty spell a field
/// name followed by a colon and a value it accepts almost never, so the property "any text is read
/// or refused" was being checked mostly on text refused at the first member.
///
/// <b>Built from the language's own lists, never from a copy.</b> Field names, their aliases and
/// their kinds come from QueryFields, the values of an enumeration from QueryFields.ValuesOf, the
/// three reserved words from QueryFields.ReservedWords. A field added tomorrow is generated
/// tomorrow without an edit here, and a list written out in this file would be the second copy of
/// a frozen contract that this project has paid for before.
///
/// <b>Mostly right and sometimes wrong, on purpose.</b> Every place a value is chosen, a share of
/// the draws is a near miss - a value with a letter dropped, a size without its unit, a range the
/// wrong way round, a stray quote - because the reader's refusals are half of what it does.
///
/// What this does not generate, said rather than left to be assumed: text in other scripts than
/// Latin (the alphabet generator does not either), and members longer than a few words. The long
/// query property in ParserPropertyTests covers length.
/// </summary>
internal static class Queries
{
    private static readonly QueryField[] Fields = [.. QueryFields.All];

    private static readonly Gen<string> Letters =
        Gen.String[Gen.OneOfConst('a', 'b', 'c', 'e', 'i', 'l', 'n', 'o', 'p', 'r', 's', 't', 'v', 'w', 'x', '0', '1', '9'), 1, 8];

    /// <summary>Characters that mean something somewhere in the language, one at a time.</summary>
    private static readonly Gen<string> Stray =
        Gen.OneOfConst("\"", "\\", ",", "*", "?", "/", "!", ":", "=", ">", "<", "-", " ", "\t", "\\\\", "\"\"", "//");

    private static readonly Gen<string> Number =
        Gen.OneOf(Gen.Int[0, 70_000].Select(value => value.ToString(CultureInfo.InvariantCulture)), Gen.OneOfConst("0", "4", "99999999999", "-1"));

    /// <summary>Each value of an enumeration in one of the spellings the language promises to take.</summary>
    private static Gen<string> Spelled(string value) =>
        Gen.OneOfConst(value, value.ToUpperInvariant(), Hyphenated(value, '-'), Hyphenated(value, '_'));

    /// <summary>A value with one letter missing - the commonest typo, and the one the hint answers.</summary>
    private static Gen<string> Mistyped(string value) =>
        value.Length < 2 ? Gen.Const(value + "x") : Gen.Int[0, value.Length - 1].Select(at => value.Remove(at, 1));

    private static Gen<string> Enumerated(QueryField field)
    {
        var values = QueryFields.ValuesOf(field.Name);

        if (values.Count == 0)
        {
            return Text;
        }

        return Gen.Frequency(
            (8, Gen.OneOf([.. values.Select(Spelled)])),
            (2, Gen.OneOf([.. values.Select(Mistyped)])),
            (1, Gen.OneOfConst([.. QueryFields.ReservedWords])),
            (1, Stray));
    }

    private static readonly Gen<string> Text = Gen.Frequency(
        (5, Letters),
        (2, Letters.Select(word => "=" + word)),
        (2, Gen.Select(Letters, Gen.OneOfConst("*", "?", "**"), Gen.Int[0, 8], (word, wild, at) => word.Insert(Math.Min(at, word.Length), wild))),
        (2, Gen.Select(Letters, Gen.OneOfConst("^", "", ".*", "[ab]", "a{2,}", "(", "\\d+", "|"), (word, pattern) => "/" + pattern + word + "/")),
        (2, Gen.Select(Letters, Letters, Gen.OneOfConst(" ", ", ", ":", "\\\"", "!"), (left, right, between) => "\"" + left + between + right + "\"")),
        (1, Gen.OneOfConst([.. QueryFields.ReservedWords])),
        (1, Gen.Select(Stray, Letters, (odd, word) => odd + word)),
        (1, Gen.Select(Letters, Stray, (word, odd) => word + odd)));

    private static readonly Gen<string> Compared = Gen.Frequency(
        (4, Number),
        (4, Gen.Select(Gen.OneOfConst(">", "<", ">=", "<=", "=", ">>", "=<"), Number, (sign, value) => sign + value)),
        (2, Gen.Select(Number, Number, (low, high) => low + "-" + high)),
        (1, Gen.OneOfConst([.. QueryFields.ReservedWords])),
        (1, Letters));

    private static readonly Gen<string> Unit = Gen.OneOfConst("B", "KB", "MB", "GB", "TB", "kb", "Mb", "", "PB", "M");

    private static readonly Gen<string> Quantity = Gen.Frequency(
        (5, Gen.Select(Number, Unit, (value, unit) => value + unit)),
        (2, Gen.Select(Gen.Int[0, 99], Gen.OneOfConst(".", ","), Gen.Int[0, 9], Unit, (whole, mark, part, unit) => $"{whole}{mark}{part}{unit}")),
        (1, Number));

    private static readonly Gen<string> Sized = Gen.Frequency(
        (4, Quantity),
        (4, Gen.Select(Gen.OneOfConst(">", "<", ">=", "<=", "="), Quantity, (sign, value) => sign + value)),
        (2, Gen.Select(Quantity, Quantity, (low, high) => low + "-" + high)),
        (1, Gen.OneOfConst([.. QueryFields.ReservedWords])));

    private static Gen<string> ValueOf(QueryField field) => field.Kind switch
    {
        QueryFieldKind.Enumeration => Enumerated(field),
        QueryFieldKind.Number => Compared,
        QueryFieldKind.Size => Sized,
        _ => Text
    };

    /// <summary>A field name as somebody might write it - the name or an alias, in any case.</summary>
    private static Gen<string> Named(QueryField field) =>
        Gen.Select(
            Gen.OneOfConst([field.Name, .. field.Aliases]),
            Gen.Int[0, 2],
            (name, casing) => casing switch { 0 => name, 1 => name.ToUpperInvariant(), _ => char.ToUpperInvariant(name[0]) + name[1..] });

    private static readonly Gen<string> FieldMember =
        from field in Gen.OneOfConst(Fields)
        from name in Named(field)
        from values in ValueOf(field).Array[1, 3]
        from separator in Gen.OneOfConst(":", ":", ":", ": ", "::")
        select name + separator + string.Join(",", values);

    private static readonly Gen<string> Member =
        from negated in Gen.Int[0, 4]
        from body in Gen.Frequency((6, FieldMember), (2, Text), (1, Stray))
        select (negated == 0 ? "!" : string.Empty) + body;

    /// <summary>A whole line: one to four members, mostly separated the way people separate them.</summary>
    internal static readonly Gen<string> Shaped =
        from members in Member.Array[1, 4]
        from separator in Gen.OneOfConst(" ", " ", " ", "  ", "\t")
        select string.Join(separator, members);

    /// <summary>
    /// One member of an enumeration field as a filter chip would write it - a field, one of its
    /// values in canonical spelling, and a side.
    /// </summary>
    internal static readonly Gen<(string Field, string Value, bool Negated)> Chip =
        from field in Gen.OneOfConst([.. Fields.Where(field => QueryFields.ValuesOf(field.Name).Count > 0)])
        from value in Gen.OneOfConst([.. QueryFields.ValuesOf(field.Name)])
        from negated in Gen.Bool
        select (field.Name, value, negated);

    /// <summary>startPending as start-pending or start_pending, the spellings the language folds.</summary>
    private static string Hyphenated(string value, char mark)
    {
        var built = new System.Text.StringBuilder(value.Length + 4);

        foreach (var letter in value)
        {
            if (built.Length > 0 && char.IsUpper(letter))
            {
                built.Append(mark).Append(char.ToLowerInvariant(letter));
            }
            else
            {
                built.Append(letter);
            }
        }

        return built.ToString();
    }
}
