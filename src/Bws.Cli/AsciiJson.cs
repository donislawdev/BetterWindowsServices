using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Bws.Cli;

/// <summary>
/// The one door every JSON document this tool prints goes through, and what it does to them:
/// nothing above U+007E leaves as itself.
///
/// <b>WHY ESCAPES AND NOT UTF-8, AND A MEASUREMENT DECIDED IT RATHER THAN TASTE</b> (stability
/// report C-1, owner's decision 2026-09-30). Until 2026-10-05 the listing and the comparison wrote
/// display names as themselves, and the console turned them into whatever its code page was. On a
/// Polish Windows that is 852, so <c>bws list --json &gt; file</c> wrote the letter a-ogonek as the
/// single byte A5 - a file jq, Python and PowerShell 7 all read as broken UTF-8. Forcing UTF-8 out
/// looked like the cure and was measured as the opposite: PowerShell decodes what a native program
/// prints with the CONSOLE's code page, so UTF-8 bytes on a console set to 852 come back wrong, and
/// <c>bws list --json | ConvertFrom-Json</c> - the most common pipeline there is - would have broken
/// on every default Polish console. Text made only of ASCII reads the same under every code page a
/// Windows console uses, so it is the one choice that leaves both the pipe and the file intact.
///
/// <b>Why after serialising rather than inside it.</b> The two encoders the framework offers both
/// miss: the default one, and every one built with <c>JavaScriptEncoder.Create</c>, also escapes
/// <c>&amp;</c>, the apostrophe, <c>+</c> and the angle brackets - so every English description
/// carrying "isn't" would spell its apostrophe as a six character escape - and the relaxed one leaves letters outside ASCII
/// as they are. An encoder of our own has to override a method that takes a raw pointer, which
/// would be the first unsafe code in this assembly. Measured 2026-10-05 on .NET 10.0.12. So the
/// document is written with the relaxed encoder and then every character above U+007E becomes
/// <c>\uXXXX</c> here.
///
/// <b>Why that is safe, which is the whole argument.</b> A document this serialiser writes holds a
/// character outside ASCII only inside a string - names, punctuation and the indentation are all
/// ASCII. Inside a JSON string an escape and the character it names are the same value (RFC 8259,
/// section 7), and a character outside the basic plane is two escapes, one per surrogate, which is
/// how that section spells it. A parser hands back exactly what the machine said.
///
/// <b>NOT THE SNAPSHOT FILE.</b> That is a file this tool writes itself, in UTF-8, and its kept copy
/// pins display names as themselves (SnapshotJson, SnapshotGoldenTests). It never passes through a
/// console, so none of the above applies to it.
/// </summary>
internal static class AsciiJson
{
    /// <summary>
    /// The encoder every document here is written with before the escaping below.
    ///
    /// Relaxed, so that ASCII punctuation stays readable - <c>&amp;</c> in a display name, an
    /// apostrophe in a description. It still escapes the control characters and the surrogates,
    /// which JSON requires. What it leaves alone that a console could break is exactly what
    /// <see cref="Serialize"/> takes care of.
    /// </summary>
    internal static JavaScriptEncoder Encoder => JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    /// <summary>
    /// A document, written so that every byte of it is ASCII.
    ///
    /// <b>The only place in the command line that calls the serialiser</b>, and a guard in the
    /// architecture tests holds it there: a renderer that serialised on its own would print letters
    /// outside ASCII again, and nothing about its output on an English machine would show it.
    /// </summary>
    internal static string Serialize<T>(T value, JsonSerializerOptions options) =>
        Escaped(JsonSerializer.Serialize(value, options));

    /// <summary>
    /// The last character that leaves as itself - the tilde, U+007E.
    ///
    /// <b>Where exactly between U+007E and U+00A0 this sits changes nothing today, and the mutation
    /// registry found that out:</b> the relaxed encoder escapes DEL, the C1 controls and U+00A0 itself
    /// (measured 2026-10-05), so none of them reaches the loop below as a character. What does reach it
    /// is every letter outside ASCII and the direction overrides, which the encoder leaves alone. The
    /// tilde is kept because it is the one boundary that does not lean on what an encoder escapes.
    /// </summary>
    private const char Last = '~';

    private static string Escaped(string document)
    {
        var first = document.AsSpan().IndexOfAnyExceptInRange('\0', Last);

        // Most documents on an English machine never get past this line, and the copy below is
        // not worth making for them.
        if (first < 0)
        {
            return document;
        }

        var text = new StringBuilder(document.Length + 64);
        text.Append(document, 0, first);

        foreach (var character in document.AsSpan(first))
        {
            // Upper case hex, which is how the serialiser spells the escapes it writes itself - so
            // one document does not carry two spellings of the same thing.
            if (character <= Last)
            {
                text.Append(character);
            }
            else
            {
                text.Append(CultureInfo.InvariantCulture, $"\\u{(int)character:X4}");
            }
        }

        return text.ToString();
    }
}
