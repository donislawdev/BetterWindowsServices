namespace Bws.Gui.ViewModels;

/// <summary>
/// Which field of the query language each column is about.
///
/// <b>Built 2026-09-05 for the menu under a right click on a heading</b> - the owner's ask was to
/// click Status and choose Running or Stopped without learning the language first. The answer that
/// menu needs is in two halves: which FIELD this column is, which is here, and what that field
/// ACCEPTS, which is <c>QueryFields.ValuesOf</c> in the core and was opened the same day.
///
/// <b>ONE MAP RATHER THAN A PROPERTY ON EACH COLUMN, and the argument is the one Columns.Groups
/// already makes about the picker's headings.</b> A mapping is only worth having if somebody can
/// look at the whole of it at once and ask whether it is right - spread over twenty-seven
/// declarations, each entry becomes a local decision and nobody notices that two columns claim the
/// same field or that a column claims one it is not about. Both of those are here on purpose and
/// both are visible from one screen.
///
/// <b>WHY IT IS NOT DERIVED FROM THE NAMES, which is the shortcut this deliberately refuses.</b>
/// Eleven of these agree with the column identifier and the rest do not: the start type column is
/// <c>start</c>, the process id is <c>pid</c>, the signature is <c>signed</c>, and the two
/// mismatch columns are one field between them. A rule with sixteen exceptions is a lookup table
/// written twice.
///
/// <b>EVERY COLUMN THAT HAS A FIELD IS HERE, not only the ones that will draw a menu.</b> Whether
/// there is anything to offer is decided by the field, at the moment of asking - a text field
/// answers with an empty list and the menu shows no values. Listing only the enumerations here
/// would make a column left out look the same as a column forgotten, and a guard could not tell
/// the difference either.
/// </summary>
internal static partial class Columns
{
    /// <summary>
    /// The query field each column is about, for the columns that are about one.
    ///
    /// <b>What is deliberately absent, said rather than left to be noticed:</b> the delayed start,
    /// the file version, the hash, the load order group, the error control and the list of
    /// dependencies. The first is a QUALIFIER on the start type rather than a field - it is
    /// <c>start:delayed</c>, one value of a field this map would then be pointing a whole menu at.
    /// The other five have no field in the language at all, which is a fact about `docs/07` rather
    /// than an oversight here, and a guard says so.
    /// </summary>
    private static readonly Dictionary<string, string> Fields = new(StringComparer.Ordinal)
    {
        ["serviceName"] = "name",
        ["displayName"] = "display",
        ["description"] = "description",
        ["status"] = "status",
        ["startType"] = "start",
        ["account"] = "account",
        ["processId"] = "pid",
        ["entryType"] = "type",
        ["perUserRole"] = "peruser",
        ["binaryOnDisk"] = "file",
        ["binaryPath"] = "path",
        ["binaryFile"] = "path",
        ["signature"] = "signed",
        ["publisher"] = "publisher",
        ["memory"] = "memory",
        ["sidType"] = "sidtype",
        ["triggers"] = "trigger",
        ["requiredPrivileges"] = "privilege",
        ["securityDescriptor"] = "sddl",

        // Both directions of one relation, and both take a service NAME rather than a set of
        // words - so a heading menu over either offers no values and ends at the last item. The
        // pair is here anyway, because the map answers "which field is this column" and that has
        // an answer for both of them.
        ["dependsOn"] = "dependson",
        ["requiredBy"] = "requiredby",

        // TWO COLUMNS, ONE FIELD, AND THAT IS THE LANGUAGE'S OWN DECISION rather than a shortcut
        // taken here - backlog 170 and 172. A service stopped while set to start and a service
        // running while disabled are opposite faults with opposite remedies, so they are two
        // columns. They are one field because a member has to be readable: mismatch:stopped is the
        // first and mismatch:running is the second, and QuerySymbols.MismatchSymbols is where that
        // pairing lives.
        //
        // So a right click on either heading offers both directions. That is the honest menu: the
        // question this column is about has two answers and the other column shows the other one.
        ["runsAgainstItsStartType"] = "mismatch",
        ["runsWhileDisabled"] = "mismatch"
    };

    /// <summary>The query field this column is about, or nothing when it is about none.</summary>
    internal static string? FieldOf(string id) => Fields.GetValueOrDefault(id);
}
