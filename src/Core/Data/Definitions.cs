namespace EternalDungeon.Core.Data;

// Content definitions read from data/*.json. Anchor: Stat system.

public enum TagGroup { DamageType, Delivery, Style, Function, Element }

/// <summary>A label on an action, such as Melee, Fire or Spell.</summary>
public sealed record TagDef(string Id, string Name, TagGroup Group);

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
    bool Hidden)
{
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

/// <summary>A hero or enemy as content: base stats, tag stats, compound stats and its actions.</summary>
public sealed record UnitDef(
    string Id,
    string Name,
    UnitSize Size,
    IReadOnlyList<StatValue> Stats,
    IReadOnlyDictionary<string, double> Compounds,
    IReadOnlyList<string> Actions);

/// <summary>Who an action is aimed at.</summary>
public enum ActionTarget { Enemy, Ally, Self, Tile }

/// <summary>
/// Which tiles an action can reach (placeholders from the brief): Melee from the front row to the enemy front
/// row, Reach (spear) also the second row, Any is any enemy tile (ranged and spells).
/// </summary>
public enum ActionRange { Melee, Reach, Any }

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
    IReadOnlyList<EffectRef> Effects)
{
    public bool DealsDamage => BaseDamage > 0;
}

/// <summary>How long an effect lasts: no time at all (instant), a number of buff-clock turns, or until the
/// affected unit's next turn starts.</summary>
public enum DurationKind { Instant, Turns, UntilNextTurn }

/// <summary>When a buff's trigger fires (Anchor: Combat › Buffs and effects). Periodic effects are
/// <see cref="EffectDef.PeriodicDamage"/> and <see cref="EffectDef.PeriodicHeal"/>.</summary>
public enum TriggerOn { HitTaken, TurnStart, ActionComplete }

/// <summary>Who a triggered effect lands on: the buffed unit, or the other unit in the event (the attacker
/// for <see cref="TriggerOn.HitTaken"/>, the action's target for <see cref="TriggerOn.ActionComplete"/>).</summary>
public enum TriggerTarget { Self, Other }

/// <summary>A buff's trigger: when <see cref="On"/> happens and the state check passes, apply <see cref="Effect"/>.
/// The only state check so far: the buffed unit's Health is below a share of its maximum.</summary>
public sealed record TriggerDef(TriggerOn On, string Effect, TriggerTarget Target, double? HealthBelow);

/// <summary>
/// An effect or buff (Anchor: Combat › Buffs and effects). An instant effect heals or shields once. A buff
/// (any other duration) adds stat modifiers while it lasts, can carry a Shield that goes when it ends, can
/// deal damage or heal on every buff-clock turn, and can have triggers. A buff is unique per source (action +
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
    IReadOnlyList<TriggerDef> Triggers)
{
    public bool IsBuff => Duration != DurationKind.Instant;
}
