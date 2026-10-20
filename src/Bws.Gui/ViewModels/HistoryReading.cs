using System.ComponentModel;
using System.IO;
using System.Xml;
using Bws.Core.History;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The history of one entry, as a section of the panel about it - what the panel asks of the change
/// history, and all it may ask.
/// </summary>
internal interface IEntryHistory
{
    /// <summary>The section for the entry with this internal name, from the last reading.</summary>
    DetailSection SectionFor(string serviceName);

    /// <summary>The panel is about to show a section - read the log, unless it has been read already.</summary>
    void Need();

    /// <summary>A reading came back, so a section shown from the one before is out of date.</summary>
    event EventHandler? Changed;
}

/// <summary>
/// Reading the change history from the window - J2, owner's decisions J2-a to J2-d of 2026-10-20
/// (`docs/PROJEKT-DRUGA-POLOWA-AUDYTU-20261014.md` section 10). The view of the whole machine in the
/// middle of the window, and the section of the panel about one entry, both from one reading.
///
/// <b>The work and the state, with no window in it</b> - GUI rule 15 of the project notes, the
/// arrangement <see cref="Snapshotting"/> has.
///
/// <b>ONE READING OF THE WHOLE LOG, KEPT, AND FILTERED PER ENTRY.</b> The panel never reads the log for
/// one entry: it takes the last reading and asks it for that name (<see cref="ChangeHistory.Of"/>). So
/// moving the panel from entry to entry costs nothing, and there is no race in which the answer for the
/// last entry lands under the next one.
///
/// <b>READ WHEN FIRST NEEDED, AND AGAIN ON F5 AND ON EVERY OPENING OF THE VIEW.</b> Not at start: a
/// window opened to stop a service has no business reading the System log. Measured 2026-10-20 through
/// <c>bws history --timing</c>: 57-91 ms over 328 changes (runs two to six of six), so the reading is
/// cheap enough to repeat on every opening, and a log on a busy server is NOT MEASURED - which is why it
/// runs off the window's thread and says it is reading while it does.
///
/// <b>ON ONE THREAD FROM START TO FINISH</b>: a query handle of the event log may only be used on the
/// thread that made it (F21, the documentation of EvtQuery), so the whole of
/// <see cref="IChangeHistoryReader.Read"/> runs inside one call on one pool thread, and nothing comes back
/// until it has finished.
///
/// <b>ONE READING AT A TIME.</b> A second ask while one is out waits for that one, rather than reading the
/// log twice for one question - the rule Snapshotting.Busy keeps for the same reason.
/// </summary>
public sealed class HistoryReading : Observable, IEntryHistory
{
    private readonly IChangeHistoryReader? _reader;
    private ChangeHistory? _latest;
    private string _failure = string.Empty;
    private bool _asked;
    private HistorySheet? _shown;

    /// <summary>
    /// The last section made, for which reading and which entry. The panel builds itself again every time
    /// the row it follows moves - once a second while a service is changing state - and the section only
    /// changes with a new reading or another entry. Since a review of 2026-10-20 (#81).
    /// </summary>
    private (ChangeHistory History, string Name, DetailSection Section)? _made;

    /// <param name="reader">The log, or nothing for a model built without one - a test's state, which says so.</param>
    internal HistoryReading(IChangeHistoryReader? reader)
    {
        _reader = reader;
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>The view of the whole machine in the middle of the window, or nothing while the list is there.</summary>
    public HistorySheet? Shown
    {
        get => _shown;
        private set => Set(ref _shown, value);
    }

    /// <summary>
    /// The reading out right now, or a finished task. Kept rather than walked away from - the tests and
    /// the catalogue await it - and it does not fault: every failure is a sentence (<see cref="ReadAsync"/>).
    /// </summary>
    internal Task Out { get; private set; } = Task.CompletedTask;

    /// <summary>Whether the view of the whole machine stands where the list was.</summary>
    internal bool InTheMiddle => Shown is not null;

    /// <summary>
    /// Puts the view of the whole machine in the middle of the window and reads the log again, so what it
    /// shows is the log as it is now rather than as it was when somebody last looked.
    /// </summary>
    internal Task Open()
    {
        Shown ??= Filled(new HistorySheet());

        return Again();
    }

    /// <summary>F5: the log again, but only once somebody has asked for it - a refresh is not an ask.</summary>
    internal Task Fresh() => _asked ? Again() : Task.CompletedTask;

    /// <inheritdoc />
    public void Need()
    {
        if (!_asked)
        {
            // Not awaited here and not thrown away either: the reading is kept in Out, and when it comes
            // back Changed tells the panel to build its section again.
            _ = Again();
        }
    }

    /// <summary>Takes the view out of the middle of the window - true when there was one, for Escape.</summary>
    internal bool PutAway()
    {
        if (Shown is null)
        {
            return false;
        }

        Shown = null;

        return true;
    }

    /// <inheritdoc />
    public DetailSection SectionFor(string serviceName)
    {
        if (_latest is not { } history)
        {
            return DetailSection.Written(Heading, [], _failure.Length > 0 ? _failure : Texts.Of("gui.history.reading"));
        }

        // The same reading and the same entry give the same section - compared by reference, because a new
        // reading is a new ChangeHistory, and by name the way ChangeHistory.Of compares it.
        if (_made is { } made && ReferenceEquals(made.History, history) && string.Equals(made.Name, serviceName, StringComparison.OrdinalIgnoreCase))
        {
            return made.Section;
        }

        var changes = history.Of(serviceName);
        var lines = ChangeGroup.Of(changes).Select(Line).ToList();
        var section = DetailSection.Written(Heading, lines, string.Join(" ", HistoryWords.Admissions(history, changes, oneEntry: true)));

        _made = (history, serviceName, section);

        return section;
    }

    /// <summary>The panel's heading for the section, a key like every other section's.</summary>
    private const string Heading = "gui.details.section.history";

    /// <summary>
    /// One line of the section: the last time as the label, the change and who made it as the value -
    /// and for a group, how many times since when (J2-c).
    /// </summary>
    private static DetailLine Line(ChangeGroup group) => DetailLine.Written(
        HistoryWords.When(group.Last ?? group.Latest.At),
        group.Times == 1
            ? Texts.Of("gui.history.line.one", HistoryWords.What(group.Latest), HistoryWords.Who(group.Latest.By))
            : Texts.Of("gui.history.line.many", HistoryWords.What(group.Latest), HistoryWords.Who(group.Latest.By), group.Times, HistoryWords.When(group.First)));

    private Task Again()
    {
        _asked = true;

        if (Out.IsCompleted)
        {
            Out = ReadAsync();
        }

        return Out;
    }

    /// <summary>
    /// Reads the log off the window's thread and hands the answer to the view and the panel - or the
    /// reason it could not, as a sentence with the way out (`docs/11` section 5). Nothing from an earlier
    /// reading survives a failure, because an old answer under a new failure would read as current.
    ///
    /// <b>The four failures the reading has, named rather than caught all at once</b>: Windows refusing or
    /// failing (Win32Exception - the log service stopped, access refused), an event too large to render or
    /// a log that does not add up (InvalidDataException), an event that is not well formed XML
    /// (XmlException), and a refusal reported as one (UnauthorizedAccessException). Anything else is a
    /// fault nobody foresaw, and it goes where those go - Mishaps - rather than into a sentence that
    /// would pretend to know what happened.
    /// </summary>
    private async Task ReadAsync()
    {
        // A NEW READING IS NOT THE LAST ONE'S FAILURE - since a review of 2026-10-20 (#81). Kept, the panel
        // went on saying the log could not be read while the view beside it said it was reading. Cleared
        // here, and the panel is told, so it says "reading" - or keeps showing a good earlier reading,
        // which stays in _latest until the new answer replaces it.
        _failure = string.Empty;
        Shown?.Reading();
        Changed?.Invoke(this, EventArgs.Empty);

        try
        {
            _latest = _reader is { } reader
                ? await Task.Run(reader.Read).ConfigureAwait(true)
                : throw new InvalidDataException(Texts.Of("gui.history.noReader"));
        }
        catch (Win32Exception failure)
        {
            Failed(failure.Message);
        }
        catch (InvalidDataException failure)
        {
            Failed(failure.Message);
        }
        catch (XmlException failure)
        {
            Failed(failure.Message);
        }
        catch (UnauthorizedAccessException failure)
        {
            Failed(failure.Message);
        }

        if (Shown is { } sheet)
        {
            Filled(sheet);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A failed reading: no history, and the sentence saying why with the way out.</summary>
    private void Failed(string why)
    {
        _latest = null;
        _failure = Texts.Of("gui.history.readFailed", why);
    }

    /// <summary>What the last reading says, put on the sheet - a reading that has not come back yet leaves it saying so.</summary>
    private HistorySheet Filled(HistorySheet sheet)
    {
        if (_latest is { } history)
        {
            sheet.Read(history);
        }
        else if (_failure.Length > 0)
        {
            sheet.Failed(_failure);
        }

        return sheet;
    }
}
