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

            // ONE NOTIFICATION, SINCE THE COPY FIELDS LEFT THIS CLASS ON 2026-08-18. Two more
            // stood here for the two names a copy used to read from the chosen row, and they now
            // belong to the selection rather than to the panel - Copying answers for however many
            // rows somebody picked. Raising the name of a property that no longer exists does not
            // fail, and nothing about the panel would look wrong, so it is worth saying that these
            // went with the properties rather than being forgotten beside them.
            Raise(nameof(Row));
        }
    }

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

        // AFTER Showing, so a view that scrolls itself to the top on this has a panel to scroll.
        // Raised here and nowhere else: a refresh of the followed row rebuilds the sections and
        // leaves the scroll where it was, because a person reading a description does not want
        // it snatched back to the top once a second - GUI rule 3. What does want the top is a
        // DIFFERENT entry, and this is the only road one arrives by.
        Opened?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>
    /// The panel has just been opened on an entry - the same one again, or another.
    ///
    /// <b>An event rather than a property the view watches, because the thing that changed is
    /// not a value.</b> Opening the panel on the entry it already shows changes no property at
    /// all, and it still means "start reading from the top". Found on the owner's own capture of
    /// 2026-09-16: a panel opened on a new entry with its first lines above the fold, because the
    /// scroll position survived the swap of sections underneath it.
    /// </summary>
    internal event EventHandler? Opened;

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

        // ABOUT THE ROW THE PANEL IS FOLLOWING, NOT THE ROW THAT HAPPENS TO BE SELECTED - fixed
        // 2026-08-26, and it had been the wrong one since this was written.
        //
        // The panel deliberately does not follow the selection: opening it is Enter, and clicking
        // elsewhere afterwards leaves it where it was. Two lines of this class say so. This method
        // then asked its question about Row, which is the selection - so the moment somebody
        // clicked another row, the only thing the panel can say about the entry it is showing was
        // being answered about a different entry.
        //
        // The clearest way it broke was the scope switch, which sets Row to nothing: this returned
        // at the door and the panel simply stopped being able to notice its service had gone -
        // silently, and for the rest of the session.
        if (!Showing || _followed is not { } row)
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

        // Shown rather than Of: the two names above are the panel's head, and Of would repeat
        // them as the first two lines of the first section - which it did from 2026-08-13 to
        // 2026-09-16. A copy still takes Of, because a copy has no head.
        Sections = Details.Shown(row.Entry);

        Raise(nameof(ShownName));
        Raise(nameof(ShownLabel));
    }
}
