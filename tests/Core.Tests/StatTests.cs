using EternalDungeon.Core.Data;
using EternalDungeon.Core.Stats;

namespace EternalDungeon.Core.Tests;

// M1 system 1 and 2: combine modes, modifiers per source, tag stats. Numbers checked by hand.
public class StatTests
{
    const int Precision = 12;

    static StatBlock NewBlock() => new(TestData.Repo);

    // A data set with a Mult stat, since no real stat uses Mult yet.
    static readonly GameData WithMult = new(
        [new TagDef("fire", "Fire", TagGroup.Element)],
        [new StatDef("echo", "Echo", StatGroup.Attack, CombineMode.Mult, false, false)]);

    // ---- Combine modes ----

    [Fact]
    public void Add_sums_and_removing_restores()
    {
        var s = NewBlock();
        s.Add("base", "health", 100);
        s.Add("ring", "health", 25);
        Assert.Equal(125, s.Get("health"));

        s.RemoveSource("ring");
        Assert.Equal(100, s.Get("health"));
    }

    [Fact]
    public void Dim_combines_toward_but_never_reaching_one()
    {
        var s = NewBlock();
        s.Add("a", "avoid", 0.5);
        s.Add("b", "avoid", 0.5);
        Assert.Equal(0.75, s.Get("avoid"), Precision);       // 1 − 0.5 × 0.5

        s.Add("c", "avoid", 0.2);
        Assert.Equal(0.8, s.Get("avoid"), Precision);        // 1 − 0.25 × 0.8

        for (var i = 0; i < 10; i++) s.Add($"x{i}", "avoid", 0.9);
        Assert.True(s.Get("avoid") < 1);
    }

    [Fact]
    public void Dim_removal_matches_the_anchor_formula()
    {
        // Anchor: decrease = 1 − (1 − Old) / (1 − New).
        Assert.Equal(0.5, Combine.Remove(CombineMode.Dim, 0.75, 0.5), Precision);

        var s = NewBlock();
        s.Add("a", "hit", 0.3);
        s.Add("b", "hit", 0.6);
        s.Add("c", "hit", 0.25);
        var before = s.Get("hit");
        s.RemoveSource("b");
        Assert.Equal(Combine.Remove(CombineMode.Dim, before, 0.6), s.Get("hit"), Precision);
        Assert.Equal(1 - 0.7 * 0.75, s.Get("hit"), Precision);
    }

    [Fact]
    public void Mult_multiplies_and_removing_divides()
    {
        var s = new StatBlock(WithMult);
        Assert.Equal(1, s.Get("echo"));                      // identity
        s.Add("a", "echo", 2);
        s.Add("b", "echo", 1.5);
        Assert.Equal(3, s.Get("echo"), Precision);
        s.RemoveSource("a");
        Assert.Equal(1.5, s.Get("echo"), Precision);
        Assert.Equal(1.5, Combine.Remove(CombineMode.Mult, 3, 2), Precision);
    }

    [Fact]
    public void Removing_one_source_removes_all_its_modifiers_and_nothing_else()
    {
        var s = NewBlock();
        s.Add("base", "power", 10);
        s.Add("enrage", "power", 20);
        s.Add("enrage", "speed", 10);
        Assert.Equal(2, s.RemoveSource("enrage"));
        Assert.Equal(10, s.Get("power"));
        Assert.Equal(0, s.Get("speed"));
    }

    [Fact]
    public void Adding_then_removing_many_buffs_leaves_no_drift()
    {
        var s = NewBlock();
        s.Add("base", "resist", 0.1);
        for (var i = 0; i < 50; i++) s.Add($"buff{i}", "resist", 0.137);
        for (var i = 0; i < 50; i++) s.RemoveSource($"buff{i}");
        Assert.Equal(0.1, s.Get("resist"));                  // exact, not approximately
    }

    [Fact]
    public void Invalid_modifiers_are_rejected()
    {
        var s = NewBlock();
        Assert.Throws<ArgumentException>(() => s.Add("x", "hit", 1.0));          // dim must stay below 1
        Assert.Throws<ArgumentException>(() => s.Add("x", "speed", 10.5));       // integer stat
        Assert.Throws<ArgumentException>(() => s.Add("x", "health", 10, "fire")); // character stats are untagged
        Assert.Throws<ArgumentException>(() => s.Add("x", "power", 10, "lava"));  // unknown tag
        Assert.Throws<ArgumentException>(() => s.Add("x", "luck", 10));           // unknown stat
        Assert.Throws<ArgumentException>(() => new StatBlock(WithMult).Add("x", "echo", 0));
        Assert.Empty(s.Modifiers);
    }

    // ---- Tag stats ----

    [Fact]
    public void Tag_stats_apply_only_to_actions_with_that_tag()
    {
        var s = NewBlock();
        s.Add("base", "power", 10);
        s.Add("staff", "power", 50, "fire");
        s.Add("ring", "power", 20, "ice");

        Assert.Equal(60, s.Get("power", ["spell", "ranged", "fire"]));   // Fire Bolt: 10 + 50
        Assert.Equal(30, s.Get("power", ["spell", "ice"]));              // Frost Shard: 10 + 20
        Assert.Equal(10, s.Get("power", ["physical", "melee"]));         // untagged only
        Assert.Equal(80, s.Get("power", ["fire", "ice"]));               // every matching tag applies
        Assert.Equal(10, s.Get("power"));
        Assert.Equal(50, s.Get(new StatKey("power", "fire")));
    }

    [Fact]
    public void Tag_stats_use_their_stat_combine_mode()
    {
        var s = NewBlock();
        s.Add("base", "hit", 0.5);
        s.Add("bow", "hit", 0.5, "ranged");                              // "Ranged Hit 0.5"

        Assert.Equal(0.75, s.Get("hit", ["physical", "ranged"]), Precision);
        Assert.Equal(0.5, s.Get("hit", ["physical", "melee"]), Precision);
    }
}
