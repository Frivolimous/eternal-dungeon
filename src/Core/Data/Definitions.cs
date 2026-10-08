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
    double PointValue = 0.01,
    double Base = 0)
{
    // PointValue: what one compound-stat point adds to this stat. Compound points are percentages, so a stat
    // written as a fraction (Hit 0.10, Rate 0.5) gets 0.01 a point; Power, written in points, gets 1.
    // Base: added after the modifiers combine, wherever the stat is read. Hit has Base 1: its modifiers start
    // from 0 and combine as usual (Dim), and the result is offset, so every unit hits 100% by default.

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
    IReadOnlyList<string> Procs,
    MoveTo MoveTo = MoveTo.None,
    DefaultRole Replaces = DefaultRole.None)
{
    public bool DealsDamage => BaseDamage > 0;

    // Procs: what the action does besides its own damage. They fire for the actor on this action's events only
    // (hit, miss, crit, Brutal, action complete) and roll like any proc.
}

/// <summary>How long a buff lasts: until the holder's next turn starts, a number of buff-clock turns, or a number
/// of the holder's own actions (a buff applied before damage with 1 action lasts just that hit).</summary>
public enum DurationKind { UntilNextTurn, Turns, Actions }

/// <summary>What a proc does, one result per key/value pair (Anchor: Combat › Procs). Damage, heal, Shield,
/// lifesteal and stagger are amounts, which add up when copies merge; displace, interrupt and apply buff are states.</summary>
public enum ProcResult { Damage, Heal, Shield, Lifesteal, Stagger, Interrupt, Displace, ApplyBuff }

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
/// any), roll <see cref="Chance"/> × (1 + Rate) ÷ (1 + Deval) over <see cref="Tags"/> (at most 100%; no Deval when
/// <see cref="IgnoreDeval"/>), then apply its results. Amounts (damage, heal, Shield, lifesteal share, stagger) add
/// only across merged copies; states (<see cref="Displace"/>, <see cref="Interrupt"/>, <see cref="Buff"/>, CC
/// included) never add up. Stagger knocks the target's Act back by its amount × (1 − the target's Force Deval).
/// Proc damage and heals go through the formulas with the proc's own tags, and are not actions. Units, buffs and
/// actions all carry procs.
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
    double? OwnerHealthBelow,
    double Damage = 0,
    double Heal = 0,
    double Shield = 0,
    double Lifesteal = 0,
    int Stagger = 0,
    Displace Displace = Displace.None,
    string? Buff = null,
    bool Interrupt = false,
    bool IgnoreDeval = false)
{
    public bool DoesSomething => Damage > 0 || Heal > 0 || Shield > 0 || Lifesteal > 0 || Stagger > 0 || Interrupt || Displace != Displace.None || Buff is not null;
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
/// A buff (Anchor: Combat › Buffs): a timed bundle on a unit. It adds stat modifiers while it lasts, can carry a
/// Shield that goes when it ends, can deal damage or heal on every buff-clock turn, can put crowd control on its
/// holder, and can grant procs while it lasts. Procs apply buffs. A buff is unique per source (action or proc +
/// caster) unless <see cref="Stacking"/>. <see cref="Length"/> counts turns or actions, by <see cref="Duration"/>.
/// </summary>
public sealed record BuffDef(
    string Id,
    string Name,
    DurationKind Duration,
    int Length,
    bool Stacking,
    int MaxStacks,
    IReadOnlyList<StatValue> Stats,
    double ShieldMaxHealth,
    int PeriodicDamage,
    int PeriodicHeal,
    IReadOnlyList<string> Procs,
    CcKind Cc = CcKind.None,
    int DelayedDamage = 0,
    bool BreakOnAttack = false)
{
    // BreakOnAttack: the buff ends when its holder uses an enemy-targeted action, hit or miss (Stealth).
}
