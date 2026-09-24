namespace Bws.Gui.ViewModels;

/// <summary>
/// A sentence about one entry, cut where the entry's name stands, so the view can set the name
/// apart from the words around it - owner's decision, 2026-09-24. The plan panel said "stop" six
/// times on one sheet and the name it was about read exactly like the words beside it.
///
/// <b>THE LANGUAGE FILE STILL DECIDES WHERE THE NAME GOES.</b> The sentence is put together from its
/// key as before, with <see cref="Slot"/> standing where the name belongs, and cut at that mark
/// afterwards - so a translation that puts the name first or last is followed rather than
/// overridden, and the key stays written where TextKeyGuards can see it.
///
/// <b>The name itself is never searched for the mark.</b> It is a service name, which is somebody
/// else's input (`docs/09`), and it goes into the middle part after the cut rather than into the
/// text being cut. Only our own words - the number, the verb, the reason - share a string with the
/// mark, and none of them can hold a character from the private use area.
///
/// <b>THREE PARTS RATHER THAN A LIST OF THEM, and that is a limit said out loud.</b> One name per
/// sentence is what the owner asked for - the title and each step - and three parts can be drawn
/// by three runs in a theme file, which cannot name a type of this assembly (`docs/10` trap 10).
/// A sentence naming several entries - a warning listing what comes along - would need a list and
/// a view that is not a theme file.
/// </summary>
/// <remarks>Public for the reason <see cref="PlanLine"/> is: WPF cannot bind to an internal type.</remarks>
public sealed record NamedSentence(string Before, string Name, string After)
{
    /// <summary>
    /// Where the name stands while the sentence is put together - one character from Unicode's
    /// private use area, which no word in the language file carries.
    ///
    /// <b>Built from its number rather than written as a literal</b>, so the source stays plain
    /// ASCII (PublicSurfaceGuards) and no editor or tool can turn an escape into the character.
    /// The first version was written as an escape, and a tool on the way to the disk did exactly that.
    /// </summary>
    internal static readonly string Slot = char.ConvertFromUtf32(0xE000);

    /// <summary>The whole sentence, as a person reads it and as a test or a copy takes it.</summary>
    public string Text => Before + Name + After;

    /// <summary>
    /// A sentence put together with <see cref="Slot"/> in place of the name, cut there.
    ///
    /// <b>No mark, or more than one, and the sentence comes back whole with nothing set apart</b>
    /// - the name in every place the language file put it, and the words all correct. A
    /// translation that dropped the name, or a template that could not be formatted and came back
    /// untouched, reads as it would have read before this type existed.
    /// </summary>
    internal static NamedSentence Around(string formatted, string name)
    {
        ArgumentNullException.ThrowIfNull(formatted);
        ArgumentNullException.ThrowIfNull(name);

        var at = formatted.IndexOf(Slot, StringComparison.Ordinal);

        if (at < 0)
        {
            return Unnamed(formatted);
        }

        var after = formatted[(at + Slot.Length)..];

        return after.Contains(Slot, StringComparison.Ordinal)
            ? Unnamed(formatted.Replace(Slot, name, StringComparison.Ordinal))
            : new NamedSentence(formatted[..at], name, after);
    }

    /// <summary>A sentence with no name to set apart - the heading over several entries, or none.</summary>
    internal static NamedSentence Unnamed(string text) => new(text, string.Empty, string.Empty);
}
