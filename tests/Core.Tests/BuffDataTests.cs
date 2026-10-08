using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Tests;

// The buffs table: what a buff does is key/value pairs, like a proc's results (Jeremy, 2026-10-08); stat changes stay
// in buff_stats. A buff stacks when its max_stacks is above 1.
public class BuffDataTests
{
    static GameData Load(string buff) => TestData.LoadTables(("buffs", $"[{buff}]"));

    static DataException Fails(string buff) => TestData.TablesFail(("buffs", $"[{buff}]"));

    const string Turns = "\"id\": \"b\", \"name\": \"B\", \"duration\": \"turns\", \"length\": 2";

    [Fact]
    public void Effects_are_read_from_key_value_pairs()
    {
        var b = Load($$"""{ {{Turns}}, "key_1": "shield", "value_1": "0.1", "key_2": "cc", "value_2": "stun", "key_3": "break_on_attack" }""").Buffs["b"];
        Assert.Equal(0.1, b.ShieldMaxHealth);
        Assert.Equal(CcKind.Stun, b.Cc);
        Assert.True(b.BreakOnAttack);

        var dot = Load($$"""{ {{Turns}}, "key_1": "periodic_damage", "value_1": "4", "key_2": "periodic_heal", "value_2": "2", "key_3": "delayed_damage", "value_3": "9" }""").Buffs["b"];
        Assert.Equal((4, 2, 9), (dot.PeriodicDamage, dot.PeriodicHeal, dot.DelayedDamage));

        var flagged = Load($$"""{ {{Turns}}, "key_1": "break_on_attack", "value_1": "true" }""").Buffs["b"];
        Assert.True(flagged.BreakOnAttack);                               // a flag may say true, or nothing
    }

    [Fact]
    public void A_buff_stacks_only_when_max_stacks_is_above_1()
    {
        var plain = Load($$"""{ {{Turns}} }""").Buffs["b"];
        Assert.Equal(1, plain.MaxStacks);
        Assert.False(plain.Stacking);
        var stacking = Load($$"""{ {{Turns}}, "max_stacks": 3 }""").Buffs["b"];
        Assert.True(stacking.Stacking);
        Assert.Contains("at least 1", Fails($$"""{ {{Turns}}, "max_stacks": 0 }""").Message);
    }

    [Fact]
    public void Buff_pairs_are_checked_by_key()
    {
        Assert.Contains("expected one of", Fails($$"""{ {{Turns}}, "key_1": "glow", "value_1": "1" }""").Message);
        Assert.Contains("true or nothing", Fails($$"""{ {{Turns}}, "key_1": "break_on_attack", "value_1": "yes" }""").Message);
        Assert.Contains("expected one of", Fails($$"""{ {{Turns}}, "key_1": "cc", "value_1": "dance" }""").Message);
        Assert.Contains("needs a value", Fails($$"""{ {{Turns}}, "key_1": "cc" }""").Message);
        Assert.Contains("whole number", Fails($$"""{ {{Turns}}, "key_1": "periodic_damage", "value_1": "2.5" }""").Message);
        Assert.Contains("can't be negative", Fails($$"""{ {{Turns}}, "key_1": "shield", "value_1": "-0.1" }""").Message);
        Assert.Contains("already one of", Fails($$"""{ {{Turns}}, "key_1": "cc", "value_1": "stun", "key_2": "cc", "value_2": "fear" }""").Message);
        Assert.Equal("[0].key_2", Fails($$"""{ {{Turns}}, "value_2": "3" }""").Field);
    }
}
