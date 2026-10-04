using EternalDungeon.Core.Stats;

namespace EternalDungeon.Core.Data;

/// <summary>
/// Reads and validates every data file. Any problem throws a <see cref="DataException"/> naming the
/// file and field; a successful load means the content is internally consistent.
/// </summary>
public static class DataLoader
{
    public const string TagsFile = "tags.json";
    public const string StatsFile = "stats.json";
    public const string CompoundStatsFile = "compound_stats.json";
    public const string EffectsFile = "effects.json";
    public const string ProcsFile = "procs.json";
    public const string ActionsFile = "actions.json";
    public const string AiProfilesFile = "ai_profiles.json";
    public const string UnitsFile = "units.json";
    public const string EncountersFile = "encounters.json";
    public const string DefaultsFile = "defaults.json";

    /// <summary>Each side's area on the battle grid (Anchor: 3×2 by default).</summary>
    public const int AreaCols = 3, AreaRows = 2;

    /// <summary>AP costs an action may have (Anchor: Combat › Turn order). 200 works like a cooldown.</summary>
    public static readonly int[] ApCosts = [50, 100, 200];

    public static GameData LoadDirectory(string directory) => Load(DataSource.FromDirectory(directory));

    public static GameData Load(DataSource source)
    {
        var tags = ReadList(source, TagsFile, ReadTag).ToDictionary(t => t.Id);
        var stats = ReadList(source, StatsFile, ReadStat).ToDictionary(s => s.Id);
        var compounds = ReadList(source, CompoundStatsFile, f => ReadCompound(f, tags, stats));
        if (compounds.FirstOrDefault(c => stats.ContainsKey(c.Id)) is { } clash)
            throw new DataException(CompoundStatsFile, "", $"\"{clash.Id}\" is both a stat and a compound stat");
        var compoundIds = compounds.Select(c => c.Id).ToHashSet();

        // Effects (buffs) grant procs and procs apply effects, so each side's references are checked once both are read.
        var procRefs = new List<(JsonField Field, string Proc)>();
        var effects = ReadList(source, EffectsFile, f => ReadEffect(f, tags, stats, procRefs));
        var effectIds = effects.Select(e => e.Id).ToHashSet();
        var procs = ReadList(source, ProcsFile, f => ReadProc(f, tags, stats, effectIds));
        var procIds = procs.Select(p => p.Id).ToHashSet();
        foreach (var (field, proc) in procRefs)
            if (!procIds.Contains(proc))
                throw field.Error($"unknown proc \"{proc}\" (not in {ProcsFile})");

        var actions = ReadList(source, ActionsFile, f => ReadAction(f, tags, effectIds));
        var actionIds = actions.Select(a => a.Id).ToHashSet();
        var actionsById = actions.ToDictionary(a => a.Id);
        var ais = ReadList(source, AiProfilesFile, f => ReadAiProfile(f, actionsById, effectIds));
        var aiIds = ais.Select(a => a.Id).ToHashSet();
        var defaultsText = source.Read(DefaultsFile) ?? throw new DataException(DefaultsFile, "", "file is missing");
        var defaults = JsonField.Parse(DefaultsFile, defaultsText);
        defaults.OnlyFields("unitStats", "defaultActions");
        var unitDefaults = defaults["unitStats"].Items().Select(s => ReadStatEntry(s, tags, stats, tagRequired: false)).ToList();
        var defaultActions = defaults.Optional("defaultActions") is { } da ? ReadDefaultActions(da, actionsById) : null;

        var units = ReadList(source, UnitsFile, f => ReadUnit(f, tags, stats, compoundIds, actionIds, aiIds, procIds));
        foreach (var unit in units)
        {
            var has = unit.Actions.Concat(defaultActions?.All ?? []).ToHashSet();
            if (ais.First(a => a.Id == unit.Ai).Rules.FirstOrDefault(r => !has.Contains(r.Action)) is { } missing)
                throw new DataException(UnitsFile, unit.Id, $"its AI \"{unit.Ai}\" uses \"{missing.Action}\", which isn't in its actions or the default actions");
        }
        var unitsById = units.ToDictionary(u => u.Id);
        var encounters = ReadList(source, EncountersFile, f => ReadEncounter(f, unitsById));
        return new GameData([.. tags.Values], [.. stats.Values], compounds, units, actions, effects, ais, encounters, unitDefaults, procs,
            defaultActions);
    }

    /// <summary>defaults.json's <c>defaultActions</c>: which actions are the basic Attack, Defend and Move.</summary>
    static DefaultActions ReadDefaultActions(JsonField f, Dictionary<string, ActionDef> actions)
    {
        f.OnlyFields("attack", "defend", "move");
        ActionDef Get(string role, Func<ActionDef, bool> fits, string what)
        {
            var id = f[role].Id();
            if (!actions.TryGetValue(id, out var def))
                throw f[role].Error($"unknown action \"{id}\" (not in {ActionsFile})");
            if (!fits(def)) throw f[role].Error($"the {role} action must be {what}");
            return def;
        }
        return new DefaultActions(
            Get("attack", a => a.Target == ActionTarget.Enemy && a.DealsDamage, "an enemy-targeted action that deals damage").Id,
            Get("defend", a => a.Target == ActionTarget.Self, "self-targeted").Id,
            Get("move", a => a.MoveTo == MoveTo.Own, "a move within the unit's own area").Id);
    }

    static EffectDef ReadEffect(JsonField f, Dictionary<string, TagDef> tags, Dictionary<string, StatDef> stats,
        List<(JsonField, string)> procRefs)
    {
        f.OnlyFields("id", "name", "duration", "stacking", "maxStacks", "stats", "heal", "shieldMaxHealth",
            "periodicDamage", "periodicHeal", "procs", "cc", "stagger", "delayedDamage", "displace");

        var duration = DurationKind.Instant;
        var turns = 0;
        if (f.Optional("duration") is { } d)
        {
            if (d.Element.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                if (d.String() != "until_next_turn")
                    throw d.Error($"expected a number of turns or \"until_next_turn\", got \"{d.String()}\"");
                duration = DurationKind.UntilNextTurn;
            }
            else
            {
                duration = DurationKind.Turns;
                turns = d.Int();
                if (turns < 1) throw d.Error("must be at least 1 turn");
            }
        }

        var statValues = new List<StatValue>();
        foreach (var s in f.Optional("stats")?.Items() ?? [])
            statValues.Add(ReadStatEntry(s, tags, stats, tagRequired: false));

        var grantedProcs = new List<string>();
        foreach (var p in f.Optional("procs")?.Items() ?? [])
        {
            procRefs.Add((p, p.Id()));
            grantedProcs.Add(p.Id());
        }

        var def = new EffectDef(
            f["id"].Id(),
            f["name"].String(),
            duration,
            turns,
            f.Optional("stacking")?.Bool() ?? false,
            f.Optional("maxStacks")?.Int() ?? int.MaxValue,
            statValues,
            f.Optional("heal")?.Number() ?? 0,
            f.Optional("shieldMaxHealth")?.Number() ?? 0,
            f.Optional("periodicDamage")?.Int() ?? 0,
            f.Optional("periodicHeal")?.Int() ?? 0,
            grantedProcs,
            f.Optional("cc")?.Enum<CcKind>() ?? CcKind.None,
            f.Optional("stagger")?.Int() ?? 0,
            f.Optional("delayedDamage")?.Int() ?? 0,
            f.Optional("displace")?.Enum<Displace>() ?? Displace.None);

        if (!def.IsBuff)
        {
            foreach (var buffOnly in new[] { "stacking", "maxStacks", "stats", "periodicDamage", "periodicHeal", "procs", "cc", "delayedDamage" })
                if (f.Optional(buffOnly) is { } field)
                    throw field.Error("only buffs (effects with a duration) can have this");
        }
        else
        {
            foreach (var instantOnly in new[] { "heal", "stagger", "displace" })
                if (f.Optional(instantOnly) is { } field)
                    throw field.Error("only instant effects (no duration) can have this");
        }
        if (def.Stagger < 0 || def.DelayedDamage < 0) throw f.Error("stagger and delayed damage can't be negative");
        if (def.MaxStacks < 1) throw f["maxStacks"].Error("must be at least 1");
        if (f.Optional("maxStacks") is { } ms && !def.Stacking) throw ms.Error("only stacking buffs have a stack limit");
        if (def.Heal < 0 || def.ShieldMaxHealth < 0 || def.PeriodicDamage < 0 || def.PeriodicHeal < 0)
            throw f.Error("heal, shield and periodic amounts can't be negative");
        return def;
    }

    /// <summary>One <c>{ "stat", "tag"?, "value" }</c> entry, checked against the stat's rules.</summary>
    static StatValue ReadStatEntry(JsonField f, Dictionary<string, TagDef> tags, Dictionary<string, StatDef> stats, bool tagRequired)
    {
        f.OnlyFields("tag", "stat", "value");
        var stat = f["stat"].Id();
        if (!stats.TryGetValue(stat, out var def))
            throw f["stat"].Error($"unknown stat \"{stat}\" (not in {StatsFile})");
        string? tag = null;
        if (tagRequired || f.Optional("tag") is not null)
        {
            tag = f["tag"].Id();
            if (!tags.ContainsKey(tag))
                throw f["tag"].Error($"unknown tag \"{tag}\" (not in {TagsFile})");
            if (!def.TagKeyed)
                throw f["stat"].Error($"{def.Name} is a character stat and can't be keyed to a tag");
        }
        return new StatValue(stat, tag, ReadStatValue(f["value"], def));
    }

    static readonly ProcTrigger[] HitTriggers =
        [ProcTrigger.Hit, ProcTrigger.Crit, ProcTrigger.Brutal, ProcTrigger.Struck, ProcTrigger.Damaged];

    static ProcDef ReadProc(JsonField f, Dictionary<string, TagDef> tags, Dictionary<string, StatDef> stats, HashSet<string> effects)
    {
        f.OnlyFields("id", "name", "trigger", "triggerTags", "tags", "chance", "target", "phase", "duplicates",
            "damage", "heal", "shield", "lifesteal", "hitStats", "effect", "ownerHealthBelow");

        List<string> TagList(string field)
        {
            var list = new List<string>();
            foreach (var t in f.Optional(field)?.Items() ?? [])
            {
                var tag = t.Id();
                if (!tags.ContainsKey(tag))
                    throw t.Error($"unknown tag \"{tag}\" (not in {TagsFile})");
                list.Add(tag);
            }
            return list;
        }

        var trigger = f["trigger"].Enum<ProcTrigger>();
        var phase = f.Optional("phase")?.Enum<ProcPhase>() ?? ProcPhase.AfterHit;
        var effect = f.Optional("effect")?.Id();
        if (effect is not null && !effects.Contains(effect))
            throw f["effect"].Error($"unknown effect \"{effect}\" (not in {EffectsFile})");

        var proc = new ProcDef(
            f["id"].Id(),
            f["name"].String(),
            trigger,
            TagList("triggerTags"),
            TagList("tags"),
            f.Optional("chance")?.Number() ?? 1,
            f["target"].Enum<ProcTarget>(),
            phase,
            f.Optional("duplicates")?.Enum<Duplicates>() ?? Duplicates.Merge,
            f.Optional("damage")?.Number() ?? 0,
            f.Optional("heal")?.Number() ?? 0,
            f.Optional("shield")?.Number() ?? 0,
            f.Optional("lifesteal")?.Number() ?? 0,
            [.. (f.Optional("hitStats")?.Items() ?? []).Select(s => ReadStatEntry(s, tags, stats, tagRequired: false))],
            effect,
            f.Optional("ownerHealthBelow")?.Number());

        if (proc.Chance <= 0) throw f["chance"].Error("must be above 0");
        if (proc.Damage < 0 || proc.Heal < 0 || proc.Shield < 0 || proc.Lifesteal < 0)
            throw f.Error("damage, heal, shield and lifesteal can't be negative");
        if (!proc.HasAmounts && effect is null)
            throw f.Error("does nothing: give it damage, heal, shield, lifesteal, hitStats or an effect");
        if (phase == ProcPhase.BeforeDamage && trigger != ProcTrigger.Hit)
            throw f["phase"].Error("only hit procs can resolve before damage");
        if (proc.HitStats.Count > 0 && phase != ProcPhase.BeforeDamage)
            throw f["hitStats"].Error("this-hit stats need phase before_damage");
        if (proc.Lifesteal > 0 && Array.IndexOf(HitTriggers, trigger) < 0)
            throw f["lifesteal"].Error("lifesteal needs a trigger with a hit (hit, crit, brutal, struck, damaged)");
        if (proc.Target == ProcTarget.Other && trigger is ProcTrigger.TurnStart or ProcTrigger.FightStart)
            throw f["target"].Error("turn start and fight start have no other unit: use self");
        if (proc.Damage > 0 && proc.Target == ProcTarget.Self)
            throw f["damage"].Error("a proc can't damage its own owner");
        return proc;
    }

    static ActionDef ReadAction(JsonField f, Dictionary<string, TagDef> tags, HashSet<string> effects)
    {
        f.OnlyFields("id", "name", "tags", "target", "range", "apCost", "manaCost", "baseDamage", "allDamage", "castTime", "effects", "moveTo");

        var actionTags = new List<string>();
        foreach (var t in f["tags"].Items())
        {
            var tag = t.Id();
            if (!tags.ContainsKey(tag))
                throw t.Error($"unknown tag \"{tag}\" (not in {TagsFile})");
            if (actionTags.Contains(tag))
                throw t.Error($"tag \"{tag}\" is listed twice");
            actionTags.Add(tag);
        }

        // Heavy and Light are weapon weights, found only on melee actions (Jeremy, 2026-10-04).
        if (actionTags.FirstOrDefault(t => t is "heavy" or "light") is { } weight && !actionTags.Contains("melee"))
            throw f["tags"].Error($"\"{weight}\" is a melee-only tag, so the action must also be tagged melee");

        var target = f["target"].Enum<ActionTarget>();
        var range = f.Optional("range")?.Enum<ActionRange>();
        if (target is ActionTarget.Enemy or ActionTarget.Ally && range is null)
            throw new DataException(f.File, $"{f.Path}.range", $"is required for a {JsonField.SnakeCase(target.ToString())} action");

        var ap = f["apCost"].Int();
        if (Array.IndexOf(ApCosts, ap) < 0)
            throw f["apCost"].Error($"expected one of {string.Join(", ", ApCosts)}, got {ap}");

        var effectRefs = new List<EffectRef>();
        foreach (var e in f.Optional("effects")?.Items() ?? [])
        {
            e.OnlyFields("effect", "on");
            var id = e["effect"].Id();
            if (!effects.Contains(id))
                throw e["effect"].Error($"unknown effect \"{id}\" (not in {EffectsFile})");
            effectRefs.Add(new EffectRef(id, e.Optional("on")?.Enum<EffectAim>() ?? EffectAim.Target));
        }

        var action = new ActionDef(
            f["id"].Id(),
            f["name"].String(),
            actionTags,
            target,
            range,
            ap,
            f.Optional("manaCost")?.Int() ?? 0,
            f.Optional("baseDamage")?.Number() ?? 0,
            f.Optional("allDamage")?.Number() ?? 0,
            f.Optional("castTime")?.Int() ?? 0,
            effectRefs,
            f.Optional("moveTo")?.Enum<MoveTo>() ?? MoveTo.None);
        if ((action.Target == ActionTarget.Tile) != (action.MoveTo != MoveTo.None))
            throw new DataException(f.File, $"{f.Path}.moveTo", "tile actions need moveTo (own or enemy), and only they can have it");
        if (action.ManaCost < 0) throw f["manaCost"].Error("can't be negative");
        if (action.BaseDamage < 0) throw f["baseDamage"].Error("can't be negative");
        if (action.CastTime < 0) throw f["castTime"].Error("can't be negative");
        if (action.DealsDamage && target != ActionTarget.Enemy)
            throw f["baseDamage"].Error("only enemy-targeted actions deal damage");
        return action;
    }

    static EncounterDef ReadEncounter(JsonField f, Dictionary<string, UnitDef> units)
    {
        f.OnlyFields("id", "name", "party", "enemies");
        return new EncounterDef(f["id"].Id(), f["name"].String(), ReadSide(f["party"], units), ReadSide(f["enemies"], units));
    }

    /// <summary>A side's placements: known units, 1–6 of them, each footprint inside the area and not overlapping.</summary>
    static List<Placement> ReadSide(JsonField list, Dictionary<string, UnitDef> units)
    {
        var placements = new List<Placement>();
        var taken = new HashSet<(int, int)>();
        foreach (var p in list.Items())
        {
            p.OnlyFields("unit", "row", "col");
            var id = p["unit"].Id();
            if (!units.TryGetValue(id, out var unit))
                throw p["unit"].Error($"unknown unit \"{id}\" (not in {UnitsFile})");
            var row = p["row"].Int();
            var col = p["col"].Int();
            var (rows, cols) = unit.Size switch { UnitSize.Tall => (2, 1), UnitSize.Large => (2, 2), _ => (1, 1) };
            for (var r = row; r < row + rows; r++)
                for (var c = col; c < col + cols; c++)
                {
                    if (r < 0 || r >= AreaRows || c < 0 || c >= AreaCols)
                        throw p.Error($"{unit.Name} doesn't fit at row {row}, col {col} (the area is {AreaCols} wide, {AreaRows} deep)");
                    if (!taken.Add((r, c)))
                        throw p.Error($"{unit.Name} overlaps another unit at row {r}, col {c}");
                }
            placements.Add(new Placement(id, row, col));
        }
        if (placements.Count is 0 or > 6)
            throw list.Error($"needs 1 to 6 units, got {placements.Count}");
        return placements;
    }

    /// <summary>The most an AI may lean toward Threat or Vulnerability (Anchor: at most 75% toward one).</summary>
    public const double MaxAiLean = 0.75;

    static AiProfileDef ReadAiProfile(JsonField f, Dictionary<string, ActionDef> actions, HashSet<string> effects)
    {
        f.OnlyFields("id", "name", "threatWeight", "rules");
        var w = f["threatWeight"].Number();
        if (w < 1 - MaxAiLean || w > MaxAiLean)
            throw f["threatWeight"].Error($"must be between {1 - MaxAiLean} and {MaxAiLean}, got {w}");
        var rules = new List<AiRule>();
        foreach (var r in f["rules"].Items())
        {
            r.OnlyFields("action", "allyHealthBelow", "selfHealthBelow", "missingBuff", "notIntruding", "notTwiceInARow",
                "targetMissingBuff", "targetCasting");
            var action = r["action"].Id();
            if (!actions.TryGetValue(action, out var def))
                throw r["action"].Error($"unknown action \"{action}\" (not in {ActionsFile})");
            var buff = r.Optional("missingBuff")?.Id();
            if (buff is not null && !effects.Contains(buff))
                throw r["missingBuff"].Error($"unknown effect \"{buff}\" (not in {EffectsFile})");
            if (r.Optional("allyHealthBelow") is { } ally && def.Target != ActionTarget.Ally)
                throw ally.Error("only ally-targeted actions can pick an ally by Health");
            var targetBuff = r.Optional("targetMissingBuff")?.Id();
            if (targetBuff is not null && !effects.Contains(targetBuff))
                throw r["targetMissingBuff"].Error($"unknown effect \"{targetBuff}\" (not in {EffectsFile})");
            var casting = r.Optional("targetCasting")?.Bool() ?? false;
            if ((targetBuff is not null || casting) && def.Target != ActionTarget.Enemy)
                throw r.Error("target conditions need an enemy-targeted action");
            rules.Add(new AiRule(action, r.Optional("allyHealthBelow")?.Number(), r.Optional("selfHealthBelow")?.Number(),
                buff, r.Optional("notIntruding")?.Bool() ?? false, r.Optional("notTwiceInARow")?.Bool() ?? false,
                targetBuff, casting));
        }
        if (rules.Count == 0) throw f["rules"].Error("needs at least one rule");
        return new AiProfileDef(f["id"].Id(), f["name"].String(), w, rules);
    }

    static UnitDef ReadUnit(JsonField f, Dictionary<string, TagDef> tags, Dictionary<string, StatDef> stats,
        HashSet<string> compounds, HashSet<string> actions, HashSet<string> ais, HashSet<string> procs)
    {
        f.OnlyFields("id", "name", "size", "stats", "tagStats", "compounds", "actions", "ai", "procs");

        var size = f["size"];
        var unitSize = size.Number() switch
        {
            1 => UnitSize.Small,
            1.5 => UnitSize.Tall,
            2 => UnitSize.Large,
            var n => throw size.Error($"expected 1, 1.5 or 2, got {n}"),
        };

        var values = new List<StatValue>();
        foreach (var prop in f["stats"].Properties())
        {
            if (!stats.TryGetValue(prop.Name, out var def))
                throw prop.Value.Error($"unknown stat \"{prop.Name}\" (not in {StatsFile})");
            values.Add(new StatValue(def.Id, null, ReadStatValue(prop.Value, def)));
        }
        foreach (var t in f.Optional("tagStats")?.Items() ?? [])
            values.Add(ReadStatEntry(t, tags, stats, tagRequired: true));

        var compoundValues = new Dictionary<string, double>();
        foreach (var prop in f.Optional("compounds")?.Properties() ?? [])
        {
            if (!compounds.Contains(prop.Name))
                throw prop.Value.Error($"unknown compound stat \"{prop.Name}\" (not in {CompoundStatsFile})");
            compoundValues[prop.Name] = prop.Value.Number();
        }

        var actionList = new List<string>();
        foreach (var a in f["actions"].Items())
        {
            var id = a.Id();
            if (!actions.Contains(id))
                throw a.Error($"unknown action \"{id}\" (not in {ActionsFile})");
            actionList.Add(id);
        }

        var ai = f["ai"].Id();
        if (!ais.Contains(ai))
            throw f["ai"].Error($"unknown AI profile \"{ai}\" (not in {AiProfilesFile})");

        var unitProcs = new List<string>();
        foreach (var p in f.Optional("procs")?.Items() ?? [])
        {
            var id = p.Id();
            if (!procs.Contains(id))
                throw p.Error($"unknown proc \"{id}\" (not in {ProcsFile})");
            unitProcs.Add(id);
        }

        var unit = new UnitDef(f["id"].Id(), f["name"].String(), unitSize, values, compoundValues, actionList, ai, unitProcs);
        if (!values.Any(v => v.Stat == "health" && v.Value > 0))
            throw f["stats"].Error("a unit needs health above 0");
        return unit;
    }

    static double ReadStatValue(JsonField f, StatDef def)
    {
        var value = f.Number();
        try { Combine.Validate(def, value); }
        catch (ArgumentException e) { throw f.Error(e.Message); }
        return value;
    }

    /// <summary>Reads a file whose root is an array of entries with unique <c>id</c>s.</summary>
    static List<T> ReadList<T>(DataSource source, string file, Func<JsonField, T> read)
    {
        var text = source.Read(file) ?? throw new DataException(file, "", "file is missing");
        var seen = new HashSet<string>();
        var list = new List<T>();
        foreach (var item in JsonField.Parse(file, text).Items())
        {
            var id = item["id"];
            if (!seen.Add(id.Id()))
                throw id.Error($"duplicate id \"{id.String()}\"");
            list.Add(read(item));
        }
        return list;
    }

    static TagDef ReadTag(JsonField f)
    {
        f.OnlyFields("id", "name", "group");
        return new TagDef(f["id"].Id(), f["name"].String(), f["group"].Enum<TagGroup>());
    }

    static StatDef ReadStat(JsonField f)
    {
        f.OnlyFields("id", "name", "group", "combine", "integer", "hidden");
        var stat = new StatDef(
            f["id"].Id(),
            f["name"].String(),
            f["group"].Enum<StatGroup>(),
            f["combine"].Enum<CombineMode>(),
            f.Optional("integer")?.Bool() ?? false,
            f.Optional("hidden")?.Bool() ?? false);
        if (stat.Integer && stat.Combine != CombineMode.Add)
            throw f["integer"].Error("only stats that combine by add can be integers");
        return stat;
    }

    static CompoundStatDef ReadCompound(JsonField f, Dictionary<string, TagDef> tags, Dictionary<string, StatDef> stats)
    {
        f.OnlyFields("id", "name", "rows");
        var rows = new List<CompoundRow>();
        foreach (var r in f["rows"].Items())
        {
            r.OnlyFields("tag", "stat", "coef");
            var tag = r["tag"].Id();
            if (!tags.ContainsKey(tag))
                throw r["tag"].Error($"unknown tag \"{tag}\" (not in {TagsFile})");
            var stat = r["stat"].Id();
            if (!stats.TryGetValue(stat, out var def))
                throw r["stat"].Error($"unknown stat \"{stat}\" (not in {StatsFile})");
            if (!def.TagKeyed)
                throw r["stat"].Error($"{def.Name} is a character stat and can't be keyed to a tag");
            if (def.Combine == CombineMode.Mult)
                throw r["stat"].Error($"{def.Name} combines by mult; compound stats can only feed add and dim stats");
            rows.Add(new CompoundRow(tag, stat, r["coef"].Number()));
        }
        if (rows.Count == 0)
            throw f["rows"].Error("needs at least one row");
        return new CompoundStatDef(f["id"].Id(), f["name"].String(), rows);
    }
}
