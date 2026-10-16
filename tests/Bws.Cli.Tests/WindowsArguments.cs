using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Bws.Cli.Tests;

/// <summary>
/// Words the way Windows itself cuts a command line - CommandLineToArgvW, the parser behind the
/// argument array of every program that does not parse its own line.
///
/// <b>An oracle rather than a model, and that is the only reason it is here.</b> PowerShellWords
/// beside it is a model of a measurement, written by the same hand that wrote the quoting it
/// checks. This one is the operating system answering, so a line that it cuts into the words the
/// plan meant is a line that cmd hands a program as those words.
///
/// <b>What it does not do, said rather than assumed:</b> cmd makes a pass of its own before the
/// program sees anything - %name% everywhere, and ^ &amp; | &lt; &gt; outside double quotes. The plan
/// never writes one of those bare: a name holding %, or anything PowerShell reads inside double
/// quotes, goes in single quotes and is a PowerShell line only, and every other special character
/// sits inside double quotes, where cmd leaves it alone.
/// </summary>
internal static class WindowsArguments
{
    internal static string[] Of(string line)
    {
        var argv = CommandLineToArgvW(line, out var count);

        if (argv == IntPtr.Zero)
        {
            throw new Win32Exception();
        }

        try
        {
            var words = new string[count];

            for (var index = 0; index < count; index++)
            {
                words[index] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, index * IntPtr.Size)) ?? string.Empty;
            }

            return words;
        }
        finally
        {
            // The array comes from LocalAlloc, and FreeHGlobal is LocalFree on Windows.
            Marshal.FreeHGlobal(argv);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr CommandLineToArgvW(string commandLine, out int count);
}
