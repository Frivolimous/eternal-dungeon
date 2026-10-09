using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Tests;

/// <summary>The repo data plus a small test dungeon (2 Maps, 4 Events) for the run engine's tests.</summary>
static class RunTestData
{
    /// <summary>Every repo data file (tables, strings, events), by the name a DataSource reads.</summary>
    public static Dictionary<string, string> RepoFiles()
    {
        var dir = TestPaths.DataDir;
        var files = Directory.GetFiles(dir, "*.json").ToDictionary(Path.GetFileName, File.ReadAllText)!;
        files[Strings.FileName] = File.ReadAllText(Path.Combine(dir, Strings.FileName));
        var events = Path.Combine(dir, EventLoader.Folder);
        if (Directory.Exists(events))
            foreach (var f in Directory.GetFiles(events, "*.json"))
                files[$"{EventLoader.Folder}/{Path.GetFileName(f)}"] = File.ReadAllText(f);
        return files!;
    }

    /// <summary>Adds rows to a table file (a JSON list).</summary>
    public static void AddRows(Dictionary<string, string> files, string table, string rows)
    {
        var text = files[table + ".json"].TrimEnd();
        files[table + ".json"] = text == "[]" ? $"[\n{rows}\n]\n" : text[..^1].TrimEnd() + ",\n" + rows + "\n]\n";
    }

    public const string StartEvent = """
        { "id": "t_start", "type": "story", "blocks": [
          { "id": "intro", "type": "story", "choices": [
            { "id": "pick", "conditions": [{ "type": "trait", "trait": "disable" }],
              "roll": { "base": 0.5, "trait": "disable", "per_point": 0.2 }, "success": "loot", "failure": "hurt" },
            { "id": "bash", "conditions": [{ "type": "class", "class": "warrior" }], "success": "scout" },
            { "id": "magic", "conditions": [{ "type": "trait", "trait": "arcana", "min": 2 }], "success": "loot" },
            { "id": "pay", "conditions": [{ "type": "gold", "min": 5 }], "success": "loot" },
            { "id": "later", "success": "wait" } ] },
          { "id": "loot", "type": "reward", "rewards": [{ "type": "gold", "amount": 10 }, { "type": "item", "item": "health_potion" }], "success": "flag" },
          { "id": "flag", "type": "action", "actions": [{ "type": "flag", "key": "looted" }, { "type": "buff", "buff": "t_keen", "target": "all" }] },
          { "id": "hurt", "type": "resource", "resource": "health", "amount": -5, "target": "active" },
          { "id": "scout", "type": "action", "actions": [{ "type": "reveal", "nodes": ["t_c"], "icon": "boss" }] },
          { "id": "wait", "type": "action", "actions": [{ "type": "defer", "resume": "back" }] },
          { "id": "back", "type": "branch", "next": [{ "conditions": [{ "type": "flag", "key": "looted" }], "success": "loot" }, { "success": "intro" }] }
        ] }
        """;

    public const string FightEvent = """
        { "id": "t_fight", "type": "fight", "blocks": [
          { "id": "intro", "type": "story", "success": "fight" },
          { "id": "fight", "type": "combat", "encounter": "t_archer", "scale": "major", "initiative": "first_strike", "success": "won" },
          { "id": "won", "type": "action", "actions": [{ "type": "buff", "buff": "t_weak", "target": "all" }], "success": "after" },
          { "id": "after", "type": "story" }
        ] }
        """;

    public const string BossEvent = """
        { "id": "t_boss", "type": "boss", "blocks": [
          { "id": "fight", "type": "combat", "encounter": "t_brute", "scale": "boss", "success": "done" },
          { "id": "done", "type": "story" }
        ] }
        """;

    public const string EndEvent = """
        { "id": "t_end", "type": "story", "blocks": [
          { "id": "end", "type": "action", "actions": [{ "type": "complete_dungeon" }], "success": "bye" },
          { "id": "bye", "type": "story" }
        ] }
        """;

    const string TestStrings = """
        event.t_start.name,"Test Start"
        event.t_start.intro,"{hero} finds a locked chest."
        event.t_start.intro.pick,"Pick the lock"
        event.t_start.intro.bash,"Smash it"
        event.t_start.intro.magic,"Open it by magic"
        event.t_start.intro.pay,"Pay the keeper"
        event.t_start.intro.later,"Come back later"
        event.t_fight.name,"Test Fight"
        event.t_fight.intro,"An archer!"
        event.t_fight.after,"It's over."
        event.t_boss.name,"Test Boss"
        event.t_boss.done,"The brute falls."
        event.t_end.name,"Test End"
        event.t_end.bye,"The end."

        """;

    /// <summary>The files for the test dungeon, optionally changed before loading.</summary>
    public static Dictionary<string, string> Files()
    {
        var files = RepoFiles();
        AddRows(files, "buffs", """
              { "id": "t_keen", "name": "Keen", "duration": "steps", "length": 2 },
              { "id": "t_weak", "name": "Weak", "duration": "battles", "length": 1 }
            """);
        AddRows(files, "buff_stats", """
              { "buff": "t_keen", "stat": "awareness", "value": 1 },
              { "buff": "t_weak", "stat": "power", "value": -25 }
            """);
        AddRows(files, "encounters", """
              { "id": "t_archer", "name": "Test Archer" },
              { "id": "t_brute", "name": "Test Brute" }
            """);
        AddRows(files, "encounter_units", """
              { "encounter": "t_archer", "side": "enemy", "order": 1, "unit": "goblin_archer", "row": 0, "col": 1 },
              { "encounter": "t_brute", "side": "enemy", "order": 1, "unit": "goblin_brute", "row": 0, "col": 1 }
            """);
        AddRows(files, "dungeons", """  { "id": "t_dungeon", "name": "Test Dungeon" }""");
        AddRows(files, "maps", """
              { "dungeon": "t_dungeon", "order": 1, "id": "t_map1", "name": "Test Map 1", "start": "t_a" },
              { "dungeon": "t_dungeon", "order": 2, "id": "t_map2", "name": "Test Map 2", "start": "t_d" }
            """);
        AddRows(files, "nodes", """
              { "id": "t_a", "map": "t_map1", "name": "Clearing", "type": "start", "feature": "clearing", "x": 10, "y": 50, "event": "t_start" },
              { "id": "t_b", "map": "t_map1", "name": "Copse", "type": "standard", "feature": "trees", "x": 50, "y": 50, "event": "t_fight" },
              { "id": "t_c", "map": "t_map1", "name": "Gate", "type": "map_boss", "feature": "gate", "x": 90, "y": 50, "event": "t_boss", "interactables": ["pathway"] },
              { "id": "t_d", "map": "t_map2", "name": "Chapel", "type": "start", "feature": "chapel", "x": 10, "y": 50, "interactables": ["sanctuary"] },
              { "id": "t_e", "map": "t_map2", "name": "Lair", "type": "final_boss", "feature": "lair", "x": 90, "y": 50, "event": "t_end" }
            """);
        AddRows(files, "node_links", """
              { "node": "t_a", "to": "t_b" },
              { "node": "t_b", "to": "t_c" },
              { "node": "t_d", "to": "t_e" }
            """);
        files["events/t_start.json"] = StartEvent;
        files["events/t_fight.json"] = FightEvent;
        files["events/t_boss.json"] = BossEvent;
        files["events/t_end.json"] = EndEvent;
        files[Strings.FileName] = files[Strings.FileName].TrimEnd('\n') + "\n" + TestStrings;
        return files;
    }

    static GameData? loaded;

    /// <summary>The test dungeon's data, loaded once.</summary>
    public static GameData Data => loaded ??= DataLoader.Load(DataSource.FromFiles(Files()));

    public static GameData Load(Dictionary<string, string> files) => DataLoader.Load(DataSource.FromFiles(files));
}
