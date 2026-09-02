namespace Bws.Core;

/// <summary>
/// What to put in front of a person when the manager's own label is not words.
///
/// <b>Public, and for the same reason <see cref="ServiceDescription"/> beside it is: it is a rule
/// about a string, it needs no service control manager, and it is easy to get wrong in a way
/// nothing else would notice.</b> The reading lives with the enumeration and cannot be asked
/// without a machine - this is the half that can.
///
/// <b>The shape it exists for, measured on this machine 2026-09-02 over 799 entries: TWO display
/// names come back as an unresolved indirection.</b> <c>Tcpip6</c> carries
/// <c>@todo.dll,-100;Microsoft IPv6 Protocol Driver</c> and <c>tcpipreg</c> carries
/// <c>@%SystemRoot%\System32\drivers\tcpipreg.sys,-10110,</c>. Both were shown to a person exactly
/// like that until this type existed, and since the list gained a default order they were the first
/// two rows in the window, because an at sign sorts before every letter.
///
/// <b>THIS IS A DELIBERATE DIVERGENCE FROM THE SYSTEM'S OWN TOOLS AND NOT A DISAGREEMENT WITH
/// THEM.</b> Measured the same day: <c>sc.exe qc</c> and <c>Get-Service</c> both print these two
/// exactly as they are stored. So nothing here is being corrected - a choice is being made that
/// those tools did not make. What makes it ours to make is that both entries are kernel drivers,
/// which <c>services.msc</c> does not list at all, so there is no window anywhere that a person
/// could hold beside ours and find a different answer.
///
/// <b>RESOLVING THE INDIRECTION OURSELVES IS REFUTED BY MEASUREMENT RATHER THAN BY OPINION, AND
/// THAT IS WORTH KEEPING BECAUSE IT IS THE FIRST THING ANYONE WILL SUGGEST.</b>
/// <c>SHLoadIndirectString</c> is the API for exactly this string shape, and on 2026-09-02 it
/// returned <c>E_FAIL</c> for both of them. <c>todo.dll</c> is not on the disk at all. So the cost
/// of that road is one system call per entry on the listing path in exchange for nothing.
///
/// <b>WHAT THIS IS NOT ALLOWED NEAR, AND THE REASON IS THE PRODUCT ITSELF: a snapshot.</b> A
/// snapshot records what the machine said, so that a later comparison can tell whether the machine
/// changed. Storing the substitution instead would mean that the day the resource is fixed and the
/// stored value genuinely changes, both sides would still read the same and the comparison would
/// report nothing. That is rule 8 - a silent loss dressed as a clean result - and it is the one
/// failure this tool exists to prevent. <see cref="Snapshots.EntryDocument"/> therefore keeps
/// writing <see cref="ScmEntry.DisplayName"/> untouched, and the difference between the two is the
/// difference between a label and a record.
///
/// <b>Never a refusal, and that is where it parts from <see cref="ServiceDescription"/>.</b> A
/// description is optional, so "nobody could turn this into words" is a true and usable answer for
/// it. A label is not optional: the list shows it, sorts by it and searches it, so there has to be
/// something. The worst honest answer is the internal name, which is what a great many entries
/// carry as their display name anyway.
/// </summary>
public static class ServiceDisplayName
{
    /// <summary>
    /// The label for an entry, given what the manager handed back and what the entry is called.
    /// </summary>
    /// <param name="displayName">The label as the manager gave it, indirection and all.</param>
    /// <param name="serviceName">The internal name, used when there is nothing better.</param>
    public static string Of(string? displayName, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(serviceName);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return serviceName;
        }

        // STRICTER THAN ServiceDescription NEXT DOOR, AND THE DIFFERENCE IS DELIBERATE RATHER THAN
        // AN OVERSIGHT - it is the whole reason this method is not a copy of that one.
        //
        // That rule matches on the leading at sign alone and argues, correctly for itself, that a
        // stricter pattern would let a malformed indirection through as prose. The costs are not
        // symmetric here. A description that guesses wrong answers "nobody could read this", which
        // invents nothing. A LABEL that guesses wrong is replaced by the internal name, so a
        // display name the machine really holds stops being visible - and in an audit tool, hiding
        // what the machine says is the worse of the two mistakes.
        //
        // FOUND BY A GUARD WRITTEN FOR SOMETHING ELSE ENTIRELY, which is worth recording. The
        // export guard carries "@SUM(A1)" through this field to prove that a value a spreadsheet
        // would evaluate is made inert, and the first version of this rule ate it before the
        // exporter ever saw it. "@SUM(A1)" is not an indirection - it is a payload, and an
        // administrator auditing the service that carries it needs to SEE it.
        if (displayName[0] != '@' || !NamesAResource(displayName))
        {
            return displayName;
        }

        // The documented form is @[path\]file,-id[;comment], and the comment is the half a person
        // was meant to read. Tcpip6 carries "Microsoft IPv6 Protocol Driver" there, which is the
        // right answer and is better than anything this code could invent. The FIRST semicolon
        // ends the indirection - anything after it belongs to the comment, semicolons included.
        var semicolon = displayName.IndexOf(';', StringComparison.Ordinal);

        if (semicolon >= 0)
        {
            var comment = displayName[(semicolon + 1)..].Trim();

            if (comment.Length > 0)
            {
                return comment;
            }
        }

        // tcpipreg ends in a comma with no comment at all, so there is nothing here to show and
        // the internal name is the only true thing left to say.
        return serviceName;
    }

    /// <summary>
    /// Whether this names a resource inside a binary rather than merely starting with an at sign.
    ///
    /// The documented form is <c>@[path\]file,-id[;comment]</c>, so the part that makes it an
    /// indirection is the comma, the minus and a number - not the at sign, which anything may
    /// start with. Written as a scan rather than a pattern because it runs once per row per draw.
    /// </summary>
    private static bool NamesAResource(string text)
    {
        var mark = text.IndexOf(",-", StringComparison.Ordinal);

        return mark >= 0 && mark + 2 < text.Length && char.IsAsciiDigit(text[mark + 2]);
    }
}
