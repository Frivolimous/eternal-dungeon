using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using static EternalDungeon.Core.Tests.TestData;

namespace EternalDungeon.Core.Tests;

// Rules decided 2026-10-08: attacking breaks Stealth, Confusion takes the turn at random, and the forward collapse
// leaves a Rogue in the enemy area out of the step forward.
public class RogueAndConfusionTests
{
    static Unit U(string def, string id, Side side, GameData? data = null) => new(id, (data ?? Repo).Units[def], side, data ?? Repo);
    static Tile P(int row, int col) => new(BattleGrid.PartyArea, row, col);
    static Tile E(int row, int col) => new(BattleGrid.EnemyArea, row, col);

    // ---- Stealth ----

    [Fact]
    public void Attacking_breaks_stealth_hit_or_miss_but_other_actions_dont()
    {
        foreach (var avoid in new[] { -0.9, 0.99 })                       // a sure hit, then a near-sure miss
        {
            var rogue = U("rogue", "rogue", Side.Party);
            var grunt = U("goblin_grunt", "grunt", Side.Enemy);
            grunt.Stats.Add("test", "avoid", avoid);
            var grid = new BattleGrid();
            grid.Place(rogue, P(0, 0));
            grid.Place(grunt, E(0, 0));
            var b = new Battle(Repo, [rogue, grunt], seed: 1, grid);

            rogue.ActTicks = TurnClock.TurnThreshold;
            b.ActAt(rogue, Repo.Actions["stealth_move"], P(0, 1));
            Assert.Contains(rogue.Buffs, x => x.Def.Id == "stealth");
            rogue.ActTicks = TurnClock.TurnThreshold;
            var r = b.Act(rogue, Repo.Actions["dagger_attack"], grunt);
            Assert.DoesNotContain(rogue.Buffs, x => x.Def.Id == "stealth");
            Assert.Contains(r.Of<BuffExpired>(), e => e.Buff.Def.Id == "stealth");
        }

        var r2 = U("rogue", "rogue2", Side.Party);
        var b2 = new Battle(Repo, [r2, U("goblin_grunt", "g2", Side.Enemy)], seed: 1);
        r2.ActTicks = TurnClock.TurnThreshold;
        b2.ActAt(r2, Repo.Actions["stealth_move"], b2.Grid.MoveOptions(r2).First());
        r2.ActTicks = TurnClock.TurnThreshold;
        b2.Act(r2, Repo.Actions["defend"], r2);                            // not an attack: Stealth stays
        Assert.Contains(r2.Buffs, x => x.Def.Id == "stealth");
    }

    // ---- Confusion ----

    static GameData Confusing => With(
        actions: [Action("confuse", ActionTarget.Ally, new EffectRef("test_confusion", EffectAim.Target))],
        effects: [Buff("test_confusion", turns: 50) with { Cc = CcKind.Confusion }]);

    [Fact]
    public void A_confused_unit_picks_a_random_action_and_any_target_in_reach_allies_included()
    {
        var data = Confusing;
        var hitAlly = false;
        var hitEnemy = false;
        var other = false;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var w = U("warrior", "w", Side.Party, data);
            var rogue = U("rogue", "r", Side.Party, data);
            var grunt = U("goblin_grunt", "g", Side.Enemy, data);
            var grid = new BattleGrid();
            grid.Place(w, P(0, 0));
            grid.Place(rogue, P(0, 1));                                    // next to the Warrior
            grid.Place(grunt, E(0, 0));
            var b = new Battle(data, [w, rogue, grunt], seed, grid);
            rogue.ActTicks = TurnClock.TurnThreshold;
            b.Act(rogue, data.Actions["confuse"], w);

            var d = UnitAi.Decide(b, w)!;
            if (d.Target == rogue) hitAlly = true;
            else if (d.Target == grunt) hitEnemy = true;
            else other = true;                                            // Defend, a Move, or nothing in reach
            if (d.Target is { } t && t != w)
            {
                w.ActTicks = TurnClock.TurnThreshold;
                b.Act(w, d.Action, t);                                     // the battle accepts an ally as the target
            }
        }
        Assert.True(hitAlly && hitEnemy && other);
    }

    [Fact]
    public void A_confused_hero_never_waits_for_the_player_and_the_battle_still_replays()
    {
        // The Warrior confuses itself at the start of the fight (a fight-start proc), so a replay rebuilds it from data.
        var c = Confusing;
        var data = new GameData(c.TagList, c.StatList, c.CompoundList,
            [.. c.UnitList.Select(u => u.Id == "warrior" ? u with { Procs = ["test_confused_start"] } : u)],
            c.ActionList, c.EffectList, c.AiProfileList, c.EncounterList, c.UnitDefaults,
            [.. c.ProcList, Proc("test_confused_start", ProcTrigger.FightStart, ProcTarget.Self) with { Effect = "test_confusion" }],
            c.DefaultActions, c.Text);
        var s = new BattleSession(data, data.Encounters["goblin_patrol"], 5);
        s.Advance();
        var warrior = s.Battle.Units.First(u => u.Def.Id == "warrior");
        Assert.True(warrior.Has(CcKind.Confusion));
        var warriorActed = false;
        while (!s.Over)
        {
            Assert.False(s.Awaiting == warrior && warrior.Has(CcKind.Confusion));
            var h = s.Awaiting!;
            var d = UnitAi.ValidTargets(s.Battle, h, data.Actions["attack"]);
            var choice = h.CantUse(data.Actions["attack"]) is null && d.Count > 0
                ? new Choice(h.Id, "attack", d[0].Id)
                : new Choice(h.Id, "defend");
            s.Choose(choice);
            s.Advance();
        }
        warriorActed = s.Battle.Results.Any(r => r.Actor == warrior && r.Action is not null);
        Assert.True(warriorActed);                                       // it acted, on its own
        Assert.DoesNotContain(s.Choices, ch => ch.Unit == warrior.Id);   // and none of its turns were choices
        var replayed = Replay.Parse(s.ToReplay().ToJson()).Play(data);
        Assert.Equal(CombatLog.Write(s.Battle, LogLevel.Full), CombatLog.Write(replayed.Battle, LogLevel.Full));
    }

    // ---- The forward collapse with a Rogue in the enemy area ----

    static (BattleGrid Grid, Battle Battle, Unit Rogue, Unit Grunt) RogueInFront(Tile rogueAt, Tile gruntAt)
    {
        var rogue = U("rogue", "rogue", Side.Party);
        var grunt = U("goblin_grunt", "grunt", Side.Enemy);
        var w = U("warrior", "w", Side.Party);
        var grid = new BattleGrid();
        grid.Place(w, P(0, 0));
        grid.Place(rogue, rogueAt);
        grid.Place(grunt, gruntAt);
        return (grid, new Battle(Repo, [w, rogue, grunt], seed: 1, grid), rogue, grunt);
    }

    [Fact]
    public void A_rogue_in_the_enemy_front_row_doesnt_stop_the_enemies_collapsing()
    {
        var (grid, _, rogue, grunt) = RogueInFront(E(0, 0), E(1, 2));
        var (moves, grown) = grid.Collapse(BattleGrid.EnemyArea);
        Assert.Equal(E(0, 2), grid.AnchorOf(grunt));                     // the enemies step forward
        Assert.Equal(E(0, 0), grid.AnchorOf(rogue));                     // the Rogue keeps its free tile
        Assert.Equal([(grunt, E(1, 2), E(0, 2), "collapse")], moves);
        Assert.Empty(grown);
    }

    [Fact]
    public void A_rogue_in_the_way_goes_to_the_front_most_free_tile_nearest_its_lane()
    {
        var (grid, _, rogue, grunt) = RogueInFront(E(0, 1), E(1, 1));
        var (moves, _) = grid.Collapse(BattleGrid.EnemyArea);
        Assert.Equal(E(0, 1), grid.AnchorOf(grunt));
        Assert.Equal(E(0, 0), grid.AnchorOf(rogue));                     // front row, one lane over
        Assert.Contains((rogue, E(0, 1), E(0, 0), "displaced"), moves);
    }

    [Fact]
    public void A_collapse_triggered_by_a_death_moves_the_rogue_in_the_log()
    {
        var (grid, b, rogue, grunt) = RogueInFront(E(0, 1), E(1, 1));
        var front = U("goblin_grunt", "front", Side.Enemy);
        // Rebuild with a second grunt in the front row that the Rogue kills.
        grid = new BattleGrid();
        var w = b.Units[0];
        grid.Place(w, P(0, 0));
        grid.Place(rogue, E(1, 0));
        grid.Place(front, E(0, 1));
        grid.Place(grunt, E(1, 1));
        b = new Battle(Repo, [w, rogue, front, grunt], seed: 1, grid);
        front.TakeDamage(front.MaxHealth - 1);
        front.Stats.Add("test", "avoid", -0.9);
        rogue.ActTicks = TurnClock.TurnThreshold;
        var r = b.Act(rogue, Repo.Actions["dagger_attack"], front);
        Assert.False(front.Alive);
        Assert.Equal(E(0, 1), grid.AnchorOf(grunt));
        Assert.Equal(E(1, 0), grid.AnchorOf(rogue));                     // behind the enemies: its tile stays free
        Assert.Contains(r.Of<Moved>(), m => m.Unit == grunt && m.Why == "collapse");
        Assert.DoesNotContain(r.Of<Moved>(), m => m.Unit == rogue);
    }

    [Fact]
    public void A_back_row_added_at_the_back_keeps_everyones_place_from_the_front()
    {
        // Side-on style board: the party faces with its last column, so the back is column 0.
        var grid = new BattleGrid(
            [new BattleArea(BattleGrid.PartyArea, Side.Party, Cols: 2, Rows: 3), new BattleArea(BattleGrid.EnemyArea, Side.Enemy, 3, 2)],
            [new Front(BattleGrid.PartyArea, Edge.ColEnd, BattleGrid.EnemyArea, Edge.RowStart)]);
        var w = U("warrior", "w", Side.Party);
        grid.Place(w, grid.TileAt(BattleGrid.PartyArea, depth: 0, lane: 1));
        grid.AddBackRow(BattleGrid.PartyArea);
        Assert.Equal(3, grid.Areas[BattleGrid.PartyArea].Cols);
        Assert.Equal((0, 1), grid.Relative(grid.AnchorOf(w)!.Value));    // still front row, lane 1
        Assert.Contains(grid.TilesOf(BattleGrid.PartyArea), t => grid.Relative(t).Depth == 2);
    }
}
