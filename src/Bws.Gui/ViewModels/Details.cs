using Bws.Core;

namespace Bws.Gui.ViewModels;

/// <summary>
/// One line of the details panel: what the field is called, and what this entry says in it.
///
/// <b>Public because the panel binds to it</b>, like <see cref="ColumnChoice"/> and for the same
/// reason - WPF's binding cannot see an internal property from outside this assembly, and finding
/// that out costs a blank panel with nothing in the build to say so.
/// </summary>
public sealed class DetailLine
{
    private readonly string _labelKey;

    internal DetailLine(string labelKey, string value, bool fixedWidth)
    {
        _labelKey = labelKey;
        Value = value;
        FixedWidth = fixedWidth;
    }

    /// <summary>What the field is called, in the language of whoever is reading it.</summary>
    public string Label => Texts.Of(_labelKey);

    /// <summary>
    /// What this entry says in it, in exactly the words the column's own cell would use.
    ///
    /// <b>Read through the same catalogue the grid reads</b>, so a field cannot say one thing in a
    /// cell and another in the panel. Two paths to one answer drift the first time either is
    /// touched, and nothing in a green build would notice.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Whether it is drawn in a fixed width face - a launch path and a security descriptor.
    ///
    /// The same decision <see cref="ColumnFace.Fixed"/> makes in the list and for the same reason
    /// out of `docs/03` part 4: alignment carries meaning there, and two different values must not
    /// be able to look the same.
    /// </summary>
    public bool FixedWidth { get; }
}

/// <summary>
/// One headed group of lines in the details panel.
///
/// <b>The groups are the picker's groups</b>, decided once in <see cref="Columns"/>, so a person
/// who has looked for a field in the column list finds it in the same place here. A second
/// grouping would be a second thing to keep in step, and `docs/11` complaint 1 is about making
/// somebody hunt.
/// </summary>
public sealed class DetailSection
{
    private readonly string _headingKey;
    private readonly string _noteKey;

    internal DetailSection(string headingKey, IReadOnlyList<DetailLine> lines, string noteKey = "")
    {
        _headingKey = headingKey;
        _noteKey = noteKey;
        Lines = lines;
    }

    public string Heading => Texts.Of(_headingKey);

    /// <summary>A sentence about the whole group, or nothing - which is the ordinary case.</summary>
    public string Note => _noteKey.Length == 0 ? string.Empty : Texts.Of(_noteKey);

    public IReadOnlyList<DetailLine> Lines { get; }
}

/// <summary>
/// Everything this window knows about one entry, arranged for a person rather than for a grid.
///
/// <b>It is the column catalogue applied to one row, and that is the whole design.</b> Eighteen
/// columns are already eighteen label-and-value pairs derived from an entry, with translated
/// headings and cells that answer the four read states - so the panel is those pairs, all of them,
/// whether or not somebody turned the column on. Building a second list of fields here would be a
/// second place to add a field to, and the two would disagree the first time only one was touched.
///
/// <b>Nothing here asks the manager anything.</b> Every value comes out of the entry the listing
/// already read, which is what keeps this slice free of the questions `ADR-13` exists to answer -
/// no cost, no background thread, no field that is expensive on somebody else's machine.
///
/// <b>The last section is the one that makes this panel honest.</b> Four fields are deliberately
/// not read by this window - signature, file version, hash and memory, all of them the second pass
/// of `ADR-13` that the window does not have. In a cell they would write "nobody looked" eight
/// hundred times, which is why they are not columns. In a panel about ONE entry, saying it once is
/// exactly right - and leaving them out entirely would let the panel read as complete when it is
/// not, which is rule 8 in the place a details panel breaks it most easily.
/// </summary>
internal static class Details
{
    // NAMED CONSTANTS RATHER THAN LITERALS IN THE ARRAY, and that is not a style preference.
    // TextKeyGuards finds the keys a window can say by the shapes in which somebody CHOOSES one -
    // a call to Texts.Of, a LabelKey assignment, a constant declaration - and a literal sitting in
    // a collection initialiser is none of them. Written as literals there, these four would be
    // reported as text that reaches no screen, which is the guard failing in its safe direction and
    // a red run for a correct panel.
    private const string SignatureLabel = "gui.column.signature";
    private const string FileVersionLabel = "gui.column.fileVersion";
    private const string BinaryHashLabel = "gui.column.binaryHash";
    private const string MemoryLabel = "gui.column.memory";

    private const string NotReadHeading = "gui.details.group.notRead";
    private const string NotReadNote = "gui.details.notRead.why";

    /// <summary>The four fields this window does not read, in the order the specification lists them.</summary>
    private static readonly string[] NotRead =
        [SignatureLabel, FileVersionLabel, BinaryHashLabel, MemoryLabel];

    /// <summary>Everything the window can say about one entry, in the picker's own order.</summary>
    internal static IReadOnlyList<DetailSection> Of(ScmEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var sections = new List<DetailSection>();

        foreach (var heading in new[] { Columns.Basics, Columns.About, Columns.Binary, Columns.Advanced })
        {
            var lines = Columns.All
                .Where(column => Columns.GroupOf(column.Id) == heading)
                .Select(column => new DetailLine(
                    column.LabelKey,
                    column.Reads(entry),
                    column.Face == ColumnFace.Fixed))
                .ToList();

            sections.Add(new DetailSection(heading, lines));
        }

        sections.Add(new DetailSection(
            NotReadHeading,
            [.. NotRead.Select(key => new DetailLine(key, Texts.Of("gui.details.notRead"), fixedWidth: false))],
            NotReadNote));

        return sections;
    }
}
