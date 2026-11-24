using Bws.Core.Querying;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The four columns about the FILE an entry runs - signature, publisher, version and hash - which the second
/// pass of ADR-13 fills together. Their own file since 2026-11-23, when the recovery columns took Columns.cs
/// past its size ceiling: one family, one reading, one seam.
/// </summary>
/// <remarks>
/// THE SECOND PHASE OF ADR-13, arriving 2026-08-18 - backlog 21. Until that slice the window read none of
/// these and said so, which was honest and left the fields invisible. Five columns came that day rather than
/// the four the backlog estimated, because the signature carries two facts: what the system thinks of it, and
/// who signed it - and the fifth, memory, stays in Columns.cs beside the process id it measures.
///
/// ALL OFF AT THE START, and that is not the usual caution about width. Turning one on is what makes the window
/// go and read them, and the reading measured 8.86-9.42 s of processor against 1.39-1.42 s without it, over
/// about 810 entries. A column shown by default would spend that on every F5 for everybody.
/// </remarks>
internal static partial class Columns
{
    /// <summary>The four, in the order they are offered. A method, for the reason RecoveryColumns gives.</summary>
    private static Column[] FileColumns() =>
    [
        new Column
        {
            Id = "signature",
            LabelKey = "gui.column.signature",
            WidthKey = "ColumnSignature",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Needs = ExtraRead.Signatures,
            Reads = entry => CellFaces.SignatureLabel(entry.Signature),
            Outcome = entry => entry.Signature.Outcome
        },
        new Column
        {
            Id = "publisher",
            LabelKey = "gui.column.publisher",
            WidthKey = "ColumnPublisher",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Needs = ExtraRead.Signatures,
            Reads = entry => CellFaces.PublisherLabel(entry.Signature),
            Outcome = entry => entry.Signature.Outcome
        },
        new Column
        {
            Id = "fileVersion",
            LabelKey = "gui.column.fileVersion",
            WidthKey = "ColumnFileVersion",
            Face = ColumnFace.Text,
            ShownAtFirst = false,
            Needs = ExtraRead.Signatures,
            Reads = entry => CellFaces.Say(entry.FileVersion, value => value),
            Outcome = entry => entry.FileVersion.Outcome
        },
        new Column
        {
            // Monospaced, and this is the column the rule was written for: sixty-four hexadecimal
            // characters where two different values must not be able to look the same - `docs/03`,
            // part 4. Proportional digits make that exactly what they do.
            Id = "binaryHash",
            LabelKey = "gui.column.binaryHash",
            WidthKey = "ColumnBinaryHash",
            Face = ColumnFace.Fixed,
            ShownAtFirst = false,

            // The same family as the signature and the version beside it, because one pass fills
            // all three - SecondPass asks for the three answers of each file together. NOT from one
            // opening of the file, which this said until 2026-09-28: each answer opens it on its own.
            Needs = ExtraRead.Signatures,
            Reads = entry => CellFaces.Say(entry.BinaryHash, value => value),
            Outcome = entry => entry.BinaryHash.Outcome
        }
    ];
}
