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

        var tags = ReadTags(t);
        var stats = t["stats"].Rows.Select(ReadStat).ToDictionary(s => s.Id);
        foreach (var r in t["compound_stats"].Rows)
            if (stats.ContainsKey(r.Str("id")))
                throw r.Error("id", $"\"{r.Str("id")}\" is both a stat and a compound stat");
        foreach (var r in t["stats"].Rows.Concat(t["compound_stats"].Rows))
            if (Schemas.UnitFixedColumns.Contains(r.Str("id")))
                throw r.Error("id", $"\"{r.Str("id")}\" is reserved: the units table has a column with that name");
        var compounds = t["compound_stats"].Rows.Select(r => ReadCompound(r, t, tags, stats)).ToList();
        var compoundIds = compounds.Select(c => c.Id).ToHashSet();

        // Buffs grant procs and procs apply buffs, so buffs' proc references are checked once both are read.
        var buffs = t["buffs"].Rows.Select(r => ReadBuff(r, t, tags, stats)).ToList();
        var buffIds = buffs.Select(e => e.Id).ToHashSet();
        var procs = t["procs"].Rows.Select(r => ReadProc(r, t, tags, buffIds)).ToList();
        var procIds = procs.Select(p => p.Id).ToHashSet();
        foreach (var r in t["buffs"].Rows)
            foreach (var p in r.List("procs"))
                if (!procIds.Contains(p))
                    throw r.Error("procs", $"unknown proc \"{p}\" (not in {t["procs"].File})");

        var procsById = procs.ToDictionary(p => p.Id);
        var actions = t["actions"].Rows.Select(r => ReadAction(r, t, tags, procsById)).ToDictionary(a => a.Id);
        var ais = t["ai_profiles"].Rows.Select(r => ReadAiProfile(r, t, actions, buffIds)).ToDictionary(a => a.Id);
        var defaultActions = ReadDefaultActions(t, actions);
        var unitDefaults = t["default_stats"].Rows.Select(r => ReadStatEntry(r, tags, stats, t)).ToList();

        var units = new List<UnitDef>();
        foreach (var r in t["units"].Rows)
        {
            var unit = ReadUnit(r, t, tags, stats, compoundIds, actions, ais, procIds);
            // Every unit starts with the default stats, so its own row only adds to them.
            var health = stats["health"].Base + Combine.Total(stats["health"].Combine,
                unitDefaults.Concat(unit.Stats).Where(v => v.Stat == "health" && v.Tag is null).Select(v => v.Value));
            if (health <= 0)
                throw r.Error("health", $"a unit needs health above 0 (its own plus the default stats), got {health}");
            var replaced = unit.Actions.Select(a => actions[a].Replaces).Where(x => x != DefaultRole.None).ToList();
            if (replaced.GroupBy(x => x).FirstOrDefault(g => g.Count() > 1) is { } twice)
                throw r.Error("actions", $"two actions replace the default {JsonField.SnakeCase(twice.Key.ToString())}");
            var has = unit.Actions
                .Concat(defaultActions is null ? [] : new[] { DefaultRole.Attack, DefaultRole.Defend, DefaultRole.Move }
                    .Where(x => !replaced.Contains(x)).Select(defaultActions.For))
                .ToHashSet();
            if (ais[unit.Ai].Rules.FirstOrDefault(x => !has.Contains(x.Action)) is { } missing)
                throw r.Error("ai", $"its AI \"{unit.Ai}\" uses \"{missing.Action}\", which isn't in its actions or the default actions");
            units.Add(unit);
        }
        var unitsById = units.ToDictionary(u => u.Id);
        var encounters = t["encounters"].Rows.Select(r => ReadEncounter(r, t, unitsById)).ToList();
        return new GameData([.. tags.Values], [.. stats.Values], compounds, units, [.. actions.Values], buffs, [.. ais.Values],
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
            r.Bool("integer"), r.Bool("hidden"), r.Num("point_value"), r.Num("base"));
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

    /// <summary>The tags table. An implied tag must exist and imply nothing itself, so implication is one step.</summary>
    static Dictionary<string, TagDef> ReadTags(DataTables t)
    {
        var rows = t["tags"].Rows;
        var ids = rows.Select(r => r.Str("id")).ToHashSet();
        var implying = rows.Where(r => r.List("implies").Count > 0).Select(r => r.Str("id")).ToHashSet();
        var tags = new Dictionary<string, TagDef>();
        foreach (var r in rows)
        {
            var implies = r.List("implies");
            foreach (var x in implies)
            {
                if (!ids.Contains(x)) throw r.Error("implies", $"unknown tag \"{x}\" (not in {t["tags"].File})");
                if (x == r.Str("id")) throw r.Error("implies", "a tag can't imply itself");
                if (implying.Contains(x)) throw r.Error("implies", $"\"{x}\" implies tags itself; implied tags can't imply others");
            }
            tags[r.Str("id")] = new TagDef(r.Str("id"), r.Str("name"), r.Enum<TagGroup>("group"), implies.Count > 0 ? [.. implies] : null);
        }
        return tags;
    }

    /// <summary>A list of tag ids. With <paramref name="withImplied"/>, the tags they imply are added after them
    /// (an action or proc tagged Fire also carries Elemental); filters such as trigger tags leave it off.</summary>
    static List<string> TagList(Row r, string column, Dictionary<string, TagDef> tags, DataTables t, bool withImplied = false)
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
        if (withImplied)
            foreach (var implied in list.ToList().SelectMany(x => tags[x].Implies ?? []))
                if (!list.Contains(implied)) list.Add(implied);
        return list;
    }

    static BuffDef ReadBuff(Row r, DataTables t, Dictionary<string, TagDef> tags, Dictionary<string, StatDef> stats)
    {
        var duration = r.Enum<DurationKind>("duration");
        if (duration is DurationKind.Turns or DurationKind.Actions && r.Int("length") < 1)
            throw r.Error("length", $"a buff lasting {JsonField.SnakeCase(duration.ToString())} needs a length of at least 1");
        if (duration == DurationKind.UntilNextTurn && r.Has("length"))
            throw r.Error("length", "a buff lasting until the next turn has no length");

        var def = new BuffDef(
            r.Str("id"),
            r.Str("name"),
            duration,
            r.Int("length"),
            r.OptInt("max_stacks") ?? 1,
            [.. ChildrenOf(t, "buff_stats", r.Str("id")).Select(x => ReadStatEntry(x, tags, stats, t))],
            0, 0, 0,
            r.List("procs"));
        if (def.MaxStacks < 1) throw r.Error("max_stacks", "must be at least 1 (above 1, the buff stacks)");

        // What it does: key_N names an effect, value_N gives its amount or kind (a flag takes nothing, or true).
        var seen = new HashSet<BuffEffect>();
        for (var i = 1; i <= Schemas.BuffEffects; i++)
        {
            string keyColumn = $"key_{i}", valueColumn = $"value_{i}";
            if (r.OptEnum<BuffEffect>(keyColumn) is not { } key)
            {
                if (r.Has(valueColumn)) throw r.Error(keyColumn, $"{valueColumn} has a value but {keyColumn} is empty");
                continue;
            }
            var name = JsonField.SnakeCase(key.ToString());
            if (!seen.Add(key)) throw r.Error(keyColumn, $"{name} is already one of this buff's effects");
            if (key == BuffEffect.BreakOnAttack)
            {
                if (r.OptStr(valueColumn) is string flag && flag != "true")
                    throw r.Error(valueColumn, $"{name} takes true or nothing, got \"{flag}\"");
                def = def with { BreakOnAttack = true };
                continue;
            }
            var value = r.OptStr(valueColumn) ?? throw r.Error(valueColumn, $"{name} needs a value");
            double Amount()
            {
                if (!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n))
                    throw r.Error(valueColumn, $"{name} needs a number, got \"{value}\"");
                return n >= 0 ? n : throw r.Error(valueColumn, $"{name} can't be negative");
            }
            int Whole() => Amount() is var n && n == Math.Floor(n) ? (int)n : throw r.Error(valueColumn, $"{name} needs a whole number, got \"{value}\"");
            def = key switch
            {
                BuffEffect.Shield => def with { ShieldMaxHealth = Amount() },
                BuffEffect.PeriodicDamage => def with { PeriodicDamage = Whole() },
                BuffEffect.PeriodicHeal => def with { PeriodicHeal = Whole() },
                BuffEffect.DelayedDamage => def with { DelayedDamage = Whole() },
                _ => def with
                {
                    Cc = System.Enum.GetValues<CcKind>().Where(c => c != CcKind.None).FirstOrDefault(c => JsonField.SnakeCase(c.ToString()) == value) is var cc && cc != CcKind.None
                        ? cc
                        : throw r.Error(valueColumn, $"expected one of {string.Join(", ", System.Enum.GetValues<CcKind>().Where(c => c != CcKind.None).Select(c => JsonField.SnakeCase(c.ToString())))}, got \"{value}\""),
                },
            };
        }
        return def;
    }

    static readonly ProcTrigger[] HitTriggers =
        [ProcTrigger.Hit, ProcTrigger.Crit, ProcTrigger.Brutal, ProcTrigger.Struck, ProcTrigger.Damaged];

    static ProcDef ReadProc(Row r, DataTables t, Dictionary<string, TagDef> tags, HashSet<string> buffs)
    {
        var trigger = r.Enum<ProcTrigger>("trigger");
        var phase = r.Enum<ProcPhase>("phase");
        var proc = new ProcDef(
            r.Str("id"),
            r.Str("name"),
            trigger,
            TagList(r, "trigger_tags", tags, t),
            TagList(r, "tags", tags, t, withImplied: true),
            r.Num("chance"),
            r.Enum<ProcTarget>("target"),
            phase,
            r.Enum<Duplicates>("duplicates"),
            r.OptNum("owner_health_below"),
            IgnoreDeval: r.Bool("ignore_deval"));

        // The results: key_N names one, value_N gives its amount, direction or buff.
        var seen = new HashSet<ProcResult>();
        for (var i = 1; i <= Schemas.ProcResults; i++)
        {
            string keyColumn = $"key_{i}", valueColumn = $"value_{i}";
            if (r.OptEnum<ProcResult>(keyColumn) is not { } key)
            {
                if (r.Has(valueColumn)) throw r.Error(keyColumn, $"{valueColumn} has a value but {keyColumn} is empty");
                continue;
            }
            var name = JsonField.SnakeCase(key.ToString());
            if (!seen.Add(key)) throw r.Error(keyColumn, $"{name} is already one of this proc's results");
            if (key == ProcResult.Interrupt)
            {
                if (r.OptStr(valueColumn) is string flag && flag != "true")
                    throw r.Error(valueColumn, $"interrupt takes true or nothing, got \"{flag}\"");
                proc = proc with { Interrupt = true };
                continue;
            }
            var value = r.OptStr(valueColumn) ?? throw r.Error(valueColumn, $"{name} needs a value");
            double Amount()
            {
                if (!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n))
                    throw r.Error(valueColumn, $"{name} needs a number, got \"{value}\"");
                return n > 0 ? n : throw r.Error(valueColumn, $"{name} must be above 0");
            }
            proc = key switch
            {
                ProcResult.Damage => proc with { Damage = Amount() },
                ProcResult.Heal => proc with { Heal = Amount() },
                ProcResult.Shield => proc with { Shield = Amount() },
                ProcResult.Lifesteal => proc with { Lifesteal = Amount() },
                ProcResult.Threat => proc with { Threat = Amount() },
                ProcResult.Stagger => proc with
                {
                    Stagger = Amount() is var s && s == Math.Floor(s) ? (int)s : throw r.Error(valueColumn, $"stagger needs a whole number, got \"{value}\""),
                },
                ProcResult.Displace => proc with
                {
                    Displace = value switch
                    {
                        "push" => Displace.Push,
                        "pull" => Displace.Pull,
                        _ => throw r.Error(valueColumn, $"displace needs push or pull, got \"{value}\""),
                    },
                },
                _ => proc with
                {
                    Buff = buffs.Contains(value) ? value : throw r.Error(valueColumn, $"unknown buff \"{value}\" (not in {t["buffs"].File})"),
                },
            };
        }

        if (proc.Chance <= 0) throw r.Error("chance", "must be above 0");
        if (!proc.DoesSomething)
            throw r.Error("key_1", "the proc does nothing: give it a result (damage, heal, shield, lifesteal, stagger, threat, interrupt, displace or apply_buff)");
        if (phase == ProcPhase.BeforeDamage && trigger != ProcTrigger.Hit)
            throw r.Error("phase", "only hit procs can resolve before damage");
        if (proc.Lifesteal > 0 && Array.IndexOf(HitTriggers, trigger) < 0)
            throw r.Error("trigger", "lifesteal needs a trigger with a hit (hit, crit, brutal, struck, damaged)");
        if (proc.Target == ProcTarget.Other && trigger is ProcTrigger.TurnStart or ProcTrigger.FightStart)
            throw r.Error("target", "turn start and fight start have no other unit: use self");
        if (proc.Damage > 0 && proc.Target == ProcTarget.Self)
            throw r.Error("target", "a proc can't damage its own owner");
        return proc;
    }

    /// <summary>The triggers an action's own procs can use: its events for the actor. Only enemy-targeted actions roll
    /// to hit, so the others have just action complete.</summary>
    static readonly ProcTrigger[] ActionTriggers =
        [ProcTrigger.Hit, ProcTrigger.Miss, ProcTrigger.Crit, ProcTrigger.Brutal, ProcTrigger.ActionComplete];

    static ActionDef ReadAction(Row r, DataTables t, Dictionary<string, TagDef> tags, Dictionary<string, ProcDef> procs)
    {
        var actionTags = TagList(r, "tags", tags, t, withImplied: true);

        // Heavy and Light are weapon weights, found only on melee actions (Jeremy, 2026-10-04).
        if (actionTags.FirstOrDefault(x => x is "heavy" or "light") is { } weight && !actionTags.Contains("melee"))
            throw r.Error("tags", $"\"{weight}\" is a melee-only tag, so the action must also be tagged melee");

        var target = r.Enum<ActionTarget>("target");
        var range = r.OptEnum<ActionRange>("range");
        if (target is ActionTarget.Enemy or ActionTarget.Ally && range is null)
            throw r.Error("range", $"is required for a {JsonField.SnakeCase(target.ToString())} action");

        var ap = r.Int("ap_cost");
        // Any cost on the 100 scale (Anchor: Combat › Turn order); 0 would let a unit act without the clock moving.
        if (ap <= 0)
            throw r.Error("ap_cost", $"must be above 0, got {ap}");

        foreach (var id in r.List("procs"))
        {
            if (!procs.TryGetValue(id, out var p))
                throw r.Error("procs", $"unknown proc \"{id}\" (not in {t["procs"].File})");
            if (target == ActionTarget.Enemy ? Array.IndexOf(ActionTriggers, p.Trigger) < 0 : p.Trigger != ProcTrigger.ActionComplete)
                throw r.Error("procs", $"proc \"{id}\" triggers on {JsonField.SnakeCase(p.Trigger.ToString())}, which this action never sets off "
                    + (target == ActionTarget.Enemy ? "(use hit, miss, crit, brutal or action_complete)" : "(only enemy actions roll to hit: use action_complete)"));
            if (target == ActionTarget.Tile && p.Target == ProcTarget.Other)
                throw r.Error("procs", $"proc \"{id}\" targets the other unit, but a tile action has none: use self");
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
            r.List("procs"),
            r.Enum<MoveTo>("move_to"),
            r.Enum<DefaultRole>("replaces"));
        if ((action.Target == ActionTarget.Tile) != (action.MoveTo != MoveTo.None))
            throw r.Error("move_to", "tile actions need move_to (own or own_or_enemy), and only they can have it");
        if (action.Replaces != DefaultRole.None && !FitsRole(action, action.Replaces, out var what))
            throw r.Error("replaces", $"an action replacing the default {JsonField.SnakeCase(action.Replaces.ToString())} must be {what}");
        if (action.ManaCost < 0) throw r.Error("mana_cost", "can't be negative");
        if (action.BaseDamage < 0) throw r.Error("base_damage", "can't be negative");
        if (action.CastTime < 0) throw r.Error("cast_time", "can't be negative");
        if (action.DealsDamage && target != ActionTarget.Enemy)
            throw r.Error("base_damage", "only enemy-targeted actions deal damage");
        return action;
    }

    static AiProfileDef ReadAiProfile(Row r, DataTables t, Dictionary<string, ActionDef> actions, HashSet<string> buffs)
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
            if (buff is not null && !buffs.Contains(buff))
                throw x.Error("missing_buff", $"unknown buff \"{buff}\" (not in {t["buffs"].File})");
            if (x.Has("ally_health_below") && def.Target is not (ActionTarget.Ally or ActionTarget.Self))
                throw x.Error("ally_health_below", "only ally-targeted actions (which pick that ally) and self actions (used while an ally is that hurt) can check an ally's Health");
            var targetBuff = x.OptStr("target_missing_buff");
            if (targetBuff is not null && !buffs.Contains(targetBuff))
                throw x.Error("target_missing_buff", $"unknown buff \"{targetBuff}\" (not in {t["buffs"].File})");
            var casting = x.Bool("target_casting");
            if ((targetBuff is not null || casting) && def.Target != ActionTarget.Enemy)
                throw x.Error("action", "target conditions need an enemy-targeted action");
            var toEnemyArea = x.Bool("to_enemy_area");
            if (toEnemyArea && def.MoveTo != MoveTo.OwnOrEnemy)
                throw x.Error("to_enemy_area", "needs a move that can enter the enemy area (move_to own_or_enemy)");
            rules.Add(new AiRule(def.Id, x.OptNum("ally_health_below"), x.OptNum("self_health_below"), buff,
                x.Bool("not_intruding"), x.Bool("not_twice_in_a_row"), targetBuff, casting, toEnemyArea));
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

        string Get(string key, DefaultRole role)
        {
            var row = rows.First(r => r.Str("key") == key);
            var id = row.Str("value");
            if (!actions.TryGetValue(id, out var def))
                throw row.Error("value", $"unknown action \"{id}\" (not in {t["actions"].File})");
            if (!FitsRole(def, role, out var what) || (role == DefaultRole.Move && def.MoveTo != MoveTo.Own))
                throw row.Error("value", $"the {key.Replace('_', ' ')} must be {(role == DefaultRole.Move ? "a move within the unit's own area" : what)}");
            if (def.Replaces != DefaultRole.None)
                throw row.Error("value", $"\"{id}\" replaces a default action, so it can't be one");
            return def.Id;
        }
        return new DefaultActions(Get(AttackKey, DefaultRole.Attack), Get(DefendKey, DefaultRole.Defend), Get(MoveKey, DefaultRole.Move));
    }

    /// <summary>Whether an action can serve as the default <paramref name="role"/>, and if not, what it must be.</summary>
    static bool FitsRole(ActionDef a, DefaultRole role, out string what)
    {
        (var fits, what) = role switch
        {
            DefaultRole.Attack => (a.Target == ActionTarget.Enemy && a.DealsDamage, "an enemy-targeted action that deals damage"),
            DefaultRole.Defend => (a.Target == ActionTarget.Self, "self-targeted"),
            DefaultRole.Move => (a.Target == ActionTarget.Tile, "a tile action (a move)"),
            _ => (true, ""),
        };
        return fits;
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
