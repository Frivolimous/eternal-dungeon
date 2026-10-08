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
    public void An_action_can_replace_a_default_for_the_units_that_have_it()
    {
        Assert.Equal(["dagger_attack", "attack", "defend", "stealth_move"], Repo.ActionsOf(Repo.Units["rogue"]));
        Assert.Equal("stealth_move", Repo.DefaultFor(Repo.Units["rogue"], DefaultRole.Move));
        Assert.Equal("move", Repo.DefaultFor(Repo.Units["warrior"], DefaultRole.Move));

        static DataException ReplacementFails(string actions, string units) => TestData.TablesFail(
            ("tags", "[]"), ("stats", """[{ "id": "health", "name": "Health", "group": "character", "combine": "add" }]"""),
            ("compound_stats", "[]"), ("compound_stat_rows", "[]"),
            ("actions", $$"""
                [{ "id": "attack", "name": "Attack", "target": "enemy", "range": "melee", "ap_cost": 100, "base_damage": 5 },
                 { "id": "defend", "name": "Defend", "target": "self", "ap_cost": 100 },
                 { "id": "move", "name": "Move", "target": "tile", "ap_cost": 50, "move_to": "own" },
                 {{actions}}]
                """),
            ("defaults", """[{ "key": "attack_action", "value": "attack" }, { "key": "defend_action", "value": "defend" }, { "key": "move_action", "value": "move" }]"""),
            ("ai_profiles", """[{ "id": "basic", "name": "Basic", "threat_weight": 0.5 }]"""),
            ("ai_rules", """[{ "profile": "basic", "order": 1, "action": "attack" }]"""),
            ("units", $"[{units}]"));

        var notAMove = ReplacementFails("""{ "id": "x", "name": "X", "target": "self", "ap_cost": 100, "replaces": "move" }""", "");
        Assert.Equal(("actions.json", "[3].replaces"), (notAMove.File, notAMove.Field));

        var twice = ReplacementFails("""
            { "id": "x", "name": "X", "target": "tile", "ap_cost": 50, "move_to": "own_or_enemy", "replaces": "move" },
            { "id": "y", "name": "Y", "target": "tile", "ap_cost": 50, "move_to": "own", "replaces": "move" }
            """, """{ "id": "u", "name": "U", "size": 1, "health": 1, "actions": ["x", "y"], "ai": "basic" }""");
        Assert.Equal(("units.json", "[0].actions"), (twice.File, twice.Field));
    }

    [Fact]
    public void To_enemy_area_needs_a_move_that_can_go_there()
    {
        var e = TestData.TablesFail(
            ("actions", """[{ "id": "move", "name": "Move", "target": "tile", "ap_cost": 50, "move_to": "own" }]"""),
            ("ai_profiles", """[{ "id": "basic", "name": "Basic", "threat_weight": 0.5 }]"""),
            ("ai_rules", """[{ "profile": "basic", "order": 1, "action": "move", "to_enemy_area": true }]"""));
        Assert.Equal(("ai_rules.json", "[0].to_enemy_area"), (e.File, e.Field));
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
        Assert.Equal(("move", E(0, 0)), (step.Action.Id, step.Tile));   // the free front tile from which it reaches the Warrior

        grid.Place(U(Repo, "goblin_grunt", "g3", Side.Enemy), E(0, 0)); // the front row is now full
        grid.Place(U(Repo, "goblin_grunt", "g4", Side.Enemy), E(0, 2));
        Assert.Equal("defend", UnitAi.Decide(b, grunt2)!.Action.Id);
    }

    [Fact]
    public void A_melee_unit_with_nobody_in_reach_sidesteps_toward_a_target()
    {
        var grunt = U(Repo, "goblin_grunt", "g", Side.Enemy);
        var w = U(Repo, "warrior", "w", Side.Party);
        var grid = new BattleGrid();
        grid.Place(w, P(0, 0));
        grid.Place(grunt, E(0, 2));                                     // two lanes away: out of reach
        var b = new Battle(Repo, [w, grunt], seed: 1, grid);

        var d = UnitAi.Decide(b, grunt)!;
        Assert.Equal(("move", E(0, 0)), (d.Action.Id, d.Tile));         // along its row to face the Warrior
        Assert.Equal(E(0, 2), grid.AnchorOf(grunt));                    // trying tiles left the grid as it was
    }

    [Fact]
    public void A_caster_out_of_mana_falls_back_on_the_default_attack()
    {
        var shaman = U(Repo, "goblin_shaman", "s", Side.Enemy);
        var e = U(Repo, "elementalist", "e", Side.Party);
        var grid = new BattleGrid();
        grid.Place(e, P(0, 1));
        grid.Place(shaman, E(0, 2));
        var b = new Battle(Repo, [e, shaman], seed: 1, grid);
        e.SpendMana(e.MaxMana);
        shaman.SpendMana(shaman.MaxMana);

        Assert.Equal(("attack", shaman), (UnitAi.Decide(b, e)!.Action.Id, UnitAi.Decide(b, e)!.Target));
        Assert.Equal(("attack", e), (UnitAi.Decide(b, shaman)!.Action.Id, UnitAi.Decide(b, shaman)!.Target));
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
        Assert.Equal("move", retreat.Action.Id);
        Assert.Equal(1, grid.Depth(retreat.Tile!.Value));                // any tile further from the front
        w.ActTicks = TurnClock.TurnThreshold;
        Assert.Throws<InvalidOperationException>(() => b.ActAt(w, data.Actions["move"], P(0, 2)));   // sideways: not away
        b.ActAt(w, data.Actions["move"], P(1, 2));

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
