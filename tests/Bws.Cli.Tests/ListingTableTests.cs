using Bws.Core;

namespace Bws.Cli.Tests;

/// <summary>
/// The table a person reads in a terminal.
///
/// <b>These are the first tests this assembly has ever had, and the reason is worth writing
/// down.</b> Everything in Bws.Cli is internal, because it is a program rather than a library,
/// and every test that existed for it ran the built executable as a separate process. That is
/// the right way to ask what somebody at a terminal sees, and it left the pieces inside
/// unreachable - so a defect in one of them could only be found by reading, and a coverage
/// measurement could not see the assembly at all.
///
/// The regression surface names one of those defects outright: "szerokosc kolumny listingu
/// odpowiada temu, co widac - BRAK", with the reason given as this project having no test
/// project. It does now.
///
/// <b>What is deliberately NOT asserted here:</b> that a full-width character lines up. It does
/// not. A Han, Kana or Hangul character takes two terminal columns and is counted as one text
/// element, so a listing on a Japanese Windows still drifts. That is recorded in the regression
/// surface and in backlog 98, and writing a test that pinned the current behaviour would turn a
/// known gap into a promise.
/// </summary>
public sealed class ListingTableTests
{
    /// <summary>
    /// A name holding a character stored as two units does not push the next column along.
    ///
    /// <b>This is the fault the fix of 2026-08-03 was about, and nothing held it afterwards.</b>
    /// The width of a column was counted with string.Length and the padding was applied with
    /// PadRight, which counts the same way - so the two were wrong together and nothing showed
    /// until a name arrived carrying something outside the basic plane. An emoji in a product
    /// name is enough.
    /// </summary>
    [Fact]
    public void A_name_holding_a_surrogate_pair_does_not_shift_the_column_after_it()
    {
        var lines = Rendered([
            Entry("Aaa", "\U0001F600 vendor tool"),
            Entry("Bbb", "ordinary name")
        ]);

        Assert.Equal(TypeColumnAt(lines[1]), TypeColumnAt(lines[2]));
    }

    /// <summary>
    /// The same claim for a letter written as a base plus a combining mark, which is a different
    /// shape of the same problem: two chars, two text elements under the old counting, one
    /// character on screen.
    /// </summary>
    [Fact]
    public void A_name_holding_a_combining_mark_does_not_shift_the_column_after_it()
    {
        var lines = Rendered([
            Entry("Aaa", "e\u0301cole service"),
            Entry("Bbb", "ecole service")
        ]);

        Assert.Equal(TypeColumnAt(lines[1]), TypeColumnAt(lines[2]));
    }

    /// <summary>
    /// A field the machine refused says so, rather than rendering as an empty cell.
    ///
    /// The one meaning this cell must never carry is "nothing here", because that is what an
    /// entry with no account genuinely looks like. Rule 8 of CLAUDE.md at the width of one cell.
    /// </summary>
    [Fact]
    public void A_refused_field_says_so_instead_of_rendering_as_empty()
    {
        var refused = Rendered([
            Entry("Aaa", "one") with { Account = Reading<string>.Denied(5, "Access is denied.") }
        ])[1];

        var absent = Rendered([
            Entry("Aaa", "one") with { Account = Reading<string>.Absent() }
        ])[1];

        // Against the ABSENT reading rather than against a present one, because absent is the
        // case this must not be confused with - an entry that genuinely has no account. A first
        // version compared against a present account and failed for a reason that says nothing:
        // "LocalSystem" is simply longer than the refusal.
        Assert.True(
            refused.Length > absent.Length,
            $"a refused account rendered as <{refused}>, no wider than the absent one <{absent}>");
    }

    /// <summary>
    /// No line ends in spaces.
    ///
    /// Padding the last column would make every line carry invisible trailing spaces into
    /// anything somebody copies out of a terminal.
    /// </summary>
    [Fact]
    public void No_line_carries_trailing_spaces()
    {
        var lines = Rendered([Entry("Aaa", "one"), Entry("Bbbbbbbbbbbb", "another")]);

        Assert.All(lines, line => Assert.Equal(line.TrimEnd(), line));
    }

    /// <summary>
    /// The signature column is there because the data is, never because somebody asked.
    ///
    /// A column shown over entries nobody read would be a row of blanks that reads as a row of
    /// "unsigned" - which is the difference between "not asked" and "no signature", printed the
    /// wrong way round.
    /// </summary>
    [Fact]
    public void The_signature_column_appears_only_when_something_was_read()
    {
        var unread = Rendered([Entry("Aaa", "one")]);

        var read = Rendered([
            Entry("Aaa", "one") with
            {
                Signature = Reading<BinarySignature>.Present(
                    new BinarySignature(SignatureStatus.Trusted, 0, "Contoso Systems"))
            }
        ]);

        Assert.Equal(unread[0].Split("  ", StringSplitOptions.RemoveEmptyEntries).Length + 1,
            read[0].Split("  ", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    /// <summary>
    /// The start cell says everything that changes what the start type means, in the order it
    /// has always said it.
    /// </summary>
    /// <remarks>
    /// <b>Written 2026-08-05 because the coverage gate went red and pointed at a real gap.</b>
    /// The rule deciding WHICH qualifiers apply moved into `Bws.Core.StartQualifiers` when the
    /// window started showing the same column - two copies of one rule was the alternative, and
    /// this project has paid for that shape before. What stayed here is the WORDING, and the
    /// wording had no test at all: the only thing watching it was a contract test running the
    /// built executable against whatever this machine happens to have installed.
    ///
    /// So the gate did its job in the way `ADR-10` intends. Coverage fell because covered code
    /// left this assembly, and the answer was the test that should have existed rather than a
    /// lower floor. Backlog item 125.
    /// </remarks>
    [Fact]
    public void The_start_cell_says_delayed_then_trigger_then_a_missing_file()
    {
        var lines = Rendered([
            Entry("Aaa", "one") with
            {
                StartType = Reading<StartType>.Present(StartType.Automatic),
                DelayedAuto = Reading<bool>.Present(true),
                Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Present(
                    [new ServiceTrigger(TriggerKind.Unknown, TriggerAction.Start)]),
                BinaryOnDisk = Reading<bool>.Present(false)
            }
        ]);

        var row = lines[1];

        Assert.Contains("Automatic (delayed), on trigger, file missing", row, StringComparison.Ordinal);
    }

    /// <summary>
    /// Windows ignores the delay flag unless the entry starts automatically, so saying it beside
    /// Manual would be a sentence about a setting with no effect - true of eight entries on the
    /// machine this was measured on.
    /// </summary>
    [Fact]
    // Named apart from the integration test making the same claim against a live machine, on
    // purpose. Two tests with one method name are a filter waiting to match both and report
    // "caught" when only one of them went red - the shape mutate.ps1 was itself caught in.
    public void The_start_cell_marks_a_delay_only_where_it_changes_what_the_start_type_means()
    {
        var lines = Rendered([
            Entry("Aaa", "one") with
            {
                StartType = Reading<StartType>.Present(StartType.Manual),
                DelayedAuto = Reading<bool>.Present(true)
            }
        ]);

        Assert.DoesNotContain("delayed", lines[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// A trigger that stops an entry says nothing about whether it will come up, so marking it
    /// where somebody is deciding whether a stopped service is broken answers a question nobody
    /// asked with a fact about something else.
    /// </summary>
    [Fact]
    public void A_trigger_that_only_stops_the_entry_is_not_marked_in_the_start_cell()
    {
        var lines = Rendered([
            Entry("Aaa", "one") with
            {
                StartType = Reading<StartType>.Present(StartType.Automatic),
                Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Present(
                    [new ServiceTrigger(TriggerKind.Unknown, TriggerAction.Stop)])
            }
        ]);

        Assert.DoesNotContain("trigger", lines[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// A refused reading is not an absent one. The cell says so in words, because a blank there
    /// would read as "this entry has no start type", which is the one thing it never means.
    /// </summary>
    [Fact]
    public void A_refused_start_type_says_so_rather_than_going_blank()
    {
        var lines = Rendered([
            Entry("Aaa", "one") with { StartType = Reading<StartType>.Denied(5, "Access is denied.") }
        ]);

        Assert.Contains("no access", lines[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// A verdict carries whose name is on it, because "Trusted" alone answers half the question
    /// an administrator is asking - trusted by whom.
    /// </summary>
    [Fact]
    public void The_signature_cell_carries_the_publisher_beside_the_verdict()
    {
        var lines = Rendered([
            Entry("Aaa", "one") with
            {
                Signature = Reading<BinarySignature>.Present(
                    new BinarySignature(SignatureStatus.Trusted, 0, "Contoso Systems"))
            }
        ]);

        Assert.Contains("Trusted (Contoso Systems)", lines[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// And a verdict with nobody's name on it says just the verdict, rather than an empty pair
    /// of brackets that reads like something failed to fill in.
    /// </summary>
    [Fact]
    public void A_signature_with_no_publisher_shows_the_verdict_on_its_own()
    {
        var lines = Rendered([
            Entry("Aaa", "one") with
            {
                Signature = Reading<BinarySignature>.Present(
                    new BinarySignature(SignatureStatus.NotSigned, 0, null))
            }
        ]);

        // "Not signed" rather than "NotSigned" since backlog 260. This test is about the absence
        // of an empty pair of brackets rather than about the spelling, but it named the value and
        // the word change reached it.
        Assert.Contains("Not signed", lines[1], StringComparison.Ordinal);
        Assert.DoesNotContain("(", lines[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// A memory figure never travels without how many entries it belongs to.
    ///
    /// Glossary pitfall P12, and it is the difference between a number and a misleading one:
    /// a shared host process holds the memory of everything in it, so "36.1 MB" beside one
    /// service reads as that service's cost and is not.
    /// </summary>
    [Fact]
    public void A_shared_memory_figure_says_how_many_entries_share_it()
    {
        var lines = Rendered([
            Entry("Aaa", "one") with
            {
                Memory = Reading<ProcessMemory>.Present(new ProcessMemory(37_800_000, 20_000_000, 5))
            }
        ]);

        Assert.Contains("shared by 5", lines[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// And a process holding one entry says only the number, because there is nothing to warn
    /// about and a note on every row would teach people to stop reading it.
    /// </summary>
    [Fact]
    public void A_memory_figure_belonging_to_one_entry_carries_no_note()
    {
        var lines = Rendered([
            Entry("Aaa", "one") with
            {
                Memory = Reading<ProcessMemory>.Present(new ProcessMemory(37_800_000, 20_000_000, 1))
            }
        ]);

        Assert.DoesNotContain("shared by", lines[1], StringComparison.Ordinal);
    }


    /// <summary>
    /// A state that takes two words is printed as two words, the way the window prints it.
    ///
    /// <b>Backlog 124, and it is a disagreement between the interfaces rather than a wrong
    /// word.</b> The window has said <c>Start pending</c> since `docs/11` 3.7 asked for sentence
    /// case, and this table printed <c>StartPending</c> - the name of an enumeration value, which
    /// is a shape from the code. Owner's decision, 2026-09-01: align them.
    ///
    /// <b>The machine readable half is asserted here too, and that is the half that must NOT
    /// change.</b> <c>bws list --json</c> and the snapshot both carry <c>StartPending</c> as a
    /// field value and they are a frozen contract - every tool that compares this product against
    /// <c>sc.exe</c> reads the JSON. A repair that reached the JSON would break all of them
    /// silently, so the guard holds both sides of the line rather than one.
    /// </summary>
    [Fact]
    public void A_state_of_two_words_is_printed_as_two_words_and_the_json_is_left_alone()
    {
        var waiting = Entry("Aaa", "waiting to start") with { Status = EntryStatus.StartPending };

        var lines = Rendered([waiting]);

        Assert.Contains("Start pending", lines[1], StringComparison.Ordinal);
        Assert.DoesNotContain("StartPending", lines[1], StringComparison.Ordinal);

        // The frozen half. The document a script reads still names the value the way the glossary
        // binds it, and this assertion is what stops the repair above from reaching it.
        Assert.Contains(
            "\"status\": \"StartPending\"",
            ListingJson.Render([waiting]),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The same repair on the signature column, and the same line held.
    ///
    /// <b>Backlog 260, owner's decision 2026-09-01.</b> The window has said <c>Not signed</c> and
    /// <c>Untrusted root</c> since it grew this column, and this table printed <c>NotSigned</c> and
    /// <c>UntrustedRoot</c> - the names of enumeration values. It is the same disagreement backlog
    /// 124 settled for the state column, in the two surfaces that row did not name.
    ///
    /// <b>The machine readable half is asserted here for the same reason it is above</b>, and it
    /// is the half that must not move: every tool in <c>tools/</c> comparing this product against
    /// <c>sc.exe</c> reads the JSON, so a repair that reached it would break all of them silently.
    /// </summary>
    [Fact]
    public void A_signature_verdict_of_two_words_is_printed_as_two_words_and_the_json_is_left_alone()
    {
        var unsigned = Entry("Aaa", "one") with
        {
            Signature = Reading<BinarySignature>.Present(
                new BinarySignature(SignatureStatus.UntrustedRoot, 0, null))
        };

        var lines = Rendered([unsigned]);

        Assert.Contains("Untrusted root", lines[1], StringComparison.Ordinal);
        Assert.DoesNotContain("UntrustedRoot", lines[1], StringComparison.Ordinal);

        Assert.Contains("UntrustedRoot", ListingJson.Render([unsigned]), StringComparison.Ordinal);
    }

    private static string[] Rendered(IReadOnlyList<ScmEntry> entries) =>
        ListingTable.Render(entries).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// How many columns of a terminal stand before the type cell.
    ///
    /// <b>Counted in grapheme clusters, and the first version of this counted UTF-16 units -
    /// which is the exact mistake these tests are about.</b> String.IndexOf answers in units of
    /// storage, so the row carrying an emoji reported 22 against the plain row's 21 while both
    /// were perfectly aligned on screen. The test failed and the code was right.
    ///
    /// A terminal gives one cell to one grapheme cluster, so that is what has to be counted. It
    /// is the same measure the code uses, and that is not the trap it looks like: the fault this
    /// pins counted UTF-16 units for BOTH the width and the padding, so it agreed with itself
    /// and disagreed with the screen. Counting clusters here is an independent definition of
    /// what alignment means, and it goes red on that code.
    ///
    /// It shares one limit with the code, said out loud: a full-width character takes two cells
    /// and is one cluster, so neither this nor the listing handles Han, Kana or Hangul.
    /// </summary>
    private static int TypeColumnAt(string line)
    {
        var index = line.IndexOf("OwnProcess", StringComparison.Ordinal);

        Assert.True(index > 0, $"the type column was not found in <{line}>");

        var clusters = System.Globalization.StringInfo.GetTextElementEnumerator(line[..index]);
        var column = 0;

        while (clusters.MoveNext())
        {
            column++;
        }

        return column;
    }

    private static ScmEntry Entry(string name, string displayName) => new()
    {
        ServiceName = name,
        DisplayName = displayName,
        Description = Reading<string>.Present(name + " description"),
        EntryType = EntryType.OwnProcess,
        PerUserRole = PerUserRole.None,
        Status = EntryStatus.Running,
        ProcessId = Reading<int>.Present(1234),
        AcceptsStop = Reading<bool>.Present(true),
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(false),
        Account = Reading<string>.Present("LocalSystem"),
        DependsOn = Reading<IReadOnlyList<string>>.Absent(),
        RequiredBy = Reading<IReadOnlyList<string>>.NotRead(),
        Triggers = Reading<IReadOnlyList<ServiceTrigger>>.Absent(),
        BinaryPath = Reading<string>.Present(@"C:\Windows\System32\svchost.exe"),
        BinaryFile = Reading<string>.Present(@"C:\Windows\System32\svchost.exe"),
        BinaryOnDisk = Reading<bool>.Present(true),
        Signature = Reading<BinarySignature>.NotRead(),
        FileVersion = Reading<string>.NotRead(),
        BinaryHash = Reading<string>.NotRead(),
        RequiredPrivileges = Reading<IReadOnlyList<string>>.Absent(),
        SidType = Reading<ServiceSidType>.Absent(),
        SecurityDescriptor = Reading<string>.Absent(),
        ErrorControl = Reading<ErrorControl>.Present(Core.ErrorControl.Normal),
        LoadOrderGroup = Reading<string>.Absent(),
        Memory = Reading<ProcessMemory>.NotRead()
    };
}
