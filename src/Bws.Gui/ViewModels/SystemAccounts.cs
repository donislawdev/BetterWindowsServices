using Bws.Core;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The three accounts most services run as, and the names a person knows them by.
///
/// <b>WHY THIS EXISTS.</b> The account column showed what the manager holds, and on the owner's
/// machine that was <c>NT AUTHORITY\LocalService</c> cut to "NT AUTHORITY\Loca..." in a column
/// wide enough for anything else - the part that tells Local Service from Network Service was the
/// part that gave way. Worse, measured on 2026-09-15 over 336 services: the same account arrives
/// in TWO spellings, <c>LocalSystem</c> 197 times and <c>localSystem</c> 18, <c>NT AUTHORITY\</c>
/// 72 times and <c>NT Authority\</c> 19, so one account sorted apart from itself and read as two.
/// services.msc says "Local System", "Local Service" and "Network Service" in its own column, and
/// that is the tool an administrator compares this one with - `CLAUDE.md`, verification against
/// the system.
///
/// <b>A TABLE OF SPELLINGS RATHER THAN A LOOKUP OF THE SID, AND THE REASON IS RULE 10 BEFORE IT
/// IS COST.</b> Rule 3 of the project notes wants identity by SID, and the honest way to know
/// that a string is S-1-5-19 is to ask Windows. But the call that answers - LookupAccountName -
/// goes to a domain controller for a name it does not know locally, and a service running as a
/// domain account would have this window making a network call per row. Rule 10 forbids exactly
/// that. So this is the three spellings Microsoft documents for CreateService, compared without
/// case, and nothing else: an account outside the table shows the manager's spelling untouched,
/// which is what every account showed before this file existed.
///
/// <b>WHAT IS DELIBERATELY NOT IN THE TABLE, SAID SO THAT NOBODY ADDS IT ON A GUESS.</b> The
/// forms with a space - "NT AUTHORITY\Local Service" - are what the account is CALLED and are
/// accepted by the lookup above, but whether the manager accepts them when a service is created
/// was not checked and is not assumed. A spelling left out costs the person nothing but the label:
/// the cell shows the spelling and the tooltip is not needed. A spelling wrongly put in would
/// name an account the service does not run as.
///
/// <b>Presentation, so it lives here and not in the core.</b> The command line prints and
/// serialises the manager's spelling, and <c>account:</c> in the query language matches it - the
/// tooltip on the cell says so, because a person who reads "Local Service" and types it into the
/// box would otherwise get an empty list and no idea why.
/// </summary>
internal static class SystemAccounts
{
    /// <summary>
    /// What the account cell shows: the known name of a system account, the manager's own spelling
    /// for every other account, and the four states of a reading exactly as every other cell says
    /// them - <see cref="CellFaces.Say"/> is the one place those words are decided.
    /// </summary>
    internal static string Shown(Reading<string> account) =>
        CellFaces.Say(account, value => LabelOf(value) ?? value);

    /// <summary>
    /// What the manager holds behind a shown name, or nothing when the cell already shows the
    /// spelling itself.
    ///
    /// Nothing rather than the spelling for an account outside the table, because the tooltip and
    /// the details line built on this would otherwise repeat a cell that is already telling the
    /// whole truth - the rule <see cref="CellTips"/> states for every trimmed cell.
    /// </summary>
    internal static string? Held(Reading<string> account) =>
        account.Outcome == ReadOutcome.Present
            && account.Value is { } spelling
            && LabelOf(spelling) is not null
            ? spelling
            : null;

    /// <summary>
    /// The known name of a system account, or nothing for an account that is not one.
    ///
    /// Three comparisons rather than a dictionary, so that each key is written at the call that
    /// asks for it - <c>TextKeyGuards</c> reads keys where they are chosen and a key held in a
    /// table would be one it could not see. Compared without case, because the registry holds
    /// both cases of each - see the measurement at the head of this file.
    /// </summary>
    internal static string? LabelOf(string account)
    {
        if (Spelled(account, "LocalSystem"))
        {
            return Texts.Of("gui.cell.account.localSystem");
        }

        if (Spelled(account, @"NT AUTHORITY\LocalService"))
        {
            return Texts.Of("gui.cell.account.localService");
        }

        if (Spelled(account, @"NT AUTHORITY\NetworkService"))
        {
            return Texts.Of("gui.cell.account.networkService");
        }

        return null;
    }

    private static bool Spelled(string account, string documented) =>
        string.Equals(account, documented, StringComparison.OrdinalIgnoreCase);
}
