namespace Bws.Core.Snapshots;

/// <summary>
/// What makes this comparison less than exact, as facts rather than sentences.
///
/// Facts because the core does not write anything a person reads - `ADR-3` keeps it from
/// knowing an interface exists, and rule 13 keeps user-facing wording out of code entirely.
/// Whoever displays this turns the flags into words in their own language.
///
/// <b>Its own file since 2026-09-30, when it grew from four facts to seven</b> - stability report
/// D-5, package E. The rule that works them out moved here with it, so that the method matching
/// the two sides' entries does not also decide what the two sides' metadata means.
/// </summary>
/// <param name="OperatingSystemDiffers">
/// The two were taken on different Windows, counting the monthly update when both files say which
/// one. <b>Wider since 2026-09-30:</b> until then it compared only the text that ends in ".0" where
/// the update belongs, so a machine either side of an update compared as the same Windows.
/// </param>
/// <param name="LanguageDiffers">
/// The two managers name things in different languages - both files say which, and they disagree.
/// Display names and descriptions are then not compared on any entry, because every translated one
/// would differ without anybody having changed anything.
/// </param>
/// <param name="AccountDiffers">
/// Different accounts took the two. What one account may read another may be refused, and the
/// language decision of 2026-09-30 leans on this too: whether the manager follows the caller's
/// own display language was NOT measured, and two accounts on one machine are where it would show.
/// Compared without case, because Windows compares account names that way.
/// </param>
/// <param name="NotKnown">
/// Metadata fields at least one of the two files does not carry, so the caveat each one feeds could
/// not be decided either way - camelCase names, as they are written in the file. A version four
/// file carries neither of the two added in version five, and saying so is the difference between
/// "the same" and "nobody could tell".
/// </param>
public sealed record ComparisonCaveats(
    bool ElevationDiffers,
    bool MachineDiffers,
    bool OperatingSystemDiffers,
    bool ToolVersionDiffers,
    bool LanguageDiffers,
    bool AccountDiffers,
    IReadOnlyList<string> NotKnown)
{
    /// <summary>The field name of <see cref="SnapshotMetadata.OperatingSystemVersion"/> in the file.</summary>
    internal const string VersionField = "operatingSystemVersion";

    /// <summary>The field name of <see cref="SnapshotMetadata.NamesLanguage"/> in the file.</summary>
    internal const string LanguageField = "namesLanguage";

    /// <summary>
    /// The caveats two snapshots' metadata add up to.
    ///
    /// <b>Two fields decided three ways, and "not known" is the one that is easy to lose.</b> When
    /// both sides carry a value the answer is same or different. When either side does not, the
    /// answer is neither - it goes on <see cref="NotKnown"/> and the flag stays false, so nothing
    /// that could not be checked is reported as a difference and nothing is reported as checked.
    /// </summary>
    internal static ComparisonCaveats Between(SnapshotMetadata before, SnapshotMetadata after)
    {
        var notKnown = new List<string>();

        if (before.OperatingSystemVersion is null || after.OperatingSystemVersion is null)
        {
            notKnown.Add(VersionField);
        }

        if (before.NamesLanguage is null || after.NamesLanguage is null)
        {
            notKnown.Add(LanguageField);
        }

        return new ComparisonCaveats(
            before.Elevated != after.Elevated,
            !string.Equals(before.Machine, after.Machine, StringComparison.OrdinalIgnoreCase),
            !string.Equals(before.OperatingSystem, after.OperatingSystem, StringComparison.Ordinal)
                || Disagree(before.OperatingSystemVersion, after.OperatingSystemVersion, StringComparison.Ordinal),
            !string.Equals(before.Tool, after.Tool, StringComparison.Ordinal),
            Disagree(before.NamesLanguage, after.NamesLanguage, StringComparison.OrdinalIgnoreCase),
            !string.Equals(before.TakenBy, after.TakenBy, StringComparison.OrdinalIgnoreCase),
            notKnown);
    }

    /// <summary>Both known and different. Either one missing is not a disagreement - it is not knowing.</summary>
    private static bool Disagree(string? before, string? after, StringComparison comparison) =>
        before is not null && after is not null && !string.Equals(before, after, comparison);
}
