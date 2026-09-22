using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Bws.Architecture.Tests;

/// <summary>
/// Reads what an assembly refers to, straight from its metadata.
///
/// Three different questions need three different answers:
///   - assembly references say which other assemblies this one links against,
///   - type references say which types it actually names,
///   - native modules say which operating system libraries its P/Invokes bind.
///
/// Only the second one catches a call into something that lives in the base class
/// library. System.Console is the case that matters here: every assembly links
/// against the runtime anyway, so an assembly-level check would never see it.
///
/// <b>And only the third catches a way out of this machine that is not managed at all.</b>
/// Added 2026-09-22 for <see cref="OutboundGuards"/>: a call to WinHTTP through the Win32
/// generator produces a binding to winhttp.dll and a type reference to Windows.Win32.PInvoke,
/// so a check reading the first two sees an ordinary native call and says nothing. It lives
/// here rather than in a reader of its own for the reason <c>Sources.cs</c> gives about the
/// file list it holds - a second copy of "open the assembly and read its metadata" is a second
/// chance for one of them to quietly stop reading the right thing.
/// </summary>
internal sealed class AssemblyFacts
{
    private AssemblyFacts(
        string path,
        string name,
        IReadOnlySet<string> assemblyReferences,
        IReadOnlySet<string> typeReferences,
        IReadOnlySet<string> nativeModules)
    {
        Path = path;
        Name = name;
        AssemblyReferences = assemblyReferences;
        TypeReferences = typeReferences;
        NativeModules = nativeModules;
    }

    internal string Path { get; }

    /// <summary>
    /// The simple name this assembly carries in its own metadata - what another assembly's
    /// reference to it would be called. Since 2026-09-22 that is not the project name for the
    /// two that ship as executables, and a guard asserting "never references X" needs the X
    /// that could actually appear.
    /// </summary>
    internal string Name { get; }

    /// <summary>Simple names, without version or public key.</summary>
    internal IReadOnlySet<string> AssemblyReferences { get; }

    /// <summary>Fully qualified names, such as "System.Console".</summary>
    internal IReadOnlySet<string> TypeReferences { get; }

    /// <summary>
    /// The operating system libraries this assembly's P/Invokes bind, as the linker wrote
    /// them - "ADVAPI32.dll", "dwmapi.dll". Compared without case, because the case comes from
    /// whoever declared the import and differs between our generator and other people's code.
    ///
    /// <b>What this cannot see, named rather than left to be assumed covered:</b> a module
    /// loaded by name at run time, through NativeLibrary.Load or LoadLibrary. That is a string,
    /// and no reader of metadata will ever see it. <c>tools/outbound-probe/outbound.ps1</c> is
    /// what answers that question, by watching what the running process actually loaded.
    /// </summary>
    internal IReadOnlySet<string> NativeModules { get; }

    internal static AssemblyFacts Read(string path)
    {
        using var file = File.OpenRead(path);
        using var portableExecutable = new PEReader(file);
        var metadata = portableExecutable.GetMetadataReader();

        var ownName = metadata.GetString(metadata.GetAssemblyDefinition().Name);

        var assemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var handle in metadata.AssemblyReferences)
        {
            assemblies.Add(metadata.GetString(metadata.GetAssemblyReference(handle).Name));
        }

        var types = new HashSet<string>(StringComparer.Ordinal);
        foreach (var handle in metadata.TypeReferences)
        {
            var typeReference = metadata.GetTypeReference(handle);
            var declaringNamespace = metadata.GetString(typeReference.Namespace);
            var name = metadata.GetString(typeReference.Name);

            types.Add(string.IsNullOrEmpty(declaringNamespace) ? name : $"{declaringNamespace}.{name}");
        }

        // Walked through the method table rather than through the ModuleRef table, and the
        // reason is availability rather than preference: MetadataReader does not expose a row
        // count for that table to anything outside itself in this runtime. Measured 2026-09-22
        // to give the same answer on all four assemblies that were compared - what it reports
        // is every module a P/Invoke actually binds, which is the narrower and more honest of
        // the two questions anyway.
        var modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var handle in metadata.MethodDefinitions)
        {
            var import = metadata.GetMethodDefinition(handle).GetImport();

            if (!import.Module.IsNil)
            {
                modules.Add(metadata.GetString(metadata.GetModuleReference(import.Module).Name));
            }
        }

        return new AssemblyFacts(path, ownName, assemblies, types, modules);
    }

    /// <summary>
    /// The same read, for a file that might not be a managed assembly at all.
    ///
    /// <b>It exists because one guard walks a directory rather than naming a file.</b> A build
    /// output folder holds whatever the restore put there, and asking a native library for its
    /// managed metadata throws. Returning false is the answer for "this is not the kind of file
    /// I read" - it is never used to swallow a failure on a file that IS one, because such a
    /// file failing to open is a broken build and should say so.
    /// </summary>
    internal static bool TryRead(string path, out AssemblyFacts facts)
    {
        facts = null!;

        using var file = File.OpenRead(path);
        using var portableExecutable = new PEReader(file);

        if (!portableExecutable.HasMetadata)
        {
            return false;
        }

        var metadata = portableExecutable.GetMetadataReader();

        if (!metadata.IsAssembly)
        {
            return false;
        }

        facts = Read(path);
        return true;
    }

    internal static AssemblyFacts Of(string projectName) => Read(GuardedAssemblies.PathOf(projectName));
}
