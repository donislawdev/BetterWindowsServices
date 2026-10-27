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
        At = group.Last ?? group.Latest.At;
        FirstAt = group.Times > 1 ? group.First : null;
        When = HistoryWords.When(At);
        Times = group.Times > 1 ? group.Times.ToString(CultureInfo.CurrentCulture) : string.Empty;
        Entry = HistoryWords.Entry(group.Latest);
        Change = HistoryWords.What(group.Latest);
        By = HistoryWords.Who(group.Latest.By);
        Since = FirstAt is null ? string.Empty : HistoryWords.When(FirstAt);
    }

    /// <summary>When - the last time, for a group.</summary>
    public string When { get; }

    /// <summary>
    /// The moment behind <see cref="When"/>, which the table sorts by - since 2026-10-27, logic report H04.
    /// The text is this machine's local time without its offset, so the hour after the clocks go back
    /// sorts before the hour it repeats, and two changes in it read as one moment. An instant does not.
    /// Null when the log recorded no time, which sorts as the oldest, where the text "(not recorded)" sorted.
    /// </summary>
    public DateTimeOffset? At { get; }

    /// <summary>The moment behind <see cref="Since"/>, for the same reason - null for a change recorded once.</summary>
    public DateTimeOffset? FirstAt { get; }

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
