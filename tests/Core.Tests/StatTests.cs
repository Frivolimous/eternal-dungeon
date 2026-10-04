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

    // ---- Negative chance modifiers ----

    [Fact]
    public void Negative_dim_modifiers_stack_separately_and_are_subtracted()
    {
        var s = NewBlock();
        s.Add("a", "avoid", 0.5);
        s.Add("b", "avoid", 0.2);      // P = 1 − 0.5 × 0.8 = 0.6
        s.Add("curse1", "avoid", -0.2);
        s.Add("curse2", "avoid", -0.2); // N = 1 − 0.8 × 0.8 = 0.36
        Assert.Equal(0.24, s.Get("avoid"), Precision);

        s.RemoveSource("a");
        s.RemoveSource("b");
        Assert.Equal(-0.36, s.Get("avoid"), Precision);  // can go below 0
        Assert.Throws<ArgumentException>(() => s.Add("x", "avoid", -1));
    }

    // ---- Compound stats ----

    [Fact]
    public void Strength_scales_with_the_heavy_and_light_adjusters()
    {
        var s = NewBlock();
        s.Add("base", "strength", 10);

        Assert.Equal(15, s.Get("power", ["physical", "melee", "heavy"]));   // 10 × (1 + 0.5)
        Assert.Equal(5, s.Get("power", ["physical", "melee", "light"]));    // 10 × (1 − 0.5)
        Assert.Equal(10, s.Get("power", ["physical", "melee"]));
        Assert.Equal(5, s.Get("power", ["physical", "ranged", "heavy"]));   // adjusters apply on their own
        Assert.Equal(0, s.Get("power", ["spell", "fire"]));
        Assert.Equal(0, s.Get("power"));                                    // compounds only feed tag stats
    }

    [Fact]
    public void Light_weapons_split_between_strength_and_dexterity()
    {
        var s = NewBlock();
        s.Add("base", "strength", 10);
        s.Add("base", "dexterity", 10);
        s.Add("base", "power", 4);
        // Dagger Attack (Melee Light Finesse): Str 10 × 0.5 + Dex 10 × (0.5 + 0.5) + 4 untagged.
        Assert.Equal(19, s.Get("power", ["physical", "melee", "light", "finesse"]));
    }

    [Fact]
    public void Every_matching_row_counts()
    {
        var s = NewBlock();
        s.Add("base", "elemental", 10);
        Assert.Equal(20, s.Get("power", ["spell", "fire", "ice"]));         // 10 × (1 + 1)
    }

    [Fact]
    public void Compound_points_are_percent_on_chance_stats()
    {
        var s = NewBlock();
        s.Add("base", "hit", 0.95);
        s.Add("base", "accuracy", 10);
        Assert.Equal(1 - 0.05 * 0.9, s.Get("hit", ["physical", "melee"]), Precision);  // 0.95 dim 0.10 = 0.955

        s.Add("base", "dodge", 20);
        Assert.Equal(0.3, s.Get("avoid", ["physical", "projectile", "grenade"]), Precision);  // 0.20 × 1.5

        s.Add("base", "intellect", 10);
        Assert.Equal(10, s.Get("power", ["gadget"]));
        Assert.Equal(0.1, s.Get("rate", ["gadget"]), Precision);
        Assert.Equal(0.15, s.Get("rate", ["gadget", "cryptic"]), Precision);
    }

    [Fact]
    public void Compound_stats_are_capped_at_100_points()
    {
        var s = NewBlock();
        s.Add("base", "strength", 80);
        s.Add("rage", "strength", 50);
        Assert.Equal(100, s.GetCompound("strength"));
        Assert.Equal(100, s.Get("power", ["melee"]));
        Assert.Equal(150, s.Get("power", ["melee", "heavy"]));                // the cap is on points, not contributions
    }

    [Fact]
    public void Compounds_above_95_are_flagged_and_the_repo_has_none()
    {
        Assert.Empty(Data.DataWarnings.Check(TestData.Repo));
        var data = new Data.GameData(TestData.Repo.TagList, TestData.Repo.StatList, TestData.Repo.CompoundList,
            [TestData.Repo.Units["warrior"] with { Compounds = new Dictionary<string, double> { ["strength"] = 96 } }]);
        Assert.Contains("Strength 96", Assert.Single(Data.DataWarnings.Check(data)));
    }

    [Fact]
    public void A_compound_alone_cannot_reach_full_chance()
    {
        var s = NewBlock();
        s.Add("base", "dodge", 100);
        Assert.Equal(StatBlock.MaxCompoundChance, s.Get("avoid", ["projectile", "grenade"]), Precision);  // 1.5 capped
    }

    [Fact]
    public void Compound_modifiers_add_and_leave_with_their_source()
    {
        var s = NewBlock();
        s.Add("base", "strength", 10);
        s.Add("enrage", "strength", 5);
        Assert.Equal(15, s.GetCompound("strength"));
        Assert.Equal(15, s.Get("power", ["melee"]));

        s.RemoveSource("enrage");
        Assert.Equal(10, s.Get("power", ["melee"]));
        Assert.Throws<ArgumentException>(() => s.Add("x", "strength", 5, "melee"));
    }
}
