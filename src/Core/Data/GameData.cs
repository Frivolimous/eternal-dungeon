namespace EternalDungeon.Core.Data;

/// <summary>All content loaded from data files, validated. Lists keep file order; lookups are by id.</summary>
public sealed class GameData(
    IReadOnlyList<TagDef> tagList,
    IReadOnlyList<StatDef> statList,
    IReadOnlyList<CompoundStatDef>? compoundList = null,
    IReadOnlyList<UnitDef>? unitList = null)
{
    public IReadOnlyList<TagDef> TagList { get; } = tagList;
    public IReadOnlyList<StatDef> StatList { get; } = statList;
    public IReadOnlyList<CompoundStatDef> CompoundList { get; } = compoundList ?? [];
    public IReadOnlyList<UnitDef> UnitList { get; } = unitList ?? [];

    public IReadOnlyDictionary<string, TagDef> Tags { get; } = tagList.ToDictionary(t => t.Id);
    public IReadOnlyDictionary<string, StatDef> Stats { get; } = statList.ToDictionary(s => s.Id);
    public IReadOnlyDictionary<string, CompoundStatDef> Compounds { get; } = (compoundList ?? []).ToDictionary(c => c.Id);
    public IReadOnlyDictionary<string, UnitDef> Units { get; } = (unitList ?? []).ToDictionary(u => u.Id);
}
