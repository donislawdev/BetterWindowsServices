namespace Bws.Gui.ViewModels;

/// <summary>
/// One block of commands on the plan sheet - the equivalent commands before a run, the way back
/// after one - and how the sheet shows them: a line at a time with a copy button beside each, or,
/// past <see cref="ListedUpTo"/> lines, all of them in one field that can only be read.
///
/// <b>WHY A THRESHOLD AT ALL - W6 of the performance series, report item G-7, 2026-09-29.</b> One
/// line is a box, a scroller, a text and a button, about fifteen elements. A stop over the whole
/// Services scope on the owner's machine had 334 of them - 5013 of the sheet's 7185 elements - and
/// the window did not answer for 0.46-0.8 s while the sheet was built (measured in process by
/// tools/gui-probe/row-cost.ps1 -PlanCost, numbers in the performance analysis, section 5, "W6").
/// Past a screenful nobody copies commands one at a time, so the button beside each line is what
/// the threshold gives up, and "Copy all" plus selecting inside the field is what stays. The
/// owner's decision of the same day.
///
/// <b>A TYPE RATHER THAN MORE PROPERTIES ON <see cref="Planned"/></b>, which stood at the ceiling
/// of methods a type may carry. Whether a block has anything, more than one thing, what it copies
/// and how it is shown are one answer about one list, and both blocks ask it the same way - so this
/// replaced the six properties that used to answer it twice.
///
/// <b>Immutable.</b> A new block is made when its lines change, so every binding reading one of
/// these reads the same answer, and the joined text is made once rather than once per binding.
/// </summary>
/// <remarks>
/// Public because WPF cannot see an internal type from outside this assembly - the reason
/// <see cref="PlanLine"/> gives.
/// </remarks>
public sealed class CommandBlock
{
    /// <summary>
    /// The most lines shown one at a time.
    ///
    /// <b>Twenty, because that is past a screenful.</b> The body of the sheet over a plan of fifty
    /// entries, in a window at its opening size, was a view 351 units tall (measured W6), which
    /// holds about a dozen command lines - so a list longer than this is one nobody reads a line at
    /// a time. The same plan measured 843 elements for its 56 commands, so twenty lines cost about
    /// three hundred: the price of a list short enough to use.
    /// </summary>
    public const int ListedUpTo = 20;

    /// <summary>A block with nothing in it - no plan, or a section that has nothing to say yet.</summary>
    public static CommandBlock None { get; } = new([]);

    /// <param name="lines">The commands, one per line, in the order they are to be run.</param>
    public CommandBlock(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        Lines = lines;

        // THE PLATFORM'S LINE ENDING RATHER THAN A BARE NEWLINE, because what this is for is pasting
        // into a Windows terminal, which runs the lines one after another. Joined here rather than in
        // the view, which holds layout and bindings and nothing else (GUI rule 11).
        All = string.Join(Environment.NewLine, lines);
    }

    /// <summary>Every command, one per line.</summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>
    /// Whether the block has anything to show - what takes its heading off the screen with it when
    /// it has not (backlog 203: a heading over nothing states something false).
    /// </summary>
    public bool Any => Lines.Count > 0;

    /// <summary>
    /// Whether the block is worth a button that takes all of it at once - the owner's request of
    /// 2026-09-16, made over a sheet with five commands and five buttons that each took one.
    ///
    /// <b>More than one, not at least one.</b> Over a single command "Copy all" beside "Copy" is two
    /// buttons for one thing, and the second would be the one somebody wonders about.
    /// </summary>
    public bool Several => Lines.Count > 1;

    /// <summary>Whether the lines stand in one field rather than one box each - see <see cref="ListedUpTo"/>.</summary>
    public bool AtOnce => Lines.Count > ListedUpTo;

    /// <summary>
    /// The lines shown one at a time, and NOTHING past the threshold rather than the lines with the
    /// list hidden. The list then holds nothing it does not show, so whether WPF builds boxes for the
    /// items of a collapsed list - not measured here - has no way to cost anything.
    /// </summary>
    public IReadOnlyList<string> Listed => AtOnce ? [] : Lines;

    /// <summary>Every command joined one per line, as the clipboard and the one field hold them.</summary>
    public string All { get; }
}
