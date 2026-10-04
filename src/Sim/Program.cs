using System.Globalization;
using EternalDungeon.Core;
using EternalDungeon.Core.Assets;
using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;

// Command-line tool: battle simulator and data/asset checks. See Help() for usage.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
Console.OutputEncoding = System.Text.Encoding.UTF8;

var command = args.Length > 0 ? args[0] : "help";
var options = Options.Parse(args.Skip(1).ToArray());
var root = RepoRoot.Find();

try
{
    return command switch
    {
        "run" => Run(),
        "batch" => Batch(),
        "encounters" => Encounters(),
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
catch (OptionException e)
{
    Console.Error.WriteLine(e.Message);
    return 2;
}

GameData LoadData() => DataLoader.LoadDirectory(Path.Combine(root, "data"));

EncounterDef EncounterFrom(GameData data)
{
    var id = options.Get("encounter") ?? throw new OptionException("--encounter is required (see: sim encounters)");
    return data.Encounters.TryGetValue(id, out var e)
        ? e
        : throw new OptionException($"Unknown encounter \"{id}\". Known: {string.Join(", ", data.Encounters.Keys)}");
}

int Run()
{
    var data = LoadData();
    var encounter = EncounterFrom(data);
    var seed = options.GetULong("seed", 1);
    var level = options.Get("log-level") switch
    {
        null or "brief" => LogLevel.Brief,
        "full" => LogLevel.Full,
        var other => throw new OptionException($"--log-level must be brief or full, got \"{other}\""),
    };

    var battle = EncounterSetup.Build(data, encounter, seed);
    BattleRunner.Run(battle);
    Console.WriteLine($"{encounter.Name} · seed {seed}");
    Console.WriteLine(Roster(battle));
    Console.WriteLine();
    Console.Write(CombatLog.Write(battle, level));
    return 0;
}

int Batch()
{
    var data = LoadData();
    var encounter = EncounterFrom(data);
    var runs = options.GetInt("runs", 1000);
    var firstSeed = options.GetULong("seed", 1);
    var summary = new BatchSummary();
    for (var i = 0; i < runs; i++)
    {
        var battle = EncounterSetup.Build(data, encounter, firstSeed + (ulong)i);
        BattleRunner.Run(battle);
        summary.Add(battle);
    }
    Console.WriteLine($"{encounter.Name} · {runs} battles · seeds {firstSeed}–{firstSeed + (ulong)runs - 1}");
    Console.Write(summary.Report());
    return 0;
}

int Encounters()
{
    var data = LoadData();
    foreach (var e in data.EncounterList)
        Console.WriteLine($"{e.Id,-16} {e.Name}: {string.Join(", ", e.Enemies.Select(p => data.Units[p.Unit].Name))}");
    return 0;
}

int Data()
{
    var data = LoadData();
    Console.WriteLine($"{GameInfo.Title} {GameInfo.Version}: data OK");
    Console.WriteLine($"  {data.TagList.Count} tags, {data.StatList.Count} stats, {data.CompoundList.Count} compound stats, " +
        $"{data.EffectList.Count} effects, {data.ProcList.Count} procs, {data.ActionList.Count} actions, {data.AiProfileList.Count} AI profiles, " +
        $"{data.UnitList.Count} units, {data.EncounterList.Count} encounters");
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
        Usage: dotnet run --project src/Sim -- <command> [options]
          run --encounter <id> [--seed N] [--log-level brief|full]   one battle with its combat log
          batch --encounter <id> [--runs N] [--seed first]           many seeded battles, then a summary
          encounters                                                 list the encounters
          data                                                       load and validate data/*.json
          assets                                                     check the asset manifest, list AI placeholders
        """);
    return command == "help" ? 0 : 1;
}

static string Roster(Battle battle) =>
    string.Join("\n", battle.Units.GroupBy(u => u.Side).Select(g =>
        $"{(g.Key == Side.Party ? "Party" : "Enemies")}: {string.Join(", ", g.Select(u => $"{u.Name} {u.MaxHealth} HP"))}"));

sealed class OptionException(string message) : Exception(message);

sealed class Options(Dictionary<string, string> values)
{
    public static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) throw new OptionException($"Unexpected argument \"{args[i]}\"");
            if (i + 1 >= args.Length) throw new OptionException($"{args[i]} needs a value");
            values[args[i][2..]] = args[++i];
        }
        return new Options(values);
    }

    public string? Get(string name) => values.GetValueOrDefault(name);

    public int GetInt(string name, int fallback) =>
        Get(name) is not { } v ? fallback
        : int.TryParse(v, out var n) && n > 0 ? n : throw new OptionException($"--{name} must be a positive whole number");

    public ulong GetULong(string name, ulong fallback) =>
        Get(name) is not { } v ? fallback
        : ulong.TryParse(v, out var n) ? n : throw new OptionException($"--{name} must be a whole number");
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
