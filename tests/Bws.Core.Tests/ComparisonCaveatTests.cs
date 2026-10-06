using System.Globalization;
using Bws.Core.Snapshots;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// What the two snapshots' metadata add up to, schema five - stability report D-5, owner's
/// decisions of 2026-09-30.
///
/// <b>Three answers for each of the two added fields, and the third is the one that is easy to
/// lose:</b> the same, different, or not known because one file does not carry it. A caveat that
/// turned "not known" into "the same" would be the silence rule 8 forbids, and one that turned it
/// into "different" would invent a reason to distrust a comparison that is fine.
/// </summary>
public sealed class ComparisonCaveatTests
{
    [Fact]
    public void Two_updates_of_one_build_are_different_windows()
    {
        // The text written since the format began ends in ".0" on both sides, so until schema five
        // these two compared as taken on the same Windows.
        var caveats = Caveats(Metadata(version: "10.0.26200.9550"), Metadata(version: "10.0.26200.9551"));

        Assert.True(caveats.OperatingSystemDiffers);
        Assert.Empty(caveats.NotKnown);
    }

    [Fact]
    public void The_same_update_on_both_sides_is_the_same_windows()
    {
        Assert.False(Caveats(Metadata(), Metadata()).OperatingSystemDiffers);
    }

    [Fact]
    public void An_update_nobody_recorded_is_not_known_rather_than_different()
    {
        var caveats = Caveats(Metadata(version: null), Metadata());

        Assert.False(caveats.OperatingSystemDiffers);
        Assert.Equal(["operatingSystemVersion"], caveats.NotKnown);
    }

    [Fact]
    public void Two_languages_leave_the_names_out_of_every_entry()
    {
        var diff = Between(
            Metadata(language: "en-US"),
            Metadata(language: "pl-PL"),
            Entries.Any with { DisplayName = "Bufor wydruku" });

        Assert.True(diff.Caveats.LanguageDiffers);

        // Not a change and not "not fully compared" on the entry either - the caveat says it once,
        // rather than the same admission on every row of eight hundred.
        Assert.Empty(diff.Changed);
        Assert.Empty(diff.NotFullyCompared);
    }

    [Fact]
    public void One_language_on_both_sides_still_compares_the_names()
    {
        var diff = Between(Metadata(), Metadata(), Entries.Any with { DisplayName = "Something else" });

        Assert.Equal("displayName", Assert.Single(Assert.Single(diff.Changed).Differences).Field);
    }

    [Fact]
    public void A_language_nobody_recorded_still_compares_the_names_and_says_it_could_not_check()
    {
        // The owner's decision for a version four file: compared as before, and one line saying
        // what the older file does not record.
        var diff = Between(Metadata(language: null), Metadata(), Entries.Any with { DisplayName = "Something else" });

        Assert.False(diff.Caveats.LanguageDiffers);
        Assert.Equal(["namesLanguage"], diff.Caveats.NotKnown);
        Assert.Single(diff.Changed);
    }

    [Theory]
    [InlineData(@"TESTBOX\somebody", false)]
    [InlineData(@"testbox\SOMEBODY", false)]
    [InlineData(@"TESTBOX\someone", true)]
    public void A_different_account_is_said_and_different_capitals_are_not_one(string takenBy, bool differs)
    {
        Assert.Equal(differs, Caveats(Metadata(), Metadata(takenBy: takenBy)).AccountDiffers);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public void A_schema_outside_the_range_this_build_reads_is_refused_and_the_range_is_named(int version)
    {
        var text = SnapshotJson.Render(Snapshot.Of(Specimens.All, note: null, new FakeClock()))
            .Replace($"\"schemaVersion\": {Snapshot.CurrentSchemaVersion}", $"\"schemaVersion\": {version}", StringComparison.Ordinal);

        Assert.False(SnapshotJson.TryRead(text, out _, out var failure));
        Assert.Contains("versions 4 to 6", failure!, StringComparison.Ordinal);
    }

    [Fact]
    public void This_machine_says_its_update_and_the_language_its_manager_names_things_in()
    {
        // Asked of the machine the tests run on, because the two readers ARE the machine - a fake
        // here would test the fake. Windows keeps an update number on every supported release.
        var version = Environment.OSVersion.Version;
        var written = SystemFacts.OperatingSystemVersion();

        // The update read here a second way, through the same documented value, because a fourth part
        // that merely parses would let ".0" through - the very thing Environment.OSVersion writes.
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        var update = Assert.IsType<int>(key?.GetValue("UBR"));

        Assert.Equal(
            string.Create(CultureInfo.InvariantCulture, $"{version.Major}.{version.Minor}.{version.Build}.{update}"),
            written);

        var language = SystemFacts.NamesLanguage();

        Assert.NotNull(language);
        Assert.Equal(language, CultureInfo.GetCultureInfo(language).Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Metadata a test controls, with the two schema five fields known unless a test says otherwise.</summary>
    internal static SnapshotMetadata Metadata(
        string? version = "10.0.26200.9550", string? language = "pl-PL", string takenBy = @"TESTBOX\somebody") =>
        new()
        {
            SchemaVersion = Snapshot.CurrentSchemaVersion,
            Machine = "TESTBOX",
            OperatingSystem = "Microsoft Windows NT 10.0.26200.0",
            TakenAt = DateTimeOffset.UnixEpoch,
            TakenBy = takenBy,
            Elevated = true,
            Note = null,
            Tool = "0.1.0",
            OperatingSystemVersion = version,
            NamesLanguage = language
        };

    private static ComparisonCaveats Caveats(SnapshotMetadata before, SnapshotMetadata after) =>
        ComparisonCaveats.Between(before, after);

    private static SnapshotDiff Between(SnapshotMetadata before, SnapshotMetadata after, ScmEntry later)
    {
        var earlier = Snapshot.Of([Entries.Any], note: null, new FakeClock()) with { Metadata = before };
        var now = Snapshot.Of([later], note: null, new FakeClock()) with { Metadata = after };

        Assert.True(SnapshotDiff.TryBetween(earlier, now, out var diff, out var failure), failure);

        return diff;
    }
}
