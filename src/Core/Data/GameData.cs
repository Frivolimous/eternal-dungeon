namespace EternalDungeon.Core.Data;

/// <summary>All content loaded from data files, validated. Lists keep file order; lookups are by id.</summary>
public sealed class GameData(IReadOnlyList<TagDef> tagList, IReadOnlyList<StatDef> statList)
{
    public IReadOnlyList<TagDef> TagList { get; } = tagList;
    public IReadOnlyList<StatDef> StatList { get; } = statList;

    public IReadOnlyDictionary<string, TagDef> Tags { get; } = tagList.ToDictionary(t => t.Id);
    public IReadOnlyDictionary<string, StatDef> Stats { get; } = statList.ToDictionary(s => s.Id);
}
