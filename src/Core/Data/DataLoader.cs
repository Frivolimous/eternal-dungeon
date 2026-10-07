using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Stats;

namespace EternalDungeon.Core.Data;

/// <summary>Every data table, by name, as read from one place (the JSON files, or TSVs being imported).</summary>
public sealed class DataTables(IReadOnlyDictionary<string, Table> tables)
{
    public Table this[string name] => tables[name];

    /// <summary>Tables in load order.</summary>
    public IEnumerable<Table> All => Schemas.Names.Select(n => tables[n]);

    /// <summary>
    /// Reads every table with <paramref name="read"/>, which gets a schema and returns its table or null when the
    /// file is missing. The units schema is built from the stats and compound stats tables first.
    /// </summary>
    public static DataTables Read(Func<TableSchema, Table?> read, Func<TableSchema, string> fileName)
    {
        var tables = new Dictionary<string, Table>();
        void Get(TableSchema schema) =>
            tables[schema.Name] = read(schema) ?? throw new DataException(fileName(schema), "", "file is missing");
        foreach (var schema in Schemas.BeforeUnits)
            Get(schema);
        Get(Schemas.Units(tables["stats"].Rows.Select(r => r.Str("id")), tables["compound_stats"].Rows.Select(r => r.Str("id"))));
        foreach (var schema in Schemas.AfterUnits)
            Get(schema);
        return new DataTables(tables);
    }

    /// <summary>The JSON data files from <paramref name="source"/>.</summary>
    public static DataTables FromJson(DataSource source) =>
        Read(s => source.Read(s.JsonFile) is { } text ? TableFormat.ReadJson(s, text) : null, s => s.JsonFile);
}

/// <summary>
/// Reads and validates every data table. Any problem throws a <see cref="DataException"/> naming the file, row and
/// column; a successful load means the content is internally consistent.
/// </summary>
public static class DataLoader
{
    /// <summary>Each side's area on the battle grid (Anchor: 3×2 by default).</summary>
    public const int AreaCols = 3, AreaRows = 2;

    /// <summary>AP costs an action may have (Anchor: Combat › Turn order).</summary>
    public static readonly int[] ApCosts = [50, 100, 200];

    /// <summary>The most an AI may lean toward Threat or Vulnerability (Anchor: at most 75% toward one).</summary>
    public const double MaxAiLean = 0.75;

    /// <summary>The keys of the defaults table: which actions are every unit's basic Attack, Defend and Move.</summary>
    public const string AttackKey = "attack_action", DefendKey = "defend_action", MoveKey = "move_action";

    public static GameData LoadDirectory(string directory) => Load(DataSource.FromDirectory(directory));

    public static GameData Load(DataSource source) => Build(DataTables.FromJson(source), Strings.Load(source));

    /// <summary>Validates <paramref name="t"/> and builds the game's content from it.</summary>
    public static GameData Build(DataTables t, Strings? text = null)
    {
        CheckParents(t);

        var tags = t["tags"].Rows.Select(r => new TagDef(r.Str("id"), r.Str("name"), r.Enum<TagGroup>("group"))).ToDictionary(x => x.Id);
        var stats = t["stats"].Rows.Select(ReadStat).ToDictionary(s => s.Id);
        foreach (var r in t["compound_stats"].Rows)
            if (stats.ContainsKey(r.Str("id")))
                throw r.Error("id", $"\"{r.Str("id")}\" is both a stat and a compound stat");
        foreach (var r in t["stats"].Rows.Concat(t["compound_stats"].Rows))
            if (Schemas.UnitFixedColumns.Contains(r.Str("id")))
                throw r.Error("id", $"\"{r.Str("id")}\" is reserved: the units table has a column with that name");
        var compounds = t["compound_stats"].Rows.Select(r => ReadCompound(r, t, tags, stats)).ToList();
        var compoundIds = compounds.Select(c => c.Id).ToHashSet();

        // Effects (buffs) grant procs and procs apply effects, so effects' proc references are checked once both are read.
        var effects = t["effects"].Rows.Select(r => ReadEffect(r, t, tags, stats)).ToList();
        var effectIds = effects.Select(e => e.Id).ToHashSet();
        var procs = t["procs"].Rows.Select(r => ReadProc(r, t, tags, stats, effectIds)).ToList();
        var procIds = procs.Select(p => p.Id).ToHashSet();
        foreach (var r in t["effects"].Rows)
            foreach (var p in r.List("procs"))
                if (!procIds.Contains(p))
                    throw r.Error("procs", $"unknown proc \"{p}\" (not in {t["procs"].File})");

        var actions = t["actions"].Rows.Select(r => ReadAction(r, t, tags, effectIds)).ToDictionary(a => a.Id);
        var ais = t["ai_profiles"].Rows.Select(r => ReadAiProfile(r, t, actions, effectIds)).ToDictionary(a => a.Id);
        var defaultActions = ReadDefaultActions(t, actions);
        var unitDefaults = t["default_stats"].Rows.Select(r => ReadStatEntry(r, tags, stats, t)).ToList();

        var units = new List<UnitDef>();
        foreach (var r in t["units"].Rows)
        {
            var unit = ReadUnit(r, t, tags, stats, compoundIds, actions, ais, procIds);
            var has = unit.Actions.Concat(defaultActions?.All ?? []).ToHashSet();
            if (ais[unit.Ai].Rules.FirstOrDefault(x => !has.Contains(x.Action)) is { } missing)
                throw r.Error("ai", $"its AI \"{unit.Ai}\" uses \"{missing.Action}\", which isn't in its actions or the default actions");
            units.Add(unit);
        }
        var unitsById = units.ToDictionary(u => u.Id);
        var encounters = t["encounters"].Rows.Select(r => ReadEncounter(r, t, unitsById)).ToList();
        return new GameData([.. tags.Values], [.. stats.Values], compounds, units, [.. actions.Values], effects, [.. ais.Values],
            encounters, unitDefaults, procs, defaultActions, text);
    }

    /// <summary>Every child row must point at a row of its parent table.</summary>
    static void CheckParents(DataTables t)
    {
        foreach (var child in t.All.Where(x => x.Schema.Parent is not null))
        {
            var parent = t[child.Schema.Parent!];
            var ids = parent.Rows.Select(r => r.Str("id")).ToHashSet();
            var column = child.Schema.ParentColumn!;
            foreach (var r in child.Rows)
                if (!ids.Contains(r.Str(column)))
                    throw r.Error(column, $"unknown {column} \"{r.Str(column)}\" (not in {parent.File})");
        }
    }

    /// <summary>The rows of <paramref name="child"/> that belong to <paramref name="parentId"/>, in order.</summary>
    static IEnumerable<Row> ChildrenOf(DataTables t, string child, string parentId)
    {
        var table = t[child];
        var rows = table.Rows.Where(r => r.Str(table.Schema.ParentColumn!) == parentId);
        return table.Schema.Ordered ? rows.OrderBy(r => r.Int("order")) : rows;
    }

    static StatDef ReadStat(Row r)
    {
        var stat = new StatDef(r.Str("id"), r.Str("name"), r.Enum<StatGroup>("group"), r.Enum<CombineMode>("combine"),
            r.Bool("integer"), r.Bool("hidden"), r.Num("point_value"));
        if (stat.Integer && stat.Combine != CombineMode.Add)
            throw r.Error("integer", "only stats that combine by add can be integers");
        return stat;
    }

    static CompoundStatDef ReadCompound(Row r, DataTables t, Dictionary<string, TagDef> tags, Dictionary<string, StatDef> stats)
    {
        var rows = new List<CompoundRow>();
        foreach (var x in ChildrenOf(t, "compound_stat_rows", r.Str("id")))
        {
            Tag(x, "tag", tags, t);
            var def = Stat(x, "stat", stats, t);
            if (!def.TagKeyed)
                throw x.Error("stat", $"{def.Name} is a character stat and can't be keyed to a tag");
            if (def.Combine == CombineMode.Mult)
                throw x.Error("stat", $"{def.Name} combines by mult; compound stats can only feed add and dim stats");
            rows.Add(new CompoundRow(x.Str("tag"), def.Id, x.Num("coef")));
        }
        if (rows.Count == 0)
            throw r.Error($"needs at least one row in {t["compound_stat_rows"].File}");
        return new CompoundStatDef(r.Str("id"), r.Str("name"), rows);
    }

    static TagDef Tag(Row r, string column, Dictionary<string, TagDef> tags, DataTables t) =>
        tags.TryGetValue(r.Str(column), out var tag) ? tag : throw r.Error(column, $"unknown tag \"{r.Str(column)}\" (not in {t["tags"].File})");

    static StatDef Stat(Row r, string column, Dictionary<string, StatDef> stats, DataTables t) =>
        stats.TryGetValue(r.Str(column), out var s) ? s : throw r.Error(column, $"unknown stat \"{r.Str(column)}\" (not in {t["stats"].File})");

    /// <summary>A <c>stat, tag, value</c> row, checked against the stat's rules.</summary>
    static StatValue ReadStatEntry(Row r, Dictionary<string, TagDef> tags, Dictionary<string, StatDef> stats, DataTables t)
    {
        var def = Stat(r, "stat", stats, t);
        string? tag = null;
        if (r.Has("tag"))
        {
            tag = Tag(r, "tag", tags, t).Id;
            if (!def.TagKeyed)
                throw r.Error("stat", $"{def.Name} is a character stat and can't be keyed to a tag");
        }
        return new StatValue(def.Id, tag, StatValue(r, "value", def));
    }

    static double StatValue(Row r, string column, StatDef def)
    {
        var value = r.Num(column);
        try { Combine.Validate(def, value); }
        catch (ArgumentException e) { throw r.Error(column, e.Message); }
        return value;
    }

    static List<string> TagList(Row r, string column, Dictionary<string, TagDef> tags, DataTables t)
    {
        var list = new List<string>();
        foreach (var tag in r.List(column))
        {
            if (!tags.ContainsKey(tag))
                throw r.Error(column, $"unknown tag \"{tag}\" (not in {t["tags"].File})");
            if (list.Contains(tag))
                throw r.Error(column, $"tag \"{tag}\" is listed twice");
            list.Add(tag);
        }
        return list;
    }

    static EffectDef ReadEffect(Row r, DataTables t, Dictionary<string, TagDef> tags, Dictionary<string, StatDef> stats)
    {
        var duration = r.Enum<DurationKind>("duration");
        if (duration == DurationKind.Turns && r.Int("turns") < 1)
            throw r.Error("turns", "a buff lasting turns needs at least 1 turn");
        if (duration != DurationKind.Turns && r.Has("turns"))
            throw r.Error("turns", "only a duration of turns has a number of turns");

        var statRows = ChildrenOf(t, "effect_stats", r.Str("id")).ToList();
        var def = new EffectDef(
            r.Str("id"),
            r.Str("name"),
            duration,
            r.Int("turns"),
            r.Bool("stacking"),
            r.OptInt("max_stacks") ?? int.MaxValue,
            [.. statRows.Select(x => ReadStatEntry(x, tags, stats, t))],
            r.Num("heal"),
            r.Num("shield_max_health"),
            r.Int("periodic_damage"),
            r.Int("periodic_heal"),
            r.List("procs"),
            r.Enum<CcKind>("cc"),
            r.Int("stagger"),
            r.Int("delayed_damage"),
            r.Enum<Displace>("displace"));

        if (!def.IsBuff)
        {
            if (statRows.Count > 0)
                throw statRows[0].Error("effect", "only buffs (effects with a duration) can have stats");
            foreach (var buffOnly in new[] { "stacking", "max_stacks", "periodic_damage", "periodic_heal", "procs", "cc", "delayed_damage" })
                if (r.Has(buffOnly))
                    throw r.Error(buffOnly, "only buffs (effects with a duration) can have this");
        }
        else
        {
            foreach (var instantOnly in new[] { "heal", "stagger", "displace" })
                if (r.Has(instantOnly))
                    throw r.Error(instantOnly, "only instant effects (no duration) can have this");
        }
        if (def.Stagger < 0 || def.DelayedDamage < 0) throw r.Error("stagger and delayed damage can't be negative");
        if (def.MaxStacks < 1) throw r.Error("max_stacks", "must be at least 1");
        if (r.Has("max_stacks") && !def.Stacking) throw r.Error("max_stacks", "only stacking buffs have a stack limit");
        if (def.Heal < 0 || def.ShieldMaxHealth < 0 || def.PeriodicDamage < 0 || def.PeriodicHeal < 0)
            throw r.Error("heal, shield and periodic amounts can't be negative");
        return def;
    }

    static readonly ProcTrigger[] HitTriggers =
        [ProcTrigger.Hit, ProcTrigger.Crit, ProcTrigger.Brutal, ProcTrigger.Struck, ProcTrigger.Damaged];

    static ProcDef ReadProc(Row r, DataTables t, Dictionary<string, TagDef> tags, Dictionary<string, StatDef> stats, HashSet<string> effects)
    {
        var trigger = r.Enum<ProcTrigger>("trigger");
        var phase = r.Enum<ProcPhase>("phase");
        var effect = r.OptStr("effect");
        if (effect is not null && !effects.Contains(effect))
            throw r.Error("effect", $"unknown effect \"{effect}\" (not in {t["effects"].File})");

        var proc = new ProcDef(
            r.Str("id"),
            r.Str("name"),
            trigger,
            TagList(r, "trigger_tags", tags, t),
            TagList(r, "tags", tags, t),
            r.Num("chance"),
            r.Enum<ProcTarget>("target"),
            phase,
            r.Enum<Duplicates>("duplicates"),
            r.Num("damage"),
            r.Num("heal"),
            r.Num("shield"),
            r.Num("lifesteal"),
            [.. ChildrenOf(t, "proc_hit_stats", r.Str("id")).Select(x => ReadStatEntry(x, tags, stats, t))],
            effect,
            r.OptNum("owner_health_below"));

        if (proc.Chance <= 0) throw r.Error("chance", "must be above 0");
        if (proc.Damage < 0 || proc.Heal < 0 || proc.Shield < 0 || proc.Lifesteal < 0)
            throw r.Error("damage, heal, shield and lifesteal can't be negative");
        if (!proc.HasAmounts && effect is null)
            throw r.Error("does nothing: give it damage, heal, shield, lifesteal, hit stats or an effect");
        if (phase == ProcPhase.BeforeDamage && trigger != ProcTrigger.Hit)
            throw r.Error("phase", "only hit procs can resolve before damage");
        if (proc.HitStats.Count > 0 && phase != ProcPhase.BeforeDamage)
            throw r.Error("phase", "this-hit stats (proc_hit_stats) need phase before_damage");
        if (proc.Lifesteal > 0 && Array.IndexOf(HitTriggers, trigger) < 0)
            throw r.Error("lifesteal", "lifesteal needs a trigger with a hit (hit, crit, brutal, struck, damaged)");
        if (proc.Target == ProcTarget.Other && trigger is ProcTrigger.TurnStart or ProcTrigger.FightStart)
            throw r.Error("target", "turn start and fight start have no other unit: use self");
        if (proc.Damage > 0 && proc.Target == ProcTarget.Self)
            throw r.Error("damage", "a proc can't damage its own owner");
        return proc;
    }

    static ActionDef ReadAction(Row r, DataTables t, Dictionary<string, TagDef> tags, HashSet<string> effects)
    {
        var actionTags = TagList(r, "tags", tags, t);

        // Heavy and Light are weapon weights, found only on melee actions (Jeremy, 2026-10-04).
        if (actionTags.FirstOrDefault(x => x is "heavy" or "light") is { } weight && !actionTags.Contains("melee"))
            throw r.Error("tags", $"\"{weight}\" is a melee-only tag, so the action must also be tagged melee");

        var target = r.Enum<ActionTarget>("target");
        var range = r.OptEnum<ActionRange>("range");
        if (target is ActionTarget.Enemy or ActionTarget.Ally && range is null)
            throw r.Error("range", $"is required for a {JsonField.SnakeCase(target.ToString())} action");

        var ap = r.Int("ap_cost");
        if (Array.IndexOf(ApCosts, ap) < 0)
            throw r.Error("ap_cost", $"expected one of {string.Join(", ", ApCosts)}, got {ap}");

        var effectRefs = new List<EffectRef>();
        foreach (var e in ChildrenOf(t, "action_effects", r.Str("id")))
        {
            if (!effects.Contains(e.Str("effect")))
                throw e.Error("effect", $"unknown effect \"{e.Str("effect")}\" (not in {t["effects"].File})");
            effectRefs.Add(new EffectRef(e.Str("effect"), e.Enum<EffectAim>("on")));
        }

        var action = new ActionDef(
            r.Str("id"),
            r.Str("name"),
            actionTags,
            target,
            range,
            ap,
            r.Int("mana_cost"),
            r.Num("base_damage"),
            r.Num("all_damage"),
            r.Int("cast_time"),
            effectRefs,
            r.Enum<MoveTo>("move_to"));
        if ((action.Target == ActionTarget.Tile) != (action.MoveTo != MoveTo.None))
            throw r.Error("move_to", "tile actions need move_to (own or enemy), and only they can have it");
        if (action.ManaCost < 0) throw r.Error("mana_cost", "can't be negative");
        if (action.BaseDamage < 0) throw r.Error("base_damage", "can't be negative");
        if (action.CastTime < 0) throw r.Error("cast_time", "can't be negative");
        if (action.DealsDamage && target != ActionTarget.Enemy)
            throw r.Error("base_damage", "only enemy-targeted actions deal damage");
        return action;
    }

    static AiProfileDef ReadAiProfile(Row r, DataTables t, Dictionary<string, ActionDef> actions, HashSet<string> effects)
    {
        var w = r.Num("threat_weight");
        if (w < 1 - MaxAiLean || w > MaxAiLean)
            throw r.Error("threat_weight", $"must be between {1 - MaxAiLean} and {MaxAiLean}, got {w}");
        var rules = new List<AiRule>();
        foreach (var x in ChildrenOf(t, "ai_rules", r.Str("id")))
        {
            if (!actions.TryGetValue(x.Str("action"), out var def))
                throw x.Error("action", $"unknown action \"{x.Str("action")}\" (not in {t["actions"].File})");
            var buff = x.OptStr("missing_buff");
            if (buff is not null && !effects.Contains(buff))
                throw x.Error("missing_buff", $"unknown effect \"{buff}\" (not in {t["effects"].File})");
            if (x.Has("ally_health_below") && def.Target != ActionTarget.Ally)
                throw x.Error("ally_health_below", "only ally-targeted actions can pick an ally by Health");
            var targetBuff = x.OptStr("target_missing_buff");
            if (targetBuff is not null && !effects.Contains(targetBuff))
                throw x.Error("target_missing_buff", $"unknown effect \"{targetBuff}\" (not in {t["effects"].File})");
            var casting = x.Bool("target_casting");
            if ((targetBuff is not null || casting) && def.Target != ActionTarget.Enemy)
                throw x.Error("action", "target conditions need an enemy-targeted action");
            rules.Add(new AiRule(def.Id, x.OptNum("ally_health_below"), x.OptNum("self_health_below"), buff,
                x.Bool("not_intruding"), x.Bool("not_twice_in_a_row"), targetBuff, casting));
        }
        if (rules.Count == 0) throw r.Error($"needs at least one rule in {t["ai_rules"].File}");
        return new AiProfileDef(r.Str("id"), r.Str("name"), w, rules);
    }

    /// <summary>The defaults table's actions: all three of Attack, Defend and Move, or none.</summary>
    static DefaultActions? ReadDefaultActions(DataTables t, Dictionary<string, ActionDef> actions)
    {
        string[] keys = [AttackKey, DefendKey, MoveKey];
        var rows = t["defaults"].Rows;
        if (rows.FirstOrDefault(r => !keys.Contains(r.Str("key"))) is { } unknown)
            throw unknown.Error("key", $"unknown key (allowed: {string.Join(", ", keys)})");
        if (rows.Count == 0) return null;
        if (keys.FirstOrDefault(k => rows.All(r => r.Str("key") != k)) is { } missing)
            throw new DataException(t["defaults"].File, "", $"needs all of {string.Join(", ", keys)} (missing {missing})");

        string Get(string key, Func<ActionDef, bool> fits, string what)
        {
            var row = rows.First(r => r.Str("key") == key);
            var id = row.Str("value");
            if (!actions.TryGetValue(id, out var def))
                throw row.Error("value", $"unknown action \"{id}\" (not in {t["actions"].File})");
            if (!fits(def)) throw row.Error("value", $"the {key.Replace('_', ' ')} must be {what}");
            return def.Id;
        }
        return new DefaultActions(
            Get(AttackKey, a => a.Target == ActionTarget.Enemy && a.DealsDamage, "an enemy-targeted action that deals damage"),
            Get(DefendKey, a => a.Target == ActionTarget.Self, "self-targeted"),
            Get(MoveKey, a => a.MoveTo == MoveTo.Own, "a move within the unit's own area"));
    }

    static UnitDef ReadUnit(Row r, DataTables t, Dictionary<string, TagDef> tags, Dictionary<string, StatDef> stats,
        HashSet<string> compounds, Dictionary<string, ActionDef> actions, Dictionary<string, AiProfileDef> ais, HashSet<string> procs)
    {
        var size = r.Num("size") switch
        {
            1 => UnitSize.Small,
            1.5 => UnitSize.Tall,
            2 => UnitSize.Large,
            var n => throw r.Error("size", $"expected 1, 1.5 or 2, got {n}"),
        };

        var values = new List<StatValue>();
        foreach (var def in stats.Values)
            if (r.Has(def.Id))
                values.Add(new StatValue(def.Id, null, StatValue(r, def.Id, def)));
        foreach (var x in ChildrenOf(t, "unit_tag_stats", r.Str("id")))
            values.Add(ReadStatEntry(x, tags, stats, t));

        var compoundValues = compounds.Where(r.Has).ToDictionary(c => c, r.Num);

        foreach (var id in r.List("actions"))
            if (!actions.ContainsKey(id))
                throw r.Error("actions", $"unknown action \"{id}\" (not in {t["actions"].File})");

        var ai = r.Str("ai");
        if (!ais.ContainsKey(ai))
            throw r.Error("ai", $"unknown AI profile \"{ai}\" (not in {t["ai_profiles"].File})");

        foreach (var id in r.List("procs"))
            if (!procs.Contains(id))
                throw r.Error("procs", $"unknown proc \"{id}\" (not in {t["procs"].File})");

        if (!values.Any(v => v.Stat == "health" && v.Value > 0))
            throw r.Error("health", "a unit needs health above 0");
        return new UnitDef(r.Str("id"), r.Str("name"), size, values, compoundValues, r.List("actions"), ai, r.List("procs"));
    }

    static EncounterDef ReadEncounter(Row r, DataTables t, Dictionary<string, UnitDef> units)
    {
        var rows = ChildrenOf(t, "encounter_units", r.Str("id")).ToList();
        List<Placement> Side(Side side)
        {
            var placements = new List<Placement>();
            var taken = new HashSet<(int, int)>();
            foreach (var p in rows.Where(x => x.Enum<Side>("side") == side).OrderBy(x => x.Int("order")))
            {
                var id = p.Str("unit");
                if (!units.TryGetValue(id, out var unit))
                    throw p.Error("unit", $"unknown unit \"{id}\" (not in {t["units"].File})");
                var row = p.Int("row");
                var col = p.Int("col");
                var (depth, width) = unit.Size switch { UnitSize.Tall => (2, 1), UnitSize.Large => (2, 2), _ => (1, 1) };
                for (var y = row; y < row + depth; y++)
                    for (var x = col; x < col + width; x++)
                    {
                        if (y < 0 || y >= AreaRows || x < 0 || x >= AreaCols)
                            throw p.Error($"{unit.Name} doesn't fit at row {row}, col {col} (the area is {AreaCols} wide, {AreaRows} deep)");
                        if (!taken.Add((y, x)))
                            throw p.Error($"{unit.Name} overlaps another unit at row {y}, col {x}");
                    }
                placements.Add(new Placement(id, row, col));
            }
            if (placements.Count is 0 or > BattleGrid.MaxUnitsPerSide)
                throw r.Error($"each side needs 1 to {BattleGrid.MaxUnitsPerSide} units; the {JsonField.SnakeCase(side.ToString())} side has {placements.Count}");
            return placements;
        }
        return new EncounterDef(r.Str("id"), r.Str("name"), Side(Combat.Side.Party), Side(Combat.Side.Enemy), r.Enum<BoardLayout>("layout"));
    }
}
