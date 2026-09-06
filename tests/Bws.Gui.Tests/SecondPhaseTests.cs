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

    /// <summary>
    /// A question about the OTHER family does not open a single file a second time.
    ///
    /// <b>It cost the whole thing twice until 2026-08-26.</b> The pass filled signatures and memory
    /// together and the window remembered only the family that had been ASKED about. So somebody
    /// who typed signed:no and then memory:>1MB was asking for something the rows already held, and
    /// the window answered by reading the manager again and verifying every signature on the
    /// machine a second time - 7.5 s of processor and 18 MB, with the tick suspended for all of it.
    ///
    /// <b>WHAT THIS GUARDS CHANGED ON 2026-09-05 AND THE NUMBER IT GUARDS DID NOT.</b> The two
    /// passes were split, so the rows no longer hold memory just because signatures were read -
    /// which means the second question does now send the window back to the manager, once. That
    /// read is measured at 423-500 ms over 810 entries and the memory pass behind it at under a
    /// millisecond over 110 processes. The file reading is the cost this test was written about,
    /// and not one file is opened again.
    ///
    /// <b>Exactly one more reading, not "at least one".</b> An upper bound is what makes this
    /// catch the fault in the other direction: a window that forgot what it had tried would go
    /// back to the manager on every tick for ever, and every one of those would look like progress.
    ///
    /// <b>Counted rather than timed</b>, for the reason the guard above gives: a duration in a test
    /// is a promise about somebody else's machine.
    /// </summary>
    [Fact]
    public async Task The_second_question_does_not_pay_for_the_pass_again()
    {
        var machine = new LiveMachine(Entry());
        var inspector = new Inspector(SignatureStatus.NotSigned);
        var model = new MainViewModel(machine, new SteppedClock(), inspector, new Memory());

        await model.LoadAsync();

        model.QueryText = "signed:no";

        await model.RefreshAsync();

        var filesOpened = inspector.Asked;
        var readings = machine.FullReads;

        Assert.True(filesOpened > 0, "The pass never ran, so the second half of this proves nothing.");

        // The other family, which since the split these rows do NOT hold - so this is a question
        // the window has to go and answer rather than one it can answer from what it has.
        model.QueryText = "memory:>1MB";

        await model.RefreshAsync();
        await model.RefreshAsync();

        // NOT ONE FILE OPENED AGAIN, which is the seven and a half seconds this test is about.
        Assert.Equal(filesOpened, inspector.Asked);

        // One reading to fetch the family, and then it stops - two ticks, one read between them.
        Assert.Equal(readings + 1, machine.FullReads);
    }

    /// <summary>
    /// A question about memory alone does not open a single file.
    ///
    /// <b>THE SPLIT, FROM THE SIDE THAT COSTS MONEY - 2026-09-05, owner's decision.</b> One
    /// expression filled both families, on the argument that a window which has decided to pay for
    /// one has no reason to decide again about the other. The two prices are not comparable: asking
    /// 110 processes what they are using is under a millisecond, and opening 810 binaries to verify
    /// them is 7.5 s of processor and 18 MB. So the cheap answer could only ever be had at the
    /// expensive one's price, with the tick suspended for all of it.
    ///
    /// <b>Counted rather than timed</b>, for the reason the guards above give.
    /// </summary>
    [Fact]
    public async Task A_question_about_memory_alone_opens_no_files()
    {
        var inspector = new Inspector(SignatureStatus.NotSigned);
        var memory = new Memory();
        var model = new MainViewModel(new LiveMachine(Entry()), new SteppedClock(), inspector, memory);

        await model.LoadAsync();

        model.QueryText = "memory:>1MB";

        await model.RefreshAsync();

        Assert.True(memory.Asked > 0, "The memory pass never ran, so the rest of this proves nothing.");
        Assert.Equal(0, inspector.Asked);
    }

    /// <summary>
    /// Showing a column is a way of asking, which for five columns it was not until 2026-09-05.
    ///
    /// <b>THE FAULT AS THE OWNER MET IT.</b> The memory column read "unknown" on all 810 rows, on
    /// every machine, for ever. The window worked out what to go and read from the QUERY alone, so
    /// a column turned on told nobody - <c>ColumnBar.Changed</c> had one subscriber and it wrote
    /// the layout file. Four more columns had the same fault and nobody had reported them.
    ///
    /// <b>Driven through a real picker rather than by handing the model an answer.</b> The claim
    /// is that TICKING A BOX is what sends the window, so a test setting the question directly
    /// would pass over a build where <see cref="ColumnBar.Needs"/> read the wrong flag or read
    /// nothing at all.
    ///
    /// <b>And it asserts what did NOT happen, which is the half that keeps the fix affordable.</b>
    /// The memory column must not drag the signature pass along behind it.
    /// </summary>
    [Fact]
    public async Task Showing_a_column_is_what_sends_the_window_to_read_what_fills_it()
    {
        var inspector = new Inspector(SignatureStatus.NotSigned);
        var memory = new Memory();
        var model = new MainViewModel(new LiveMachine(Entry()), new SteppedClock(), inspector, memory);
        var columns = new ColumnBar();

        model.ColumnsNeed = () => columns.Needs;

        await model.LoadAsync();
        await model.RefreshAsync();

        // Nobody has asked anything: the box is empty and the memory column starts off.
        Assert.Equal(0, memory.Asked);

        columns.Choices.Single(choice => choice.Column.Id == "memory").IsShown = true;

        await model.RefreshAsync();

        Assert.True(
            memory.Asked > 0,
            "Turning the memory column on did not send the window to read what fills it, so the "
            + "column shows 'unknown' on every row - see Column.Needs.");

        Assert.Equal(0, inspector.Asked);
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
        /// <summary>
        /// How many processes this was asked about, which is the other half of the split.
        ///
        /// The inspector has counted since the day the pass was written and this did not, because
        /// while one method filled both families there was nothing a second counter could tell
        /// anybody. Once each family can run without the other, "the cheap one ran" and "the
        /// expensive one did not" are two claims and each needs its own number.
        /// </summary>
        internal int Asked { get; private set; }

        public Reading<ProcessMemory> Read(int processId)
        {
            Asked++;

            return Reading<ProcessMemory>.Present(new ProcessMemory(1024, 2048, 1));
        }
    }
}
