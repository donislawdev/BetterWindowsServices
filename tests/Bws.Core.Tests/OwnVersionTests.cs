using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace Bws.Core.Tests;

/// <summary>
/// The version is the one the file carries itself - package SB, the owner's decision of 2026-10-06.
///
/// <b>The judge reads the file's bytes and nothing else.</b> The version resource is found by walking
/// the PE resource directory with the standard library's PEReader - no version.dll, no language file,
/// nothing Windows could redirect - so it cannot agree with the product by sharing its mistake.
///
/// <b>Asked of a real system file, and the cost of that is said here rather than found later.</b> The
/// difference this guards only exists where Windows keeps a language file whose version differs from
/// the binary's own - on the machine this was written on, svchost.exe says 10.0.26100.8737 in its own
/// resource and 10.0.26100.8875 through the framework's localised read. On a machine where the two
/// agree this test passes either way and proves nothing, so the guard that holds the rule everywhere
/// is tools/security-probe/version-doors.ps1, which measures it against Explorer on every binary.
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
        var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "svchost.exe");

        var reported = new WindowsBinaryInspector().Inspect(file).Version;

        // The string Windows binaries carry is the dotted number and then a build label in brackets,
        // "10.0.26100.8737 (WinBuild.160101.0800)", so the number is what is compared, whole.
        Assert.Equal(ReadOutcome.Present, reported.Outcome);
        Assert.Equal(FixedVersion(file), reported.Value!.Split(' ')[0]);
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
