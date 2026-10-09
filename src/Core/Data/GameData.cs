namespace EternalDungeon.Core.Data;

/// <summary>All content loaded from data files, validated. Lists keep file order; lookups are by id.</summary>
public sealed class GameData(
    IReadOnlyList<TagDef> tagList,
    IReadOnlyList<StatDef> statList,
    IReadOnlyList<CompoundStatDef>? compoundList = null,
    IReadOnlyList<UnitDef>? unitList = null,
    IReadOnlyList<ActionDef>? actionList = null,
    IReadOnlyList<BuffDef>? buffList = null,
    IReadOnlyList<AiProfileDef>? aiProfileList = null,
    IReadOnlyList<EncounterDef>? encounterList = null,
    IReadOnlyList<StatValue>? unitDefaults = null,
    IReadOnlyList<ProcDef>? procList = null,
    DefaultActions? defaultActions = null,
    Strings? text = null,
    IReadOnlyList<ClassDef>? classList = null,
    IReadOnlyList<HeroDef>? heroList = null,
    IReadOnlyList<ItemDef>? itemList = null,
    IReadOnlyList<DungeonDef>? dungeonList = null,
    RunRules? runRules = null)
{
    /// <summary>Classes, with their family and traits.</summary>
    public IReadOnlyDictionary<string, ClassDef> Classes { get; } = (classList ?? []).ToDictionary(c => c.Id);

    /// <summary>The preset party a new game starts with, in party order.</summary>
    public IReadOnlyList<HeroDef> Heroes { get; } = heroList ?? [];

    public IReadOnlyList<ItemDef> ItemList { get; } = itemList ?? [];
    public IReadOnlyDictionary<string, ItemDef> Items { get; } = (itemList ?? []).ToDictionary(i => i.Id);

    /// <summary>The belt item whose battle action is <paramref name="actionId"/>, or null.</summary>
    public ItemDef? ItemFor(string actionId) => ItemList.FirstOrDefault(i => i.Action == actionId);

    public IReadOnlyList<DungeonDef> DungeonList { get; } = dungeonList ?? [];
    public IReadOnlyDictionary<string, DungeonDef> Dungeons { get; } = (dungeonList ?? []).ToDictionary(d => d.Id);

    /// <summary>Every Node of every dungeon, by id (node ids are unique across Maps).</summary>
    public IReadOnlyDictionary<string, NodeDef> Nodes { get; } =
        (dungeonList ?? []).SelectMany(d => d.Maps).SelectMany(m => m.Nodes).ToDictionary(n => n.Id);

    public RunRules RunRules { get; } = runRules ?? new RunRules();

    /// <summary>Events from data/events/, by id. Empty when only the tables were loaded.</summary>
    public IReadOnlyDictionary<string, EventDef> Events { get; internal set; } = new Dictionary<string, EventDef>();

    /// <summary>The traits (stats in the trait group), in table order.</summary>
    public IEnumerable<StatDef> Traits => StatList.Where(s => s.Group == StatGroup.Trait);

    /// <summary>Every piece of text the game shows (strings.csv).</summary>
    public Strings Text { get; } = text ?? Strings.Parse("keys,en\n");

    /// <summary>The actions every unit has on top of its own, or null when the data defines none.</summary>
    public DefaultActions? DefaultActions { get; } = defaultActions;

    /// <summary>Everything <paramref name="unit"/> can do: its own actions, then the default ones (or its own
    /// replacement for each, such as the Rogue's Move) that it doesn't already list.</summary>
    public IEnumerable<string> ActionsOf(UnitDef unit) =>
        unit.Actions.Where(a => Actions[a].Replaces == DefaultRole.None)
            .Concat(DefaultActions is null ? [] : DefaultRoles.Select(r => DefaultFor(unit, r)!))
            .Distinct();

    static readonly DefaultRole[] DefaultRoles = [DefaultRole.Attack, DefaultRole.Defend, DefaultRole.Move];

    /// <summary>The action <paramref name="unit"/> uses as its <paramref name="role"/> default: its own action that
    /// replaces it, or the shared default. Null when the data defines no defaults.</summary>
    public string? DefaultFor(UnitDef unit, DefaultRole role) =>
        unit.Actions.FirstOrDefault(a => Actions[a].Replaces == role) ?? DefaultActions?.For(role);

    public IReadOnlyList<ProcDef> ProcList { get; } = procList ?? [];
    public IReadOnlyDictionary<string, ProcDef> Procs { get; } = (procList ?? []).ToDictionary(p => p.Id);

    /// <summary>Stats every unit starts with (defaults.json), before its own.</summary>
    public IReadOnlyList<StatValue> UnitDefaults { get; } = unitDefaults ?? [];

    public IReadOnlyList<EncounterDef> EncounterList { get; } = encounterList ?? [];
    public IReadOnlyDictionary<string, EncounterDef> Encounters { get; } = (encounterList ?? []).ToDictionary(e => e.Id);

    public IReadOnlyList<AiProfileDef> AiProfileList { get; } = aiProfileList ?? [];
    public IReadOnlyDictionary<string, AiProfileDef> AiProfiles { get; } = (aiProfileList ?? []).ToDictionary(a => a.Id);

    public IReadOnlyList<BuffDef> BuffList { get; } = buffList ?? [];
    public IReadOnlyDictionary<string, BuffDef> Buffs { get; } = (buffList ?? []).ToDictionary(e => e.Id);

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
