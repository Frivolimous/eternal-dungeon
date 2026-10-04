using System.Globalization;
using System.Text;
using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

public enum LogLevel
{
    /// <summary>Actions and what they did.</summary>
    Brief,
    /// <summary>Adds every roll and every damage factor, so any number can be checked by hand.</summary>
    Full,
}

/// <summary>
/// Turns a battle's results into the readable combat log (brief format from the M1 build brief):
/// <code>
/// [T 3.40] Warrior → Power Attack → Goblin Grunt #1
///          hit (82%) · 34 dmg (Physical) · Goblin Grunt #1 HP 46 → 12
/// </code>
/// T is battle time in base-speed turns.
/// </summary>
public static class CombatLog
{
    const string Indent = "         ";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Write(Battle battle, LogLevel level = LogLevel.Brief)
    {
        var sb = new StringBuilder();
        foreach (var r in battle.Results)
            foreach (var line in Lines(battle.Data, r, level))
                sb.AppendLine(line);
        sb.AppendLine(Ending(battle));
        return sb.ToString();
    }

    public static string Ending(Battle battle) => battle.Winner switch
    {
        Side.Party => $"[T {T(battle.Clock.Tick)}] Victory: the party wins.",
        Side.Enemy => $"[T {T(battle.Clock.Tick)}] Defeat: the party has fallen.",
        _ => $"[T {T(battle.Clock.Tick)}] Stalemate: no winner by the time limit.",
    };

    public static IEnumerable<string> Lines(GameData data, ActionResult r, LogLevel level)
    {
        var head = $"[T {T(r.Tick)}] ";
        if (r.Action is { } action)
        {
            if (r.Outcomes.FirstOrDefault() is CastStarted cast)
            {
                yield return $"{head}{r.Actor.Name} begins casting {action.Name}{(r.Target is null ? "" : $" at {r.Target.Name}")} (ready at T {T(cast.ReadyAt)})";
                yield break;
            }
            var aim = action.Target switch
            {
                ActionTarget.Self => "",
                ActionTarget.Tile => "",
                _ => r.Target is null ? "" : $" → {r.Target.Name}",
            };
            yield return $"{head}{r.Actor.Name} → {action.Name}{aim}";
            foreach (var line in Details(data, action, r.Outcomes, level))
                yield return Indent + line;
            yield break;
        }

        // Clock results (turn start, buff ticks, skips): one line per thing that happened.
        foreach (var line in Details(data, null, r.Outcomes, level))
            yield return head + line;
    }

    static IEnumerable<string> Details(GameData data, ActionDef? action, List<Outcome> outcomes, LogLevel level)
    {
        // The action's own hit and damage share a line, as in the brief.
        var parts = new List<string>();
        var full = new List<string>();
        var rest = new List<string>();
        foreach (var o in outcomes)
        {
            switch (o)
            {
                case Attempt a:
                    parts.Add($"{(a.Roll.Success ? "hit" : "miss")} ({Pct(a.Chance)})");
                    if (level == LogLevel.Full)
                        full.Add($"roll {F(a.Roll.Value, 3)} vs {F(a.Chance, 3)} → {(a.Roll.Success ? "hit" : "miss")}");
                    break;
                case Damaged d:
                    var type = action is null ? "" : DamageType(data, action);
                    parts.Add($"{d.Breakdown.Final} dmg{(type.Length > 0 ? $" ({type})" : "")}");
                    if (d.Taken.Absorbed > 0) parts.Add($"Shield absorbs {d.Taken.Absorbed}");
                    parts.Add($"{d.Target.Name} HP {d.HealthBefore} → {d.HealthBefore - d.Taken.ToHealth}");
                    if (level == LogLevel.Full) full.Add(Breakdown(d.Breakdown));
                    break;
                default:
                    if (Describe(o) is string text) rest.Add(text);
                    break;
            }
        }
        if (parts.Count > 0) yield return string.Join(" · ", parts);
        foreach (var f in full) yield return f;
        foreach (var x in rest) yield return x;
    }

    static string? Describe(Outcome o) => o switch
    {
        Healed h when h.Amount > 0 => $"{h.Source} heals {h.Target.Name} for {h.Amount} (HP {h.HealthBefore} → {h.HealthBefore + h.Amount})",
        Healed => null,
        Shielded s => $"{s.Source} shields {s.Target.Name} for {s.Amount}",
        BuffApplied b => $"{(b.Refreshed ? "refreshes" : "applies")} {BuffText(b)} on {b.Target.Name}",
        BuffExpired e => $"{e.Buff.Def.Name} on {e.Target.Name} ends",
        PeriodicDamaged p => $"{p.Buff.Def.Name} deals {p.Taken.Absorbed + p.Taken.ToHealth} to {p.Target.Name} (HP {p.HealthBefore} → {p.HealthBefore - p.Taken.ToHealth})",
        DelayedDamaged p => $"{p.Buff.Def.Name} bursts for {p.Taken.Absorbed + p.Taken.ToHealth} on {p.Target.Name} (HP {p.HealthBefore} → {p.HealthBefore - p.Taken.ToHealth})",
        Triggered t => $"{t.Buff.Def.Name} on {t.Owner.Name} triggers",
        Staggered s => $"staggers {s.Target.Name} +{s.Amount} (bar {s.Bar}{(s.Broke ? ", broken: stunned" : "")})",
        Interrupted i => $"{i.Unit.Name}'s cast is interrupted",
        Fizzled f => $"fizzles: {f.Reason}",
        Died d => $"{d.Unit.Name} falls",
        Moved m when m.Why == "collapse" => $"{m.Unit.Name} steps forward ({Pos(m.Unit, m.From)} → {Pos(m.Unit, m.To)})",
        Moved m => $"{m.Unit.Name} moves {Pos(m.Unit, m.From)} → {Pos(m.Unit, m.To)}{(m.Why is "Move" ? "" : $" ({m.Why})")}",
        Waited w => $"{w.Unit.Name} waits",
        TurnLost l => $"{l.Unit.Name} loses the turn ({l.Reason})",
        _ => null,
    };

    static string BuffText(BuffApplied b)
    {
        var d = b.Buff.Def;
        var bits = new List<string>();
        if (d.PeriodicDamage > 0) bits.Add($"DoT {d.PeriodicDamage * b.Stacks}/turn");
        if (d.PeriodicHeal > 0) bits.Add($"regen {d.PeriodicHeal * b.Stacks}/turn");
        if (d.Cc != CcKind.None) bits.Add(d.Cc.ToString());
        foreach (var s in d.Stats)
            bits.Add($"{(s.Value >= 0 ? "+" : "")}{F(s.Value * b.Stacks, 2)} {(s.Tag is null ? "" : s.Tag + " ")}{s.Stat}");
        if (b.Shield > 0) bits.Add($"Shield {b.Shield}");
        bits.Add(d.Duration switch
        {
            DurationKind.Turns => $"{b.Remaining} turns",
            DurationKind.UntilNextTurn => "until next turn",
            _ => "",
        });
        if (b.Stacks > 1) bits.Add($"×{b.Stacks}");
        return $"{d.Name} ({string.Join(", ", bits.Where(x => x.Length > 0))})";
    }

    static string Breakdown(DamageBreakdown d) =>
        $"dmg = base {F(d.Base, 2)}"
        + $" × power {F(d.PowerFactor, 3)} ({F(d.Power, 1)})"
        + $" × mult {F(d.MultiplierFactor, 3)} ({F(d.Multiplier, 2)})"
        + $" × resist {F(d.ResistFactor, 3)} (res {F(d.Resist, 3)}, pen {F(d.Penetrate, 3)})"
        + $" × all dmg {F(d.AllDamageFactor, 3)} ({F(d.AllDamage, 2)})"
        + $" × all res {F(d.AllResistFactor, 3)} ({F(d.AllResist, 3)})"
        + $" = {F(d.Raw, 3)} → {d.Final}";

    static string DamageType(GameData data, ActionDef action) =>
        action.Tags.Select(t => data.Tags[t]).FirstOrDefault(t => t.Group == TagGroup.DamageType)?.Name ?? "";

    /// <summary>Row and column; marked when the unit stands in the other side's area.</summary>
    static string Pos(Unit u, Tile t) => $"r{t.Row}c{t.Col}{(t.Area != u.Side ? " (opposing area)" : "")}";

    public static string T(long tick) => (tick / (double)TurnClock.TicksPerTurn).ToString("0.00", Inv);
    static string Pct(double p) => (p * 100).ToString("0", Inv) + "%";
    static string F(double v, int decimals) => v.ToString("0." + new string('#', decimals), Inv);
}
