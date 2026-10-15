using System.IO;
using System.Text.RegularExpressions;

namespace Bws.Gui.Tests;

/// <summary>
/// Where the window's file dialogs start - backlog 562, owner's decision of 2026-10-14.
///
/// <b>Built, never opened.</b> A dialog opened from a session in the background never appears, so
/// what is checked is the dialog as the window hands it to Windows: a default folder, no initial
/// folder, and the filter it always had. Whether Windows then shows that folder is Windows' half,
/// and it is answered by opening the window from Explorer and pressing Snapshots.
///
/// <b>And that nothing else in the window builds one</b>, because a fourth dialog written out by
/// hand would start in System32 again without a word - which is the whole of 562.
/// </summary>
public sealed class FileDialogGuards
{
    private const string DialogsFile = "FileDialogs.cs";

    [Fact]
    public void Every_dialog_starts_in_Documents_unless_Windows_remembers_a_folder()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        var (saveDefault, saveInitial, openDefault, openInitial) = WpfHost.On(() =>
        {
            var save = FileDialogs.Save("a.json", ".json", "Snapshot");
            var open = FileDialogs.Open(".json", "Snapshot", "Choose a snapshot");

            return (save.DefaultDirectory, save.InitialDirectory, open.DefaultDirectory, open.InitialDirectory);
        });

        Assert.Equal(documents, saveDefault);
        Assert.Equal(documents, openDefault);

        // EMPTY, and that is the decision rather than a gap: an initial folder wins over what Windows
        // remembers, so a person who keeps snapshots in one folder would be sent away from it on
        // every press.
        Assert.Equal(string.Empty, saveInitial);
        Assert.Equal(string.Empty, openInitial);

        // And never the folder 562 was about. Asked only when there is an answer at all - a profile
        // with no Documents gives an empty one, which the dialog treats as no default.
        if (documents.Length > 0)
        {
            Assert.False(
                documents.StartsWith(windows, StringComparison.OrdinalIgnoreCase),
                $"the dialogs would start under {windows}: {documents}");
        }
    }

    [Fact]
    public void The_dialogs_keep_the_name_extension_and_filter_they_always_had()
    {
        var (name, extension, filter, adds, openFilter, mustExist, title) = WpfHost.On(() =>
        {
            var save = FileDialogs.Save("services.csv", ".csv", "CSV file");
            var open = FileDialogs.Open(".json", "Snapshot", "Choose the first of two snapshots to compare");

            return (save.FileName, save.DefaultExt, save.Filter, save.AddExtension, open.Filter, open.CheckFileExists, open.Title);
        });

        // Pinned to the strings the three call sites built by hand until 2026-10-14, so moving them
        // here changed where a dialog starts and nothing else.
        Assert.Equal("services.csv", name);

        // WITHOUT THE DOT, and that is WPF rather than us - measured 2026-10-14, the first run of
        // this test expected ".csv" back. The property is set with the dot, as every call site set
        // it before, and the dialog keeps the extension bare.
        Assert.Equal("csv", extension);
        Assert.Equal("CSV file (*.csv)|*.csv", filter);
        Assert.True(adds);
        Assert.Equal("Snapshot (*.json)|*.json", openFilter);
        Assert.True(mustExist);

        // Since 2026-10-15 an open dialog says what it is choosing for - two of them come one after
        // the other when two snapshots are compared, and "Open" alone said nothing about which was which.
        Assert.Equal("Choose the first of two snapshots to compare", title);
    }

    [Fact]
    public void No_file_dialog_is_built_outside_the_one_file()
    {
        var offenders = Mentions(inDialogsFile: false);

        Assert.True(
            offenders.Count == 0,
            "a file dialog is built outside " + DialogsFile + " - it would start wherever the process"
                + " happens to stand, System32 when Explorer started it. Ask FileDialogs for it:"
                + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void The_scan_reaches_the_file_it_claims_to_cover()
    {
        // The canary. A walk that reads no files, or a pattern that matches nothing, satisfies the
        // assertion above and looks exactly like a guard that works. Four is what the dialogs file
        // has: the save dialog and the open dialog, each named as what its method hands back and
        // again where it is built.
        Assert.Equal(4, Mentions(inDialogsFile: true).Count);
    }

    /// <summary>
    /// Lines naming a file dialog type in the code half, in the dialogs file or everywhere else in
    /// the window. A type name rather than a construction, so a target-typed new with the type on
    /// the left is caught as well.
    /// </summary>
    private static List<string> Mentions(bool inDialogsFile)
    {
        var named = new Regex(@"\b(SaveFileDialog|OpenFileDialog|OpenFolderDialog)\b", RegexOptions.None, TimeSpan.FromSeconds(5));

        // A directory rather than a file, so the guard does not go quiet the day the code moves.
        var window = Path.Combine(SourceTree.Root(), "src", "Bws.Gui");
        var hits = new List<string>();

        foreach (var file in Directory.EnumerateFiles(window, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(window, file).Replace('\\', '/');

            if (relative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileName(file), DialogsFile, StringComparison.Ordinal) != inDialogsFile)
            {
                continue;
            }

            var lines = File.ReadAllLines(file);

            for (var index = 0; index < lines.Length; index++)
            {
                // The code half only - a comment may name the type to explain where it went.
                if (named.IsMatch(lines[index].Split("//", 2)[0]))
                {
                    hits.Add($"{relative}:{index + 1}");
                }
            }
        }

        return hits;
    }
}
