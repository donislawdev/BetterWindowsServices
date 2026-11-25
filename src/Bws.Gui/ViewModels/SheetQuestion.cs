using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// A recovery question on the plan sheet: the form, the entries it writes to, and what the sheet says while the
/// form has asked nothing yet or the plan under it is catching up (backlog 555 part a2, 2026-11-24).
///
/// <b>Every other sheet opens on a plan already worked out</b> - a menu item names the whole question. A
/// recovery is asked by filling a form ON the sheet, so the sheet opens with no plan, and the window puts a plan
/// under the form each time the form settles. Until then the title and the button come from here.
///
/// <b>Its own type rather than more of <see cref="Planned"/></b>, which stands at the ceiling of members a
/// shipped type may have (CodeShapeGuards) - and this is a different thing: the question, where Planned is the
/// answer.
/// </summary>
public sealed class SheetQuestion : Observable
{
    private bool _outdated;
    private readonly string _shownAs;

    internal SheetQuestion(RecoveryForm form, BulkAction asking, string? shownAs)
    {
        Form = form;
        Asking = asking;
        _shownAs = shownAs ?? string.Empty;
    }

    /// <summary>What the person fills in.</summary>
    public RecoveryForm Form { get; }

    /// <summary>The entries the form writes to, as a recovery ask with no change yet.</summary>
    internal BulkAction Asking { get; }

    /// <summary>
    /// Whether the form moved since the plan under it was worked out. The button is grey from the first key to
    /// the plan that matches it: a plan on screen about what the form said a moment ago is a preview that differs
    /// from what would run - the one thing a preview may never do (untouchable rule 5).
    /// </summary>
    public bool Outdated
    {
        get => _outdated;
        internal set => Set(ref _outdated, value);
    }

    /// <summary>The title while there is no plan - the words a plan of this kind would have.</summary>
    internal NamedSentence Heading => Asked == 1
        ? NamedSentence.Around(Texts.Of("gui.plan.heading.one", PlanWords.Doing(Asking.Kind), NamedSentence.Slot), Named)
        : NamedSentence.Unnamed(Texts.Of("gui.plan.heading.many", PlanWords.Doing(Asking.Kind), Asked));

    /// <summary>The manager's own name under a title that used the display name - empty when they are the same.</summary>
    internal string Subtitle => Asked != 1 || string.Equals(Named, Asking.ServiceNames[0], StringComparison.Ordinal)
        ? string.Empty
        : Asking.ServiceNames[0];

    /// <summary>What the button says it would do, grey until there is a plan.</summary>
    internal string CarryOutLabel => Asked == 1
        ? Texts.Of("gui.plan.carryOut.recovery.one", Named)
        : Texts.Of("gui.plan.carryOut.recovery.many", Asked);

    /// <summary>What the notice says while the form has not asked anything.</summary>
    internal string Notice =>
        Form.HasProblem ? Texts.Of("gui.plan.notice.form.fix")
        : Form.Change is null ? Texts.Of("gui.plan.notice.form.ask")
        : Texts.Of("gui.plan.notice.form.working");

    private int Asked => Asking.ServiceNames.Distinct(StringComparer.OrdinalIgnoreCase).Count();

    private string Named => string.IsNullOrWhiteSpace(_shownAs) ? Asking.ServiceNames[0] : _shownAs;

    /// <summary>
    /// What the notice says once a plan stands under the form: how many warnings and refusals there are, the
    /// owner's decision of 2026-11-25 (P2). The form pushes them below the fold on a small window while the
    /// button stays in view, so the line above the form - which never scrolls - says they are there. A count in
    /// brackets rather than a plural word, the way the Polish file already says counts.
    /// </summary>
    internal static string Under(int warnings, int problems)
    {
        string?[] kinds =
        [
            warnings == 0 ? null : Texts.Of("gui.plan.notice.form.warnings", warnings),
            problems == 0 ? null : Texts.Of("gui.plan.notice.form.refusals", problems)
        ];

        var said = string.Join(", ", kinds.OfType<string>());

        return said.Length == 0 ? Texts.Of("gui.plan.notice.notYet") : Texts.Of("gui.plan.notice.form.under", said);
    }
}
