using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using static EternalDungeon.Core.Tests.TestData;

namespace EternalDungeon.Core.Tests;

// M1 system 10: the battle grid. Areas are 3 columns × 2 rows; row 0 is the front.
public class GridTests
{
    static Unit U(string def, string id, Side side, GameData? data = null) =>
        new(id, (data ?? Repo).Units[def], side, data ?? Repo);

    static Tile P(int row, int col) => new(BattleGrid.PartyArea, row, col);
    static Tile E(int row, int col) => new(BattleGrid.EnemyArea, row, col);

    [Fact]
    public void Footprints_cover_one_two_and_four_tiles()
    {
        var grid = new BattleGrid();
        var grunt = U("goblin_grunt", "grunt", Side.Enemy);
        var brute = U("goblin_brute", "brute", Side.Enemy);
        var chief = U("goblin_chief", "chief", Side.Enemy);

        grid.Place(chief, E(0, 0));
        grid.Place(brute, E(0, 2));
        Assert.Equal([E(0, 0), E(0, 1), E(1, 0), E(1, 1)], grid.Footprint(chief));
        Assert.Equal([E(0, 2), E(1, 2)], grid.Footprint(brute));
        Assert.Equal(chief, grid.At(E(1, 1)));
        Assert.Equal(brute, grid.At(E(1, 2)));

        Assert.False(grid.Fits(grunt, E(1, 0)));                         // taken by the Chief
        Assert.False(grid.Fits(brute, E(1, 2)));                         // a Tall unit can't start in the back row
        Assert.Throws<InvalidOperationException>(() => grid.Place(grunt, E(0, 1)));
    }

    [Fact]
    public void At_most_six_units_a_side()
    {
        var grid = new BattleGrid(cols: 4, rows: 2);
        for (var i = 0; i < 6; i++)
            grid.Place(U("goblin_grunt", $"g{i}", Side.Enemy), E(i / 4, i % 4));
        Assert.Throws<InvalidOperationException>(() => grid.Place(U("goblin_grunt", "g6", Side.Enemy), E(1, 3)));
    }

    [Fact]
    public void Melee_needs_both_front_rows_reach_gets_the_second_and_ranged_gets_anything()
    {
        var grid = new BattleGrid();
        var warrior = U("warrior", "warrior", Side.Party);
        var mage = U("elementalist", "mage", Side.Party);
        var front = U("goblin_grunt", "front", Side.Enemy);
        var back = U("goblin_archer", "back", Side.Enemy);
        grid.Place(warrior, P(0, 1));
        grid.Place(mage, P(1, 1));
        grid.Place(front, E(0, 0));
        grid.Place(back, E(1, 2));

        var attack = Repo.Actions["attack"];
        var spear = attack with { Range = ActionRange.Reach };
        var bolt = Repo.Actions["fire_bolt"];

        Assert.Null(grid.CantTarget(warrior, attack, front));
        Assert.Equal("out_of_reach", grid.CantTarget(warrior, attack, back));
        Assert.Null(grid.CantTarget(warrior, spear, back));
        Assert.Equal("not_in_front_row", grid.CantTarget(mage, attack, front));
        Assert.Null(grid.CantTarget(mage, bolt, back));
        Assert.Equal("not_an_enemy", grid.CantTarget(warrior, attack, mage));
        Assert.Equal("not_an_ally", grid.CantTarget(mage, Repo.Actions["mend"], front));
    }

    [Fact]
    public void Melee_reaches_straight_ahead_or_diagonally_only()
    {
        var grid = new BattleGrid();
        var warrior = U("warrior", "warrior", Side.Party);
        var left = U("goblin_grunt", "left", Side.Enemy);
        var right = U("goblin_grunt", "right", Side.Enemy);
        var backRight = U("goblin_archer", "back", Side.Enemy);
        grid.Place(warrior, P(0, 0));
        grid.Place(left, E(0, 1));                                       // diagonal: next lane over
        grid.Place(right, E(0, 2));                                      // two lanes over
        grid.Place(backRight, E(1, 1));
        var attack = Repo.Actions["attack"];
        var spear = attack with { Range = ActionRange.Reach };

        Assert.Null(grid.CantTarget(warrior, attack, left));
        Assert.Equal("out_of_reach", grid.CantTarget(warrior, attack, right));
        Assert.Null(grid.CantTarget(warrior, spear, backRight));          // Reach: the second row, same lanes
        grid.MoveTo(warrior, P(0, 2));
        Assert.Null(grid.CantTarget(warrior, attack, right));             // straight ahead
        Assert.Null(grid.CantTarget(warrior, attack, left));

        // A Large unit counts every lane it covers.
        var chiefGrid = new BattleGrid();
        var chief = U("goblin_chief", "chief", Side.Enemy);
        chiefGrid.Place(chief, E(0, 1));                                 // lanes 1 and 2
        chiefGrid.Place(warrior, P(0, 0));
        Assert.Null(chiefGrid.CantTarget(warrior, attack, chief));
        Assert.Null(chiefGrid.CantTarget(chief, Repo.Actions["chief_cleave"], warrior));
    }

    [Fact]
    public void Close_combat_inside_one_area_reaches_adjacent_and_diagonal_tiles()
    {
        var grid = new BattleGrid();
        var rogue = U("rogue", "rogue", Side.Party);
        var near = U("goblin_grunt", "near", Side.Enemy);
        var far = U("goblin_archer", "far", Side.Enemy);
        grid.Place(rogue, E(1, 0));                                      // standing in the enemy area
        grid.Place(near, E(0, 1));
        grid.Place(far, E(1, 2));
        var dagger = Repo.Actions["dagger_attack"];
        Assert.Null(grid.CantTarget(rogue, dagger, near));
        Assert.Null(grid.CantTarget(near, Repo.Actions["goblin_slash"], rogue));
        Assert.Equal("out_of_reach", grid.CantTarget(rogue, dagger, far));
    }

    [Fact]
    public void A_tall_unit_counts_as_front_row()
    {
        var grid = new BattleGrid();
        var warrior = U("warrior", "warrior", Side.Party);
        var brute = U("goblin_brute", "brute", Side.Enemy);
        grid.Place(warrior, P(0, 0));
        grid.Place(brute, E(0, 1));
        Assert.Null(grid.CantTarget(warrior, Repo.Actions["attack"], brute));
        Assert.Null(grid.CantTarget(brute, Repo.Actions["brute_smash"], warrior));
    }

    [Fact]
    public void Move_goes_to_any_empty_tile_in_the_area_for_50_ap()
    {
        var warrior = U("warrior", "warrior", Side.Party);
        var rogue = U("rogue", "rogue", Side.Party);
        var grunt = U("goblin_grunt", "grunt", Side.Enemy);
        var grid = new BattleGrid();
        grid.Place(warrior, P(0, 0));
        grid.Place(rogue, P(0, 1));
        grid.Place(grunt, E(0, 0));
        var b = new Battle(Repo, [warrior, rogue, grunt], seed: 1, grid);

        Assert.Equal([P(0, 2), P(1, 0), P(1, 1), P(1, 2)], grid.MoveOptions(warrior));   // every empty tile; the rogue holds (0, 1)
        warrior.ActTicks = TurnClock.TurnThreshold;
        var r = b.ActAt(warrior, Repo.Actions["move"], P(1, 2));          // across the area in one Move
        Assert.Equal(new Moved(warrior, P(0, 0), P(1, 2), "Move"), r.Outcomes[0]);
        Assert.Equal(5000, warrior.ActTicks);                            // 50 AP
        warrior.ActTicks = TurnClock.TurnThreshold;
        Assert.Throws<InvalidOperationException>(() => b.ActAt(warrior, Repo.Actions["move"], P(0, 1)));  // taken
        Assert.Throws<InvalidOperationException>(() => b.ActAt(warrior, Repo.Actions["move"], E(1, 1)));  // not its area
    }

    [Fact]
    public void The_rogues_move_goes_into_the_enemy_area_where_melee_works_both_ways()
    {
        var rogue = U("rogue", "rogue", Side.Party);
        var warrior = U("warrior", "warrior", Side.Party);
        var grunt = U("goblin_grunt", "grunt", Side.Enemy);
        var archer = U("goblin_archer", "archer", Side.Enemy);
        var grid = new BattleGrid();
        grid.Place(warrior, P(0, 0));
        grid.Place(rogue, P(1, 0));
        grid.Place(grunt, E(0, 0));
        grid.Place(archer, E(1, 1));
        var b = new Battle(Repo, [rogue, warrior, grunt, archer], seed: 1, grid);

        rogue.ActTicks = TurnClock.TurnThreshold;
        var move = Repo.Actions[Repo.DefaultFor(rogue.Def, DefaultRole.Move)!];
        Assert.Equal("stealth_move", move.Id);                           // the Stealth mastery replaces Move
        Assert.Contains(E(1, 2), Options.TilesFor(b, rogue, move));
        Assert.Contains(P(1, 1), Options.TilesFor(b, rogue, move));   // and it still steps within its own area
        var r = b.ActAt(rogue, move, E(1, 2));
        Assert.Equal("stealth", r.Of<BuffApplied>().Single().Buff.Def.Id);
        Assert.True(grid.Intruding(rogue));
        Assert.Throws<InvalidOperationException>(() => b.ActAt(warrior, Repo.Actions["move"], E(1, 0)));   // others can't

        Assert.Null(grid.CantTarget(rogue, Repo.Actions["dagger_attack"], archer));   // a back-row target
        Assert.Null(grid.CantTarget(archer, Repo.Actions["goblin_slash"], rogue));
        Assert.Null(grid.CantTarget(grunt, Repo.Actions["goblin_slash"], warrior));  // the front line is unchanged
    }

    [Fact]
    public void From_the_enemy_area_the_rogue_can_move_back_to_any_empty_tile_of_its_own()
    {
        var rogue = U("rogue", "rogue", Side.Party);
        var warrior = U("warrior", "warrior", Side.Party);
        var grunt = U("goblin_grunt", "grunt", Side.Enemy);
        var grid = new BattleGrid();
        grid.Place(warrior, P(0, 0));
        grid.Place(rogue, E(1, 2));                                      // already in the enemy area
        grid.Place(grunt, E(0, 0));
        var b = new Battle(Repo, [rogue, warrior, grunt], seed: 1, grid);
        var move = Repo.Actions[Repo.DefaultFor(rogue.Def, DefaultRole.Move)!];

        var tiles = Options.TilesFor(b, rogue, move);
        Assert.Contains(P(1, 2), tiles);                                 // any empty tile at home, not just next to it
        Assert.Contains(P(0, 2), tiles);
        Assert.DoesNotContain(P(0, 0), tiles);                           // the Warrior's tile
        Assert.Contains(E(0, 2), tiles);                                 // and it can still move about the enemy area

        rogue.ActTicks = TurnClock.TurnThreshold;
        b.ActAt(rogue, move, P(1, 2));
        Assert.False(grid.Intruding(rogue));
        Assert.DoesNotContain(P(0, 0), Options.TilesFor(b, rogue, move));
        Assert.Empty(grid.HomeAreaOptions(rogue));                        // back home: no more jumps within its own area
        Assert.Empty(grid.HomeAreaOptions(warrior));                      // and nobody else gets one
    }

    [Fact]
    public void The_area_collapses_forward_when_its_front_row_empties()
    {
        var warrior = U("warrior", "warrior", Side.Party);
        var g1 = U("goblin_grunt", "g1", Side.Enemy);
        var archer = U("goblin_archer", "archer", Side.Enemy);
        var shaman = U("goblin_shaman", "shaman", Side.Enemy);
        var grid = new BattleGrid();
        grid.Place(warrior, P(0, 1));
        grid.Place(g1, E(0, 1));
        grid.Place(archer, E(1, 0));
        grid.Place(shaman, E(1, 2));
        var b = new Battle(Repo, [warrior, g1, archer, shaman], seed: 1, grid);

        g1.TakeDamage(g1.MaxHealth - 1);
        g1.Stats.Add("test", "avoid", -0.9);
        warrior.ActTicks = TurnClock.TurnThreshold;
        var r = b.Act(warrior, Repo.Actions["attack"], g1);

        Assert.False(g1.Alive);
        Assert.Equal(E(0, 0), grid.AnchorOf(archer));
        Assert.Equal(E(0, 2), grid.AnchorOf(shaman));
        Assert.Equal(2, r.Of<Moved>().Count(m => m.Why == "collapse"));
        Assert.Null(grid.CantTarget(warrior, Repo.Actions["attack"], archer));
    }

    [Fact]
    public void Push_and_pull_move_a_unit_a_row_if_there_is_room()
    {
        var data = With(
            actions: [Action("shove", ActionTarget.Enemy) with { Procs = ["test_push"] },
                      Action("hook", ActionTarget.Enemy) with { Procs = ["test_pull"] }],
            procs: [Proc("test_push", ProcTrigger.Hit, ProcTarget.Other) with { Displace = Displace.Push },
                    Proc("test_pull", ProcTrigger.Hit, ProcTarget.Other) with { Displace = Displace.Pull }]);
        var warrior = U("warrior", "warrior", Side.Party, data);
        var g1 = U("goblin_grunt", "g1", Side.Enemy, data);
        var g2 = U("goblin_grunt", "g2", Side.Enemy, data);
        var archer = U("goblin_archer", "archer", Side.Enemy, data);
        foreach (var g in new[] { g1, g2, archer }) g.Stats.Add("test", "avoid", -0.9);
        var grid = new BattleGrid();
        grid.Place(warrior, P(0, 0));
        grid.Place(g1, E(0, 0));
        grid.Place(g2, E(0, 1));
        grid.Place(archer, E(1, 2));
        var b = new Battle(data, [warrior, g1, g2, archer], seed: 1, grid);

        warrior.ActTicks = TurnClock.TurnThreshold;
        b.Act(warrior, data.Actions["shove"], g1);
        Assert.Equal(E(1, 0), grid.AnchorOf(g1));

        warrior.ActTicks = TurnClock.TurnThreshold;
        b.Act(warrior, data.Actions["hook"], archer);                    // test actions have Any range
        Assert.Equal(E(0, 2), grid.AnchorOf(archer));

        warrior.ActTicks = TurnClock.TurnThreshold;
        var blocked = b.Act(warrior, data.Actions["hook"], g2);          // already in front: nowhere to go
        Assert.Empty(blocked.Of<Moved>());
    }
}
