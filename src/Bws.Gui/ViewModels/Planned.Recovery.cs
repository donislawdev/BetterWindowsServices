using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The one state of the sheet no other plan has: a recovery question still being written (backlog 555 part a2,
/// 2026-11-24). What the question says is <see cref="SheetQuestion"/>, and this is only how the sheet holds it.
///
/// <b>The form goes once a run begins</b>: the sheet is a record of a run then, and a record may not grow a
/// second question - the argument at <c>MainWindow.TakeTheOffer</c>.
/// </summary>
public sealed partial class Planned
{
    private SheetQuestion? _question;

    /// <summary>The recovery question on the sheet - nothing on every other sheet, and nothing once a run begins.</summary>
    public SheetQuestion? Question => Showing && _run is null && !Busy ? _question : null;

    /// <summary>
    /// Opens the sheet on a form that has asked nothing yet, or takes the plan away from under one whose fields
    /// no longer make a question - nothing ticked, or something typed that does not read.
    /// </summary>
    /// <param name="form">What the person fills in.</param>
    /// <param name="asking">The names the form writes to, as a recovery ask with no change yet.</param>
    /// <param name="shownAs">What a person calls the one entry, when there is one.</param>
    internal bool Ask(RecoveryForm form, BulkAction asking, string? shownAs)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(asking);

        if (Busy)
        {
            return false;
        }

        var question = new SheetQuestion(form, asking, shownAs);

        // The button follows the question's own mark, which the window sets when the form moves.
        question.PropertyChanged += (_, _) =>
        {
            Raise(nameof(CanCarryOut));
            Raise(nameof(CarryOutTip));
        };

        _question = question;
        _shownAs = shownAs ?? string.Empty;
        _because = string.Empty;
        _plan = null;
        _stepsInPlan = 0;
        _commandBlock = CommandBlock.None;
        _warnings = null;
        _wayBack = (null, CommandBlock.None);
        _run = null;
        Typed = string.Empty;
        Underway.End();
        Showing = true;

        RaiseTheSheet();

        return true;
    }
}
