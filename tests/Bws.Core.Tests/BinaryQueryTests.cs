using Bws.Core.Querying;
using Bws.Core.Tests.Fakes;

namespace Bws.Core.Tests;

/// <summary>
/// Asking about the launch command and about the file it points at.
///
/// Two fields rather than one, because they answer different questions. The path is text
/// and takes the wildcards and expressions any text field takes. Whether the file is there
/// is a fact about something else, worked out by looking at the disk.
///
/// C13 of the specification calls the pairing an orphan, and the language spells that out
/// of parts it already had rather than adding a word for it: file:missing start:auto.
/// Measured on a real machine on 2026-08-01, that is an empty list, while file:missing on
/// its own is five entries - which is why the primitive is the useful thing to expose.
/// </summary>
public sealed class BinaryQueryTests
{
    [Fact]
    public void A_missing_file_can_be_asked_about_whatever_the_start_type_is()
    {
        var missing = Match("file:missing");

        Assert.Contains("ProtonVPN WireGuard", missing);
        Assert.Contains("amduw23g-202073-df09ebb6", missing);
        Assert.DoesNotContain("Spooler", missing);
    }

    [Fact]
    public void An_orphan_is_written_out_of_parts_the_language_already_had()
    {
        // The glossary defines an orphan as automatic plus no file. Both specimens whose
        // file is gone are manual or disabled, exactly as on the real machine, so this is
        // empty - and that emptiness is the reason there is no orphan:yes to be had.
        Assert.Empty(Match("file:missing start:auto"));

        // The same query with the start type dropped is not empty, so the emptiness above
        // is a fact about the machine and not a field that answers nothing.
        Assert.NotEmpty(Match("file:missing"));
    }

    [Fact]
    public void Present_and_missing_divide_the_entries_that_have_a_file()
    {
        var present = Match("file:present");
        var missing = Match("file:missing");

        Assert.Empty(present.Intersect(missing, StringComparer.OrdinalIgnoreCase));

        // An entry naming nothing to run is in neither, and that is the third answer rather
        // than a gap: there is no file for the question to be about.
        Assert.DoesNotContain("PathLess", present);
        Assert.DoesNotContain("PathLess", missing);
        Assert.Contains("PathLess", Match("file:none"));
    }

    [Fact]
    public void A_driver_with_no_path_of_its_own_still_has_a_file()
    {
        // The pairing that catches a reader who took "no path" to mean "no file". Absent
        // path, present file, and both answers are correct at the same time.
        Assert.Contains("Beep", Match("path:none"));
        Assert.Contains("Beep", Match("file:present"));
    }

    [Fact]
    public void The_path_is_matched_whole_including_the_arguments()
    {
        // What a person sees in the listing is what they can search. Matching the resolved
        // file instead would answer "which services run out of svchost" with nothing.
        Assert.Contains("McmSvc", Match("path:svchost"));
        Assert.Contains("McmSvc", Match(@"path:""-s McmSvc"""));
    }

    [Fact]
    public void A_path_takes_the_operators_any_text_field_takes()
    {
        Assert.Contains("AppvStrm", Match(@"path:\SystemRoot*"));
        Assert.Contains("Spooler", Match("path:/spool.*exe$/"));
    }

    [Fact]
    public void A_bare_word_searches_the_path_too()
    {
        // The query language document promised this would happen the moment paths were read.
        // A column somebody can see and cannot search reads as a bug rather than a rule.
        //
        // The word is chosen so that the path is the only way to reach it: "svchost" is not
        // in McmSvc's name, not in its Polish display name and not in its account. The first
        // version of this test looked for "ubisoft" on an entry whose display name is
        // "Ubisoft UPC Elevation Service", so it passed with the path taken out of the search
        // altogether - a test proving nothing, exactly as ADR-10 warns a fixture can do.
        Assert.Contains("McmSvc", Match("svchost"));

        // And it still searches what it always searched.
        Assert.Contains("Spooler", Match("bufor"));
    }

    [Fact]
    public void A_refused_reading_is_not_answered_as_a_missing_file()
    {
        // The whole of rule 8 on one field. An entry whose configuration was refused knows
        // nothing about its file, and saying "missing" would turn a fact about permissions
        // into an accusation about the machine.
        Assert.DoesNotContain("Locked", Match("file:missing"));
        Assert.DoesNotContain("Locked", Match("file:present"));
        Assert.DoesNotContain("Locked", Match("file:none"));
        Assert.Contains("Locked", Match("file:?"));
    }

    [Fact]
    public void Judging_by_a_refused_field_makes_the_result_say_it_is_incomplete()
    {
        var parsed = QueryParser.Parse("file:missing");
        var result = parsed.Query!.Filter(Specimens.All);

        // Not a failure and not silence. The count is short by whatever could not be read
        // and the caller is told so, which is what lets the CLI end with zero and still
        // write a warning.
        Assert.True(result.Unreadable > 0);
    }

    private static List<string> Match(string query)
    {
        var parsed = QueryParser.Parse(query);

        Assert.True(parsed.IsValid, string.Join(", ", parsed.Problems.Select(problem => problem.Kind)));

        return [.. parsed.Query!.Filter(Specimens.All).Entries.Select(entry => entry.ServiceName)];
    }
}
