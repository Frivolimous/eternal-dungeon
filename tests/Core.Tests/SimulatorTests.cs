using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Tests;

// The battle simulator: determinism, the starter encounters, logs and the batch summary.
public class SimulatorTests
{
    static Battle Play(string encounter, ulong seed)
    {
        var battle = EncounterSetup.Build(TestData.Repo, TestData.Repo.Encounters[encounter], seed);
        BattleRunner.Run(battle);
        return battle;
    }

    [Fact]
    public void The_same_seed_always_produces_the_same_combat_log()
    {
        foreach (var encounter in TestData.Repo.Encounters.Keys)
        {
            var first = CombatLog.Write(Play(encounter, 42), LogLevel.Full);
            var second = CombatLog.Write(Play(encounter, 42), LogLevel.Full);
            Assert.Equal(first, second);
        }
        Assert.NotEqual(CombatLog.Write(Play("goblin_patrol", 1)), CombatLog.Write(Play("goblin_patrol", 2)));
    }

    [Fact]
    public void Every_starter_encounter_runs_to_a_finish()
    {
        Assert.Equal(["goblin_patrol", "brute_squad", "chief_hall"], TestData.Repo.EncounterList.Select(e => e.Id));
        foreach (var encounter in TestData.Repo.Encounters.Keys)
            for (ulong seed = 1; seed <= 25; seed++)
                Assert.NotNull(Play(encounter, seed).Winner);
    }

    [Fact]
    public void Duplicate_units_are_numbered_and_placed_as_the_data_says()
    {
        var battle = EncounterSetup.Build(TestData.Repo, TestData.Repo.Encounters["goblin_patrol"], 1);
        Assert.Equal(["Rogue", "Warrior", "Elementalist", "Goblin Grunt #1", "Goblin Grunt #2", "Goblin Archer", "Goblin Shaman"],
            battle.Units.Select(u => u.Name));
        Assert.Equal(new Tile(Side.Enemy, 1, 2), battle.Grid.AnchorOf(battle.Unit("goblin_shaman#1")));
    }

    [Fact]
    public void The_brief_log_reads_like_the_brief()
    {
        var log = CombatLog.Write(Play("goblin_patrol", 42));
        var lines = log.Split('\n');
        Assert.Matches(@"^\[T \d+\.\d\d\] .+ → .+", lines[0]);
        Assert.Contains(lines, l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^ {9}hit \(\d+%\) · \d+ dmg \(Physical\) · .+ HP \d+ → \d+"));
        Assert.Contains("begins casting Fire Bolt", log);
        Assert.Matches(@"\[T \d+\.\d\d\] (Victory|Defeat)", lines.Last(l => l.Length > 0));
    }

    [Fact]
    public void The_batch_summary_reports_the_brief_numbers()
    {
        var summary = new BatchSummary();
        for (ulong seed = 1; seed <= 20; seed++) summary.Add(Play("brute_squad", seed));
        var report = summary.Report();
        foreach (var heading in new[] { "Win rate", "Battle length", "Party HP left", "Damage dealt per battle",
                     "Actions used per battle", "Effects and CC applied per battle", "Stagger breaks" })
            Assert.Contains(heading, report);
        Assert.Equal(20, summary.Battles);
    }

    [Fact]
    public void Encounter_placements_are_checked()
    {
        static DataException Fails(string enemies) => Assert.Throws<DataException>(() => DataLoader.Load(DataSource.FromFiles(
            new Dictionary<string, string>
            {
                [DataLoader.TagsFile] = "[]",
                [DataLoader.StatsFile] = """[{ "id": "health", "name": "Health", "group": "character", "combine": "add" }]""",
                [DataLoader.CompoundStatsFile] = "[]",
                [DataLoader.EffectsFile] = "[]",
                [DataLoader.ProcsFile] = "[]",
                [DataLoader.ActionsFile] = """[{ "id": "wait", "name": "Wait", "tags": [], "target": "self", "apCost": 100 }]""",
                [DataLoader.AiProfilesFile] = """[{ "id": "idle", "name": "Idle", "threatWeight": 0.5, "rules": [{ "action": "wait" }] }]""",
                [DataLoader.UnitsFile] = """
                    [{ "id": "imp", "name": "Imp", "size": 1, "stats": { "health": 5 }, "actions": ["wait"], "ai": "idle" },
                     { "id": "ogre", "name": "Ogre", "size": 2, "stats": { "health": 50 }, "actions": ["wait"], "ai": "idle" }]
                    """,
                [DataLoader.EncountersFile] = $$"""[{ "id": "e", "name": "E", "party": [{ "unit": "imp", "row": 0, "col": 0 }], "enemies": {{enemies}} }]""",
                [DataLoader.DefaultsFile] = """{ "unitStats": [] }""",
            })));

        Assert.Contains("doesn't fit", Fails("""[{ "unit": "ogre", "row": 0, "col": 2 }]""").Message);
        Assert.Contains("overlaps", Fails("""[{ "unit": "ogre", "row": 0, "col": 0 }, { "unit": "imp", "row": 1, "col": 1 }]""").Message);
        Assert.Equal("[0].enemies[0].unit", Fails("""[{ "unit": "dragon", "row": 0, "col": 0 }]""").Field);
        Assert.Contains("1 to 6", Fails("[]").Message);
    }
}
