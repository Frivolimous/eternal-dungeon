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

    public static readonly TableSchema Buffs = Top("buffs",
        Enum<DurationKind>("duration", required: true),
        Int("length"),
        Bool("stacking"),
        Int("max_stacks"),
        Number("shield_max_health", @default: 0),
        Int("periodic_damage", @default: 0),
        Int("periodic_heal", @default: 0),
        Enum<CcKind>("cc", @default: CcKind.None),
        Int("delayed_damage", @default: 0),
        Bool("break_on_attack"),
        List("procs"));

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
        Enum<DefaultRole>("replaces", @default: DefaultRole.None));

    public static readonly TableSchema AiProfiles = Top("ai_profiles", Number("threat_weight", required: true));

    public static readonly TableSchema AiRules = Child("ai_rules", "ai_profiles", "profile", ["order"],
        Int("order", required: true),
        Id("action"),
        Number("ally_health_below"),
        Number("self_health_below"),
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
        Enum<Side>("side", required: true), Int("order", required: true), Id("unit"), Int("row", required: true), Int("col", required: true));

    /// <summary>Settings as key/value rows (the keys are listed in <see cref="DataLoader"/>).</summary>
    public static readonly TableSchema Defaults = new("defaults", [Id("key"), Text("value", required: true), Text("note")], ["key"]);

    /// <summary>Stats every unit starts with, before its own.</summary>
    public static readonly TableSchema DefaultStats = new("default_stats", StatEntry, ["stat", "tag"]);

    /// <summary>The tables loaded before units, whose columns depend on the stats and compound stats.</summary>
    public static readonly TableSchema[] BeforeUnits =
        [Tags, Stats, CompoundStats, CompoundStatRows, Buffs, BuffStats, Procs, Actions, AiProfiles, AiRules];

    /// <summary>The tables loaded after units.</summary>
    public static readonly TableSchema[] AfterUnits = [UnitTagStats, Encounters, EncounterUnits, Defaults, DefaultStats];

    /// <summary>All table names, in load order.</summary>
    public static IEnumerable<string> Names => BeforeUnits.Select(s => s.Name).Append("units").Concat(AfterUnits.Select(s => s.Name));
}
