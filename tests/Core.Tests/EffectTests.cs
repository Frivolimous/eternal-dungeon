using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using static EternalDungeon.Core.Tests.TestData;

namespace EternalDungeon.Core.Tests;

// M1 system 8: the effect queue and buffs.
public class EffectTests
{
    static Unit U(GameData data, string def, string id, Side side) => new(id, data.Units[def], side, data);

    /// <summary>Makes every action against <paramref name="u"/> land: negative Avoid pushes success to 1.</summary>
    static Unit Exposed(Unit u)
    {
        u.Stats.Add("test", "avoid", -0.9);
        return u;
    }

    /// <summary>Gives <paramref name="u"/> a full Act meter so it can act now.</summary>
    static Unit Ready(Unit u)
    {
        u.ActTicks = TurnClock.TurnThreshold;
        return u;
    }

    [Fact]
    public void Rot_deals_damage_every_buff_turn_then_expires()
    {
        var data = Repo;
        var shaman = Ready(U(data, "goblin_shaman", "shaman", Side.Enemy));
        var warrior = Exposed(U(data, "warrior", "warrior", Side.Party));
        var battle = new Battle(data, [warrior, shaman], seed: 1);

        var r = battle.Act(shaman, data.Actions["rotting_hex"], warrior);
        Assert.Single(r.Of<BuffApplied>());
        var afterHit = warrior.Health;

        for (var i = 0; i < 3; i++) battle.BuffTick();
        Assert.Equal(afterHit - 12, warrior.Health);                    // 4 × 3 turns
        Assert.Empty(warrior.Buffs);
        battle.BuffTick();
        Assert.Equal(afterHit - 12, warrior.Health);
    }

    [Fact]
    public void Damage_over_time_locks_in_the_casters_power_and_multiplier()
    {
        var data = Repo;
        var shaman = Ready(U(data, "goblin_shaman", "shaman", Side.Enemy));   // Magic 10: Spell Power 10
        var warrior = Exposed(U(data, "warrior", "warrior", Side.Party));
        shaman.Stats.Add("test", "power", 30, "toxic");
        shaman.Stats.Add("test", "multiplier", 0.5);
        var battle = new Battle(data, [warrior, shaman], seed: 1);

        battle.Act(shaman, data.Actions["rotting_hex"], warrior);
        Assert.Equal(1.4 * 1.5, warrior.Buffs.Single().DotFactor, 9);           // Power 40, Multiplier 0.5
        shaman.Stats.RemoveSource("test");                                       // later changes don't count

        var before = warrior.Health;
        var tick = battle.BuffTick();
        Assert.Equal(8, tick.Of<PeriodicDamaged>().Single().Taken.ToHealth);     // 4 × 2.1 = 8.4
        Assert.Equal(before - 8, warrior.Health);

        warrior.AddShield(5);
        Assert.Equal(new DamageTaken(5, 3, false), battle.BuffTick().Of<PeriodicDamaged>().Single().Taken);  // Shield still absorbs
    }

    [Fact]
    public void Buffs_apply_only_after_every_effect_resolves()
    {
        // Listed first: a +50 Power buff on the caster. Listed second: a heal that scales with the caster's Power.
        var data = With(
            actions: [Action("rally", ActionTarget.Self, new("pump", EffectAim.Self), new("patch", EffectAim.Self))],
            effects: [Buff("pump", stats: [new("power", null, 50)]), Instant("patch", heal: 20)]);
        var w = Ready(U(data, "warrior", "w", Side.Party));
        w.TakeDamage(100);
        var battle = new Battle(data, [w], seed: 1);

        var r = battle.Act(w, data.Actions["rally"], null);

        Assert.Equal(20, r.Of<Healed>().Single().Amount);               // not 30: the buff wasn't on yet
        Assert.IsType<Healed>(r.Outcomes[0]);
        Assert.IsType<BuffApplied>(r.Outcomes[1]);
        Assert.Equal(50, w.Stats.Get("power"));
    }

    [Fact]
    public void The_same_source_refreshes_a_buff_and_another_source_adds_one()
    {
        var data = With(
            actions: [Action("shout", ActionTarget.Ally, new EffectRef("brave", EffectAim.Target))],
            effects: [Buff("brave", turns: 3, stats: [new("power", null, 10)])]);
        var a = Ready(U(data, "warrior", "a", Side.Party));
        var b = Ready(U(data, "warrior", "b", Side.Party));
        var battle = new Battle(data, [a, b], seed: 1);
        var shout = data.Actions["shout"];

        battle.Act(a, shout, b);
        battle.BuffTick();
        Assert.Equal(2, b.Buffs.Single().Remaining);

        a.ActTicks = TurnClock.TurnThreshold;
        var again = battle.Act(a, shout, b);                            // same action + caster: refresh
        Assert.True(again.Of<BuffApplied>().Single().Refreshed);
        Assert.Equal(3, b.Buffs.Single().Remaining);
        Assert.Equal(10, b.Stats.Get("power"));

        battle.Act(b, shout, b);                                         // same buff, new caster: a second buff
        Assert.Equal(2, b.Buffs.Count);
        Assert.Equal(20, b.Stats.Get("power"));
    }

    [Fact]
    public void A_stacking_buff_adds_stacks_up_to_its_limit_and_resets_the_timer()
    {
        var data = Repo;                                                 // War Cry: +10 Power a stack, max 3, 3 turns
        var chief = U(data, "goblin_chief", "chief", Side.Enemy);
        var battle = new Battle(data, [chief], seed: 1);
        var cry = data.Actions["war_cry"];

        for (var i = 1; i <= 4; i++)
        {
            chief.ActTicks = TurnClock.TurnThreshold;
            battle.Act(chief, cry, null);
            battle.BuffTick();
            Assert.Equal(Math.Min(i, 3), chief.Buffs.Single().Stacks);
            Assert.Equal(2, chief.Buffs.Single().Remaining);            // reset to 3, then one tick
        }
        Assert.Equal(30, chief.Stats.Get("power"));

        battle.BuffTick();
        battle.BuffTick();
        Assert.Empty(chief.Buffs);
        Assert.Equal(0, chief.Stats.Get("power"));
    }

    [Fact]
    public void The_same_effect_from_several_sources_stacks_and_similar_effects_stay_separate()
    {
        var data = With(
            actions: [Action("hex_a", ActionTarget.Enemy, new EffectRef("rot", EffectAim.Target)),
                      Action("hex_b", ActionTarget.Enemy, new EffectRef("blight", EffectAim.Target))],
            effects: [Buff("blight", turns: 3, periodicDamage: 4)]);    // same numbers as Rot, a different effect
        var s1 = U(data, "goblin_shaman", "s1", Side.Enemy);
        var s2 = U(data, "goblin_shaman", "s2", Side.Enemy);
        var w = Exposed(U(data, "warrior", "w", Side.Party));
        var battle = new Battle(data, [w, s1, s2], seed: 1);

        foreach (var (caster, action) in new[] { (s1, "rotting_hex"), (s2, "rotting_hex"), (s1, "hex_b") })
        {
            caster.ActTicks = TurnClock.TurnThreshold;
            battle.Act(caster, data.Actions[action], w);
        }

        Assert.Equal(3, w.Buffs.Count);                                  // Rot from s1, Rot from s2, Blight from s1
        var before = w.Health;
        battle.BuffTick();
        Assert.Equal(before - 12, w.Health);
    }

    [Fact]
    public void Effects_from_a_buffs_procs_join_the_same_queue()
    {
        // Thorns grants two procs: when struck, put Rot on the attacker; when struck below half Health, shield yourself.
        var data = With(
            actions: [Action("bless", ActionTarget.Self, new EffectRef("thorns", EffectAim.Self))],
            effects: [Buff("thorns", procs: ["thorn_rot", "second_wind"])],
            procs: [
                Proc("thorn_rot", ProcTrigger.Struck, ProcTarget.Other) with { Effect = "rot" },
                Proc("second_wind", ProcTrigger.Struck, ProcTarget.Self) with { Shield = 14, OwnerHealthBelow = 0.5 }]);
        var w = Exposed(Ready(U(data, "warrior", "w", Side.Party)));
        var g = Ready(U(data, "goblin_grunt", "g", Side.Enemy));
        var battle = new Battle(data, [w, g], seed: 1);
        battle.Act(w, data.Actions["bless"], null);

        var r = battle.Act(g, data.Actions["goblin_slash"], w);
        Assert.Collection(r.Outcomes,
            o => Assert.IsType<Attempt>(o),
            o => Assert.IsType<CritRolled>(o),                              // Slash is a weapon attack: 5% Crit Rating
            o => Assert.IsType<Damaged>(o),
            o => Assert.Equal("thorn_rot", Assert.IsType<ProcRolled>(o).Proc.Id),
            o => Assert.Equal(g, Assert.IsType<BuffApplied>(o).Target));  // Second Wind's check failed: Health is high
        Assert.Equal(0, w.Shield);

        w.TakeDamage(w.Health - 30);                                      // now below half
        g.ActTicks = TurnClock.TurnThreshold;
        var low = battle.Act(g, data.Actions["goblin_slash"], w);
        Assert.Single(low.Of<Shielded>());
        Assert.Equal(14, w.Shield);
    }

    [Fact]
    public void Defend_lasts_until_the_units_next_turn()
    {
        var data = Repo;
        var w = Ready(U(data, "warrior", "w", Side.Party));
        var battle = new Battle(data, [w], seed: 1);

        battle.Act(w, data.Actions["defend"], null);
        Assert.Equal(14, w.Shield);                                       // 10% of 140
        Assert.Equal(1 - 0.95 * 0.7, w.Stats.Get("avoid"), 12);          // 0.05 dim 0.3

        w.TakeDamage(5);
        battle.StartTurn(w);
        Assert.Equal(0, w.Shield);
        Assert.Equal(0.05, w.Stats.Get("avoid"), 12);
        Assert.Empty(w.Buffs);
    }

    [Fact]
    public void Mend_heals_with_the_casters_power()
    {
        var data = Repo;
        var shaman = Ready(U(data, "goblin_shaman", "shaman", Side.Enemy)); // Magic 10: Spell Power 10
        var grunt = U(data, "goblin_grunt", "grunt", Side.Enemy);
        grunt.TakeDamage(40);
        var battle = new Battle(data, [shaman, grunt], seed: 1);

        var r = battle.Act(shaman, data.Actions["mend"], grunt);
        Assert.Equal(22, r.Of<Healed>().Single().Amount);                // 20 × 1.10
        Assert.Equal(30, shaman.Mana);
    }

    [Fact]
    public void A_cast_resolves_when_its_timer_completes_and_fizzles_if_the_target_fell()
    {
        var data = Repo;
        var mage = Ready(U(data, "elementalist", "mage", Side.Party));
        var g1 = Exposed(U(data, "goblin_grunt", "g1", Side.Enemy));
        var g2 = U(data, "goblin_grunt", "g2", Side.Enemy);
        var battle = new Battle(data, [mage, g1, g2], seed: 1);

        Assert.IsType<TurnReady>(battle.Clock.Next());
        var start = battle.Act(mage, data.Actions["fire_bolt"], g1);
        Assert.Equal(50, start.Of<CastStarted>().Single().ReadyAt);
        Assert.Equal(g1.MaxHealth, g1.Health);

        var done = battle.CompleteCast((CastComplete)NextNonTurn(battle));
        Assert.Single(done.Of<Damaged>());
        Assert.True(g1.Health < g1.MaxHealth);

        mage.ActTicks = TurnClock.TurnThreshold;
        battle.Act(mage, data.Actions["fire_bolt"], g2);
        g2.TakeDamage(999);
        var fizzle = battle.CompleteCast((CastComplete)NextNonTurn(battle));
        Assert.Single(fizzle.Of<Fizzled>());
    }

    [Fact]
    public void A_cast_fizzles_when_its_caster_dies()
    {
        var data = Repo;
        var mage = Ready(U(data, "elementalist", "mage", Side.Party));
        var g = U(data, "goblin_grunt", "g", Side.Enemy);
        var battle = new Battle(data, [mage, g], seed: 1);
        battle.Clock.Next();
        battle.Act(mage, data.Actions["fire_bolt"], g);
        Assert.NotNull(mage.Casting);

        mage.Stats.Add("test", "avoid", -0.9);
        mage.TakeDamage(mage.Health - 1);
        g.ActTicks = TurnClock.TurnThreshold;
        var r = battle.Act(g, data.Actions["goblin_slash"], mage);
        Assert.Contains(r.Of<Fizzled>(), f => f.Reason.Contains("Fire Bolt fizzles"));
        Assert.Null(mage.Casting);
        Assert.DoesNotContain(Enumerable.Range(0, 5).Select(_ => battle.Clock.Next()), e => e is CastComplete);
    }

    static ClockEvent NextNonTurn(Battle b)
    {
        while (true)
        {
            var e = b.Clock.Next();
            if (e is not TurnReady and not BuffTick) return e;
            if (e is TurnReady t) TurnClock.Spend(t.Unit, 100);
        }
    }
}
