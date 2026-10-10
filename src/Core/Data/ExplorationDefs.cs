namespace EternalDungeon.Core.Data;

// Content for dungeon runs (Anchor: Exploration, Dungeons, Classes, Equipment): classes and their traits, the preset
// party, belt items, and hand-authored dungeons with their Maps and Nodes. Events are in EventDefs.cs.

/// <summary>Each class layer a hero takes adds one point to its family (Anchor: Equipment › Slots).</summary>
public enum ClassFamily
{
    /// <summary>Higher-tier primary equipment without a penalty.</summary>
    Primary,
    /// <summary>One more belt slot.</summary>
    Belt,
    /// <summary>One more spell slot.</summary>
    Spell,
}

/// <summary>A class: its family and its 3 traits, most central first (a Primary class gives all 3 at level 1).</summary>
public sealed record ClassDef(string Id, string Name, ClassFamily Family, IReadOnlyList<string> Traits);

/// <summary>
/// A hero of the preset party Dungeon 0 starts with (Anchor: Dungeons › Dungeon 0): its Primary class, the unit it
/// fights as, where it stands (front-relative, like an encounter placement), and the belt items it starts with, one
/// per belt slot, each full.
/// </summary>
public sealed record HeroDef(string Id, string Name, string Class, string Unit, int Row, int Col, IReadOnlyList<string> Belt);

/// <summary>
/// A belt item (Anchor: Equipment › Belt items): a consumable whose battle action is <see cref="Action"/>. A belt slot
/// holds up to <see cref="Uses"/> charges of one item; spare charges wait in the party pack. Items usable
/// <see cref="OutsideCombat"/> (potions) apply their action's heal share and Mana share from the Hero Panel. An Alchemist
/// Station sells one charge for <see cref="Price"/> Gold (0: not for sale).
/// </summary>
public sealed record ItemDef(string Id, string Name, string Action, int Uses, bool OutsideCombat, int Price);

/// <summary>
/// Run-wide numbers kept in defaults.json (placeholders: Anchor: Placeholders › Exploration and progression). The
/// defaults here are only used when the data leaves a key out. XP is what a won battle gives, by scale, shared equally
/// among the heroes who finish it standing.
/// </summary>
public sealed record RunRules(
    int MaxStamina = 4,
    double CampRestore = 0.5,
    int ExhaustedSpeed = -10,
    int SevereSpeed = -25,
    double ExhaustedRoll = -0.1,
    double SevereRoll = -0.25,
    int InitiativeModifier = 30,
    int XpSkirmish = 15,
    int XpMajor = 45,
    int XpBoss = 90);

public enum MapKind { Outdoor, Indoor }

/// <summary>What a Node is, for the map and for the rules: where the party arrives, a standard Node, an empty one, a
/// Sanctuary, the Map boss guarding the way on, or the dungeon's final boss.</summary>
public enum NodeType { Start, Standard, Empty, Sanctuary, MapBoss, FinalBoss }

/// <summary>A lasting feature of a Node the party can use (Anchor: Exploration › Interactables). In M3A: the Sanctuary
/// (once: full Health and Mana, and a Rest), the Pathway to the next Map, and the Alchemist Station (buys potions).</summary>
public enum InteractableKind { Sanctuary, Pathway, AlchemistStation }

/// <summary>
/// One Node of a Map: what's drawn (its name and Feature icon, at a position from 0 to 100 across and down the Map),
/// its type, its Event (if any), its Interactables and the Nodes it connects to (both ways).
/// </summary>
public sealed record NodeDef(
    string Id,
    string Map,
    string Name,
    NodeType Type,
    string Feature,
    double X,
    double Y,
    string? Event,
    IReadOnlyList<InteractableKind> Interactables,
    IReadOnlyList<string> Links);

/// <summary>One Map of a dungeon, in order: its Nodes and the one the party arrives at.</summary>
public sealed record MapDef(string Id, string Dungeon, string Name, MapKind Kind, string Start, IReadOnlyList<NodeDef> Nodes)
{
    public NodeDef Node(string id) => Nodes.First(n => n.Id == id);
}

/// <summary>A hand-authored dungeon: its Maps in order and the Camp charges the party gets (Anchor: Exploration › Rest,
/// Camp and Sanctuary).</summary>
public sealed record DungeonDef(string Id, string Name, int CampCharges, IReadOnlyList<MapDef> Maps);

/// <summary>A tree skill (5 levels, bought with skill points) or a mastery (one level, unlocked by the points spent in
/// its class's tree; Anchor: Classes › Skill trees, Masteries).</summary>
public enum SkillKind { Tree, Mastery }

/// <summary>A stat (or compound stat) a skill raises: <see cref="PerLevel"/> × its level. A flag stat (an effect
/// switched on, such as the opening crit) counts as on above 0.</summary>
public sealed record SkillStat(string Stat, string? Tag, double PerLevel);

/// <summary>
/// One skill of a class: a tree skill with its prerequisite (1+ point in it) and tier position, or a mastery with the
/// tree points that unlock it (1, 6, 11). Either can raise stats, grant actions (a mastery's active ability, or a
/// replacement for a default action such as the Rogue's Move) and grant procs.
/// </summary>
public sealed record SkillDef(
    string Id,
    string Name,
    string Class,
    SkillKind Kind,
    int Order,
    int MaxLevel,
    string? Requires,
    int Points,
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> Procs,
    IReadOnlyList<SkillStat> Stats);

/// <summary>A hero level and the total XP it takes to reach it.</summary>
public sealed record LevelDef(int Level, int Xp);
