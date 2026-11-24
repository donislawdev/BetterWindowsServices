using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Cli;

/// <summary>
/// What the recovery verb was asked for, as typed - the item words after the name and the four switches - before
/// anything is made of it. Backlog 555 part (a2), the spelling frozen in `docs/02` on 2026-11-24.
///
/// <b>Words rather than values, for the reason <see cref="StartTypeAsk"/> keeps its word</b>: a word that is not
/// an item has to appear in the sentence that says so. <b>One property on <see cref="CommandLine"/> for all of
/// it</b>, because that record stands at the ceiling for state and nothing here means anything to another verb.
/// </summary>
/// <param name="Items">The bare words after the name, in the order typed - the list, failure 1 first.</param>
/// <param name="Reset">What followed <c>--reset</c>, or null when it was not given.</param>
/// <param name="Program">What followed <c>--program</c>, or null - an empty text is a removal, not an absence.</param>
/// <param name="Message">What followed <c>--message</c>, or null, the same way.</param>
/// <param name="OnErrorStops">What followed <c>--on-error-stops</c>, or null.</param>
internal sealed record RecoveryAsk(
    IReadOnlyList<string> Items, string? Reset, string? Program, string? Message, string? OnErrorStops)
{
    /// <summary>Nothing asked - every verb but one.</summary>
    internal static RecoveryAsk None { get; } = new([], null, null, null, null);
}

/// <summary>
/// The values the configuration verbs carry beside the name, as typed - held together on <see cref="CommandLine"/>
/// since 2026-11-24, when the second of them arrived and that record stood at its ceiling for state.
/// </summary>
internal sealed record WriteValues(StartTypeAsk Setting, RecoveryAsk Recovery)
{
    /// <summary>Nothing asked.</summary>
    internal static WriteValues None { get; } = new(StartTypeAsk.None, RecoveryAsk.None);
}

/// <summary>
/// Turns a <see cref="RecoveryAsk"/> into the change the core plans, or into the sentence saying what is wrong
/// with it - asked by <see cref="Refusals"/> before the manager is opened, and again by the entry point for the
/// value, the way a start type word is read twice.
/// </summary>
internal static class RecoveryAsks
{
    /// <summary>The change, or nothing when the ask is not one - <see cref="Mistake"/> says why.</summary>
    internal static RecoveryChange? Change(RecoveryAsk asked) => Read(asked).Change;

    /// <summary>The sentence for what is wrong with the ask, or nothing when it reads.</summary>
    internal static string? Mistake(RecoveryAsk asked, string serviceName) =>
        Read(asked) is { Mistake: { } key, Word: var word }
            ? Texts.Of(key, word ?? EquivalentCommand.Typed(serviceName))
            : null;

    private static (RecoveryChange? Change, string? Mistake, string? Word) Read(RecoveryAsk asked)
    {
        var (items, itemMistake, itemWord) = ReadItems(asked);

        if (itemMistake is not null)
        {
            return (null, itemMistake, itemWord);
        }

        var parts = asked.Items.Count > 0 ? RecoveryParts.Actions : RecoveryParts.None;
        var reset = TimeSpan.Zero;

        if (asked.Reset is { } typed)
        {
            if (RecoveryCommandWords.ReadPeriod(typed) is not { } period)
            {
                return (null, "cli.recovery.badReset", typed);
            }

            reset = period;
            parts |= RecoveryParts.ResetPeriod;
        }

        bool? errorStops = null;

        if (asked.OnErrorStops is { } yesOrNo)
        {
            errorStops = RecoveryCommandWords.ReadYesNo(yesOrNo);

            if (errorStops is null)
            {
                return (null, "cli.recovery.badYesNo", yesOrNo);
            }

            parts |= RecoveryParts.OnNonCrashFailures;
        }

        parts |= asked.Program is null ? RecoveryParts.None : RecoveryParts.Command;
        parts |= asked.Message is null ? RecoveryParts.None : RecoveryParts.RebootMessage;

        if (parts == RecoveryParts.None)
        {
            return (null, "cli.recovery.nothing", null);
        }

        // An empty program or message is the removal the switch documents, carried as the reading's own word
        // for none - which is what the manager hands back once it is removed.
        var to = new RecoverySettings(
            new RecoveryList(items, reset)
            {
                Command = string.IsNullOrEmpty(asked.Program) ? null : asked.Program,
                RebootMessage = string.IsNullOrEmpty(asked.Message) ? null : asked.Message
            },
            errorStops ?? false);

        return (new RecoveryChange(parts, to), null, null);
    }

    /// <summary>
    /// The items, or the key and word of what is wrong with them. <c>none</c> stands alone: beside an item it
    /// is two lists, and beside <c>--reset</c> a period the manager removes with the list.
    /// </summary>
    private static (List<RecoveryItem> Items, string? Mistake, string? Word) ReadItems(RecoveryAsk asked)
    {
        var items = new List<RecoveryItem>();
        var none = false;

        foreach (var word in asked.Items)
        {
            if (word.Equals(RecoveryCommandWords.NoActions, StringComparison.OrdinalIgnoreCase))
            {
                none = true;
                continue;
            }

            if (RecoveryCommandWords.ReadItem(word) is not { } item)
            {
                return (items, RecoveryCommandWords.NamesAnItem(word) ? "cli.recovery.badDelay" : "cli.recovery.badItem", word);
            }

            items.Add(item);
        }

        if (none && asked.Items.Count > 1)
        {
            return (items, "cli.recovery.noneAlone", null);
        }

        return none && asked.Reset is not null
            ? (items, "cli.recovery.noneWithReset", null)
            : (items, null, null);
    }
}
