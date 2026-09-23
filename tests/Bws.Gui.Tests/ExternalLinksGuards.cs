using System.IO;
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
        // satisfies the assertion above and looks exactly like a guard that works. Five is what the
        // links file has - the three declarations, the call OpenSupport makes to Open, and the one
        // Process.Start - so losing one is a look of its own.
        Assert.Equal(5, Calls(inLinksFile: true).Count);
    }

    [Fact]
    public void The_button_stands_at_the_right_end_of_the_top_row_named_by_its_word()
    {
        var window = WpfHost.Window();

        var (id, name, hint, heart, donateRight, columnsRight) = WpfHost.On(() =>
        {
            window.WindowStyle = WindowStyle.None;
            window.ShowInTaskbar = false;
            window.Left = -4000;
            window.Show();
            window.UpdateLayout();

            var donate = window.Scope.Donate;
            var mark = FirstPath(donate);

            return (
                AutomationProperties.GetAutomationId(donate),
                AutomationProperties.GetName(donate),
                donate.ToolTip as string,
                mark?.ActualWidth ?? 0,
                donate.TransformToAncestor(window).Transform(new Point(donate.ActualWidth, 0)).X,
                window.ColumnsButton.TransformToAncestor(window).Transform(new Point(window.ColumnsButton.ActualWidth, 0)).X);
        });

        WpfHost.On(window.Close);

        Assert.Equal("donate", id);
        Assert.Equal(Texts.Of("gui.support.button"), name);
        Assert.Equal(Texts.Of("gui.support.hint"), hint);

        // Drawn, not only declared - a Path with no size is a heart nobody sees.
        Assert.Equal((double)WpfHost.Resources["SizeSupportMark"], heart);

        // GUI rule 19: aligned with the last control of the filters row rather than placed by eye.
        Assert.Equal(columnsRight, donateRight, precision: 1);
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