namespace Bws.Architecture.Tests;

/// <summary>
/// Two rules about the prose in this repository, which nothing checked before 2026-09-23: a flat
/// hyphen and no semicolons (rule 12 of CLAUDE.md), and English only (rule 13).
///
/// Added on the owner's decision, after the same pair in his Rust project. Measured the day this was
/// written, before a line was changed: 58 comment lines carried a semicolon in their prose, 23 under
/// src/ and 35 under tests/, plus one in the changelog and one in a markup comment. All of them were
/// rewritten the same day, so this is an assertion of zero rather than a ratchet - a ratchet would
/// quietly permit the first new one.
///
/// <b>English is checked by WORD, because the letters are already held.</b> The ASCII sweep in
/// <see cref="PublicSurfaceGuards"/> refuses a Polish letter, so what reaches a comment is Polish with
/// its letters stripped - and that is exactly what was found: nine lines in eight files quoting the
/// planning documents, the specification most of all, written without their diacritics and invisible
/// to the letter check. They were translated. The owner's Rust project recorded the same about itself.
///
/// <b>What this does NOT read:</b> Markdown outside the root, site/ and packaging/, and comments
/// trailing a line of YAML. Polish is looked for in comments of code and markup only - the Markdown
/// and the workflows are held to the semicolon rule, which is where they were measured.
/// </summary>
public sealed class ProseGuards
{
    /// <summary>The floor under the comment lines read - 38 089 on the day. It catches a scan that read nothing.</summary>
    private const int FewestLinesRead = 10000;

    /// <summary>
    /// Polish words that survive without their diacritics. Every one has to be a word that cannot be
    /// English - "to", "me" and "pole" are Polish too and are not here, because a list that reddens on
    /// English prose gets switched off rather than fixed. It may grow and should not shrink.
    /// </summary>
    private static readonly HashSet<string> PolishWords = new(StringComparer.Ordinal)
    {
        "nie", "oraz", "tylko", "przez", "zeby", "czyli", "albo", "wiec", "dlatego", "poniewaz", "zamiast",
        "wylacznie", "kolejnosc", "plasterek", "plasterka", "straznik", "straznika", "wlasciciel", "wlasciciela",
        "zmierzone", "sonda", "bramka", "jawne", "cisza", "uslugi", "usluga", "wiersz", "wiersza", "okno",
        "czlowieka", "wiedzac", "kliknie", "zatrzyma", "oznaczanie", "cichego", "pokazywania", "etykieta",
    };

    /// <summary>
    /// Files whose comments may quote Polish, each with the quotation itself and the reason. A quotation
    /// of DATA is not prose written in Polish - the rule is about what this project writes, not what
    /// Windows says.
    ///
    /// <b>The quotation, not the file.</b> The first version excused every comment in the file, so a
    /// Polish sentence written beside the quotation would have passed - the review of the pull request
    /// that brought this said so. Only the registered text is taken out of a line now, and the rest of
    /// that line, and every other line of the file, is read like any other.
    /// </summary>
    private static readonly Dictionary<string, Excuse> MayQuotePolish = new(StringComparer.Ordinal)
    {
        ["src/Bws.Core/Reading.cs"] = new(
            "\"Nie mo\u017cna uruchomi\u0107",
            "Quotes the message a Polish Windows returns for a refusal, beside the English one, to show why the " +
            "product keeps the error number rather than the sentence. It is the example, not the prose."),
    };

    private static readonly Lazy<ProseLine[]> CodeComments = new(() =>
    [
        .. Sources.Shipped().Concat(Sources.Testing())
            .SelectMany(path => Prose.InCode(CodeShape.NameOf(path), CodeShape.TreeOf(path).GetRoot())),
        .. Sources.ShippedMarkup().SelectMany(path => Prose.InMarkup(CodeShape.NameOf(path), File.ReadAllText(path))),
    ]);

    private static readonly Lazy<ProseLine[]> Documents = new(() =>
    [
        .. PublicSurfaceGuards.Published().Select(path => (Path: path, Name: CodeShape.NameOf(path)))
            .Where(file => IsRootMarkdown(file.Name) || IsWorkflow(file.Name))
            .SelectMany(file => IsWorkflow(file.Name)
                ? Prose.InWorkflow(file.Name, File.ReadAllLines(file.Path))
                : Prose.InMarkdown(file.Name, File.ReadAllLines(file.Path))),
    ]);

    [Fact]
    public void No_prose_carries_a_semicolon()
    {
        var found = CodeComments.Value.Concat(Documents.Value)
            .Where(line => Prose.Unquoted(line.Text).Contains(';', StringComparison.Ordinal))
            .Select(line => $"  {line.Where}  {line.Text.Trim()}")
            .ToArray();

        Assert.True(CodeComments.Value.Length >= FewestLinesRead, $"The prose scan read {CodeComments.Value.Length} comment lines - it is reading the wrong place.");
        Assert.True(Documents.Value.Length > 0, "The prose scan read no Markdown and no workflow - the list of files in git came back empty.");
        Assert.True(
            found.Length == 0,
            "Rule 12: a flat hyphen, and no semicolon in prose. Code, a quotation, an entity and an address are " +
            "already left out, so what is left is a sentence - end it, or join it with a hyphen:" +
            Environment.NewLine + string.Join(Environment.NewLine, found));
    }

    [Fact]
    public void No_comment_is_written_in_polish()
    {
        var found = CodeComments.Value
            .Where(IsPolishProse)
            .Select(line => $"  {line.Where}  {PolishIn(line.Text)}: {line.Text.Trim()}")
            .ToArray();

        Assert.True(
            found.Length == 0,
            "Rule 13: everything in this repository is English, comments included. A Polish word without its " +
            "diacritics looks like English to the letter check, which is how these got in:" +
            Environment.NewLine + string.Join(Environment.NewLine, found));
    }

    [Fact]
    public void The_files_excused_from_the_language_rule_still_quote_polish()
    {
        // The other direction, so an excuse cannot outlive the quotation it was written for - and since
        // the excuse is the quotation itself, the file has to still carry that exact text.
        var stale = MayQuotePolish
            .Where(excuse => !CodeComments.Value.Any(line =>
                line.File == excuse.Key && line.Text.Contains(excuse.Value.Quotation, StringComparison.Ordinal)))
            .Select(excuse => excuse.Key)
            .ToArray();

        Assert.True(stale.Length == 0, "These files are excused for a quotation they no longer carry: " + string.Join(", ", stale));
    }

    [Theory]
    [InlineData("Reads the file, then writes it back.", false)]
    [InlineData("Reads the file; then writes it back.", true)]
    [InlineData("Ends with <c>return;</c> and nothing else.", false)]
    [InlineData("Ends with `return;` and nothing else.", false)]
    [InlineData("The manager says \"access denied; try again\" here.", false)]
    [InlineData("Written as &lt;Foo&gt; in the markup.", false)]
    [InlineData("See https://example.org/a;b for the reason.", false)]
    public void A_semicolon_counts_only_in_the_prose_itself(string line, bool counts) =>
        Assert.Equal(counts, Prose.Unquoted(line).Contains(';', StringComparison.Ordinal));

    [Fact]
    public void A_code_block_in_a_documentation_comment_is_not_prose_and_keeps_its_line_numbers()
    {
        var root = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
            "/// <summary>\n/// Shaped like this:\n/// <code>\n/// var answer = 42;\n/// </code>\n/// </summary>\nclass C { }",
            CodeShape.Language).GetRoot();
        var lines = Prose.InCode("C.cs", root).ToArray();

        Assert.DoesNotContain(lines, line => Prose.Unquoted(line.Text).Contains(';', StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Line == 2 && line.Text.Contains("Shaped", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("// the owner said: nie wiedzac o tym", "nie")]
    [InlineData("// the SONDA reads it", "sonda")]
    [InlineData("// a Nieuwland probe and a sondage", null)]
    [InlineData("// nothing Polish here at all", null)]
    public void A_polish_word_is_found_only_as_a_whole_word(string line, string? expected) =>
        Assert.Equal(expected, PolishIn(line));

    [Theory]
    [InlineData("/// reads \"The service cannot be started...\" on one machine and \"Nie mo\u017cna uruchomi\u0107", false)]
    [InlineData("// to nie jest dobre", true)]
    [InlineData("/// \"Nie mo\u017cna uruchomi\u0107 nie wiem\" is not the quotation it is excused for", true)]
    public void Only_the_quotation_a_file_is_excused_for_is_excused(string text, bool reported) =>
        Assert.Equal(reported, IsPolishProse(new ProseLine("src/Bws.Core/Reading.cs", 1, text)));

    [Fact]
    public void A_fence_closes_only_on_its_own_marker()
    {
        // A four-backtick fence around a three-backtick one, a closing marker that carries text and so
        // is not one, and a tilde fence. The prose is the three lines numbered below and nothing else.
        string[] lines =
        [
            "Prose one.",
            "````markdown",
            "```",
            "inside; still code",
            "```",
            "````",
            "Prose two.",
            "~~~",
            "code; here",
            "```js",
            "~~~",
            "Prose three.",
        ];

        Assert.Equal([1, 7, 12], Prose.InMarkdown("X.md", lines).Select(line => line.Line));
    }

    /// <summary>Whether a line of prose holds a Polish word outside the quotation its file is excused for.</summary>
    private static bool IsPolishProse(ProseLine line) =>
        PolishIn(MayQuotePolish.TryGetValue(line.File, out var excuse)
            ? line.Text.Replace(excuse.Quotation, " ", StringComparison.Ordinal)
            : line.Text) is not null;

    /// <summary>One quotation of Polish a file may carry in its comments, and why.</summary>
    private sealed record Excuse(string Quotation, string Reason);

    /// <summary>The first Polish word a line holds, whole and in any case, or nothing.</summary>
    private static string? PolishIn(string line) =>
        line.Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.ToLowerInvariant())
            .FirstOrDefault(PolishWords.Contains);

    private static readonly char[] Separators = [.. Enumerable.Range(0, 128).Select(code => (char)code).Where(character => !char.IsLetter(character))];

    private static bool IsRootMarkdown(string name) => name.EndsWith(".md", StringComparison.Ordinal) && !name.Contains('/', StringComparison.Ordinal);

    private static bool IsWorkflow(string name) => name.StartsWith(".github/", StringComparison.Ordinal) && name.EndsWith(".yml", StringComparison.Ordinal);
}
