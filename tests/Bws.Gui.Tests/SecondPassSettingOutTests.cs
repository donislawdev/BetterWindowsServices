using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The second pass setting out - G-11 of the external performance report of 2026-09-28.
///
/// <b>It says the list is still being filled and does nothing else.</b> Until that day it said so
/// by recutting the scope and running the query again over entries the reading just before it had
/// narrowed and shown - a whole rethink of the window to change one sentence.
/// </summary>
public sealed class SecondPassSettingOutTests
{
    private const string File = @"C:\windows\system32\spoolsv.exe";

    /// <summary>
    /// While the pass reads, the sentence under the list says the signatures are being read - the
    /// one thing setting out has to say - and the query runs twice over the whole reading: once for
    /// the entries, once for what the pass filled in. Never a third time in between.
    /// </summary>
    [Fact]
    public async Task Setting_out_says_the_signatures_are_being_read_and_runs_the_query_only_around_the_pass()
    {
        MainViewModel? model = null;
        string? saidWhileReading = null;

        // Read from inside the pass, which is the moment "still reading" has to be true.
        var inspector = new Recording(() => saidWhileReading ??= model!.Says.Notice);

        model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler") with { BinaryFile = Reading<string>.Present(File) }),
            new SteppedClock(),
            inspector);

        await model.LoadAsync();

        model.QueryText = "signed:no";

        var rethought = 0;

        model.Filters[0].PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(FilterChip.IsOn))
            {
                rethought++;
            }
        };

        await model.RefreshAsync();

        Assert.NotNull(saidWhileReading);
        Assert.Contains(Texts.Of("gui.query.readingSignatures"), saidWhileReading, StringComparison.Ordinal);
        Assert.Equal(2, rethought);
    }

    /// <summary>
    /// G-9 of the external stability report: the flag went up and the sentence was said BEFORE the
    /// try, so a sentence that threw left the list "still filling" for good - the catch that puts
    /// the flag down was never reached. Asked of the readings directly, because the window's own
    /// sentence has no way to throw on purpose.
    /// </summary>
    [Fact]
    public async Task A_sentence_that_throws_on_setting_out_does_not_leave_the_list_filling()
    {
        var said = 0;
        var says = new Says();

        var readings = new Readings(
            new LiveMachine(Rows.Entry("Spooler") with { BinaryFile = Reading<string>.Present(File) }),
            new RowIndex(new SteppedClock()),
            () => says,
            () => { },
            () => { },
            () => Announced(ref said),
            new Recording(() => { }),
            wanted: () => Bws.Core.Querying.ExtraRead.Signatures);

        await readings.LoadAsync(Relisting.Afresh);

        Assert.Equal(1, said);
        Assert.False(readings.Filling);
        Assert.Contains("the sentence threw", says.Problem, StringComparison.Ordinal);
    }

    /// <summary>A sentence that throws the first time it is said.</summary>
    private static void Announced(ref int said)
    {
        if (said++ == 0)
        {
            throw new InvalidOperationException("the sentence threw");
        }
    }

    private sealed class Recording(Action asked) : IBinaryInspector
    {
        private Reading<BinarySignature> ReadSignature()
        {
            asked();

            return Reading<BinarySignature>.Present(new BinarySignature(SignatureStatus.NotSigned, 0, "Someone"));
        }

        public FileInspection Inspect(string file) =>
            new(ReadSignature(), Reading<string>.Present("1.0.0.0"), Reading<string>.Present(new string('a', 64)));
    }
}
