using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

/// <summary>
/// A buff on a unit. Its source is the action plus the caster: the same buff from the same source refreshes
/// (or adds a stack, if stacking), while the same buff from another source is a separate buff whose stats
/// stack (Anchor: Combat › Buffs and effects).
/// </summary>
public sealed class Buff(EffectDef def, string casterId, string actionId)
{
    public EffectDef Def { get; } = def;
    public string CasterId { get; } = casterId;
    public string ActionId { get; } = actionId;

    public int Stacks { get; set; } = 1;
    /// <summary>Buff-clock turns left, for buffs with a number of turns.</summary>
    public int Remaining { get; set; } = def.Turns;
    /// <summary>The caster's Power factor × Multiplier factor for the buff's tags, locked in when it was applied;
    /// every damage-over-time tick is scaled by it.</summary>
    public double DotFactor { get; set; } = 1;

    /// <summary>The Shield this buff granted, taken back (as far as it's left) when the buff ends.</summary>
    public int ShieldGranted { get; set; }

    /// <summary>The StatBlock source for this buff's modifiers.</summary>
    public string SourceKey => Key(Def.Id, ActionId, CasterId);

    public static string Key(string effectId, string actionId, string casterId) => $"buff:{effectId}:{actionId}:{casterId}";

    public override string ToString() => Stacks > 1 ? $"{Def.Name} ×{Stacks}" : Def.Name;
}
