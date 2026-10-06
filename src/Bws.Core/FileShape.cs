namespace Bws.Core;

/// <summary>
/// Whether a path is shaped like a FILE ON A DISK, which is the only thing whose content this tool
/// reads - since 2026-10-06, package SB, the rest of security report S-4, the owner's decision that day.
///
/// <b>A different question from <see cref="NetworkPath.LeavesThisMachine"/>, and both are asked.</b>
/// That one decides whether a path may leave this machine, and <c>--follow-network</c> answers it.
/// This one decides whether the path names a file at all. Until that day <c>--follow-network</c> let
/// every shape through to the opening, so a launch path naming a named pipe or a device was opened by
/// an elevated process - a connection to whoever serves that pipe, or a device opened for nothing - and
/// only then failed to look like a file. A refusal decided by the SHAPE happens before anything is
/// opened, which is the whole point of deciding it here.
///
/// <b>What is a file here, as a closed list rather than a list of exceptions:</b> a path that does not
/// begin with two separators (a drive letter, or what the resolver made full), <c>\\?\X:</c> and
/// <c>\\.\X:</c>, <c>\\?\Volume{...}</c>, and a share - <c>\\server\share</c> or <c>\\?\UNC\</c> - unless
/// the share is <c>pipe</c> or <c>mailslot</c>, which Microsoft documents as the namespaces of named
/// pipes (<c>\\ServerName\pipe\PipeName</c>) and mailslots (<c>\\ComputerName\mailslot\name</c>), not
/// shares. Everything else in the device namespace - a pipe, a device, <c>GLOBALROOT</c>, the dotted
/// spelling <c>\\.\UNC\</c> of a share - is not read, with <c>--follow-network</c> or without it.
///
/// <b>The cost, said rather than discovered:</b> the two unusual spellings of a share - <c>\\.\UNC\</c>
/// and <c>\\?\GLOBALROOT\Device\Mup\</c> - stop being read under <c>--follow-network</c>. On the
/// machine this was written on, 0 of 787 launch paths begin with two separators at all (counted
/// 2026-10-05 with tools/scm-probe/scm-probe.ps1). A shape this cannot see - a drive letter that
/// some session mapped to something other than a disk - is caught after the opening by asking the
/// handle what it is (<see cref="WindowsBinaryInspector"/>), which is the second fence, not the first.
/// </summary>
public static class FileShape
{
    /// <summary>Whether the content behind this path may be read - see the type.</summary>
    public static bool NamesAFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Separators folded first, for the reason NetworkPath gives: Windows parses a forward slash
        // as a separator everywhere, so //./pipe/x is the pipe \\.\pipe\x.
        var value = path.TrimStart().Replace('/', '\\');

        if (!value.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return true;
        }

        if (value.Length > 3 && (value[2] is '?' or '.') && value[3] == '\\')
        {
            var name = value.AsSpan(4);

            return NetworkPath.NamesALocalVolume(name)
                || (value[2] == '?' && name.StartsWith(@"UNC\", StringComparison.OrdinalIgnoreCase) && IsAShare(name[4..]));
        }

        return IsAShare(value.AsSpan(2));
    }

    /// <summary>
    /// <c>server\share...</c> with both parts there and the share not one of the two namespaces that
    /// are reached through the same spelling without being shares.
    /// </summary>
    private static bool IsAShare(ReadOnlySpan<char> name)
    {
        var server = name.IndexOf('\\');

        if (server <= 0)
        {
            return false;
        }

        var rest = name[(server + 1)..];
        var end = rest.IndexOf('\\');
        var share = end < 0 ? rest : rest[..end];

        return !share.IsEmpty
            && !share.Equals("pipe", StringComparison.OrdinalIgnoreCase)
            && !share.Equals("mailslot", StringComparison.OrdinalIgnoreCase);
    }
}
