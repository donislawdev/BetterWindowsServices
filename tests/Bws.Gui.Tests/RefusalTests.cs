using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// Something the window tried on somebody's behalf and could not do.
///
/// <b>Rule 8 arriving where it is easy to think it does not apply.</b> The silence that rule
/// forbids is usually about a reading that skipped an entry - but a copy that quietly did
/// nothing is the same failure with a worse ending, because the person then pastes whatever they
/// copied before into a command that stops a service.
///
/// The clipboard is the live example and it is not hypothetical: it belongs to whichever process
/// grabbed it last, so a copy genuinely fails on a working machine.
/// </summary>
public sealed class RefusalTests
{
    [Fact]
    public async Task A_refusal_is_shown_rather_than_swallowed()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        await model.LoadAsync();

        Assert.Equal(string.Empty, model.Says.Problem);

        model.Says.CouldNotDo("The clipboard is in use.");

        Assert.Contains("clipboard", model.Says.Problem, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AND IT OUTLIVES A TICK, which is the whole reason it does not simply share a field with
    /// the query's own complaint.
    ///
    /// Every line under the list is rewritten whenever anything on the machine moves. On a
    /// working machine that is every few seconds, so a message written into the same field would
    /// be gone before it was read - present, correct and useless, which is a shape this product
    /// has shipped before.
    /// </summary>
    [Fact]
    public async Task A_refusal_survives_the_list_refreshing_underneath_it()
    {
        var machine = new LiveMachine(Rows.Entry("Spooler"), Rows.Entry("BITS"));
        var model = new MainViewModel(machine, new SteppedClock());

        await model.LoadAsync();

        model.Says.CouldNotDo("The clipboard is in use.");

        machine.Stop("BITS");

        await model.RefreshAsync();

        Assert.Contains("clipboard", model.Says.Problem, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// It goes away when the person asks for something else, which is the only signal available
    /// that they have moved on. A refusal left standing would end up describing a list it no
    /// longer refers to.
    /// </summary>
    [Fact]
    public async Task Asking_for_something_else_puts_the_refusal_away()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        await model.LoadAsync();

        model.Says.CouldNotDo("The clipboard is in use.");
        model.QueryText = "spool";

        Assert.Equal(string.Empty, model.Says.Problem);
    }

    /// <summary>
    /// A refusal and a query's complaint are both said while both are true, each in its own place.
    ///
    /// <b>Until 2026-09-23 this test said the refusal WON</b> - one line at the foot of the window,
    /// two things wanting it, and the query's complaint went unsaid while a copy's refusal stood.
    /// UX-GUI-002 of the audit that day moved the complaint under the box it is about, so there is no
    /// tie left to break: the refusal stays at the foot of the window and the complaint stands under
    /// the box, and neither hides the other.
    /// </summary>
    [Fact]
    public async Task A_refusal_and_a_query_that_will_not_parse_are_both_said()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock());

        await model.LoadAsync();

        model.QueryText = "start:nonsense";

        var aboutTheQuery = model.Says.QueryProblem;

        Assert.NotEqual(string.Empty, aboutTheQuery);
        Assert.Equal(string.Empty, model.Says.Problem);

        model.Says.CouldNotDo("The clipboard is in use.");

        Assert.Contains("clipboard", model.Says.Problem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(aboutTheQuery, model.Says.QueryProblem);
    }
}
