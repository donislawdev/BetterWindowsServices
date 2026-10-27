using Bws.Core.History;

namespace Bws.Gui.Tests;

/// <summary>
/// A System log for the window's change history (J2) that answers with what a test hands it - or with
/// the failure it is told to throw - and can be held mid-reading, the way <see cref="LiveMachine"/> holds
/// the manager, so a test can have a reading genuinely out.
/// </summary>
internal sealed class HistoryLog : IChangeHistoryReader
{
    internal const string SystemSid = "S-1-5-18";
    internal const string PersonSid = "S-1-5-21-1111111111-2222222222-3333333333-1001";

    private readonly ManualResetEventSlim _gate = new(initialState: true);
    private int _reads;

    internal HistoryLog(ChangeHistory history) => History = history;

    /// <summary>What the next reading answers with.</summary>
    internal ChangeHistory History { get; set; }

    /// <summary>Thrown by the next reading instead of answering, when set.</summary>
    internal Exception? FailWith { get; set; }

    /// <summary>How many readings were asked for.</summary>
    internal int Reads => Volatile.Read(ref _reads);

    public ChangeHistory Read()
    {
        Interlocked.Increment(ref _reads);
        _gate.Wait(TimeSpan.FromSeconds(30));

        return FailWith is { } failure ? throw failure : History;
    }

    internal void Hold() => _gate.Reset();

    internal void Release() => _gate.Set();

    /// <summary>
    /// The owner's machine on 2026-10-20 in small: BITS switched both ways by SYSTEM twice, Spooler
    /// switched once by a person, a driver installed twice from one folder, newest first.
    /// </summary>
    internal static ChangeHistory Ordinary() => new(
        Log(),
        [
            Installed("WinDivert", @"C:\one\WinDivert64.sys", Day(20, 12)),
            BySystem("BITS", Day(20, 11)),
            BySystem("BITS", Day(20, 10), back: true),
            ByPerson("Spooler", Day(18, 9)),
            Installed("WinDivert", @"C:\one\WinDivert64.sys", Day(17, 8)),
            BySystem("BITS", Day(16, 7)),
            BySystem("BITS", Day(16, 6), back: true)
        ]);

    /// <summary>A log just cleared - nothing in it but the clearing.</summary>
    internal static ChangeHistory Empty() => new(Log(), []);

    internal static LogCoverage Log() => ChangeHistory.Cover(
        "System",
        new LogFacts(2_166_784UL, 20_971_520UL, Full: false, LogMode.Circular),
        Day(11, 16),
        new Clearing(Day(11, 16), new Actor(PersonSid, @"PC\someone")));

    /// <summary>A start type switched by Windows itself - automatic to on demand, or the way back.</summary>
    internal static ServiceChange BySystem(string name, DateTimeOffset at, bool back = false) => new()
    {
        At = at,
        Kind = ChangeKind.StartTypeChanged,
        ServiceName = name,
        DisplayName = name,
        By = new Actor(SystemSid, @"NT AUTHORITY\SYSTEM"),
        Words = back
            ? new WindowsWords("demand start", "auto start", null, null)
            : new WindowsWords("auto start", "demand start", null, null)
    };

    /// <summary>The same switch made by a person.</summary>
    internal static ServiceChange ByPerson(string name, DateTimeOffset at, bool back = false) =>
        BySystem(name, at, back) with { By = new Actor(PersonSid, @"PC\someone") };

    internal static ServiceChange Installed(string display, string path, DateTimeOffset at) => new()
    {
        At = at,
        Kind = ChangeKind.Installed,
        DisplayName = display,
        By = new Actor(PersonSid, @"PC\someone"),
        BinaryPath = path,
        Words = new WindowsWords(null, null, "kernel mode driver", "demand start")
    };

    internal static DateTimeOffset Day(int day, int hour) => new(2026, 10, day, hour, 0, 0, TimeSpan.Zero);
}
