using System.Globalization;

namespace Bws.Core.Planning;

/// <summary>
/// How a recovery change is spelled on a command line - read and written by one table, the way
/// <see cref="StartTypeWords"/> spells a startup setting. Frozen in `docs/02` on 2026-11-24 (backlog 555 part
/// a2, the owner's decision "words and switches").
///
/// <b>In the core, beside the writer of the line, for the argument <see cref="EquivalentCommand"/> makes</b>: a
/// way back is a line somebody pastes, and a word this tool prints that it does not read back is a way out that
/// fails on the first line.
///
/// <b>The item words are the query language's own</b> - <c>recovery:restart</c>, <c>recovery:program</c>,
/// <c>recovery:reboot</c> (`docs/07`) - so asking which entries restart and making one restart are spelled the
/// same. <c>nothing</c> is Microsoft's "take no action", and <c>none</c> is a list with no items at all.
/// </summary>
public static class RecoveryCommandWords
{
    /// <summary>The switch carrying the reset period.</summary>
    public const string Reset = "--reset";

    /// <summary>The switch carrying the program's command line.</summary>
    public const string Program = "--program";

    /// <summary>The switch carrying the message before the computer restarts.</summary>
    public const string Message = "--message";

    /// <summary>The switch carrying whether the list also answers stops with an error.</summary>
    public const string OnErrorStops = "--on-error-stops";

    /// <summary>A list with no items - its own word, because "nothing" is an item.</summary>
    public const string NoActions = "none";

    /// <summary>A reset period that never ends until the machine restarts - <c>INFINITE</c>.</summary>
    public const string Never = "never";

    /// <summary>The two words <see cref="OnErrorStops"/> takes.</summary>
    public const string Yes = "yes";

    /// <inheritdoc cref="Yes"/>
    public const string No = "no";

    private static readonly (RecoveryAction Action, string Word)[] Kinds =
    [
        (RecoveryAction.RestartService, "restart"),
        (RecoveryAction.RunProgram, "program"),
        (RecoveryAction.RestartComputer, "reboot"),
        (RecoveryAction.Nothing, "nothing")
    ];

    private static readonly (string Unit, long Milliseconds)[] DelayUnits =
        [("d", 86_400_000), ("h", 3_600_000), ("m", 60_000), ("s", 1_000), ("ms", 1)];

    private static readonly (string Unit, long Seconds)[] PeriodUnits =
        [("d", 86_400), ("h", 3_600), ("m", 60), ("s", 1)];

    /// <summary>
    /// One item as typed - <c>restart:2m</c>, <c>nothing</c>, <c>nothing:5s</c> - or nothing where it is not
    /// one. A delay is required on the three items that do something, because any default would be a guess:
    /// services.msc offers a minute and <c>sc failure</c> makes it be typed.
    /// </summary>
    public static RecoveryItem? ReadItem(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        var colon = word.IndexOf(':', StringComparison.Ordinal);
        var name = colon < 0 ? word : word[..colon];
        var kind = Kinds.FirstOrDefault(entry => entry.Word.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (kind.Word is null)
        {
            return null;
        }

        if (colon < 0)
        {
            return kind.Action == RecoveryAction.Nothing ? new RecoveryItem(kind.Action, TimeSpan.Zero) : null;
        }

        return ReadDelay(word[(colon + 1)..]) is { } delay ? new RecoveryItem(kind.Action, delay) : null;
    }

    /// <summary>Whether a word names an item kind at all, with or without a delay that reads - for the refusal that says which half is wrong.</summary>
    public static bool NamesAnItem(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        var colon = word.IndexOf(':', StringComparison.Ordinal);
        var name = colon < 0 ? word : word[..colon];

        return Kinds.Any(entry => entry.Word.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>How an item is typed, or nothing for a type this tool cannot name.</summary>
    public static string? ItemWord(RecoveryItem item)
    {
        var kind = Kinds.FirstOrDefault(entry => entry.Action == item.Action);

        if (kind.Word is null)
        {
            return null;
        }

        return item.Action == RecoveryAction.Nothing && item.Delay == TimeSpan.Zero
            ? kind.Word
            : string.Concat(kind.Word, ":", DelayWord(item.Delay));
    }

    /// <summary>
    /// A delay as typed - a whole number with <c>ms</c>, <c>s</c>, <c>m</c>, <c>h</c> or <c>d</c>, or a bare 0 - up
    /// to what <c>SC_ACTION.Delay</c> holds, a DWORD of milliseconds.
    /// </summary>
    public static TimeSpan? ReadDelay(string text) =>
        Read(text, DelayUnits, uint.MaxValue, TimeSpan.FromMilliseconds);

    /// <summary>A delay in the largest unit that divides it exactly - 120000 ms is <c>2m</c>.</summary>
    public static string DelayWord(TimeSpan delay) => Written((long)delay.TotalMilliseconds, DelayUnits);

    /// <summary>
    /// A reset period as typed - a whole number with <c>s</c>, <c>m</c>, <c>h</c> or <c>d</c>, a bare 0, or
    /// <c>never</c> - up to one second short of what <c>dwResetPeriod</c> holds, because its largest value is
    /// <c>INFINITE</c>.
    /// </summary>
    public static TimeSpan? ReadPeriod(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Equals(Never, StringComparison.OrdinalIgnoreCase)
            ? Timeout.InfiniteTimeSpan
            : Read(text, PeriodUnits, uint.MaxValue - 1UL, TimeSpan.FromSeconds);
    }

    /// <summary>A reset period in the largest unit that divides it exactly, or <c>never</c>.</summary>
    public static string PeriodWord(TimeSpan period) =>
        period == Timeout.InfiniteTimeSpan ? Never : Written((long)period.TotalSeconds, PeriodUnits);

    /// <summary><c>yes</c> or <c>no</c>, and nothing for any other word.</summary>
    public static bool? ReadYesNo(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Equals(Yes, StringComparison.OrdinalIgnoreCase) ? true
            : text.Equals(No, StringComparison.OrdinalIgnoreCase) ? false
            : null;
    }

    /// <summary>
    /// The words after the name that ask for this change: the items (or <see cref="NoActions"/>), then each
    /// switch followed by its value, unquoted. Nothing when an item has no word - a list holding a type this tool
    /// cannot name has no line, and the plan refuses to touch one.
    ///
    /// <b>The period is left out beside an empty list</b>, because the manager removes it with the list and the
    /// command line refuses the pair.
    /// </summary>
    public static IReadOnlyList<(string Word, bool IsValue)>? Words(RecoveryChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        var words = new List<(string Word, bool IsValue)>();
        var items = change.To.List.Items;

        if (change.Writes(RecoveryParts.Actions))
        {
            if (ItemWords(items) is not { } typed)
            {
                return null;
            }

            words.AddRange(typed.Select(word => (word, false)));
        }

        if (change.Writes(RecoveryParts.ResetPeriod) && !(change.Writes(RecoveryParts.Actions) && items.Count == 0))
        {
            words.Add((Reset, false));
            words.Add((PeriodWord(change.To.List.ResetPeriod), false));
        }

        if (change.Writes(RecoveryParts.Command))
        {
            words.Add((Program, false));
            words.Add((change.To.List.Command ?? string.Empty, true));
        }

        if (change.Writes(RecoveryParts.RebootMessage))
        {
            words.Add((Message, false));
            words.Add((change.To.List.RebootMessage ?? string.Empty, true));
        }

        if (change.Writes(RecoveryParts.OnNonCrashFailures))
        {
            words.Add((OnErrorStops, false));
            words.Add((change.To.OnNonCrashFailures ? Yes : No, false));
        }

        return words;
    }

    /// <summary>The words of a list - <see cref="NoActions"/> for none, nothing when an item has no word.</summary>
    private static IReadOnlyList<string>? ItemWords(IReadOnlyList<RecoveryItem> items)
    {
        if (items.Count == 0)
        {
            return [NoActions];
        }

        string?[] words = [.. items.Select(ItemWord)];

        return words.Any(word => word is null) ? null : [.. words.OfType<string>()];
    }

    /// <param name="bound">The largest count of the smallest unit the manager holds.</param>
    private static TimeSpan? Read(
        string text, (string Unit, long Size)[] units, ulong bound, Func<double, TimeSpan> make)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text == "0")
        {
            return TimeSpan.Zero;
        }

        // Longest unit name first in the match, so "500ms" is read as milliseconds and never as "500m" and an s.
        foreach (var (unit, size) in units.OrderByDescending(entry => entry.Unit.Length))
        {
            if (!text.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var digits = text[..^unit.Length];

            // Digits only - no sign, no decimals, no thousands separator - read the same on every culture.
            if (digits.Length == 0 || !digits.All(char.IsAsciiDigit)
                || !ulong.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            {
                return null;
            }

            return count > bound / (ulong)size ? null : make(count * (ulong)size);
        }

        return null;
    }

    private static string Written(long value, (string Unit, long Size)[] units)
    {
        if (value == 0)
        {
            return "0";
        }

        var (unit, size) = units.First(entry => value % entry.Size == 0);

        return string.Concat((value / size).ToString(CultureInfo.InvariantCulture), unit);
    }
}
