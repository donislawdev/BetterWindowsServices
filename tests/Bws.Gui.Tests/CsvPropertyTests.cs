using System.Text;
using Bws.Gui.ViewModels;
using CsCheck;

namespace Bws.Gui.Tests;

/// <summary>
/// The files the window exports for a spreadsheet, read back - step 3b of the fuzzing project, point
/// C6 (docs/PROJEKT-FUZZING-20261016.md, outside the repository).
///
/// <b>Through Exporting.Line, which both exports call</b> - the list and the comparison share it
/// rather than carry copies, so one property over it holds both files.
///
/// <b>Two promises in one comparison.</b> Every value comes back as itself, through the separator,
/// quotes and line breaks it may hold. And a value a spreadsheet would act on - one starting with any
/// of <see cref="Acted"/> - comes back with one apostrophe in front and nothing else changed, because
/// the values in these files are written by whoever installed a service (Exporting.Inert has the
/// argument and the owner's decision of 2026-08-26 that the apostrophe is worth the changed value).
/// </summary>
public sealed class CsvPropertyTests
{
    /// <summary>
    /// What a spreadsheet acts on at the start of a cell, written here again ON PURPOSE rather than
    /// read from the product: a character dropped from the product's own list has to turn this red,
    /// not agree with it.
    /// </summary>
    private static readonly char[] Acted = ['=', '+', '-', '@', '\t', '\r'];

    private static readonly Gen<string> Piece = Gen.Frequency(
        (5, Gen.Char[' ', '~'].Select(character => character.ToString())),
        (3, Gen.OneOfConst(",", ";", "\"", "\"\"", "\r", "\n", "\r\n", "=", "+", "-", "@", "\t", "'", ((char)0x060C).ToString(), ((char)0x061B).ToString())),
        (1, Gen.Char[(char)0x00A0, (char)0xFFFD].Select(character => character.ToString())));

    private static readonly Gen<string> Value = Piece.Array[0, 12].Select(string.Concat);

    /// <summary>
    /// The four every culture on this machine uses (measured 2026-10-17: ";" in 604 of them, "," in
    /// 280, and two Arabic ones), and any other single character Exporting.Separator could hand on - it
    /// refuses a quote and a line break. A separator of more than one character, which Region settings
    /// might let somebody type, is NOT drawn and NOT MEASURED.
    /// </summary>
    private static readonly Gen<string> Separator = Gen.Frequency(
        (6, Gen.OneOfConst(",", ";", ((char)0x060C).ToString(), ((char)0x061B).ToString())),
        (1, Gen.Char[' ', '~'].Where(character => character != '"').Select(character => character.ToString())));

    [Fact]
    [Trait("search", "weekly")]
    public void Every_value_exported_reads_back_as_itself_and_none_starts_a_formula()
    {
        Gen.Select(Separator, Value.Array[1, 8].Array[1, 5])
            .Searched(
                drawn =>
                {
                    var (separator, records) = drawn;
                    var text = new StringBuilder();

                    foreach (var record in records)
                    {
                        Exporting.Line(text, record, separator);
                    }

                    var wrong = Wrong(records, CsvText.Read(text.ToString(), separator));

                    return wrong is null ? true : throw new InvalidOperationException(wrong);
                },
                new(10_000) { Print = drawn => $"separator <{drawn.Item1}>, {drawn.Item2.Length} line(s): " + string.Join(" | ", drawn.Item2.Select(record => string.Join(" / ", record))) });
    }

    /// <summary>The first field that did not come back as it should, or nothing.</summary>
    private static string? Wrong(string[][] written, List<List<string>> read)
    {
        if (read.Count != written.Length)
        {
            return $"{written.Length} line(s) written, {read.Count} read back.";
        }

        var fields = written.Zip(read).SelectMany(line => line.First.Length == line.Second.Count
            ? line.First.Zip(line.Second)
            : new[] { ("<" + line.First.Length + " values>", "<" + line.Second.Count + " fields>") });

        return fields
            .Where(field => field.Second != Expected(field.First))
            .Select(field => $"<{field.First}> came back as <{field.Second}>.")
            .FirstOrDefault();
    }

    private static string Expected(string value) =>
        value.Length > 0 && Acted.Contains(value[0]) ? "'" + value : value;
}
