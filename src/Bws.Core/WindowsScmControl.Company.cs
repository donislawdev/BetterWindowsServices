using System.ComponentModel;

namespace Bws.Core;

/// <summary>
/// The two readings the step that ends a process takes just before it does: who lives in the process,
/// and who depends on them. Since 2026-09-30, stability report W-6, package B2.
///
/// <b>Handed to the catalog rather than written again</b>, because these are the listing's own questions
/// and a second implementation would be a second answer to one question. The catalog is built per call:
/// it holds nothing but a setting about network paths that neither of these reads, so there is no state
/// to share and nothing to keep alive between two plans.
///
/// <b>In a file of its own because the size ceiling asked for it</b> - the file beside it holds 198 lines
/// of code against a line of 202.
/// </summary>
public sealed partial class WindowsScmControl
{
    public Reading<IReadOnlyList<ScmStatus>> ReadStatuses()
    {
        try
        {
            return Reading<IReadOnlyList<ScmStatus>>.Present(new WindowsScmCatalog().ReadStatuses());
        }
        catch (Win32Exception refused)
        {
            // The catalog throws, because a listing that failed is a broken screen. Here it is a step that
            // refuses and ends nothing, so it goes back as the refusal it is, number and words.
            return Reading<IReadOnlyList<ScmStatus>>.Denied(refused.NativeErrorCode, refused.Message);
        }
    }

    public Reading<IReadOnlyList<string>> ReadDependents(string serviceName) =>
        new WindowsScmCatalog().ReadDependents(serviceName);
}
