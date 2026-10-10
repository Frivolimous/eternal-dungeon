using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using EternalDungeon.Core.Exploration;
using static EternalDungeon.Core.Tests.TestData;

namespace EternalDungeon.Core.Tests;

/// <summary>XP, levels, skill points, prerequisites and masteries (Anchor: Classes › Skill points, Masteries; M3A
/// brief §6).</summary>
public class ProgressionTests
{
    static GameData D => RunTestData.Data;

    static DungeonRun Run()
    {
        var run = new DungeonRun(D, "t_dungeon", 1);
        return run;
    }

    [Fact]
    public void A_new_hero_starts_at_level_1_with_one_point_to_spend_before_the_run_starts()
    {
        var run = Run();
        var w = run.Hero("warrior");
        Assert.Equal((1, 0, 1), (w.Level, w.Xp, w.SkillPoints));
        var r = run.SpendPoint("warrior", "vigor");
        Assert.Equal(1, w.SkillLevel("vigor"));
        Assert.Equal(0, w.SkillPoints);
        Assert.Equal("power_attack", Assert.Single(r.Of<MasteryUnlocked>()).Mastery.Id);
        Assert.Equal("no_skill_points", w.CantRaise("battle_might"));
    }

    [Fact]
    public void The_level_table_turns_xp_into_levels()
    {
        Assert.Equal([0, 20, 50, 90], Repo.Levels.Take(4).Select(l => l.Xp));
        Assert.Equal(1, Repo.LevelFor(19));
        Assert.Equal(2, Repo.LevelFor(20));
        Assert.Equal(4, Repo.LevelFor(100));
        Assert.Equal(20, Repo.LevelFor(1_000_000));
    }

    [Fact]
    public void Prerequisites_and_maximum_levels_hold()
    {
        var run = Run();
        var w = run.Hero("warrior");
        w.Xp = 1_000_000;                                           // level 20: 20 points
        Assert.Equal("needs_prerequisite", w.CantRaise("battle_might"));
        Assert.Equal("not_in_tree", w.CantRaise("shadow_mastery"));
        Assert.Equal("not_in_tree", w.CantRaise("power_attack"));     // masteries aren't bought
        for (var i = 0; i < 5; i++) run.SpendPoint("warrior", "vigor");
        Assert.Equal("skill_maxed", w.CantRaise("vigor"));
        Assert.Null(w.CantRaise("battle_might"));
        Assert.Equal("needs_prerequisite", w.CantRaise("imposing_presence"));   // needs Weapon Mastery
    }

    [Fact]
    public void Masteries_unlock_at_1_6_and_11_tree_points()
    {
        var run = Run();
        var w = run.Hero("warrior");
        w.Xp = 1_000_000;
        var unlocked = new List<(int Points, string Mastery)>();
        string[] order = ["vigor", "vigor", "vigor", "vigor", "vigor", "weapon_mastery", "weapon_mastery", "weapon_mastery",
            "weapon_mastery", "weapon_mastery", "fortitude"];
        foreach (var skill in order)
            foreach (var m in run.SpendPoint("warrior", skill).Of<MasteryUnlocked>())
                unlocked.Add((w.TreePoints, m.Mastery.Id));
        Assert.Equal([(1, "power_attack"), (6, "defensive_blow"), (11, "colossal_strike")], unlocked);
    }

    [Fact]
    public void Skills_raise_the_maximums_and_the_current_values_with_them()
    {
        var run = Run();
        var w = run.Hero("warrior");
        var max = w.MaxHealth;
        w.Health = max - 30;
        run.SpendPoint("warrior", "vigor");                         // +8 Health a level, + Strength
        Assert.Equal(max + 8, w.MaxHealth);
        Assert.Equal(max - 22, w.Health);
    }

    [Fact]
    public void Skills_and_masteries_go_into_battle_with_the_hero()
    {
        var run = Run();
        run.SpendPoint("rogue", "shadow_mastery");
        run.Start();
        run.Choose("bash");
        run.Explore("t_b");
        run.Continue();
        var rogue = run.Battle!.Session.Battle.Unit("rogue");
        Assert.Contains("stealth_move", rogue.ActionIds);
        Assert.DoesNotContain("move", rogue.ActionIds);                // the Rogue's Move replaces it
        var plain = new Unit("x", D.Units["rogue"], Side.Party, D);
        Assert.Equal(plain.Stats.GetCompound("accuracy") + 3, rogue.Stats.GetCompound("accuracy"));
        Assert.DoesNotContain("power_attack", run.Battle.Session.Battle.Unit("warrior").ActionIds);   // no point spent
    }

    [Fact]
    public void A_won_battle_shares_its_xp_among_the_heroes_standing()
    {
        var run = Run();
        run.Start();
        run.Choose("bash");
        run.Explore("t_b");
        run.Continue();
        var s = run.Battle!.Session;
        s.Advance();
        var fled = s.Awaiting!.Id;
        s.Choose(new Choice(fled, "flee"));
        s.AutoBattle = true;
        s.Advance();
        var r = run.FinishBattle();
        var gains = r.Of<XpGained>().ToList();
        Assert.Equal(2, gains.Count);                               // the hero who fled gets none
        Assert.All(gains, g => Assert.Equal(D.RunRules.XpMajor / 2, g.Amount));
        Assert.DoesNotContain(gains, g => g.Hero.Id == fled);
        Assert.All(gains, g => Assert.Contains(r.Of<LevelUp>(), l => l.Hero == g.Hero && l.Level == 2));
    }

    [Fact]
    public void A_party_rows_skills_give_it_its_masteries_in_a_plain_encounter()
    {
        var battle = EncounterSetup.Build(Repo, Repo.Encounters["systems_showcase"], 1);
        var warrior = battle.Units.First(u => u.Def.Id == "warrior");
        Assert.Contains("power_attack", warrior.ActionIds);
        Assert.Equal(warrior.MaxHealth, warrior.Health);           // full at its new maximum
    }

    [Fact]
    public void The_auto_player_reaches_about_level_4_before_the_final_boss()
    {
        var levels = new List<double>();
        for (ulong seed = 1; seed <= 5; seed++)
        {
            var run = new DungeonRun(Repo, "dungeon_0", seed);
            new RunPolicy
            {
                OnBattle = r =>
                {
                    if (r.Battle!.Session.Encounter.Id == "d0_goblin_chief") levels.Add(r.Battle.Heroes.Average(h => h.Level));
                },
            }.Play(run);
        }
        Assert.NotEmpty(levels);
        Assert.InRange(levels.Average(), 3.5, 4.5);
    }

    [Fact]
    public void Skill_data_errors_name_the_row()
    {
        Assert.Contains("must come earlier", Fails("""{ "id": "b", "name": "B", "class": "warrior", "kind": "tree", "order": 1, "requires": "vigor" }"""));
        Assert.Contains("unlocks at 1, 6, 11", Fails("""{ "id": "b", "name": "B", "class": "warrior", "kind": "mastery", "order": 9, "points": 3 }"""));
        Assert.Contains("two masteries at 1 points", Fails("""{ "id": "b", "name": "B", "class": "warrior", "kind": "mastery", "order": 9, "points": 1 }"""));
    }

    /// <summary>Loads the repo data with one extra skill row; returns the error.</summary>
    static string Fails(string row)
    {
        var files = RunTestData.RepoFiles();
        RunTestData.AddRows(files, "skills", "  " + row);
        return Assert.Throws<DataException>(() => RunTestData.Load(files)).Message;
    }
}
