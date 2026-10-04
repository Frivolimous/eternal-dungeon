namespace EternalDungeon.Core.Data;

/// <summary>All content loaded from data files, validated. Lists keep file order; lookups are by id.</summary>
public sealed class GameData(
    IReadOnlyList<TagDef> tagList,
    IReadOnlyList<StatDef> statList,
    IReadOnlyList<CompoundStatDef>? compoundList = null,
    IReadOnlyList<UnitDef>? unitList = null,
    IReadOnlyList<ActionDef>? actionList = null,
    IReadOnlyList<EffectDef>? effectList = null)
{
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
