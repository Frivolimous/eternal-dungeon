using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Stats;

/// <summary>
/// The three combine modes (Anchor: Stat system › Combine modes). Totals are always recomputed from the
/// full modifier list (<see cref="Total"/>), so removing a modifier leaves no rounding drift; <see cref="Apply"/>
/// and <see cref="Remove"/> are the Anchor's incremental formulas (within one sign group for Dim), kept for
/// display and for tests that check the two approaches agree.
/// </summary>
public static class Combine
{
    /// <summary>The value of a stat with no modifiers.</summary>
    public static double Identity(CombineMode mode) => mode == CombineMode.Mult ? 1 : 0;

    public static double Apply(CombineMode mode, double old, double value) => mode switch
    {
        CombineMode.Add => old + value,
        CombineMode.Dim => 1 - (1 - old) * (1 - value),
        CombineMode.Mult => old * value,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    public static double Remove(CombineMode mode, double old, double value) => mode switch
    {
        CombineMode.Add => old - value,
        CombineMode.Dim => 1 - (1 - old) / (1 - value),
        CombineMode.Mult => old / value,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    /// <summary>
    /// The combined value of <paramref name="values"/>. For Dim, positive and negative modifiers stack
    /// separately and the negatives' total is subtracted: +0.5 and +0.2 give P = 0.6, −0.2 and −0.2 give
    /// N = 0.36, so the total is 0.24. The total can go below 0 (Jeremy, 2026-10-04).
    /// </summary>
    public static double Total(CombineMode mode, IEnumerable<double> values)
    {
        if (mode != CombineMode.Dim)
            return Fold(mode, values) ?? Identity(mode);
        var positive = Fold(mode, values.Where(v => v > 0)) ?? 0;
        var negative = Fold(mode, values.Where(v => v < 0).Select(v => -v)) ?? 0;
        return positive - negative;
    }

    // Starts from the first value rather than the identity: for Dim, 1 − (1 − 0.1) isn't exactly 0.1.
    static double? Fold(CombineMode mode, IEnumerable<double> values)
    {
        double? total = null;
        foreach (var v in values)
            total = total is double t ? Apply(mode, t, v) : v;
        return total;
    }

    /// <summary>Throws when <paramref name="value"/> can't be a modifier of <paramref name="stat"/>.</summary>
    public static void Validate(StatDef stat, double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentException($"{stat.Name} modifier must be a finite number, got {value}");
        if (stat.Combine == CombineMode.Dim && Math.Abs(value) >= 1)
            throw new ArgumentException($"{stat.Name} combines by dim, so a modifier must be between −1 and 1, got {value}");
        if (stat.Combine == CombineMode.Mult && value <= 0)
            throw new ArgumentException($"{stat.Name} combines by mult, so a modifier must be above 0, got {value}");
        if (stat.Integer && value != Math.Floor(value))
            throw new ArgumentException($"{stat.Name} is an integer stat, got {value}");
    }
}
