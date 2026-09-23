// Explicit for the same reason SourceTree.cs says so at the top of itself: these guards read
// files off disk, and the implicit using set is not something to depend on across projects.
namespace Bws.Architecture.Tests;

/// <summary>
/// What <see cref="OutboundGuards"/> refuses, and why each entry is on its list.
///
/// <b>Kept apart from the assertions for the reason <c>Sources.cs</c> gives about the file
/// list it holds.</b> A register is read by a person deciding whether to add a line to it,
/// and that person is not reading the assertions - so the argument for every entry belongs
/// where the entry is. The whole case for these three registers existing at all, including
/// what they cannot do, is at the top of <see cref="OutboundGuards"/>.
/// </summary>
internal static class OutboundRegisters
{
    /// <summary>
    /// Every native module our own code may bind, and why. Measured 2026-09-22 by reading the
    /// P/Invoke imports out of the built assemblies: four modules in the core, one in the
    /// window, and none at all in the terminal. The window went to six on 2026-09-23 with the
    /// Donate button - one new module it calls, for the desktop's shell, and four the generator
    /// declares beside types those interfaces name and nothing calls. Each entry says which it is.
    ///
    /// <b>Adding a line here is the deliberate act.</b> Failing this test is the question being
    /// asked, and the answer belongs in this list rather than in a comment beside the call.
    /// </summary>
    internal static readonly Dictionary<string, string> RegisteredNativeModules =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ADVAPI32.dll"] =
                "The service control manager itself - opening it, enumerating it, reading and " +
                "changing configuration, starting and stopping. Every name in the first half of " +
                "src/Bws.Core/NativeMethods.txt lives here.",

            ["KERNEL32.dll"] =
                "Process handles and what can be asked of them without opening a process for " +
                "reading: OpenProcess, GetProcessTimes, TerminateProcess, and the handle types " +
                "underneath all of the above. The window binds it too since 2026-09-23, for " +
                "CloseHandle alone, which the generator declares as the release function of a " +
                "handle type the shell interfaces name - declared and never called.",

            ["PSAPI.dll"] =
                "GetProcessMemoryInfo, which is the one question in this product asked of a " +
                "process rather than of the manager - how much memory the thing behind an entry " +
                "is using.",

            ["WINTRUST.dll"] =
                "Signature verification: WinVerifyTrust and the catalogue calls beside it. This " +
                "is the module closest to being a way out, because a certificate chain can " +
                "fetch. Revocation checking is off by WTD_REVOKE_NONE and the comment there " +
                "names ADR-19 as the reason - what that switch does NOT cover is measured by " +
                "the runtime probe rather than argued about here.",

            ["dwmapi.dll"] =
                "DwmSetWindowAttribute: telling the window manager that the title bar above this " +
                "window is a dark one. It was the whole of what the window touched in the " +
                "operating system until 2026-09-23.",

            ["OLE32.dll"] =
                "CoCreateInstance, for one object: the desktop's ShellWindows, a local server run " +
                "as the interactive user. The Donate button asks it to open one constant address " +
                "so that the browser starts without this window's administrator rights - " +
                "ExternalLinks.cs. COM between two processes on this machine, not a network.",

            ["OLEAUT32.dll"] =
                "SysFreeString, declared by the generator for the BSTR the desktop's ShellExecute " +
                "takes. The string itself is made and freed through Marshal, so this one is " +
                "declared and never called.",

            ["USER32.dll"] =
                "DestroyMenu, declared by the generator as the release function of HMENU, which " +
                "IShellBrowser names in its signatures. Declared and never called.",

            ["COMCTL32.dll"] =
                "DestroyPropertySheetPage, declared by the generator as the release function of " +
                "HPROPSHEETPAGE, which IShellView names in its signatures. Declared and never " +
                "called."
        };

    /// <summary>
    /// Modules that exist to speak to a network, refused in anything that ships beside us.
    ///
    /// <b>A deny list, and the header says why it is one here and an allow list above.</b> It
    /// can only refuse what it knows, so each entry is a name a networking library would
    /// actually bind rather than a guess at a family.
    /// </summary>
    internal static readonly Dictionary<string, string> NetworkingModules =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ws2_32.dll"] = "Winsock, which is what a socket is on Windows",
            ["wsock32.dll"] = "the older Winsock",
            ["mswsock.dll"] = "the Winsock service provider",
            ["winhttp.dll"] = "WinHTTP, the Windows HTTP stack",
            ["wininet.dll"] = "WinINet, the other Windows HTTP stack",
            ["urlmon.dll"] = "URLDownloadToFile lives here - one call, one download",
            ["httpapi.dll"] = "the HTTP Server API",
            ["dnsapi.dll"] = "a name lookup, which is a network round trip",
            ["iphlpapi.dll"] = "the IP helper API. It reads tables rather than opening " +
                               "connections, and it is refused anyway because nothing in this " +
                               "product has any use for it",
            ["netapi32.dll"] = "network management, including enumerating somebody else's shares",
            ["mpr.dll"] = "the multiple provider router - WNetAddConnection, which is how a " +
                          "share gets mounted. The nearest module to the way ADR-19 was " +
                          "actually broken here",
            ["rasapi32.dll"] = "dial-up and VPN",
            ["winsta.dll"] = "the session manager, which reaches other sessions and other hosts"
        };

    /// <summary>
    /// Names refused in the Win32 inventory files, checked at the DECLARATION rather than at
    /// the module.
    ///
    /// <b>This is the weakest of the checks in this class and it is kept for one reason: the
    /// error message.</b> Everything it catches, the module register above catches too - and
    /// the module register additionally catches a hand-written DllImport that never appears in
    /// an inventory file. What this one adds is where it fires. NativeMethods.txt calls itself
    /// "the honest inventory of what the tool touches in the operating system", and a guard
    /// that fails on the line somebody typed says more than one that fails on a built file.
    ///
    /// <b>MATCHED ON THE START OF THE DECLARATION, NEVER ON A FRAGMENT OF IT, AND THE FIRST RUN
    /// OF THIS GUARD IS WHY.</b> Written with a substring match it refused
    /// <c>SC_MANAGER_CONNECT</c> - which is connecting to the service control manager, and is
    /// on the real list in src/Bws.Core/NativeMethods.txt. <c>connect</c> and <c>socket</c> are
    /// ordinary words, and this repository already carries the same lesson from the other
    /// direction, where a mechanical replacement of <c>Status</c> landed in a cell template.
    /// The file declares one API name per line, so the beginning of that name is what
    /// identifies the family, and <c>SC_MANAGER_CONNECT</c> begins with neither of them. That
    /// exact case is in the canary below so that it cannot come back.
    /// </summary>
    internal static readonly string[] NetworkingNamesInTheInventory =
    [
        "WinHttp",
        "InternetOpen",
        "InternetConnect",
        "URLDownloadToFile",
        "WSAStartup",
        "WSASocket",
        "socket",
        "connect",
        "DnsQuery",
        "GetAddrInfo",
        "gethostby",
        "WNetAddConnection",
        "NetShareEnum",
        "HttpSendRequest",
        "HttpOpenRequest"
    ];

    /// <summary>
    /// The managed libraries that land beside the product when it is built.
    ///
    /// <b>Both output folders, unioned by assembly name.</b> The window and the terminal do not
    /// carry the same set - Wpf.Ui reaches only the first - and reading one folder would leave
    /// the other unguarded while looking complete.
    /// </summary>
    internal static IEnumerable<AssemblyFacts> ThirdPartyAssemblies()
    {
        var ours = new HashSet<string>(
            GuardedAssemblies.Shipped.Select(GuardedAssemblies.AssemblyNameOf),
            StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in GuardedAssemblies.Shipped)
        {
            var folder = Path.GetDirectoryName(GuardedAssemblies.PathOf(project));

            if (folder is null)
            {
                continue;
            }

            // Top level only. A runtimes/ folder below holds native halves that carry no
            // managed metadata at all, and walking into it would trade a clear answer for a
            // pile of files this reader cannot open.
            foreach (var file in Directory.EnumerateFiles(folder, "*.dll", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileNameWithoutExtension(file);

                if (ours.Contains(name) || !seen.Add(name))
                {
                    continue;
                }

                if (AssemblyFacts.TryRead(file, out var facts))
                {
                    yield return facts;
                }
            }
        }
    }

    /// <summary>
    /// Whether one declaration from a Win32 inventory file reaches off this machine.
    ///
    /// <b>A pure function over one line, so the canary can point it at declarations that are
    /// not in this repository at all.</b> A canary that could only run the scan over the real
    /// files would prove the scan runs, never that it can say no.
    /// </summary>
    internal static bool ReachesTheNetwork(string declaration) =>
        Array.Exists(
            NetworkingNamesInTheInventory,
            name => declaration.StartsWith(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// One line of an inventory file reduced to the name it declares, or empty if it declares
    /// none. Comments carry the argument for every name on those lists, and one of them would
    /// have to be able to discuss WinHTTP in order to say the product does not use it.
    /// </summary>
    internal static string Declaration(string line)
    {
        var comment = line.IndexOf("//", StringComparison.Ordinal);

        return (comment >= 0 ? line[..comment] : line).Trim();
    }

    /// <summary>The Win32 inventory files, one per project that declares any native call.</summary>
    internal static IEnumerable<string> InventoryFiles() =>
        Directory.EnumerateFiles(
            Path.Combine(SourceTree.Root(), "src"),
            "NativeMethods.txt",
            SearchOption.AllDirectories);
}
