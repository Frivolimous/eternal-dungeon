using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using EternalDungeon.Core.Exploration;

namespace EternalDungeon.Core.Tests;

/// <summary>The dungeon run and event engine (Anchor: Exploration; M3A brief §1, §3–5), on a small test dungeon.</summary>
public class RunTests
{
    static GameData D => RunTestData.Data;

    static DungeonRun Started(ulong seed = 1)
    {
        var run = new DungeonRun(D, "t_dungeon", seed);
        run.Start();
        return run;
    }

    /// <summary>Plays the fight in progress on auto-battle and hands it back.</summary>
    static RunResult AutoFight(DungeonRun run)
    {
        run.Battle!.Session.AutoBattle = true;
        run.Battle.Session.Advance();
        return run.FinishBattle();
    }

    /// <summary>Every hero flees on its first turn.</summary>
    static RunResult FleeAll(DungeonRun run)
    {
        var s = run.Battle!.Session;
        s.Advance();
        while (!s.Over)
        {
            s.Choose(new Choice(s.Awaiting!.Id, "flee"));
            s.Advance();
        }
        return run.FinishBattle();
    }

    [Fact]
    public void Starting_explores_the_start_node_and_runs_its_event()
    {
        var run = Started();
        Assert.True(run.Nodes["t_a"].Explored);
        Assert.Equal(RunState.Event, run.State);
        Assert.Equal(["t_b"], run.Eligible.Select(n => n.Def.Id));
        var story = Assert.Single(run.Results[0].Of<StoryShown>());
        Assert.Equal("intro", story.Block.Id);
        Assert.Equal(1, run.Steps);
    }

    [Fact]
    public void Only_choices_whose_conditions_hold_are_shown()
    {
        var run = Started();
        // The Rogue gives Disable, the Warrior its class; nobody has Arcana 2; the party has no Gold.
        Assert.Equal(["pick", "bash", "later"], run.Choices.Select(c => c.Id));
    }

    [Fact]
    public void A_trait_roll_is_made_by_the_best_hero_with_its_chance()
    {
        var run = Started();
        var preview = run.Previews.Single(p => p.Choice.Id == "pick");
        Assert.Equal("rogue", Assert.Single(preview.Heroes).Id);
        Assert.Equal(0.7, preview.Chance!.Value, 6);              // 0.5 + 0.2 × Disable 1
        var r = run.Choose("pick");
        var roll = Assert.Single(r.Of<EventRolled>());
        Assert.Equal("rogue", roll.Hero.Id);
        Assert.Equal(0.7, roll.Roll.Chance, 6);
        Assert.Equal("rogue", run.Results.Last().Of<ChoiceMade>().Single().Hero.Id);
    }

    [Fact]
    public void Exhaustion_lowers_the_rolling_heros_chance()
    {
        var run = Started();
        run.Hero("rogue").Stamina = -2;                            // Severe: −25%
        Assert.Equal(0.45, run.Previews.Single(p => p.Choice.Id == "pick").Chance!.Value, 6);
    }

    [Fact]
    public void A_successful_roll_rewards_and_sets_flags_and_buffs()
    {
        // Find a seed where the lock pick succeeds.
        for (ulong seed = 1; seed < 50; seed++)
        {
            var run = Started(seed);
            var r = run.Choose("pick");
            if (!r.Of<EventRolled>().Single().Roll.Success) continue;
            Assert.Equal(10, run.Gold);
            Assert.Equal(1, run.Pack["health_potion"]);
            Assert.True(run.Flags["looted"]);
            Assert.All(run.Heroes, h => Assert.Equal(1, h.Buffs.Count));
            Assert.Equal(1, run.Hero("warrior").Trait("awareness"));   // not a Warrior trait, +1 from Keen
            Assert.Equal(EventStatus.Closed, run.Nodes["t_a"].Event);
            Assert.Equal(RunState.Exploring, run.State);
            return;
        }
        Assert.Fail("no seed succeeded");
    }

    [Fact]
    public void Deferring_keeps_the_event_and_resuming_continues_at_the_resume_block()
    {
        var run = Started();
        run.Choose("later");
        Assert.Equal(EventStatus.Deferred, run.Nodes["t_a"].Event);
        Assert.Equal(RunState.Exploring, run.State);
        Assert.Equal(["t_a"], run.Deferred.Select(n => n.Def.Id));

        var r = run.Resume("t_a");                                  // not looted: back to the intro
        Assert.True(Assert.Single(r.Of<EventStarted>()).Resumed);
        Assert.Equal("intro", Assert.Single(r.Of<StoryShown>()).Block.Id);
    }

    [Fact]
    public void A_reveal_marks_a_node_before_it_is_eligible()
    {
        var run = Started();
        run.Choose("bash");
        Assert.Equal(RevealIcon.Boss, run.Nodes["t_c"].Revealed);
        Assert.DoesNotContain(run.Eligible, n => n.Def.Id == "t_c");
    }

    [Fact]
    public void A_won_fight_carries_health_and_costs_stamina_after_the_fight()
    {
        var run = Started();
        run.Choose("bash");
        run.Explore("t_b");
        run.Continue();
        Assert.Equal(RunState.Battle, run.State);
        var battle = run.Battle!.Session.Battle;
        // Exhaustion and First Strike: everyone at full Stamina, so no Speed penalty; +30 Initiative.
        Assert.All(run.Heroes, h => Assert.Equal(new Unit("x", h.Unit, Side.Party, D).Stats.Get("initiative") + 30, battle.Unit(h.Id).Stats.Get("initiative")));
        var r = AutoFight(run);
        var ended = Assert.Single(r.Of<BattleEnded>());
        Assert.Equal(BattleOutcome.Victory, ended.Outcome);
        foreach (var hero in run.Heroes)
        {
            Assert.Equal(3, hero.Stamina);
            Assert.Equal(battle.Unit(hero.Id).Health, hero.Health);
        }
        // The fight's success link: the Weak curse (1 battle), then the closing story.
        Assert.All(run.Heroes, h => Assert.Contains(h.Buffs, b => b.Def.Id == "t_weak"));
        Assert.Equal(RunState.Event, run.State);
        run.Continue();
        Assert.Equal(EventStatus.Closed, run.Nodes["t_b"].Event);
    }

    [Fact]
    public void Exhaustion_slows_heroes_in_battle()
    {
        var run = Started();
        run.Choose("bash");
        run.Hero("warrior").Stamina = 0;                           // Exhausted: −10 Speed
        run.Hero("rogue").Stamina = -3;                            // Severe: −25 Speed
        run.Explore("t_b");
        run.Continue();
        var battle = run.Battle!.Session.Battle;
        Assert.Equal(new Unit("x", D.Units["warrior"], Side.Party, D).Speed - 10, battle.Unit("warrior").Speed);
        Assert.Equal(new Unit("x", D.Units["rogue"], Side.Party, D).Speed - 25, battle.Unit("rogue").Speed);
    }

    [Fact]
    public void Battle_timed_curses_join_the_next_battle_and_count_down_after_it()
    {
        var run = Started();
        run.Choose("bash");
        run.Explore("t_b");
        run.Continue();
        AutoFight(run);                                            // the win gives Weak (1 battle)
        run.Continue();
        run.Explore("t_c");                                        // the boss
        var unit = run.Battle!.Session.Battle.Unit("warrior");
        Assert.Contains(unit.Buffs, b => b.Def.Id == "t_weak");
        Assert.Equal(-25, unit.Stats.Get("power"), 6);
        FleeAll(run);
        Assert.All(run.Heroes, h => Assert.DoesNotContain(h.Buffs, b => b.Def.Id == "t_weak"));
    }

    [Fact]
    public void Step_timed_buffs_count_down_as_nodes_are_explored()
    {
        for (ulong seed = 1; seed < 50; seed++)
        {
            var run = Started(seed);
            if (!run.Choose("pick").Of<EventRolled>().Single().Roll.Success) continue;
            var elementalist = run.Hero("elementalist");
            Assert.Equal(2, elementalist.Trait("awareness"));         // Primary class 1 + Keen 1
            run.Explore("t_b");
            Assert.Equal(1, elementalist.Buffs.Single().Remaining);
            run.Continue();
            AutoFight(run);
            run.Continue();
            run.Explore("t_c");
            Assert.Empty(elementalist.Buffs.Where(b => b.Def.Id == "t_keen"));
            Assert.Equal(1, elementalist.Trait("awareness"));
            return;
        }
        Assert.Fail("no seed succeeded");
    }

    [Fact]
    public void Fleeing_a_boss_defers_its_event_to_the_fight_and_costs_stamina()
    {
        var run = Started();
        run.Choose("bash");
        run.Explore("t_b");
        run.Continue();
        AutoFight(run);
        run.Continue();
        run.Explore("t_c");
        var r = FleeAll(run);
        Assert.Equal(BattleOutcome.Fled, r.Of<BattleEnded>().Single().Outcome);
        Assert.Equal(EventStatus.Deferred, run.Nodes["t_c"].Event);
        Assert.Equal("fight", run.Nodes["t_c"].ResumeBlock);
        Assert.All(run.Heroes, h => Assert.Equal(2, h.Stamina));
        Assert.Empty(run.Usable.Where(u => u.Interactable.Kind == InteractableKind.Pathway));   // the boss still guards it

        run.Resume("t_c");
        Assert.Equal(RunState.Battle, run.State);
        Assert.All(run.Battle!.Session.Battle.Units.Where(u => u.Side == Side.Enemy), u => Assert.Equal(u.MaxHealth, u.Health));
    }

    [Fact]
    public void Fleeing_a_major_fight_closes_the_event()
    {
        var run = Started();
        run.Choose("bash");
        run.Explore("t_b");
        run.Continue();
        FleeAll(run);
        Assert.Equal(EventStatus.Closed, run.Nodes["t_b"].Event);
        Assert.All(run.Heroes, h => Assert.Equal(3, h.Stamina));
    }

    [Fact]
    public void A_victory_with_a_fled_hero_counts_as_a_victory_and_the_fled_hero_pays_stamina()
    {
        var run = Started();
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
        Assert.Equal(BattleOutcome.Victory, r.Of<BattleEnded>().Single().Outcome);
        Assert.Equal(3, run.Hero(fled).Stamina);
        Assert.False(run.Hero(fled).Dead);
    }

    [Fact]
    public void Potions_spend_charges_in_battle_and_outside_it()
    {
        var run = Started();
        run.Choose("bash");
        var warrior = run.Hero("warrior");
        warrior.Health = 20;
        run.UseItem("warrior", "health_potion");
        Assert.Equal(20 + (int)Math.Round(0.4 * warrior.MaxHealth), warrior.Health);
        Assert.Equal(1, warrior.Charges("health_potion"));

        run.Explore("t_b");
        run.Continue();
        var s = run.Battle!.Session;
        s.Advance();
        while (s.Awaiting!.Id != "warrior")
        {
            s.ChooseByAi();
            s.Advance();
        }
        s.Choose(new Choice("warrior", "health_potion"));
        Assert.Contains(s.Battle.Results.SelectMany(x => x.Outcomes), o => o is ChargeSpent { Left: 0 });
        s.AutoBattle = true;
        s.Advance();
        run.FinishBattle();
        Assert.Equal(0, warrior.Charges("health_potion"));
        Assert.Empty(warrior.Belt);
    }

    [Fact]
    public void Pack_items_move_into_a_free_belt_slot()
    {
        var run = Started();
        run.Choose("bash");
        run.Hero("rogue").Belt.Clear();
        run.Pack["mana_potion"] = 3;
        run.MoveToBelt("rogue", "mana_potion");
        Assert.Equal(2, run.Hero("rogue").Charges("mana_potion"));     // a slot holds the item's uses
        Assert.Equal(1, run.Pack["mana_potion"]);
        Assert.Equal("belt_full", run.CantMoveToBelt("warrior", "mana_potion"));
    }

    [Fact]
    public void Camp_restores_half_and_rests_but_never_above_max_stamina()
    {
        var run = Started();
        run.Choose("bash");
        var hero = run.Hero("warrior");
        hero.Health = 10;
        hero.Stamina = 2;
        run.Hero("rogue").Stamina = -4;
        run.Camp();
        Assert.Equal(10 + (int)Math.Round(0.5 * hero.MaxHealth), hero.Health);
        Assert.Equal(4, hero.Stamina);
        Assert.Equal(0, run.Hero("rogue").Stamina);                  // Rest ends Unconscious
        Assert.Equal(0, run.CampCharges);
    }

    [Fact]
    public void All_unconscious_rests_automatically_and_otherwise_wipes()
    {
        var run = Started();
        run.Choose("bash");
        run.Explore("t_b");
        run.Continue();
        foreach (var h in run.Heroes) h.Stamina = -3;               // the fight's cost knocks everyone out
        var r = FleeAll(run);
        Assert.Contains(r.Of<Rested>(), x => x.Camp && x.Automatic);
        Assert.All(run.Heroes, h => Assert.Equal(0, h.Stamina));    // −4 after the fight, +4 from the Rest

        var broke = Started(2);
        broke.Choose("bash");
        broke.Camp();
        broke.Explore("t_b");
        broke.Continue();
        foreach (var h in broke.Heroes) h.Stamina = -3;
        var wipe = FleeAll(broke);
        Assert.Single(wipe.Of<PartyWiped>());
        Assert.Equal(RunState.Wiped, broke.State);
        Assert.All(broke.Heroes, h => Assert.True(h.Dead));
    }

    [Fact]
    public void A_pathway_leads_to_the_next_map_and_the_last_event_completes_the_dungeon()
    {
        var run = Started();
        run.Choose("bash");
        run.Explore("t_b");
        run.Continue();
        AutoFight(run);
        run.Continue();
        run.Explore("t_c");
        foreach (var u in run.Battle!.Session.Battle.Units.Where(u => u.Side == Side.Enemy))
            u.TakeDamage(u.Health - 1);
        AutoFight(run);
        run.Continue();
        Assert.Contains(run.Usable, u => u.Interactable.Kind == InteractableKind.Pathway);
        run.UseInteractable("t_c", InteractableKind.Pathway);
        Assert.Equal("t_map2", run.Map.Id);
        Assert.Contains(run.Usable, u => u.Interactable.Kind == InteractableKind.Sanctuary);
        run.UseInteractable("t_d", InteractableKind.Sanctuary);
        Assert.All(run.Heroes.Where(h => !h.Dead), h => Assert.Equal(h.MaxHealth, h.Health));
        Assert.Empty(run.Usable.Where(u => u.Interactable.Kind == InteractableKind.Sanctuary));

        run.Explore("t_e");
        Assert.Equal(RunState.Event, run.State);                  // the ending text still shows
        var r = run.Continue();
        Assert.Single(r.Of<DungeonCompleted>());
        Assert.Equal(RunState.Completed, run.State);
    }

    [Fact]
    public void Same_seed_and_actions_give_the_same_run()
    {
        static List<string> Play(ulong seed)
        {
            var run = Started(seed);
            run.Choose("pick");
            if (run.State == RunState.Event) run.Choose("later");
            if (run.State == RunState.Exploring && run.Eligible.Any()) run.Explore("t_b");
            if (run.State == RunState.Event) run.Continue();
            if (run.State == RunState.Battle) AutoFight(run);
            return [.. run.Results.SelectMany(r => RunLog.Lines(D, r))];
        }
        Assert.Equal(Play(7), Play(7));
        Assert.All(Play(7), line => Assert.DoesNotContain("[", line));
    }
}
