namespace Bws.Gui.ViewModels;

/// <summary>
/// The bar of tabs and the Tools page, as far as the list's model is concerned - `docs/PROJEKT-TOOLS-20261020.md`.
///
/// <b>Little, on purpose.</b> Which page is up, the marks on the tabs and the Tools page are
/// <see cref="ViewModels.Tabs"/>, which knows nothing about the list. What is here is the two things only the
/// list's model can do for it: put away whatever stands where the list was, and name an entry from every row
/// the window holds.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>The bar of tabs - which page is up, and the Tools page.</summary>
    public Tabs Tabs { get; }

    /// <summary>
    /// What a person calls an entry, from every row the window holds rather than the ones a query left on
    /// screen - or nothing when the reading has no such entry. For the title of a sheet opened by name, and
    /// for whether a card on the Tools page has anything to restart.
    /// </summary>
    internal string? DisplayNameOf(string serviceName) =>
        _index.Ordered.FirstOrDefault(row => string.Equals(row.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
            ?.DisplayName;

    /// <summary>
    /// Puts away whatever stands where the list was - a comparison or the form before a snapshot, the change
    /// history, the overview - and the panel about one entry, which stands beside a list. What a page arriving
    /// asks of the list (Tabs), and the same rule OpenHistory follows: the newer thing somebody asked for wins.
    /// </summary>
    private void ClearTheMiddle()
    {
        Snapshotting.PutAway();
        History.PutAway();
        ShowingOverview = false;
        Chosen.Hide();
    }
}
