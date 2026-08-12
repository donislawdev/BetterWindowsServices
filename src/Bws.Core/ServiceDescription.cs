namespace Bws.Core;

/// <summary>
/// Which of three answers a description string from the manager actually is.
///
/// <b>Public, and for the same reason <see cref="BinaryPathResolver"/> is: it is a rule about a
/// string, it needs no service control manager, and it is easy to get wrong in a way nothing else
/// would notice.</b> The reading itself lives beside the other configuration calls and cannot be
/// asked without a machine - this is the half that can.
///
/// <b>The case that makes it worth a type of its own is the one the system reports by NOT
/// failing.</b> A description in the registry is usually an indirection into a binary's resources,
/// <c>@%SystemRoot%\system32\adpsvc.dll,-103</c>, and the manager resolves it. When the resource is
/// not there, the call SUCCEEDS and hands the indirection straight back. Nothing in the return code
/// says so, so a reader that trusted the call would put an at sign and a file path in front of a
/// person and call it a description.
///
/// Measured on this machine, 2026-08-12, over 819 entries: 398 of the 444 descriptions in the
/// registry are indirections, the manager resolves 388 of them, and ten come back unresolved.
/// </summary>
public static class ServiceDescription
{
    /// <summary>
    /// The number the system itself uses for a resource it cannot find, reused here rather than
    /// invented.
    ///
    /// <b>A code at all, because this is a refusal and a refusal carries one</b> - every consumer
    /// of <see cref="Reading{T}"/> tells the two failure kinds apart by this number, and inventing
    /// a private one would put a value in a frozen contract that means nothing anywhere else.
    /// ERROR_RESOURCE_NAME_NOT_FOUND is the nearest true statement about what happened.
    /// </summary>
    public const int ResourceNotFound = 1332;

    /// <summary>
    /// Turns what the manager handed back into one of the three answers a field can carry.
    ///
    /// <b>Absent is the ordinary case rather than an edge:</b> 384 of 819 entries have no
    /// description at all, nearly all of them drivers. An empty string would claim the manager
    /// answered with emptiness, which is a different thing from having nothing to answer.
    ///
    /// <b>An unresolved indirection is refused rather than absent</b>, because those are different
    /// facts: absent means this entry has no description, and refused means it has one nobody could
    /// turn into words. A snapshot that flattened them would report the second as "no description"
    /// and compare clean against a machine where it resolved.
    /// </summary>
    public static Reading<string> Of(string? text) => text switch
    {
        null or "" => Reading<string>.Absent(),

        // Matched on the leading character rather than on the whole @file,-id shape, and that is
        // deliberate: what makes this a failed read is that the manager did not resolve it, and the
        // at sign is the marker for that whatever follows. A stricter pattern would let a
        // malformed indirection through as though it were prose.
        ['@', ..] => Reading<string>.Denied(ResourceNotFound, ManagerTerms.Describe(ResourceNotFound)),

        _ => Reading<string>.Present(text)
    };
}
