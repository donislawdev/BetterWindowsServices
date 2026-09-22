// Explicit for the same reason SourceTree.cs says so at the top of itself: these guards read
// files off disk, and the implicit using set is not something to depend on across projects.
using System.IO;

namespace Bws.Architecture.Tests;

/// <summary>
/// The three ways out of this machine that <see cref="LayeringGuards"/> cannot see.
///
/// <b>What was already held, so that this class is read as the addition it is.</b>
/// <c>LayeringGuards.No_shipped_assembly_reaches_for_the_network</c> refuses any reference to
/// <c>System.Net.*</c> in the three assemblies that ship, read out of their compiled metadata
/// rather than out of their source. That covers the whole managed surface - an
/// <c>HttpClient</c>, a <c>TcpClient</c>, a name lookup - and it covers it in the one place
/// that cannot be argued with by formatting or by a comment.
///
/// <b>Three ways past it, and all three were measured on 2026-09-22 rather than imagined.</b>
///
///   1. <b>A P/Invoke.</b> Adding <c>WinHttpOpen</c> to NativeMethods.txt and calling it emits a
///      binding to <c>winhttp.dll</c> and a type reference to <c>Windows.Win32.PInvoke</c>.
///      Not one character of <c>System.Net</c> appears, and that guard stays green. So does a
///      hand-written <c>[DllImport("ws2_32.dll")]</c> that never touches the inventory file at
///      all.
///   2. <b>Somebody else's assembly.</b> Four managed libraries ship beside the window and the
///      terminal, and the guard above reads none of them. A package is precisely how a network
///      client arrives without us naming one - the guard says so about itself, in its own
///      comment, and calls itself PARTIAL for exactly this reason.
///   3. <b>A fourth project.</b> The list of assemblies those guards read is written out by
///      hand in <see cref="GuardedAssemblies.Shipped"/>. It agrees with <c>src</c> today. A
///      project added to <c>src</c> tomorrow is read by nothing and reddens nothing.
///
/// <b>WHAT THIS IS NOT, AND THE PROJECT'S OWN HISTORY IS THE ARGUMENT.</b> ADR-19 has been
/// broken twice here and NEITHER time by anything resembling a network client: once by
/// <c>//server/share</c> being parsed as a local path because Windows takes a forward slash as
/// a separator, and once by a UNC path walking into <c>File.Exists</c> - which blocks for
/// 21 053 ms on an unreachable host against 1.23 ms locally, and which authenticates to
/// somebody else's share with the elevated token of whoever ran the tool. Both arrived through
/// <c>System.IO</c>. A register of network names would have caught zero of the two, and the
/// thing that holds that surface is <see cref="Bws.Core.NetworkPath"/> and its mutation
/// entries, not this file. This one locks the surface nobody has breached yet, which is worth
/// doing and is a smaller claim than "the tool cannot reach the network".
///
/// <b>And it says nothing about what happens at run time.</b> A module loaded by name through
/// <c>NativeLibrary.Load</c> or <c>LoadLibrary</c> is a string, and a string is invisible to
/// every check here. So is a certificate chain reaching for an AIA URL inside WinVerifyTrust,
/// which is the one call in this product that could go outbound without our code saying so -
/// revocation checking is off deliberately (<c>WindowsBinaryInspector.Request</c>, with ADR-19
/// named in its comment), and chain building is not the same switch. The instrument for all of
/// that is <c>tools/outbound-probe/outbound.ps1</c>, which runs the product and watches the
/// modules it actually loads and the sockets it actually holds. This class is what runs on
/// every push.
///
/// <b>TWO REGISTERS, DELIBERATELY DIFFERENT SHAPES.</b> Our own assemblies get an ALLOW list:
/// every native module they bind is named here with its reason, and a new one reddens the
/// build until somebody writes down why. We control that list, so it does not churn, and
/// "nobody adds a way out by accident" is the whole point. Somebody else's assemblies get a
/// DENY list instead: we do not choose what WPF-UI binds, so an allow list there would ask a
/// question on every dependency bump and the question would not be ours to answer. The cost of
/// that choice is stated rather than hidden - a deny list can only refuse the names it knows,
/// so a networking module under a name not listed there would pass. The managed half of the
/// same check has no such hole, because "no type whose namespace begins System.Net" needs no
/// register at all.
///
/// <b>Both registers, and the two pure functions the canary points at, are in
/// <see cref="OutboundRegisters"/>.</b> They were in this file until it passed the length
/// ceiling on the day it was written, and the split went along the seam this project already
/// uses everywhere else - what the guards read lives apart from what the guards assert.
/// </summary>
public sealed class OutboundGuards
{
    /// <summary>
    /// Every native module the three shipped assemblies bind is one somebody wrote down.
    /// </summary>
    [Theory]
    [InlineData("Bws.Core")]
    [InlineData("Bws.Cli")]
    [InlineData("Bws.Gui")]
    public void Every_native_module_a_shipped_assembly_binds_is_registered(string projectName)
    {
        var assembly = AssemblyFacts.Of(projectName);

        var strangers = assembly.NativeModules
            .Where(module => !OutboundRegisters.RegisteredNativeModules.ContainsKey(module))
            .OrderBy(module => module, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            strangers.Length == 0,
            $"{projectName} binds a native module nobody registered: " +
            $"[{string.Join(", ", strangers)}]. A P/Invoke is the way out of this machine that " +
            "the System.Net guard cannot see - winhttp.dll and ws2_32.dll carry no managed type " +
            "reference at all. If the module is legitimate, it takes a line in " +
            "OutboundRegisters.RegisteredNativeModules saying which call needs it and why (ADR-19).");
    }

    /// <summary>
    /// None of those modules is one of the refused ones, which is the same claim from the other
    /// side.
    ///
    /// <b>Not redundant with the theory above, and the difference is the failure mode.</b> That
    /// one fails when the register is out of date, whichever direction the change went. This one
    /// fails when the register itself has been edited to admit a way out - which is the edit a
    /// reader of a red build is most tempted to make, because it turns the build green.
    /// </summary>
    [Theory]
    [InlineData("Bws.Core")]
    [InlineData("Bws.Cli")]
    [InlineData("Bws.Gui")]
    public void No_shipped_assembly_binds_a_networking_module(string projectName)
    {
        var assembly = AssemblyFacts.Of(projectName);

        var found = assembly.NativeModules
            .Where(OutboundRegisters.NetworkingModules.ContainsKey)
            .Select(module => $"{module} ({OutboundRegisters.NetworkingModules[module]})")
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            found.Length == 0,
            $"{projectName} binds a module whose purpose is to speak to a network: " +
            $"[{string.Join(", ", found)}]. Zero outbound connections is a decision, not an " +
            "aspiration (ADR-19), and registering the module in this file does not make it one.");
    }

    /// <summary>
    /// The reader is looking at something. A scan that read no modules would satisfy both
    /// theories above by finding nothing, and would look exactly like a clean product.
    /// </summary>
    [Fact]
    public void The_module_reader_is_reading_something()
    {
        var core = AssemblyFacts.Of("Bws.Core");

        // NAMES RATHER THAN A COUNT, and the review of the pull request that brought this file
        // is why. A count of three passes when the reader finds three modules nobody expected,
        // which is exactly the state this canary exists to refuse: it would read as a working
        // scan over a product whose real imports were never seen.
        string[] mustBeThere = ["ADVAPI32.dll", "KERNEL32.dll", "PSAPI.dll", "WINTRUST.dll"];

        var missing = mustBeThere.Where(module => !core.NativeModules.Contains(module)).ToArray();

        Assert.True(
            missing.Length == 0,
            $"the module reader did not find [{string.Join(", ", missing)}] in Bws.Core, which " +
            "talks to the service control manager, to processes and to WinVerifyTrust. It read " +
            $"[{string.Join(", ", core.NativeModules.OrderBy(m => m, StringComparer.OrdinalIgnoreCase))}]. " +
            "Either it is reading the wrong thing - and every other assertion in this file is " +
            "then passing for that reason rather than because the product is clean - or the " +
            "product genuinely stopped making one of those calls, which is worth the same look.");
    }

    /// <summary>
    /// Nothing shipping beside us names the managed network, and this one needs no register.
    /// </summary>
    [Fact]
    public void Nothing_that_ships_beside_us_names_the_managed_network()
    {
        var offenders = new List<string>();

        foreach (var assembly in OutboundRegisters.ThirdPartyAssemblies())
        {
            var types = assembly.TypeReferences
                .Where(type => type.StartsWith("System.Net.", StringComparison.Ordinal));

            var assemblies = assembly.AssemblyReferences
                .Where(name => name.StartsWith("System.Net.", StringComparison.OrdinalIgnoreCase));

            foreach (var name in types.Concat(assemblies).OrderBy(n => n, StringComparer.Ordinal))
            {
                offenders.Add($"{assembly.Name} names {name}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "a library that ships beside this product names the network: " +
            $"[{string.Join(", ", offenders)}]. Measured clean on 2026-09-22 across all four - " +
            "Wpf.Ui, Wpf.Ui.Abstractions, WinRT.Runtime and Microsoft.Windows.SDK.NET. A " +
            "dependency is how a network client arrives without anybody here naming one, so " +
            "this is a conversation with the owner rather than an entry in a register (ADR-19).");
    }

    /// <summary>
    /// And none of them binds a networking module either, which is the native half of it.
    /// </summary>
    [Fact]
    public void Nothing_that_ships_beside_us_binds_a_networking_module()
    {
        var offenders = new List<string>();

        foreach (var assembly in OutboundRegisters.ThirdPartyAssemblies())
        {
            foreach (var module in assembly.NativeModules.Where(OutboundRegisters.NetworkingModules.ContainsKey))
            {
                offenders.Add($"{assembly.Name} binds {module} ({OutboundRegisters.NetworkingModules[module]})");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "a library that ships beside this product binds a networking module: " +
            $"[{string.Join(", ", offenders)}] (ADR-19).");
    }

    /// <summary>
    /// The same canary as above, for the half of the scan that walks a directory.
    /// </summary>
    [Fact]
    public void The_scan_of_what_ships_beside_us_found_the_libraries_it_is_meant_to_read()
    {
        var names = OutboundRegisters.ThirdPartyAssemblies()
            .Select(assembly => assembly.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        // The four measured on 2026-09-22, named rather than counted. A count of four is
        // satisfied by four libraries nobody expected - and the way this scan would actually
        // break is by reading ONE output folder instead of both, which loses Wpf.Ui while
        // still finding four things beside the terminal.
        string[] mustBeThere =
        [
            "Wpf.Ui", "Wpf.Ui.Abstractions", "WinRT.Runtime", "Microsoft.Windows.SDK.NET"
        ];

        var missing = mustBeThere
            .Where(name => !names.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"the scan did not read [{string.Join(", ", missing)}]. It read " +
            $"[{string.Join(", ", names)}]. Either it is looking in the wrong folder - and the " +
            "two assertions above are then passing over a set that does not contain what ships " +
            "- or a dependency left the tree, which wants a look of its own rather than a " +
            "quietly smaller scan.");
    }

    /// <summary>
    /// The Win32 inventory names nothing that reaches off this machine.
    /// </summary>
    [Fact]
    public void The_win32_inventory_names_nothing_that_reaches_the_network()
    {
        var offenders = new List<string>();

        foreach (var file in OutboundRegisters.InventoryFiles())
        {
            var lines = File.ReadAllLines(file);

            for (var index = 0; index < lines.Length; index++)
            {
                // Comments carry the argument for every name on this list, and one of them
                // would have to be able to discuss WinHTTP in order to say the product does
                // not use it. The declarations are what this reads.
                var line = OutboundRegisters.Declaration(lines[index]);

                if (line.Length == 0)
                {
                    continue;
                }

                if (OutboundRegisters.ReachesTheNetwork(line))
                {
                    offenders.Add(
                        $"{Path.GetFileName(Path.GetDirectoryName(file))}/NativeMethods.txt:{index + 1} declares {line}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"the Win32 inventory declares something that reaches the network: [{string.Join(", ", offenders)}]. " +
            "That file calls itself the honest inventory of what this tool touches in the " +
            "operating system, and zero outbound connections is ADR-19.");
    }

    /// <summary>
    /// The list of shipped projects still matches what is under <c>src</c>.
    ///
    /// <b>This is the guard for the guards, and it is the cheapest one in the file.</b> Every
    /// assembly-reading check in this project - layering, licences, the two theories above -
    /// walks a list written out by hand. A fourth project under <c>src</c> would be read by
    /// none of them and would redden nothing, which is the failure this project already names
    /// as the worst kind: a thing that never ran looks exactly like a thing that found nothing.
    /// </summary>
    [Fact]
    public void The_list_of_shipped_projects_still_matches_what_is_under_src()
    {
        var onDisk = Directory
            .EnumerateDirectories(Path.Combine(SourceTree.Root(), "src"))
            .Select(path => Path.GetFileName(path)!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var registered = GuardedAssemblies.Shipped
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            onDisk.SequenceEqual(registered, StringComparer.Ordinal),
            $"src holds [{string.Join(", ", onDisk)}] and GuardedAssemblies.Shipped says " +
            $"[{string.Join(", ", registered)}]. Every guard that reads a built assembly walks " +
            "that list, so a project missing from it ships without one of them ever looking at " +
            "it - and every one of those guards stays green while that is true.");
    }

    /// <summary>
    /// The registers reject what they exist to reject, and leave alone what they do not.
    ///
    /// <b>A guard nobody has watched fail is indistinguishable from a guard that reads
    /// nothing</b>, and the product has been clean on every one of these since the day it was
    /// written - so nothing above has ever been seen to say no.
    /// </summary>
    [Fact]
    public void The_registers_catch_every_shape_they_exist_to_catch()
    {
        string[] mustBeRefusedAsModules =
        [
            "ws2_32.dll", "WS2_32.DLL", "winhttp.dll", "wininet.dll",
            "urlmon.dll", "dnsapi.dll", "mpr.dll", "netapi32.dll"
        ];

        var missedModules = mustBeRefusedAsModules
            .Where(module => !OutboundRegisters.NetworkingModules.ContainsKey(module))
            .ToArray();

        Assert.True(
            missedModules.Length == 0,
            $"the networking module register does not know: [{string.Join(", ", missedModules)}]. " +
            "The casing cases are in that list on purpose - a module name arrives from the " +
            "linker in whatever case the SDK wrote it, and ADVAPI32 is upper while kernel32 in " +
            "somebody else's assembly is lower.");

        string[] mustBeLeftAlone = ["ADVAPI32.dll", "KERNEL32.dll", "dwmapi.dll", "UXTHEME.dll"];

        var wronglyRefused = mustBeLeftAlone.Where(OutboundRegisters.NetworkingModules.ContainsKey).ToArray();

        Assert.True(
            wronglyRefused.Length == 0,
            $"the networking module register refuses something ordinary: [{string.Join(", ", wronglyRefused)}]. " +
            "A register that refused everything would satisfy the half above and be useless.");

        string[] inventoryLinesThatMustFail =
        [
            "WinHttpOpen", "InternetOpenUrl", "URLDownloadToFileW", "WSAStartup",
            "DnsQuery_W", "WNetAddConnection2W"
        ];

        var missedNames = inventoryLinesThatMustFail.Where(line => !OutboundRegisters.ReachesTheNetwork(line)).ToArray();

        Assert.True(
            missedNames.Length == 0,
            $"the inventory register would not refuse: [{string.Join(", ", missedNames)}].");

        // Every one of these is on the real list today, and the third is the one that matters.
        // SC_MANAGER_CONNECT reddened the first run of this guard, because it contains the word
        // "connect" and "connect" is an ordinary word. It is here so that a future widening of
        // the register back into substring matching fails on a clean tree rather than sending
        // somebody to read the service control manager's own constants.
        string[] inventoryLinesThatMustPass =
        [
            "OpenSCManager",
            "QueryServiceConfig2",
            "SC_MANAGER_CONNECT",
            "WinVerifyTrust",
            "GetProcessMemoryInfo",
            "DwmSetWindowAttribute",
            "CryptCATAdminEnumCatalogFromHash",
            "SERVICE_ENUMERATE_DEPENDENTS"
        ];

        var wronglyRejected = inventoryLinesThatMustPass.Where(OutboundRegisters.ReachesTheNetwork).ToArray();

        Assert.True(
            wronglyRejected.Length == 0,
            $"the inventory register refuses a name the product genuinely uses: " +
            $"[{string.Join(", ", wronglyRejected)}]. Every one of those is on the real list in " +
            "src/Bws.Core/NativeMethods.txt, so this would be a red build on a clean tree.");

        // The comment half, which is the other way this scan could be wrong. Those files argue
        // at length for every name they carry, and one of them has to be able to say the words
        // in the register in order to explain that the product does not use them.
        string[] commentaryThatMustNotCount =
        [
            "// WinHttpOpen is deliberately not here - ADR-19 forbids it outright.",
            "   // socket, and why this product never opens one",
            "WinVerifyTrust // not WinHttp, whatever the first four letters suggest"
        ];

        var countedProse = commentaryThatMustNotCount
            .Where(line => OutboundRegisters.ReachesTheNetwork(OutboundRegisters.Declaration(line)))
            .ToArray();

        Assert.True(
            countedProse.Length == 0,
            $"the inventory scan counted a comment as a declaration: [{string.Join(", ", countedProse)}]. " +
            "Those files exist to argue about what the product touches, so a scan that cannot " +
            "tell an argument from a declaration makes the argument unwritable.");
    }
}
