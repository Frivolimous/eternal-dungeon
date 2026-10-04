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
    public const string ActionsFile = "actions.json";
    public const string UnitsFile = "units.json";

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

        // Triggers point at effects in the same file, so their ids are checked once the whole file is read.
        var triggerRefs = new List<(JsonField Field, string Effect)>();
        var effects = ReadList(source, EffectsFile, f => ReadEffect(f, tags, stats, triggerRefs));
        var effectIds = effects.Select(e => e.Id).ToHashSet();
        foreach (var (field, effect) in triggerRefs)
            if (!effectIds.Contains(effect))
                throw field.Error($"unknown effect \"{effect}\"");

        var actions = ReadList(source, ActionsFile, f => ReadAction(f, tags, effectIds));
        var actionIds = actions.Select(a => a.Id).ToHashSet();
        var units = ReadList(source, UnitsFile, f => ReadUnit(f, tags, stats, compoundIds, actionIds));
        return new GameData([.. tags.Values], [.. stats.Values], compounds, units, actions, effects);
    }

    static EffectDef ReadEffect(JsonField f, Dictionary<string, TagDef> tags, Dictionary<string, StatDef> stats,
        List<(JsonField, string)> triggerRefs)
    {
        f.OnlyFields("id", "name", "duration", "stacking", "maxStacks", "stats", "heal", "shieldMaxHealth",
            "periodicDamage", "periodicHeal", "triggers");

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

        var triggers = new List<TriggerDef>();
        foreach (var t in f.Optional("triggers")?.Items() ?? [])
        {
            t.OnlyFields("on", "effect", "target", "healthBelow");
            var effect = t["effect"].Id();
            triggerRefs.Add((t["effect"], effect));
            triggers.Add(new TriggerDef(
                t["on"].Enum<TriggerOn>(),
                effect,
                t.Optional("target")?.Enum<TriggerTarget>() ?? TriggerTarget.Self,
                t.Optional("healthBelow")?.Number()));
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
            triggers);

        if (!def.IsBuff)
        {
            foreach (var buffOnly in new[] { "stacking", "maxStacks", "stats", "periodicDamage", "periodicHeal", "triggers" })
                if (f.Optional(buffOnly) is { } field)
                    throw field.Error("only buffs (effects with a duration) can have this");
        }
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

    static ActionDef ReadAction(JsonField f, Dictionary<string, TagDef> tags, HashSet<string> effects)
    {
        f.OnlyFields("id", "name", "tags", "target", "range", "apCost", "manaCost", "baseDamage", "allDamage", "castTime", "effects");

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
            effectRefs);
        if (action.ManaCost < 0) throw f["manaCost"].Error("can't be negative");
        if (action.BaseDamage < 0) throw f["baseDamage"].Error("can't be negative");
        if (action.CastTime < 0) throw f["castTime"].Error("can't be negative");
        if (action.DealsDamage && target != ActionTarget.Enemy)
            throw f["baseDamage"].Error("only enemy-targeted actions deal damage");
        return action;
    }

    static UnitDef ReadUnit(JsonField f, Dictionary<string, TagDef> tags, Dictionary<string, StatDef> stats,
        HashSet<string> compounds, HashSet<string> actions)
    {
        f.OnlyFields("id", "name", "size", "stats", "tagStats", "compounds", "actions");

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
        if (actionList.Count == 0)
            throw f["actions"].Error("a unit needs at least one action");

        var unit = new UnitDef(f["id"].Id(), f["name"].String(), unitSize, values, compoundValues, actionList);
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
