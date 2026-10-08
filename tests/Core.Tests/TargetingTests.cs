using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using static EternalDungeon.Core.Tests.TestData;

namespace EternalDungeon.Core.Tests;

// M1 system 11: enemy targeting and the AI rules.
public class TargetingTests
{
    static Unit U(string def, string id, Side side) => new(id, Repo.Units[def], side, Repo);
    static Tile P(int row, int col) => new(BattleGrid.PartyArea, row, col);
    static Tile E(int row, int col) => new(BattleGrid.EnemyArea, row, col);

    [Fact]
    public void The_score_weighs_threat_against_vulnerability()
    {
        var loud = U("warrior", "loud", Side.Party);
        var hurt = U("rogue", "hurt", Side.Party);
        loud.ThreatScore = 100;                                           // threat 1.0 (the most)
        hurt.TakeDamage(50);                                              // 50 HP: the lowest, so vulnerability 1
        double Score(Unit u, double w) => UnitAi.Scores([loud, hurt], w).Single(s => s.Unit == u).Score;

        Assert.Equal(0.75 + 0.25 * 50 / 140.0, Score(loud, 0.75), 9);     // loud: 140 HP, vulnerability 50/140
        Assert.Equal(0.25, Score(hurt, 0.75), 9);
        Assert.Equal(0.25 + 0.75 * 50 / 140.0, Score(loud, 0.25), 9);
        Assert.Equal(0.75, Score(hurt, 0.25), 9);
    }

    [Fact]
    public void A_tank_being_hit_isnt_more_vulnerable_until_it_is_the_easiest_kill()
    {
        var tank = U("warrior", "tank", Side.Party);                     // 140 HP
        var mage = U("elementalist", "mage", Side.Party);                // 80 HP
        double Vulnerability(Unit u) => UnitAi.Scores([tank, mage], 0).Single(s => s.Unit == u).Score;
        Assert.Equal(1, Vulnerability(mage), 9);
        tank.TakeDamage(50);                                              // 90 HP: still more than the mage
        Assert.True(Vulnerability(tank) < Vulnerability(mage));
        tank.TakeDamage(30);                                              // 60 HP: now the easiest kill
        Assert.Equal(1, Vulnerability(tank), 9);
        tank.Stats.Add("curse", "vulnerability", 20);                    // the stat adds on top, in percent
        Assert.Equal(1.2, Vulnerability(tank), 9);
    }

    [Fact]
    public void The_pick_is_weighted_by_score_on_the_seeded_rng()
    {
        var loud = U("warrior", "loud", Side.Party);
        var quiet = U("warrior", "quiet", Side.Party);
        loud.ThreatScore = 100;
        var picks = Enumerable.Range(1, 200).Select(seed => UnitAi.PickTarget([loud, quiet], 0.5, new Rng((ulong)seed))).ToList();
        Assert.Equal(picks, Enumerable.Range(1, 200).Select(seed => UnitAi.PickTarget([loud, quiet], 0.5, new Rng((ulong)seed))));
        var share = picks.Count(p => p == loud) / 200.0;                  // scores 1 and 0.5: weights 1 and 0.25, so 80%
        Assert.InRange(share, 0.7, 0.9);

        var rng = new Rng(9);
        var probe = new Rng(9);
        Assert.Equal(loud, UnitAi.PickTarget([loud], 0.5, rng));          // one candidate: no roll
        Assert.Equal(probe.NextULong(), rng.NextULong());
    }

    [Fact]
    public void Threat_rises_with_damage_dealt_and_healing_done()
    {
        var warrior = U("warrior", "warrior", Side.Party);
        var grunt = U("goblin_grunt", "grunt", Side.Enemy);
        var shaman = U("goblin_shaman", "shaman", Side.Enemy);
        grunt.Stats.Add("test", "avoid", -0.9);
        var b = new Battle(Repo, [warrior, grunt, shaman], seed: 1);

        warrior.ActTicks = TurnClock.TurnThreshold;
        var hit = b.Act(warrior, Repo.Actions["attack"], grunt);
        Assert.Equal(20 + hit.Of<Damaged>().Single().Breakdown.Final, warrior.ThreatScore);   // Starting Threat 20

        shaman.ActTicks = TurnClock.TurnThreshold;
        var heal = b.Act(shaman, Repo.Actions["mend"], grunt);
        Assert.True(shaman.ThreatScore >= heal.Of<Healed>().Single().Amount);   // the full heal, overheal included
    }

    [Fact]
    public void Stealth_hides_threat()
    {
        var rogue = U("rogue", "rogue", Side.Party);
        rogue.ThreatScore = 80;
        rogue.Stats.Add("stealth", "threatening", -1);
        Assert.Equal(0, rogue.EffectiveThreat);
    }

    [Fact]
    public void Enemies_only_pick_targets_in_range()
    {
        var warrior = U("warrior", "warrior", Side.Party);
        var mage = U("elementalist", "mage", Side.Party);
        var grunt = U("goblin_grunt", "grunt", Side.Enemy);
        var grid = new BattleGrid();
        grid.Place(warrior, P(0, 0));
        grid.Place(mage, P(1, 2));
        grid.Place(grunt, E(0, 1));
        var b = new Battle(Repo, [warrior, mage, grunt], seed: 1, grid);
        mage.TakeDamage(70);                                              // very vulnerable, but out of melee reach

        var d = UnitAi.Decide(b, grunt)!;
        Assert.Equal(warrior, d.Target);
    }

    [Fact]
    public void The_shaman_heals_a_hurt_ally_and_hexes_otherwise()
    {
        var warrior = U("warrior", "warrior", Side.Party);
        var grunt = U("goblin_grunt", "grunt", Side.Enemy);
        var shaman = U("goblin_shaman", "shaman", Side.Enemy);
        var b = new Battle(Repo, [warrior, grunt, shaman], seed: 1);

        Assert.Equal("rotting_hex", UnitAi.Decide(b, shaman)!.Action.Id);
        grunt.TakeDamage(grunt.MaxHealth / 2 + 5);                        // below half
        var d = UnitAi.Decide(b, shaman)!;
        Assert.Equal(("mend", grunt), (d.Action.Id, d.Target));
    }

    [Fact]
    public void The_chief_cries_when_the_buff_is_missing()
    {
        var warrior = U("warrior", "warrior", Side.Party);
        var chief = U("goblin_chief", "chief", Side.Enemy);
        var b = new Battle(Repo, [warrior, chief], seed: 1);

        Assert.Equal("war_cry", UnitAi.Decide(b, chief)!.Action.Id);
        chief.ActTicks = TurnClock.TurnThreshold;
        b.Act(chief, Repo.Actions["war_cry"], null);
        Assert.Equal("chief_cleave", UnitAi.Decide(b, chief)!.Action.Id);
    }

    [Fact]
    public void A_melee_hero_in_the_back_row_steps_forward()
    {
        var rogue = U("rogue", "rogue", Side.Party);
        var warrior = U("warrior", "warrior", Side.Party);
        var grunt = U("goblin_grunt", "grunt", Side.Enemy);
        var grid = new BattleGrid();
        grid.Place(rogue, P(0, 0));
        grid.Place(warrior, P(1, 1));
        grid.Place(grunt, E(0, 1));
        var b = new Battle(Repo, [rogue, warrior, grunt], seed: 1, grid);

        var d = UnitAi.Decide(b, warrior)!;                               // can't reach from row 1
        Assert.Equal(("move", P(0, 1)), (d.Action.Id, d.Tile));
    }

    [Fact]
    public void Ai_weights_must_stay_within_75_percent()
    {
        var e = TestData.TablesFail(
            ("actions", """[{ "id": "wait", "name": "Wait", "target": "self", "ap_cost": 100 }]"""),
            ("ai_profiles", """[{ "id": "zealot", "name": "Zealot", "threat_weight": 0.9 }]"""),
            ("ai_rules", """[{ "profile": "zealot", "order": 1, "action": "wait" }]"""));
        Assert.Equal("ai_profiles.json", e.File);
        Assert.Equal("[0].threat_weight", e.Field);
    }
}
