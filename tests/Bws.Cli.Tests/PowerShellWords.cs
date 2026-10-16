namespace Bws.Cli.Tests;

/// <summary>
/// Words the way PowerShell hands them to a program, for the two quoted forms the core writes and
/// nothing else - a model of a measurement, not a shell.
///
/// <b>What it was measured against.</b> On pwsh 7.6.6, 2026-10-05 (stability report W-12): a word in
/// double quotes arrives whole, a word in single quotes arrives whole with each doubled quote mark
/// made one, and <c>--</c> reaches the program. On pwsh 7.6.7, 2026-10-16: a bare word that looks
/// like a number - <c>1kb</c>, <c>0x10</c>, <c>1e5</c>, <c>7n</c> - reaches a native program as
/// written, not as the number PowerShell would read it as. Windows PowerShell 5.1 NOT MEASURED.
///
/// Its own file since 2026-10-16, when a second test began cutting lines with it: the guard that
/// checks a handful of names, and the property that checks any name.
/// </summary>
internal sealed class PowerShellWords
{
    private readonly List<string> _words = [];
    private readonly System.Text.StringBuilder _word = new();
    private char? _quote;

    internal List<string> Of(string line)
    {
        for (var index = 0; index < line.Length; index++)
        {
            if (_quote is null)
            {
                Outside(line[index]);
            }
            else if (Closes(line, ref index))
            {
                _quote = null;
            }
            else
            {
                _word.Append(line[index]);
            }
        }

        _words.Add(_word.ToString());
        return _words;
    }

    private void Outside(char character)
    {
        if (character == ' ')
        {
            _words.Add(_word.ToString());
            _word.Clear();
        }
        else if (character == '"' || SingleMark(character))
        {
            _quote = character;
        }
        else
        {
            _word.Append(character);
        }
    }

    /// <summary>
    /// Whether the character at the index ends the quoted word. Inside single quotes a doubled mark
    /// is one character rather than an end - the index moves to the second of the pair, which is kept.
    /// </summary>
    private bool Closes(string line, ref int index)
    {
        if (_quote == '"' || !SingleMark(line[index]))
        {
            return _quote == '"' && line[index] == '"';
        }

        if (index + 1 < line.Length && SingleMark(line[index + 1]))
        {
            index++;
            return false;
        }

        return true;
    }

    private static bool SingleMark(char character) =>
        character is '\'' or (char)0x2018 or (char)0x2019 or (char)0x201A or (char)0x201B;
}
