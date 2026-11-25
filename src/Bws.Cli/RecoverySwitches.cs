using Bws.Core.Planning;

namespace Bws.Cli;

/// <summary>
/// The four switches of the recovery verb, read as they go past - each with its value after it or after an
/// <c>=</c>, the way <c>--query</c> takes one. Since 2026-11-24 (backlog 555 part a2).
///
/// <b>A small collector of its own rather than four more branches in <c>CommandLine.Read</c></b>, which is the
/// longest method in the product and an exemption backlog 24 has been asking to split since 2026-08-02. One line
/// there hands each argument here.
/// </summary>
internal sealed class RecoverySwitches
{
    /// <summary>The four, as typed - and the ones <see cref="CommandLine"/> counts as carrying a value.</summary>
    internal static readonly string[] Names =
    [
        RecoveryCommandWords.Reset, RecoveryCommandWords.Program,
        RecoveryCommandWords.Message, RecoveryCommandWords.OnErrorStops
    ];

    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <summary>
    /// Takes one of the four with its value, noting it as given - or as incomplete when the value is missing.
    /// False for any other argument, which the reader then reads itself.
    ///
    /// <b>An empty value is a removal for the program and the message</b>, which is how the switch documents
    /// it, and nothing at all for the other two - so <c>--reset ""</c> is a value missing, the way
    /// <c>--query ""</c> is, and never a period of nothing.
    /// </summary>
    internal bool Take(string[] arguments, ref int index, List<string> given, List<string> incomplete)
    {
        var argument = arguments[index];

        foreach (var name in Names)
        {
            string? value;

            if (argument.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                value = argument[(name.Length + 1)..];
            }
            else if (Arguments.Matches(argument, name))
            {
                value = Arguments.NeedsValue(arguments, index) ? null : arguments[++index];
            }
            else
            {
                continue;
            }

            given.Add(name);

            var mayBeEmpty = name is RecoveryCommandWords.Program or RecoveryCommandWords.Message;

            if (value is null || (!mayBeEmpty && string.IsNullOrWhiteSpace(value)))
            {
                incomplete.Add(name);
            }
            else
            {
                _values[name] = value;
            }

            return true;
        }

        return false;
    }

    /// <summary>The ask, with the item words the reader collected after the name.</summary>
    internal RecoveryAsk Ask(IReadOnlyList<string> items) => new(
        items,
        _values.GetValueOrDefault(RecoveryCommandWords.Reset),
        _values.GetValueOrDefault(RecoveryCommandWords.Program),
        _values.GetValueOrDefault(RecoveryCommandWords.Message),
        _values.GetValueOrDefault(RecoveryCommandWords.OnErrorStops));
}
