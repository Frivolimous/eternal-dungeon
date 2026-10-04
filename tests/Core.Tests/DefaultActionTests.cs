using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using static EternalDungeon.Core.Tests.TestData;

namespace EternalDungeon.Core.Tests;

// Default actions (Attack, Defend, Move for every unit), Fear, and the AI rule conditions.
public class DefaultActionTests
{
    static Unit U(GameData data, string def, string id, Side side) => new(id, data.Units[def], side, data);
    static Tile P(int row, int col) => new(BattleGrid.PartyArea, row, col);
    static Tile E(int row, int col) => new(BattleGrid.EnemyArea, row, col);

    [Fact]
    public void Every_unit_has_the_default_actions_on_top_of_its_own()
    {
        Assert.Equal(new DefaultActions("attack", "defend", "move"), Repo.DefaultActions);
        Assert.Equal(["goblin_slash", "attack", "defend", "move"], Repo.ActionsOf(Repo.Units["goblin_grunt"]));
        Assert.Equal(["power_attack", "shield_bash", "attack", "defend", "move"], Repo.ActionsOf(Repo.Units["warrior"]));
    }

    [Fact]
    public void Default_actions_must_fit_their_role()
    {
        var e = TestData.TablesFail(
            ("actions", """[{ "id": "wait", "name": "Wait", "target": "self", "ap_cost": 100 }]"""),
            ("defaults", """[{ "key": "attack_action", "value": "wait" }, { "key": "defend_action", "value": "wait" }, { "key": "move_action", "value": "wait" }]"""));
        Assert.Equal("defaults.json", e.File);
        Assert.Equal("[0].value", e.Field);
        Assert.Contains("attack action must be", e.Message);
    }

    [Fact]
    public void A_unit_with_nothing_to_do_steps_forward_or_else_defends()
    {
        var grunt1 = U(Repo, "goblin_grunt", "g1", Side.Enemy);
        var grunt2 = U(Repo, "goblin_grunt", "g2", Side.Enemy);
        var w = U(Repo, "warrior", "w", Side.Party);
        var grid = new BattleGrid();
        grid.Place(w, P(0, 0));
        grid.Place(grunt1, E(0, 1));
        grid.Place(grunt2, E(1, 2));                                    // behind an empty front tile
        var b = new Battle(Repo, [w, grunt1, grunt2], seed: 1, grid);

        var step = UnitAi.Decide(b, grunt2)!;
        Assert.Equal(("move", E(0, 2)), (step.Action.Id, step.Tile));

        grid.Place(U(Repo, "goblin_grunt", "g3", Side.Enemy), E(0, 2)); // now blocked
        Assert.Equal("defend", UnitAi.Decide(b, grunt2)!.Action.Id);
    }

    [Fact]
    public void Fear_allows_only_defend_or_moving_away_from_the_front()
    {
        var data = With(actions: [Action("scare", ActionTarget.Ally, new EffectRef("dread", EffectAim.Target))]);
        var w = U(data, "warrior", "w", Side.Party);
        var rogue = U(data, "rogue", "r", Side.Party);
        var grid = new BattleGrid();
        grid.Place(w, P(0, 1));
        grid.Place(rogue, P(0, 0));
        var b = new Battle(data, [w, rogue], seed: 1, grid);
        rogue.ActTicks = TurnClock.TurnThreshold;
        b.Act(rogue, data.Actions["scare"], w);

        Assert.True(w.Afraid);
        Assert.False(w.LosesTurn);                                      // unlike Stun or Sleep, the turn isn't skipped
        Assert.Contains("afraid", w.CantUse(data.Actions["attack"]));
        Assert.Contains("afraid", w.CantUse(data.Actions["power_attack"]));
        Assert.Null(w.CantUse(data.Actions["defend"]));

        var retreat = UnitAi.Decide(b, w)!;
        Assert.Equal(("move", P(1, 1)), (retreat.Action.Id, retreat.Tile));
        w.ActTicks = TurnClock.TurnThreshold;
        Assert.Throws<InvalidOperationException>(() => b.ActAt(w, data.Actions["move"], P(0, 2)));   // sideways: not away
        b.ActAt(w, data.Actions["move"], P(1, 1));

        Assert.Equal("defend", UnitAi.Decide(b, w)!.Action.Id);         // nowhere further back
    }

    [Fact]
    public void Not_twice_in_a_row_alternates_power_attack_and_attack()
    {
        var w = U(Repo, "warrior", "w", Side.Party);
        var g = U(Repo, "goblin_grunt", "g", Side.Enemy);
        g.Stats.Add("test", "health", 900);
        g.Heal(900);
        var b = new Battle(Repo, [w, g], seed: 1);
        var used = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            var d = UnitAi.Decide(b, w)!;
            used.Add(d.Action.Id);
            w.ActTicks = TurnClock.TurnThreshold;
            b.Act(w, d.Action, d.Target);
        }
        Assert.Equal(["power_attack", "attack", "power_attack", "attack"], used);
    }

    [Fact]
    public void Target_conditions_pick_casting_or_unbuffed_targets()
    {
        var w = U(Repo, "warrior", "w", Side.Party);
        var mage = U(Repo, "elementalist", "mage", Side.Party);
        var hexer = U(Repo, "goblin_hexer", "hexer", Side.Enemy);
        var g = U(Repo, "goblin_grunt", "g", Side.Enemy);
        var b = new Battle(Repo, [w, mage, hexer, g], seed: 1);

        Assert.NotEqual("shield_bash", UnitAi.Decide(b, w)!.Action.Id);
        b.Clock.BeginCast(hexer, "hex_bolt", w.Id, 60);
        var bash = UnitAi.Decide(b, w)!;
        Assert.Equal(("shield_bash", hexer), (bash.Action.Id, bash.Target));

        var shard = UnitAi.Decide(b, mage)!;
        Assert.Equal("frost_shard", shard.Action.Id);
        mage.ActTicks = TurnClock.TurnThreshold;
        b.Act(mage, shard.Action, shard.Target);
        Assert.Equal("fire_bolt", UnitAi.Decide(b, mage)!.Action.Id);   // not twice in a row
    }
}
