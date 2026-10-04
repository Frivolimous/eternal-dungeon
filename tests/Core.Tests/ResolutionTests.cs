using EternalDungeon.Core.Combat;

namespace EternalDungeon.Core.Tests;

// M1 systems 5 and 6: actions and the hit, damage and proc formulas. Numbers checked by hand.
public class ResolutionTests
{
    const int Precision = 12;

    static Unit Make(string def, Side side) => new(def, TestData.Repo.Units[def], side, TestData.Repo);

    [Fact]
    public void Success_is_hit_times_one_minus_avoid()
    {
        Assert.Equal(0.9025, Resolution.SuccessChance(0.95, 0.05), Precision);
        Assert.Equal(1, Resolution.SuccessChance(0.95, -0.2));          // negative Avoid, capped at 1
        Assert.Equal(0, Resolution.SuccessChance(0.5, 1.2));
    }

    [Fact]
    public void Proc_chance_is_base_times_one_plus_rate_over_one_plus_deval()
    {
        Assert.Equal(0.15, Resolution.ProcChance(0.1, 0.5, 0), Precision);       // "50% more often": 10% → 15%
        Assert.Equal(0.2, Resolution.ProcChance(0.1, 1.0, 0), Precision);        // twice: +100% (Rate adds) → 20%
        Assert.Equal(0.1, Resolution.ProcChance(0.1, 0.5, 0.5), Precision);      // equal Rate and Deval cancel
        Assert.Equal(0.05, Resolution.ProcChance(0.1, 0, 1.0), Precision);       // Deval 1 halves it, never immune
        Assert.Equal(1.5, Resolution.ProcChance(1, 0.5, 0), Precision);          // not capped: the excess becomes amount
        Assert.Equal(1, Resolution.ProcChance(0.1, 0, -5), Precision);           // negative Deval: divisor floored at 0.1
        Assert.Equal(0, Resolution.ProcChance(0.1, -2, 0));                      // never below 0
    }

    [Fact]
    public void Damage_multiplies_every_factor()
    {
        var d = new DamageBreakdown(Base: 20, Power: 50, Multiplier: 0.2, Resist: 0.4, Penetrate: 0.25, AllDamage: 1.0, AllResist: 0.1);
        Assert.Equal(1.5, d.PowerFactor, Precision);
        Assert.Equal(1.2, d.MultiplierFactor, Precision);
        Assert.Equal(0.7, d.ResistFactor, Precision);                   // 1 − 0.4 × (1 − 0.25)
        Assert.Equal(2, d.AllDamageFactor, Precision);
        Assert.Equal(0.9, d.AllResistFactor, Precision);
        Assert.Equal(45.36, d.Raw, Precision);                          // 20 × 1.5 × 1.2 × 0.7 × 2 × 0.9
        Assert.Equal(45, d.Final);
    }

    [Fact]
    public void Damage_rounds_to_nearest_with_a_minimum_of_one()
    {
        static DamageBreakdown Base(double b) => new(b, 0, 0, 0, 0, 0, 0);
        Assert.Equal(34, Base(33.5).Final);
        Assert.Equal(33, Base(33.4).Final);
        Assert.Equal(1, Base(0.3).Final);
        Assert.Equal(0, Base(0).Final);
        Assert.Equal(0, new DamageBreakdown(10, -200, 0, 0, 0, 0, 0).Final);  // factors below 0 deal nothing
    }

    [Fact]
    public void Power_attack_doubles_through_all_damage()
    {
        var warrior = Make("warrior", Side.Party);
        var grunt = Make("goblin_grunt", Side.Enemy);
        var power = TestData.Repo.Actions["power_attack"];

        var d = Resolution.Damage(warrior, power, grunt);
        Assert.Equal(15, d.Base);
        Assert.Equal(22.5, d.Power, Precision);                          // Strength 15 × (1 + 0.5 Heavy)
        Assert.Equal(1, d.AllDamage);
        Assert.Equal(36.75, d.Raw, Precision);                           // 15 × 1.225 × 2
        Assert.Equal(37, d.Final);

        warrior.Stats.Add("ring", "multiplier", 0.2);
        Assert.Equal(44.1, Resolution.Damage(warrior, power, grunt).Raw, Precision);  // × 1.2 on top
    }

    [Fact]
    public void Target_resist_uses_the_action_tags()
    {
        var warrior = Make("warrior", Side.Party);
        var brute = Make("goblin_brute", Side.Enemy);                    // Physical Resist 0.1
        var d = Resolution.Damage(warrior, TestData.Repo.Actions["attack"], brute);
        Assert.Equal(0.1, d.Resist, Precision);
        Assert.Equal(15.525, d.Raw, Precision);                          // 15 × 1.15 × 0.9
        Assert.Equal(16, d.Final);

        var elementalist = Make("elementalist", Side.Party);
        Assert.Equal(0, Resolution.Damage(elementalist, TestData.Repo.Actions["fire_bolt"], brute).Resist);
    }

    [Fact]
    public void Success_uses_attacker_hit_and_target_avoid_for_the_action()
    {
        var warrior = Make("warrior", Side.Party);                       // Parry 10: Melee Avoid +0.10
        var grunt = Make("goblin_grunt", Side.Enemy);
        var archer = Make("goblin_archer", Side.Enemy);

        Assert.Equal(0.95 * 0.95, Resolution.SuccessChance(warrior, TestData.Repo.Actions["attack"], grunt), Precision);
        // Warrior Avoid vs melee: 0.05 dim 0.10 = 0.145, so 0.9 × 0.855.
        Assert.Equal(0.7695, Resolution.SuccessChance(grunt, TestData.Repo.Actions["goblin_slash"], warrior), Precision);
        // Parry doesn't help against arrows.
        Assert.Equal(0.855, Resolution.SuccessChance(archer, TestData.Repo.Actions["goblin_shot"], warrior), Precision);
    }

    [Fact]
    public void Base_dmg_stat_adds_to_the_action_base()
    {
        var warrior = Make("warrior", Side.Party);
        warrior.Stats.Add("sword", "base_dmg", 5, "melee");
        var grunt = Make("goblin_grunt", Side.Enemy);
        Assert.Equal(20, Resolution.Damage(warrior, TestData.Repo.Actions["attack"], grunt).Base);
    }
}
