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
