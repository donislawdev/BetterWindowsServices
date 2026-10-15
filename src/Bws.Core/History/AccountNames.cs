using System.Security.Principal;
using System.Text.RegularExpressions;

namespace Bws.Core.History;

/// <summary>
/// Names for the accounts in a change history - only the ones this machine can name on its own.
///
/// <b>WHY NOT EVERY ACCOUNT, AND IT IS A RULE RATHER THAN A PREFERENCE.</b> Translating a SID is a
/// LookupAccountSid underneath, and for an account of a domain Windows answers it by asking a domain
/// controller - a round trip off this machine caused by this program, which ADR-19 and rule 10 of
/// CLAUDE.md forbid. Owner's decision J1-c, 2026-10-15: well-known accounts (SYSTEM, the service
/// accounts, the built-in groups) and accounts of this machine's own account domain are named, every
/// other SID is shown as itself. On the owner's machine and on the throwaway one every SID in the
/// events was one of the first two kinds, so the difference does not show there - on a computer in a
/// domain it is NOT MEASURED.
///
/// <b>Nothing is caught here.</b> The collection form of Translate with forceSuccess off hands back an
/// account it could not name as the SID it was given, so "no name" is an answer rather than an
/// exception.
/// </summary>
public static partial class AccountNames
{
    /// <summary>
    /// Whether Windows can name this SID without leaving the machine.
    ///
    /// <b>The NT authority only (S-1-5), and within it anything that is not an account of some domain
    /// plus the accounts of this machine's own one.</b> Under S-1-5 sit SYSTEM, LOCAL SERVICE, NETWORK
    /// SERVICE, the BUILTIN groups and the per-service SIDs - all answered by this machine - and the
    /// S-1-5-21 accounts, whose domain decides where the answer comes from. Every other authority is
    /// left alone, which includes S-1-12, the accounts of Microsoft Entra ID.
    /// </summary>
    /// <param name="sid">The account to name.</param>
    /// <param name="localDomain">
    /// The SID of this machine's own account domain, or null when it could not be read - and then
    /// only the accounts that belong to no domain are named.
    /// </param>
    public static bool NamedWithoutTheNetwork(SecurityIdentifier sid, SecurityIdentifier? localDomain)
    {
        ArgumentNullException.ThrowIfNull(sid);

        if (!sid.Value.StartsWith("S-1-5-", StringComparison.Ordinal))
        {
            return false;
        }

        var domain = sid.AccountDomainSid;

        return domain is null || (localDomain is not null && domain == localDomain);
    }

    /// <summary>
    /// The names Windows gives the SIDs this machine can name, keyed by the SID as it was written.
    /// A SID absent from the answer has no name here, and stays itself on the screen.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Of(IEnumerable<string?> sids, SecurityIdentifier? localDomain)
    {
        ArgumentNullException.ThrowIfNull(sids);

        var asked = new IdentityReferenceCollection();

        // Shaped like a SID before it is handed to the constructor, which throws on anything else -
        // and a SID in an event is text written by Windows, read back by this program, and not
        // something to trust to be well formed.
        var nameable = sids
            .Distinct(StringComparer.Ordinal)
            .Where(text => text is not null && SidShape().IsMatch(text))
            .Select(text => new SecurityIdentifier(text!))
            .Where(sid => NamedWithoutTheNetwork(sid, localDomain));

        foreach (var sid in nameable)
        {
            asked.Add(sid);
        }

        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        if (asked.Count == 0)
        {
            return names;
        }

        var answered = asked.Translate(typeof(NTAccount), forceSuccess: false);

        // In the order asked: the collection translates position by position, and an account it
        // could not name comes back in its place as the SID it was.
        for (var index = 0; index < asked.Count; index++)
        {
            if (answered[index] is NTAccount account)
            {
                names[asked[index].Value] = account.Value;
            }
        }

        return names;
    }

    [GeneratedRegex(@"^S-1-\d+(-\d+)+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex SidShape();
}
