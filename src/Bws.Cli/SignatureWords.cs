using Bws.Core;

namespace Bws.Cli;

/// <summary>
/// The word for a signature verdict, in sentence case rather than the name of an enumeration value.
///
/// <b>WRITTEN 2026-09-01 FOR BACKLOG 260, AND IT IS THE SAME FAULT <see cref="StatusWords"/>
/// REPAIRS ONE FAMILY OVER.</b> The window has said <c>Not signed</c> and <c>Untrusted root</c>
/// since it grew the column, and this tool went on printing <c>NotSigned</c> and
/// <c>UntrustedRoot</c> - shapes from the code rather than words for a person. One machine, one
/// verdict, two spellings depending on which interface somebody happened to open.
///
/// <b>The machine readable output is not touched and must not be.</b> The full argument is in
/// <see cref="StatusWords"/> and it is the same one: <c>--json</c> and the snapshot carry these
/// values as field names, they are a frozen contract in `docs/02`, and every tool in
/// <c>tools/</c> that compares this product against <c>sc.exe</c> reads them.
///
/// <b>Keys written out one by one rather than built from the value's name</b>, and <b>this tool's
/// own keys rather than the window's</b> - both for the reasons <see cref="StatusWords"/> gives.
/// The second one is worth repeating because it looks like duplication and is not: what a person
/// reads comes out of the language file of the interface they are looking at, and the two files
/// agree today because somebody made them agree.
///
/// <b>Only the two members that need it? No - every one of them.</b> <c>NotSigned</c> and
/// <c>UntrustedRoot</c> are the only two that read wrong, but a switch covering two members and
/// falling through for five would print a word for some verdicts and a value name for others,
/// which is the disagreement this class exists to end rather than a smaller version of it.
/// </summary>
internal static class SignatureWords
{
    internal static string Of(SignatureStatus status) => status switch
    {
        SignatureStatus.Trusted => Texts.Of("cli.cell.signature.trusted"),
        SignatureStatus.NotSigned => Texts.Of("cli.cell.signature.notSigned"),
        SignatureStatus.UntrustedRoot => Texts.Of("cli.cell.signature.untrustedRoot"),
        SignatureStatus.Expired => Texts.Of("cli.cell.signature.expired"),
        SignatureStatus.Revoked => Texts.Of("cli.cell.signature.revoked"),
        SignatureStatus.Tampered => Texts.Of("cli.cell.signature.tampered"),
        _ => Texts.Of("cli.cell.unknown")
    };
}
