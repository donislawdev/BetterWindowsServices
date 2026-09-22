using System.Text;

namespace Bws.Site;

/// <summary>
/// The two escapes this generator needs, and no more.
///
/// Everything a person writes goes into the page as HTML on purpose - the fragments ARE HTML.
/// What has to be escaped is the handful of values that land inside an attribute or inside a
/// JSON string: a title with an ampersand in it, a description with a quote. Getting that wrong
/// does not look broken, it silently truncates an attribute, which is why it is one function
/// with one job rather than being done by hand at each call.
/// </summary>
internal static class Html
{
    internal static string Escape(string text) => text
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);

    /// <summary>A JSON string body, for the structured data block. Control characters included, because a stray newline in a description would end the string.</summary>
    internal static string Json(string text)
    {
        var builder = new StringBuilder(text.Length + 8);
        foreach (var character in text)
        {
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                case '<': builder.Append("\\u003c"); break;
                default:
                    if (character < ' ')
                    {
                        builder.Append("\\u").Append(((int)character).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        return builder.ToString();
    }
}
