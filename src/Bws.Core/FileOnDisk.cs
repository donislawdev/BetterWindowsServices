namespace Bws.Core;

/// <summary>
/// Whether a file is on disk, with the third answer <c>File.Exists</c> cannot give.
///
/// <para>
/// <b>Added 2026-10-05 for stability report R-1, and what it replaces answered false for a file it
/// was not allowed to look at.</b> <c>File.Exists</c> swallows every failure into "not there", so
/// under a token that may not read a file's attributes the listing said "file missing" about a file
/// that is there, and the signature, version and hash said "absent" instead of "refused". A snapshot
/// taken that way and one taken elevated then differed on the field, and the difference read as a
/// change to the machine rather than as "not compared" - rule 8 broken in an audit tool's most
/// expensive field.
/// </para>
/// <para>
/// <b>Measured that day with tools/restricted/file-denied.ps1</b>, on a file in a directory open
/// only to SYSTEM and Administrators, under a restricted token: <c>File.Exists</c> false,
/// <c>GetAttributes</c> code 5. <b>A name that is NOT there in the same directory answers 2, not
/// 5</b> - traverse checking is bypassed for everyone, so a refusal comes back only for something
/// that exists. That is what lets <see cref="BinaryPathResolver"/> stop at the first refused
/// candidate rather than guess past it.
/// </para>
/// <para>
/// <c>File.GetAttributes</c> rather than a call of our own, because the framework already does the
/// part that is easy to get wrong: when the attribute call is refused or meets a sharing violation it
/// asks the directory listing instead, which is how a file marked for deletion or a page file still
/// gets an answer. A wrapper around the bare attribute call would have called those refused.
/// </para>
/// </summary>
internal static class FileOnDisk
{
    /// <summary>
    /// Whether there is a file at this path, as the system answers it.
    ///
    /// A directory is "not there", which is what <c>File.Exists</c> says too: a launch command names
    /// a file, and a directory of the same name is not the thing it runs.
    /// </summary>
    internal static Reading<bool> Ask(string path)
    {
        try
        {
            return Reading<bool>.Present(!File.GetAttributes(path).HasFlag(FileAttributes.Directory));
        }
        catch (Exception failure) when (failure is ArgumentException or IOException
                                            or UnauthorizedAccessException or NotSupportedException)
        {
            // An argument failure is an empty name or one holding a null character - a name no file
            // can have, which is a fact about the name rather than a failure to read anything.
            return failure is ArgumentException
                ? Reading<bool>.Present(false)
                : FromFailure(failure.HResult, failure.Message);
        }
    }

    /// <summary>
    /// The answer a failed question gives.
    ///
    /// <b>Five codes mean the file is not there and every other one means nobody can say.</b> Not
    /// found (2), a directory on the way not found (3, also what a path through a FILE answers and
    /// what a drive letter with no drive behind it answers - both measured), no such drive (15), and
    /// a name no file can have (123, 161). Listing the codes that mean absent rather than the ones
    /// that mean refused is the direction that fails safe: a code nobody here has met becomes a
    /// refusal with its number, which says "not compared", and never a confident "missing".
    /// </summary>
    /// <param name="hResult">
    /// The failure's HResult. A Win32 failure carries its code in the low sixteen bits under facility
    /// 7, and only then is the code taken out - anything else is kept whole, because cutting it would
    /// produce a number that means something unrelated.
    /// </param>
    internal static Reading<bool> FromFailure(int hResult, string message)
    {
        if ((hResult & unchecked((int)0xFFFF0000)) != unchecked((int)0x80070000))
        {
            return Reading<bool>.Denied(hResult, message);
        }

        var code = hResult & 0xFFFF;

        return code is NotFound or PathNotFound or NoSuchDrive or BadName or BadPathName
            ? Reading<bool>.Present(false)
            : Reading<bool>.Denied(code, ManagerTerms.Describe(code));
    }

    private const int NotFound = 2;
    private const int PathNotFound = 3;
    private const int NoSuchDrive = 15;
    private const int BadName = 123;
    private const int BadPathName = 161;
}
