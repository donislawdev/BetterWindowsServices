using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bws.Site;

/// <summary>
/// Reading the site's JSON, with two rules that pull against each other held at once.
///
/// <b>An unknown key is an error</b>, because a typo in a key name is otherwise a setting that
/// silently does nothing - <c>"schema"</c> spelled <c>"shema"</c> would leave a page without its
/// structured data and say nothing about it.
///
/// <b>A key beginning with an underscore is a comment.</b> JSON has no comments, and every file
/// in this project explains itself where it is rather than in a document somebody has to find.
/// Trailing commas and <c>//</c> comments would be the other way to get that, and they make the
/// file something only this program can read - <c>jq</c>, an editor and a diff viewer all read
/// plain JSON. So the underscore keys are stripped here, before the deserialiser sees them.
/// </summary>
internal static class Json
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    internal static T Read<T>(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var stripped = Strip(document.RootElement);
        return JsonSerializer.Deserialize<T>(stripped, Options)
            ?? throw new InvalidOperationException($"{path} is empty.");
    }

    private static string Strip(JsonElement element)
    {
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            Write(element, writer);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void Write(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().Where(property => !property.Name.StartsWith('_')))
                {
                    writer.WritePropertyName(property.Name);
                    Write(property.Value, writer);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    Write(item, writer);
                }

                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}
