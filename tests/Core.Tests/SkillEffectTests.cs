using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using static EternalDungeon.Core.Tests.TestData;

namespace EternalDungeon.Core.Tests;

/// <summary>The special effects of the Warrior, Rogue and Elementalist tree skills (Anchor: Classes › The starting
/// classes), with the per-level numbers read from the data.</summary>
public class SkillEffectTests
{
    static Unit U(string def, string id, Side side, params (string Skill, int Level)[] skills)
    {
        var u = new Unit(id, Repo.Units[def], side, Repo);
        if (skills.Length > 0)
        {
            Skills.Apply(Repo, u, skills.ToDictionary(s => s.Skill, s => s.Level));
            u.SetVitals(u.MaxHealth, u.MaxMana);
        }
        return u;
    }

    /// <summary>A target that never avoids, or (with <paramref name="dodges"/>) always does.</summary>
    static Unit Target(string id = "g", bool dodges = false)
    {
        var g = U("goblin_grunt", id, Side.Enemy);
        g.Stats.Add("test", "avoid", dodges ? 0.95 : -0.9);
        g.Stats.Add("test", "health", 900);
        g.Heal(900);
        return g;
    }

    static ActionResult Use(Battle b, Unit actor, string action, Unit? target = null)
    {
        actor.ActTicks = TurnClock.TurnThreshold;
        return b.Act(actor, b.Data.Actions[action], target ?? actor);
    }

    static double PerLevel(string skill, string stat) => Repo.Skills[skill].Stats.Single(s => s.Stat == stat).PerLevel;

    [Fact]
    public void Battle_might_makes_the_first_basic_attack_that_hits_a_crit()
    {
        var w = U("warrior", "w", Side.Party, ("vigor", 1), ("battle_might", 1));
        var g = Target();
        var b = new Battle(Repo, [w, g], seed: 3);
        var first = Use(b, w, "attack", g).Of<CritRolled>().Single();
        Assert.True(first.Opening);
        Assert.True(first.Tiers >= 1);
        var second = Use(b, w, "attack", g).Of<CritRolled>().SingleOrDefault();
        Assert.False(second?.Opening ?? false);                    // only once a battle
    }

    [Fact]
    public void Weapon_mastery_staggers_on_every_weapon_hit()
    {
        var w = U("warrior", "w", Side.Party, ("vigor", 1), ("weapon_mastery", 5));
        var g = Target();
        var b = new Battle(Repo, [w, g], seed: 1);
        var act = g.ActTicks;
        var stagger = Use(b, w, "attack", g).Of<Staggered>().Single();
        Assert.Equal((int)(PerLevel("weapon_mastery", "hit_stagger") * 5), stagger.Amount);
        Assert.Equal(act - stagger.Amount * TurnClock.TicksPerTurn, g.ActTicks);
    }

    [Fact]
    public void Imposing_presence_doubles_block_until_the_first_melee_attack_avoided()
    {
        var w = U("warrior", "w", Side.Party, ("vigor", 1), ("weapon_mastery", 1), ("imposing_presence", 2));
        var g = U("goblin_grunt", "g", Side.Enemy);
        var b = new Battle(Repo, [w, g], seed: 1);
        var block = w.Stats.GetCompound("block");
        b.Start();
        Assert.Equal(block * 2, w.Stats.GetCompound("block"), 6);
        w.Stats.Add("test", "avoid", 0.95);                         // the next attack is avoided (this seed)
        w.Stats.Add("test2", "avoid", 0.95);
        var r = Use(b, g, "goblin_slash", w);
        Assert.Single(r.Of<OpeningBlockSpent>());
        Assert.Equal(block, w.Stats.GetCompound("block"), 6);
    }

    [Fact]
    public void Executioner_adds_power_against_targets_below_half_health()
    {
        var rogue = U("rogue", "r", Side.Party, ("executioner", 5));
        var g = Target();
        var b = new Battle(Repo, [rogue, g], seed: 1);
        var full = Resolution.Damage(rogue, Repo.Actions["dagger_attack"], g).Power;
        g.TakeDamage(g.Health - g.MaxHealth / 2 + 1);               // just below half
        var low = Resolution.Damage(rogue, Repo.Actions["dagger_attack"], g).Power;
        Assert.Equal(PerLevel("executioner", "execute_power") * 5, low - full, 6);
        _ = b;
    }

    [Fact]
    public void Opportunist_refunds_ap_against_a_stunned_or_slowed_target()
    {
        var rogue = U("rogue", "r", Side.Party, ("opportunist", 5));
        var cap = PerLevel("opportunist", "opportunist") * 5;
        var stunned = Target("s");
        var slowed = Target("c");
        var b = new Battle(Repo, [rogue, stunned, slowed], seed: 1);
        stunned.Buffs.Add(new Buff(Repo.Buffs["daze"], "x", "x"));
        var dagger = Repo.Actions["dagger_attack"];
        Assert.Equal((int)Math.Round(cap * dagger.ApCost, MidpointRounding.AwayFromZero), Use(b, rogue, "dagger_attack", stunned).Of<ApRefunded>().Single().Ap);

        // Chill: −30 Speed. The share lost to buffs, capped.
        slowed.Stats.Add("buff:chill:x:x", "speed", -30);
        var share = Math.Min(cap, 30.0 / (slowed.Stats.Get("speed") + 30));
        Assert.Equal((int)Math.Round(share * dagger.ApCost, MidpointRounding.AwayFromZero), Use(b, rogue, "dagger_attack", slowed).Of<ApRefunded>().Single().Ap);
        slowed.Stats.RemoveSource("buff:chill:x:x");
        Assert.Empty(Use(b, rogue, "dagger_attack", slowed).Of<ApRefunded>());
    }

    [Fact]
    public void Mana_conduit_makes_elemental_spells_cheaper()
    {
        var e = U("elementalist", "e", Side.Party, ("magical_aptitude", 1), ("mana_conduit", 5));
        var bolt = Repo.Actions["fire_bolt"];
        var share = PerLevel("mana_conduit", "mana_cost") * 5;
        Assert.Equal((int)Math.Round(bolt.ManaCost * (1 + share), MidpointRounding.AwayFromZero), e.ManaCost(bolt));
        Assert.True(e.ManaCost(bolt) < bolt.ManaCost);
    }

    [Fact]
    public void Shadow_mastery_lengthens_stealth_and_adds_crit_damage_in_it()
    {
        var rogue = U("rogue", "r", Side.Party, ("shadow_mastery", 4));
        var g = Target();
        var b = new Battle(Repo, [rogue, g], seed: 1);
        var r = b.ActAt(rogue, Repo.Actions["stealth_move"], b.Grid.TileOptions(rogue, Repo.Actions["stealth_move"]).First());
        var stealth = r.Of<BuffApplied>().Single(x => x.Buff.Def.Id == "stealth");
        Assert.Equal(Repo.Buffs["stealth"].Length + (int)Math.Floor(PerLevel("shadow_mastery", "stealth_turns") * 4 + 1e-9), stealth.Remaining);
        Assert.True(rogue.Stealthed);
        var inStealth = Resolution.Damage(rogue, Repo.Actions["dagger_attack"], g, 1).CMult;
        rogue.Buffs.Clear();
        var outOf = Resolution.Damage(rogue, Repo.Actions["dagger_attack"], g, 1).CMult;
        Assert.Equal(PerLevel("shadow_mastery", "stealth_c_mult") * 4, inStealth - outOf, 6);
    }

    [Fact]
    public void Deadly_shadows_keeps_stealth_on_a_crit()
    {
        var rogue = U("rogue", "r", Side.Party, ("shadow_mastery", 1), ("deadly_shadows", 1));
        rogue.Stats.Add("test", "c_rate", 2);                      // always crits
        var g = Target();
        var b = new Battle(Repo, [rogue, g], seed: 1);
        b.ActAt(rogue, Repo.Actions["stealth_move"], b.Grid.TileOptions(rogue, Repo.Actions["stealth_move"]).First());
        Use(b, rogue, "dagger_attack", g);
        Assert.True(rogue.Stealthed);
        rogue.Stats.Add("test", "c_rate", -2);                     // never crits
        Use(b, rogue, "dagger_attack", g);
        Assert.False(rogue.Stealthed);
    }

    [Fact]
    public void Dancing_shadows_grants_stealth_on_a_dodge()
    {
        var rogue = U("rogue", "r", Side.Party, ("shadow_mastery", 1), ("dancing_shadows", 1));
        rogue.Stats.Add("test", "avoid", 0.95);
        rogue.Stats.Add("test2", "avoid", 0.95);
        var g = U("goblin_grunt", "g", Side.Enemy);
        var b = new Battle(Repo, [rogue, g], seed: 1);
        var r = Use(b, g, "goblin_slash", rogue);
        Assert.False(r.Of<Attempt>().Single().Roll.Success);
        Assert.True(rogue.Stealthed);
    }

    [Fact]
    public void Elemental_ward_gives_adjacent_allies_elemental_avoid()
    {
        var e = U("elementalist", "e", Side.Party, ("magical_aptitude", 1), ("mana_conduit", 1), ("elemental_ward", 5));
        var w = U("warrior", "w", Side.Party);
        var far = U("rogue", "r", Side.Party);
        var g = U("goblin_grunt", "g", Side.Enemy);
        var grid = new BattleGrid();
        grid.Place(e, new Tile(BattleGrid.PartyArea, 0, 0));
        grid.Place(w, new Tile(BattleGrid.PartyArea, 0, 1));
        grid.Place(far, new Tile(BattleGrid.PartyArea, 1, 2));
        grid.Place(g, new Tile(BattleGrid.EnemyArea, 0, 0));
        var b = new Battle(Repo, [e, w, far, g], seed: 1, grid);
        b.Start();
        string[] fire = ["fire", "elemental", "spell"];
        Assert.Equal(PerLevel("elemental_ward", "elemental_ward") * 5, w.Stats.Get("avoid", fire) - far.Stats.Get("avoid", fire), 6);
    }
}
