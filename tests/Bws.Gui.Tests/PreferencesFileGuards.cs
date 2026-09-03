// Explicit, because UseWPF swaps the implicit using set and takes System.IO out of it.
using System.IO;
using System.Text;
using Bws.Gui.ViewModels;

namespace Bws.Gui.Tests;

/// <summary>
/// The layout as a file on a disk, which is where the failures nobody plans for live.
///
/// <b>Split from <see cref="ColumnLayoutGuards"/> on 2026-08-13 and the seam is the product's
/// own.</b> That class is about what a layout means; this one is about a real path, a real profile
/// and the four ways a real file refuses: it is not there, something else is holding it open, it
/// cannot be moved out of the way, and there is nowhere to put it. None of those is exotic on the
/// machines this tool is for - a roaming profile is synchronised, and backup and antivirus both
/// open files nobody asked them to.
///
/// <b>Every test here writes into a temporary directory of its own and deletes it.</b> Reading the
/// real profile would make the answers depend on which columns somebody had turned on last night,
/// and writing it would rearrange their window from a test run.
/// </summary>
public sealed class PreferencesFileGuards : IDisposable
{
    private readonly List<string> _made = [];

    /// <summary>
    /// No file is the ordinary first run, and the ordinary answer to somebody deleting it.
    ///
    /// The second half of the promise `docs/04` writes for this slice: delete the file and the
    /// usual layout comes back, with nothing to report because nothing went wrong.
    /// </summary>
    [Fact]
    public void With_no_file_there_is_nothing_to_say_and_the_usual_columns_are_shown()
    {
        var reading = Fresh().Read();

        Assert.Null(reading.Layouts);
        Assert.False(reading.WorthSaying);
        Assert.Equal(
            ColumnLayout.DefaultFor(EntryScope.Services).Columns,
            ColumnPlan.Of(reading.Layouts?.Services, EntryScope.Services).Layout.Columns);
    }

    /// <summary>
    /// Written and read back through a real file, which is the whole promise in one test.
    /// </summary>
    [Fact]
    public void A_layout_written_to_disk_is_the_layout_that_comes_back()
    {
        var file = Fresh();

        var written = new ColumnLayout(
        [
            .. Columns.All.Select(column => new KeptColumn(
                column.Id,
                Shown: column.Id == "status",
                Width: column.Id == "status" ? "444" : null))
        ]);

        Assert.Null(file.Write(ColumnLayouts.Default with { Services = written }));

        var read = file.Read();

        Assert.Equal(written.Columns, read.Layouts?.Services.Columns);
    }

    /// <summary>
    /// A file that will not parse is moved aside rather than overwritten, and its content survives.
    ///
    /// `ADR-18`. A layout is a small thing to lose, but the rule is not about size: the file a
    /// person cannot read is sometimes the only trace of what they had, and this is a product whose
    /// subject is keeping evidence. It is also what lets the next write succeed at all.
    /// </summary>
    [Fact]
    public void An_unreadable_file_is_moved_aside_with_its_content_intact()
    {
        var file = Fresh();

        File.WriteAllText(file.Where, "this is not a layout");

        var reading = file.Read();

        Assert.NotNull(reading.Unreadable);
        Assert.NotNull(reading.MovedAside);
        Assert.False(File.Exists(file.Where));
        Assert.Equal("this is not a layout", File.ReadAllText(reading.MovedAside!));
    }

    /// <summary>
    /// Bytes that are not text this build reads are refused rather than guessed at.
    ///
    /// <b>Until 2026-08-26 this read the file with a decoder that substitutes a replacement
    /// character for anything it cannot make sense of, and says nothing about having done it.</b>
    /// The two files in this product were answering one question in two ways: SnapshotFiles chose
    /// the refusing decoder and wrote down what the other choice cost - a snapshot saved once in
    /// the machine's own code page came back as 297 entries changed, none of which had.
    ///
    /// The stakes here are smaller and the argument is the same one. It is quarantined like any
    /// other file that turns out not to be a layout, because it is the only trace of what somebody
    /// had.
    /// </summary>
    [Fact]
    public void Bytes_that_are_not_text_this_build_reads_are_refused_rather_than_guessed_at()
    {
        var file = Fresh();

        // A LAYOUT THAT IS OTHERWISE PERFECTLY GOOD, and the whole discrimination is in that word.
        // The first version of this test wrote five bytes of nonsense - which the substituting
        // decoder also turns into something that will not parse, so both answers came out the same
        // and the mutation entry came back MISSED. Measured 2026-08-26 rather than reasoned, and it
        // is the ordinary way a guard about refusing ends up proving nothing.
        //
        // So the file has to be one that READS FINE once a byte is quietly replaced: a real layout
        // this program wrote, with one extra key nothing looks at, holding one byte that is not
        // UTF-8. Substituting decoder - it parses, the key is ignored, the layout comes back.
        // Refusing decoder - this is not text this build reads, and nothing is guessed at.
        Assert.Null(file.Write(ColumnLayouts.Default));

        var good = File.ReadAllText(file.Where).TrimEnd();
        var opening = good[..good.LastIndexOf('}')] + ",\"note\":\"";

        File.WriteAllBytes(
            file.Where,
            [.. Encoding.UTF8.GetBytes(opening), 0x9F, .. Encoding.UTF8.GetBytes("\"}")]);

        var reading = file.Read();

        Assert.NotNull(reading.Unreadable);
        Assert.NotNull(reading.MovedAside);
        Assert.False(File.Exists(file.Where));
    }

    /// <summary>
    /// A file something else is holding open comes back as a reason rather than as a crash.
    ///
    /// <b>Ordinary rather than exotic on the machines this tool is for:</b> a roaming profile is
    /// synchronised, and backup and antivirus both open files nobody asked them to. The window is
    /// mid-construction when this read happens, so an exception here is a program that will not
    /// start because of a preferences file.
    /// </summary>
    [Fact]
    public void A_file_something_else_is_holding_comes_back_as_a_reason()
    {
        var file = Fresh();

        File.WriteAllText(file.Where, ColumnLayouts.Default.Render());

        using var held = new FileStream(file.Where, FileMode.Open, FileAccess.Read, FileShare.None);

        var reading = file.Read();

        Assert.Null(reading.Layouts);
        Assert.NotNull(reading.Unreadable);
    }

    /// <summary>
    /// A damaged file that cannot even be moved aside says so, rather than claiming it moved.
    ///
    /// <b>The difference matters because of what the sentence tells somebody to expect.</b> Moved
    /// aside means this happens once and the next close writes a fresh file. Still there means it
    /// will happen again every time they open the window, and that is worth knowing rather than
    /// discovering.
    /// </summary>
    [Fact]
    public void A_damaged_file_that_cannot_be_moved_says_so_rather_than_claiming_it_moved()
    {
        var file = Fresh();

        File.WriteAllText(file.Where, "not a layout");

        // Readable and unmovable at once, which is exactly what a synchronisation client holding a
        // handle looks like: the content comes back and the rename is refused.
        using var held = new FileStream(file.Where, FileMode.Open, FileAccess.Read, FileShare.Read);

        var reading = file.Read();

        Assert.NotNull(reading.Unreadable);
        Assert.Null(reading.MovedAside);
        Assert.True(File.Exists(file.Where), "The file is gone, so something moved it after all.");
    }

    /// <summary>
    /// A layout from another schema is still on disk, byte for byte, after being refused.
    ///
    /// The assertion the class comment argues for: refusing to read it and refusing to touch it
    /// are the same decision, and only the second one can be checked from outside.
    /// </summary>
    [Fact]
    public void A_file_from_another_schema_is_left_exactly_where_it_was()
    {
        var file = Fresh();
        var content = """{ "columns": [], "schemaVersion": 9 }""";

        File.WriteAllText(file.Where, content);

        Assert.Equal(9, file.Read().OtherSchemaVersion);
        Assert.Equal(content, File.ReadAllText(file.Where));
    }

    /// <summary>
    /// The folder is made when it is not there, which is the boundary `ADR-18` left open.
    ///
    /// The rule that survives both cases: a directory the tool CHOSE may be created, one it was
    /// HANDED may not. A snapshot into a directory that does not exist is still refused, and
    /// <c>AtomicFileTests</c> holds that end of it.
    /// </summary>
    [Fact]
    public void The_folder_in_the_profile_is_made_rather_than_demanded()
    {
        var file = Fresh(create: false);

        Assert.Null(file.Write(ColumnLayouts.Default));
        Assert.True(File.Exists(file.Where));
    }

    /// <summary>
    /// A write that cannot happen says so instead of taking the window down.
    ///
    /// <b>Every way this fails is a fact about somebody's machine</b> - a roaming profile on a
    /// share that went away, a folder an administrator locked down. A file is used here to stand
    /// in for the folder, which is the one refusal a test can produce on any machine without
    /// changing anything about it.
    /// </summary>
    [Fact]
    public void A_layout_that_cannot_be_written_comes_back_as_a_sentence()
    {
        var directory = Path.Combine(Somewhere(), "in-the-way");

        File.WriteAllText(directory, "a file where the folder should be");

        var trouble = new PreferencesFile(directory).Write(ColumnLayouts.Default);

        Assert.False(string.IsNullOrWhiteSpace(trouble));
    }

    /// <summary>An account with no application data folder is a sentence, not a crash.</summary>
    [Fact]
    public void With_nowhere_in_the_profile_to_write_the_refusal_is_a_sentence()
    {
        var trouble = new PreferencesFile(string.Empty).Write(ColumnLayouts.Default);

        Assert.False(string.IsNullOrWhiteSpace(trouble));
        Assert.False(File.Exists(PreferencesFile.Name));
    }

    /// <summary>
    /// Everything a layout could not give the window arrives as one sentence naming the parts.
    ///
    /// <b>One sentence rather than four, and the identifiers are in it.</b> A person is being told
    /// about lines in a file they can open, so the sentence has to use the words that are in it -
    /// a heading would send them looking for "Display name" in a file that says `displayName`.
    /// </summary>
    [Fact]
    public void Everything_the_layout_could_not_give_the_window_is_named_in_one_sentence()
    {
        var file = Fresh();

        File.WriteAllText(
            file.Where,
            """
            {
              "columns": [
                { "id": "colourOfTheIcon", "shown": true },
                { "id": "serviceName", "shown": false, "width": "as wide as it likes" },
                { "id": "serviceName", "shown": false }
              ],
              "schemaVersion": 1
            }
            """);

        var said = new KeptColumns(file).Trouble(["serviceName"]);

        Assert.NotNull(said);
        Assert.Contains("colourOfTheIcon", said, StringComparison.Ordinal);
        Assert.Contains("serviceName", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// A layout from another schema is named BY ITS NUMBER in the window, not just refused.
    ///
    /// The number is the only thing that tells somebody which of their two builds wrote it, and
    /// therefore which one to open if they want the arrangement back.
    /// </summary>
    [Fact]
    public void A_layout_from_another_schema_is_named_by_its_number_in_the_window()
    {
        var file = Fresh();

        File.WriteAllText(file.Where, """{ "columns": [], "schemaVersion": 7 }""");

        var said = new KeptColumns(file).Trouble([]);

        Assert.NotNull(said);
        Assert.Contains("7", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing to say when the file is the one the window wrote, which is the ordinary case.
    ///
    /// Without this the four tests above pass just as well against a class that complains about
    /// every layout it is ever given.
    /// </summary>
    [Fact]
    public void A_layout_the_window_wrote_itself_has_nothing_to_say_about_it()
    {
        var file = Fresh();

        Assert.Null(file.Write(ColumnLayouts.Default));
        Assert.Null(new KeptColumns(file).Trouble([]));
    }

    /// <summary>
    /// A file too big to be a layout is refused before it is read - backlog 308(c).
    ///
    /// <b>The reader builds ONE STRING out of every byte in the file</b>, so a path that happens to
    /// point at something enormous is a window that will not open, with an out of memory failure in
    /// place of a sentence - and this read happens while the window is being constructed.
    ///
    /// <b>The size is set rather than written, which is how this stays a test somebody will run.</b>
    /// Asking the file system for a length is instant on NTFS; writing two megabytes to disk on
    /// every run to prove a rule about size would be a test that earns its own line in a report
    /// about slow suites.
    ///
    /// Quarantined like any other file that turns out not to be a layout, which is `ADR-18` and
    /// also what lets the next write succeed.
    /// </summary>
    [Fact]
    public void A_file_too_big_to_be_a_layout_is_refused_rather_than_read_into_memory()
    {
        var file = Fresh();

        Assert.Null(file.Write(ColumnLayouts.Default));

        using (var big = new FileStream(file.Where, FileMode.Create, FileAccess.Write))
        {
            big.SetLength(2L * 1024 * 1024);
        }

        var reading = file.Read();

        // THE SENTENCE RATHER THAN ONLY THE REFUSAL, AND THE MUTATION REGISTRY IS WHY. Two
        // megabytes of nothing is also not a layout, so a run without the ceiling refuses it too -
        // after reading every byte - and a test asserting only that something was refused would
        // pass either way. What names the ceiling is the reason it gives.
        Assert.Equal(Bws.Gui.Texts.Of("gui.layout.tooBig", 1024), reading.Unreadable);
        Assert.NotNull(reading.MovedAside);
        Assert.False(File.Exists(file.Where));
    }

    private PreferencesFile Fresh(bool create = true) =>
        new(create ? Somewhere() : Path.Combine(Somewhere(), "not-yet"));

    private string Somewhere()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "bws-layout-tests",
            Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));

        Directory.CreateDirectory(directory);
        _made.Add(directory);

        return directory;
    }

    public void Dispose()
    {
        foreach (var directory in _made.Where(Directory.Exists))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
