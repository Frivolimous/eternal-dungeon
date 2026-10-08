using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using static EternalDungeon.Core.Tests.TestData;

namespace EternalDungeon.Core.Tests;

// The Threat score, Threatening and committed enemy intents (Jeremy, 2026-10-08).
public class ThreatTests
{
    static Unit U(GameData data, string def, string id, Side side) => new(id, data.Units[def], side, data);
    static Tile P(int row, int col) => new(BattleGrid.PartyArea, row, col);
    static Tile E(int row, int col) => new(BattleGrid.EnemyArea, row, col);

    static Unit Exposed(Unit u)
    {
        u.Stats.Add("test", "avoid", -0.9);
        return u;
    }

    static ActionResult Use(Battle b, Unit actor, string action, Unit? target = null)
    {
        actor.ActTicks = TurnClock.TurnThreshold;
        return b.Act(actor, b.Data.Actions[action], target ?? actor);
    }

    static IEnumerable<IntentSet> Changes(ActionResult r) => r.Of<IntentSet>().Where(i => i.Triggered);

    // ---- The Threat score ----

    [Fact]
    public void Starting_threat_sets_the_first_score()
    {
        var w = U(Repo, "warrior", "w", Side.Party);
        var rogue = U(Repo, "rogue", "r", Side.Party);
        _ = new Battle(Repo, [w, rogue, U(Repo, "goblin_grunt", "g", Side.Enemy)], seed: 1);
        Assert.Equal(20, w.ThreatScore);
        Assert.Equal(0, rogue.ThreatScore);
    }

    [Fact]
    public void Overkill_and_shield_absorbed_damage_count_in_full_and_a_miss_counts_nothing()
    {
        var w = U(Repo, "warrior", "w", Side.Party);
        var grunt = Exposed(U(Repo, "goblin_grunt", "g", Side.Enemy));
        var b = new Battle(Repo, [w, grunt], seed: 1);
        grunt.TakeDamage(grunt.MaxHealth - 3);
        grunt.AddShield(2);
        var before = w.ThreatScore;
        var r = Use(b, w, "power_attack", grunt);
        var hit = r.Of<Damaged>().Single();
        Assert.True(hit.Breakdown.Final > hit.Taken.Absorbed + hit.Taken.ToHealth);  // overkill
        Assert.Equal(before + hit.Breakdown.Final, w.ThreatScore);

        for (ulong seed = 1; ; seed++)                                      // the first seed that misses
        {
            var dodgy = U(Repo, "goblin_grunt", "d", Side.Enemy);
            dodgy.Stats.Add("test", "avoid", 0.9);
            var w2 = U(Repo, "warrior", "w2", Side.Party);
            var b2 = new Battle(Repo, [w2, dodgy], seed);
            if (Use(b2, w2, "power_attack", dodgy).Of<Attempt>().Single().Roll.Success) continue;
            Assert.Equal(20, w2.ThreatScore);
            break;
        }
    }

    [Fact]
    public void Overheal_counts_in_full()
    {
        var data = With(
            actions: [Action("patch_up", ActionTarget.Self) with { Procs = ["heal_20"] }],
            procs: [Proc("heal_20", ProcTrigger.ActionComplete, ProcTarget.Self) with { Heal = 20 }]);
        var rogue = U(data, "rogue", "r", Side.Party);
        var b = new Battle(data, [rogue, U(data, "goblin_grunt", "g", Side.Enemy)], seed: 1);
        var r = Use(b, rogue, "patch_up");
        Assert.Equal(0, r.Of<Healed>().Single().Amount);                 // already at full Health
        Assert.Equal(20, rogue.ThreatScore);
    }

    // ---- Scoring ----

    [Fact]
    public void Threat_is_scaled_by_the_highest_and_threatening_multiplies_it_when_scored()
    {
        var a = U(Repo, "rogue", "a", Side.Party);                       // same Health: vulnerability 1 each
        var c = U(Repo, "rogue", "c", Side.Party);
        a.ThreatScore = 50;
        c.ThreatScore = 100;
        var scores = UnitAi.Scores([a, c], 0.5).ToDictionary(s => s.Unit, s => s.Score);
        Assert.Equal(0.25 + 0.5, scores[a], 9);
        Assert.Equal(0.5 + 0.5, scores[c], 9);

        c.Stats.Add("stealth", "threatening", -1);                       // Stealth: ×0 while it lasts
        Assert.Equal(0, c.EffectiveThreat);
        Assert.Equal(1, UnitAi.Scores([a, c], 0.5).Single(s => s.Unit == a).Score, 9);
        c.Stats.Add("more", "threatening", -1);
        Assert.Equal(0, c.Threatening);                                   // never below 0
        c.Stats.RemoveSource("stealth");
        c.Stats.RemoveSource("more");
        Assert.Equal(100, c.EffectiveThreat);                             // the whole history counts again

        a.ThreatScore = 0;
        c.ThreatScore = 0;
        Assert.All(UnitAi.Scores([a, c], 0.5), s => Assert.Equal(0.5, s.Score, 9));   // all zero: no threat part at all
    }

    // ---- Intents ----

    /// <summary>
    /// A Grunt facing the Warrior and the Rogue, both in its reach. Picks are weighted, so this tries seeds from
    /// <paramref name="firstSeed"/> until the Grunt's opening plan aims at the Rogue (or the Warrior).
    /// </summary>
    static (Battle B, Unit Grunt, Unit Warrior, Unit Rogue) Standoff(GameData? data = null, double rogueThreat = 100,
        bool aimAtRogue = true, ulong firstSeed = 1)
    {
        data ??= Repo;
        for (var seed = firstSeed; ; seed++)
        {
            var w = Exposed(U(data, "warrior", "w", Side.Party));
            var rogue = Exposed(U(data, "rogue", "r", Side.Party));
            var grunt = Exposed(U(data, "goblin_grunt", "g", Side.Enemy));
            var grid = new BattleGrid();
            grid.Place(w, P(0, 0));
            grid.Place(rogue, P(0, 1));
            grid.Place(grunt, E(0, 1));
            var b = new Battle(data, [w, rogue, grunt], seed, grid);
            rogue.ThreatScore = rogueThreat;
            b.Start();
            if (grunt.Intent!.Decision!.Target == (aimAtRogue ? rogue : w)) return (b, grunt, w, rogue);
        }
    }

    [Fact]
    public void Enemies_plan_at_the_start_and_heroes_dont()
    {
        var (b, grunt, w, rogue) = Standoff();
        Assert.Equal(rogue, grunt.Intent!.Decision!.Target);
        Assert.Null(w.Intent);
        Assert.Contains(b.Results[0].Of<IntentSet>(), i => i.Unit == grunt && i.Why == IntentReason.BattleStart);
    }

    [Fact]
    public void Trigger_1_the_target_lowering_its_threatening_replans_and_it_wearing_off_doesnt()
    {
        var data = With(actions: [Action("hide", ActionTarget.Self, "stealth")]);
        var (b, grunt, _, rogue) = Standoff(data);
        var r = Use(b, rogue, "hide");
        Assert.Equal(IntentReason.TargetHid, Changes(r).Single().Why);
        var plan = grunt.Intent;

        var ticks = Enumerable.Range(0, 3).Select(_ => b.BuffTick()).ToList();
        Assert.Contains(ticks.SelectMany(t => t.Outcomes), o => o is BuffExpired { Buff.Def.Id: "stealth" });
        Assert.DoesNotContain(ticks, t => t.Of<IntentSet>().Any());       // wearing off never changes a plan
        Assert.Same(plan, grunt.Intent);
    }

    [Fact]
    public void Trigger_2_a_taunt_may_draw_the_plan_and_likelier_the_higher_the_taunter_scores()
    {
        int Drawn(double rogueThreat)
        {
            var drawn = 0;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var (b, grunt, w, _) = Standoff(rogueThreat: rogueThreat, firstSeed: seed * 1000);
                var r = Use(b, w, "taunt");                               // +60 Threat, then Threatening ×1.5
                Assert.Contains(r.Of<ThreatAdded>(), t => t.Target == w);
                if (Changes(r).Any(i => i.Why == IntentReason.Drawn))
                {
                    Assert.Equal(w, grunt.Intent!.Decision!.Target);
                    drawn++;
                }
            }
            return drawn;
        }
        var close = Drawn(30);                                           // the Warrior now outscores the Rogue: ~65%
        var far = Drawn(500);                                            // still behind: ~19%
        Assert.True(close > far + 10, $"{close} vs {far}");
        Assert.True(far > 0);                                             // a taunt can work even from behind
    }

    [Fact]
    public void Ordinary_damage_and_threat_changes_dont_change_a_plan()
    {
        var (b, grunt, w, rogue) = Standoff(rogueThreat: 30);
        var r = Use(b, w, "power_attack", grunt);                        // the Warrior's score now beats the Rogue's
        Assert.True(w.EffectiveThreat > rogue.EffectiveThreat);
        Assert.True(grunt.Alive);
        Assert.Empty(Changes(r));
        Assert.Equal(rogue, grunt.Intent!.Decision!.Target);
    }

    [Fact]
    public void Trigger_3_the_target_falling_replans()
    {
        // A second grunt finishes the Rogue that the first one planned to hit.
        for (ulong seed = 1; ; seed++)
        {
            var data = Repo;
            var other = Exposed(U(data, "goblin_grunt", "g2", Side.Enemy));
            var w3 = Exposed(U(data, "warrior", "w3", Side.Party));
            var r3 = Exposed(U(data, "rogue", "r3", Side.Party));
            var g3 = Exposed(U(data, "goblin_grunt", "g3", Side.Enemy));
            var grid = new BattleGrid();
            grid.Place(w3, P(0, 0));
            grid.Place(r3, P(0, 1));
            grid.Place(g3, E(0, 1));
            grid.Place(other, E(0, 0));
            var b3 = new Battle(data, [w3, r3, g3, other], seed, grid);
            r3.ThreatScore = 100;
            b3.Start();
            if (g3.Intent!.Decision!.Target != r3) continue;
            r3.TakeDamage(r3.Health - 1);
            var kill = Use(b3, other, "goblin_slash", r3);
            Assert.False(r3.Alive);
            Assert.Contains(Changes(kill), i => i.Unit == g3 && i.Why == IntentReason.TargetFell);
            Assert.Equal(w3, g3.Intent!.Decision!.Target);
            return;
        }
    }

    [Fact]
    public void Trigger_4_the_target_moving_out_of_reach_replans()
    {
        var (b, grunt, w, rogue) = Standoff(rogueThreat: 0, aimAtRogue: false);
        w.ActTicks = TurnClock.TurnThreshold;
        var r = b.ActAt(w, Repo.Actions["move"], P(1, 0));               // the back row: out of melee reach
        Assert.Equal(IntentReason.Blocked, Changes(r).Single().Why);
        Assert.Equal(rogue, grunt.Intent!.Decision!.Target);
    }

    [Fact]
    public void Gaining_fear_replans_at_once_and_losing_it_doesnt()
    {
        var data = With(actions: [Action("scare", ActionTarget.Enemy, "dread")]);
        var (b, grunt, w, _) = Standoff(data);
        var r = Use(b, w, "scare", grunt);
        Assert.Contains(Changes(r), i => i.Unit == grunt && i.Why == IntentReason.Feared);
        Assert.Contains(grunt.Intent!.Decision!.Action.Id, new[] { "defend", "move" });
        var ticks = Enumerable.Range(0, 5).Select(_ => b.BuffTick()).ToList();
        Assert.False(grunt.Afraid);
        Assert.DoesNotContain(ticks, t => t.Of<IntentSet>().Any());
    }

    [Fact]
    public void A_confused_enemy_shows_an_unknown_plan()
    {
        var data = With(
            actions: [Action("befuddle", ActionTarget.Enemy, "test_confusion")],
            buffs: [Buff("test_confusion", turns: 3) with { Cc = CcKind.Confusion }]);
        var (b, grunt, w, _) = Standoff(data);
        var r = Use(b, w, "befuddle", grunt);
        Assert.Contains(Changes(r), i => i.Unit == grunt && i.Why == IntentReason.Confused);
        Assert.True(grunt.Intent!.Unknown);
    }

    // ---- Whole battles ----

    [Fact]
    public void Enemies_do_what_they_showed_and_no_plan_goes_stale()
    {
        foreach (var encounter in Repo.EncounterList)
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var battle = EncounterSetup.Build(Repo, encounter, seed);
                BattleRunner.Run(battle);
                Assert.Equal(0, battle.StaleIntents);

                var shown = new Dictionary<Unit, Intent>();
                foreach (var r in battle.Results)
                {
                    var turnEnd = r.Of<IntentSet>().Any(i => i.Unit == r.Actor && i.Why == IntentReason.TurnEnd);
                    if (turnEnd && shown.TryGetValue(r.Actor, out var plan) && plan is { Unknown: false, Decision: { } d } && r.Action is { } act)
                    {
                        Assert.Equal(d.Action.Id, act.Id);
                        if (d.Tile is { } tile) Assert.Contains(r.Of<Moved>(), m => m.Unit == r.Actor && m.To == tile);
                        else if (d.Action.Target is ActionTarget.Enemy or ActionTarget.Ally) Assert.Equal(d.Target, r.Target);
                    }
                    foreach (var i in r.Of<IntentSet>()) shown[i.Unit] = i.Intent;
                }
            }
    }

    [Fact]
    public void Same_seed_same_intents_and_log()
    {
        string Log()
        {
            var battle = EncounterSetup.Build(Repo, Repo.Encounters["systems_showcase"], 42);
            BattleRunner.Run(battle);
            return CombatLog.Write(battle, LogLevel.Full);
        }
        Assert.Equal(Log(), Log());
        Assert.Contains("plans", Log());
    }
}
