using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

/// <summary>Every term of one damage calculation, so a full combat log can show how a number was reached.</summary>
public sealed record DamageBreakdown(
    double Base,
    double Power,
    double Multiplier,
    double Resist,
    double Penetrate,
    double AllDamage,
    double AllResist,
    int CritTiers = 0,
    double CritMult = 0,
    double CritResist = 0,
    double CritPenetrate = 0)
{
    public double PowerFactor => 1 + Power / 100;
    public double MultiplierFactor => 1 + Multiplier;
    public double ResistFactor => 1 - Resist * (1 - Penetrate);
    public double AllDamageFactor => 1 + AllDamage;
    public double AllResistFactor => 1 - AllResist;

    /// <summary>1 + tiers × Crit Mult × (1 − Critical Resist × (1 − Critical Penetrate)); 1 when it didn't crit.</summary>
    public double CritFactor => 1 + CritTiers * CritMult * (1 - CritResist * (1 - CritPenetrate));

    /// <summary>The unrounded result, never below 0.</summary>
    public double Raw => Math.Max(0, Base * PowerFactor * MultiplierFactor * ResistFactor * AllDamageFactor * AllResistFactor * CritFactor);

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

    /// <summary>Crit Rating's hard cap (Anchor: 200%).</summary>
    public const double MaxCritRating = 2;

    /// <summary>
    /// The per-hit chance of a crit for a Crit Rating, and again of a Brutal crit after one:
    /// c = (√(1 + 4 × Rating) − 1) / 2, so c + c² = Rating and expected damage rises in a straight line.
    /// Rating 1 gives 61.8%; Rating 2 (the cap) gives 100%.
    /// </summary>
    public static double CritChance(double rating)
    {
        var r = Math.Clamp(rating, 0, MaxCritRating);
        return (Math.Sqrt(1 + 4 * r) - 1) / 2;
    }

    /// <summary>The attacker's Crit Rating for the action's tags, lowered by the target's Critical Deval, capped.</summary>
    public static double CritRating(Unit attacker, ActionDef action, Unit target) =>
        Math.Clamp(attacker.Stats.Get("crit_rating", action.Tags) * (1 - target.Stats.GetKeyed("deval", Critical)), 0, MaxCritRating);

    static readonly string[] Critical = ["critical"];

    /// <summary>The chance <paramref name="attacker"/>'s action succeeds against <paramref name="target"/>:
    /// the attacker's Hit and the target's Avoid, both for the action's tags.</summary>
    public static double SuccessChance(Unit attacker, ActionDef action, Unit target) =>
        SuccessChance(attacker.Stats.Get("hit", action.Tags), target.Stats.Get("avoid", action.Tags));

    /// <summary>
    /// Damage from <paramref name="attacker"/>'s action to <paramref name="target"/>. Attack stats come from the
    /// attacker and defense stats from the target, all for the action's tags. Base adds the attacker's Base Dmg
    /// to the action's base damage, and the action's All Damage adds to the attacker's.
    /// </summary>
    /// <param name="critTiers">0 (no crit), 1 (crit) or 2 (Brutal). Critical Resist and Penetrate count only their
    /// Critical-keyed parts: the untagged ones already applied to the hit.</param>
    public static DamageBreakdown Damage(Unit attacker, ActionDef action, Unit target, int critTiers = 0)
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
            AllResist: d.Get("all_resist"),
            CritTiers: critTiers,
            CritMult: critTiers > 0 ? a.Get("crit_mult", tags) : 0,
            CritResist: critTiers > 0 ? d.GetKeyed("resist", Critical) : 0,
            CritPenetrate: critTiers > 0 ? a.GetKeyed("penetrate", Critical) : 0);
    }
}
