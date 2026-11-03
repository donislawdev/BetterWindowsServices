using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Bws.Architecture.Tests;

/// <summary>
/// The archives FeedGuards puts in the release folders it builds: written from named entries, and when a
/// case needs one, an archive whose headers lie about the size of an entry. The bytes of the zip format
/// live here, so the guard keeps to what a package and its script must do.
/// </summary>
internal static class ReleaseArchive
{
    /// <summary>An archive of the named entries, each a copy of its source file, or a line of text when it has none.</summary>
    internal static void Write(string path, (string Name, string? Source)[] entries)
    {
        using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (name, source) in entries)
        {
            Pack(archive, name, source);
        }
    }

    /// <summary>
    /// Sets the size the local and the central header of an archive declare for one entry, and leaves its
    /// bytes as they are - an archive that lies about what it holds, which only a hand makes.
    /// </summary>
    internal static void Declare(string path, string name, uint size)
    {
        var bytes = File.ReadAllBytes(path);
        var named = Encoding.UTF8.GetBytes(name);
        var patched = 0;

        // Each header as its signature, where it keeps the uncompressed size, the name's length and the name.
        foreach (var (signature, sizeAt, lengthAt, nameAt) in new[] { (0x04034b50u, 22, 26, 30), (0x02014b50u, 24, 28, 46) })
        {
            for (var at = 0; at + nameAt + named.Length <= bytes.Length; at++)
            {
                var header = bytes.AsSpan(at);
                if (BinaryPrimitives.ReadUInt32LittleEndian(header) == signature && BinaryPrimitives.ReadUInt16LittleEndian(header[lengthAt..]) == named.Length
                    && header.Slice(nameAt, named.Length).SequenceEqual(named))
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(header[sizeAt..], size);
                    patched++;
                }
            }
        }

        Assert.True(patched == 2, $"Found {patched} headers naming {name} in {Path.GetFileName(path)}, and an archive holding it once has two.");
        File.WriteAllBytes(path, bytes);
    }

    // Stored, not compressed: the .NET reader returns every byte of a stored entry whatever its header
    // declares, which is what the lying archives of FeedGuards rely on.
    private static void Pack(ZipArchive archive, string name, string? source)
    {
        if (source is not null)
        {
            archive.CreateEntryFromFile(source, name, CompressionLevel.NoCompression);
            return;
        }

        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write("stand-in for " + name);
    }
}
