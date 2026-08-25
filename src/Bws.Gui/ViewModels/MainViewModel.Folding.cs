namespace Bws.Gui.ViewModels;

/// <summary>
/// Whether the per-user instances are folded under their templates, and what moving that costs.
///
/// <b>A partial for the same reason <see cref="MainViewModel.Scope"/> is one</b> - the rule itself
/// is already a class that knows nothing about a window, <see cref="Folding"/>, and what is here is
/// what has to happen to the REST of the model when the switch moves.
///
/// <b>STATE BESIDE THE QUERY RATHER THAN A MEMBER OF IT, and that is settled ground rather than a
/// fresh choice.</b> <see cref="ScopeChoice"/> carries the argument in full: a filter chip holds
/// nothing because whether it is lit is a question asked of the query text, and that doctrine is
/// right for a filter. This is not one - it does not decide which entries the answer holds, it
/// decides how many rows that same answer is drawn as. The count line still says how many entries
/// matched, and the number of rows under it is smaller.
///
/// <b>Which is exactly why the window owes a sentence for the difference.</b> A list shorter than
/// the number above it, with nothing saying why, is rule 8 broken in the first place a person
/// looks - so <see cref="Sentences.Admissions"/> takes the folded count and names both the number
/// and the way out of it.
///
/// <b>NOT REMEMBERED BETWEEN RUNS, said rather than left to be discovered.</b> The one file this
/// program keeps is a frozen contract, and a new field in it is a schema bump and a conversation
/// rather than an edit - the same sentence the folding filters row already carries for the same
/// reason.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>
    /// Whether every session's copy is on screen, rather than folded under the template it came
    /// from.
    ///
    /// <b>Off when the window opens, which is what `A11` asks for in as many words</b> - "domyslnie
    /// zwijamy je do jednej pozycji-szablonu z licznikiem". Measured on this machine on 2026-08-25:
    /// 23 of 798 entries are session copies, and on a machine with several people logged on it is
    /// that many times over.
    ///
    /// <b>The selection is deliberately NOT let go of when this moves, and the reason is an
    /// invariant rather than an oversight.</b> Whatever a folded row stands for is recomputed on
    /// every pass, and moving this switch is a pass - so by the time anybody can press a button,
    /// every row on screen already stands for exactly what the fold left it standing for. A plan
    /// built from the selection therefore cannot be wider or narrower than the rows a person is
    /// looking at, which is the property the specification's warning about `A11` is really about.
    /// </summary>
    public bool ShowingEveryInstance
    {
        get => _showingEveryInstance;

        set
        {
            if (_showingEveryInstance == value)
            {
                return;
            }

            _showingEveryInstance = value;

            // The whole pass rather than a redraw. The fold happens inside it, and it is also what
            // recomputes the count line, the admission underneath and what every row stands for.
            Apply();

            Raise(nameof(ShowingEveryInstance));
        }
    }
}
