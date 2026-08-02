namespace Bws.Cli;

/// <summary>
/// The one place in this tool that writes to the data channel.
///
/// It exists because of a guard, and the guard was right. <c>OutputChannelGuards</c> holds that
/// exactly one place may write to standard output, on the grounds that a second one makes "what
/// lands in somebody's pipe" depend on which branch ran. Adding help and version on 2026-08-02
/// made three, and the honest fix was not to loosen the count - it was to make the one place
/// real rather than incidental.
///
/// <b>The rule this protects is a frozen contract, not tidiness.</b> Standard output carries
/// data and nothing else: a listing, a JSON document, a version. Everything a person reads while
/// something goes wrong goes to standard error, so a failed run never drops a stray line into
/// whatever comes next in the pipeline. docs/02 writes it down and two guards keep it.
/// </summary>
internal static class Output
{
    /// <summary>
    /// Put a document on the data channel.
    ///
    /// One method rather than one per kind of document, because the channel does not care and a
    /// second method would be a second place again by a different name.
    /// </summary>
    internal static void Data(string document) => Console.Out.WriteLine(document);
}
