using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;

namespace Bws.Gui.Tests;

/// <summary>
/// The one place this window hands an address to the shell, and what has to stay true about it.
///
/// <b>Four things, and the second is the reason the file exists.</b> The destination is the one
/// the button promises. A window with administrator rights goes to the desktop and ONLY to the
/// desktop, whatever the desktop answers - the fallback to a direct start would open a browser
/// with those rights, which is the outcome ExternalLinks exists to avoid. A shell that refuses
/// says so instead of throwing out of a click. And no other file chooses an address.
///
/// <b>Two more since a review of 2026-09-23.</b> A refusal reads as a sentence of the window's own
/// with the number Windows gave, never as the runtime's text about itself. And a press waits for
/// the shell on a thread of its own, for a bounded time and one hand-over at a time - the shell is
/// another process, and nothing else puts a limit on how long it takes to answer.
///
/// <b>What is NOT here, and where it is.</b> Whether the desktop really answers is a question about
/// the machine, so it lives in Bws.Integration.Tests, which runs where there is a desktop and not on
/// the build server. Whether the page really opens is a question nobody should answer by opening a
/// browser on somebody's screen from a test run, so it is answered by pressing the button.
/// </summary>
public sealed class ExternalLinksGuards
{
    private const string LinksFile = "ExternalLinks.cs";

    [Fact]
    public void The_support_address_is_the_one_the_button_promises()
    {
        // Pinned rather than derived. The tooltip names this host before anything is pressed, so a
        // typo here is a promise broken on screen rather than just a bad link.
        Assert.Equal("https://donislawdev.com/support/", ExternalLinks.Support);

        // https asserted apart, because a plaintext address would be the one insecure step this
        // window could take and nothing on screen would show it.
        Assert.StartsWith("https://", ExternalLinks.Support, StringComparison.Ordinal);

        Assert.Contains(
            new Uri(ExternalLinks.Support).Host,
            Texts.Of("gui.support.hint"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void An_elevated_window_goes_to_the_desktop_and_never_starts_the_browser_itself()
    {
        var started = 0;

        var said = ExternalLinks.Open(
            ExternalLinks.Support,
            elevated: true,
            start: _ => { started++; return null; },
            handToDesktop: _ => "the desktop said no.");

        // The desktop refused, and the browser was still not started directly. This is the whole
        // safety property of the file, so it is asserted on the refusal rather than on the success.
        Assert.Equal(0, started);
        Assert.NotNull(said);
        Assert.Contains("the desktop said no.", said, StringComparison.Ordinal);
        Assert.Contains(ExternalLinks.Support, said, StringComparison.Ordinal);
    }

    [Fact]
    public void A_window_without_rights_starts_the_browser_itself_and_leaves_the_desktop_alone()
    {
        var handed = 0;
        string? asked = null;

        var said = ExternalLinks.Open(
            ExternalLinks.Support,
            elevated: false,
            start: address => { asked = address; return null; },
            handToDesktop: _ => { handed++; return null; });

        Assert.Null(said);
        Assert.Equal(0, handed);
        Assert.Equal(ExternalLinks.Support, asked);
    }

    [Fact]
    public void An_address_the_shell_refuses_is_a_sentence_rather_than_a_throw()
    {
        // A real refusal rather than a stand-in. The path does not exist, so the shell fails before
        // it looks for an association, and the runtime asks it for no error dialog - deterministic,
        // and it opens no window and no browser. A throw here would leave a Click handler, and that
        // reaches the window's last line instead of the status line.
        var missing = Path.Combine(Path.GetTempPath(), $"bws-no-such-file-{Guid.NewGuid():N}.nothing");
        Assert.False(File.Exists(missing), "the test needs an address the shell cannot possibly take");

        var said = ExternalLinks.Start(missing);

        Assert.False(string.IsNullOrWhiteSpace(said));
    }

    [Fact]
    public void A_desktop_refusal_is_a_sentence_of_the_windows_own_with_the_number_windows_gave()
    {
        // The two refusals whose text the runtime writes itself, in English and about itself -
        // measured on .NET 10.0.12. That text is what reached the status line until a review of
        // 2026-09-23 asked about it.
        var noInterface = ExternalLinks.Refused(new InvalidCastException());
        var failed = ExternalLinks.Refused(new COMException(null, unchecked((int)0x80004005)));

        Assert.Equal(Texts.Of("gui.support.desktopRefused", "80004002"), noInterface);
        Assert.Equal(Texts.Of("gui.support.desktopRefused", "80004005"), failed);
        Assert.DoesNotContain(new InvalidCastException().Message, noInterface, StringComparison.Ordinal);
        Assert.DoesNotContain(new COMException(null, unchecked((int)0x80004005)).Message, failed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_press_hands_the_shell_its_work_on_a_background_thread_of_the_windows_apartment()
    {
        // The desktop's shell objects are apartment threaded and the window's own thread was one,
        // so the thread that replaces it has to be one too - a pool thread would not be. And it is
        // a background thread, so a hand-over the shell never answers holds nothing open when the
        // window closes.
        var asker = Environment.CurrentManagedThreadId;
        var seen = new List<(int Thread, ApartmentState Apartment, bool Background)>();

        var handover = new ShellHandover(
            () =>
            {
                var thread = Thread.CurrentThread;
                lock (seen)
                {
                    seen.Add((thread.ManagedThreadId, thread.GetApartmentState(), thread.IsBackground));
                }

                return null;
            },
            TimeSpan.FromSeconds(30),
            "slow");

        Assert.Null(await handover.Ask());
        Assert.Null(await handover.Ask());

        // Two presses one after the other are two hand-overs, because the first had answered.
        Assert.Equal(2, seen.Count);
        Assert.All(seen, one =>
        {
            Assert.NotEqual(asker, one.Thread);
            Assert.Equal(ApartmentState.STA, one.Apartment);
            Assert.True(one.Background, "a hand-over thread that is not a background thread keeps a closed window's process alive");
        });
    }

    [Fact]
    public async Task A_shell_that_does_not_answer_is_a_sentence_once_the_bound_has_passed()
    {
        var shellAnswers = new TaskCompletionSource();
        var handover = new ShellHandover(() => { shellAnswers.Task.Wait(); return null; }, TimeSpan.FromMilliseconds(50), "slow");

        try
        {
            var asking = handover.Ask();

            // The test's own limit is far past the bound, so reaching it means the bound was not
            // kept - which is the window waiting on a shell that never answers.
            Assert.Same(asking, await Task.WhenAny(asking, Task.Delay(TimeSpan.FromSeconds(30))));
            Assert.Equal("slow", await asking);
        }
        finally
        {
            shellAnswers.SetResult();
        }
    }

    [Fact]
    public async Task A_second_press_while_the_first_is_with_the_shell_starts_nothing_new()
    {
        // A person who sees nothing happen presses again. A shell that then comes back would open
        // the page once per press, so a press that finds the last one still out joins it.
        var shellAnswers = new TaskCompletionSource();
        var handed = 0;
        var handover = new ShellHandover(
            () => { Interlocked.Increment(ref handed); shellAnswers.Task.Wait(); return null; },
            TimeSpan.FromMilliseconds(50),
            "slow");

        try
        {
            Assert.Equal("slow", await handover.Ask());
            Assert.Equal("slow", await handover.Ask());
            Assert.Equal("slow", await handover.Ask());

            Assert.Equal(1, Volatile.Read(ref handed));
        }
        finally
        {
            shellAnswers.SetResult();
        }
    }

    [Fact]
    public async Task A_throw_on_the_shells_thread_reaches_the_press_rather_than_ending_the_process()
    {
        // Anything the shell throws that the links file does not name is the shell breaking its
        // own contract, and it has to reach the window's last line in Mishaps the way it did when
        // the call ran on the window's thread. Thrown on a thread nobody catches on, it would end
        // the process instead.
        var handover = new ShellHandover(
            () => throw new InvalidOperationException("the shell broke its contract"),
            TimeSpan.FromSeconds(30),
            "slow");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(handover.Ask);

        Assert.Equal("the shell broke its contract", thrown.Message);
    }

    [Fact]
    public void Nothing_outside_the_links_file_hands_the_shell_an_address()
    {
        var offenders = Calls(inLinksFile: false);

        Assert.True(
            offenders.Count == 0,
            "an address reaches the shell from outside " + LinksFile
                + " - add a named destination there instead:" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void The_scan_reaches_the_file_it_claims_to_cover()
    {
        // The canary. A walk that reads no files, or a rename that makes the search match nothing,
        // satisfies the assertion above and looks exactly like a guard that works. Six is what the
        // links file has - the three declarations, the call the support hand-over makes to Open,
        // the one Process.Start, and since 2026-09-24 the call the help menu's two pages make to
        // Open through one builder (UX-GUI-014) - so losing one is a look of its own.
        Assert.Equal(6, Calls(inLinksFile: true).Count);
    }

    [Fact]
    public void Help_ends_the_top_row_in_line_with_Columns_and_Donate_stands_beside_it()
    {
        var window = WpfHost.Window();

        var (id, name, hint, heart, donateRight, helpLeft, helpRight, columnsRight) = WpfHost.On(() =>
        {
            window.WindowStyle = WindowStyle.None;
            window.ShowInTaskbar = false;
            window.Left = -4000;
            window.Show();
            window.UpdateLayout();

            var donate = window.Scope.Donate;
            var help = window.Scope.Help;
            var mark = FirstPath(donate);

            return (
                AutomationProperties.GetAutomationId(donate),
                AutomationProperties.GetName(donate),
                donate.ToolTip as string,
                mark?.ActualWidth ?? 0,
                donate.TransformToAncestor(window).Transform(new Point(donate.ActualWidth, 0)).X,
                help.TransformToAncestor(window).Transform(new Point(0, 0)).X,
                help.TransformToAncestor(window).Transform(new Point(help.ActualWidth, 0)).X,
                window.ColumnsButton.TransformToAncestor(window).Transform(new Point(window.ColumnsButton.ActualWidth, 0)).X);
        });

        WpfHost.On(window.Close);

        Assert.Equal("donate", id);
        Assert.Equal(Texts.Of("gui.support.button"), name);
        Assert.Equal(Texts.Of("gui.support.hint"), hint);

        // Drawn, not only declared - a Path with no size is a heart nobody sees.
        Assert.Equal((double)WpfHost.Resources["SizeSupportMark"], heart);

        // GUI rule 19: the last control of the row aligned with the last control of the filters row
        // rather than placed by eye. HELP SINCE 2026-09-24 (UX-GUI-014) - the corner is where people
        // look for help - and Donate stands just left of it, one gap between them.
        Assert.Equal(columnsRight, helpRight, precision: 1);
        Assert.Equal(
            ((System.Windows.Thickness)WpfHost.Resources["MarginBetweenControls"]).Right,
            helpLeft - donateRight,
            precision: 1);
    }

    private static System.Windows.Shapes.Path? FirstPath(DependencyObject root)
    {
        if (root is System.Windows.Shapes.Path path)
        {
            return path;
        }

        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (FirstPath(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Every line of code in the window that takes an address to the shell.
    ///
    /// Outside the links file a call has to name the class, so that is what is looked for - and a
    /// bare word would find the typing timer's Start and every method whose name begins with Open.
    /// Inside it the three are declared and called bare, and the canary counts those as whole
    /// words, so OpenSupport - the named destination - is not one of them.
    /// </summary>
    private static List<string> Calls(bool inLinksFile)
    {
        var outside = new Regex(@"\bExternalLinks\.(Open|Start)\(", RegexOptions.None, TimeSpan.FromSeconds(5));
        var inside = new Regex(@"\b(Open|Start|HandToDesktop)\(", RegexOptions.None, TimeSpan.FromSeconds(5));

        // The walk names a directory rather than a file, because a guard that reads sources by path
        // goes quiet the moment the code it guards moves.
        var window = Path.Combine(SourceTree.Root(), "src", "Bws.Gui");
        var hits = new List<string>();

        foreach (var file in Directory.EnumerateFiles(window, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(window, file).Replace('\\', '/');

            if (relative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileName(file), LinksFile, StringComparison.Ordinal) != inLinksFile)
            {
                continue;
            }

            var lines = File.ReadAllLines(file);

            for (var index = 0; index < lines.Length; index++)
            {
                // The code half of the line only. The address itself holds two slashes, which is
                // harmless here because the calls stand before it.
                var code = lines[index].Split("//", 2)[0];

                if ((inLinksFile ? inside : outside).IsMatch(code))
                {
                    hits.Add($"{relative}:{index + 1}");
                }
            }
        }

        return hits;
    }
}