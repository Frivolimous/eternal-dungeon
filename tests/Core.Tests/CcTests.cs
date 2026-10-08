using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using static EternalDungeon.Core.Tests.TestData;

namespace EternalDungeon.Core.Tests;

// M1 system 9: crowd control, stagger (Act knocked back) and interrupts.
public class CcTests
{
    static Unit U(GameData data, string def, string id, Side side) => new(id, data.Units[def], side, data);

    static Unit Exposed(Unit u)
    {
        u.Stats.Add("test", "avoid", -0.9);
        return u;
    }

    static BuffDef Cc(string id, CcKind cc, int turns = 2) =>
        Buff(id, turns) with { Cc = cc };

    /// <summary>Puts <paramref name="effect"/> on <paramref name="target"/> through a test action.</summary>
    static ActionResult Apply(Battle b, Unit caster, Unit target, string actionId)
    {
        caster.ActTicks = TurnClock.TurnThreshold;
        return b.Act(caster, b.Data.Actions[actionId], target);
    }

    static GameData WithCc(params BuffDef[] effects) => With(
        actions: [.. effects.Select(e => Action("apply_" + e.Id, ActionTarget.Ally, e.Id))],
        buffs: effects);

    [Fact]
    public void Slow_lowers_speed_while_it_lasts()
    {
        var data = Repo;
        var mage = U(data, "elementalist", "mage", Side.Party);
        var grunt = Exposed(U(data, "goblin_grunt", "grunt", Side.Enemy));
        var b = new Battle(data, [mage, grunt], seed: 1);

        Apply(b, mage, grunt, "frost_shard");
        Assert.Equal(60, grunt.Speed);                                   // 90 − 30
        b.BuffTick();
        b.BuffTick();
        Assert.Equal(90, grunt.Speed);
    }

    [Fact]
    public void Stun_stops_the_meter_and_interrupts_a_cast()
    {
        var data = WithCc(Cc("test_daze", CcKind.Stun));
        var w = U(data, "warrior", "w", Side.Party);
        var mage = U(data, "elementalist", "mage", Side.Party);
        var b = new Battle(data, [w, mage], seed: 1);
        b.Clock.BeginCast(mage, "fire_bolt", null, 50);

        var r = Apply(b, w, mage, "apply_test_daze");
        Assert.Single(r.Of<Interrupted>());
        Assert.Null(mage.Casting);
        Assert.Equal(0, mage.Speed);
        b.BuffTick();
        b.BuffTick();
        Assert.Equal(100, mage.Speed);
    }

    [Fact]
    public void Root_blocks_moving_and_silence_blocks_spells()
    {
        var data = WithCc(Cc("snare", CcKind.Root), Cc("hush", CcKind.Silence));
        var w = U(data, "warrior", "w", Side.Party);
        var mage = U(data, "elementalist", "mage", Side.Party);
        var b = new Battle(data, [w, mage], seed: 1);

        Apply(b, w, mage, "apply_snare");
        Assert.Equal("rooted", mage.CantUse(data.Actions["move"]));
        Assert.Null(mage.CantUse(data.Actions["fire_bolt"]));

        Apply(b, w, mage, "apply_hush");
        Assert.Equal("silenced", mage.CantUse(data.Actions["fire_bolt"]));
        Assert.Null(mage.CantUse(data.Actions["defend"]));
        mage.ActTicks = TurnClock.TurnThreshold;
        Assert.Throws<InvalidOperationException>(() => b.Act(mage, data.Actions["fire_bolt"], w));
    }

    [Fact]
    public void Sleep_takes_turns_until_a_hit_wakes_the_unit()
    {
        var data = WithCc(Cc("doze", CcKind.Sleep, turns: 5));
        var mage = U(data, "elementalist", "mage", Side.Party);
        var grunt = Exposed(U(data, "goblin_grunt", "grunt", Side.Enemy));
        var shaman = U(data, "goblin_shaman", "shaman", Side.Enemy);
        var b = new Battle(data, [mage, grunt, shaman], seed: 1);

        Apply(b, shaman, grunt, "apply_doze");                         // the test action targets an ally
        Assert.True(grunt.LosesTurn);
        grunt.ActTicks = TurnClock.TurnThreshold;
        Assert.Single(b.SkipTurn(grunt).Of<TurnLost>());
        Assert.Equal(0, grunt.ActTicks);

        Apply(b, mage, grunt, "frost_shard");
        Assert.False(grunt.LosesTurn);
    }

    [Fact]
    public void Delayed_damage_lands_when_the_buff_ends()
    {
        var data = WithCc(Buff("doom", turns: 2) with { DelayedDamage = 25 });
        var w = U(data, "warrior", "w", Side.Party);
        var b = new Battle(data, [w], seed: 1);
        Apply(b, w, w, "apply_doom");

        b.BuffTick();
        Assert.Equal(140, w.Health);
        var r = b.BuffTick();
        Assert.Single(r.Of<DelayedDamaged>());
        Assert.Equal(115, w.Health);
    }

    // ---- Stagger and Interrupt (reworked 2026-10-08) ----

    /// <summary>A unit at the very start of its meter, so the knock-back is easy to read.</summary>
    static Unit AtZero(Unit u)
    {
        u.ActTicks = 0;
        return u;
    }

    [Fact]
    public void Stagger_knocks_the_act_meter_back_and_can_push_it_negative()
    {
        var data = Repo;
        var smash = data.Procs["smash_stagger"].Stagger;                 // read from data: tuning changes it
        Assert.True(smash > 0);
        var brute = U(data, "goblin_brute", "brute", Side.Enemy);
        var w = AtZero(Exposed(U(data, "warrior", "w", Side.Party)));
        var b = new Battle(data, [w, brute], seed: 1);

        var r = Apply(b, brute, w, "brute_smash");
        Assert.Equal(new Staggered(w, smash, 0), r.Of<Staggered>().Single());
        Assert.Equal(-smash * TurnClock.TicksPerTurn, w.ActTicks);       // below 0: the next turn comes later
        Assert.Equal(100, w.Speed);                                       // no slow, no bar, nothing lingers
        b.BuffTick();
        Assert.Equal(-smash * TurnClock.TicksPerTurn, w.ActTicks);
    }

    static GameData Knocking => With(
        actions: [Action("knock_hit", ActionTarget.Enemy) with { Procs = ["knock"] }],
        procs: [Proc("knock", ProcTrigger.Hit, ProcTarget.Other, tags: ["force"]) with { Stagger = 20, IgnoreDeval = true }]);

    [Theory]
    [InlineData(0.5, 10)]                                                 // halved
    [InlineData(1.0, 0)]                                                  // fully resisted
    [InlineData(1.5, 0)]                                                  // never below 0
    [InlineData(-0.5, 20)]                                                // never above the full amount
    [InlineData(0.26, 15)]                                                // 14.8 rounds to a whole Act
    public void Force_deval_resists_stagger_as_a_share(double deval, int taken)
    {
        var data = Knocking;
        var brute = U(data, "goblin_brute", "brute", Side.Enemy);
        var w = AtZero(Exposed(U(data, "warrior", "w", Side.Party)));
        w.Stats.Add("test", "deval", deval, "force");
        var b = new Battle(data, [w, brute], seed: 1);

        var r = Apply(b, brute, w, "knock_hit");
        Assert.Equal(new Staggered(w, taken, 20 - taken), r.Of<Staggered>().Single());
        Assert.Equal(-taken * TurnClock.TicksPerTurn, w.ActTicks);
    }

    [Fact]
    public void An_ignore_deval_proc_keeps_its_chance_against_deval()
    {
        var plain = Proc("plain", ProcTrigger.Hit, ProcTarget.Other, chance: 0.5, tags: ["force"]) with { Stagger = 20 };
        var target = U(Repo, "goblin_grunt", "grunt", Side.Enemy);
        var owner = U(Repo, "warrior", "w", Side.Party);
        target.Stats.Add("test", "deval", 0.5, "force");
        Assert.Equal(0.5 / 1.5, Battle.CopyChance(plain, owner, target), 9);
        Assert.Equal(0.5, Battle.CopyChance(plain with { IgnoreDeval = true }, owner, target), 9);
    }

    [Fact]
    public void Smash_interrupts_a_cast_and_does_nothing_more_to_a_unit_not_casting()
    {
        var data = Repo;
        Assert.True(data.Procs["smash_stagger"].Interrupt);
        var brute = U(data, "goblin_brute", "brute", Side.Enemy);
        var tank = Exposed(U(data, "warrior", "tank", Side.Party));      // any unit can cast
        var b = new Battle(data, [tank, brute], seed: 1);

        var idle = Apply(b, brute, tank, "brute_smash");
        Assert.Empty(idle.Of<Interrupted>());

        b.Clock.BeginCast(tank, "fire_bolt", null, 50);
        var r = Apply(b, brute, tank, "brute_smash");
        Assert.Single(r.Of<Interrupted>());
        Assert.Null(tank.Casting);
    }

    [Fact]
    public void Bosses_resist_half_of_any_stagger()
    {
        var chief = U(Repo, "goblin_chief", "chief", Side.Enemy);
        Assert.Equal(0.5, chief.Stats.Get("deval", ["force"]), 9);
    }
}
