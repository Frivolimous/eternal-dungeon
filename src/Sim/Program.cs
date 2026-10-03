using EternalDungeon.Core;
using EternalDungeon.Core.Assets;
using EternalDungeon.Core.Data;

// Command-line tool. Battle commands (run, batch) arrive in M1.
//   sim data     load and validate data/*.json
//   sim assets   check game/assets/manifest.json and list AI placeholders

var command = args.Length > 0 ? args[0] : "help";
var root = RepoRoot.Find();

try
{
    return command switch
    {
        "data" => Data(),
        "assets" => Assets(),
        _ => Help(),
    };
}
catch (DataException e)
{
    Console.Error.WriteLine($"Data error: {e.Message}");
    return 1;
}

int Data()
{
    var data = DataLoader.LoadDirectory(Path.Combine(root, "data"));
    Console.WriteLine($"{GameInfo.Title} {GameInfo.Version}: data OK");
    Console.WriteLine($"  {data.TagList.Count} tags, {data.StatList.Count} stats");
    return 0;
}

int Assets()
{
    var dir = Path.Combine(root, "game", "assets");
    var manifest = AssetManifest.Load(dir);
    var problems = manifest.CheckFiles(dir);
    var flagged = manifest.AiPlaceholders.ToList();

    Console.WriteLine($"{manifest.Entries.Count} assets in the manifest, {flagged.Count} flagged as AI placeholders.");
    foreach (var e in flagged)
        Console.WriteLine($"  AI  {e.Id,-24} {e.Path} ({e.Width}×{e.Height}){(e.Note is null ? "" : "  " + e.Note)}");
    foreach (var p in problems)
        Console.WriteLine($"  !!  {p}");
    if (flagged.Count > 0)
        Console.WriteLine("Every flagged asset must be replaced by human-made art before release.");
    return problems.Count > 0 ? 1 : 0;
}

int Help()
{
    Console.WriteLine("""
        Usage: dotnet run --project src/Sim -- <command>
          data     load and validate data/*.json
          assets   check game/assets/manifest.json and list AI placeholders
        """);
    return command == "help" ? 0 : 1;
}

static class RepoRoot
{
    /// <summary>The folder holding EternalDungeon.sln, searched upward from the current and the tool's folder.</summary>
    public static string Find()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "EternalDungeon.sln")))
                    return dir.FullName;
        throw new InvalidOperationException("Can't find the repo root (EternalDungeon.sln).");
    }
}
