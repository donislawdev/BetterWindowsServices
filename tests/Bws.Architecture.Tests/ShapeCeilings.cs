namespace Bws.Architecture.Tests;

/// <summary>
/// The numbers the shape guards hold, and why each stands where it does. Every number is what the
/// tree measured on 2026-09-23 when <see cref="CodeShapeGuards"/> was written, set by running the
/// guards with zeros and reading back what they said - so nothing had to be rewritten to pass.
///
/// <b>Down is routine, up is the owner's decision.</b> Each number is pinned to the measurement
/// exactly: shrink the largest method and the guard asks for the ceiling to come down with it, in
/// the same change, and says to what. That is the routine door and it is meant to be one line.
/// Raising a number to get a build green is the thing this file exists to prevent.
///
/// <b>History of a number belongs beside it</b>, the convention <see cref="SizeCeilings"/> set:
/// the next session reads it at the moment it is blocked, which is the only moment it matters.
/// </summary>
internal static class ShapeCeilings
{
    internal const string ShippedLength = "shipped method length";
    internal const string ShippedBranching = "shipped method complexity";
    internal const string ShippedDepth = "shipped method depth";
    internal const string ShippedWidth = "shipped signature width";
    internal const string ShippedTypeMethods = "shipped type methods";
    internal const string ShippedTypeState = "shipped type state";
    internal const string TestLength = "test method length";
    internal const string TestBranching = "test method complexity";
    internal const string TestDepth = "test method depth";
    internal const string TestWidth = "test signature width";

    /// <summary>
    /// What "close to the ceiling" means on the axes with a wide range: 70%, the band the Python
    /// suite this follows measured rather than picked. Depth and width use a fixed number instead,
    /// see <see cref="DepthNear"/>.
    /// </summary>
    internal const double NearShare = 0.7;

    /// <summary>
    /// 68 lines of code, EntryDocument.From - the snapshot document built field by field. Refusals.Answer
    /// is one behind at 67 and three more stand at 66, so the band is crowded right under the top.
    /// </summary>
    internal const int LongestMethod = 68;

    /// <summary>Fourteen at 48 lines of code or more.</summary>
    internal const int MethodsNearLongest = 14;

    /// <summary>
    /// 23 forks, shared by PlanBuilder.AddWarnings and EntryDocument.From. Execution.Report is next
    /// at 21. The entry point and CommandLine.Read stand far above and are named exemptions.
    /// </summary>
    internal const int MostComplexMethod = 23;

    /// <summary>Five at 17 forks or more.</summary>
    internal const int MethodsNearMostComplex = 5;

    /// <summary>
    /// 4 levels, shared by the command line's top-level statements and QueryScanner.TryScan - a
    /// character loop with quoting inside it.
    /// </summary>
    internal const int DeepestMethod = 4;

    /// <summary>
    /// A fixed band rather than a share, and the reason is arithmetic: 70% of 4 is 2.8, so the band
    /// would be "3 or more" today and "2 or more" the day the ceiling came down to 3 - which would
    /// take the count from 24 to 118 without a line changing. A band that reshapes itself under the
    /// thing it is watching says nothing. The Python suite made the same call for the same reason.
    /// </summary>
    internal const int DepthNear = 3;

    /// <summary>Twenty-four at 3 levels or more.</summary>
    internal const int MethodsNearDeepest = 24;

    /// <summary>
    /// 9 parameters, Sentences.Admissions - the window's sentence about what it admitted, built from
    /// nine counts. Readings..ctor and Says.AboutTheAnswer are next at 8.
    /// </summary>
    internal const int WidestSignature = 9;

    /// <summary>Fixed for the reason written at <see cref="DepthNear"/>: the range is 0 to 9.</summary>
    internal const int WidthNear = 8;

    /// <summary>Three at 8 parameters or more.</summary>
    internal const int SignaturesNearWidest = 3;

    /// <summary>
    /// 79 members with code in them, the plan's view model Planned across six partial files.
    /// MainWindow is second at 63 across eleven, and those two are the whole band. Neither was ever
    /// near a file ceiling, which is the point of measuring the type rather than the file.
    /// </summary>
    internal const int MostMethodsInType = 79;

    /// <summary>Two at 56 or more: Planned and MainWindow.</summary>
    internal const int TypesNearMostMethods = 2;

    /// <summary>
    /// 32 fields, CommandLine - one per option the command line can carry. EntryDocument (27) and
    /// ScmEntry (26) are the rest of the band, and all three are DATA shapes: a new field in either
    /// of the last two is a new field in the snapshot, which is a contract change on its own.
    /// </summary>
    internal const int MostStateInType = 32;

    /// <summary>Three at 23 or more: CommandLine, EntryDocument, ScmEntry.</summary>
    internal const int TypesNearMostState = 3;

    /// <summary>63 lines of code, OutboundGuards.The_registers_catch_every_shape_they_exist_to_catch.</summary>
    internal const int LongestTestMethod = 63;

    /// <summary>
    /// Ten at 45 lines of code or more. Eleven until the review of PR #11, when a third copy of the
    /// pixel counting in the GUI guards made it twelve and went red in CI - the three copies became
    /// Drawn in Bws.Gui.Tests, and WaitingBoxGuards.RedInsideTheBox left the crowd with them.
    /// </summary>
    internal const int TestMethodsNearLongest = 10;

    /// <summary>
    /// 13 forks, and it is Naming.MemberOf - part of the reader these guards stand on, written the
    /// same day. Said plainly because it looks like the new code setting its own record, which it
    /// is: the previous largest in tests/ was 10. It is a flat switch over thirteen kinds of
    /// declaration with nothing nested, which McCabe's count scores exactly like thirteen nested ifs.
    /// That is a known weakness of the measure, and it is left visible rather than argued away by
    /// choosing a measure that happens to score this file lower.
    /// </summary>
    internal const int MostComplexTestMethod = 13;

    /// <summary>Three at 10 forks or more.</summary>
    internal const int TestMethodsNearMostComplex = 3;

    /// <summary>5 levels, AppearanceGuards.No_view_invents_an_appearance_value_of_its_own.</summary>
    internal const int DeepestTestMethod = 5;

    /// <summary>Fixed, for the reason at <see cref="DepthNear"/>.</summary>
    internal const int TestDepthNear = 4;

    /// <summary>Four at 4 levels or more.</summary>
    internal const int TestMethodsNearDeepest = 4;

    /// <summary>5 parameters, AssemblyFacts..ctor and BinaryPathResolverTests.Resolve.</summary>
    internal const int WidestTestSignature = 5;

    /// <summary>Fixed, for the reason at <see cref="DepthNear"/>.</summary>
    internal const int TestWidthNear = 4;

    /// <summary>Seventeen at 4 parameters or more.</summary>
    internal const int TestSignaturesNearWidest = 17;

    /// <summary>
    /// The floor under a scan, well below the tree on purpose: 192 files and 1113 units under src/,
    /// 210 and 1905 under tests/ on the day. It exists to catch a scan that read nothing, not to
    /// track the tree, so it does not follow it down.
    /// </summary>
    internal const int FewestFilesRead = 100;

    internal const int FewestUnitsRead = 500;

    /// <summary>
    /// Named exceptions, and the rule that admits one, applied to every entry below: a unit stands
    /// so far above everything else on an axis that setting the ceiling on it would hold nothing
    /// for the other thousand units. It is taken out by name, with its reason, and the guard refuses
    /// the entry the day it stops being needed.
    ///
    /// <b>ONE ENTRY DOES NOT MEET THAT RULE ON ITS OWN, AND IT IS SAID RATHER THAN HIDDEN</b> - found by
    /// review on 2026-09-23, after the reason written here claimed "three times the next method on
    /// every axis", which was true of length only. CommandLine.Read is one level over the depth
    /// ceiling, not far over it. It is exempt on depth because it is ALREADY exempt on length and
    /// branching and backlog 24 will split it as a whole: without the entry the depth ceiling would
    /// be 5 for everybody, handing a level to the other thousand units to spare one method a
    /// sentence it is already under. The same reasoning does not reach the entry point, which stands
    /// ON the depth ceiling rather than over it, and it is not exempt there.
    /// </summary>
    internal static readonly ShapeExemption[] Exemptions =
    [
        new("Bws.Cli.CommandLine.Read", ShippedLength,
            "221 lines of code, over three times the length ceiling of 68. Backlog 24 has asked for it to be split since " +
            "2026-08-02, and the analyser before this said so too. Split, not exempted for ever."),
        new("Bws.Cli.CommandLine.Read", ShippedBranching,
            "50 forks, over twice the branching ceiling of 23. The same method, the same backlog row."),
        new("Bws.Cli.CommandLine.Read", ShippedDepth,
            "5 levels, ONE over the depth ceiling of 4 - exempt because it is exempt on the two axes above and will be " +
            "split as a whole, not because it is far over this one. See the summary of this list."),
        new("Bws.Core.Querying.QueryFields.BuildAll", ShippedLength,
            "166 lines of code and no branching: a table, one declaration per field of the query language. Splitting " +
            "it would put the list of fields in several places, which is the drift QueryFields.cs exists to prevent."),
        new("Bws.Cli.<top-level statements>", ShippedLength,
            "209 lines of code, the command line's entry point in Program.cs. Found by this guard on the day it was " +
            "written: the analyser that held method length before does not report top-level statements at all, so " +
            "the largest unit in the product had never been under any ceiling. Backlog 431."),
        new("Bws.Cli.<top-level statements>", ShippedBranching, "54 forks, the same entry point, the same backlog row."),
    ];
}
