using System.Text.Json;

namespace Bws.Integration.Tests;

/// <summary>
/// What the manager says about privileges, identities and permissions, checked against
/// sc.exe.
///
/// Three commands to compare with rather than one - qprivs, qsidtype and sdshow - because
/// this is three questions that happen to be read together, and a single comparison would
/// leave two of them unchecked. services.msc shows none of the three.
///
/// Read-only. Every call here asks and nothing tells.
/// </summary>
public sealed class PermissionContractTests
{
    [Fact]
    public void Our_privileges_are_the_ones_sc_lists_in_the_order_sc_lists_them()
    {
        // Order included on purpose. The manager returns a multi-string and the obvious
        // mistake with one of those is to stop at the first value - a bug that leaves the
        // list non-empty, so a test asking only "does it have privileges" would pass while
        // reporting one privilege for a service that declares twenty-eight.
        var sampled = 0;

        foreach (var entry in CommandLineTool.Listing("--query", "privilege:any").Take(6))
        {
            var name = CommandLineTool.Text(entry, "serviceName");

            var ours = entry.GetProperty("requiredPrivileges")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray();

            Assert.Equal(PrivilegesAccordingToServiceControl(name), ours);
            sampled++;
        }

        Assert.Equal(6, sampled);
    }

    [Fact]
    public void An_entry_we_say_declares_none_declares_none_according_to_sc()
    {
        // The other direction, which is the one that would hide a reading that quietly
        // returned nothing for everybody. It matters more here than elsewhere, because
        // "declares no privileges" is the permissive answer: a service asking for nothing
        // keeps everything its account has, so a false empty reads as reassuring.
        var sampled = 0;

        foreach (var entry in CommandLineTool.Listing("--query", "privilege:none !type:driver").Take(6))
        {
            Assert.Empty(PrivilegesAccordingToServiceControl(CommandLineTool.Text(entry, "serviceName")));
            sampled++;
        }

        Assert.Equal(6, sampled);
    }

    [Fact]
    public void Our_identity_kind_is_the_one_sc_reports()
    {
        // Both kinds and the absence, in one pass, because the three are read by the same
        // call and a mapping that got one wrong would usually get it wrong in a way that
        // looks like another. Restricted is the rare one - 11 services on the machine this
        // was written on - so it is asked for by name rather than hoped for in a sample.
        foreach (var query in new[] { "sidtype:unrestricted", "sidtype:restricted", "sidtype:none !type:driver" })
        {
            var sampled = 0;

            foreach (var entry in CommandLineTool.Listing("--query", query).Take(4))
            {
                var name = CommandLineTool.Text(entry, "serviceName");

                var ours = entry.GetProperty("sidType").GetString() ?? "none";

                Assert.Equal(SidTypeAccordingToServiceControl(name), ours);
                sampled++;
            }

            Assert.True(sampled > 0, $"Nothing on this machine answered {query}, so nothing was checked.");
        }
    }

    [Fact]
    public void Our_permissions_are_the_ones_sc_shows_and_our_differences_are_the_two_we_intended()
    {
        // The strongest guard in this file, because it is an equality against the system
        // rather than a count. Two differences are deliberate and both are asserted here
        // rather than described in a comment somebody could stop believing:
        //
        //   we carry the owner and the group, which sc sdshow does not print
        //   sc sdshow carries the audit list, which we deliberately do not read
        //
        // Anything else differing is a bug, and this is the shape of test that would find
        // it: strip the two known differences and demand the rest match character for
        // character.
        var sampled = 0;

        foreach (var entry in CommandLineTool.Listing("--query", "sddl:any").Take(8))
        {
            var name = CommandLineTool.Text(entry, "serviceName");
            var ours = CommandLineTool.Text(entry, "securityDescriptor");

            Assert.StartsWith("O:", ours, StringComparison.Ordinal);
            Assert.Contains("G:", ours, StringComparison.Ordinal);
            Assert.DoesNotContain("S:", PermissionsPart(ours), StringComparison.Ordinal);

            Assert.Equal(
                PermissionsAccordingToServiceControl(name),
                PermissionsPart(ours));

            sampled++;
        }

        Assert.Equal(8, sampled);
    }

    [Fact]
    public void Reading_the_permissions_costs_no_other_field()
    {
        // The mistake this family invites, and the one measurement that decided its design:
        // the descriptor needs READ_CONTROL, which is a right the listing does not otherwise
        // hold. Asking for it on the same handle as the configuration was measured under a
        // restricted token to lose five entries their start type, account and launch path.
        //
        // On an elevated machine nothing refuses, so this cannot be checked by counting
        // refusals. It is checked as a property instead: whatever the descriptor did, every
        // entry still answers the questions it answered before this family existed.
        var listing = CommandLineTool.Listing();

        Assert.All(listing, entry =>
        {
            var refused = entry.TryGetProperty("unreadable", out var unreadable)
                && unreadable.TryGetProperty("startType", out _);

            Assert.False(
                refused,
                $"{CommandLineTool.Text(entry, "serviceName")} could not report its start type. " +
                "If that arrived with the permissions, the descriptor is being read on the " +
                "configuration handle instead of one of its own.");
        });

        Assert.True(listing.Length > 100);
    }

    [Fact]
    public void Privileges_and_identities_are_a_real_part_of_the_machine()
    {
        // Not a number pinned to one install, but the shape the slice rests on. Measured on
        // the machine this was written on: 215 services of 339 declare a privilege and 261
        // have an identity of their own. A reading that answered for a handful and gave up
        // would land far below either.
        var withPrivileges = CommandLineTool.Listing("--query", "privilege:any").Length;
        var withIdentity = CommandLineTool.Listing("--query", "sidtype:any").Length;

        Assert.True(withPrivileges > 50, $"Only {withPrivileges} entries declared a privilege.");
        Assert.True(withIdentity > 50, $"Only {withIdentity} entries had an identity of their own.");
    }

    /// <summary>
    /// Everything after the permissions marker, which is the part sc.exe and we both have.
    /// </summary>
    private static string PermissionsPart(string descriptor)
    {
        var start = descriptor.IndexOf("D:", StringComparison.Ordinal);

        return start < 0 ? descriptor : descriptor[start..];
    }

    /// <summary>
    /// What sc.exe shows, with the audit list taken off. sc prints owner and group for
    /// nothing and the audit list for everything, so the comparable part is between the
    /// permissions marker and the audit one.
    /// </summary>
    private static string PermissionsAccordingToServiceControl(string serviceName)
    {
        var shown = CommandLineTool.ServiceControl("sdshow", serviceName).StandardOutput.Trim();
        var audit = shown.IndexOf("S:", StringComparison.Ordinal);

        return audit < 0 ? shown : shown[..audit];
    }

    /// <summary>
    /// The privileges sc.exe lists, in its order. Every line after the first carries one,
    /// introduced by a colon, and the first also holds the label.
    /// </summary>
    private static string[] PrivilegesAccordingToServiceControl(string serviceName) =>
    [
        .. CommandLineTool.ServiceControl("qprivs", serviceName).StandardOutput
            .Split('\n')
            .Where(line => line.Contains("PRIVILEGES", StringComparison.Ordinal) || line.TrimStart().StartsWith(':'))
            .Select(line => line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim())
            .Where(value => value.Length > 0)
    ];

    /// <summary>
    /// The identity kind sc.exe reports, in our spelling. sc shouts it and we do not, which
    /// is the only difference between the two vocabularies.
    /// </summary>
    private static string SidTypeAccordingToServiceControl(string serviceName)
    {
        var line = CommandLineTool.ServiceControl("qsidtype", serviceName).StandardOutput
            .Split('\n')
            .FirstOrDefault(text => text.Contains("SERVICE_SID_TYPE", StringComparison.Ordinal))
            ?? string.Empty;

        return line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim().ToLowerInvariant();
    }
}
