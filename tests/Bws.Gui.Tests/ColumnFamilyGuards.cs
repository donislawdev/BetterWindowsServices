using Bws.Core;
using Bws.Core.Querying;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// Which columns are fed by the second phase of `ADR-13`, and whether each of them says so.
///
/// <b>Its own file since 2026-09-05, and the size ratchet is what asked - ColumnGuards was
/// four lines under five hundred and this test is eighty.</b> The seam is real rather than
/// arithmetic: that file is the CATALOGUE of columns, asking what each one is called, what its
/// cell says and what it sorts by. This asks something else entirely - where a cell's answer comes
/// FROM, and therefore what the window has to go and read before the column can say anything at
/// all. ColumnOrderGuards and ColumnPickerGuards came out of the same file on the same argument.
///
/// <b>The fault behind it is the one the owner reported that day.</b> The memory column read
/// "unknown" on every row of every machine, because only a typed query could ask for the family
/// that fills it and turning the column on told nobody. Four more columns had the same fault and
/// nobody had noticed them - see Column.Needs.
/// </summary>
public sealed class ColumnFamilyGuards
{
    /// <summary>
    /// A column fed by the second phase of `ADR-13` declares which family feeds it, and a column
    /// that is not fed by it declares nothing.
    ///
    /// <b>THE FAULT THIS EXISTS FOR RAN FOR WEEKS AND LOOKED LIKE A BROKEN COLUMN.</b> Until
    /// 2026-09-05 the window worked out what to go and read from the query alone, so the five
    /// columns fed by that phase - the memory column, the signature and the three beside it - read
    /// "unknown" on all 810 rows unless somebody happened to type a member of the same family into
    /// the box. Turning the column on reached one listener and it wrote the layout file. The owner
    /// reported it as the memory column not working, and it was never about memory.
    ///
    /// <b>It asks the pair rather than the declaration, which is what makes it a guard and not a
    /// copy of the catalogue.</b> A list of five identifiers written out here would be a second
    /// place to keep the same fact, and it would go stale exactly when a sixth column arrives -
    /// which is the moment it is supposed to speak. So the answer is taken from the CELL: a cell
    /// that says "unknown" over an entry whose first phase is complete is reading the second
    /// phase, whatever it claims, and which pass makes it stop saying so is which family it needs.
    ///
    /// <b>Both directions, because half of it would pass over an empty set.</b> A column that
    /// declares a family it does not read sends the window to open eight hundred files for
    /// nothing, and that is the more expensive mistake of the two.
    /// </summary>
    [Fact]
    public void Every_column_fed_by_the_second_phase_declares_the_family_that_feeds_it()
    {
        // THE WORD FOR A CELL NOBODY HAS ASKED THE MACHINE FOR - "not read" since 2026-09-16, the
        // glossary's word, where it was "unknown" before. This test finds the second phase by
        // that word, so it changed in the same edit as the word: left on the old one it would
        // have found no unread cell at all and passed over nothing.
        var unknown = Bws.Gui.Texts.Of("gui.cell.notRead");

        // The first phase complete and the second phase untouched, which is the state every row is
        // in the moment a listing arrives and before anybody has asked for anything.
        var bare = Rows.Entry("Spooler");

        var signed = bare with
        {
            Signature = Reading<BinarySignature>.Present(
                new BinarySignature(SignatureStatus.Trusted, 0, "Microsoft Windows")),
            FileVersion = Reading<string>.Present("10.0.26200.1"),
            BinaryHash = Reading<string>.Present(new string('a', 64))
        };

        var measured = bare with
        {
            Memory = Reading<ProcessMemory>.Present(new ProcessMemory(1024, 2048, SharedBy: 1))
        };

        // THE THIRD FAMILY, 2026-09-06. It arrived with a column of its own, and until this
        // specimen existed the loop below could tell that the cell was fed by SOMETHING and not
        // by which - so it reported "neither pass fills it", which reads as the column being
        // broken rather than as this test not knowing about a third pass.
        var stood = bare with
        {
            RequiredBy = Reading<IReadOnlyList<string>>.Present(["Spooler", "Fax"])
        };

        var wrong = new List<string>();

        foreach (var column in Columns.All)
        {
            if (column.Reads(bare) != unknown)
            {
                if (column.Needs != ExtraRead.None)
                {
                    wrong.Add(
                        $"  {column.Id} declares {column.Needs} and its cell answers without it");
                }

                continue;
            }

            var fills = column.Reads(signed) != unknown
                ? ExtraRead.Signatures
                : column.Reads(measured) != unknown
                    ? ExtraRead.Memory
                    : column.Reads(stood) != unknown
                        ? ExtraRead.RequiredBy
                        : ExtraRead.None;

            if (fills == ExtraRead.None)
            {
                wrong.Add($"  {column.Id} says unknown and neither pass of the second phase fills it");
            }
            else if (column.Needs != fills)
            {
                wrong.Add($"  {column.Id} declares {column.Needs} and is filled by {fills}");
            }
        }

        Assert.True(
            wrong.Count == 0,
            "A column and the family it needs have come apart. Whichever way round it is, the "
            + "window is wrong on screen: a column needing more than it declares prints 'unknown' "
            + "on every row for ever, and one declaring more than it needs pays for a pass nobody "
            + "can see the result of - see Column.Needs:"
            + Environment.NewLine + string.Join(Environment.NewLine, wrong));
    }

    /// <summary>
    /// Every column that needs a family says how its reading went, and says it truthfully.
    ///
    /// <b>The details panel is the reader</b>, since 2026-09-16: over one entry it draws a value
    /// nobody has asked for as a state rather than as an answer, and the only way it can tell the
    /// two apart is <see cref="Column.Outcome"/>. A column needing a family without it would draw
    /// "not read" in white, as if the machine had said so - the fault the outcome exists to stop,
    /// one column at a time and silently.
    ///
    /// <b>Asked, not only declared.</b> An outcome answering Present over an entry whose second
    /// phase is untouched would be a declaration that says the right thing and does the wrong one.
    /// </summary>
    [Fact]
    public void Every_column_fed_by_the_second_phase_says_how_its_reading_went()
    {
        var bare = Rows.Entry("Spooler");

        var silent = Columns.All
            .Where(column => column.Needs != ExtraRead.None && column.Outcome is null)
            .Select(column => column.Id)
            .ToList();

        Assert.True(
            silent.Count == 0,
            "These columns need a family and cannot say whether it was read, so the details panel "
            + "would draw their 'not read' as an answer: " + string.Join(", ", silent));

        var lying = Columns.All
            .Where(column => column.Outcome is not null && column.Outcome(bare) != ReadOutcome.NotRead)
            .Select(column => column.Id)
            .ToList();

        Assert.True(
            lying.Count == 0,
            "These columns say their reading went some other way than NotRead over an entry whose "
            + "second phase nobody has asked for: " + string.Join(", ", lying));
    }
}
