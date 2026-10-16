using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Bws.Core.Tests;

/// <summary>
/// The version is the one the file carries itself - package SB, the owner's decision of 2026-10-06.
///
/// <b>The judge reads the file's bytes and nothing else.</b> The version resource is found by walking
/// the PE resource directory with the standard library's PEReader - no version.dll, no language file,
/// nothing Windows could redirect - so it cannot agree with the product by sharing its mistake.
///
/// <b>Asked of a copy of a real system file whose language files are made to disagree with it.</b>
/// The difference this guards only exists where the language file Windows finds carries another
/// version than the binary - on the machine this was written on, svchost.exe says 10.0.26100.8737 in
/// its own resource and 10.0.26100.8875 through the framework's localised read. Until 2026-10-16 the
/// test asked the file in System32 itself, said that a machine where the two agree proves nothing,
/// and the mutation registry's first runs on GitHub came back MISSED twice for exactly that reason.
/// So the test builds the disagreement: a copy of the binary in a folder of its own and, in the
/// language folders beside it, copies of its language files with every digit of their FileVersion
/// moved on by one. Measured that day: Windows's localised read of such a copy gives the moved
/// string, and the file's own resource still gives 8737. The test checks the first of those before
/// it asks anything, so a machine where Windows stops looking beside the copy fails it rather than
/// passing it with nothing proved.
/// </summary>
public sealed class OwnVersionTests
{
    /// <summary>RT_VERSION, the resource type that holds VS_VERSIONINFO.</summary>
    private const int VersionType = 16;

    /// <summary>VS_FIXEDFILEINFO's signature, which sits right after the "VS_VERSION_INFO" key.</summary>
    private const uint FixedSignature = 0xFEEF04BD;

    [Fact]
    public void The_version_of_a_system_binary_is_the_one_in_its_own_resource()
    {
        using var specimen = Disagreeing.Copy("svchost.exe");

        // NOT VACUOUS: the localised read - the framework's, and the one the mutation puts back - gives
        // a language file's version for this copy, so a reader that consulted one cannot pass below.
        Assert.Contains(FileVersionInfo.GetVersionInfo(specimen.Binary).FileVersion ?? "no FileVersion", specimen.Moved);

        var reported = new WindowsBinaryInspector().Inspect(specimen.Binary).Version;

        // The string Windows binaries carry is the dotted number and then a build label in brackets,
        // "10.0.26100.8737 (WinBuild.160101.0800)", so the number is what is compared, whole.
        Assert.Equal(ReadOutcome.Present, reported.Outcome);
        Assert.Equal(FixedVersion(specimen.Binary), reported.Value!.Split(' ')[0]);
    }

    /// <summary>
    /// A copy of a system binary in a temporary folder of its own, with copies of its language files
    /// in the language folders beside it, each with every digit of its FileVersion moved on by one -
    /// so the language file and the binary cannot agree on any machine. Deleted when disposed.
    /// </summary>
    private sealed class Disagreeing : IDisposable
    {
        private readonly string _folder;

        private Disagreeing(string folder, string binary, IReadOnlyList<string> moved)
        {
            _folder = folder;
            Binary = binary;
            Moved = moved;
        }

        public string Binary { get; }

        /// <summary>The FileVersion each language file carries after the move.</summary>
        public IReadOnlyList<string> Moved { get; }

        public static Disagreeing Copy(string name)
        {
            var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var folder = Directory.CreateTempSubdirectory("bws-own-version-").FullName;
            var moved = new List<string>();

            File.Copy(Path.Combine(system, name), Path.Combine(folder, name));

            foreach (var language in Directory.EnumerateDirectories(system).Where(at => File.Exists(Path.Combine(at, name + ".mui"))))
            {
                var beside = Directory.CreateDirectory(Path.Combine(folder, Path.GetFileName(language))).FullName;
                moved.Add(Move(Path.Combine(language, name + ".mui"), Path.Combine(beside, name + ".mui")));
            }

            return new Disagreeing(folder, Path.Combine(folder, name), moved);
        }

        public void Dispose() => Directory.Delete(_folder, recursive: true);

        /// <summary>
        /// Copies a language file with every digit of its FileVersion string moved on by one, wherever
        /// that string stands in the file, and returns the moved string. The length is kept, so
        /// nothing else in the resource shifts.
        /// </summary>
        private static string Move(string from, string to)
        {
            var said = FileVersionInfo.GetVersionInfo(from).FileVersion
                ?? throw new InvalidDataException($"{from} carries no FileVersion, so it cannot be the specimen.");
            var moved = string.Concat(said.Select(c => char.IsAsciiDigit(c) ? (char)('0' + ((c - '0' + 1) % 10)) : c));

            // A version with no digit would move to itself, and the loop below would never end.
            Assert.NotEqual(said, moved);

            var bytes = File.ReadAllBytes(from);
            var text = Encoding.Unicode.GetBytes(said);
            var found = 0;

            for (var at = bytes.AsSpan().IndexOf(text); at >= 0; at = bytes.AsSpan().IndexOf(text))
            {
                Encoding.Unicode.GetBytes(moved).CopyTo(bytes, at);
                found++;
            }

            Assert.True(found > 0, $"{from} says {said} through Windows and nowhere in its bytes as UTF-16.");
            File.WriteAllBytes(to, bytes);

            return moved;
        }
    }

    /// <summary>
    /// The fixed part of the first version resource in the file, from its bytes. Each step of the
    /// resource tree takes the first entry, which is what a file with one version resource has.
    /// </summary>
    private static string FixedVersion(string file)
    {
        using var stream = File.OpenRead(file);
        using var pe = new PEReader(stream);

        var table = pe.PEHeaders.PEHeader!.ResourceTableDirectory;
        var tree = pe.GetSectionData(table.RelativeVirtualAddress).GetContent(0, table.Size).AsSpan();

        var names = Child(tree, 0, VersionType);
        var languages = Child(tree, names, id: null);
        var leaf = Child(tree, languages, id: null);

        var rva = BinaryPrimitives.ReadInt32LittleEndian(tree[leaf..]);
        var size = BinaryPrimitives.ReadInt32LittleEndian(tree[(leaf + 4)..]);
        var info = pe.GetSectionData(rva).GetContent(0, size).AsSpan();

        Assert.Equal(FixedSignature, BinaryPrimitives.ReadUInt32LittleEndian(info[40..]));

        var high = BinaryPrimitives.ReadUInt32LittleEndian(info[48..]);
        var low = BinaryPrimitives.ReadUInt32LittleEndian(info[52..]);

        return $"{high >> 16}.{high & 0xFFFF}.{low >> 16}.{low & 0xFFFF}";
    }

    /// <summary>
    /// Where the entry of a resource directory leads - the one with this identifier, or the first one
    /// when none is asked for. The high bit of the target says "another directory" and is cleared.
    /// </summary>
    private static int Child(ReadOnlySpan<byte> tree, int directory, int? id)
    {
        var named = BinaryPrimitives.ReadUInt16LittleEndian(tree[(directory + 12)..]);
        var numbered = BinaryPrimitives.ReadUInt16LittleEndian(tree[(directory + 14)..]);

        for (var index = 0; index < named + numbered; index++)
        {
            var entry = directory + 16 + (index * 8);
            var name = BinaryPrimitives.ReadInt32LittleEndian(tree[entry..]);

            if (id is null || name == id)
            {
                return BinaryPrimitives.ReadInt32LittleEndian(tree[(entry + 4)..]) & 0x7FFFFFFF;
            }
        }

        throw new InvalidDataException("The file holds no version resource, so it cannot be the specimen.");
    }
}
