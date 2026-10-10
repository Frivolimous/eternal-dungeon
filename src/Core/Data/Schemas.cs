using EternalDungeon.Core.Combat;
using static EternalDungeon.Core.Data.Column;

namespace EternalDungeon.Core.Data;

/// <summary>
/// Every data table and its columns (data/README.md describes them). Each table is one file in data/: a list of
/// flat rows. Lists of records are child tables pointing at their parent's id; plain lists of ids sit in one cell.
/// </summary>
public static class Schemas
{
    static TableSchema Top(string name, params Column[] columns) =>
        new(name, [Id("id"), Text("name", required: true), .. columns, Text("note")], ["id"]);

    static TableSchema Child(string name, string parent, string parentColumn, string[] key, params Column[] columns) =>
        new(name, [Id(parentColumn), .. columns], [parentColumn, .. key], parent, parentColumn);

    static Column[] StatEntry => [Id("stat"), Id("tag", required: false), Number("value", required: true)];

    public static readonly TableSchema Tags = Top("tags", Enum<TagGroup>("group", required: true), List("implies"));

    public static readonly TableSchema Stats = Top("stats",
        Enum<StatGroup>("group", required: true),
        Enum<CombineMode>("combine", required: true),
        Bool("integer"),
        Bool("hidden"),
        Number("point_value", @default: 0.01),
        Number("base", @default: 0));

    public static readonly TableSchema CompoundStats = Top("compound_stats");

    public static readonly TableSchema CompoundStatRows = Child("compound_stat_rows", "compound_stats", "compound", ["tag", "stat"],
        Id("tag"), Id("stat"), Number("coef", required: true));

    /// <summary>How many effect pairs (key_N, value_N) a buff has.</summary>
    public const int BuffEffects = 3;

    public static readonly TableSchema Buffs = Top("buffs",
        [Enum<DurationKind>("duration", required: true),
         Int("length"),
         Int("max_stacks", @default: 1),
         List("procs"),
         .. Enumerable.Range(1, BuffEffects).SelectMany(i => new[] { Enum<BuffEffect>($"key_{i}"), Text($"value_{i}") })]);

    public static readonly TableSchema BuffStats = Child("buff_stats", "buffs", "buff", ["stat", "tag"], StatEntry);

    /// <summary>How many result pairs (key_N, value_N) a proc has.</summary>
    public const int ProcResults = 3;

    public static readonly TableSchema Procs = Top("procs",
        [Enum<ProcTrigger>("trigger", required: true),
         List("trigger_tags"),
         List("tags"),
         Number("chance", @default: 1),
         Enum<ProcTarget>("target", required: true),
         Enum<ProcPhase>("phase", @default: ProcPhase.AfterHit),
         Enum<Duplicates>("duplicates", @default: Duplicates.Merge),
         Number("owner_health_below"),
         Bool("ignore_deval"),
         .. Enumerable.Range(1, ProcResults).SelectMany(i => new[] { Enum<ProcResult>($"key_{i}"), Text($"value_{i}") })]);

    public static readonly TableSchema Actions = Top("actions",
        List("tags"),
        Enum<ActionTarget>("target", required: true),
        Enum<ActionRange>("range"),
        Int("ap_cost", required: true),
        Int("mana_cost", @default: 0),
        Number("base_damage", @default: 0),
        Number("all_damage", @default: 0),
        Int("cast_time", @default: 0),
        List("procs"),
        Enum<MoveTo>("move_to", @default: MoveTo.None),
        Enum<DefaultRole>("replaces", @default: DefaultRole.None),
        Int("stamina_cost", @default: 0));

    /// <summary>Stats an action adds to its user while it resolves (Deadly Precision's Accuracy and Penetrate).</summary>
    public static readonly TableSchema ActionStats = Child("action_stats", "actions", "action", ["stat", "tag"], StatEntry);

    public static readonly TableSchema AiProfiles = Top("ai_profiles", Number("threat_weight", required: true));

    public static readonly TableSchema AiRules = Child("ai_rules", "ai_profiles", "profile", ["order"],
        Int("order", required: true),
        Id("action"),
        Number("ally_health_below"),
        Number("self_health_below"),
        Number("self_mana_below"),
        Id("missing_buff", required: false),
        Bool("not_intruding"),
        Bool("not_twice_in_a_row"),
        Id("target_missing_buff", required: false),
        Bool("target_casting"),
        Bool("to_enemy_area"));

    /// <summary>Fixed columns of the units table; one column per stat and per compound stat follows them.</summary>
    public static readonly string[] UnitFixedColumns = ["id", "name", "size", "actions", "ai", "procs", "note"];

    /// <summary>The units table: id, name and size, then one column per stat (untagged values) and per compound
    /// stat, in the order of their tables, then the unit's actions, AI and procs.</summary>
    public static TableSchema Units(IEnumerable<string> stats, IEnumerable<string> compounds) =>
        new("units",
            [Id("id"), Text("name", required: true), Number("size", required: true),
             .. stats.Select(s => Number(s)), .. compounds.Select(c => Number(c)),
             List("actions"), Id("ai"), List("procs"), Text("note")],
            ["id"]);

    public static readonly TableSchema UnitTagStats = Child("unit_tag_stats", "units", "unit", ["stat", "tag"],
        Id("stat"), Id("tag"), Number("value", required: true));

    public static readonly TableSchema Encounters = Top("encounters", Enum<BoardLayout>("layout", @default: BoardLayout.Vertical));

    public static readonly TableSchema EncounterUnits = Child("encounter_units", "encounters", "encounter", ["side", "order"],
        Enum<Side>("side", required: true), Int("order", required: true), Id("unit"), Int("row", required: true), Int("col", required: true),
        List("skills"));

    /// <summary>Settings as key/value rows (the keys are listed in <see cref="DataLoader"/>).</summary>
    public static readonly TableSchema Defaults = new("defaults", [Id("key"), Text("value", required: true), Text("note")], ["key"]);

    /// <summary>Stats every unit starts with, before its own.</summary>
    public static readonly TableSchema DefaultStats = new("default_stats", StatEntry, ["stat", "tag"]);

    /// <summary>Classes: family and their 3 traits (stats in the trait group), most central first.</summary>
    public static readonly TableSchema Classes = Top("classes", Enum<ClassFamily>("family", required: true), List("traits"));

    /// <summary>Belt items: the battle action, charges per belt slot, use outside combat, Alchemist price.</summary>
    public static readonly TableSchema Items = Top("items",
        Id("action"), Int("uses", required: true), Bool("outside_combat"), Int("price", @default: 0));

    /// <summary>Tree skills and masteries of each class.</summary>
    public static readonly TableSchema Skills = Top("skills",
        Id("class"), Enum<SkillKind>("kind", required: true), Int("order", required: true), Int("max_level", @default: 5),
        Id("requires", required: false), Int("points", @default: 0), List("actions"), List("procs"));

    /// <summary>What a skill raises per level: a stat (optionally tag-keyed) or a compound stat.</summary>
    public static readonly TableSchema SkillStats = Child("skill_stats", "skills", "skill", ["stat", "tag"],
        Id("stat"), Id("tag", required: false), Number("per_level", required: true));

    /// <summary>Hero levels and the total XP each takes.</summary>
    public static readonly TableSchema Levels = new("levels", [Int("level", required: true), Int("xp", required: true), Text("note")], ["level"]);

    /// <summary>The preset party Dungeon 0 starts with.</summary>
    public static readonly TableSchema Heroes = Top("heroes",
        Id("class"), Id("unit"), Int("row", required: true), Int("col", required: true), List("belt"));

    public static readonly TableSchema Dungeons = Top("dungeons", Int("camp_charges", @default: 1));

    public static readonly TableSchema Maps = Child("maps", "dungeons", "dungeon", ["order"],
        Int("order", required: true), Id("id"), Text("name", required: true), Enum<MapKind>("kind", @default: MapKind.Outdoor), Id("start"));

    public static readonly TableSchema Nodes = new("nodes",
        [Id("id"), Id("map"), Text("name", required: true), Enum<NodeType>("type", required: true), Id("feature"),
         Number("x", required: true), Number("y", required: true), Id("event", required: false), List("interactables"), Text("note")],
        ["id"], "maps", "map");

    /// <summary>Connections between Nodes, both ways (one row per pair).</summary>
    public static readonly TableSchema NodeLinks = Child("node_links", "nodes", "node", ["to"], Id("to"));

    /// <summary>The tables loaded before units, whose columns depend on the stats and compound stats.</summary>
    public static readonly TableSchema[] BeforeUnits =
        [Tags, Stats, CompoundStats, CompoundStatRows, Buffs, BuffStats, Procs, Actions, ActionStats, AiProfiles, AiRules, Classes, Items, Skills, SkillStats, Levels];

    /// <summary>The tables loaded after units.</summary>
    public static readonly TableSchema[] AfterUnits =
        [UnitTagStats, Encounters, EncounterUnits, Defaults, DefaultStats, Heroes, Dungeons, Maps, Nodes, NodeLinks];

    /// <summary>All table names, in load order.</summary>
    public static IEnumerable<string> Names => BeforeUnits.Select(s => s.Name).Append("units").Concat(AfterUnits.Select(s => s.Name));
}
