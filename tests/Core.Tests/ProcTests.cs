using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using static EternalDungeon.Core.Tests.TestData;

namespace EternalDungeon.Core.Tests;

// Procs (Anchor: Combat › Procs).
public class ProcTests
{
    const int Precision = 9;

    /// <summary>A unit with extra procs of its own (later these come from items and skills).</summary>
    static Unit U(GameData data, string def, string id, Side side, params string[] procs) =>
        new(id, data.Units[def] with { Procs = procs }, side, data);

    static Unit Sure(Unit u)
    {
        u.Stats.Add("test", "avoid", -0.9);                                   // every action against it lands
        return u;
    }

    static ActionResult Hit(Battle b, Unit actor, string action, Unit target)
    {
        actor.ActTicks = TurnClock.TurnThreshold;
        return b.Act(actor, b.Data.Actions[action], target);
    }

    // ---- Duplicates ----

    [Theory]
    [InlineData(1.0, 15, 1.0, 20, 1.0, 35)]        // Flaming 100% × 15 + 100% × 20
    [InlineData(0.25, 15, 0.5, 20, 0.625, 22)]     // expected 3.75 + 10 = 13.75 = 0.625 × 22
    [InlineData(0.05, 20, 1.0, 15, 1.0, 16)]       // the cheap copy adds only its expected 1
    public void Merging_keeps_the_expected_amount(double c1, double a1, double c2, double a2, double chance, double amount)
    {
        var merged = Battle.MergeCopies([(c1, a1), (c2, a2)]);
        Assert.Equal(chance, merged.Chance, Precision);
        Assert.Equal(amount, merged.Amount, Precision);
    }

    [Fact]
    public void Merged_states_combine_as_at_least_one_copy_firing()
    {
        Assert.Equal(0.36, Battle.MergeCopies([(0.2, 1), (0.2, 1)]).Chance, Precision);   // two 20% stuns
    }

    static (Battle Battle, Unit Warrior, Unit Grunt) TwoFlamingCopies(Duplicates rule)
    {
        var flaming = Repo.Procs["flaming"] with { Id = "flame_test", Duplicates = rule };
        var data = With(
            actions: [Action("oil", ActionTarget.Self, new EffectRef("fire_oil", EffectAim.Self))],
            effects: [Buff("fire_oil", turns: 9, procs: ["flame_test"])],
            procs: [flaming]);
        var w = U(data, "warrior", "w", Side.Party, "flame_test");          // one copy of its own...
        var g = Sure(U(data, "goblin_grunt", "g", Side.Enemy));
        g.Stats.Add("test", "health", 500);
        g.Heal(500);
        var b = new Battle(data, [w, g], seed: 1);
        Hit(b, w, "oil", w);                                                  // ...and one from a buff
        return (b, w, g);
    }

    [Fact]
    public void Merge_rolls_once_with_the_amounts_added()
    {
        var (b, w, g) = TwoFlamingCopies(Duplicates.Merge);
        var r = Hit(b, w, "attack", g);
        var roll = Assert.Single(r.Of<ProcRolled>());
        Assert.Equal(1, roll.Chance);
        Assert.Equal(2, roll.Scale);
        Assert.Equal(10, r.Of<ProcDamaged>().Single().Breakdown.Base);      // 5 + 5
    }

    [Fact]
    public void Separate_copies_each_roll()
    {
        var (b, w, g) = TwoFlamingCopies(Duplicates.Separate);
        var r = Hit(b, w, "attack", g);
        Assert.Equal(2, r.Of<ProcRolled>().Count());
        Assert.All(r.Of<ProcDamaged>(), d => Assert.Equal(5, d.Breakdown.Base));
    }

    [Fact]
    public void Unique_counts_only_the_strongest_copy()
    {
        var (b, w, g) = TwoFlamingCopies(Duplicates.Unique);
        var r = Hit(b, w, "attack", g);
        Assert.Equal(1, Assert.Single(r.Of<ProcRolled>()).Scale);
        Assert.Equal(5, r.Of<ProcDamaged>().Single().Breakdown.Base);
    }

    // ---- Chance ----

    [Fact]
    public void Chance_uses_rate_and_deval_over_the_procs_tags()
    {
        var w = U(Repo, "warrior", "w", Side.Party);
        var g = U(Repo, "goblin_grunt", "g", Side.Enemy);
        var dazzle = Repo.Procs["dazzling"];                                   // 5%, tags holy + control
        w.Stats.Add("test", "rate", 0.5, "control");
        Assert.Equal(0.075, Battle.CopyChance(dazzle, w, g), Precision);
        g.Stats.Add("test", "deval", 0.2, "control");                         // Tenacity-style resistance
        Assert.Equal(0.0625, Battle.CopyChance(dazzle, w, g), Precision);   // 0.075 ÷ 1.2
        Assert.Equal(0.075, Battle.CopyChance(dazzle, w, w), Precision);   // own Deval doesn't count on self
    }

    [Fact]
    public void Rate_above_100_percent_is_lost_and_never_scales_amounts()
    {
        var w = Sure(U(Repo, "warrior", "w", Side.Party, "flaming"));
        var g = Sure(U(Repo, "goblin_grunt", "g", Side.Enemy));
        w.Stats.Add("test", "rate", 0.5);
        Assert.Equal(1, Battle.CopyChance(Repo.Procs["flaming"], w, g));                  // 150% stops at 100%

        var b = new Battle(Repo, [w, g], seed: 1);
        var r = Hit(b, w, "attack", g);
        Assert.Equal(1, r.Of<ProcRolled>().Single().Scale);
        Assert.Equal(5, r.Of<ProcDamaged>().Single().Breakdown.Base);                     // not 7.5
    }

    // ---- Results ----

    [Fact]
    public void Proc_damage_goes_through_the_formula_with_the_procs_tags()
    {
        var w = U(Repo, "warrior", "w", Side.Party, "flaming");
        var g = Sure(U(Repo, "goblin_grunt", "g", Side.Enemy));
        w.Stats.Add("test", "power", 50, "fire");
        g.Stats.Add("test", "resist", 0.5, "fire");
        var b = new Battle(Repo, [w, g], seed: 1);

        var d = Hit(b, w, "attack", g).Of<ProcDamaged>().Single();
        Assert.Equal(3.75, d.Breakdown.Raw, Precision);                       // 5 × 1.5 × (1 − 0.5)
        Assert.Equal(4, d.Breakdown.Final);
        Assert.Equal(0, d.Breakdown.CritTiers);
    }

    [Fact]
    public void Proc_damage_is_not_an_action_and_triggers_nothing()
    {
        // The grunt is Spikey: struck by the warrior's Attack it fires once; the Flaming damage doesn't count as a strike.
        var w = Sure(U(Repo, "warrior", "w", Side.Party, "flaming"));
        var g = Sure(U(Repo, "goblin_grunt", "g", Side.Enemy, "spikey"));
        var b = new Battle(Repo, [w, g], seed: 1);

        var r = Hit(b, w, "attack", g);
        Assert.Single(r.Of<Attempt>());
        Assert.Single(r.Of<ProcRolled>(), p => p.Proc.Id == "spikey");
        Assert.Single(r.Of<ProcDamaged>(), p => p.Proc.Id == "spikey" && p.Target == w);
    }

    [Fact]
    public void Trigger_tags_filter_the_event()
    {
        var archer = U(Repo, "goblin_archer", "a", Side.Enemy);
        var grunt = U(Repo, "goblin_grunt", "g", Side.Enemy);
        var w = Sure(U(Repo, "warrior", "w", Side.Party, "spikey"));         // Spikey: struck by Melee only
        var b = new Battle(Repo, [w, archer, grunt], seed: 1);

        Assert.Empty(Hit(b, archer, "goblin_shot", w).Of<ProcRolled>());
        Assert.Single(Hit(b, grunt, "goblin_slash", w).Of<ProcRolled>());
    }

    [Fact]
    public void A_before_damage_proc_changes_only_that_hit()
    {
        var sure = Repo.Procs["armor_break"] with { Chance = 1 };
        var data = With(procs: [sure with { Id = "sure_break" }]);
        var w = U(data, "warrior", "w", Side.Party, "sure_break");
        var brute = Sure(U(data, "goblin_brute", "brute", Side.Enemy));     // Physical Resist 0.1
        var b = new Battle(data, [w, brute], seed: 1);

        var heavy = Hit(b, w, "power_attack", brute).Of<Damaged>().Single();
        Assert.Equal(0.5, heavy.Breakdown.Penetrate, Precision);
        Assert.Equal(0, w.Stats.Get("penetrate"));                            // gone after the hit

        var light = Hit(b, w, "attack", brute).Of<Damaged>().Single();       // not Heavy: no Armor Break
        Assert.Equal(0, light.Breakdown.Penetrate);
    }

    [Fact]
    public void Lifesteal_heals_a_share_of_the_damage_dealt()
    {
        var w = U(Repo, "warrior", "w", Side.Party, "vampiric");
        var g = Sure(U(Repo, "goblin_grunt", "g", Side.Enemy));
        w.TakeDamage(50);
        var b = new Battle(Repo, [w, g], seed: 1);

        var r = Hit(b, w, "attack", g);
        var dealt = r.Of<Damaged>().Single().Taken.ToHealth;
        Assert.Equal((int)Math.Round(dealt * 0.05, MidpointRounding.AwayFromZero), r.Of<Healed>().Single().Amount);
    }

    [Fact]
    public void A_proc_effect_applies_through_the_queue()
    {
        var sure = Repo.Procs["dazzling"] with { Id = "sure_dazzle", Chance = 1 };
        var data = With(procs: [sure]);
        var w = U(data, "warrior", "w", Side.Party, "sure_dazzle");
        var g = Sure(U(data, "goblin_grunt", "g", Side.Enemy));
        var b = new Battle(data, [w, g], seed: 1);

        var r = Hit(b, w, "attack", g);
        Assert.Equal("daze", r.Of<BuffApplied>().Single().Buff.Def.Id);
        Assert.True(g.Stunned);
        Assert.IsType<BuffApplied>(r.Outcomes[^1]);                            // buffs land after everything else
    }

    [Fact]
    public void Miss_avoided_and_crit_triggers()
    {
        var data = With(procs: [
            Proc("on_miss", ProcTrigger.Miss, ProcTarget.Self) with { Shield = 3 },
            Proc("on_avoid", ProcTrigger.Avoided, ProcTarget.Self) with { Shield = 4 }]);
        var w = U(data, "warrior", "w", Side.Party, "on_miss", "explosive");
        var g = U(data, "goblin_grunt", "g", Side.Enemy, "on_avoid");
        g.Stats.Add("test", "avoid", 0.99);
        var b = new Battle(data, [w, g], seed: 1);

        var miss = Hit(b, w, "attack", g);
        Assert.Equal(["on_miss", "on_avoid"], miss.Of<ProcRolled>().Select(p => p.Proc.Id));

        g.Stats.RemoveSource("test");
        g.Stats.Add("test", "avoid", -0.9);
        w.Stats.Add("test", "crit_rating", 2);                                // always crits: Explosive fires
        var crit = Hit(b, w, "attack", g);
        Assert.Contains(crit.Of<ProcRolled>(), p => p.Proc.Id == "explosive" && p.Roll.Success);
    }

    [Fact]
    public void Fight_start_procs_fire_once()
    {
        var data = With(procs: [Proc("rally", ProcTrigger.FightStart, ProcTarget.Self) with { Effect = "berserk" }]);
        var w = U(data, "warrior", "w", Side.Party, "rally");
        var b = new Battle(data, [w], seed: 1);

        Assert.Single(b.Start().Of<BuffApplied>());
        Assert.Equal(25, w.Stats.Get("power"));
        Assert.Throws<InvalidOperationException>(() => b.Start());
    }

    // ---- Data ----

    static DataException ProcFails(string proc, string hitStats = "[]") => TestData.TablesFail(
        ("tags", """[{ "id": "fire", "name": "Fire", "group": "element" }]"""),
        ("stats", """[{ "id": "health", "name": "Health", "group": "character", "combine": "add" }, { "id": "penetrate", "name": "Penetrate", "group": "attack", "combine": "dim" }]"""),
        ("effects", """[{ "id": "burn", "name": "Burn", "duration": "turns", "turns": 2, "periodic_damage": 3 }]"""),
        ("procs", $"[{proc}]"),
        ("proc_hit_stats", hitStats));

    [Fact]
    public void Proc_data_is_checked()
    {
        Assert.Contains("only hit procs", ProcFails("""{ "id": "p", "name": "P", "trigger": "struck", "target": "other", "phase": "before_damage", "damage": 1 }""").Message);
        Assert.Contains("phase before_damage", ProcFails("""{ "id": "p", "name": "P", "trigger": "hit", "target": "other" }""", """[{ "proc": "p", "stat": "penetrate", "value": 0.1 }]""").Message);
        Assert.Contains("lifesteal needs", ProcFails("""{ "id": "p", "name": "P", "trigger": "miss", "target": "self", "lifesteal": 0.1 }""").Message);
        Assert.Contains("use self", ProcFails("""{ "id": "p", "name": "P", "trigger": "fight_start", "target": "other", "effect": "burn" }""").Message);
        Assert.Contains("its own owner", ProcFails("""{ "id": "p", "name": "P", "trigger": "hit", "target": "self", "damage": 3 }""").Message);
        Assert.Contains("does nothing", ProcFails("""{ "id": "p", "name": "P", "trigger": "hit", "target": "other" }""").Message);
        Assert.Equal("[0].effect", ProcFails("""{ "id": "p", "name": "P", "trigger": "hit", "target": "other", "effect": "freeze" }""").Field);
    }
}
