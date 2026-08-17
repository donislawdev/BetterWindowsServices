namespace Bws.Gui.ViewModels;

/// <summary>
/// The entry the window is looking at, and everything the window says about that one entry.
///
/// <b>A seam the details panel asked for, and the size ratchet insisted on - 2026-08-13.</b>
/// <see cref="MainViewModel"/> stood at exactly five hundred lines, which is one line short of
/// being a long file with all three allowances already spent, so the panel could not add a single
/// property there. The seam it found is real rather than convenient: that class is about the LIST -
/// what the query selects, what the machine says, what may rearrange itself - and none of those
/// questions are about the one row somebody is pointing at.
///
/// <b>The chosen row and the panel are one subject rather than two.</b> The panel shows the chosen
/// row, the copy items copy the chosen row, and both need the same answer to "which one". Holding
/// them apart would be two objects that have to agree, which is the shape this project pays for
/// most often.
///
/// <b>Public, because the panel binds to it.</b> WPF cannot see an internal property from outside
/// this assembly, and finding that out costs a blank panel with nothing in the build to say so -
/// the same reason <see cref="DetailLine"/> is public.
/// </summary>
public sealed class Chosen : Observable
{
    private EntryRow? _row;
    private bool _showing;
    private bool _gone;
    private IReadOnlyList<DetailSection> _sections = [];

    /// <summary>
    /// The row somebody has chosen, or nothing.
    ///
    /// <b>Handed in at the moment it is needed rather than bound to the list, and that is a
    /// repair.</b> It was a two way binding on SelectedItem for one afternoon, and the window
    /// journey turned flaky inside it - passages reporting a grid that disagreed with its own count
    /// line, with the window saying it was holding still. A binding into a list that reconciles
    /// itself once a second is another party in the middle of `A10`.
    ///
    /// Setting it does NOT open the panel. The list is a tool for searching - `docs/11` opens with
    /// that sentence - and a panel that appeared on every click would take half the width of the
    /// list away from anybody who was only scrolling.
    /// </summary>
    public EntryRow? Row
    {
        get => _row;

        set
        {
            if (ReferenceEquals(_row, value))
            {
                return;
            }

            // The panel follows whatever it was opened on rather than following the selection, so
            // nothing here touches it. Choosing a different row while it is open leaves it where
            // it was - Enter is what moves it, which is the same key that opened it.
            _row = value;

            Raise(nameof(Row));
            Raise(nameof(ServiceName));
            Raise(nameof(DisplayName));
        }
    }

    /// <summary>What a copy of the name would put on the clipboard, or nothing when no row is chosen.</summary>
    public string? ServiceName => Row?.ServiceName;

    /// <summary>The same for the display name, which is the one a person recognises.</summary>
    public string? DisplayName => Row?.DisplayName;

    /// <summary>
    /// What this entry says about itself, which is the field a cell can hold least of.
    ///
    /// Read through the catalogue like every other cell, so a copy cannot say something the column
    /// does not - the same rule the whole panel rests on.
    /// </summary>
    public string? Description => Row?["description"];

    /// <summary>
    /// Everything the window knows about the chosen entry, as text somebody can paste.
    ///
    /// <b>The catalogue rather than the row on screen, so a copy does not depend on which columns
    /// happen to be turned on.</b> Somebody copying an entry to put in a ticket wants what there is
    /// to know, not what they had room for - and the twelve columns that are off by default are
    /// exactly the ones too long to have kept.
    ///
    /// <b>A label, a tab and a value per line.</b> Tab because that is what a spreadsheet and a
    /// ticket both take, and one field per line because several of these values run to hundreds of
    /// characters - a single line would be unreadable in every destination.
    ///
    /// The section headings are kept, on their own lines. They are how the picker groups the same
    /// fields, so a person who copies this recognises the shape of it.
    /// </summary>
    public string? Everything => Row is not { } row ? null : string.Join(
        Environment.NewLine,
        Details.Of(row.Entry).SelectMany(section => section.Lines
            .Select(line => line.Label + "\t" + line.Value)
            .Prepend(section.Heading)));

    /// <summary>
    /// Whether the details panel is on screen.
    ///
    /// Closed when the window opens, and it stays that way until somebody asks - the layout file of
    /// `S6d3` deliberately does not remember it, because that would be a change to a schema frozen
    /// a day earlier.
    /// </summary>
    public bool Showing
    {
        get => _showing;
        private set => Set(ref _showing, value);
    }

    /// <summary>Everything the window knows about the shown entry, in the picker's own order.</summary>
    public IReadOnlyList<DetailSection> Sections
    {
        get => _sections;
        private set => Set(ref _sections, value);
    }

    /// <summary>
    /// Whether the shown entry has left the listing while the panel was open.
    ///
    /// <b>Rule 8 where a details panel breaks it most easily.</b> A service somebody deletes, or one
    /// that Windows removes with an update, leaves the list - and a panel that carried on showing
    /// the last reading would be answering questions about a service that is not there, with
    /// nothing on screen to say so.
    /// </summary>
    public bool Gone
    {
        get => _gone;
        private set
        {
            if (Set(ref _gone, value))
            {
                Raise(nameof(Notice));
            }
        }
    }

    /// <summary>The sentence about the whole panel, which today is only ever the one above.</summary>
    public string Notice => Gone ? Texts.Of("gui.details.gone") : string.Empty;

    /// <summary>
    /// The name of the shown entry, which is the identity rather than the label - `ADR-14`.
    ///
    /// Both are on screen: the display name reads as the heading because it is the one somebody
    /// recognises, and the internal name is under it because it is the one they will type into a
    /// command. Neither stands alone.
    /// </summary>
    public string ShownName { get; private set; } = string.Empty;

    /// <inheritdoc cref="ShownName"/>
    public string ShownLabel { get; private set; } = string.Empty;

    /// <summary>
    /// Opens the panel on the chosen row, and says whether it did anything.
    ///
    /// <b>The answer matters rather than being a courtesy.</b> A key press marked handled by
    /// something that decided to do nothing is a key that silently stops working for whatever
    /// needed it next - the same reasoning <see cref="MainViewModel.ClearQuery"/> carries.
    ///
    /// Opening it again on another row moves it, which is why this is not a toggle: Enter means
    /// "show me this one" every time, and Escape is the way out.
    /// </summary>
    internal bool Show()
    {
        if (Row is not { } row)
        {
            return false;
        }

        Follow(row);

        Showing = true;

        return true;
    }

    /// <summary>
    /// Closes the panel, and says whether there was one to close.
    ///
    /// <b>The answer is what puts Escape in the right order.</b> Escape closes the panel first and
    /// clears the query only when there is no panel - decided in `docs/04` rather than here,
    /// because one press doing two things at once loses somebody their query while they were
    /// reaching for the panel.
    /// </summary>
    internal bool Hide()
    {
        if (!Showing)
        {
            return false;
        }

        Stop();

        Showing = false;
        Gone = false;

        return true;
    }

    /// <summary>
    /// Asks whether the shown entry is still in the listing.
    ///
    /// <b>Against the WHOLE listing rather than against the rows on screen, and the difference is
    /// the whole point.</b> A row leaves the visible list whenever a query stops selecting it,
    /// which is an ordinary thing that happens on every keystroke - saying "this is gone" then
    /// would be the window being confidently wrong about a service that is running perfectly well.
    /// </summary>
    internal void StillIn(IReadOnlyCollection<EntryRow> everything)
    {
        ArgumentNullException.ThrowIfNull(everything);

        if (!Showing || Row is not { } row)
        {
            return;
        }

        Gone = !everything.Contains(row);
    }

    /// <summary>
    /// Follows one row, so the panel moves when the list does.
    ///
    /// <b>The panel refreshes because it listens, not because anything refreshes it.</b> A row
    /// keeps its identity for the life of the window and its cells move underneath it - see
    /// <see cref="EntryRow"/> - so a panel built once from the entry would show whatever was true
    /// at the moment somebody pressed Enter and would never say otherwise.
    /// </summary>
    private void Follow(EntryRow row)
    {
        Stop();

        _followed = row;
        _followed.PropertyChanged += Moved;

        Rebuild();
    }

    /// <summary>
    /// Lets go of whatever row was being followed.
    ///
    /// <b>The teardown case, and it is the one this class would leak through.</b> A row lives as
    /// long as the window does, so a handler left on it keeps this object alive and keeps
    /// rebuilding five sections for a panel nobody is looking at - once per second, for as long as
    /// the window is open.
    /// </summary>
    private void Stop()
    {
        if (_followed is not null)
        {
            _followed.PropertyChanged -= Moved;
            _followed = null;
        }
    }

    private EntryRow? _followed;

    private void Moved(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Rebuild();

    /// <summary>
    /// Builds the lines again from whatever the followed row now holds.
    ///
    /// Whole rather than in part, because a row says one word when any of its cells move - the
    /// notification is <c>Item[]</c> for all of them at once - so there is nothing finer to react
    /// to. Five sections of eighteen lines is not a measurement worth making cleverer.
    /// </summary>
    private void Rebuild()
    {
        if (_followed is not { } row)
        {
            return;
        }

        ShownName = row.ServiceName;
        ShownLabel = row.DisplayName;
        Sections = Details.Of(row.Entry);

        Raise(nameof(ShownName));
        Raise(nameof(ShownLabel));
    }
}
