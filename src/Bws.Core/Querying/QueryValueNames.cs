namespace Bws.Core.Querying;

/// <summary>
/// The spellings each enumeration field accepts, away from the table of which fields exist.
///
/// <b>Split out of <see cref="QueryFields"/> on 2026-08-18 - backlog 176.</b> That file stood at
/// 559 lines and was the ceiling every other shipped file is measured against, so nothing could
/// be added to the query language at all without moving it. It argues against splitting the FIELD
/// table in its own opening lines, and that argument is right: the point of it is that the list of
/// fields is in one place. This is a different subject.
///
/// <b>What makes it a seam rather than a cut: these lists answer a question nobody asks at the
/// same time as the other one.</b> "Which fields does the language have" is read while writing a
/// query or a document about one. "What may I write after start:" is read while getting one member
/// right. The field table now reads as a table, at one line per property, and each of these is a
/// list of words with the reasoning for the words beside them.
///
/// <b>Nothing here is public.</b> The names ARE a frozen contract - they go out in queries people
/// keep in scripts - but the contract is the strings, and it is guarded by tools/audit/audit.ps1
/// comparing them against docs/07 rather than by the accessibility of this class.
/// </summary>
internal static class QueryValueNames
{
    /// <summary>The spellings <c>type:</c> accepts.</summary>
    internal static IReadOnlyList<QueryValueName> Type { get; } =
    [
        // "driver" covers both driver kinds. Windows keeps drivers and services in
        // one place, so telling them apart is the first thing anyone asks for, and
        // spelling out two values for it would be a poor answer to a common question.
        new QueryValueName("driver", "kerneldriver", "filesystemdriver"),
        new QueryValueName("kernelDriver", "kerneldriver"),
        new QueryValueName("fileSystemDriver", "filesystemdriver"),
        new QueryValueName("ownProcess", "ownprocess"),
        new QueryValueName("sharedProcess", "sharedprocess"),
        new QueryValueName("unknown", "unknown")
    ];

    /// <summary>The spellings <c>peruser:</c> accepts.</summary>
    internal static IReadOnlyList<QueryValueName> PerUser { get; } =
    [
        // "yes" is a group of the two real sides, the same shape as type:driver: somebody
        // asking "is this session noise" does not care which side, and both are askable
        // underneath for drilling in.
        new QueryValueName("yes", "template", "instance"),

        // NO RATHER THAN NONE, and the difference from sidtype:none is the reason.
        //
        // There, "none" is a value the manager reports about a service - it has no service
        // SID. Here, not belonging to the per-user family is not a property an entry carries,
        // it is the answer to a yes-or-no question about which family it is in. Offering both
        // spellings would be two words for one answer, which is what a query language pays for
        // twice: once in the parser and once in every person who has to guess which one works.
        new QueryValueName("no", "none"),

        new QueryValueName("template", "template"),
        new QueryValueName("instance", "instance")
    ];

    /// <summary>The spellings <c>status:</c> accepts.</summary>
    internal static IReadOnlyList<QueryValueName> Status { get; } =
    [
        new QueryValueName("running", "running"),
        new QueryValueName("stopped", "stopped"),
        new QueryValueName("paused", "paused"),

        // FOUR STATES UNDER ONE WORD, the same shape as type:driver and signed:no. Nobody
        // arrives asking whether a service is specifically continue-pending - they ask
        // what is in the middle of something, and that is one question with four answers.
        // Added 2026-08-12 for the chips, and an ADDITION rather than a change of meaning.
        new QueryValueName("pending", "startpending", "stoppending", "pausepending", "continuepending"),

        new QueryValueName("startPending", "startpending"),
        new QueryValueName("stopPending", "stoppending"),
        new QueryValueName("pausePending", "pausepending"),
        new QueryValueName("continuePending", "continuepending"),
        new QueryValueName("unknown", "unknown")
    ];

    /// <summary>The spellings <c>start:</c> accepts.</summary>
    internal static IReadOnlyList<QueryValueName> Start { get; } =
    [
        new QueryValueName("automatic", "automatic"),

        // An alias means the same thing as the word it stands for. "auto" therefore
        // covers delayed entries too, because "automatic" does. Somebody who wants
        // the distinction asks for "delayed", and somebody who wants automatic
        // without delayed writes: start:auto !start:delayed
        new QueryValueName("auto", "automatic"),

        // Not a start type Windows reports. The manager returns the same number, 2,
        // for both, and the delay is a separate piece of configuration. It is a
        // value here because that is how a person thinks about it and how
        // services.msc shows it.
        new QueryValueName("delayed", "delayed"),

        new QueryValueName("manual", "manual"),
        new QueryValueName("disabled", "disabled"),
        new QueryValueName("boot", "boot"),
        new QueryValueName("system", "system"),
        new QueryValueName("unknown", "unknown")
    ];

    /// <summary>The spellings <c>trigger:</c> accepts.</summary>
    internal static IReadOnlyList<QueryValueName> Trigger { get; } =
    [
        new QueryValueName("device", "devicearrival"),
        new QueryValueName("ip", "ipaddress"),
        new QueryValueName("domain", "domainjoin"),
        new QueryValueName("firewall", "firewallport"),
        new QueryValueName("policy", "grouppolicy"),
        new QueryValueName("network", "networkendpoint"),
        new QueryValueName("custom", "custom"),
        new QueryValueName("state", "customsystemstatechange"),
        new QueryValueName("unknown", "unknown"),

        // Not a kind but an action, and worth asking about on its own: a trigger
        // that stops a service is a very different fact from one that starts it.
        new QueryValueName("start", "start"),
        new QueryValueName("stop", "stop")
    ];

    /// <summary>The spellings <c>file:</c> accepts.</summary>
    internal static IReadOnlyList<QueryValueName> File { get; } =
    [
        new QueryValueName("present", "present"),
        new QueryValueName("missing", "missing")
    ];

    /// <summary>The spellings <c>signed:</c> accepts.</summary>
    internal static IReadOnlyList<QueryValueName> Signed { get; } =
    [
        new QueryValueName("yes", "trusted"),
        new QueryValueName("no", "notsigned", "untrustedroot", "expired", "revoked", "tampered"),
        new QueryValueName("trusted", "trusted"),
        new QueryValueName("notSigned", "notsigned"),
        new QueryValueName("untrustedRoot", "untrustedroot"),
        new QueryValueName("expired", "expired"),
        new QueryValueName("revoked", "revoked"),
        new QueryValueName("tampered", "tampered"),

        // Deliberately not inside "no". A result this code could not name is not a
        // finding about the file, it is a gap in our naming, and sweeping it in with
        // the untrusted ones would turn our own ignorance into an accusation.
        new QueryValueName("unknown", "unknown")
    ];

    /// <summary>The spellings <c>sidtype:</c> accepts.</summary>
    internal static IReadOnlyList<QueryValueName> Sidtype { get; } =
    [
        new QueryValueName("unrestricted", "unrestricted"),
        new QueryValueName("restricted", "restricted"),
        new QueryValueName("unknown", "unknown")
    ];

    /// <summary>The spellings <c>mismatch:</c> accepts.</summary>
    internal static IReadOnlyList<QueryValueName> Mismatch { get; } =
    [
        // TWO VALUES THAT ARE OPPOSITES RATHER THAN A NARROW ONE AND A WIDE ONE, which is why
        // there is no third spelling covering both. `any` and `none` already do that, and they are
        // the reserved words every enumeration field has - so the pair here stays sharp.
        new QueryValueName("stopped", "stopped"),
        new QueryValueName("running", "running")
    ];
}
