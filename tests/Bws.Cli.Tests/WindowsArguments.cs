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
/// <b>cmd makes a pass of its own before the program sees anything</b> - %name% everywhere, and
/// ^ &amp; | &lt; &gt; ( ) outside double quotes - and CommandLineToArgvW knows nothing of it. So the
/// line is checked against that pass first, since the review of #75: outside double quotes only what
/// the plan writes bare, and no percent sign anywhere. A line that fails it is refused here, rather
/// than cut as though cmd had left it alone.
/// </summary>
internal static class WindowsArguments
{
    internal static string[] Of(string line)
    {
        LeftAloneByCmd(line);

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

    private static void LeftAloneByCmd(string line)
    {
        var quoted = false;

        foreach (var character in line)
        {
            if (character == '"')
            {
                quoted = !quoted;
            }
            else if (character == '%' || (!quoted && character != ' ' && !PowerShellWords.Bare(character)))
            {
                throw new InvalidOperationException(
                    $"U+{(int)character:X4} is read by cmd before the program sees the line{(quoted ? "" : ", outside double quotes")}.");
            }
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr CommandLineToArgvW(string commandLine, out int count);
}
