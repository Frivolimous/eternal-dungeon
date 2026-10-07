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

    /// <summary>
    /// Core is front-relative: the same fight on a board whose areas face each other along other edges (the party's
    /// area turned on its side and facing with its last column, the enemy's facing with its last row) plays out
    /// exactly the same. Range, Move (also the Rogue's into the enemy area), Push/Pull and collapse all run in these fights.
    /// </summary>
    [Fact]
    public void The_same_fight_plays_identically_on_a_rotated_board()
    {
        static BattleGrid Rotated() => new(
            [new BattleArea(BattleGrid.PartyArea, Side.Party, Cols: 2, Rows: 3), new BattleArea(BattleGrid.EnemyArea, Side.Enemy, 3, 2)],
            [new Front(BattleGrid.PartyArea, Edge.ColEnd, BattleGrid.EnemyArea, Edge.RowEnd)]);

        var seen = new HashSet<string>();
        foreach (var encounter in TestData.Repo.EncounterList)
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var normal = EncounterSetup.Build(TestData.Repo, encounter, seed);
                var rotated = EncounterSetup.Build(TestData.Repo, encounter, seed, Rotated());
                BattleRunner.Run(normal);
                BattleRunner.Run(rotated);
                Assert.Equal(CombatLog.Write(normal, LogLevel.Full), CombatLog.Write(rotated, LogLevel.Full));
                Assert.NotEqual(normal.Grid.AnchorOf(normal.Units[0]), rotated.Grid.AnchorOf(rotated.Units[0]));
                foreach (var m in rotated.Results.SelectMany(r => r.Of<Moved>()))
                    seen.Add(m.Why switch
                    {
                        "Move" when rotated.Grid.SideOf(m.To) != m.Unit.Side => "into the enemy area",
                        "Move" or "collapse" => m.Why,
                        _ => "shove",
                    });
            }
        Assert.True(seen.SetEquals(["Move", "into the enemy area", "collapse", "shove"]), string.Join(", ", seen));
    }

    /// <summary>Every log line comes from strings.csv: a missing key would show as [key].</summary>
    [Fact]
    public void Logs_have_no_missing_strings()
    {
        foreach (var encounter in TestData.Repo.Encounters.Keys)
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var log = CombatLog.Write(Play(encounter, seed), LogLevel.Full);
                Assert.DoesNotMatch(@"\[[a-z_]+\.[a-z_.]+\]", log);
                Assert.DoesNotMatch(@"\{[a-z_]+\}", log);
            }
    }

    [Fact]
    public void Every_encounter_runs_to_a_finish()
    {
        Assert.Equal(["goblin_patrol", "brute_squad", "chief_hall", "systems_showcase"], TestData.Repo.EncounterList.Select(e => e.Id));
        foreach (var encounter in TestData.Repo.Encounters.Keys)
            for (ulong seed = 1; seed <= 25; seed++)
                Assert.NotNull(Play(encounter, seed).Winner);
    }

    /// <summary>
    /// The Systems Showcase exists so every M1 system shows up in a typical seed. Each must fire in at least half
    /// of 20 seeds (they currently fire in 15–20).
    /// </summary>
    [Fact]
    public void The_showcase_exercises_every_system()
    {
        var checks = new Dictionary<string, Func<Outcome, bool>>
        {
            ["Slow"] = o => o is BuffApplied { Buff.Def.Id: "chill" },
            ["Stealth move"] = o => o is Moved { Why: "Move" } m && InEnemyArea(m.Unit, m.To),
            ["Move"] = o => o is Moved { Why: "Move" } m && !InEnemyArea(m.Unit, m.To),
            ["Defend"] = o => o is BuffApplied { Buff.Def.Id: "guard" },
            ["Stagger break"] = o => o is Staggered { Broke: true },
            ["Cast interrupt"] = o => o is Interrupted,
            ["Proc"] = o => o is ProcRolled { Roll.Success: true },
            ["Fear"] = o => o is BuffApplied { Buff.Def.Cc: CcKind.Fear },
            ["Push"] = o => o is Moved { Why: "Push" },
        };
        var seen = checks.Keys.ToDictionary(k => k, _ => 0);
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var outcomes = Play("systems_showcase", seed).Results.SelectMany(r => r.Outcomes).ToList();
            foreach (var (name, check) in checks)
                if (outcomes.Any(check)) seen[name]++;
        }
        Assert.All(seen, kv => Assert.True(kv.Value >= 10, $"{kv.Key} fired in only {kv.Value} of 20 seeds"));
    }

    /// <summary>On the default board (one party and one enemy area).</summary>
    static bool InEnemyArea(Unit unit, Tile tile) => (tile.Area == BattleGrid.EnemyArea) != (unit.Side == Side.Enemy);

    [Fact]
    public void Duplicate_units_are_numbered_and_placed_as_the_data_says()
    {
        var battle = EncounterSetup.Build(TestData.Repo, TestData.Repo.Encounters["goblin_patrol"], 1);
        Assert.Equal(["Rogue", "Warrior", "Elementalist", "Goblin Grunt #1", "Goblin Grunt #2", "Goblin Archer", "Goblin Shaman"],
            battle.Units.Select(u => u.Name));
        Assert.Equal(new Tile(BattleGrid.EnemyArea, 1, 2), battle.Grid.AnchorOf(battle.Unit("goblin_shaman#1")));
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
        static DataException Fails(params string[] enemies) => TestData.TablesFail(
            ("stats", """[{ "id": "health", "name": "Health", "group": "character", "combine": "add" }]"""),
            ("actions", """[{ "id": "wait", "name": "Wait", "target": "self", "ap_cost": 100 }]"""),
            ("ai_profiles", """[{ "id": "idle", "name": "Idle", "threat_weight": 0.5 }]"""),
            ("ai_rules", """[{ "profile": "idle", "order": 1, "action": "wait" }]"""),
            ("units", """
                [{ "id": "imp", "name": "Imp", "size": 1, "health": 5, "actions": ["wait"], "ai": "idle" },
                 { "id": "ogre", "name": "Ogre", "size": 2, "health": 50, "actions": ["wait"], "ai": "idle" }]
                """),
            ("encounters", """[{ "id": "e", "name": "E" }]"""),
            ("encounter_units", "[" + string.Join(", ", ["""{ "encounter": "e", "side": "party", "order": 1, "unit": "imp", "row": 0, "col": 0 }""",
                .. enemies.Select((x, i) => $$"""{ "encounter": "e", "side": "enemy", "order": {{i + 1}}, {{x}} }""")]) + "]"));

        Assert.Contains("doesn't fit", Fails("""  "unit": "ogre", "row": 0, "col": 2""").Message);
        Assert.Contains("overlaps", Fails("""  "unit": "ogre", "row": 0, "col": 0""", """ "unit": "imp", "row": 1, "col": 1""").Message);
        var unknown = Fails("""  "unit": "dragon", "row": 0, "col": 0""");
        Assert.Equal(("encounter_units.json", "[1].unit"), (unknown.File, unknown.Field));
        Assert.Contains("1 to 6", Fails().Message);
    }
}
