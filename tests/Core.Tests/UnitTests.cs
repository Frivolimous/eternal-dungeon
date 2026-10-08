using EternalDungeon.Core.Combat;

namespace EternalDungeon.Core.Tests;

// M1 system 4: units and vitals.
public class UnitTests
{
    static Unit Make(string def, Side side = Side.Party, string? id = null) =>
        new(id ?? def, TestData.Repo.Units[def], side, TestData.Repo);

    [Fact]
    public void A_new_unit_starts_full_with_act_at_initiative()
    {
        var w = Make("warrior");
        // Its own Health and Initiative add to the default stats'.
        double Total(string stat) => TestData.Repo.UnitDefaults.Concat(TestData.Repo.Units["warrior"].Stats)
            .Where(s => s.Stat == stat && s.Tag is null).Sum(s => s.Value);
        Assert.Equal(Total("health"), w.MaxHealth);
        Assert.Equal(w.MaxHealth, w.Health);
        Assert.Equal(Total("initiative"), w.Act);
        Assert.Equal(0, w.Shield);
        Assert.True(w.Alive);
        Assert.Equal(15, w.Stats.Get("power", ["physical", "melee"]));   // Strength 15 × 1

        var e = Make("elementalist");
        Assert.Equal(TestData.Repo.Units["elementalist"].Stats.Single(s => s.Stat == "mana" && s.Tag is null).Value, e.Mana);
        Assert.Equal(e.MaxMana, e.Mana);
    }

    [Fact]
    public void Shield_absorbs_damage_before_health()
    {
        var w = Make("warrior");
        w.AddShield(14);

        Assert.Equal(new DamageTaken(14, 6, false), w.TakeDamage(20));
        Assert.Equal(0, w.Shield);
        Assert.Equal(134, w.Health);
    }

    [Fact]
    public void Health_stops_at_zero_and_the_unit_dies()
    {
        var g = Make("goblin_grunt", Side.Enemy);
        Assert.Equal(new DamageTaken(0, g.MaxHealth, true), g.TakeDamage(500));
        Assert.Equal(0, g.Health);
        Assert.False(g.Alive);
        Assert.Equal(new DamageTaken(0, 0, false), g.TakeDamage(5));    // no second kill
        Assert.Equal(0, g.Heal(30));                                     // the dead stay dead
    }

    [Fact]
    public void Healing_is_capped_at_max_health()
    {
        var w = Make("warrior");
        w.TakeDamage(30);
        Assert.Equal(30, w.Heal(50));
        Assert.Equal(140, w.Health);
    }

    [Fact]
    public void Mana_is_spent_only_when_there_is_enough()
    {
        var e = Make("elementalist");
        Assert.True(e.SpendMana(e.MaxMana - 15));
        Assert.False(e.SpendMana(20));
        Assert.Equal(15, e.Mana);
        e.RestoreMana(e.MaxMana + 100);
        Assert.Equal(e.MaxMana, e.Mana);
    }

    [Fact]
    public void Health_and_mana_never_regenerate_on_their_own()
    {
        var e = Make("elementalist");
        var g = Make("goblin_grunt", Side.Enemy);
        var battle = new Battle(TestData.Repo, [e, g], seed: 1);
        e.TakeDamage(30);
        e.SpendMana(40);
        for (var i = 0; i < 10; i++) battle.BuffTick();
        Assert.Equal(e.MaxHealth - 30, e.Health);
        Assert.Equal(e.MaxMana - 40, e.Mana);
    }

    [Fact]
    public void The_battle_ends_when_one_side_has_no_living_units()
    {
        var w = Make("warrior");
        var g1 = Make("goblin_grunt", Side.Enemy, "goblin_grunt#1");
        var g2 = Make("goblin_grunt", Side.Enemy, "goblin_grunt#2");
        Unit[] all = [w, g1, g2];

        Assert.Null(BattleRules.Winner(all));
        g1.TakeDamage(999);
        Assert.Null(BattleRules.Winner(all));
        g2.TakeDamage(999);
        Assert.Equal(Side.Party, BattleRules.Winner(all));

        w.TakeDamage(999);
        Assert.Equal(Side.Enemy, BattleRules.Winner(all));               // everyone down = party wipe
    }

    [Fact]
    public void All_starter_units_build()
    {
        foreach (var def in TestData.Repo.UnitList)
            Assert.True(new Unit(def.Id, def, Side.Enemy, TestData.Repo).Alive, def.Id);
    }
}
