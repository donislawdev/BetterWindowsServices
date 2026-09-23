namespace Bws.Cli.Tests;

/// <summary>
/// What this build refuses to try to read as a snapshot. Backlog 308(c).
///
/// <b>The reader builds ONE STRING out of every byte in the file.</b> A path pointing at something
/// enormous therefore ends the process with an out of memory failure and a stack trace - which is
/// the one shape the entry point's own catch cannot turn into a sentence worth reading, and which
/// arrives with an exit code no script can be expected to know.
///
/// <b>Measured rather than guessed:</b> a snapshot of this machine, 799 entries, is 1.009 MB. The
/// ceiling is sixty four, so it refuses files that are not snapshots rather than snapshots that are
/// large.
/// </summary>
public sealed class SnapshotSizeGuards : IDisposable
{
    private readonly List<string> _made = [];

    /// <summary>
    /// A file too big to be a snapshot is turned back BEFORE it is opened.
    ///
    /// <b>THE FIRST VERSION OF THIS TEST PROVED NOTHING AND THE MUTATION REGISTRY SAID SO.</b> It
    /// asserted the refusal and its exit code - and a file of sixty five megabytes of nothing is
    /// also not a snapshot, so the run without the ceiling refused it too, with the same code, after
    /// reading every byte. A test that passes whether or not the code is there is the shape this
    /// project builds the registry to catch.
    ///
    /// <b>So the file is held open with no sharing, and that is what tells the two apart.</b>
    /// Asking the file system how long a file is needs no handle, so the ceiling answers with the
    /// code for what somebody typed. Opening it does need one, so without the ceiling the read
    /// fails as a machine problem instead - a different code, on the same file, from the same call.
    /// The distinction docs/12 step 5 asks about, standing in for a memory figure no test can see.
    ///
    /// <b>The size is set rather than written, which keeps this a test somebody will run.</b>
    /// Asking for a length is instant on NTFS - writing sixty five megabytes on every run to prove a
    /// rule about size would earn its own line in a report about slow suites.
    /// </summary>
    [Fact]
    public void A_file_too_big_to_be_a_snapshot_is_turned_back_before_it_is_even_opened()
    {
        var path = Somewhere("enormous.json");

        using (var big = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            big.SetLength(65L * 1024 * 1024);
        }

        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.False(SnapshotFiles.Load(path, out var snapshot, out var code));
        Assert.Null(snapshot);
        Assert.Equal(ExitCode.Usage, code);
    }

    /// <summary>
    /// And a file of an ordinary size still gets as far as being read, so the ceiling is not
    /// standing in front of everything.
    ///
    /// <b>Its own assertion because a rule that refuses too much looks exactly like a rule that
    /// works.</b> What is asserted is that this one fails LATER and for a different reason - it is
    /// not a snapshot rather than not being allowed to be one.
    /// </summary>
    [Fact]
    public void A_file_of_an_ordinary_size_is_read_and_judged_on_what_is_in_it()
    {
        var path = Somewhere("small.json");

        File.WriteAllText(path, "{ \"nothing\": true }");

        Assert.False(SnapshotFiles.Load(path, out var snapshot, out var code));
        Assert.Null(snapshot);
        Assert.Equal(ExitCode.Usage, code);
    }

    private string Somewhere(string name)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "bws-snapshot-size-tests",
            Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));

        Directory.CreateDirectory(directory);
        _made.Add(directory);

        return Path.Combine(directory, name);
    }

    public void Dispose()
    {
        foreach (var directory in _made.Where(Directory.Exists))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
