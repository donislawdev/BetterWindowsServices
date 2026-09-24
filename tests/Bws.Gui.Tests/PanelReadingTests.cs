using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The details panel reads the three expensive families for its own entry - UX-GUI-005.
///
/// <b>Until 2026-09-24 the panel read nothing</b> and said "not read" beside the signature, the
/// memory and who depends on the entry, with a note sending the reader to turn on a column - which
/// reads every entry on the machine. Measured before the change over 797 entries: the same reading
/// for ONE entry costs 47-67 ms the first time a window asks and 6-13 ms after that, and half a
/// second for the one 98 MB driver on the machine. So the panel reads for itself.
///
/// <b>What these guard is the contract around that reading rather than its speed</b>: it writes
/// into the row so the panel and a copy say the same thing, it never counts as the LIST having
/// read, an answer for a listing that has since been replaced is thrown away, a field that cannot
/// be read is asked for once, and a failure is said rather than swallowed.
/// </summary>
public sealed class PanelReadingTests
{
    private const string File = @"C:\windows\system32\spoolsv.exe";

    [Fact]
    public async Task Opening_the_panel_reads_what_its_entry_is_missing()
    {
        var inspector = new Inspector();
        var memory = new Memory();
        var model = new MainViewModel(new LiveMachine(Entry()), new SteppedClock(), inspector, memory);
        await model.LoadAsync();
        var row = model.Rows.Single();

        await Open(model, row);

        // INTO THE ROW, which is what keeps Ctrl+C and the panel on one answer.
        Assert.True(row.Entry.Signature.IsPresent, "The panel did not read the signature of its entry.");
        Assert.True(row.Entry.Memory.IsPresent, "The panel did not read the memory of its entry.");
        Assert.NotEqual(ReadOutcome.NotRead, row.Entry.RequiredBy.Outcome);

        // ONE FILE, not the machine - the whole point of reading for one entry.
        Assert.Equal(1, inspector.Asked);

        Assert.Equal("Someone", Line(model, "gui.column.publisher").Shown);
        Assert.Equal(string.Empty, model.Chosen.Notice);
    }

    [Fact]
    public async Task While_it_reads_its_lines_say_so()
    {
        var inspector = new Inspector();
        var model = new MainViewModel(new LiveMachine(Entry()), new SteppedClock(), inspector, new Memory());
        await model.LoadAsync();
        var row = model.Rows.Single();

        inspector.Hold();
        model.Chosen.Row = row;
        model.Chosen.Show();

        Assert.Equal(Texts.Of("gui.details.reading"), Line(model, "gui.column.signature").Shown);
        Assert.True(Line(model, "gui.column.signature").Missing, "A line still being read is drawn as an answer.");

        inspector.Release();
        await model.Chosen.CatchingUp;

        Assert.NotEqual(Texts.Of("gui.details.reading"), Line(model, "gui.column.signature").Shown);
    }

    /// <summary>
    /// ONE FILLED ROW IS NOT THE LIST HAVING READ. A question about signatures still needs the
    /// second phase over every entry, and the window still says the signatures are unread.
    ///
    /// <b>Asserted on THAT sentence, not on the line being non-empty</b> - the first version asked
    /// only for "something under the list", and the mutation registry caught it passing with the
    /// rule broken: in a session without administrator rights the line always carries the sentence
    /// about rights, so it is never empty. A test that depends on who runs it is a fault of the test.
    /// </summary>
    [Fact]
    public async Task A_panel_reading_does_not_count_as_the_list_having_read()
    {
        var model = new MainViewModel(new LiveMachine(Entry()), new SteppedClock(), new Inspector(), new Memory());
        await model.LoadAsync();

        await Open(model, model.Rows.Single());
        Assert.True(model.Rows.Single().Entry.Signature.IsPresent);

        model.QueryText = "signed:no";

        Assert.Contains(Texts.Of("gui.query.unreadSignatures"), model.Says.Notice, StringComparison.Ordinal);
    }

    /// <summary>
    /// A full reading replaces every entry while the panel's reading is out, so the answer belongs
    /// to entries that no longer exist - it is thrown away and the panel asks again.
    /// </summary>
    [Fact]
    public async Task An_answer_for_a_listing_that_has_been_replaced_is_thrown_away()
    {
        var inspector = new Inspector();
        var model = new MainViewModel(new LiveMachine(Entry()), new SteppedClock(), inspector, new Memory());
        await model.LoadAsync();
        var row = model.Rows.Single();

        inspector.Hold();
        model.Chosen.Row = row;
        model.Chosen.Show();
        var first = model.Chosen.CatchingUp;

        await model.LoadAsync();

        inspector.Release();
        await first;
        await model.Chosen.CatchingUp;

        Assert.Equal(2, inspector.Asked);
        Assert.True(row.Entry.Signature.IsPresent);
    }

    /// <summary>
    /// A field that comes back "not read" - a file on another machine, which the window does not
    /// reach for - is asked for once per listing, or every refresh of the row would ask again.
    /// </summary>
    [Fact]
    public async Task A_field_the_panel_cannot_read_is_asked_for_once()
    {
        var inspector = new Inspector { Answer = Reading<BinarySignature>.NotRead() };
        var model = new MainViewModel(new LiveMachine(Entry()), new SteppedClock(), inspector, new Memory());
        await model.LoadAsync();
        var row = model.Rows.Single();

        await Open(model, row);
        await Open(model, row);

        Assert.Equal(1, inspector.Asked);
        Assert.Equal(Texts.Of("gui.cell.notRead"), Line(model, "gui.column.signature").Shown);
    }

    /// <summary>
    /// The service restarts between the question and the answer: the memory read belongs to the
    /// process that was asked, and against the new one it would be a stranger's figure in the right
    /// field. It is left unread rather than kept.
    /// </summary>
    [Fact]
    public async Task A_memory_read_for_a_process_that_has_since_restarted_is_not_kept()
    {
        var inspector = new Inspector();
        var machine = new LiveMachine(Entry());
        var model = new MainViewModel(machine, new SteppedClock(), inspector, new Memory());
        await model.LoadAsync();
        var row = model.Rows.Single();

        inspector.Hold();
        model.Chosen.Row = row;
        model.Chosen.Show();
        var first = model.Chosen.CatchingUp;

        machine.Start("Spooler", 4321);
        await model.RefreshAsync();

        inspector.Release();
        await first;

        Assert.Equal(4321, row.Entry.ProcessId.Value);
        Assert.Equal(ReadOutcome.NotRead, row.Entry.Memory.Outcome);
        Assert.True(row.Entry.Signature.IsPresent, "The file's answer does not depend on the process and should have been kept.");
    }

    [Fact]
    public async Task A_reading_that_fails_is_said_in_the_panel()
    {
        var inspector = new Inspector { Fails = true };
        var model = new MainViewModel(new LiveMachine(Entry()), new SteppedClock(), inspector, new Memory());
        await model.LoadAsync();

        await Open(model, model.Rows.Single());

        Assert.Contains("The file vanished mid-read", model.Chosen.Notice, StringComparison.Ordinal);
    }

    /// <summary>
    /// Opens the panel and waits for its reading - WITH A CEILING, because a reading that never
    /// stops asking is a failure this class exists to catch, and a plain await would turn it into a
    /// test run that never ends and reports nothing. tools/mutate/mutate.ps1, note 2.
    /// </summary>
    private static async Task Open(MainViewModel model, EntryRow row)
    {
        model.Chosen.Row = row;
        Assert.True(model.Chosen.Show());

        await model.Chosen.CatchingUp.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static DetailLine Line(MainViewModel model, string labelKey) =>
        model.Chosen.Sections.SelectMany(section => section.Lines).Single(line => line.Label == Texts.Of(labelKey));

    private static ScmEntry Entry() => Rows.Entry("Spooler") with { BinaryFile = Reading<string>.Present(File) };

    /// <summary>An inspector that counts, can be held mid-read, and can fail.</summary>
    private sealed class Inspector : IBinaryInspector
    {
        private readonly ManualResetEventSlim _gate = new(initialState: true);
        private int _asked;

        internal int Asked => Volatile.Read(ref _asked);

        internal Reading<BinarySignature> Answer { get; init; } =
            Reading<BinarySignature>.Present(new BinarySignature(SignatureStatus.Trusted, 0, "Someone"));

        internal bool Fails { get; init; }

        internal void Hold() => _gate.Reset();

        internal void Release() => _gate.Set();

        public Reading<BinarySignature> ReadSignature(string file)
        {
            Interlocked.Increment(ref _asked);
            _gate.Wait(TimeSpan.FromSeconds(10));

            return Fails ? throw new System.IO.IOException("The file vanished mid-read.") : Answer;
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
