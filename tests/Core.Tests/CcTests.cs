using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using static EternalDungeon.Core.Tests.TestData;

namespace EternalDungeon.Core.Tests;

// M1 system 9: crowd control and the stagger bar.
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

    // ---- Stagger bar ----

    [Fact]
    public void Stagger_halves_speed_while_the_bar_is_above_zero()
    {
        var data = Repo;
        var smash = data.Procs["smash_stagger"].Stagger;                 // read from data: tuning changes it
        Assert.InRange(smash, 11, 99);
        var brute = U(data, "goblin_brute", "brute", Side.Enemy);
        var w = Exposed(U(data, "warrior", "w", Side.Party));
        var b = new Battle(data, [w, brute], seed: 1);

        var r = Apply(b, brute, w, "brute_smash");
        Assert.Equal(new Staggered(w, smash, smash, false), r.Of<Staggered>().Single());
        Assert.Equal(50, w.Speed);

        b.BuffTick();
        Assert.Equal(smash - Battle.StaggerDrain, w.Stagger);             // drains each buff-clock turn
        Assert.Equal(50, w.Speed);
        for (var i = 0; i < 10; i++) b.BuffTick();
        Assert.Equal(0, w.Stagger);
        Assert.Equal(100, w.Speed);
    }

    [Fact]
    public void A_full_bar_stuns_ignores_stagger_and_drains_back()
    {
        var data = Repo;
        var brute = U(data, "goblin_brute", "brute", Side.Enemy);
        var tank = Exposed(U(data, "warrior", "tank", Side.Party));      // any unit can cast; 140 Health survives the Smashes
        var b = new Battle(data, [tank, brute], seed: 1);
        b.Clock.BeginCast(tank, "fire_bolt", null, 50);

        ActionResult last;
        do last = Apply(b, brute, tank, "brute_smash");                  // Smash until the bar fills
        while (!last.Of<Staggered>().Single().Broke);
        Assert.Single(last.Of<Interrupted>());
        Assert.True(tank.StaggerBroken);
        Assert.Equal(Unit.StaggerMax, tank.Stagger);
        Assert.Equal(0, tank.Speed);

        var white = Apply(b, brute, tank, "brute_smash");                 // white bar: no more stagger
        Assert.Equal(Unit.StaggerMax, tank.Stagger);
        Assert.Empty(white.Of<Staggered>());
        Assert.Equal(new StaggerIgnored(tank), white.Of<StaggerIgnored>().Single());

        for (var i = 0; i < 9; i++) b.BuffTick();
        Assert.Equal(10, tank.Stagger);
        Assert.Equal(0, tank.Speed);                                      // still stunned until empty
        b.BuffTick();
        Assert.False(tank.StaggerBroken);
        Assert.Equal(100, tank.Speed);
    }
}
