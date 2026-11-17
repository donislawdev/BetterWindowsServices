using Bws.Core;

namespace Bws.Gui.ViewModels;

public static partial class Catalogue
{
    /// <summary>
    /// A model whose list under the search box is the guide, for the SearchGuide view on the sheet
    /// (2026-11-17, docs/PROJEKT-POLE-ZAPYTANIA-20261117.md).
    ///
    /// <b>Its own model, never a shared one</b>: the guide IS the list under the box, so asking for it
    /// opens that list - on a shared model it would open under every SearchRow on the sheet too.
    ///
    /// <b>A field holding a factory rather than a method</b>, because this type stood one method under
    /// the crowd line of the shape ratchet that day (53 of 53.9), and Catalogue.Views.cs three lines
    /// under its ceiling of length.
    /// </summary>
    private static readonly Func<MainViewModel> Guiding = static () =>
    {
        var model = new MainViewModel(new Frozen(Specimens()), new SystemClock());

        model.Suggesting.Guide(string.Empty);

        return model;
    };
}
