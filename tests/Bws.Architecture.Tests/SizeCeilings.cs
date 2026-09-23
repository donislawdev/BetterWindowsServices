namespace Bws.Architecture.Tests;

/// <summary>
/// The six numbers the file ratchet holds, and the record of why each stands where it does.
///
/// <b>LINES OF CODE SINCE 2026-09-23, AND EVERY NUMBER HERE STARTED AGAIN THAT DAY.</b> Until then
/// these were raw lines - comments and blank lines included - and this file carried four hundred
/// lines of their history, from 807 on 2026-08-02 down to 488. That history describes numbers that
/// no longer exist, so it is not kept beside these ones. It is in this file's git history as of
/// commit 4353527, which is where to read how each seam was found.
///
/// <b>Four lessons from that history still hold, and they are the reason this file looks the way
/// it does:</b>
///
///   1. Every step down was forced by the ceiling, never done for tidiness, and every split followed
///      a seam that was already there once somebody was made to look for one. That is the whole
///      argument for a ceiling.
///   2. Sessions shortened a fresh comment to fit under the number at least three times - and the
///      theme's values file was "cured" three times in one session by deleting its own reasoning.
///      That is what counting only lines of code ends.
///   3. A ceiling left above the file it holds was let through by a hundred lines of slack at least
///      five times, each time caught only by a mutation run in the full gate. That is what pinning
///      the number exactly ends: the test now says so on the next run, and names the new number.
///   4. "Set it once, at the end" was written three times and broken anyway, because it is hard to
///      know when the end has arrived. With exact pinning the question stops mattering - the number
///      is whatever the tree measures when the change is committed.
///
/// The ceilings are the largest file in each pool on 2026-09-23. The counts are how many files stood
/// within 70% of that ceiling - <see cref="ShapeCeilings.NearShare"/> - which replaced a fixed five
/// hundred lines, so the band follows its ceiling down instead of going quiet under it.
/// </summary>
internal static class SizeCeilings
{
    /// <summary>
    /// 289 lines of code, Gui/ViewModels/Columns.cs - the column table of the list, 469 lines with its
    /// reasons. PlanBuilder.cs is next at 246.
    /// </summary>
    internal const int LongestShippedFile = 289;

    /// <summary>
    /// Eight at 203 or more: Columns, PlanBuilder, CommandLine.Reading, Program, WindowsBinaryInspector,
    /// Suggesting, QueryFields and Catalogue.Views.
    /// </summary>
    internal const int ShippedFilesNearLongest = 8;

    /// <summary>
    /// 178 lines of markup, PlanView.xaml. OverviewView.xaml is 171 and Themes/Catalogue.xaml 168, so
    /// the top three stand within ten lines of each other.
    /// </summary>
    internal const int LongestShippedMarkupFile = 178;

    /// <summary>Five at 125 or more: PlanView, OverviewView, Catalogue, Controls and Chips.</summary>
    internal const int MarkupFilesNearLongest = 5;

    /// <summary>
    /// 406 lines of code, Gui.Tests/MainViewModelTests.cs. Held to the same rule as the product on
    /// purpose: a test file nobody can read is a test file nobody checks.
    ///
    /// 407 until 2026-09-23, when the unused-member rule switched on that day found a forwarder
    /// at the bottom of the file that no test called.
    /// </summary>
    internal const int LongestTestFile = 406;

    /// <summary>
    /// Four at 285 or more: MainViewModelTests, Fakes/Specimens, QueryMatchingTests, CatalogueGuards.
    /// The reader behind the shape guards stood third at 358 on the day it was written, and was split
    /// in two the same hour rather than counted - one job per file.
    /// </summary>
    internal const int TestFilesNearLongest = 4;
}
