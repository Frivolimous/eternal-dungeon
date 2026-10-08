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
// One optional positional argument after the command (a folder or file), then --options.
var positional = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : null;
var options = Options.Parse(args.Skip(positional is null ? 1 : 2).ToArray());
var root = RepoRoot.Find();
// Exit code when a sheet sync stops to protect edits that exist on one side only; --force goes ahead.
const int Refused = 3;

try
{
    return command switch
    {
        "run" => Run(),
        "batch" => Batch(),
        "encounters" => Encounters(),
        "data" => Data(),
        "assets" => Assets(),
        "export-tsv" => ExportTsv(),
        "import-tsv" => ImportTsv(),
        "format-data" => FormatData(),
        "replay" => PlayReplay(),
        "art-requests" => ArtRequests(),
        "pull-sheets" => PullSheets(),
        "push-sheets" => PushSheets(),
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
catch (SheetException e)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}

string DataDir() => Path.Combine(root, "data");

GameData LoadData() => DataLoader.LoadDirectory(DataDir());

string Folder(string what) =>
    Path.GetFullPath(positional ?? throw new OptionException($"Give the folder {what}, for example: sim {command} sheets"));

int ExportTsv()
{
    var dir = Folder("to export into");
    var tables = DataTables.FromJson(DataSource.FromDirectory(DataDir()));
    DataLoader.Build(tables);
    Directory.CreateDirectory(dir);
    var files = DataExchange.ExportTsv(tables);
    foreach (var (name, text) in files)
        File.WriteAllText(Path.Combine(dir, name), text);
    Console.WriteLine($"Exported {files.Count} tables to {dir}");
    return 0;
}

int ImportTsv()
{
    var dir = Folder("to import from");
    if (!Directory.Exists(dir)) throw new OptionException($"No folder {dir}");
    var current = DataTables.FromJson(DataSource.FromDirectory(DataDir()));
    var update = DataExchange.ImportTsv(DataSource.FromDirectory(dir), current);   // throws before writing anything
    return Write(update, "Imported");
}

// ---- Google Sheets sync (Core SheetSync; the sheet's side is tools/sheets-sync.gs) ----

// What data/ and the sheet held after the last push or pull, to tell whose edits are whose. Not in git.
string LastSyncDir() => Path.Combine(root, "sheets", "last-sync");

string StringsPath() => Path.Combine(DataDir(), Strings.FileName);

(DataTables Tables, string Strings)? LastSync()
{
    var dir = LastSyncDir();
    if (!File.Exists(Path.Combine(dir, Strings.FileName))) return null;
    try { return (DataTables.FromJson(DataSource.FromDirectory(dir)), File.ReadAllText(Path.Combine(dir, Strings.FileName))); }
    catch (DataException) { return null; }   // saved before the tables changed shape: compare with data/ instead
}

void SaveLastSync()
{
    var dir = LastSyncDir();
    Directory.CreateDirectory(dir);
    foreach (var file in Directory.GetFiles(DataDir(), "*.json").Append(StringsPath()))
        File.Copy(file, Path.Combine(dir, Path.GetFileName(file)), overwrite: true);
}

static void Print(string title, IEnumerable<string> lines)
{
    Console.WriteLine(title);
    foreach (var line in lines)
        Console.WriteLine("  " + line);
}

int PullSheets()
{
    var current = DataTables.FromJson(DataSource.FromDirectory(DataDir()));
    var currentStrings = File.ReadAllText(StringsPath());
    var sheet = GoogleSheet.FromConfig(root);
    Console.WriteLine("Reading the sheet...");
    var tabs = sheet.Read();
    var update = SheetSync.Pull(tabs, current);   // throws before anything is written
    var strings = SheetSync.StringsCsv(tabs)
        ?? throw new DataException("the sheet", "", $"has no \"{SheetSync.StringsTab}\" tab (push-sheets creates the tabs)");

    if (!options.Flag("force") && LastSync() is { } last)
    {
        var local = DataExchange.Changes(last.Tables, current).Concat(SheetSync.StringChanges(last.Strings, currentStrings)).ToList();
        if (local.Count > 0)
        {
            Print("data/ has changes that aren't on the sheet yet, and a pull would undo them:", local);
            Console.WriteLine("Push them first (push-sheets), or pull with --force to drop them.");
            return Refused;
        }
    }
    var result = Write(new DataUpdate(
        new Dictionary<string, string>(update.JsonFiles) { [Strings.FileName] = strings },
        [.. update.Changes, .. SheetSync.StringChanges(currentStrings, strings)]), "Pulled");
    SaveLastSync();
    return result;
}

int PushSheets()
{
    var force = options.Flag("force");
    var tables = DataTables.FromJson(DataSource.FromDirectory(DataDir()));
    var strings = File.ReadAllText(StringsPath());
    DataLoader.Build(tables, Strings.Parse(strings));
    var sheet = GoogleSheet.FromConfig(root);
    Console.WriteLine("Reading the sheet...");
    var tabs = sheet.Read();

    var changes = new List<string>();
    if (!SheetSync.IsBlank(tabs))
    {
        try
        {
            if (!force)
            {
                // Edits made on the sheet since the last sync (with no record of one: anything that differs from data/).
                var (baseTables, baseStrings) = LastSync() ?? (tables, strings);
                var edits = SheetSync.Edits(tabs, baseTables, baseStrings);
                if (edits.Count > 0)
                {
                    Print("The sheet has edits that aren't in data/ yet, and a push would overwrite them:", edits);
                    Console.WriteLine("Pull them first (pull-sheets), or push with --force to overwrite them.");
                    return Refused;
                }
            }
            changes = SheetSync.PushChanges(tabs, tables, strings);
        }
        catch (DataException e) when (!force)
        {
            Console.WriteLine($"The sheet has invalid data, so a push can't tell what it would overwrite: {e.Message}");
            Console.WriteLine("Fix it in the sheet, or push with --force to overwrite it.");
            return Refused;
        }
        catch (DataException e)
        {
            Console.WriteLine($"Overwriting the sheet's invalid data ({e.Message}).");
        }
    }

    var ops = SheetSync.Plan(tabs, tables, strings);
    if (ops.Count > 0)
    {
        Console.WriteLine($"Writing {ops.Count} edit(s) to the sheet...");
        sheet.Apply(ops);
    }
    if (changes.Count > 0) Print("Changed on the sheet:", changes);
    Console.WriteLine(ops.Count == 0 ? "The sheet was already up to date." : "Pushed.");
    SaveLastSync();
    return 0;
}

int PlayReplay()
{
    var file = positional ?? throw new OptionException("Give the replay file, for example: sim replay fight" + Replay.Extension);
    var level = options.Get("log-level") == "full" ? LogLevel.Full : LogLevel.Brief;
    var replay = Replay.Parse(File.ReadAllText(file), Path.GetFileName(file));
    var session = replay.Play(LoadData());
    Console.WriteLine($"{session.Encounter.Name} · seed {replay.Seed} · replay of {replay.Choices.Count} hero decisions");
    Console.WriteLine(Roster(session.Battle));
    Console.WriteLine();
    if (session.Over)
        Console.Write(CombatLog.Write(session.Battle, level));
    else
    {
        foreach (var r in session.Battle.Results)
            foreach (var line in CombatLog.Lines(session.Battle, r, level))
                Console.WriteLine(line);
        Console.WriteLine($"The replay ends here: {session.Awaiting?.Name} is waiting for a decision.");
    }
    return 0;
}

int FormatData()
{
    var tables = DataTables.FromJson(DataSource.FromDirectory(DataDir()));
    DataLoader.Build(tables);
    return Write(new DataUpdate(DataExchange.JsonFiles(tables), []), "Formatted");
}

/// <summary>Writes the JSON files that differ from what's on disk and prints the change summary.</summary>
int Write(DataUpdate update, string verb)
{
    foreach (var line in update.Changes)
        Console.WriteLine("  " + line);
    var written = 0;
    foreach (var (name, text) in update.JsonFiles)
    {
        var path = Path.Combine(DataDir(), name);
        if (File.Exists(path) && File.ReadAllText(path) == text) continue;
        File.WriteAllText(path, text);
        written++;
    }
    Console.WriteLine(written == 0 ? "No changes." : $"{verb}: {written} data file(s) written.");
    return 0;
}

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
        $"{data.BuffList.Count} buffs, {data.ProcList.Count} procs, {data.ActionList.Count} actions, {data.AiProfileList.Count} AI profiles, " +
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
    var missing = new Dictionary<string, int>();
    problems = [.. problems, .. manifest.CheckStyles(dir, ArtCatalog.All(LoadData()), missing)];
    foreach (var style in manifest.Styles)
        Console.WriteLine($"  {(style.AiPlaceholder ? "AI" : "  ")}  style {style.Id,-18} {style.Name}: {missing.GetValueOrDefault(style.Id)} required image(s) missing (placeholders shown)");
    foreach (var p in problems)
        Console.WriteLine($"  !!  {p}");
    if (flagged.Count > 0)
        Console.WriteLine("Every flagged asset must be replaced by human-made art before release.");
    return problems.Count > 0 ? 1 : 0;
}

int ArtRequests()
{
    var path = Path.Combine(root, "docs", "art-requests.md");
    var text = ArtCatalog.Markdown(LoadData());
    File.WriteAllText(path, text);
    Console.WriteLine($"Wrote {path} ({ArtCatalog.All(LoadData()).Count} images)");
    return 0;
}

int Help()
{
    Console.WriteLine("""
        Usage: dotnet run --project src/Sim -- <command> [options]
          run --encounter <id> [--seed N] [--log-level brief|full]   one battle with its combat log
          batch --encounter <id> [--runs N] [--seed first]           many seeded battles, then a summary
          encounters                                                 list the encounters
          data                                                       load and validate data/*.json
          export-tsv <folder>                                        write every data table as a TSV (for spreadsheets)
          import-tsv <folder>                                        validate TSVs, print the changes, write data/*.json
          replay <file> [--log-level brief|full]                     play a replay saved by the game, print its log
          format-data                                                rewrite data/*.json in canonical form
          assets                                                     check the asset manifest and art styles, list AI placeholders
          art-requests                                               write docs/art-requests.md (every image a style needs)
          pull-sheets [--force]                                      read the Google Sheet, validate, print the changes, write data/
          push-sheets [--force]                                      write data/ to the Google Sheet (keeps _ columns and row order)
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
            // A flag (--force) has no value.
            values[args[i][2..]] = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "";
        }
        return new Options(values);
    }

    public string? Get(string name) => values.GetValueOrDefault(name);

    public bool Flag(string name) => values.ContainsKey(name);

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
