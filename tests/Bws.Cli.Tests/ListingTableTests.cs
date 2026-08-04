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
        EntryType = EntryType.OwnProcess,
        Status = EntryStatus.Running,
        ProcessId = Reading<int>.Present(1234),
        StartType = Reading<StartType>.Present(Core.StartType.Automatic),
        DelayedAuto = Reading<bool>.Present(false),
        Account = Reading<string>.Present("LocalSystem"),
        DependsOn = Reading<IReadOnlyList<string>>.Absent(),
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
