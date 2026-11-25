using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// What every entry under the recovery form agrees on, part by part - null for a part where two differ or one
/// was not read. The program and the message carry whether they agree beside the value, because "no program" is
/// an answer the entries can agree on. What the form shows while a part has no tick, and what taking a tick off
/// puts back (<see cref="RecoveryForm"/>).
/// </summary>
internal sealed record RecoveryInCommon(
    IReadOnlyList<RecoveryItem>? Items,
    TimeSpan? Period,
    (bool Agreed, string? Value) Command,
    (bool Agreed, string? Value) Message,
    bool? Flag)
{
    internal bool Agreed(RecoveryParts part) => part == RecoveryParts.Command ? Command.Agreed : Message.Agreed;

    /// <summary>What these settings agree on - nothing at all when one of them was not read.</summary>
    internal static RecoveryInCommon Over(IReadOnlyList<RecoverySettings?> all)
    {
        if (all.Count == 0 || all.Any(one => one is null))
        {
            return new RecoveryInCommon(null, null, (false, null), (false, null), null);
        }

        var first = all[0]!;
        var same = (Func<RecoverySettings, bool> agrees) => all.All(one => agrees(one!));
        var command = same(one => string.Equals(one.List.Command, first.List.Command, StringComparison.Ordinal));
        var message = same(one => string.Equals(one.List.RebootMessage, first.List.RebootMessage, StringComparison.Ordinal));

        return new RecoveryInCommon(
            same(one => one.List.Items.SequenceEqual(first.List.Items)) ? first.List.Items : null,
            same(one => one.List.ResetPeriod == first.List.ResetPeriod) ? first.List.ResetPeriod : null,
            (command, command ? first.List.Command : null),
            (message, message ? first.List.RebootMessage : null),
            same(one => one.OnNonCrashFailures == first.OnNonCrashFailures) ? first.OnNonCrashFailures : null);
    }
}
