namespace EternalDungeon.Core.Data;

// Content definitions read from data/*.json. Anchor: Stat system.

/// <summary>Source: what made the action (Weapon: any weapon attack). Family: a tag carried because another tag
/// implies it (Elemental, from Fire, Electric and Ice).</summary>
public enum TagGroup { DamageType, Delivery, Style, Function, Element, Source, Family }

/// <summary>A label on an action, such as Melee, Fire or Spell. Every action or proc carrying it also carries the
/// tags it <see cref="Implies"/> (Fire implies Elemental).</summary>
public sealed record TagDef(string Id, string Name, TagGroup Group, IReadOnlyList<string>? Implies = null);

/// <summary>How a stat combines when modifiers are added or removed (Anchor: Combine modes).</summary>
public enum CombineMode
{
    /// <summary>Old + New.</summary>
    Add,
    /// <summary>1 − (1 − Old)(1 − New): approaches but never reaches 1.</summary>
    Dim,
    /// <summary>Old × New. Reserved for rare, build-defining effects.</summary>
    Mult,
}

/// <summary>Character stats are untagged; Attack and Defense stats can be keyed to a tag.</summary>
public enum StatGroup { Character, Attack, Defense }

public sealed record StatDef(
    string Id,
    string Name,
    StatGroup Group,
    CombineMode Combine,
    bool Integer,
    bool Hidden,
    double PointValue = 0.01)
{
    // PointValue: what one compound-stat point adds to this stat. Compound points are percentages, so a stat
    // written as a fraction (Hit 0.10, Rate 0.5) gets 0.01 a point; Power, written in points, gets 1.

    /// <summary>Whether the stat can be written as a tag stat, such as "Fire Power 50".</summary>
    public bool TagKeyed => Group != StatGroup.Character;
}

/// <summary>One recipe row: the compound's value × <see cref="Coef"/> goes to <see cref="Tag"/> <see cref="Stat"/>.</summary>
public sealed record CompoundRow(string Tag, string Stat, double Coef);

/// <summary>
/// A friendly stat, such as Strength, that converts into tag stats (Anchor: Stat system › Compound stats).
/// Points are percentages: 10 Strength is 10 Power on a melee action, 10 Accuracy is +0.10 Hit.
/// </summary>
public sealed record CompoundStatDef(string Id, string Name, IReadOnlyList<CompoundRow> Rows);

/// <summary>An enemy's footprint (Anchor: Combat › Battlefield). Heroes are always Small.</summary>
public enum UnitSize
{
    /// <summary>Size 1: one tile (a person).</summary>
    Small,
    /// <summary>Size 1.5: two tiles in a column, front and back (a troll).</summary>
    Tall,
    /// <summary>Size 2: a 2×2 block (a Balrog).</summary>
    Large,
}

/// <summary>A base stat value on a unit definition: untagged when <see cref="Tag"/> is null.</summary>
public sealed record StatValue(string Stat, string? Tag, double Value);

/// <summary>A hero or enemy as content: base stats, tag stats, compound stats, its actions and its AI.</summary>
public sealed record UnitDef(
    string Id,
    string Name,
    UnitSize Size,
    IReadOnlyList<StatValue> Stats,
    IReadOnlyDictionary<string, double> Compounds,
    IReadOnlyList<string> Actions,
    string Ai = "",
    IReadOnlyList<string>? Procs = null);

/// <summary>
/// One action-choice rule: use <see cref="Action"/> if it's usable, has a valid target, and every condition
/// given holds. Rules are tried in order; the first that passes is used.
/// </summary>
public sealed record AiRule(
    string Action,
    double? AllyHealthBelow = null,
    double? SelfHealthBelow = null,
    string? MissingBuff = null,
    bool NotIntruding = false,
    bool NotTwiceInARow = false,
    string? TargetMissingBuff = null,
    bool TargetCasting = false,
    bool ToEnemyArea = false);

/// <summary>
/// The actions every unit has on top of its own (defaults.json): a basic weapon Attack, Defend and Move. Fear
/// allows only Defend and Move (away from the front).
/// </summary>
public sealed record DefaultActions(string Attack, string Defend, string Move)
{
    public IEnumerable<string> All => [Attack, Defend, Move];

    public string For(DefaultRole role) => role switch
    {
        DefaultRole.Attack => Attack,
        DefaultRole.Defend => Defend,
        DefaultRole.Move => Move,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };
}

/// <summary>
/// How a unit picks actions and targets (Anchor: Combat › Enemy targeting). Targets score
/// w × Threat + (1 − w) × Vulnerability, w = <see cref="ThreatWeight"/>, at most 0.75 either way.
/// </summary>
public sealed record AiProfileDef(string Id, string Name, double ThreatWeight, IReadOnlyList<AiRule> Rules);

/// <summary>A unit and where it starts: <see cref="Row"/> 0 is the front; Tall and Large units anchor at their
/// front-left tile.</summary>
public sealed record Placement(string Unit, int Row, int Col);

/// <summary>How the board is shown: party at the bottom and enemies at the top, or party left and enemies right.
/// Presentation only: Core's rules are front-relative and ignore it.</summary>
public enum BoardLayout { Vertical, SideOn }

/// <summary>A fixed battle: the party's formation, the enemies' layout (front-relative rows and columns), and how
/// the board is shown.</summary>
public sealed record EncounterDef(string Id, string Name, IReadOnlyList<Placement> Party, IReadOnlyList<Placement> Enemies,
    BoardLayout Layout = BoardLayout.Vertical);

/// <summary>Who an action is aimed at.</summary>
public enum ActionTarget { Enemy, Ally, Self, Tile }

/// <summary>
/// Which tiles an action can reach (placeholders from the brief): Melee from the front row to the enemy front
/// row, Reach (spear) also the second row, Any is any enemy tile (ranged and spells).
/// </summary>
public enum ActionRange { Melee, Reach, Any }

/// <summary>Where a tile-targeted action moves its user: a neighbouring tile in the area it stands in (Move), or
/// that or any empty tile in the other side's area (the Rogue's Move, with the Stealth mastery).</summary>
public enum MoveTo { None, Own, OwnOrEnemy }

/// <summary>Which default action an action takes the place of for a unit that has it (the Rogue's Move replaces
/// Move). Masteries that modify basic actions use this.</summary>
public enum DefaultRole { None, Attack, Defend, Move }

/// <summary>Who an action's effect lands on: the action's target or the unit acting.</summary>
public enum EffectAim { Target, Self }

/// <summary>An effect an action applies. On an enemy-targeted action it applies only when the action succeeds.</summary>
public sealed record EffectRef(string Effect, EffectAim On);

/// <summary>Something a unit can do on its turn (Anchor: Combat).</summary>
public sealed record ActionDef(
    string Id,
    string Name,
    IReadOnlyList<string> Tags,
    ActionTarget Target,
    ActionRange? Range,
    int ApCost,
    int ManaCost,
    double BaseDamage,
    double AllDamage,
    int CastTime,
    IReadOnlyList<EffectRef> Effects,
    MoveTo MoveTo = MoveTo.None,
    DefaultRole Replaces = DefaultRole.None)
{
    public bool DealsDamage => BaseDamage > 0;
}

/// <summary>How long an effect lasts: no time at all (instant), a number of buff-clock turns, or until the
/// affected unit's next turn starts.</summary>
public enum DurationKind { Instant, Turns, UntilNextTurn }

/// <summary>The event a proc fires on (Anchor: Combat › Procs). Hit, Miss, Crit, Brutal and ActionComplete are the
/// owner's own actions; Struck, Avoided and Damaged are actions against the owner (Damaged: only damage from
/// an action).</summary>
public enum ProcTrigger { Hit, Miss, Crit, Brutal, ActionComplete, Struck, Avoided, Damaged, TurnStart, FightStart }

/// <summary>Who a proc lands on: its owner, or the other unit in the event (the owner's target, or the attacker).</summary>
public enum ProcTarget { Self, Other }

/// <summary>When a Hit proc resolves: after the hit (default), or before its damage, to change that hit.</summary>
public enum ProcPhase { AfterHit, BeforeDamage }

/// <summary>How copies of the same proc on one unit combine (Anchor: Combat › Procs).</summary>
public enum Duplicates
{
    /// <summary>One roll at 1 − Π(1 − chance); amounts weighted by chance so the expected amount is unchanged.</summary>
    Merge,
    /// <summary>Each copy rolls on its own.</summary>
    Separate,
    /// <summary>Only the strongest copy counts.</summary>
    Unique,
}

/// <summary>
/// A proc: when <see cref="Trigger"/> happens (and the event's action carries one of <see cref="TriggerTags"/>, if
/// any), roll <see cref="Chance"/> × (1 + Rate) ÷ (1 + Deval) over <see cref="Tags"/> (at most 100%), then apply its
/// building blocks. Amounts (damage, heal, Shield, lifesteal share, this-hit stats) add only across merged copies; <see cref="Effect"/> (any instant effect or buff, CC included) is a state that
/// never adds up. Proc damage goes through the damage formula with the proc's tags, and is not an action.
/// </summary>
public sealed record ProcDef(
    string Id,
    string Name,
    ProcTrigger Trigger,
    IReadOnlyList<string> TriggerTags,
    IReadOnlyList<string> Tags,
    double Chance,
    ProcTarget Target,
    ProcPhase Phase,
    Duplicates Duplicates,
    double Damage,
    double Heal,
    double Shield,
    double Lifesteal,
    IReadOnlyList<StatValue> HitStats,
    string? Effect,
    double? OwnerHealthBelow)
{
    public bool HasAmounts => Damage > 0 || Heal > 0 || Shield > 0 || Lifesteal > 0 || HitStats.Count > 0;
}

/// <summary>
/// Crowd control a buff puts on a unit (Anchor: Combat › Crowd control). Slow and stat reduction are plain
/// negative stats; damage over time is periodic damage; delayed damage lands when the buff ends.
/// </summary>
public enum CcKind
{
    None,
    /// <summary>Speed × 0. Interrupts a cast.</summary>
    Stun,
    /// <summary>Can't move (actions that target a tile).</summary>
    Root,
    /// <summary>Can't use Spell actions.</summary>
    Silence,
    /// <summary>Skips its turns; any hit wakes it.</summary>
    Sleep,
    /// <summary>Skips its turns (placeholder: later it will flee or only defend).</summary>
    Fear,
    /// <summary>Picks its target at random among every living unit, allies included.</summary>
    Confusion,
}

/// <summary>Forced movement on the battle grid.</summary>
public enum Displace { None, Push, Pull }

/// <summary>
/// An effect or buff (Anchor: Combat › Buffs and effects). An instant effect heals or shields once. A buff
/// (any other duration) adds stat modifiers while it lasts, can carry a Shield that goes when it ends, can
/// deal damage or heal on every buff-clock turn, and can grant procs while it lasts. A buff is unique per source (action +
/// caster) unless <see cref="Stacking"/>.
/// </summary>
public sealed record EffectDef(
    string Id,
    string Name,
    DurationKind Duration,
    int Turns,
    bool Stacking,
    int MaxStacks,
    IReadOnlyList<StatValue> Stats,
    double Heal,
    double ShieldMaxHealth,
    int PeriodicDamage,
    int PeriodicHeal,
    IReadOnlyList<string> Procs,
    CcKind Cc = CcKind.None,
    int Stagger = 0,
    int DelayedDamage = 0,
    Displace Displace = Displace.None,
    bool BreakOnAttack = false)
{
    public bool IsBuff => Duration != DurationKind.Instant;

    // BreakOnAttack: the buff ends when its holder uses an enemy-targeted action, hit or miss (Stealth).
}
