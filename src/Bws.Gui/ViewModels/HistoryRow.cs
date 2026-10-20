using System.Globalization;
using Bws.Core.History;

namespace Bws.Gui.ViewModels;

/// <summary>
/// One line of the change history table: a change, or identical changes gathered into one
/// (<see cref="ChangeGroup"/>, owner's decision J2-b).
///
/// <b>Public because the table binds to it</b> - WPF's binding cannot see an internal property from
/// outside this assembly, the reason <see cref="DetailLine"/> gives.
///
/// <b>Worked out once, when the history arrives.</b> A history is a statement about what the log held
/// when it was read, so nothing here moves afterwards - the comparison's argument about its own rows.
/// </summary>
public sealed class HistoryRow
{
    internal HistoryRow(ChangeGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        Group = group;
        When = HistoryWords.When(group.Last ?? group.Latest.At);
        Times = group.Times > 1 ? group.Times.ToString(CultureInfo.CurrentCulture) : string.Empty;
        Entry = HistoryWords.Entry(group.Latest);
        Change = HistoryWords.What(group.Latest);
        By = HistoryWords.Who(group.Latest.By);
        Since = group.Times > 1 ? HistoryWords.When(group.First) : string.Empty;
    }

    /// <summary>When - the last time, for a group.</summary>
    public string When { get; }

    /// <summary>How many changes the line stands for, and empty for one - "1" in a column of counts is noise.</summary>
    public string Times { get; }

    /// <summary>The entry, or the display name in quotes for an installation.</summary>
    public string Entry { get; }

    /// <summary>What changed, in the words Windows wrote at the time.</summary>
    public string Change { get; }

    /// <summary>Who changed it.</summary>
    public string By { get; }

    /// <summary>The first time, for a group - empty for a change recorded once.</summary>
    public string Since { get; }

    /// <summary>
    /// How many changes, as a number the table sorts by - <see cref="Times"/> is text and empty for
    /// one, and text sorts "9" after "56".
    /// </summary>
    public int Count => Group.Times;

    /// <summary>What the line was made from, for the filters and a test.</summary>
    internal ChangeGroup Group { get; }
}
