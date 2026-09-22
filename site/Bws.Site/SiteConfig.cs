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
}

internal sealed record Archives
{
    public required string Window { get; init; }

    public required string Cli { get; init; }
}
