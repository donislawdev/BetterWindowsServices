using Bws.Core.History;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The change history's cells for the component catalogue - GUI rule 4: every view in every state it
/// has (J2, 2026-10-20).
///
/// <b>A history written here rather than read</b>, because the catalogue reads nothing from the machine
/// it runs on - the README says so of `--catalogue`. Its shape is the owner's machine on the day J2 was
/// designed, cut down: Windows switching one service both ways by itself, a person switching another, a
/// driver installed twice from one place and once from another, and one change that names no entry.
/// </summary>
public static partial class Catalogue
{
    /// <summary>
    /// The view of the whole machine, the "Show" row over it and the table under it, in the states they
    /// have. <b>Data</b> is the history as it opens - identical changes together, every account.
    /// <b>Empty</b> is a log just cleared. <b>Wrong</b> is a log Windows would not hand over.
    /// <b>Loading</b> is the first reading still out. <b>Extreme</b> is a log losing its oldest events,
    /// each change on its own line, narrowed to what SYSTEM did not do and searched for the driver,
    /// under the longest name the catalogue has.
    /// </summary>
    private static Built[] HistoryViews()
    {
        var data = Read(Recorded.Ordinary());
        var empty = Read(Recorded.Cleared());
        var wrong = new HistorySheet();
        var loading = new HistorySheet();
        var extreme = Read(Recorded.Overwritten());
        var unmatched = Read(Recorded.Ordinary());

        wrong.Failed(Texts.Of("gui.history.readFailed", "Access is denied."));
        Narrowed(extreme.Filter);
        extreme.Filter.SearchText = "Specimen64";
        unmatched.Filter.SearchText = "nothing like this";

        return
        [
            View(() => new HistoryView(), data: data, empty: empty, wrong: wrong, loading: loading, extreme: extreme),

            // The empty cell is a search that finds nothing - the one way the row stands over no line. A
            // log with no change in it has no table, and no row over one.
            View(() => new ShowRow { Named = "history" }, key: nameof(ShowRow) + " over the change history",
                data: data.Filter, empty: unmatched.Filter, extreme: extreme.Filter),

            View(() => new HistoryTable(), data: data, wrong: wrong, loading: loading, extreme: extreme)
        ];
    }

    private static HistorySheet Read(ChangeHistory history)
    {
        var sheet = new HistorySheet();

        sheet.Read(history);

        return sheet;
    }

    /// <summary>Each change on its own line, and only what SYSTEM did not do - the second tick of each group.</summary>
    private static void Narrowed(HistoryFilter filter)
    {
        filter.Groups[0][1].IsChosen = true;
        filter.Groups[1][1].IsChosen = true;
    }

    /// <summary>
    /// A change history that answers at once and always the same - for the catalogue's panels and views,
    /// and for nothing else.
    /// </summary>
    internal sealed class Recorded(ChangeHistory history) : IChangeHistoryReader
    {
        private const string SystemSid = "S-1-5-18";
        private const string PersonSid = "S-1-5-21-1111111111-2222222222-3333333333-1001";

        public ChangeHistory Read() => history;

        /// <summary>The history the panels and the data cell are drawn over.</summary>
        internal static ChangeHistory Ordinary() => new(Log(cleared: true, overwritten: false), Changes(LongestSpecimen().DisplayName));

        /// <summary>A log just cleared - the clearing is all it holds.</summary>
        internal static ChangeHistory Cleared() => new(Log(cleared: true, overwritten: false), []);

        /// <summary>A log at its size limit, throwing its oldest events away.</summary>
        internal static ChangeHistory Overwritten() => new(Log(cleared: false, overwritten: true), Changes(LongDescription));

        private static LogCoverage Log(bool cleared, bool overwritten) => ChangeHistory.Cover(
            "System",
            new LogFacts(overwritten ? 21_000_000UL : 2_166_784UL, 20_971_520UL, Full: false, LogMode.Circular),
            At(11, 16, 58),
            cleared ? new Clearing(At(11, 16, 58), new Actor(PersonSid, @"CATALOGUE\admin")) : null);

        /// <summary>Newest first, the order a history is in.</summary>
        private static List<ServiceChange> Changes(string installedName) =>
        [
            Installed(installedName, @"C:\Tools\driver\Specimen64.sys", At(20, 12, 24)),
            Switched("BITS", "auto start", "demand start", SystemSid, At(20, 12, 18)),
            Switched("BITS", "demand start", "auto start", SystemSid, At(20, 12, 15)),
            Installed(installedName, @"C:\Tools\driver\Specimen64.sys", At(19, 22, 33)),
            Switched("Spooler", "auto start", "demand start", PersonSid, At(18, 9, 2)),
            Switched("BITS", "auto start", "demand start", SystemSid, At(17, 7, 40)),
            Switched("BITS", "demand start", "auto start", SystemSid, At(17, 7, 31)),
            Installed(installedName, @"D:\Elsewhere\Specimen64.sys", At(15, 10, 8)),
            Switched(null, "auto start", "disabled", PersonSid, At(14, 19, 29))
        ];

        private static ServiceChange Switched(string? name, string before, string after, string sid, DateTimeOffset at) => new()
        {
            At = at,
            Kind = ChangeKind.StartTypeChanged,
            ServiceName = name,
            DisplayName = name,
            By = new Actor(sid, sid == SystemSid ? @"NT AUTHORITY\SYSTEM" : @"CATALOGUE\admin"),
            Words = new WindowsWords(before, after, null, null)
        };

        private static ServiceChange Installed(string display, string path, DateTimeOffset at) => new()
        {
            At = at,
            Kind = ChangeKind.Installed,
            DisplayName = display,
            By = new Actor(PersonSid, @"CATALOGUE\admin"),
            BinaryPath = path,
            Words = new WindowsWords(null, null, "kernel mode driver", "demand start")
        };

        private static DateTimeOffset At(int day, int hour, int minute) => new(2026, 10, day, hour, minute, 0, TimeSpan.Zero);
    }
}
