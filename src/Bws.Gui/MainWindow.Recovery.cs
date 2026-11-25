using Bws.Core.Planning;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The recovery form: the sheet opened on a question still being written, and a plan put under it each time
/// the form settles (backlog 555 part a2, owner's decision of 2026-11-24).
///
/// <b>Its own file because it is the one sheet the window keeps asking about after it opened.</b> Every other
/// plan is asked once, from a menu, through <see cref="Preview"/>. This one is asked again on every edit of the
/// form, and the waiting, the ticket and the dropping of late answers belong together.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// How long the form has to stay still before a plan is worked out for it. Long enough that typing
    /// "1d" does not build a plan for "1", short enough that the plan appears while somebody is still
    /// looking at the field - an estimate, not a measurement.
    /// </summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Opens the sheet on the form, over the picked rows - the same doors <see cref="Preview"/> keeps: not
    /// during a run, not over nothing, not over drivers alone.
    ///
    /// <b>The form starts from what the listing read</b>, which is on the rows already, so opening it asks
    /// the manager nothing. The plan it leads to reads each entry's whole recovery again before it is
    /// built, and that is what it compares and writes against - the form only shows a starting point.
    /// </summary>
    internal bool AskRecovery()
    {
        if (_model.Planned.Busy)
        {
            _model.Says.CouldNotDo(Texts.Of("gui.plan.blocked.running"));

            return false;
        }

        var picked = PickedRows();
        var names = Picking.Names(picked);

        if (names.Count == 0)
        {
            return false;
        }

        if (Picking.OnlyDrivers(picked))
        {
            _model.Says.CouldNotDo(Texts.Of("gui.action.onlyDrivers"));

            return false;
        }

        _model.Chosen.Hide();

        var form = RecoveryForm.Of([.. picked.SelectMany(row => row.Instances.Prepend(row)).Select(row => row.Entry.Recovery)]);
        var asking = new BulkAction(ActionKind.SetRecovery, names);
        var label = picked.Count == 1 ? picked[0].DisplayName : null;

        if (!_model.Planned.Ask(form, asking, label))
        {
            return false;
        }

        ++_previews;
        form.Edited += async (_, _) => await Replan(form, asking, label).ConfigureAwait(true);

        // A restart as administrator asks again what the sheet asked, and a form is not a question it
        // can carry yet - so it opens the list, and the form is filled in again there.
        _asked = null;

        PlanPanel.TakeTheKeyboard();

        return true;
    }

    /// <summary>
    /// Works out the plan for what the form says now, once it has stayed still for <see cref="Settle"/>.
    ///
    /// <b>The same ticket every preview takes</b>, so an answer that arrives after a later edit is dropped
    /// rather than shown - a plan for "1" under a field reading "1d" is the preview and the run parting.
    /// And the button is grey from the edit to the plan that matches it (<see cref="SheetQuestion.Outdated"/>).
    /// </summary>
    private async Task Replan(RecoveryForm form, BulkAction asking, string? label)
    {
        var ticket = ++_previews;

        if (form.Change is not { } change)
        {
            _model.Planned.Ask(form, asking, label);

            return;
        }

        if (_model.Planned.Question is { } question)
        {
            question.Outdated = true;
        }

        await Task.Delay(Settle).ConfigureAwait(true);

        if (ticket != _previews || _model.Planned.Question?.Form != form)
        {
            return;
        }

        var plan = await _model.PlanAsync(asking with { Recovery = change }).ConfigureAwait(true);

        if (ticket != _previews || _model.Planned.Question?.Form != form)
        {
            return;
        }

        if (!_model.Planned.Show(plan, label))
        {
            _model.Planned.Ask(form, asking, label);
        }
    }
}
