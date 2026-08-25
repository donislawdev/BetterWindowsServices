using Bws.Core;

namespace Bws.Gui.ViewModels;

/// <summary>What a fold left on screen, and how much it took off it.</summary>
internal readonly record struct Rolled
{
    /// <summary>The rows to show, in the order they came.</summary>
    public required List<EntryRow> Rows { get; init; }

    /// <summary>
    /// How many instances went under a template.
    ///
    /// <b>Carried out rather than worked out afterwards, for the reason <see cref="Narrowed"/>
    /// gives about its own two counts.</b> It is the only evidence that the list on screen is
    /// shorter than the answer behind it, and rule 8 forbids handing back a result that looks whole
    /// when it is not - the window owes a sentence for exactly this number.
    /// </summary>
    public required int Instances { get; init; }
}

/// <summary>
/// Which per-user instances stand under which template, and what the row standing for them says.
///
/// <b>`A11`, the half that happens in the window.</b> Windows 10 and later make one copy of a
/// per-user service per logged-on session, with a hex tail on the name, and the specification's
/// complaint about them is that they are "sieczka" - noise that fills a list and answers nothing.
/// Measured on this machine on 2026-08-25 with <c>sc.exe</c> over 798 names: 23 templates, 23
/// instances, one logged-on session.
///
/// <b>THE BITS DECIDE THE ROLE AND THE NAME ONLY LINKS TWO ROWS THE BITS HAVE ALREADY SPOKEN
/// ABOUT, and that order is the whole safety of this file.</b> `CLAUDE.md` records the specimen
/// that punishes the other order: <c>Power</c> is a plain 0x20 share process and
/// <c>Power_a17007</c> a plain 0x10 own process, so a fold that grouped by a hex-looking tail
/// would file the power service away as session noise. Neither of them can reach the branch
/// below, because neither carries a per-user bit - and <see cref="PerUserRole"/> is read from the
/// type bits that come back with every enumerated entry.
///
/// <b>Why the name is used at all, said rather than left as a smell.</b> The manager hands over no
/// field naming an instance's template. The link is the convention Windows itself follows -
/// <c>template_hex</c> - and it was measured before it was relied on: 23 of 23 instances on this
/// machine have a prefix that names a template present in the same listing, with no orphans. An
/// instance whose prefix names nothing stays a row of its own rather than disappearing, which is
/// the (D) case this file is written around.
///
/// Pure, so what a person will see can be checked without opening a window - the same property
/// <see cref="Narrowing"/> and <see cref="ListState"/> are built for.
/// </summary>
internal static class Folding
{
    /// <summary>
    /// Folds the instances of this answer under their templates, and tells every row what it now
    /// stands for.
    ///
    /// <b>AN INSTANCE IS FOLDED ONLY WHEN ITS TEMPLATE IS IN THE SAME ANSWER, and that clause is
    /// what makes this survive the query language rather than fight it.</b> Somebody asking
    /// <c>peruser:instance</c> gets 23 instances and no templates, so there is nothing for them to
    /// go under and all 23 stay on screen - without the clause the window would hide exactly what
    /// was asked for, and hide it under rows the query had rejected.
    ///
    /// <b>Told to every row rather than only to the ones that gained instances.</b> A template that
    /// stood for four of them a keystroke ago and stands for none now has to lose the badge saying
    /// so, and a row nobody has ever folded anything under has to be told that too - this runs on
    /// every keystroke and on every tick that moved something, so the cheap way to be right is to
    /// say it about all of them.
    /// </summary>
    /// <param name="rows">What the query selected, in order.</param>
    /// <param name="showingEveryInstance">
    /// Whether somebody asked to see them all. When they have, nothing folds and every row stands
    /// only for itself - which is what makes the switch honest rather than cosmetic: a row that
    /// still claimed to stand for four others would put four services under one press.
    /// </param>
    internal static Rolled Of(IReadOnlyList<EntryRow> rows, bool showingEveryInstance)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var templates = Templates(rows);
        var under = new Dictionary<string, List<EntryRow>>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<EntryRow>(rows.Count);

        foreach (var row in rows)
        {
            if (showingEveryInstance
                || row.Entry.PerUserRole != PerUserRole.Instance
                || TemplateOf(row.ServiceName) is not { } template
                || !templates.Contains(template))
            {
                kept.Add(row);

                continue;
            }

            if (!under.TryGetValue(template, out var instances))
            {
                under[template] = instances = [];
            }

            instances.Add(row);
        }

        foreach (var row in rows)
        {
            row.StandsAlsoFor(under.TryGetValue(row.ServiceName, out var mine) ? mine : []);
        }

        return new Rolled { Rows = kept, Instances = rows.Count - kept.Count };
    }

    /// <summary>
    /// The names of the templates in this answer.
    ///
    /// <b>Case insensitive, because Windows compares service names that way</b> and keeps whatever
    /// spelling it was given - <c>ADR-14</c>. A fold that matched exactly would leave an instance
    /// on its own the day a machine spells one of the two halves differently, and it would do it
    /// silently.
    /// </summary>
    private static HashSet<string> Templates(IReadOnlyList<EntryRow> rows)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (row.Entry.PerUserRole == PerUserRole.Template)
            {
                names.Add(row.ServiceName);
            }
        }

        return names;
    }

    /// <summary>
    /// The template an instance's name points at, or nothing when it points at no name at all.
    ///
    /// <b>The LAST underscore rather than the first, and that is not a preference.</b> Names in
    /// this family carry underscores of their own - <c>webthreatdefusersvc</c> does not, but
    /// nothing stops one - and the tail Windows appends is the last thing on the name. Cutting at
    /// the first would hand back a prefix that names nothing, which this file treats as an orphan
    /// and leaves on screen, so the mistake would be quiet rather than loud.
    ///
    /// <b>Both halves have to be non-empty.</b> A name that is all tail or all prefix is not the
    /// shape this convention makes, and answering with an empty string would ask
    /// <see cref="Templates"/> about a service that cannot exist.
    ///
    /// <b>What is deliberately NOT checked: whether the tail is hexadecimal.</b> The tail is a
    /// session identifier whose spelling is the manager's business rather than ours, and a check on
    /// its shape would be this window holding an opinion about a format nobody promised us. The bit
    /// already said this row is an instance - that is the fact, and the name only says of what.
    /// </summary>
    private static string? TemplateOf(string name)
    {
        var cut = name.LastIndexOf('_');

        return cut <= 0 || cut == name.Length - 1 ? null : name[..cut];
    }

    /// <summary>
    /// What a row standing for a family says beside its name, and nothing at all when it stands
    /// only for itself.
    ///
    /// <b>The count carries the run state, which is the owner's decision of 2026-08-25 and it
    /// answers a fault the fold would otherwise create.</b> A template is never running - measured
    /// 0 of 23 on this machine, against 8 of its 23 instances - so a folded row's Status column
    /// reads "Stopped" about a family that is working. Leaving it there would repeat, in the most
    /// visible column in the window, exactly the false alarm that <c>peruser:</c> had just taken
    /// out of the query language. The column stays true to the entry, because that is what
    /// <c>sc.exe</c> would say about it, and the badge carries what the column cannot.
    ///
    /// <b>Four sentences rather than one with numbers in it, and the plural is why.</b> Backlog 207
    /// asks for a singular beside every plural, and backlog 225 records that the guard watching for
    /// it reddens on a correct singular built out of a plural template. Written out at the call
    /// rather than picked into a variable, because a key travelling as a variable is invisible to
    /// TextKeyGuards - the note <see cref="Sentences"/> and <see cref="ListState"/> both carry.
    /// </summary>
    internal static string Badge(IReadOnlyList<EntryRow> instances)
    {
        if (instances.Count == 0)
        {
            return string.Empty;
        }

        var running = instances.Count(row => row.Entry.Status == EntryStatus.Running);

        if (instances.Count == 1)
        {
            return running == 1
                ? Texts.Of("gui.rollup.one.running")
                : Texts.Of("gui.rollup.one");
        }

        return running == 0
            ? Texts.Of("gui.rollup.many", instances.Count)
            : Texts.Of("gui.rollup.many.running", instances.Count, running);
    }
}
