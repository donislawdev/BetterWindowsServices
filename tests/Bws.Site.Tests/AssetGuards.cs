using System.Text.Json;

namespace Bws.Site.Tests;

/// <summary>
/// The files that sit BESIDE the generator, asked whether they are what site.json says they are.
///
/// <b>Why these are not in GeneratorTests.</b> Every test in that file damages something on
/// purpose and asks the build whether it notices - it is about the generator. These three build
/// nothing. They read two binaries and a block of JSON off disk and compare them with what every
/// published page claims about them, which is a different question with a different failure mode:
/// nothing here is ever wrong at build time, it is wrong for a stranger looking at a browser tab
/// or at a link somebody pasted.
///
/// <b>They were in that file until 2026-09-22</b>, when it reached 504 lines and
/// <c>SizeRatchetGuards.Not_more_files_are_long_than_were_long</c> went red - a third test file
/// over five hundred where the ratchet was set at two. The ratchet was not raised. This is the
/// seam it pointed at.
/// </summary>
public sealed class AssetGuards
{
    /// <summary>
    /// The site's favicon is the program's icon, byte for byte.
    ///
    /// <b>Two copies of an icon is two icons that will differ, and this pair already did.</b>
    /// <c>src/Bws.Gui/Bws.Gui.csproj</c> says exactly that in a comment, which is why both shipped
    /// programs point at one file - but <c>site/assets/favicon.ico</c> is a hand made copy of that
    /// file and nothing compared them. On 2026-09-22 the bean was redrawn half again as large and
    /// the site would have kept serving the old small one in browser tabs, looking like a
    /// different product from the one in the taskbar. Nothing in the build would have said a word.
    ///
    /// <b>Why a copy at all, rather than the generator reading src/.</b> The Pages workflow builds
    /// from the repository and the generator is told not to reach into <c>src/</c> for anything
    /// but the two files it parses - the site's assets are the site's. The copy is the price, and
    /// this test is what makes the price payable. Re-copy it after running
    /// <c>tools/icon/make-ico.ps1</c>.
    /// </summary>
    [Fact]
    public void The_sites_favicon_is_the_programs_icon()
    {
        var root = Repository.Root();
        var shipped = File.ReadAllBytes(Path.Combine(root, "src", "bws.ico"));
        var served = File.ReadAllBytes(Path.Combine(root, "site", "assets", "favicon.ico"));

        Assert.True(
            shipped.AsSpan().SequenceEqual(served),
            $"site/assets/favicon.ico is {served.Length} bytes and src/bws.ico is {shipped.Length}, "
            + "and they have to be the same file. The site would otherwise serve a different "
            + "drawing from the one in the taskbar. Copy src/bws.ico over it.");
    }

    /// <summary>
    /// The social card on disk is the size every page claims it is.
    ///
    /// <b>The card is the one image on this site that nobody ever looks at.</b> It appears only in
    /// somebody else's chat window, after the link has already been sent. Every page writes
    /// <c>og:image:width</c> and <c>og:image:height</c> out of site.json, and a card drawn at
    /// another size makes all twenty two pages tell a client the wrong thing - which some clients
    /// use to reserve space before the image arrives.
    ///
    /// Read out of the PNG header rather than with an image library: bytes 16 to 24 of a PNG are
    /// the width and the height of IHDR, big endian, and they are in the first chunk by
    /// definition of the format.
    /// </summary>
    [Fact]
    public void The_social_card_is_the_size_every_page_says_it_is()
    {
        var root = Repository.Root();
        var declared = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "site", "site.json")))
            .RootElement.GetProperty("social_image");
        var relative = declared.GetProperty("path").GetString()!.TrimStart('/');
        var file = Path.Combine(root, "site", relative.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(file), $"site.json points social_image at '{relative}' and there is no file there.");

        var bytes = File.ReadAllBytes(file);
        Assert.True(bytes.Length > 24, $"{relative} is {bytes.Length} bytes, which is not a PNG.");
        Assert.True(bytes[0] == 0x89 && bytes[1] == 0x50, $"{relative} does not start with the PNG signature.");

        var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        var height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];

        Assert.True(
            width == declared.GetProperty("width").GetInt32() && height == declared.GetProperty("height").GetInt32(),
            $"{relative} is {width} by {height} and site.json tells every page it is "
            + $"{declared.GetProperty("width").GetInt32()} by {declared.GetProperty("height").GetInt32()}. "
            + "Redraw it with tools/site/social-preview.ps1, which refuses a size site.json does not declare.");
    }

    /// <summary>
    /// The words on the card are all there and both of its tables have three columns.
    ///
    /// <b>The drawing tool is outside git and cannot be tested from here, so this tests its
    /// input.</b> A missing string would be drawn as an empty box and a row of a different length
    /// would be drawn off the edge of a panel - both in an image only a stranger ever sees.
    ///
    /// <b>The number of picked rows is checked against the rows that exist</b>, because the card
    /// draws the first <c>picked</c> of them on the selection colour and a count larger than the
    /// list would simply highlight everything without complaining.
    /// </summary>
    [Fact]
    public void The_social_cards_words_are_complete()
    {
        var card = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repository.Root(), "site", "site.json")))
            .RootElement.GetProperty("social_image").GetProperty("card");

        string[] spoken =
        [
            "headline", "headline_accent", "lede", "platform", "footnote", "licence",
            "query", "query_count", "panel_filter_note",
            "drift_command", "drift_count", "caption",
        ];

        foreach (var key in spoken)
        {
            var value = card.GetProperty(key).GetString();
            Assert.False(string.IsNullOrWhiteSpace(value), $"social_image.card.{key} is empty, and it is drawn on the card.");
        }

        var chips = card.GetProperty("chips").EnumerateArray().Select(chip => chip.GetString()).ToList();
        Assert.True(chips.Count is >= 4 and <= 6, $"The card has room for four to six chips and site.json gives {chips.Count}.");
        Assert.DoesNotContain(chips, chip => string.IsNullOrWhiteSpace(chip));

        // Both tables are three columns wide - name, then two values - and the drawing puts each
        // column at a fixed fraction of its panel, so a row of any other length lands nowhere.
        foreach (var table in new[] { "rows_picked", "drift_rows" })
        {
            var rows = card.GetProperty(table).EnumerateArray().Select(row => row.GetArrayLength()).ToList();
            Assert.True(rows.Count > 0, $"social_image.card.{table} has no rows and a panel would be drawn empty.");
            Assert.DoesNotContain(rows, length => length != 3);
        }

        Assert.True(
            card.GetProperty("drift_columns").GetArrayLength() == 3,
            "The drift panel has three column headings: the entry the rows are about, then the two moments.");

        // Both panels are drawn by the same code and read as one idea stated twice, so both need
        // the line of what you type. A panel with no command is half a panel.
        foreach (var command in new[] { "query", "drift_command" })
        {
            Assert.StartsWith(
                command == "query" ? "start:" : "bws ",
                card.GetProperty(command).GetString(),
                StringComparison.Ordinal);
        }

        var picked = card.GetProperty("picked").GetInt32();
        var available = card.GetProperty("rows_picked").GetArrayLength();
        Assert.True(
            picked > 0 && picked < available,
            $"The card draws {picked} of {available} rows as picked. It has to be at least one - the panel is "
            + "about acting on several at once - and fewer than all of them, because a row left unpicked is "
            + "what shows that picking is a choice.");
    }
}
