using System.Text.Json;
using Bws.Core;
using CsCheck;

namespace Bws.Cli.Tests;

/// <summary>
/// Every document this tool prints is ASCII, and a parser reading it gets back what the machine
/// said - for any text an entry can carry.
///
/// AsciiDocumentTests holds this on one hostile name somebody assembled. The text in an entry is
/// written by whoever installed the service - a display name, a description, an account, a launch
/// line - and the registry holds any UTF-16 there, so the property draws from all of it: control
/// characters, the C1 range, direction overrides, letters, characters outside the basic plane, and
/// surrogates standing alone.
///
/// <b>A surrogate standing alone does not come back as itself, and that is measured rather than
/// chosen</b> (backlog 590, 2026-10-16): the serialiser writes it as U+FFFD and says nothing. The
/// property expects exactly that, so a change in the serialiser speaks up here - in either direction.
/// </summary>
public sealed class AsciiJsonPropertyTests
{
    [Fact]
    [Trait("search", "weekly")]
    public void Any_text_an_entry_carries_leaves_as_ascii_and_reads_back_as_the_machine_said_it()
    {
        Texts.Searched(
            texts => Wrong(texts) is null,
            new(5_000) { Print = texts => $"{Wrong(texts)} - in: {string.Join(" | ", texts.Select(CodePoints))}" });
    }

    private static readonly string[] Fields = ["serviceName", "displayName", "description", "account", "binaryPath"];

    /// <summary>What is wrong with the document written for these five texts, or null.</summary>
    private static string? Wrong(string[] texts)
    {
        var entry = Entries.Plain(texts[0], texts[1]) with
        {
            Description = Reading<string>.Present(texts[2]),
            Account = Reading<string>.Present(texts[3]),
            BinaryPath = Reading<string>.Present(texts[4])
        };

        var written = ListingJson.One(entry);

        foreach (var character in written)
        {
            if (character is not (>= ' ' and <= '~' or '\r' or '\n'))
            {
                return $"U+{(int)character:X4} left as itself";
            }
        }

        using var document = JsonDocument.Parse(written);
        var root = document.RootElement;

        // The display name goes out as the label a person is shown, which is the service name when
        // the manager's own label is empty or not words - a decision written beside the line that
        // makes it, in EntryDocument. Every other field goes out as the machine gave it.
        string[] said = [texts[0], ServiceDisplayName.Of(texts[1], texts[0]), texts[2], texts[3], texts[4]];

        for (var index = 0; index < Fields.Length; index++)
        {
            var back = root.GetProperty(Fields[index]).GetString();

            if (back != AsWritten(said[index]))
            {
                return $"{Fields[index]} came back as {(back is null ? "null" : CodePoints(back))}";
            }
        }

        return null;
    }

    private static readonly Gen<char> Character = Gen.Frequency(
        (8, Gen.Char[' ', '~']),
        (2, Gen.Char[(char)0x0000, (char)0x001F]),
        (1, Gen.OneOfConst((char)0x007F, (char)0x0085, (char)0x009F, (char)0x00A0, (char)0x00AD)),
        (3, Gen.OneOfConst((char)0x0105, (char)0x0142, (char)0x00F3, (char)0x03A9, (char)0x4E2D, (char)0xFFFD, (char)0xFEFF)),
        (2, Gen.OneOfConst((char)0x202A, (char)0x202E, (char)0x2066, (char)0x200B, (char)0x2028)),
        (1, Gen.Char[(char)0xD800, (char)0xDFFF]));

    /// <summary>Text of an entry, with whole pairs from outside the basic plane spliced in now and then.</summary>
    private static readonly Gen<string> Text = Gen.Frequency(
        (6, Gen.String[Character, 0, 30]),
        (1, Gen.Select(Gen.String[Character, 0, 10], Gen.Int[0x10000, 0x10FFFF], (text, code) => text + char.ConvertFromUtf32(code))));

    private static readonly Gen<string[]> Texts = Text.Array[5];

    /// <summary>What a parser gets back: the text, with each surrogate standing alone made U+FFFD.</summary>
    private static string AsWritten(string text)
    {
        var written = new System.Text.StringBuilder(text.Length);

        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                written.Append(text, index, 2);
                index++;
            }
            else
            {
                written.Append(char.IsSurrogate(text[index]) ? (char)0xFFFD : text[index]);
            }
        }

        return written.ToString();
    }

    private static string CodePoints(string text) =>
        string.Join(' ', text.Select(character => ((int)character).ToString("X4", System.Globalization.CultureInfo.InvariantCulture)));
}
