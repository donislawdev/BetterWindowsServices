using Bws.Core;
using Bws.Core.Querying;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The second half of `ADR-13` in the window - backlog 21, built 2026-08-18.
///
/// <b>What this closes is a correct sentence rather than a bug.</b> The window said out loud that
/// it had not read signatures or memory and sent people to the command line, which was true and
/// complete in itself - and left `signed:no` in the window answering with an empty list. An empty
/// list is what "there are none" looks like, so the honest sentence was carrying the weight of a
/// missing feature.
///
/// <b>The shape worth knowing, because it is the opposite of what the design notes expected.</b>
/// The pass runs INSIDE the reading rather than beside it. Starting it in the background needs a
/// cancellation, a generation counter and a rule for a list whose composition changes underneath a
/// pass still out - and none of it is needed, because both entry points refuse to start while a
/// reading is out. The reentrancy guard that already existed makes that race impossible rather
/// than handled, which is a cheaper thing to be sure of.
///
/// <b>AND IT IS ASKED FOR RATHER THAN ALWAYS RUN, which a measurement decided - owner's rule,
/// 2026-08-18.</b> A window that always ran it cost 8.86-9.42 s of processor against 1.39-1.42 s
/// without, over about 810 entries, four launches each with the first discarded. That is paid on
/// every F5. So the trigger is somebody asking a question that needs it, and the answer arrives on
/// the tick after they ask - which is what the tests below assert, in two steps rather than one.
/// </summary>
public sealed class SecondPhaseTests
{
    private const string File = @"C:\windows\system32\spoolsv.exe";

    /// <summary>
    /// The window can answer a question about signatures once it has read the machine.
    ///
    /// <b>Asked through the query rather than through the entry, because that is the promise.</b>
    /// A test reading the filled field would pass over a window where the query never saw it.
    /// </summary>
    [Fact]
    public async Task A_question_about_signatures_is_answered_once_the_reading_is_done()
    {
        var model = Window(SignatureStatus.NotSigned);

        await model.LoadAsync();

        model.QueryText = "signed:no";

        // Nothing yet, and the window says why rather than showing an empty list as if it were an
        // answer. This half is the whole reason the sentence has three states.
        Assert.Empty(model.Rows);
        Assert.NotEqual(string.Empty, model.Says.Notice);

        await model.RefreshAsync();

        Assert.Single(model.Rows);
        Assert.Equal(string.Empty, model.Says.Notice);
    }

    /// <summary>
    /// A WINDOW NOBODY ASKED DOES NOT PAY FOR THE PASS, and this is the guard that measurement
    /// bought.
    ///
    /// <b>It counts calls rather than timing anything</b>, because a duration in a test is a
    /// promise about somebody else's machine. What is asserted is the thing the number was about:
    /// a listing nobody has questioned opens no files at all.
    /// </summary>
    [Fact]
    public async Task A_listing_nobody_questioned_opens_no_files()
    {
        var inspector = new Inspector(SignatureStatus.NotSigned);
        var model = new MainViewModel(new LiveMachine(Entry()), new SteppedClock(), inspector, new Memory());

        await model.LoadAsync();
        await model.RefreshAsync();
        await model.RefreshAsync();

        Assert.Equal(0, inspector.Asked);

        // And it starts the moment somebody wants it, so the guard above cannot pass by the pass
        // being broken outright.
        model.QueryText = "signed:no";

        await model.RefreshAsync();

        Assert.True(inspector.Asked > 0, "Nobody read the signatures even though the query asked for them.");
    }

    /// <summary>
    /// And it says so rather than answering with an empty list when nobody handed it an inspector.
    ///
    /// <b>This is the state the window was in until today, kept as a guard.</b> A window built
    /// without the expensive halves has to go on admitting it - the sentence is the whole
    /// difference between "no entry is unsigned" and "nobody looked".
    /// </summary>
    [Fact]
    public async Task Without_an_inspector_the_window_still_admits_it_has_not_looked()
    {
        var model = new MainViewModel(new LiveMachine(Entry()), new SteppedClock());

        await model.LoadAsync();

        model.QueryText = "signed:no";

        Assert.Empty(model.Rows);
        Assert.NotEqual(string.Empty, model.Says.Notice);
    }

    /// <summary>
    /// A trusted binary does not answer signed:no, which is what makes the test above mean
    /// anything.
    ///
    /// <b>The double has to be able to produce both answers or neither test proves a thing</b> -
    /// `ADR-13`'s own note about doubles that cannot tell the cases apart, applied to itself.
    /// </summary>
    [Fact]
    public async Task A_trusted_binary_is_not_reported_as_unsigned()
    {
        var model = Window(SignatureStatus.Trusted);

        await model.LoadAsync();

        model.QueryText = "signed:no";

        await model.RefreshAsync();

        Assert.Empty(model.Rows);

        model.QueryText = "signed:yes";

        Assert.Single(model.Rows);
    }

    /// <summary>
    /// A fresh reading drops what it knew, because the families belong to the entries.
    ///
    /// <b>Rule 8 at the point where an audit tool lies most quietly.</b> Keeping the old signature
    /// against a newly read entry would show a verdict about a file that may have been replaced
    /// since - and it would look exactly like a fresh answer.
    /// </summary>
    [Fact]
    public async Task What_was_read_belongs_to_the_entries_it_was_read_for()
    {
        var machine = new LiveMachine(Entry());
        var model = new MainViewModel(machine, new SteppedClock(), new Inspector(SignatureStatus.NotSigned), new Memory());

        await model.LoadAsync();

        model.QueryText = "signed:no";

        await model.RefreshAsync();

        Assert.Single(model.Rows);

        // A second full reading hands over entries whose signature is unread again. The window has
        // to end up back where it started, having read them a second time rather than remembering.
        await model.LoadAsync();

        Assert.Single(model.Rows);
    }

    /// <summary>
    /// CLICKING THE CHIP IS WHAT SENDS THE WINDOW TO READ THEM - backlog 162 meeting backlog 21.
    ///
    /// <b>This is the join between the two, and neither half is worth much alone.</b> The family
    /// could not exist while the window read no signatures: a chip filtering on something nobody
    /// had looked at answers with an empty list, and an empty list is what "there are none" looks
    /// like. And the reading is deliberately not paid by anybody who never asks - so something has
    /// to ask, and the chip is it.
    ///
    /// <b>Through the chip rather than through the box</b>, because the chip writing itself into
    /// the query is the promise `A5` makes, and a test setting the text directly would pass over a
    /// chip wired to nothing.
    /// </summary>
    [Fact]
    public async Task Turning_on_the_signature_chip_is_what_sends_the_window_to_read_them()
    {
        var inspector = new Inspector(SignatureStatus.NotSigned);
        var model = new MainViewModel(new LiveMachine(Entry()), new SteppedClock(), inspector, new Memory());

        await model.LoadAsync();
        await model.RefreshAsync();

        Assert.Equal(0, inspector.Asked);

        var chip = model.FilterGroups
            .SelectMany(group => group.Chips)
            .Single(one => one.Field == "signed" && one.Value == "no");

        chip.IsOn = true;

        // And it wrote itself into the box, which is the half that makes it a query rather than a
        // switch - the whole mechanism stands on the filter being something you can read back.
        Assert.Contains("signed:no", model.QueryText, StringComparison.Ordinal);

        await model.RefreshAsync();

        Assert.True(inspector.Asked > 0, "Turning the chip on did not send the window to read signatures.");
        Assert.Single(model.Rows);
    }

    private static MainViewModel Window(SignatureStatus status) =>
        new(new LiveMachine(Entry()), new SteppedClock(), new Inspector(status), new Memory());

    private static ScmEntry Entry() => Rows.Entry("Spooler") with { BinaryFile = Reading<string>.Present(File) };

    /// <summary>An inspector that answers for any file, so the pass has something to fill in.</summary>
    private sealed class Inspector(SignatureStatus status) : IBinaryInspector
    {
        /// <summary>How many files this was asked about, which is what the cost guard reads.</summary>
        internal int Asked { get; private set; }

        public Reading<BinarySignature> ReadSignature(string file)
        {
            Asked++;

            return Reading<BinarySignature>.Present(new BinarySignature(status, 0, "Someone"));
        }

        public Reading<string> ReadFileVersion(string file) => Reading<string>.Present("1.0.0.0");

        public Reading<string> ReadHash(string file) => Reading<string>.Present(new string('a', 64));
    }

    private sealed class Memory : IProcessMemoryReader
    {
        public Reading<ProcessMemory> Read(int processId) =>
            Reading<ProcessMemory>.Present(new ProcessMemory(1024, 2048, 1));
    }
}
