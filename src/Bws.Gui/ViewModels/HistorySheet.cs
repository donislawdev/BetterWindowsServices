using Bws.Core.History;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The change history of the whole machine, in place of the list - J2, owner's decisions J2-a and J2-b
/// of 2026-10-20 (`docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md` section 10). What HistoryView binds to.
///
/// <b>ONE SHEET FOR AS LONG AS THE VIEW IS UP, filled again by every reading</b> rather than a new sheet
/// per reading - the table under it keeps its collection, its sort and its two ticks across F5, GUI rule
/// 3 of the project notes. The comparison's sheet is made once because a comparison is never read again,
/// and this one is.
///
/// <b>FOUR STATES AND THE SENTENCE IS WHAT TELLS THEM APART.</b> Reading for the first time - the
/// sentence says so and there is no table. Failed - the sentence is Windows' reason and the way out, and
/// there is no table, because rows from an earlier reading under a failure would read as the answer.
/// Read and empty - no sentence here, because the sentences above the table already say the log holds
/// nothing (`docs/11` 3.8: a sentence, not an empty rectangle). Read with changes - the table.
/// </summary>
public sealed class HistorySheet : Observable
{
    private IReadOnlyList<string> _admissions = [];
    private string _said = Texts.Of("gui.history.reading");

    internal HistorySheet()
    {
        Filter = new HistoryFilter([]);
    }

    /// <summary>The lines the table shows and the choices over it.</summary>
    public HistoryFilter Filter { get; }

    /// <summary>
    /// What the System log can speak for, above the table: how far back it reaches, whether it was
    /// cleared or is losing its oldest events, what the quotes and the words of Windows mean. No choice
    /// under "Show" hides these - they are not rows.
    /// </summary>
    public IReadOnlyList<string> Admissions
    {
        get => _admissions;
        private set
        {
            if (Set(ref _admissions, value))
            {
                Raise(nameof(Exportable));
            }
        }
    }

    /// <summary>
    /// Whether there is a history to write to a file (AS-c) - once a reading has come back, an empty log
    /// too, because "nothing changed since the log began" is an answer somebody may need on paper. Not
    /// while the first reading is out, and not after a failure: the sentences are what a reading has to
    /// say about itself, and with no reading there are none.
    /// </summary>
    public bool Exportable => Admissions.Count > 0;

    /// <summary>The sentence in place of the table - reading, or why the log could not be read - or nothing.</summary>
    public string Said
    {
        get => _said;
        private set => Set(ref _said, value);
    }

    /// <summary>A reading has started. Says so only while there is nothing on screen to keep.</summary>
    internal void Reading()
    {
        if (Admissions.Count == 0)
        {
            Said = Texts.Of("gui.history.reading");
        }
    }

    /// <summary>A reading came back.</summary>
    internal void Read(ChangeHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);

        Filter.Renew(history.Changes);
        Admissions = HistoryWords.Admissions(history, history.Changes, oneEntry: false);
        Said = string.Empty;
    }

    /// <summary>A reading failed - the sentence says why, and nothing from an earlier reading stays.</summary>
    internal void Failed(string why)
    {
        Filter.Renew([]);
        Admissions = [];
        Said = why;
    }
}
