using Bws.Core;
using Bws.Core.Querying;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// A publisher the signature does not vouch for, as the window says it - security report S-6,
/// owner's decisions of 2026-10-06.
///
/// <b>One cell carries the mark for three places.</b> The details panel and the export read the
/// words of this cell through the column catalogue, and DetailsGuards holds a panel line to its
/// cell - so the cell is what is asserted here, and the panel follows by a guard that already exists.
/// </summary>
public sealed class UnvouchedPublisherTests
{
    private const string Microsoft = "Microsoft Windows";

    private const string File = @"C:\windows\system32\spoolsv.exe";

    [Fact]
    public void The_publisher_cell_marks_a_name_nobody_vouches_for()
    {
        Assert.Equal(
            Texts.Of("gui.cell.publisherNotVerified", Microsoft),
            CellFaces.PublisherLabel(Signature(SignatureStatus.Tampered, Microsoft)));

        Assert.Equal(Microsoft, CellFaces.PublisherLabel(Signature(SignatureStatus.Trusted, Microsoft)));

        // Nobody signed it, so there is no name to doubt and the cell stays empty as before.
        Assert.Equal(string.Empty, CellFaces.PublisherLabel(Signature(SignatureStatus.NotSigned, null)));
    }

    [Fact]
    public void The_line_under_the_box_says_it_even_while_signatures_are_being_read()
    {
        // The asymmetry is the point. The refusal sentence is held back while a family is being read,
        // because "nobody looked" and "you were not allowed" arrive as one count. This count holds
        // only signatures already read and not trusted, so it is true at every moment of the pass.
        var answer = new Narrowed { Selected = [], Unreadable = 2, TooCostly = 0, Unvouched = 2 };

        var admitted = Sentences.Admissions(
            needs: ExtraRead.Signatures, held: false, answer: answer, rights: string.Empty,
            have: ExtraRead.None, filling: true, folded: 0, listOnScreen: true);

        Assert.Contains(Texts.Of("gui.query.readingSignatures"), admitted.Reservations, StringComparison.Ordinal);
        Assert.Contains(Texts.Of("gui.status.unvouched.many", 2), admitted.Reservations, StringComparison.Ordinal);
        Assert.DoesNotContain(Texts.Of("gui.status.partial.many", 2), admitted.Reservations, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Asking_for_microsoft_leaves_a_changed_file_out_and_the_window_says_why()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler") with { BinaryFile = Reading<string>.Present(File) }),
            new SteppedClock(),
            new Inspector(),
            new Memory());

        await model.LoadAsync();

        model.QueryText = "publisher:microsoft";
        await model.RefreshAsync();

        // Read, and not believed: the list is empty, the line under the box says why, and the
        // middle does not claim in the voice of a finding that nothing matches.
        Assert.Empty(model.Rows);
        Assert.Contains(Texts.Of("gui.status.unvouched.one", 1), model.Says.Reservations, StringComparison.Ordinal);
        Assert.Equal(Texts.Of("gui.empty.nothingMatchedCheckedHere"), model.Says.ListMessage);

        // And the exclusion keeps it - the half S-6 was about.
        model.QueryText = "!publisher:microsoft";

        Assert.Single(model.Rows);
        Assert.Contains(Texts.Of("gui.status.unvouched.one", 1), model.Says.Reservations, StringComparison.Ordinal);
    }

    private static Reading<BinarySignature> Signature(SignatureStatus status, string? publisher) =>
        Reading<BinarySignature>.Present(new BinarySignature(status, 0, publisher));

    /// <summary>An inspector that finds every file changed after Microsoft signed it.</summary>
    private sealed class Inspector : IBinaryInspector
    {
        private static Reading<BinarySignature> ReadSignature() => Signature(SignatureStatus.Tampered, Microsoft);

        public FileInspection Inspect(string file) =>
            new(ReadSignature(), Reading<string>.Present("1.0.0.0"), Reading<string>.Present(new string('a', 64)));
    }

    private sealed class Memory : IProcessMemoryReader
    {
        public Reading<ProcessMemory> Read(int processId) => Reading<ProcessMemory>.Present(new ProcessMemory(1024, 2048, 1));
    }
}
