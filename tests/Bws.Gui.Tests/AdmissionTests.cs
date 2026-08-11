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
            Bws.Gui.Texts.Of("gui.status.partial", 1), notice, StringComparison.Ordinal);
    }
}
