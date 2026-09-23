using Bws.Core;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// What the answer has to admit about itself.
///
/// <b>The one here was written, translated, shipped, and reached no screen.</b> The sentence
/// "Running without administrator rights, so Windows hands over fewer entries than it has" sat in
/// the language file from the beginning with nothing referring to it - found on 2026-08-05 by the
/// guard in <c>TextKeyGuards</c>, which itself existed only as a promise in a comment.
///
/// <b>That made it the exact shape rule 8 forbids:</b> the window knew the list was short and
/// said nothing. Measured on this machine: without elevation the manager enumerates 807 entries
/// where an elevated session sees 810, and five more refuse their security descriptor. A person
/// reading a count has no way to know any of that.
/// </summary>
public sealed class AdmissionTests
{
    [Fact]
    public async Task A_session_without_administrator_rights_is_told_the_list_is_short()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock())
        {
            Says = new Says { Elevated = false }
        };

        await model.LoadAsync();

        Assert.Contains("administrator", model.Says.Notice, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The even claim, and without it the one above passes on a window that says this always.
    /// Most sessions of this tool are elevated - it is a service manager - so a permanent warning
    /// would be a permanent lie.
    /// </summary>
    [Fact]
    public async Task An_elevated_session_is_not_warned_about_anything()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock())
        {
            Says = new Says { Elevated = true }
        };

        await model.LoadAsync();

        Assert.Equal(string.Empty, model.Says.Notice);
    }

    /// <summary>
    /// FIRST, because it is a fact about the whole list rather than about this query. Every other
    /// sentence on that line describes a list the reader believes is complete.
    /// </summary>
    [Fact]
    public async Task The_warning_comes_before_whatever_the_query_has_to_admit()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock())
        {
            Says = new Says { Elevated = false }
        };

        await model.LoadAsync();

        // A query about signatures makes the window admit it never read them, which is the other
        // sentence this line can carry.
        model.QueryText = "signed:no";

        var notice = model.Says.Notice;

        Assert.Contains("signatures", notice, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            notice.IndexOf("administrator", StringComparison.OrdinalIgnoreCase)
                < notice.IndexOf("signatures", StringComparison.OrdinalIgnoreCase),
            "The warning about a short list has to come first. It is the only sentence here that "
            + "is about the whole machine rather than about what was asked for:" + Environment.NewLine + notice);
    }

    /// <summary>
    /// Memory is the second expensive family and it had no test, which is how it came to be the
    /// only branch in <c>Sentences.Admissions</c> nothing had ever executed - found on 2026-08-11
    /// while repairing backlog 161.
    ///
    /// <b>It is a separate sentence from the one about signatures, not a shared "something was
    /// not read".</b> The two cost different things and are read by different passes, so a person
    /// whose query came back empty needs to know WHICH one the window skipped - otherwise the
    /// remedy, which is to ask the command line instead, is a guess.
    /// </summary>
    [Fact]
    public async Task Asking_about_memory_says_nobody_read_it_rather_than_that_it_was_refused()
    {
        var model = new MainViewModel(new LiveMachine(Rows.Entry("Spooler")), new SteppedClock())
        {
            Says = new Says { Elevated = true }
        };

        await model.LoadAsync();

        model.QueryText = "memory:>1MB";

        var notice = model.Says.Notice;

        Assert.NotEqual(string.Empty, notice);
        Assert.Equal(Bws.Gui.Texts.Of("gui.query.unreadMemory"), notice);

        // The even claim, and it is the one that matters: this must not read as a refusal. "The
        // machine would not let us" and "nobody asked" are the two states this project spends
        // most of its rules keeping apart, and both arrive here as an empty list.
        Assert.DoesNotContain(
            Bws.Gui.Texts.Of("gui.status.partial.many", 1), notice, StringComparison.Ordinal);
    }

    /// <summary>
    /// A refusal the memory pass brought back is still a refusal, and the answer has to say so.
    ///
    /// <b>Found by the UX audit of 2026-09-23 on a live window without administrator rights</b>
    /// (<c>docs/AUDYT-UX-GUI-20260923.md</c>, UX-GUI-001): <c>status:running memory:&gt;100MB</c>
    /// answered "Nothing in this list matches" while <c>memory:?</c> counted 103 running services
    /// whose memory the machine refused. The sentence that admits a partial answer was suppressed
    /// for every query needing a second phase family - including after the pass had run, when an
    /// unread field is no longer "nobody looked" but "the machine said no". A short list that looks
    /// complete is the failure rule 8 names, and <c>docs/07</c> calls it the worst there is.
    /// </summary>
    [Fact]
    public async Task A_refusal_the_memory_pass_brought_back_is_admitted_beside_the_answer()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler"), Rows.Entry("Audiosrv")),
            new SteppedClock(),
            reader: new RefusingMemory())
        {
            Says = new Says { Elevated = true }
        };

        await model.LoadAsync();

        model.QueryText = "memory:>100MB";

        await model.RefreshAsync();

        Assert.Empty(model.Rows);
        Assert.Equal(Bws.Gui.Texts.Of("gui.status.partial.many", 2), model.Says.Notice);
    }

    /// <summary>
    /// The even claim, and without it the one above passes on a window that says this always:
    /// a pass that read every process leaves nothing to admit.
    /// </summary>
    [Fact]
    public async Task A_memory_pass_that_read_every_process_admits_nothing()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler"), Rows.Entry("Audiosrv")),
            new SteppedClock(),
            reader: new AnsweringMemory())
        {
            Says = new Says { Elevated = true }
        };

        await model.LoadAsync();

        model.QueryText = "memory:>100MB";

        await model.RefreshAsync();

        Assert.Empty(model.Rows);
        Assert.Equal(string.Empty, model.Says.Notice);
    }

    /// <summary>
    /// The boundary of the repair: while one family the query needs is still unread, the sentence
    /// about THAT family speaks alone.
    ///
    /// <b>One count carries both states</b> - the query engine counts a refused field and a field
    /// nobody read as the same "could not judge" - so saying "could not be read" while a family is
    /// still unread would turn "nobody looked" into a refusal. Here memory is read and refused,
    /// and signatures are never read because this window has no inspector.
    /// </summary>
    [Fact]
    public async Task A_family_still_unread_keeps_the_refusal_sentence_back()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Entry("Spooler")),
            new SteppedClock(),
            reader: new RefusingMemory())
        {
            Says = new Says { Elevated = true }
        };

        await model.LoadAsync();

        model.QueryText = "memory:>100MB signed:no";

        await model.RefreshAsync();

        // Memory WAS read, or this is the state the test above it already covers and proves
        // nothing about the boundary.
        Assert.DoesNotContain(Bws.Gui.Texts.Of("gui.query.unreadMemory"), model.Says.Notice, StringComparison.Ordinal);
        Assert.DoesNotContain(Bws.Gui.Texts.Of("gui.query.readingMemory"), model.Says.Notice, StringComparison.Ordinal);
        Assert.Contains(Bws.Gui.Texts.Of("gui.query.unreadSignatures"), model.Says.Notice, StringComparison.Ordinal);
        Assert.DoesNotContain(Bws.Gui.Texts.Of("gui.status.partial.one", 1), model.Says.Notice, StringComparison.Ordinal);
    }

    /// <summary>
    /// The second face of the same fault: a SHOWN column asks for its family too, so turning on
    /// Memory used to silence the partial sentence for a question about a different field.
    ///
    /// <b>Reconstructed from the code on 2026-09-23 and pinned here rather than left as a claim</b> -
    /// the user changelog says it, and prose nothing checks is how this project's wrong sentences
    /// are born. The field is the security descriptor, which a window without administrator rights
    /// is refused on a handful of entries (five on the machine the audit ran on).
    /// </summary>
    [Fact]
    public async Task A_shown_memory_column_does_not_silence_a_refusal_about_another_field()
    {
        var refused = Rows.Entry("Spooler") with
        {
            SecurityDescriptor = Reading<string>.Denied(5, "Access is denied.")
        };

        var model = new MainViewModel(new LiveMachine(refused), new SteppedClock(), reader: new AnsweringMemory())
        {
            Says = new Says { Elevated = true }
        };

        var columns = new ColumnBar();

        model.ColumnsNeed = () => columns.Needs;

        await model.LoadAsync();

        columns.Choices.Single(choice => choice.Column.Id == "memory").IsShown = true;

        model.QueryText = "sddl:A";

        await model.RefreshAsync();

        Assert.Equal(Bws.Gui.Texts.Of("gui.status.partial.one", 1), model.Says.Notice);
    }

    /// <summary>
    /// The sentence about folding is a fact about ROWS, so it stops being said when there are none.
    ///
    /// <b>Backlog 263, owner's decision 2026-09-01.</b> The machine overview REPLACES the list
    /// rather than sitting over it, so on that screen there are no rows at all - and this sentence
    /// exists to explain the difference between the count above the list and the number of rows in
    /// it. With no rows it explains nothing, and it goes further than being idle: it names a switch
    /// and a list that are not on screen to be looked at.
    ///
    /// <b>Both directions, because a sentence that never comes back is the same fault from the
    /// other side.</b> Going away is what was asked for and returning is what makes it a
    /// suppression rather than a deletion - and the returning half is the one that needed the pass
    /// to be re-run rather than the notice merely raised.
    /// </summary>
    [Fact]
    public async Task The_folded_count_is_not_explained_while_the_overview_has_the_window()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Template("CDPUserSvc"), Rows.Instance("CDPUserSvc_7b537")),
            new SteppedClock())
        {
            Says = new Says { Elevated = true }
        };

        await model.LoadAsync();

        Assert.Contains("folded", model.Says.Notice, StringComparison.Ordinal);

        model.ShowingOverview = true;

        Assert.DoesNotContain("folded", model.Says.Notice, StringComparison.Ordinal);

        model.ShowingOverview = false;

        Assert.Contains("folded", model.Says.Notice, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the sentence that must NOT go away with it, which is the whole line between them.
    ///
    /// <b>Elevation is a fact about the MACHINE, not about a list.</b> It is why the numbers on the
    /// overview are short too, and backlog 16 put it first deliberately - before anybody has asked
    /// for anything is exactly when it matters most. A repair for backlog 263 that hid the status
    /// row wholesale would have taken this with it, which is why the suppression is one sentence
    /// rather than one control.
    /// </summary>
    [Fact]
    public async Task The_warning_about_rights_survives_the_screen_that_has_no_list()
    {
        var model = new MainViewModel(
            new LiveMachine(Rows.Template("CDPUserSvc"), Rows.Instance("CDPUserSvc_7b537")),
            new SteppedClock())
        {
            Says = new Says { Elevated = false }
        };

        await model.LoadAsync();

        model.ShowingOverview = true;

        Assert.Contains("administrator", model.Says.Notice, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("folded", model.Says.Notice, StringComparison.Ordinal);
    }

    /// <summary>
    /// What a window without administrator rights gets for most service processes: error 5.
    /// </summary>
    private sealed class RefusingMemory : IProcessMemoryReader
    {
        public Reading<ProcessMemory> Read(int processId) =>
            Reading<ProcessMemory>.Denied(5, "Access is denied.");
    }

    /// <summary>A process that answers, and holds far less than the threshold the tests ask about.</summary>
    private sealed class AnsweringMemory : IProcessMemoryReader
    {
        public Reading<ProcessMemory> Read(int processId) =>
            Reading<ProcessMemory>.Present(new ProcessMemory(1024, 2048, 1));
    }
}
