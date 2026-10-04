namespace EternalDungeon.Core.Data;

/// <summary>All content loaded from data files, validated. Lists keep file order; lookups are by id.</summary>
public sealed class GameData(
    IReadOnlyList<TagDef> tagList,
    IReadOnlyList<StatDef> statList,
    IReadOnlyList<CompoundStatDef>? compoundList = null,
    IReadOnlyList<UnitDef>? unitList = null,
    IReadOnlyList<ActionDef>? actionList = null,
    IReadOnlyList<EffectDef>? effectList = null,
    IReadOnlyList<AiProfileDef>? aiProfileList = null,
    IReadOnlyList<EncounterDef>? encounterList = null,
    IReadOnlyList<StatValue>? unitDefaults = null,
    IReadOnlyList<ProcDef>? procList = null)
{
    public IReadOnlyList<ProcDef> ProcList { get; } = procList ?? [];
    public IReadOnlyDictionary<string, ProcDef> Procs { get; } = (procList ?? []).ToDictionary(p => p.Id);

    /// <summary>Stats every unit starts with (defaults.json), before its own.</summary>
    public IReadOnlyList<StatValue> UnitDefaults { get; } = unitDefaults ?? [];

    public IReadOnlyList<EncounterDef> EncounterList { get; } = encounterList ?? [];
    public IReadOnlyDictionary<string, EncounterDef> Encounters { get; } = (encounterList ?? []).ToDictionary(e => e.Id);

    public IReadOnlyList<AiProfileDef> AiProfileList { get; } = aiProfileList ?? [];
    public IReadOnlyDictionary<string, AiProfileDef> AiProfiles { get; } = (aiProfileList ?? []).ToDictionary(a => a.Id);

    public IReadOnlyList<EffectDef> EffectList { get; } = effectList ?? [];
    public IReadOnlyDictionary<string, EffectDef> Effects { get; } = (effectList ?? []).ToDictionary(e => e.Id);

    public IReadOnlyList<ActionDef> ActionList { get; } = actionList ?? [];
    public IReadOnlyDictionary<string, ActionDef> Actions { get; } = (actionList ?? []).ToDictionary(a => a.Id);

    public IReadOnlyList<TagDef> TagList { get; } = tagList;
    public IReadOnlyList<StatDef> StatList { get; } = statList;
    public IReadOnlyList<CompoundStatDef> CompoundList { get; } = compoundList ?? [];
    public IReadOnlyList<UnitDef> UnitList { get; } = unitList ?? [];

    public IReadOnlyDictionary<string, TagDef> Tags { get; } = tagList.ToDictionary(t => t.Id);
    public IReadOnlyDictionary<string, StatDef> Stats { get; } = statList.ToDictionary(s => s.Id);
    public IReadOnlyDictionary<string, CompoundStatDef> Compounds { get; } = (compoundList ?? []).ToDictionary(c => c.Id);
    public IReadOnlyDictionary<string, UnitDef> Units { get; } = (unitList ?? []).ToDictionary(u => u.Id);
}
