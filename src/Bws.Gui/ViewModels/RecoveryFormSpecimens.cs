using Bws.Core;
using Bws.Core.Planning;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The recovery form in the states the catalogue draws it in (GUI rule 4, backlog 555 part a2, 2026-11-24): one
/// entry with two parts edited, one with nothing set, a delay that does not read, and three entries that differ
/// on every part. Out of <see cref="Catalogue"/>, which stands at the share of its ceiling of members the shape
/// guard counts as near.
/// </summary>
internal static class RecoveryFormSpecimens
{
    /// <summary>A list most services have: restart, restart, nothing, a day.</summary>
    private static readonly RecoverySettings Usual = new(
        new RecoveryList(
            [
                new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromMinutes(1)),
                new RecoveryItem(RecoveryAction.RestartService, TimeSpan.FromMinutes(1)),
                new RecoveryItem(RecoveryAction.Nothing, TimeSpan.Zero)
            ],
            TimeSpan.FromDays(1)),
        false);

    /// <summary>One entry with its first delay and its period changed, so two parts are ticked.</summary>
    internal static RecoveryForm Edited()
    {
        var form = One(Usual);

        form.Items[0].DelayText = "2m";
        form.PeriodText = "2h";

        return form;
    }

    /// <summary>One entry with nothing set - an empty list, no texts.</summary>
    internal static RecoveryForm Empty() => One(RecoveryChange.Nothing);

    /// <summary>A delay and a period that do not read, each saying so under its box.</summary>
    internal static RecoveryForm Mistyped()
    {
        var form = One(Usual);

        form.Items[1].DelayText = "5x";
        form.PeriodText = "a day";

        return form;
    }

    /// <summary>
    /// Three entries that differ on the flag and one of them unread, so every part says it is not the same on
    /// all - with the longest text the catalogue has as the program set for all of them.
    /// </summary>
    internal static RecoveryForm Differing(string longest)
    {
        var form = RecoveryForm.Of(
        [
            Reading<RecoverySettings>.Present(Usual),
            Reading<RecoverySettings>.Present(Usual with { OnNonCrashFailures = true }),
            Reading<RecoverySettings>.Denied(5, "Access is denied.")
        ]);

        form.ProgramText = longest;
        form.Flag = true;

        return form;
    }

    private static RecoveryForm One(RecoverySettings settings) =>
        RecoveryForm.Of([Reading<RecoverySettings>.Present(settings)]);
}
