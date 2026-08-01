using Bws.Core;

namespace Bws.Gui.ViewModels;

/// <summary>
/// One entry as a row on screen.
///
/// A separate type from <see cref="ScmEntry"/> rather than binding to it directly, and the
/// reason is the one this project spends most of its rules on: an entry carries four read
/// states per field, and a row carries text. Turning the first into the second is a decision
/// with a wrong answer - "not read" rendered as an empty cell reads as "there is none", and
/// those two say opposite things about a service.
///
/// Doing it here means the rule lives in one place and can be checked without a window.
/// </summary>
public sealed class EntryRow
{
    private EntryRow(ScmEntry entry)
    {
        ServiceName = entry.ServiceName;
        DisplayName = entry.DisplayName;
        Status = entry.Status.ToString();
        StartType = Describe(entry.StartType, value => value.ToString());
        Account = Describe(entry.Account, value => value);
        ProcessId = Describe(entry.ProcessId, value => value.ToString(System.Globalization.CultureInfo.CurrentCulture));
    }

    public string ServiceName { get; }

    public string DisplayName { get; }

    public string Status { get; }

    public string StartType { get; }

    public string Account { get; }

    public string ProcessId { get; }

    public static EntryRow Of(ScmEntry entry) => new(entry);

    /// <summary>
    /// A reading as text, with each of the four states saying something different.
    ///
    /// The empty string is only ever used for "there is genuinely nothing", which is the one
    /// state where a blank cell tells the truth. The other two say so in words, because a
    /// person scanning a column has no other way to tell them from a value nobody has.
    /// </summary>
    private static string Describe<T>(Reading<T> reading, Func<T, string> text) => reading.Outcome switch
    {
        ReadOutcome.Present => text(reading.Value!),
        ReadOutcome.Absent => string.Empty,
        ReadOutcome.Denied => Texts.Of("gui.cell.noAccess"),
        _ => Texts.Of("gui.cell.unknown")
    };
}
