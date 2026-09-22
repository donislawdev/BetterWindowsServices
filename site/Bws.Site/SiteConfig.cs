namespace Bws.Site;

/// <summary>
/// The facts about the site that are not facts about the program: where it is served from, what
/// it links to, which languages it speaks. One file, <c>site/site.json</c>, and every page reads
/// these through tokens rather than repeating them - the address of the repository appears once.
/// </summary>
internal sealed record SiteConfig
{
    public required string Host { get; init; }

    public required string Product { get; init; }

    /// <summary>The repository address without a trailing slash, e.g. <c>https://github.com/owner/name</c>.</summary>
    public required string Repo { get; init; }

    public required string Support { get; init; }

    /// <summary>The first language is the default and lives at the root, the others under <c>/{code}/</c>.</summary>
    public required string[] Languages { get; init; }

    public required string ThemeColor { get; init; }

    public required SocialImage SocialImage { get; init; }

    /// <summary>
    /// Google Search Console verification, as a <c>&lt;meta&gt;</c> when set. Empty means the tag
    /// is not written at all, which is what a site that has not been claimed should do.
    /// </summary>
    public string SearchConsoleToken { get; init; } = "";

    public required Archives Archives { get; init; }

    /// <summary>
    /// The hosts an <c>href</c> may point at. The site promises to load nothing from anywhere
    /// else, so a stylesheet, script or image from another host fails the strict build outright.
    /// A link a person clicks is different - it takes them somewhere on purpose - and even those
    /// are held to this list, so a typo in a domain is caught rather than published.
    /// </summary>
    public required string[] LinkHosts { get; init; }

    public string Origin => "https://" + Host;

    public string DefaultLanguage => Languages[0];

    internal static SiteConfig Load(string root)
    {
        var path = Path.Combine(root, "site", "site.json");
        var config = Json.Read<SiteConfig>(path);

        if (config.Languages.Length == 0)
        {
            throw new InvalidOperationException($"{path} names no languages.");
        }

        return config;
    }
}

internal sealed record SocialImage
{
    public required string Path { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    public required string Alt { get; init; }

    /// <summary>
    /// The words drawn ON the card, which this generator never renders and validates anyway.
    ///
    /// <b>Why a type for something nothing here draws.</b> The card is drawn by
    /// <c>tools/site/social-preview.ps1</c>, which is outside git - so its strings live here,
    /// where somebody reviewing this repository can read them. Declaring them means an unknown
    /// key in this block is refused like any other, a missing one fails the build, and
    /// <c>PublicSurfaceGuards</c> holds them to ASCII and to the privacy list along with
    /// everything else. Leaving them undeclared would have meant the opposite: the one block of
    /// text nobody proof-reads, unchecked.
    /// </summary>
    public required SocialCard Card { get; init; }
}

/// <summary>
/// The card a chat client or a social site draws when somebody pastes the site's address.
/// English only, because Open Graph declares one image for the whole site.
/// </summary>
internal sealed record SocialCard
{
    /// <summary>The first line of the heading, in plain white.</summary>
    public required string Headline { get; init; }

    /// <summary>The second line, in the accent colour. Two short lines, not one long one.</summary>
    public required string HeadlineAccent { get; init; }

    public required string Lede { get; init; }

    public required string[] Chips { get; init; }

    public required string Platform { get; init; }

    public required string Footnote { get; init; }

    public required string Licence { get; init; }

    /// <summary>
    /// The two panels are built the same way on purpose: a line of what you type, a rule, and the
    /// table it gives back. So each one carries a command, an optional count beside it, three
    /// column headings, its rows, and a caption under the panel.
    /// </summary>
    public required string Query { get; init; }

    public required string QueryCount { get; init; }

    /// <summary>How many of <see cref="RowsPicked"/> are drawn on the selection colour.</summary>
    public required int Picked { get; init; }

    /// <summary>Name, status and start type per row - the three columns the card has room for.</summary>
    public required string[][] RowsPicked { get; init; }

    public required string PanelFilterNote { get; init; }

    public required string DriftCommand { get; init; }

    public required string DriftCount { get; init; }

    /// <summary>The heading over the three columns. The first names the entry the rows are about.</summary>
    public required string[] DriftColumns { get; init; }

    public required string[][] DriftRows { get; init; }

    public required string Caption { get; init; }
}

internal sealed record Archives
{
    public required string Window { get; init; }

    public required string Cli { get; init; }
}
