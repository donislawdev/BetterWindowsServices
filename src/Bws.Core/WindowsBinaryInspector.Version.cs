using System.Globalization;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;

namespace Bws.Core;

/// <summary>
/// Which version a file claims for itself - the resource IN THE FILE, and nothing beside it.
///
/// <b>Its own part since 2026-10-06 - package SB, the owner's decision that day - because the old way
/// answered a different question.</b> Until then this was <c>System.Diagnostics.FileVersionInfo</c>,
/// which asks Windows with FILE_VER_GET_LOCALISED: "loads the entire version resource ... from the
/// corresponding MUI file, if available" (Microsoft's page on GetFileVersionInfoExW). On the machine
/// this was written on that put, on 127 of 530 service binaries and 382 of 801 entries, a version
/// that no file carries - svchost.exe reported 10.0.26100.8875 while the file, Explorer's Details tab
/// and the resource loaded as data all say 10.0.26100.8737, and through a final path the same call
/// reported the language file's 10.0.26100.1 instead. Measured with
/// tools/security-probe/version-doors.ps1 and one-handle.ps1 (P8). The field is defined as the
/// version the file gives for itself (docs/03), and a language file is not the file.
///
/// <b>It also keeps S-5 whole.</b> With no flags nothing but the file is read, and the file is the
/// one the inspection's handle holds - asked through that handle's final path, whose folders cannot
/// be renamed while it is open.
///
/// <b>The string, chosen the way the framework chose it</b>, so that the 403 files with no language
/// file answer exactly as before: the first translation the file lists, then US English in Unicode,
/// in Windows-1252 and with no code page. One difference is deliberate: the framework built the first
/// translation from two SIGNED halves, which turns a code page above 0x7FFF into another table's name,
/// and this reads them unsigned.
/// </summary>
public sealed partial class WindowsBinaryInspector
{
    /// <summary>
    /// No flags - the resource in the file itself, with no language file consulted. The enumeration
    /// has no member for "none", which is why this has a name of its own.
    /// </summary>
    private const GET_FILE_VERSION_INFO_FLAGS FromTheFileItself = default;

    /// <summary>The string tables tried after the file's own first translation, in the framework's order.</summary>
    private static readonly uint[] FallbackTables = [0x040904B0, 0x040904E4, 0x04090000];

    /// <summary>
    /// The FileVersion string of the file at this path. Absent when the file carries no version
    /// resource, or one without that string - plenty of drivers ship without, which is ordinary
    /// rather than broken. Any other failure is a refusal with Windows's number, where the framework
    /// used to say nothing at all.
    /// </summary>
    private static Reading<string> OwnVersion(string path)
    {
        var size = PInvoke.GetFileVersionInfoSizeEx(FromTheFileItself, path, out _);

        if (size == 0)
        {
            var error = Marshal.GetLastWin32Error();

            return CarriesNoVersion(error)
                ? Reading<string>.Absent()
                : Reading<string>.Denied(error, ManagerTerms.Describe(error));
        }

        var block = new byte[size];

        if (!PInvoke.GetFileVersionInfoEx(FromTheFileItself, path, block))
        {
            var error = Marshal.GetLastWin32Error();

            return Reading<string>.Denied(error, ManagerTerms.Describe(error));
        }

        var text = FileVersionText(block);

        return string.IsNullOrWhiteSpace(text) ? Reading<string>.Absent() : Reading<string>.Present(text);
    }

    /// <summary>
    /// The codes that mean "this file has no version to give": no resource of that type, name or
    /// language, and a file that is not an executable image at all.
    /// </summary>
    private static bool CarriesNoVersion(int error) => (WIN32_ERROR)error is
        WIN32_ERROR.ERROR_RESOURCE_DATA_NOT_FOUND
        or WIN32_ERROR.ERROR_RESOURCE_TYPE_NOT_FOUND
        or WIN32_ERROR.ERROR_RESOURCE_NAME_NOT_FOUND
        or WIN32_ERROR.ERROR_RESOURCE_LANG_NOT_FOUND
        or WIN32_ERROR.ERROR_BAD_EXE_FORMAT;

    private static unsafe string? FileVersionText(byte[] block)
    {
        fixed (byte* data = block)
        {
            return FileVersionText(data);
        }
    }

    private static unsafe string? FileVersionText(byte* data)
    {
        foreach (var table in FallbackTables.Prepend(FirstTranslation(data)))
        {
            if (StringValue(data, table) is { } text)
            {
                return text;
            }
        }

        return null;
    }

    /// <summary>
    /// The first language and code page the file lists, as one number - the language in the high
    /// half. Windows-1252 US English when the file lists none, which is what the framework assumed.
    /// </summary>
    private static unsafe uint FirstTranslation(byte* data)
    {
        if (!PInvoke.VerQueryValue(data, @"\VarFileInfo\Translation", out var value, out var length) || length < 4)
        {
            return FallbackTables[1];
        }

        var pair = (ushort*)value;

        return ((uint)pair[0] << 16) | pair[1];
    }

    /// <summary>The FileVersion string in one table, or null when that table has none or an empty one.</summary>
    private static unsafe string? StringValue(byte* data, uint table)
    {
        var name = @"\StringFileInfo\" + table.ToString("X8", CultureInfo.InvariantCulture) + @"\FileVersion";

        if (!PInvoke.VerQueryValue(data, name, out var value, out var length) || length == 0 || value == null)
        {
            return null;
        }

        var text = new string((char*)value);

        return text.Length == 0 ? null : text;
    }
}
