using Bws.Site;

// The website generator. Reads site/ and the program, writes a directory of static files.
//
//   dotnet run --project site/Bws.Site -- --out artifacts/site
//   dotnet run --project site/Bws.Site -- --out artifacts/site --strict
//
// --strict turns every problem into exit code 1 and is what the workflow uses. Without it the
// site is written anyway and the problems are printed, so a page can be looked at while its
// second language is half done - and the list says exactly how half.

var strict = false;
string? root = null;
var output = "artifacts/site";

for (var index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--strict":
            strict = true;
            break;

        case "--out" when index + 1 < args.Length:
            output = args[++index];
            break;

        case "--root" when index + 1 < args.Length:
            root = args[++index];
            break;

        default:
            Console.Error.WriteLine($"bws-site: unknown argument '{args[index]}'. Takes --out DIR, --root DIR, --strict.");
            return 2;
    }
}

try
{
    var repository = SourceTree.Root(root);
    var target = Path.IsPathRooted(output) ? output : Path.Combine(repository, output);

    var build = new Build(repository, target);
    build.Run();

    foreach (var problem in build.Problems)
    {
        Console.Error.WriteLine("  " + problem);
    }

    Console.WriteLine($"bws-site: {build.PagesWritten} pages into {target}");

    if (build.Problems.Count == 0)
    {
        Console.WriteLine("bws-site: no problems");
        return 0;
    }

    Console.WriteLine($"bws-site: {build.Problems.Count} problem(s){(strict ? "" : " - not strict, so the site was written anyway")}");
    return strict ? 1 : 0;
}
catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException or System.Text.Json.JsonException)
{
    // Broad enough to name the four things that go wrong when a file is missing, malformed or
    // unreadable, and narrow enough that a bug in this program still comes out as a stack trace
    // rather than as a tidy sentence hiding it.
    Console.Error.WriteLine("bws-site: " + error.Message);
    return 1;
}
