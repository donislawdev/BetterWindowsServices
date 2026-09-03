using System.IO;
using System.Text;
using Bws.Core;
using Bws.Core.Snapshots;
using Bws.Gui.ViewModels;

namespace Bws.Gui;

/// <summary>
/// The one file this product keeps for the person rather than for the machine.
///
/// <b>What goes in it is a preference, and that is section H of the specification deciding the
/// filing rather than a naming habit.</b> Tags and notes describe a particular server and travel
/// with it. A column layout describes a person and travels with them - so this lives in the
/// roaming half of the profile, which is the part of Windows that means exactly that, and never
/// beside the machine's own data.
///
/// <b>Beside the executable is NOT read yet, and that is the plan's own division rather than an
/// omission.</b> Section H says a configuration file lying next to the program wins over the one
/// in the profile, and `docs/04` gives that mechanism to `S8` along with the rest of portable
/// mode. When it arrives it adds a place to look before this one - it does not change what is
/// written here or what it is called, which is why the name carries the product: a file called
/// <c>bws-preferences.json</c> can be copied from a profile to a folder full of other people's
/// tools and still be obviously ours.
///
/// <b>It creates its own folder, and this is the boundary `ADR-18` left open.</b> That decision
/// says the tool does not invent directories, and it was written about snapshots, where the path
/// comes from a person who can mistype it - inventing a tree from a typo scatters somebody's files
/// where they will not look for them. Nobody typed this path. The rule that survives both cases is
/// that <b>the tool may create a directory it chose itself and never one it was handed</b>: the
/// profile folder here is created, and a snapshot into a directory that is not there is still
/// refused. Checked against the second case before it was written down, which is rule 12.
/// </summary>
internal sealed class PreferencesFile
{
    /// <summary>
    /// What the file is called, in both of the places section H can put it.
    ///
    /// One name rather than a short one in the profile and a qualified one beside the program:
    /// a person moving to portable mode copies the file across and it works, which is the whole
    /// promise of "the admin who copies the executable to the twentieth server".
    /// </summary>
    internal const string Name = "bws-preferences.json";

    /// <summary>The folder in the profile, which is the product's name and nothing else.</summary>
    internal const string Folder = "BetterWindowsServices";

    private readonly string _directory;

    internal PreferencesFile()
        : this(InTheProfile())
    {
    }

    /// <summary>
    /// Somewhere else, which is how a test gets one of these without touching a real profile.
    ///
    /// A seam of the same kind as <see cref="ViewModels.Says.Elevated"/>: the working answer is
    /// the one a person gets, and a test cannot be asked to write into the account it runs as.
    /// </summary>
    internal PreferencesFile(string directory) => _directory = directory;

    /// <summary>Where the file is, whether or not anything is there.</summary>
    internal string Where => Path.Combine(_directory, Name);

    /// <summary>
    /// The largest this file may be before it is treated as not being one.
    ///
    /// A megabyte, against a real one of eighteen short lines. The number is not a judgement about
    /// how many columns anybody may keep - it is the point past which the thing at that path is
    /// something else.
    /// </summary>
    private const long Biggest = 1024 * 1024;

    /// <summary>
    /// Reads the layout back, moving the file aside if it turns out not to be one.
    ///
    /// <b>Quarantine rather than overwrite</b> - `ADR-18`. A file that will not parse is often the
    /// only remaining trace of what somebody had, and this is a tool whose whole subject is
    /// keeping evidence. It is also the only way the next write can succeed without destroying
    /// something a person may want back.
    /// </summary>
    internal LayoutReading Read()
    {
        string content;

        try
        {
            if (_directory.Length == 0 || !File.Exists(Where))
            {
                // The ordinary first run, and the ordinary answer to somebody deleting the file:
                // nothing is there, nothing went wrong, and the window opens as it always did.
                return new LayoutReading();
            }

            // ASKED HOW BIG IT IS BEFORE READING IT ALL, SINCE 2026-09-03 - backlog 308(c). The
            // reader below builds one string out of every byte in the file, so a path that happens
            // to point at something enormous is a window that will not open, with an out of memory
            // failure in place of a sentence. This file is eighteen short lines, so a megabyte is
            // a thousandfold allowance rather than a limit anybody could meet by using the
            // program - and it is quarantined like any other file that turns out not to be a
            // layout, which is also what lets the next write succeed.
            if (new FileInfo(Where).Length > Biggest)
            {
                return new LayoutReading
                {
                    Unreadable = Texts.Of("gui.layout.tooBig", Biggest / 1024),
                    MovedAside = Aside()
                };
            }

            content = ReadText(Where);
        }
        catch (IOException problem)
        {
            return new LayoutReading { Unreadable = problem.Message };
        }
        catch (UnauthorizedAccessException problem)
        {
            return new LayoutReading { Unreadable = problem.Message };
        }
        catch (DecoderFallbackException problem)
        {
            // BYTES THAT ARE NOT TEXT THIS BUILD READS ARE REFUSED RATHER THAN GUESSED AT, and
            // until 2026-08-26 this used File.ReadAllText, which substitutes a replacement
            // character for every byte it cannot make sense of and says nothing.
            //
            // SnapshotFiles.ReadText decided the same question the other way round and wrote the
            // measurement down: a snapshot saved once in the machine's code page came back as 297
            // entries changed, none of which had. The stakes here are far smaller - a layout that
            // will not parse goes to quarantine either way - but the two files were answering one
            // question differently, and the one with a measurement behind it is the one to keep.
            //
            // Quarantined like any other file that turns out not to be a layout: it is the only
            // trace of what somebody had, and moving it aside is also what lets the next write
            // succeed. `ADR-18`.
            return new LayoutReading { Unreadable = problem.Message, MovedAside = Aside() };
        }

        var reading = ColumnLayouts.Read(content);

        return reading.Unreadable is null ? reading : reading with { MovedAside = Aside() };
    }

    /// <summary>
    /// Writes the layout, and says what stopped it when something did.
    ///
    /// <b>A sentence back rather than an exception</b>, because every way this fails is an
    /// ordinary fact about somebody's machine - a profile on a share that went away, a folder an
    /// administrator locked down, a disk that is full - and none of them is the window falling
    /// over while somebody is using it.
    /// </summary>
    internal string? Write(ColumnLayouts layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (_directory.Length == 0)
        {
            return Texts.Of("gui.layout.noProfile");
        }

        try
        {
            // Before the write rather than inside AtomicFile, which refuses to invent a directory
            // and must keep refusing - see this class's own note about which paths may be created.
            Directory.CreateDirectory(_directory);

            AtomicFile.Write(Where, layout.Render());

            return null;
        }
        catch (IOException problem)
        {
            return problem.Message;
        }
        catch (UnauthorizedAccessException problem)
        {
            return problem.Message;
        }
    }

    /// <summary>
    /// The file as text, refusing bytes that are not text this build can read.
    ///
    /// Word for word the decoder <c>SnapshotFiles.ReadText</c> uses, and for the reason written
    /// there. A byte order mark is still honoured, so a file something else saved as UTF-16 still
    /// reads - only bytes that are none of those come back as a refusal.
    /// </summary>
    /// <exception cref="DecoderFallbackException">The bytes are not text this build can read.</exception>
    private static string ReadText(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }

    /// <summary>Moves the unreadable file out of the way, and says where it went, or nothing.</summary>
    private string? Aside()
    {
        try
        {
            return AtomicFile.Quarantine(Where, new SystemClock());
        }
        catch (IOException)
        {
            // It stays where it is, and the window says so by having nowhere to point at. The
            // reason the original file could not be read is what the person needs, and losing it
            // behind a tidying-up failure would replace a real answer with a meaningless one.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The folder in the roaming profile, or nothing when Windows names none.
    ///
    /// An account can genuinely have no application data folder - a service account without a
    /// loaded profile is the ordinary way to meet one - and the answer is to say so rather than to
    /// fall back on the current directory, which is wherever somebody happened to start the
    /// program from.
    /// </summary>
    private static string InTheProfile()
    {
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        return roaming.Length == 0 ? string.Empty : Path.Combine(roaming, Folder);
    }
}
