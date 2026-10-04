namespace EternalDungeon.Core.Tests;

public class RngTests
{
    [Fact]
    public void The_same_seed_gives_the_same_sequence()
    {
        var a = new Rng(42);
        var b = new Rng(42);
        for (var i = 0; i < 100; i++)
            Assert.Equal(a.NextULong(), b.NextULong());
    }

    [Fact]
    public void Different_seeds_differ()
    {
        Assert.NotEqual(new Rng(1).NextULong(), new Rng(2).NextULong());
    }

    [Fact]
    public void The_sequence_is_pinned()
    {
        // If this changes, every seeded combat log changes: only on purpose.
        var r = new Rng(7);
        Assert.Equal(12923355070828475994UL, r.NextULong());
        Assert.Equal([278, 839, 981], new[] { r.NextInt(1000), r.NextInt(1000), r.NextInt(1000) });
    }

    [Fact]
    public void Doubles_are_in_range_and_rolls_compare_against_the_chance()
    {
        var r = new Rng(3);
        for (var i = 0; i < 10_000; i++)
        {
            var d = r.NextDouble();
            Assert.InRange(d, 0, 0.9999999999999999);
        }
        Assert.False(new Rng(3).Roll(0).Success);
        Assert.True(new Rng(3).Roll(1).Success);
    }
}
