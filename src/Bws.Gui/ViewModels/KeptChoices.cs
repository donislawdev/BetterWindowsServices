using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The two settings somebody chooses on the settings page and the preferences file keeps - schema 7,
/// `docs/PROJEKT-USTAWIENIA-20261117.md`, owner's decisions D2-D4 and E3-E4 of 2026-11-17.
///
/// <b>THEIR OWN TYPE, AND IT KNOWS NOTHING ELSE IN THE WINDOW - that is the reason it exists.</b> The
/// language is read inside the static initialiser of <see cref="Texts"/>, before the first sentence
/// exists, so whatever reads it may not touch a type whose own start-up could ask Texts for a word -
/// it would meet Texts half built. <c>PreferencesFile.Read</c> is out for that reason and one more: on a
/// broken file it moves the file aside, which has to stay the window's move, made where it can say so.
/// This reads the same bytes through the same reader (<see cref="ReadText"/>) and touches nothing.
///
/// <b>ONE READER OF EACH KEY FOR BOTH ROADS.</b> <see cref="ColumnLayouts.Read"/> takes these two keys
/// through <see cref="LanguageFrom"/> and <see cref="RememberFrom"/>, and so does <see cref="Peek"/> - two
/// readers of one hostile key would drift the first time either was changed. The same goes for the file as
/// a whole: <see cref="FromText"/> refuses exactly what ColumnLayouts.Read refuses, and SettingsFileGuards
/// holds the two side by side, so a language is never taken from a file the window then quarantines.
///
/// <b>AND READ AGAIN ON EVERY WRITE, decision E3.</b> Every write puts this window's whole layout back, so
/// a window opened earlier and closed later would quietly undo a language chosen in another one in
/// between. KeptColumns asks <see cref="Peek"/> before it writes and keeps what is on disk for a choice
/// it did not make.
/// </summary>
internal sealed record KeptChoices(string? Language, bool RememberRecent)
{
    /// <summary>What a file says when it says nothing: follow Windows, and remember searches.</summary>
    internal static KeptChoices Defaults { get; } = new(null, true);

    /// <summary>The key of the language, written only when one is chosen - none means "same as Windows".</summary>
    internal const string LanguageKey = "language";

    /// <summary>The key of the switch over recent searches, written only as false - none means "remember".</summary>
    internal const string RememberKey = "rememberRecentQueries";

    /// <summary>
    /// The largest the preferences file may be before it is treated as not being one - for both roads, and
    /// PreferencesFile says it in its sentence. A megabyte, against a real one of twenty short lines: not a
    /// judgement about how much anybody may keep, but the point past which the thing at that path is
    /// something else.
    /// </summary>
    internal const long Biggest = 1024 * 1024;

    private static readonly JsonDocumentOptions OneValueAKey = new() { AllowDuplicateProperties = false };

    /// <summary>
    /// Word for word the decoder <c>SnapshotFiles.ReadText</c> uses, for the reason written there: bytes that
    /// are not text this build reads are refused rather than guessed at, and a byte order mark is honoured.
    /// </summary>
    private static readonly System.Text.UTF8Encoding StrictText = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// A language code in the shape this window can use, lowered, or nothing.
    ///
    /// <b>Two or three ASCII letters and nothing else, and the shape is the security rather than a tidy
    /// habit.</b> The code becomes part of a resource name and of a file path beside the program
    /// (<c>languages\gui.&lt;code&gt;.json</c>), and the value comes from a file anybody running as this
    /// account can edit - rule 25. A code of letters cannot climb out of a folder. It is also the only
    /// shape the window ever asks for: the language Windows names arrives as a two-letter ISO code.
    /// </summary>
    internal static string? Shaped(string? code) =>
        code is { Length: >= 2 and <= 3 } && code.All(char.IsAsciiLetter) ? code.ToLowerInvariant() : null;

    /// <summary>The language a file keeps, or nothing when the key is missing or not a code.</summary>
    internal static string? LanguageFrom(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var code) ? Shaped(code) : null;

    /// <summary>
    /// Whether recent searches are remembered. Only a real false turns it off - a key holding anything
    /// else is a file somebody edited into a shape this does not know, and the default is what the
    /// window did before the switch existed.
    /// </summary>
    internal static bool RememberFrom(JsonNode? node) =>
        !(node is JsonValue value && value.TryGetValue<bool>(out var remember) && !remember);

    /// <summary>Both choices out of a file's top object - the one reader both roads go through.</summary>
    internal static KeptChoices From(JsonObject root)
    {
        ArgumentNullException.ThrowIfNull(root);

        return new KeptChoices(LanguageFrom(root[LanguageKey]), RememberFrom(root[RememberKey]));
    }

    /// <summary>Writes the language key when there is a choice - between filtersFolded and overviewSeen.</summary>
    internal static void RenderLanguage(JsonObject tree, string? language)
    {
        if (Shaped(language) is { } code)
        {
            tree[LanguageKey] = code;
        }
    }

    /// <summary>Writes the switch only when it is off - between recentQueries and schemaVersion.</summary>
    internal static void RenderRemember(JsonObject tree, bool rememberRecent)
    {
        if (!rememberRecent)
        {
            tree[RememberKey] = false;
        }
    }

    /// <summary>
    /// The two choices in a file, or nothing when the file cannot be read as one this build knows -
    /// missing, too big, not text, not JSON, without a schema, or from a schema this build does not read.
    /// Never throws for any of those, never moves the file, never asks Texts for anything.
    /// </summary>
    internal static KeptChoices? Peek(string path)
    {
        try
        {
            return path.Length > 0 && File.Exists(path) && ReadText(path) is { } text ? FromText(text) : null;
        }
        // NOTHING IS SAID HERE, AND THE PERSON IS TOLD ANYWAY. At startup the window reads the same file
        // through PreferencesFile.Read a moment later and says in the line under the list why it could not
        // (gui.layout.unopened, gui.layout.unreadable) - this road only decides the language, and a file it
        // cannot read means "same as Windows", which is what that sentence goes on to describe. Before a
        // write, a file that cannot be read now leaves this window's own choices standing, and the write
        // that follows reports its own failure (gui.layout.notKept).
        catch (Exception refused) when (refused is IOException or UnauthorizedAccessException or System.Text.DecoderFallbackException)
        {
            return null;
        }
    }

    /// <summary>
    /// The text of the preferences file, or nothing when it holds more than <see cref="Biggest"/> bytes - the
    /// one reader of its bytes for both roads, this one and PreferencesFile.Read.
    ///
    /// <b>BOUNDED BY THE READ ITSELF, not by a length asked beforehand.</b> The file belongs to the account and
    /// can be replaced between a question about its size and the read after it, and a read with no end of its
    /// own would then build one string out of whatever stands there - rule 25. So the bytes are counted as
    /// they arrive and the read stops one chunk past the ceiling, whatever the file said about itself.
    /// </summary>
    /// <exception cref="IOException">The file could not be opened or read.</exception>
    /// <exception cref="UnauthorizedAccessException">This account may not read it.</exception>
    /// <exception cref="System.Text.DecoderFallbackException">The bytes are not text this build reads.</exception>
    internal static string? ReadText(string path)
    {
        using var stream = File.OpenRead(path);
        using var held = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;

        while ((read = stream.Read(chunk)) > 0)
        {
            held.Write(chunk.AsSpan(0, read));

            if (held.Length > Biggest)
            {
                return null;
            }
        }

        held.Position = 0;

        using var reader = new StreamReader(held, StrictText, detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }

    /// <summary>
    /// The two choices in the text of a file, or nothing - the half of <see cref="Peek"/> a test can reach.
    ///
    /// <b>It refuses what ColumnLayouts.Read refuses, a file without its columns included.</b> A file that
    /// gave a language here and was then quarantined by the window's own reader would open the window in a
    /// language taken from a file it goes on to call broken.
    /// </summary>
    internal static KeptChoices? FromText(string content)
    {
        JsonNode? tree;

        try
        {
            tree = JsonNode.Parse(content, documentOptions: OneValueAKey);
        }
        // Not a layout at all - the same answer as above, and the window says so when it reads the file.
        catch (JsonException)
        {
            return null;
        }

        if (tree is not JsonObject root
            || root["schemaVersion"] is not JsonValue stated
            || !stated.TryGetValue<int>(out var version)
            || version > ColumnLayout.CurrentSchemaVersion
            || version < ColumnLayout.OldestSchemaVersionRead
            || root["columns"] is not JsonArray)
        {
            return null;
        }

        return From(root);
    }
}
