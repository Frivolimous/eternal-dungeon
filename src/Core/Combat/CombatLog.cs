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
            foreach (var line in Lines(battle, r, level))
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

    public static IEnumerable<string> Lines(Battle battle, ActionResult r, LogLevel level)
    {
        var data = battle.Data;
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
            foreach (var line in Details(battle, action, r.Outcomes, level))
                yield return Indent + line;
            yield break;
        }

        // Clock results (turn start, buff ticks, skips): one line per thing that happened.
        foreach (var line in Details(battle, null, r.Outcomes, level))
            yield return head + line;
    }

    static IEnumerable<string> Details(Battle battle, ActionDef? action, List<Outcome> outcomes, LogLevel level)
    {
        var data = battle.Data;
        // The action's own hit and damage share a line, as in the brief.
        var parts = new List<string>();
        var full = new List<string>();
        var rest = new List<string>();
        var crit = outcomes.OfType<CritRolled>().FirstOrDefault()?.Tiers ?? 0;
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
                    var type = action is null ? "" : DamageTags(data, action.Tags);
                    var kinds = new[] { type, crit switch { 2 => "Brutal", 1 => "crit", _ => "" } }.Where(k => k.Length > 0).ToList();
                    parts.Add($"{d.Breakdown.Final} dmg{(kinds.Count > 0 ? $" ({string.Join(", ", kinds)})" : "")}");
                    if (d.Taken.Absorbed > 0) parts.Add($"Shield absorbs {d.Taken.Absorbed}");
                    parts.Add($"{d.Target.Name} HP {d.HealthBefore} → {d.HealthBefore - d.Taken.ToHealth}");
                    if (level == LogLevel.Full) full.Add(Breakdown(d.Breakdown));
                    break;
                case CritRolled c when level == LogLevel.Full:
                    full.Add($"crit rating {F(c.Rating, 3)} → {Pct(c.Chance)}: roll {F(c.Crit.Value, 3)} → {(c.Crit.Success ? "crit" : "no crit")}"
                        + (c.Brutal is { } b ? $"; Brutal roll {F(b.Value, 3)} → {(b.Success ? "Brutal" : "no Brutal")}" : ""));
                    break;
                case ProcRolled p:
                    if (p.Roll.Success)
                        rest.Add($"{p.Owner.Name}'s {p.Proc.Name} procs ({Pct(p.Chance)}{(p.Scale > 1.0001 ? $", ×{F(p.Scale, 2)}" : "")})"
                            + (level == LogLevel.Full ? $": roll {F(p.Roll.Value, 3)}" : ""));
                    else if (level == LogLevel.Full)
                        rest.Add($"{p.Owner.Name}'s {p.Proc.Name} doesn't proc ({Pct(p.Chance)}): roll {F(p.Roll.Value, 3)}");
                    break;
                case ProcDamaged pd:
                    rest.Add($"{pd.Proc.Name} deals {pd.Breakdown.Final} dmg{ProcTypes(data, pd.Proc)} to {pd.Target.Name} (HP {pd.HealthBefore} → {pd.HealthBefore - pd.Taken.ToHealth})");
                    if (level == LogLevel.Full) rest.Add(Breakdown(pd.Breakdown));
                    break;
                default:
                    if (Describe(battle.Grid, o) is string text) rest.Add(text);
                    break;
            }
        }
        if (parts.Count > 0) yield return string.Join(" · ", parts);
        foreach (var f in full) yield return f;
        foreach (var x in rest) yield return x;
    }

    static string? Describe(BattleGrid grid, Outcome o) => o switch
    {
        Healed h when h.Amount > 0 => $"{h.Source} heals {h.Target.Name} for {h.Amount} (HP {h.HealthBefore} → {h.HealthBefore + h.Amount})",
        Healed => null,
        Shielded s => $"{s.Source} shields {s.Target.Name} for {s.Amount}",
        BuffApplied b => $"{(b.Refreshed ? "refreshes" : "applies")} {BuffText(b)} on {b.Target.Name}",
        BuffExpired e => $"{e.Buff.Def.Name} on {e.Target.Name} ends",
        PeriodicDamaged p => $"{p.Buff.Def.Name} deals {p.Taken.Absorbed + p.Taken.ToHealth} to {p.Target.Name} (HP {p.HealthBefore} → {p.HealthBefore - p.Taken.ToHealth})",
        DelayedDamaged p => $"{p.Buff.Def.Name} bursts for {p.Taken.Absorbed + p.Taken.ToHealth} on {p.Target.Name} (HP {p.HealthBefore} → {p.HealthBefore - p.Taken.ToHealth})",
        StaggerIgnored s => $"{s.Target.Name} is broken: no stagger",
        Staggered s => $"staggers {s.Target.Name} +{s.Amount} (bar {s.Bar}{(s.Broke ? ", broken: stunned" : "")})",
        Interrupted i => $"{i.Unit.Name}'s cast is interrupted",
        Fizzled f => f.Reason.Contains(" fizzles") ? f.Reason : $"fizzles: {f.Reason}",
        Died d => $"{d.Unit.Name} falls",
        Moved m when m.Why == "collapse" => $"{m.Unit.Name} steps forward ({Pos(grid, m.Unit, m.From)} → {Pos(grid, m.Unit, m.To)})",
        Moved m => $"{m.Unit.Name} moves {Pos(grid, m.Unit, m.From)} → {Pos(grid, m.Unit, m.To)}{(m.Why is "Move" ? "" : $" ({m.Why})")}",
        Waited w => $"{w.Unit.Name} waits",
        TurnLost l => $"{l.Unit.Name} loses the turn ({l.Reason})",
        _ => null,
    };

    static string BuffText(BuffApplied b)
    {
        var d = b.Buff.Def;
        var bits = new List<string>();
        if (d.PeriodicDamage > 0) bits.Add($"DoT {Math.Max(1, (int)Math.Round(d.PeriodicDamage * b.Stacks * b.Buff.DotFactor, MidpointRounding.AwayFromZero))}/turn");
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
        + (d.CritTiers > 0 ? $" × crit {F(d.CritFactor, 3)} ({d.CritTiers} × mult {F(d.CritMult, 2)}, crit res {F(d.CritResist, 3)}, crit pen {F(d.CritPenetrate, 3)})" : "")
        + $" = {F(d.Raw, 3)} → {d.Final}";

    static string ProcTypes(GameData data, ProcDef proc) =>
        DamageTags(data, proc.Tags) is { Length: > 0 } names ? $" ({names})" : "";

    /// <summary>The damage-relevant tags (damage types and elements) in tag order: "Arcane, Fire".</summary>
    static string DamageTags(GameData data, IEnumerable<string> tags) =>
        string.Join(", ", tags.Select(t => data.Tags[t]).Where(t => t.Group is TagGroup.DamageType or TagGroup.Element).Select(t => t.Name));

    /// <summary>Front-relative row (depth) and column (lane), so the log reads the same in any layout; marked when
    /// the unit stands in the other side's area.</summary>
    static string Pos(BattleGrid grid, Unit u, Tile t)
    {
        var (depth, lane) = grid.Relative(t);
        return $"r{depth}c{lane}{(grid.SideOf(t) != u.Side ? " (opposing area)" : "")}";
    }

    public static string T(long tick) => (tick / (double)TurnClock.TicksPerTurn).ToString("0.00", Inv);
    static string Pct(double p) => (p * 100).ToString("0", Inv) + "%";
    static string F(double v, int decimals) => v.ToString("0." + new string('#', decimals), Inv);
}
