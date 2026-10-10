using System.Globalization;

namespace EternalDungeon.Core.Data;

// Reading the tables for dungeon runs: classes, belt items, the preset heroes, run rules, and dungeons with their
// Maps and Nodes. Events are read by EventLoader.
public static partial class DataLoader
{
    /// <summary>The keys of the defaults table that hold run rules (<see cref="RunRules"/>), all optional.</summary>
    public static readonly string[] RunRuleKeys =
        ["max_stamina", "camp_restore", "exhausted_speed", "severe_speed", "exhausted_roll", "severe_roll", "initiative_modifier",
         "xp_skirmish", "xp_major", "xp_boss"];

    static RunRules ReadRunRules(DataTables t)
    {
        var rules = new RunRules();
        foreach (var r in t["defaults"].Rows.Where(r => RunRuleKeys.Contains(r.Str("key"))))
        {
            var key = r.Str("key");
            if (!double.TryParse(r.Str("value"), NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                throw r.Error("value", $"{key} needs a number, got \"{r.Str("value")}\"");
            int Whole() => n == Math.Floor(n) ? (int)n : throw r.Error("value", $"{key} needs a whole number");
            double Share() => n is >= 0 and <= 1 ? n : throw r.Error("value", $"{key} is a share, from 0 to 1");
            double Penalty() => n is >= -1 and <= 0 ? n : throw r.Error("value", $"{key} is a roll penalty, from -1 to 0");
            int Xp() => Whole() is >= 0 and var x ? x : throw r.Error("value", $"{key} can't be negative");
            rules = key switch
            {
                "max_stamina" => rules with { MaxStamina = Whole() is > 0 and var m ? m : throw r.Error("value", "max_stamina must be above 0") },
                "camp_restore" => rules with { CampRestore = Share() },
                "exhausted_speed" => rules with { ExhaustedSpeed = Whole() },
                "severe_speed" => rules with { SevereSpeed = Whole() },
                "exhausted_roll" => rules with { ExhaustedRoll = Penalty() },
                "severe_roll" => rules with { SevereRoll = Penalty() },
                "initiative_modifier" => rules with { InitiativeModifier = Whole() },
                "xp_skirmish" => rules with { XpSkirmish = Xp() },
                "xp_major" => rules with { XpMajor = Xp() },
                _ => rules with { XpBoss = Xp() },
            };
        }
        return rules;
    }

    static ClassDef ReadClass(Row r, DataTables t, Dictionary<string, StatDef> stats)
    {
        var traits = r.List("traits");
        if (traits.Count != 3) throw r.Error("traits", $"a class has 3 traits, got {traits.Count}");
        foreach (var x in traits)
            if (!stats.TryGetValue(x, out var s) || s.Group != StatGroup.Trait)
                throw r.Error("traits", $"\"{x}\" isn't a trait (a stat in the trait group in {t["stats"].File})");
        if (traits.Distinct().Count() != 3) throw r.Error("traits", "a trait is listed twice");
        return new ClassDef(r.Str("id"), r.Str("name"), r.Enum<ClassFamily>("family"), traits);
    }

    static ItemDef ReadItem(Row r, DataTables t, Dictionary<string, ActionDef> actions, Dictionary<string, ProcDef> procs)
    {
        var id = r.Str("action");
        if (!actions.TryGetValue(id, out var action))
            throw r.Error("action", $"unknown action \"{id}\" (not in {t["actions"].File})");
        var item = new ItemDef(r.Str("id"), r.Str("name"), id, r.Int("uses"), r.Bool("outside_combat"), r.Int("price"));
        if (item.Uses < 1) throw r.Error("uses", "must be at least 1");
        if (item.Price < 0) throw r.Error("price", "can't be negative");
        if (item.OutsideCombat && (action.Target != ActionTarget.Self
            || !action.Procs.Select(p => procs[p]).Any(p => p.HealShare > 0 || p.ManaShare > 0)))
            throw r.Error("outside_combat", "only a self action that restores a share of Health or Mana (heal_share, mana_share) works outside combat");
        return item;
    }

    /// <summary>Belt slots a class gives: 1 base, plus 1 for a Belt+ class (Anchor: Equipment › Slots).</summary>
    public static int BeltSlots(ClassDef primary) => 1 + (primary.Family == ClassFamily.Belt ? 1 : 0);

    static List<HeroDef> ReadHeroes(DataTables t, Dictionary<string, ClassDef> classes, Dictionary<string, UnitDef> units, List<ItemDef> items)
    {
        var heroes = new List<HeroDef>();
        var taken = new HashSet<(int, int)>();
        foreach (var r in t["heroes"].Rows)
        {
            if (!classes.TryGetValue(r.Str("class"), out var cls))
                throw r.Error("class", $"unknown class \"{r.Str("class")}\" (not in {t["classes"].File})");
            if (!units.TryGetValue(r.Str("unit"), out var unit))
                throw r.Error("unit", $"unknown unit \"{r.Str("unit")}\" (not in {t["units"].File})");
            if (unit.Size != UnitSize.Small) throw r.Error("unit", "heroes are always Small");
            int row = r.Int("row"), col = r.Int("col");
            if (row is < 0 or >= AreaRows || col is < 0 or >= AreaCols)
                throw r.Error("row", $"row {row}, col {col} is outside the party's area ({AreaCols} wide, {AreaRows} deep)");
            if (!taken.Add((row, col))) throw r.Error("col", $"another hero stands at row {row}, col {col}");
            var belt = r.List("belt");
            foreach (var i in belt)
                if (items.All(x => x.Id != i)) throw r.Error("belt", $"unknown item \"{i}\" (not in {t["items"].File})");
            if (belt.Count > BeltSlots(cls))
                throw r.Error("belt", $"a {cls.Name} has {BeltSlots(cls)} belt slot(s), got {belt.Count} items");
            heroes.Add(new HeroDef(r.Str("id"), r.Str("name"), cls.Id, unit.Id, row, col, belt));
        }
        if (heroes.Count > Combat.BattleGrid.MaxUnitsPerSide)
            throw new DataException(t["heroes"].File, "", $"at most {Combat.BattleGrid.MaxUnitsPerSide} heroes, got {heroes.Count}");
        return heroes;
    }

    static List<DungeonDef> ReadDungeons(DataTables t)
    {
        var nodeRows = t["nodes"].Rows;
        var nodeMap = new Dictionary<string, string>();
        foreach (var r in nodeRows)
            if (!nodeMap.TryAdd(r.Str("id"), r.Str("map")))
                throw r.Error("id", $"node \"{r.Str("id")}\" is listed twice (node ids are unique across every Map)");

        // Links go both ways: each pair is one row, under either Node.
        var links = nodeRows.ToDictionary(r => r.Str("id"), _ => new List<string>());
        var pairs = new HashSet<(string, string)>();
        foreach (var r in t["node_links"].Rows)
        {
            string from = r.Str("node"), to = r.Str("to");
            if (!nodeMap.TryGetValue(to, out var toMap))
                throw r.Error("to", $"unknown node \"{to}\" (not in {t["nodes"].File})");
            if (from == to) throw r.Error("to", "a node can't link to itself");
            if (toMap != nodeMap[from]) throw r.Error("to", $"\"{from}\" and \"{to}\" are on different Maps");
            var pair = string.CompareOrdinal(from, to) < 0 ? (from, to) : (to, from);
            if (!pairs.Add(pair)) throw r.Error("to", $"\"{from}\" and \"{to}\" are already linked");
            links[from].Add(to);
            links[to].Add(from);
        }

        var mapIds = new HashSet<string>();
        var dungeons = new List<DungeonDef>();
        foreach (var d in t["dungeons"].Rows)
        {
            var maps = new List<MapDef>();
            foreach (var m in ChildrenOf(t, "maps", d.Str("id")))
            {
                var id = m.Str("id");
                if (!mapIds.Add(id)) throw m.Error("id", $"map \"{id}\" is listed twice");
                var nodes = nodeRows.Where(n => n.Str("map") == id).Select(n => ReadNode(n, links[n.Str("id")])).ToList();
                if (nodes.Count == 0) throw m.Error($"map \"{id}\" has no nodes in {t["nodes"].File}");
                if (nodes.All(n => n.Id != m.Str("start")))
                    throw m.Error("start", $"\"{m.Str("start")}\" isn't a node of this map");
                var map = new MapDef(id, d.Str("id"), m.Str("name"), m.Enum<MapKind>("kind"), m.Str("start"), nodes);
                CheckConnected(m, map);
                maps.Add(map);
            }
            if (maps.Count == 0) throw d.Error($"dungeon \"{d.Str("id")}\" has no maps in {t["maps"].File}");
            for (var i = 0; i < maps.Count; i++)
            {
                var last = i == maps.Count - 1;
                var pathways = maps[i].Nodes.Count(n => n.Interactables.Contains(InteractableKind.Pathway));
                if (!last && pathways == 0)
                    throw d.Error($"map \"{maps[i].Id}\" needs a Pathway (an interactable) to the next map");
                if (last && pathways > 0)
                    throw d.Error($"map \"{maps[i].Id}\" is the last map, so it can't have a Pathway");
            }
            var camps = d.Int("camp_charges");
            if (camps < 0) throw d.Error("camp_charges", "can't be negative");
            dungeons.Add(new DungeonDef(d.Str("id"), d.Str("name"), camps, maps));
        }
        foreach (var r in nodeRows)
            if (!mapIds.Contains(r.Str("map"))) throw r.Error("map", $"unknown map \"{r.Str("map")}\"");
        return dungeons;
    }

    static NodeDef ReadNode(Row r, List<string> links)
    {
        var interactables = new List<InteractableKind>();
        foreach (var x in r.List("interactables"))
        {
            var kind = Enum.GetValues<InteractableKind>().Where(k => JsonField.SnakeCase(k.ToString()) == x).Cast<InteractableKind?>().FirstOrDefault()
                ?? throw r.Error("interactables", $"unknown interactable \"{x}\" (expected {string.Join(", ", Enum.GetNames<InteractableKind>().Select(JsonField.SnakeCase))})");
            if (interactables.Contains(kind)) throw r.Error("interactables", $"\"{x}\" is listed twice");
            interactables.Add(kind);
        }
        double Position(string column) => r.Num(column) is >= 0 and <= 100 and var v ? v : throw r.Error(column, "must be from 0 to 100");
        return new NodeDef(r.Str("id"), r.Str("map"), r.Str("name"), r.Enum<NodeType>("type"), r.Str("feature"),
            Position("x"), Position("y"), r.OptStr("event"), interactables, links);
    }

    /// <summary>Every Node of a Map must be reachable from its start (Anchor: no locked Nodes or paths).</summary>
    static void CheckConnected(Row m, MapDef map)
    {
        var seen = new HashSet<string> { map.Start };
        var queue = new Queue<string>([map.Start]);
        while (queue.TryDequeue(out var id))
            foreach (var next in map.Node(id).Links)
                if (seen.Add(next)) queue.Enqueue(next);
        if (map.Nodes.FirstOrDefault(n => !seen.Contains(n.Id)) is { } lost)
            throw m.Error($"node \"{lost.Id}\" can't be reached from the map's start");
    }

    /// <summary>Mastery thresholds: the tree points that unlock a class's 1st, 2nd and 3rd mastery (Anchor: Masteries).</summary>
    public static readonly int[] MasteryPoints = [1, 6, 11];

    static List<SkillDef> ReadSkills(DataTables t, Dictionary<string, ClassDef> classes, Dictionary<string, ActionDef> actions,
        HashSet<string> procs, Dictionary<string, StatDef> stats, HashSet<string> compounds, Dictionary<string, TagDef> tags)
    {
        var skills = new List<SkillDef>();
        var rows = t["skills"].Rows;
        var ids = rows.ToDictionary(r => r.Str("id"));
        foreach (var r in rows)
        {
            var id = r.Str("id");
            if (!classes.ContainsKey(r.Str("class")))
                throw r.Error("class", $"unknown class \"{r.Str("class")}\" (not in {t["classes"].File})");
            var kind = r.Enum<SkillKind>("kind");
            var requires = r.OptStr("requires");
            var maxLevel = r.Int("max_level");
            var points = r.Int("points");
            if (kind == SkillKind.Tree)
            {
                if (maxLevel < 1) throw r.Error("max_level", "a tree skill needs at least 1 level");
                if (r.Has("points")) throw r.Error("points", "only masteries unlock at a number of tree points");
                if (requires is not null)
                {
                    if (!ids.TryGetValue(requires, out var parent)) throw r.Error("requires", $"unknown skill \"{requires}\"");
                    if (parent.Str("class") != r.Str("class") || parent.Enum<SkillKind>("kind") != SkillKind.Tree)
                        throw r.Error("requires", $"\"{requires}\" isn't a tree skill of the same class");
                    // Prerequisites point to an earlier skill, so they can't loop.
                    if (parent.Int("order") >= r.Int("order"))
                        throw r.Error("requires", $"\"{requires}\" must come earlier in the tree (a lower order)");
                }
            }
            else
            {
                if (r.Has("max_level") && maxLevel != 1) throw r.Error("max_level", "a mastery has one level");
                maxLevel = 1;
                if (requires is not null) throw r.Error("requires", "masteries unlock by tree points, not prerequisites");
                if (!MasteryPoints.Contains(points))
                    throw r.Error("points", $"a mastery unlocks at {string.Join(", ", MasteryPoints)} tree points, got {points}");
            }
            foreach (var a in r.List("actions"))
                if (!actions.ContainsKey(a)) throw r.Error("actions", $"unknown action \"{a}\" (not in {t["actions"].File})");
            foreach (var p in r.List("procs"))
                if (!procs.Contains(p)) throw r.Error("procs", $"unknown proc \"{p}\" (not in {t["procs"].File})");
            var skillStats = new List<SkillStat>();
            foreach (var x in ChildrenOf(t, "skill_stats", id))
            {
                var stat = x.Str("stat");
                string? tag = null;
                if (compounds.Contains(stat))
                {
                    if (x.Has("tag")) throw x.Error("tag", "compound stats aren't keyed to a tag");
                }
                else
                {
                    var def = Stat(x, "stat", stats, t);
                    if (x.Has("tag"))
                    {
                        tag = Tag(x, "tag", tags, t).Id;
                        if (!def.TagKeyed) throw x.Error("stat", $"{def.Name} is a character stat and can't be keyed to a tag");
                    }
                    if (def.Group == StatGroup.Trait) throw x.Error("stat", "skills don't raise traits (traits come from classes)");
                }
                skillStats.Add(new SkillStat(stat, tag, x.Num("per_level")));
            }
            skills.Add(new SkillDef(id, r.Str("name"), r.Str("class"), kind, r.Int("order"), maxLevel, requires, points,
                r.List("actions"), r.List("procs"), skillStats));
        }
        foreach (var cls in classes.Values)
            foreach (var group in skills.Where(s => s.Class == cls.Id && s.Kind == SkillKind.Mastery).GroupBy(s => s.Points))
                if (group.Count() > 1)
                    throw new DataException(t["skills"].File, group.Last().Id, $"{cls.Name} has two masteries at {group.Key} points");
        return skills;
    }

    static List<LevelDef> ReadLevels(DataTables t)
    {
        var levels = t["levels"].Rows.Select(r => new LevelDef(r.Int("level"), r.Int("xp"))).OrderBy(l => l.Level).ToList();
        for (var i = 0; i < levels.Count; i++)
        {
            var row = t["levels"].Rows.First(r => r.Int("level") == levels[i].Level);
            if (levels[i].Level != i + 1) throw row.Error("level", $"levels run 1, 2, 3… without gaps; expected {i + 1}");
            if (i == 0 && levels[i].Xp != 0) throw row.Error("xp", "level 1 takes 0 XP");
            if (i > 0 && levels[i].Xp <= levels[i - 1].Xp) throw row.Error("xp", "each level takes more total XP than the last");
        }
        return levels;
    }
}
