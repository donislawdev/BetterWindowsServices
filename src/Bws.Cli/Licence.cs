using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Bws.Cli;

/// <summary>
/// What this program is licensed under, and what it carries that somebody else wrote.
///
/// <b>Answered out of a register compiled INTO the executable, and that is the whole point.</b>
/// An administrator on a machine with no internet, holding one 98 MB file they are about to run
/// with administrator rights, can ask what is inside it and be answered by the file itself. A
/// link to a web page is not an answer on that machine.
///
/// <b>One register, three renderings.</b> <c>packaging/components.json</c> is the source. The
/// SPDX document published beside every archive is rendered from it by
/// <c>packaging/sbom.ps1</c>, <c>THIRD-PARTY-NOTICES.md</c> carries the same set with the full
/// licence texts, and this is the third. They cannot drift apart while they are one file:
/// <c>ComponentRegisterGuards</c> holds the notices against the register, and
/// <c>packaging/build-dist.ps1</c> holds the register against the manifest the publish produced,
/// in both directions.
///
/// <b>Only what THIS program carries.</b> The register describes both packages and the window
/// ships three components this file does not - naming them here would be a list that reads like
/// an inventory of this binary and is not one.
/// </summary>
internal static class Licence
{
    /// <summary>
    /// The register, linked into this project from packaging/ rather than copied into it. A copy
    /// would be a second file to update and a first one to forget.
    /// </summary>
    private const string ResourceName = "Bws.Cli.Resources.components.json";

    /// <summary>
    /// Which package of the register this executable is.
    ///
    /// Written down rather than worked out, because there is nothing at runtime to work it out
    /// from - and a wrong answer here is the one failure this whole file exists to avoid: a
    /// component list that belongs to the other program. <c>packaging/build-dist.ps1</c> runs the
    /// packaged binary and checks that it names every component the register puts in this
    /// package, which is the only place that can prove this constant right.
    /// </summary>
    private const string ThisPackage = "cli";

    /// <summary>
    /// A component as this program can state it: everything the register knows, with the version
    /// already resolved to something true on this machine.
    /// </summary>
    private sealed record Component(
        string Name, string Version, string Licence, string Supplier, string Source, string What);

    internal static string Answer(bool components)
    {
        using var document = JsonDocument.Parse(Register());
        var root = document.RootElement;
        var mine = Components(root);

        return components ? Full(root, mine) : Summary(root, mine);
    }

    /// <summary>The notice: what this is under, where the texts are, and what it carries.</summary>
    private static string Summary(JsonElement root, IReadOnlyList<Component> mine)
    {
        var text = new StringBuilder();

        text.AppendLine(Texts.Of(
            "cli.licence.notice",
            Product(root, "name"),
            Release.Number,
            Product(root, "license"),
            Product(root, "copyright")));

        // Named in prose rather than listed, because this half of the answer is for somebody
        // asking "may I put this on a server", and the names are what they need to see. The
        // version of each is the other half, under --components.
        text.AppendLine();
        text.AppendLine(Texts.Of("cli.licence.carries", Executable(root)));
        text.AppendLine("  " + string.Join(", ", mine.Select(component => component.What)));
        text.AppendLine();
        text.AppendLine(Texts.Of("cli.licence.notices"));
        text.Append(Texts.Of("cli.licence.more"));

        return text.ToString();
    }

    /// <summary>Every component, with its version, its licence and where it came from.</summary>
    private static string Full(JsonElement root, IReadOnlyList<Component> mine)
    {
        var text = new StringBuilder();

        text.AppendLine(Texts.Of("cli.licence.components", Executable(root), Release.Number));

        foreach (var component in mine)
        {
            text.AppendLine();
            text.AppendLine("  " + component.Name);
            text.AppendLine("      " + Texts.Of("cli.licence.what", component.What));
            text.AppendLine("      " + Texts.Of("cli.licence.at", component.Version));
            text.AppendLine("      " + Texts.Of("cli.licence.under", component.Licence));
            text.AppendLine("      " + Texts.Of("cli.licence.from", component.Supplier, component.Source));
        }

        text.AppendLine();
        text.Append(Texts.Of("cli.licence.notices"));

        return text.ToString();
    }

    private static IReadOnlyList<Component> Components(JsonElement root)
    {
        var mine = new List<Component>();

        foreach (var entry in root.GetProperty("components").EnumerateArray())
        {
            if (!entry.GetProperty("in").EnumerateArray().Any(
                    package => string.Equals(package.GetString(), ThisPackage, StringComparison.Ordinal)))
            {
                continue;
            }

            mine.Add(new Component(
                Text(entry, "name"),
                Version(entry),
                Text(entry, "license_concluded"),
                Text(entry, "supplier"),
                Text(entry, "source"),
                Text(entry, "notice")));
        }

        // Rule 8 of CLAUDE.md on a very small thing. An empty list here would print a heading
        // and nothing under it, which reads as "it carries nothing" - and this program carries
        // the whole .NET runtime. Every way that could happen is a broken build rather than a
        // true answer: a register that lost its entries, or the constant above naming a package
        // nobody ships.
        if (mine.Count == 0)
        {
            throw new InvalidOperationException(
                $"The component register names nothing in package '{ThisPackage}'. This program "
                + "carries a bundled .NET runtime, so an empty answer would be a false one.");
        }

        return mine;
    }

    /// <summary>
    /// The version to state, and the two cases are not a style choice.
    ///
    /// A component we REFERENCE carries its version in the register, because we chose it. One the
    /// SDK resolves does not, because its version is whatever .NET built this file - so the
    /// register marks it and the number is read from the runtime that is actually running.
    /// Measured 2026-09-23: <c>Environment.Version</c> answered 10.0.12 and the runtime pack the
    /// publish resolved was 10.0.12.
    ///
    /// <b>Why nothing is probed out of an assembly for the rest.</b> Measured the same day on
    /// Microsoft.Windows.SDK.NET, whose package version is 10.0.17763.57: the file version of the
    /// assembly it ships is 10.0.17763.55 and its assembly version is 10.0.17763.38. Three
    /// numbers, all plausible, none of them the answer. A number that looks right and is wrong is
    /// worse than saying where the exact one is written down.
    /// </summary>
    private static string Version(JsonElement entry)
    {
        if (entry.TryGetProperty("version", out var stated))
        {
            return stated.GetString() ?? string.Empty;
        }

        if (entry.TryGetProperty("version_at_runtime", out var probe)
            && string.Equals(probe.GetString(), "dotnet", StringComparison.Ordinal))
        {
            return Environment.Version.ToString();
        }

        return Texts.Of("cli.licence.versionFromBuild");
    }

    private static string Text(JsonElement entry, string field) =>
        entry.GetProperty(field).GetString() ?? string.Empty;

    private static string Product(JsonElement root, string field) =>
        Text(root.GetProperty("product"), field);

    private static string Executable(JsonElement root) =>
        Text(root.GetProperty("packages").GetProperty(ThisPackage), "executable");

    /// <remarks>
    /// Naming what the assembly does carry turns "it is missing" into "it is called something
    /// else", which is the difference between a puzzle and a fix. The same reasoning, and the
    /// same hard lesson, as <see cref="Texts"/> one file over.
    /// </remarks>
    private static Stream Register()
    {
        var assembly = Assembly.GetExecutingAssembly();

        return assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"The component register '{ResourceName}' is not in this executable. "
                + $"It carries: [{string.Join(", ", assembly.GetManifestResourceNames())}].");
    }
}
