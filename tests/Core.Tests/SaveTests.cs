using EternalDungeon.Core.Data;
using EternalDungeon.Core.Exploration;
using static EternalDungeon.Core.Tests.TestData;

namespace EternalDungeon.Core.Tests;

/// <summary>Constant saving (Anchor: Dungeons › Saving; M3A brief §9): quit and reload anywhere, mid-Event and
/// mid-battle included, and the run continues identically.</summary>
public class SaveTests
{
    /// <summary>A save taken during play: the JSON, and where the run stood (actions done, battle decisions made).</summary>
    sealed record Snapshot(string Json, int Commands, int BattleChoices);

    /// <summary>
    /// Plays Dungeon 0 on the auto-player's rules, but drives every battle one hero decision at a time (the AI decides),
    /// saving before every action and at every hero decision.
    /// </summary>
    static (DungeonRun Run, List<Snapshot> Saves) Play(ulong seed)
    {
        var run = new DungeonRun(Repo, "dungeon_0", seed);
        var saves = new List<Snapshot>();
        void Save() => saves.Add(new Snapshot(SaveGame.Of(run).ToJson(), run.Commands.Count, run.Battle?.Session.Choices.Count ?? 0));
        var policy = new RunPolicy();
        Save();
        policy.Play(run, maxActions: 0);                            // spends the first points, then arrives
        for (var i = 0; i < 5000 && !run.Over; i++)
        {
            Save();
            if (run.State == RunState.Battle)
            {
                var s = run.Battle!.Session;
                s.Advance();
                if (s.Over) run.FinishBattle();
                else
                {
                    Save();                                         // mid-battle, a hero waiting
                    s.ChooseByAi();
                }
            }
            else if (!policy.Step(run)) break;
        }
        return (run, saves);
    }

    static List<string> Log(DungeonRun run) => [.. run.Results.SelectMany(r => RunLog.Lines(Repo, r))];

    [Fact]
    public void Reloading_anywhere_continues_the_run_identically()
    {
        var (original, saves) = Play(seed: 3);
        Assert.True(original.Over);
        Assert.Contains(saves, s => s.BattleChoices > 0);           // some saves are mid-battle
        var commands = original.Commands;
        var finalSave = SaveGame.Of(original).ToJson();
        var log = Log(original);

        // Every 5th save, plus the last few: restore, then carry on with the original's actions.
        var picks = saves.Where((_, i) => i % 5 == 0 || i >= saves.Count - 3).ToList();
        foreach (var snap in picks)
        {
            var save = SaveGame.Parse(snap.Json);
            Assert.False(save.DataChanged(Repo));
            var run = save.Restore(Repo);
            Assert.Equal(snap.Commands, run.Commands.Count);
            var next = snap.Commands;
            if (run.Battle is { } fight)
            {
                // Finish the battle with the decisions the original made after this save.
                var battle = commands[next];
                Assert.Equal("battle", battle.Do);
                fight.Session.Script(battle.Battle!.Skip(snap.BattleChoices));
                fight.Session.Advance();
                run.FinishBattle();
                next++;
            }
            for (var i = next; i < commands.Count; i++)
                SaveGame.Apply(run, commands[i]);
            Assert.Equal(log, Log(run));
            Assert.Equal(finalSave, SaveGame.Of(run).ToJson());
        }
    }

    [Fact]
    public void A_save_mid_battle_comes_back_at_the_same_hero_decision()
    {
        var (_, saves) = Play(seed: 5);
        var mid = saves.First(s => s.BattleChoices > 2);
        var run = SaveGame.Parse(mid.Json).Restore(Repo);
        Assert.Equal(RunState.Battle, run.State);
        Assert.NotNull(run.Battle!.Session.Awaiting);
        Assert.Equal(mid.BattleChoices, run.Battle.Session.Choices.Count);
        // The enemies' plans came back with it.
        Assert.All(run.Battle.Session.Battle.Units.Where(u => u.Alive && u.Side == Combat.Side.Enemy), u => Assert.NotNull(u.Intent));
    }

    [Fact]
    public void A_save_round_trips_through_json()
    {
        var (_, saves) = Play(seed: 2);
        foreach (var snap in saves.Where((_, i) => i % 25 == 0))
            Assert.Equal(snap.Json, SaveGame.Parse(snap.Json).ToJson());
    }

    [Fact]
    public void A_save_made_with_other_data_is_recognised()
    {
        var run = new DungeonRun(Repo, "dungeon_0", 1);
        run.Start();
        var save = SaveGame.Parse(SaveGame.Of(run).ToJson());
        Assert.False(save.DataChanged(Repo));
        Assert.True(save.DataChanged(RunTestData.Data));             // the repo data plus the test dungeon
        Assert.NotEqual("", Repo.Fingerprint);
    }

    [Fact]
    public void A_save_out_of_step_with_the_data_says_so()
    {
        var run = new DungeonRun(Repo, "dungeon_0", 1);
        run.Start();
        var json = SaveGame.Of(run).ToJson().Replace("\"do\": \"start\" }", "\"do\": \"start\" },\n  { \"do\": \"explore\", \"node\": \"gr_fortress\" }");
        var e = Assert.Throws<SaveException>(() => SaveGame.Parse(json).Restore(Repo));
        Assert.Contains("action 2 (explore)", e.Message);
        Assert.Throws<SaveException>(() => SaveGame.Parse("{ not json"));
    }
}
