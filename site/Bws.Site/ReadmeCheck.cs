using System.Globalization;
using System.Text.RegularExpressions;

namespace Bws.Site;

/// <summary>
/// The README's own tables of switches and exit codes, held against the program - backlog 391.
///
/// <b>Why here rather than in a tool of its own.</b> The README carries a third copy of two
/// frozen contracts: the switch table and the exit code table, both of which also live in
/// docs/02 (checked by tools/audit/audit.ps1) and in the code. The README's copy is the first
/// one a stranger reads and the only one in the repository, and nothing checked it. This
/// generator already reads the code's version of both for the reference page, so the comparison
/// costs one file rather than another instrument nobody remembers to run.
///
/// <b>It runs one way on purpose: a switch or a code in the program that the README never names
/// is a problem, and the reverse is not.</b> A README is prose - it may describe the same thing
/// twice, or mention a switch inside a sentence rather than in the table - and a check that
/// demanded a row per mention would be a check people learn to work around. What it catches is
/// the case that actually happens: something new in the code, and a README quietly out of date.
/// </summary>
internal static class ReadmeCheck
{
    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(2);

    internal static void Run(string root, ProductFacts facts, Problems problems)
    {
        var path = Path.Combine(root, "README.md");
        if (!File.Exists(path))
        {
            problems.Add($"readme: {path} is missing.");
            return;
        }

        var text = File.ReadAllText(path);

        foreach (var option in facts.Switches)
        {
            if (!Regex.IsMatch(text, @"^\|\s*`" + Regex.Escape(option.Name) + @"[ `]", RegexOptions.Multiline, Ceiling))
            {
                problems.Add($"readme: the switch table has no row for {option.Name}, and OptionSurface.cs has it.");
            }
        }

        foreach (var code in facts.ExitCodes)
        {
            var row = "^\\|\\s*`" + code.Value.ToString(CultureInfo.InvariantCulture) + "`\\s*\\|";
            if (!Regex.IsMatch(text, row, RegexOptions.Multiline, Ceiling))
            {
                problems.Add($"readme: the exit code table has no row for {code.Value} ({code.Name}), and ExitCode.cs has it.");
            }
        }

        // The version the README's own instructions produce - the one place it names a number
        // about the build rather than about a service.
        if (!text.Contains("betterwindowsservices.donislawdev.com", StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("readme: it does not name the website, and the website's reference page is what its field table points at.");
        }
    }
}
