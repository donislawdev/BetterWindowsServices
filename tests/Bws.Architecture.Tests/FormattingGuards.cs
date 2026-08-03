using System.Text.RegularExpressions;

namespace Bws.Architecture.Tests;

/// <summary>
/// Nothing in the product formats a value inside an interpolated string.
///
/// <b>This exists because the analyser that is supposed to cover it cannot see the shape.</b>
/// `CA1305` is switched on in `.editorconfig` as a warning, and warnings are errors here, so
/// every <c>ToString(format)</c> and every <c>string.Format</c> without a provider already fails
/// the build. An interpolated string is compiled through an interpolation handler rather than
/// through <c>string.Format</c>, and the rule does not follow it there - so the one place in this
/// product that formatted a date with the machine's culture built cleanly for as long as it
/// existed.
///
/// <b>What that cost, measured 2026-08-03 rather than imagined.</b> The name a snapshot is given
/// when nobody supplies one carried the date through an interpolated hole. A culture brings a
/// calendar with it, so the same moment produced <c>20260803</c> on a Polish or American machine,
/// <c>25690803</c> on a Thai one, <c>14480220</c> on a Saudi one and <c>14050512</c> on an
/// Iranian one - while the timestamp written INSIDE the file stayed ISO 8601, because the
/// serialiser does not consult the machine. The file's name and the file's contents disagreed,
/// and the files stopped sorting by time.
///
/// <b>The rule is blunt on purpose: no format specifier in an interpolated string at all.</b>
/// A provider cannot be passed inside a hole, so there is no correct way to write one - the
/// answer is always <c>ToString(format, provider)</c> beside it, which this project was already
/// doing everywhere else. A blunt rule needs no judgement about which value is culture-sensitive,
/// and judgement is what let this one through: of the two holes in the product, one could not
/// matter and one mattered a great deal.
///
/// False alarm estimate, as `docs/04` requires: measured over the whole product at the moment
/// this was written, zero - both holes had been rewritten first. A ratchet, like the size ceiling
/// and the comparer guard, rather than a rule that shouts at correct code.
///
/// <b>What it does not see, said rather than left to be discovered.</b> A hole whose expression
/// contains a colon of its own - a conditional written without spaces - would match, and a hole
/// built across a raw string literal spanning lines would not. Neither shape appears here, and
/// both are worth knowing before somebody trusts a green run.
/// </summary>
public sealed class FormattingGuards
{
    /// <summary>
    /// An interpolated hole carrying a format after a colon: <c>{value:yyyy}</c>.
    ///
    /// Deliberately anchored on an identifier before the colon, so that a hole holding a
    /// conditional with spaces around its colon does not match. That is the one shape this
    /// project writes that would otherwise look the same.
    /// </summary>
    private static readonly Regex Formatted = new(
        @"\$@?""[^""]*\{[A-Za-z_][A-Za-z0-9_.\[\]()]*:[^}""]+\}",
        RegexOptions.Compiled,
        Sources.Ceiling);

    [Fact]
    public void No_value_is_formatted_inside_an_interpolated_string()
    {
        var holes = new List<string>();

        foreach (var file in Sources.Shipped())
        {
            var lines = File.ReadAllLines(file);

            for (var number = 0; number < lines.Length; number++)
            {
                if (Formatted.IsMatch(lines[number]))
                {
                    holes.Add($"{Path.GetFileName(file)}:{number + 1}  {lines[number].Trim()}");
                }
            }
        }

        Assert.True(
            holes.Count == 0,
            "A value is formatted inside an interpolated string, which formats with the machine's "
            + "culture and cannot be given a provider. CA1305 does not see this shape - it follows "
            + "ToString and string.Format, and an interpolated string goes through neither. The "
            + "one place that did this named a snapshot 25690803 on a Thai machine and 14480220 on "
            + "a Saudi one, for the same moment, while the date inside the file stayed ISO 8601. "
            + "Use ToString(format, CultureInfo.InvariantCulture) beside it instead:\n"
            + string.Join("\n", holes));
    }
}
