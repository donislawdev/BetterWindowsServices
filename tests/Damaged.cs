using CsCheck;

// Linked into every test project that damages a stored file, for the reason Searching.cs gives: a
// shared test assembly would be a new reference edge, and a third copy of these lines would be the
// one that drifts. Nothing from System.IO or System.Globalization is needed here, so the different
// implicit using sets of those projects agree about this file.
namespace Bws.Tests;

/// <summary>
/// How a stored file is damaged by the random properties that read one back - the snapshot, the
/// window's layout file, the hand-over between two windows, a rendered event.
///
/// <b>Damage to a real document rather than text from nothing.</b> Random text fails at the first
/// character and proves only that the first character is checked - the inputs worth trying are the
/// ones that look almost right. So every property here renders a real document first and damages it.
///
/// <b>Five ways, and each is something that happens to real files</b> rather than something
/// somebody would invent: it stops early (a full disk), a piece falls out, a character flips, something
/// is inserted, or a piece is written twice (a merge that kept both sides, a copy pasted once too
/// often). The fifth arrived on 2026-10-17 with the layout file's property, because it is the one
/// way that repeats a key - and a repeated key was measured that day to throw from JsonNode as an
/// ArgumentException rather than as the JsonException a reader catches.
/// </summary>
internal static class Damaged
{
    /// <summary>Somewhere in <paramref name="original"/>, by up to forty characters, in any of the five ways.</summary>
    internal static Gen<Damage> In(string original) =>
        Gen.Select(Gen.Int[0, original.Length - 1], Gen.Int[0, 40], Gen.Int[0, Damage.Kinds - 1])
            .Select(drawn => new Damage(drawn.Item1, drawn.Item2, drawn.Item3));
}

/// <summary>
/// One damage, as a value a failure can print and a property can replay.
/// </summary>
/// <param name="At">Where in the text it starts.</param>
/// <param name="Length">How many characters it reaches over, where the kind reaches over any.</param>
/// <param name="Kind">Which of the <see cref="Kinds"/> ways.</param>
internal sealed record Damage(int At, int Length, int Kind)
{
    /// <summary>How many ways there are. A property draws a kind below this.</summary>
    internal const int Kinds = 5;

    /// <summary>
    /// The text with this damage done to it.
    /// </summary>
    /// <param name="inserted">
    /// What the fourth kind inserts - the character that closes something in the format at hand, a
    /// brace for JSON and an angle bracket for XML, because a run of those is what turns a document
    /// that still parses into one that ends somewhere it should not.
    /// </param>
    internal string To(string text, char inserted)
    {
        var end = Math.Min(text.Length, At + Length);

        return Kind switch
        {
            0 => text[..At],
            1 => text[..At] + text[end..],
            2 => text[..At] + (char)0xFFFD + text[Math.Min(text.Length, At + 1)..],
            3 => text[..At] + new string(inserted, Math.Max(1, Length)) + text[At..],
            _ => text[..end] + text[At..]
        };
    }

    /// <summary>What a failure prints - where, by how much, and which way.</summary>
    public override string ToString() => $"at {At} by {Length} ({Name})";

    private string Name => Kind switch
    {
        0 => "stops early",
        1 => "a piece falls out",
        2 => "a character flips",
        3 => "something is inserted",
        _ => "a piece is written twice"
    };
}
