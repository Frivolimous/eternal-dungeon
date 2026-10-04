using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Battle;

/// <summary>Every term of one damage calculation, so a full combat log can show how a number was reached.</summary>
public sealed record DamageBreakdown(
    double Base,
    double Power,
    double Multiplier,
    double Resist,
    double Penetrate,
    double AllDamage,
    double AllResist)
{
    public double PowerFactor => 1 + Power / 100;
    public double MultiplierFactor => 1 + Multiplier;
    public double ResistFactor => 1 - Resist * (1 - Penetrate);
    public double AllDamageFactor => 1 + AllDamage;
    public double AllResistFactor => 1 - AllResist;

    /// <summary>The unrounded result, never below 0.</summary>
    public double Raw => Math.Max(0, Base * PowerFactor * MultiplierFactor * ResistFactor * AllDamageFactor * AllResistFactor);

    /// <summary>Whole damage: nearest, and at least 1 when the raw result is above 0.</summary>
    public int Final => Raw <= 0 ? 0 : Math.Max(1, (int)Math.Round(Raw, MidpointRounding.AwayFromZero));
}

/// <summary>The resolution formulas (Anchor: Combat › Formulas). Pure: no rolls, no state changes.</summary>
public static class Resolution
{
    /// <summary>Success = Hit × (1 − Avoid), kept within 0–1 (negative Avoid can push it past Hit).</summary>
    public static double SuccessChance(double hit, double avoid) => Math.Clamp(hit * (1 - avoid), 0, 1);

    /// <summary>Proc = Base × (1 + Rate) × (1 − Deval), kept within 0–1.</summary>
    public static double ProcChance(double baseChance, double rate, double deval) =>
        Math.Clamp(baseChance * (1 + rate) * (1 - deval), 0, 1);

    /// <summary>The chance <paramref name="attacker"/>'s action succeeds against <paramref name="target"/>:
    /// the attacker's Hit and the target's Avoid, both for the action's tags.</summary>
    public static double SuccessChance(Unit attacker, ActionDef action, Unit target) =>
        SuccessChance(attacker.Stats.Get("hit", action.Tags), target.Stats.Get("avoid", action.Tags));

    /// <summary>
    /// Damage from <paramref name="attacker"/>'s action to <paramref name="target"/>. Attack stats come from the
    /// attacker and defense stats from the target, all for the action's tags. Base adds the attacker's Base Dmg
    /// to the action's base damage, and the action's All Damage adds to the attacker's.
    /// </summary>
    public static DamageBreakdown Damage(Unit attacker, ActionDef action, Unit target)
    {
        var tags = action.Tags;
        var a = attacker.Stats;
        var d = target.Stats;
        return new DamageBreakdown(
            Base: action.BaseDamage + a.Get("base_dmg", tags),
            Power: a.Get("power", tags),
            Multiplier: a.Get("multiplier", tags),
            Resist: d.Get("resist", tags),
            Penetrate: a.Get("penetrate", tags),
            AllDamage: a.Get("all_damage") + action.AllDamage,
            AllResist: d.Get("all_resist"));
    }
}
