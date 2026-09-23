namespace Bws.Gui.ViewModels;

/// <summary>
/// The search box's wrong state - UX-GUI-002 of the audit of 2026-09-23.
///
/// <b>The box carried no mark while its query had a mistake in it.</b> The sentence about the
/// mistake stood at the foot of the window, about 830 pixels below the box, and the chips beside
/// the box still showed the query before it. The sentence now stands under the box, and the box
/// wears the red edge the box of seconds on the plan sheet already wears.
///
/// <b>Through <see cref="Checked"/>, the framework's own mechanism, for the reasons that class
/// gives</b>: the binding on the box reads <see cref="System.ComponentModel.INotifyDataErrorInfo"/>
/// by itself and sets <c>Validation.HasError</c>, which the style can trigger on without naming a
/// type of this assembly - `docs/10` trap 10. The rule stays where it was: <see cref="Says"/> holds
/// the sentence, and this only hands it to the binding.
///
/// <b>A file of its own because MainViewModel.cs stands at the size ceiling</b>, and this is a
/// subject of its own - what the box is told - rather than a part of reading and narrowing.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Whether the query in the box has a mistake in it.</summary>
    public override bool HasErrors => Says.QueryProblem.Length > 0;

    /// <summary>The sentence under the box, handed to the box's binding - nothing for any other property.</summary>
    protected override IEnumerable<string> ErrorsOf(string property) =>
        property == nameof(QueryText) && Says.QueryProblem.Length > 0 ? [Says.QueryProblem] : [];

    /// <summary>
    /// Says what is wrong with the query, and tells the box when that changed.
    ///
    /// <b>Only when it changed</b>, because this runs on every tick as well as every keystroke, and
    /// a binding told once a second that its errors changed takes the error down and puts it back.
    /// </summary>
    private void AboutTheQuery(string problem)
    {
        if (Says.AboutTheQuery(_queryText, problem))
        {
            RaiseErrors(nameof(QueryText));
        }
    }
}
