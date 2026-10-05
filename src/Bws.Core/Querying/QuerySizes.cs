using System.Globalization;

namespace Bws.Core.Querying;

/// <summary>
/// Reads a quantity of bytes written with a unit.
///
/// The unit is required, and that is the whole design. <c>memory:&gt;500</c> read as bytes
/// would match every running service on the machine while looking exactly like a filter
/// that worked, and read as megabytes it would be this code guessing at what somebody meant.
/// Refusing it costs one retry and a message naming the forms that work.
///
/// A file of its own since 2026-10-05, when the reserved words and the one-sided bounds grew
/// QueryValues.cs past the size the ratchet calls near its ceiling. This is the one type there that
/// compares nothing - it reads text - so it is the one that leaves.
/// </summary>
internal static class QuerySizes
{
    /// <summary>
    /// Powers of 1024, because that is what Windows means when it writes MB. Task Manager,
    /// the file properties dialog and <c>Get-Process</c> all divide by 1024, so matching the
    /// disk-drive meaning of the word would put us at odds with everything a person could
    /// check us against.
    /// </summary>
    private static readonly (string Suffix, long Multiplier)[] Units =
    [
        ("KB", 1024L),
        ("MB", 1024L * 1024),
        ("GB", 1024L * 1024 * 1024),
        ("TB", 1024L * 1024 * 1024 * 1024),

        // Last, so that the two-letter suffixes are tried first - otherwise every one of
        // them would match here on its final character and be read as a count of bytes.
        ("B", 1L)
    ];

    internal static bool TryRead(string text, out long bytes)
    {
        bytes = 0;

        foreach (var (suffix, multiplier) in Units)
        {
            if (!text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var number = text[..^suffix.Length];

            if (!long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity))
            {
                return false;
            }

            // A quantity large enough to overflow is a mistake worth reporting rather than
            // silently wrapping into a small number that matches everything.
            if (quantity > long.MaxValue / multiplier)
            {
                return false;
            }

            bytes = quantity * multiplier;
            return true;
        }

        return false;
    }
}
