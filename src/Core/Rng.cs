namespace EternalDungeon.Core;

/// <summary>
/// The one source of randomness: seeded, passed in, never global. xoshiro256** seeded by SplitMix64, so the
/// sequence is fixed by this code rather than by the .NET version. Same seed, same rolls.
/// </summary>
public sealed class Rng
{
    ulong s0, s1, s2, s3;

    public Rng(ulong seed)
    {
        var x = seed;
        s0 = SplitMix(ref x);
        s1 = SplitMix(ref x);
        s2 = SplitMix(ref x);
        s3 = SplitMix(ref x);
    }

    static ulong SplitMix(ref ulong x)
    {
        var z = x += 0x9E3779B97F4A7C15;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
        return z ^ (z >> 31);
    }

    public ulong NextULong()
    {
        var result = ulong.RotateLeft(s1 * 5, 7) * 9;
        var t = s1 << 17;
        s2 ^= s0;
        s3 ^= s1;
        s1 ^= s2;
        s0 ^= s3;
        s2 ^= t;
        s3 = ulong.RotateLeft(s3, 45);
        return result;
    }

    /// <summary>A double in [0, 1).</summary>
    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

    /// <summary>An int in [0, max).</summary>
    public int NextInt(int max)
    {
        if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max));
        return (int)(NextDouble() * max);
    }

    /// <summary>Rolls against <paramref name="chance"/> (0–1). The roll is returned for full combat logs.</summary>
    public Roll Roll(double chance)
    {
        var value = NextDouble();
        return new Roll(chance, value, value < chance);
    }
}

/// <summary>One chance roll: succeeded when <see cref="Value"/> &lt; <see cref="Chance"/>.</summary>
public readonly record struct Roll(double Chance, double Value, bool Success);
