using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using static EternalDungeon.Core.Tests.TestData;

namespace EternalDungeon.Core.Tests;

// The battle screen's Core side (M2 brief §6, §9): turn-by-turn play, replays and previews.
public class SessionTests
{
    static BattleSession Session(string encounter, ulong seed) => new(Repo, Repo.Encounters[encounter], seed);

    /// <summary>A player's pick that uses no randomness (the AI's tie-breaks roll the battle's RNG, so a test player
    /// must not ask it): the hero's first usable action, at its first valid target or tile.</summary>
    static Choice Pick(Battle battle, Unit hero)
    {
        foreach (var id in battle.Data.ActionsOf(hero.Def))
        {
            var action = battle.Data.Actions[id];
            if (Options.Unusable(battle, hero, action) is not null) continue;
            return action.Target == ActionTarget.Tile
                ? new Choice(hero.Id, id, Tile: Options.TilesFor(battle, hero, action)[0])
                : new Choice(hero.Id, id, Options.TargetsFor(battle, hero, action)[0].Id);
        }
        return new Choice(hero.Id, "");
    }

    /// <summary>Plays a whole battle as a player would: at each hero's turn, look at every preview (when asked),
    /// then pick.</summary>
    static BattleSession PlayByHand(string encounter, ulong seed, bool lookAtPreviews)
    {
        var s = Session(encounter, seed);
        s.Advance();
        while (!s.Over)
        {
            var hero = s.Awaiting!;
            if (lookAtPreviews) LookAtEverything(s.Battle, hero);
            s.Choose(Pick(s.Battle, hero));
            s.Advance();
        }
        return s;
    }

    static void LookAtEverything(Battle battle, Unit hero)
    {
        Preview.Timeline(battle, 10);
        foreach (var id in battle.Data.ActionsOf(hero.Def))
        {
            var action = battle.Data.Actions[id];
            Options.Unusable(battle, hero, action);
            Preview.NextTurn(battle, hero, action);
            Options.TilesFor(battle, hero, action);
            foreach (var target in battle.Units)
                Preview.Target(battle, hero, action, target);
        }
    }

    static string Log(Battle b) => CombatLog.Write(b, LogLevel.Full);

    [Fact]
    public void Auto_battle_plays_exactly_like_the_simulator()
    {
        foreach (var encounter in Repo.EncounterList)
        {
            var sim = EncounterSetup.Build(Repo, encounter, 42);
            BattleRunner.Run(sim);
            var s = new BattleSession(Repo, encounter, 42) { AutoBattle = true };
            s.Advance();
            Assert.True(s.Over);
            Assert.Equal(Log(sim), Log(s.Battle));
        }
    }

    [Fact]
    public void Previews_never_change_the_battle()
    {
        foreach (var encounter in Repo.Encounters.Keys)
            for (ulong seed = 1; seed <= 5; seed++)
                Assert.Equal(Log(PlayByHand(encounter, seed, false).Battle), Log(PlayByHand(encounter, seed, true).Battle));
    }

    [Fact]
    public void A_replay_reproduces_the_battle_through_its_file()
    {
        foreach (var encounter in Repo.Encounters.Keys)
        {
            var played = PlayByHand(encounter, 7, lookAtPreviews: false);
            var file = played.ToReplay().ToJson();
            var replayed = Replay.Parse(file).Play(Repo);
            Assert.True(replayed.Over);
            Assert.Equal(Log(played.Battle), Log(replayed.Battle));
            Assert.Equal(file, replayed.ToReplay().ToJson());
        }
    }

    [Fact]
    public void Switching_auto_battle_mid_fight_still_replays()
    {
        var s = Session("systems_showcase", 3);
        s.Advance();
        for (var i = 0; i < 3 && !s.Over; i++)
        {
            s.Choose(Pick(s.Battle, s.Awaiting!));
            s.Advance();
        }
        s.AutoBattle = true;
        if (s.Awaiting is { } waiting)
            s.Choose(Pick(s.Battle, waiting));
        s.Advance();
        Assert.True(s.Over);
        Assert.Contains(s.Choices, c => c.Auto);
        Assert.Equal(Log(s.Battle), Log(s.ToReplay().Play(Repo).Battle));
    }

    [Fact]
    public void Wrong_choices_are_refused_with_a_reason()
    {
        var s = Session("goblin_patrol", 1);
        s.Advance();
        var hero = s.Awaiting!;
        var enemy = s.Battle.Units.First(u => u.Side == Side.Enemy);
        Assert.Equal("needs a target", s.CantChoose(new Choice(hero.Id, "attack")));
        Assert.NotNull(s.CantChoose(new Choice(hero.Id, "fly", enemy.Id)));
        Assert.NotNull(s.CantChoose(new Choice("goblin_grunt#1", "attack", enemy.Id)));
        Assert.Throws<InvalidOperationException>(() => s.Choose(new Choice(hero.Id, "attack", hero.Id)));
        Assert.Same(hero, s.Awaiting);                                   // still waiting after a refused choice
    }

    [Fact]
    public void Target_preview_matches_the_damage_formula()
    {
        var b = EncounterSetup.Build(Repo, Repo.Encounters["goblin_patrol"], 1);
        var warrior = b.Units.First(u => u.Def.Id == "warrior");
        var grunt = b.Units.First(u => u.Def.Id == "goblin_grunt");
        var attack = Repo.Actions["attack"];
        var p = Preview.Target(b, warrior, attack, grunt);

        Assert.Equal(Resolution.SuccessChance(warrior, attack, grunt), p.HitChance);
        Assert.Equal(Resolution.Damage(warrior, attack, grunt).Final, p.Damage);
        Assert.Equal(Resolution.Damage(warrior, attack, grunt, 2).Final, p.BrutalDamage);
        Assert.Equal(Resolution.CritChance(0.05), p.CritChance, 12);
        Assert.False(p.RandomTarget);
    }

    [Fact]
    public void The_ghost_marker_lands_where_the_next_turn_does()
    {
        var w = new Unit("w", Repo.Units["warrior"], Side.Party, Repo);
        var g = new Unit("g", Repo.Units["goblin_grunt"], Side.Enemy, Repo);
        w.Stats.Add("test", "hit", -0.99);                                 // always misses: nothing changes Speed
        var b = new Battle(Repo, [w, g], seed: 1);
        b.Start();
        while (b.Clock.Next() is not TurnReady { Unit: var u } || u != w) { }

        var estimate = Preview.NextTurn(b, w, Repo.Actions["power_attack"]);
        Assert.Equal(Preview.Timeline(b, 1)[0], new UpcomingTurn(w, b.Clock.Tick));
        b.Act(w, Repo.Actions["power_attack"], g);
        ClockEvent next;
        while ((next = b.Clock.Next()) is not TurnReady { Unit: var v } || v != w)
            if (next is TurnReady other) b.Wait(other.Unit);
        Assert.Equal(estimate.NextTurn, b.Clock.Tick);
        Assert.Null(estimate.CastCompletes);
    }
}
