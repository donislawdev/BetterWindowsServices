using System.IO;
using System.Text.Json;

namespace Bws.Integration.Tests;

/// <summary>
/// A sentence the command line says, read from the product's own language file.
///
/// <b>Rather than written out in a test, and that is a rule this project already paid for
/// elsewhere.</b> <c>tools/window-journey</c> reads the window's "holding still" sentence from the
/// language file for the same reason: a copy of a sentence drifts from the sentence, and then the
/// guard is about the copy. A test asserting on remembered wording goes green after somebody
/// improves the message and red for a reason that has nothing to do with the product.
///
/// <b>Why a test needs this at all.</b> The command line refuses a file for several different
/// reasons and every refusal looks alike from outside - exit code 2, the path named, nothing on
/// standard output. Telling them apart is the difference between a guard that checks what it is
/// named for and one that passes for any refusal at all, which is what
/// <see cref="SnapshotContractTests.A_snapshot_that_is_not_utf8_is_refused_rather_than_guessed_at"/>
/// turned out to be on 2026-08-12.
///
/// Read off the source tree rather than out of the built assembly, which is how
/// <see cref="CommandLineTool"/> already finds the product.
/// </summary>
internal static class Sentences
{
    private static readonly Lazy<JsonDocument> Language = new(() =>
        JsonDocument.Parse(File.ReadAllText(
            Path.Combine(SourceTree.Root(), "src", "Bws.Cli", "Resources", "cli.en.json"))));

    /// <summary>
    /// The sentence under a key, with its placeholders filled in.
    ///
    /// A missing key throws rather than returning the key, because a guard built on a sentence that
    /// does not exist is worse than no guard - it would assert that the output contains a key name,
    /// which no output ever does, and fail for the wrong reason forever.
    /// </summary>
    internal static string Of(string key, params object?[] values)
    {
        if (!Language.Value.RootElement.TryGetProperty(key, out var text))
        {
            throw new KeyNotFoundException(
                $"The command line's language file has no key '{key}', so a guard built on it would "
                + "be asserting about a sentence the product never says.");
        }

        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            text.GetString() ?? string.Empty,
            values);
    }
}
