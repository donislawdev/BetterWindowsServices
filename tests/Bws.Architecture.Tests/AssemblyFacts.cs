using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Bws.Architecture.Tests;

/// <summary>
/// Reads what an assembly refers to, straight from its metadata.
///
/// Two different questions need two different answers:
///   - assembly references say which other assemblies this one links against,
///   - type references say which types it actually names.
///
/// Only the second one catches a call into something that lives in the base class
/// library. System.Console is the case that matters here: every assembly links
/// against the runtime anyway, so an assembly-level check would never see it.
/// </summary>
internal sealed class AssemblyFacts
{
    private AssemblyFacts(
        string path,
        string name,
        IReadOnlySet<string> assemblyReferences,
        IReadOnlySet<string> typeReferences)
    {
        Path = path;
        Name = name;
        AssemblyReferences = assemblyReferences;
        TypeReferences = typeReferences;
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

        return new AssemblyFacts(path, ownName, assemblies, types);
    }

    internal static AssemblyFacts Of(string projectName) => Read(GuardedAssemblies.PathOf(projectName));
}
