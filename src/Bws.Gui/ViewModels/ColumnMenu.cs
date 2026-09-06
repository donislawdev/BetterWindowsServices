using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What a right click on a column heading offers: the values that column can be narrowed to, and
/// a way to put the column away.
///
/// <b>THE OWNER'S ASK, 2026-09-05: click Status, choose Running, without learning the language
/// first.</b> The window already had that gesture in the chip row and it reached four fields. This
/// reaches every column that has a field at all, and it reaches it from the place a person is
/// already looking when they want it - the heading of the column they are reading.
///
/// <b>Nothing here is new machinery, which is most of the reason to build it this way.</b> A value
/// is a <see cref="FilterChip"/>: it holds no state, reads whether it is on out of the query text,
/// and writes a member somebody can read back. So ticking Running in this menu and clicking the
/// Running chip in the row above are the same act, and the box says so afterwards - which is the
/// `A5` promise arriving through a second door rather than a second mechanism.
///
/// <b>WHAT HAPPENS ON A COLUMN THAT CANNOT BE FILTERED - my decision, on the owner's instruction
/// to choose.</b> Five columns have no field in the language: the file version, the hash, the load
/// order group, the error control and the dependencies. A menu that refused to open on those would
/// teach that the gesture is unreliable, and a person cannot tell "this column has no values" from
/// "right click does nothing here" - so THE MENU ALWAYS OPENS and always ends with the same last
/// item. On those five it is the only item, and it is still worth the click: the picker moved
/// behind a submenu the same day, so putting one column away from its own heading is now the
/// shortest road there is.
///
/// <b>And it is a tick list rather than a radio list</b>, because that is what the language does:
/// members of one field are ORed, so ticking Running and Stopped shows both. Calling it "show
/// only" and then adding on a second click would be a control that lies about the query it wrote.
/// </summary>
public sealed class ColumnMenu
{
    private readonly ColumnChoice? _choice;

    internal ColumnMenu(string columnId, ColumnChoice? choice, Func<string> read, Action<string> write)
    {
        ArgumentNullException.ThrowIfNull(columnId);

        _choice = choice;

        // ASKED OF THE LANGUAGE RATHER THAN LISTED HERE, which is what QueryFields.ValuesOf was
        // opened for on the same day. A text, number or size field answers with nothing and the
        // menu simply has no values - so "which columns get a value list" is not a third list
        // anybody has to keep, it falls out of what the field is.
        Values = Columns.FieldOf(columnId) is { } field
            ?
            [
                .. QueryFields.ValuesOf(field)
                    .Select(value => FilterChip.Spelled(field, value, read, write))
            ]
            : [];
    }

    /// <summary>The values this column can be narrowed to, in the order the language offers them.</summary>
    public IReadOnlyList<FilterChip> Values { get; }

    /// <summary>What the last item says.</summary>
    public string HideLabel => Texts.Of("gui.columns.hideThisOne");

    /// <summary>
    /// Why it cannot be done, for the times it cannot.
    ///
    /// <b>It is a tooltip on a DISABLED item, which in WPF is a sentence nobody reads by default</b>
    /// - a disabled control shows no tooltip at all unless <c>ToolTipService.ShowOnDisabled</c>
    /// says otherwise, so the reason exists everywhere except on the screen. That trap is written
    /// up in `docs/10` and the style that draws this item carries the property.
    /// </summary>
    public string HideRefused => Texts.Of("gui.columns.hideThisOne.refused");

    /// <summary>
    /// Whether the column may be put away, which is false for the last one still showing.
    ///
    /// <b>Asked of the same flag the picker asks</b>, rather than counted again here. A list with
    /// no columns at all is 809 rows of nothing above a count line still saying 809, and there is
    /// one rule about that in this window rather than one per door into it.
    /// </summary>
    public bool MayHide => _choice is { MayHide: true };

    /// <summary>
    /// Puts this column away.
    ///
    /// Through the choice the picker owns, so the tick in the picker goes out at the same moment
    /// and the layout is written exactly as it would have been from there - one road, two doors.
    /// </summary>
    public void Hide()
    {
        if (_choice is not null && MayHide)
        {
            _choice.IsShown = false;
        }
    }
}
