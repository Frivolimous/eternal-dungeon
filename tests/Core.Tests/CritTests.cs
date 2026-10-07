using EternalDungeon.Core.Combat;

namespace EternalDungeon.Core.Tests;

// Crit (Anchor: Combat › Formulas): C.Rate → per-hit chance, Brutal at the same chance, C.Mult per tier.
public class CritTests
{
    const int Precision = 12;

    static Unit U(string def, string id, Side side) => new(id, TestData.Repo.Units[def], side, TestData.Repo);

    [Fact]
    public void C_rate_converts_so_that_c_plus_c_squared_equals_it()
    {
        Assert.Equal(0, Resolution.CritChance(0));
        Assert.Equal((Math.Sqrt(5) - 1) / 2, Resolution.CritChance(1), Precision);   // 61.8%
        Assert.Equal(1, Resolution.CritChance(2), Precision);                        // the cap: always crit, always Brutal
        Assert.Equal(1, Resolution.CritChance(3.5), Precision);                      // capped at 200%
        foreach (var r in new[] { 0.05, 0.5, 1.3 })
        {
            var c = Resolution.CritChance(r);
            Assert.Equal(r, c + c * c, Precision);                                    // expected tiers = C.Rate
        }
    }

    [Fact]
    public void Every_unit_starts_with_weapon_c_rate_and_half_c_mult()
    {
        var w = U("warrior", "w", Side.Party);
        var g = U("goblin_grunt", "g", Side.Enemy);
        Assert.Equal(0.05, Resolution.CRate(w, TestData.Repo.Actions["attack"], g), Precision);
        Assert.Equal(0, Resolution.CRate(U("elementalist", "e", Side.Party), TestData.Repo.Actions["fire_bolt"], g));
        Assert.Equal(0.5, w.Stats.Get("c_mult", TestData.Repo.Actions["fire_bolt"].Tags));   // untagged: spells too

        var e = U("elementalist", "e2", Side.Party);
        e.Stats.Add("ring", "c_rate", 0.2, "spell");                              // a spell gains crit without its own mult
        Assert.Equal(0.2, Resolution.CRate(e, TestData.Repo.Actions["fire_bolt"], g), Precision);
    }

    [Fact]
    public void Critical_deval_lowers_c_rate_before_the_curve()
    {
        var w = U("warrior", "w", Side.Party);
        var g = U("goblin_grunt", "g", Side.Enemy);
        w.Stats.Add("test", "c_rate", 0.95);                                      // C.Rate 1.0 with the default 0.05
        g.Stats.Add("test", "deval", 0.25, "critical");
        Assert.Equal(0.8, Resolution.CRate(w, TestData.Repo.Actions["attack"], g), Precision);   // 1.0 ÷ 1.25
        g.Stats.Add("curse", "deval", 0.5);                                            // untagged Deval isn't Critical Deval
        Assert.Equal(0.8, Resolution.CRate(w, TestData.Repo.Actions["attack"], g), Precision);
    }

    [Fact]
    public void Each_tier_adds_c_mult_reduced_by_critical_resist()
    {
        var w = U("warrior", "w", Side.Party);
        var g = U("goblin_grunt", "g", Side.Enemy);
        var attack = TestData.Repo.Actions["attack"];

        var plain = Resolution.Damage(w, attack, g);
        var crit = Resolution.Damage(w, attack, g, critTiers: 1);
        var brutal = Resolution.Damage(w, attack, g, critTiers: 2);
        Assert.Equal(1.5, crit.CritFactor, Precision);
        Assert.Equal(2.0, brutal.CritFactor, Precision);
        Assert.Equal(plain.Raw * 1.5, crit.Raw, Precision);

        var chief = U("goblin_chief", "chief", Side.Enemy);                            // Critical Resist 0.10
        Assert.Equal(1 + 0.5 * 0.9, Resolution.Damage(w, attack, chief, 1).CritFactor, Precision);
        w.Stats.Add("axe", "penetrate", 0.5, "critical");
        Assert.Equal(1 + 0.5 * (1 - 0.1 * 0.5), Resolution.Damage(w, attack, chief, 1).CritFactor, Precision);
    }

    [Fact]
    public void Crit_is_rolled_only_on_a_hit_and_brutal_only_after_a_crit()
    {
        var w = U("warrior", "w", Side.Party);
        var g = U("goblin_grunt", "g", Side.Enemy);
        w.Stats.Add("test", "c_rate", 0.5);
        var battle = new Battle(TestData.Repo, [w, g], seed: 3);
        int hits = 0, crits = 0, brutals = 0, misses = 0;

        for (var i = 0; i < 400; i++)
        {
            w.ActTicks = TurnClock.TurnThreshold;
            g.Heal(999);
            var r = battle.Act(w, TestData.Repo.Actions["attack"], g);
            var attempt = r.Of<Attempt>().Single();
            var roll = r.Of<CritRolled>().SingleOrDefault();
            if (!attempt.Roll.Success)
            {
                misses++;
                Assert.Null(roll);
                continue;
            }
            hits++;
            Assert.NotNull(roll);
            if (roll.Crit.Success) crits++;
            else Assert.Null(roll.Brutal);
            if (roll.Tiers == 2) brutals++;
            Assert.Equal(roll.Tiers, r.Of<Damaged>().Single().Breakdown.CritTiers);
        }
        Assert.True(misses > 0);
        // C.Rate 0.55 → c ≈ 0.42: about 42% of hits crit and 42% of those go Brutal.
        Assert.InRange((double)crits / hits, 0.34, 0.50);
        Assert.InRange((double)brutals / crits, 0.30, 0.55);
    }

    [Fact]
    public void Damage_over_time_and_effects_never_crit()
    {
        var shaman = U("goblin_shaman", "s", Side.Enemy);
        var w = U("warrior", "w", Side.Party);
        w.Stats.Add("test", "avoid", -0.9);
        shaman.Stats.Add("test", "c_rate", 2);                                     // even at the cap
        var battle = new Battle(TestData.Repo, [w, shaman], seed: 1);
        shaman.ActTicks = TurnClock.TurnThreshold;

        var hex = battle.Act(shaman, TestData.Repo.Actions["rotting_hex"], w);          // its direct damage can crit
        Assert.Equal(2, hex.Of<CritRolled>().Single().Tiers);
        var tick = battle.BuffTick();
        Assert.Empty(tick.Of<CritRolled>());
        Assert.Equal(4, tick.Of<PeriodicDamaged>().Single().Taken.ToHealth);
    }
}
