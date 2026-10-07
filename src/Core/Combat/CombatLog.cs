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
/// T is battle time in base-speed turns. Every word comes from strings.csv (<see cref="Strings"/>), so the simulator
/// and the game print the same lines.
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

    static string Head(Strings s, long tick) => s.Format("log.head", ("time", T(tick))) + " ";

    public static string Ending(Battle battle)
    {
        var s = battle.Data.Text;
        return Head(s, battle.Clock.Tick) + battle.Winner switch
        {
            Side.Party => s["log.victory"],
            Side.Enemy => s["log.defeat"],
            _ => s["log.stalemate"],
        };
    }

    public static IEnumerable<string> Lines(Battle battle, ActionResult r, LogLevel level)
    {
        var s = battle.Data.Text;
        var head = Head(s, r.Tick);
        if (r.Action is { } action)
        {
            if (r.Outcomes.FirstOrDefault() is CastStarted cast)
            {
                yield return head + (r.Target is null
                    ? s.Format("log.begins_casting", ("actor", r.Actor.Name), ("action", action.Name), ("ready", T(cast.ReadyAt)))
                    : s.Format("log.begins_casting_at", ("actor", r.Actor.Name), ("action", action.Name), ("target", r.Target.Name), ("ready", T(cast.ReadyAt))));
                yield break;
            }
            var aimed = action.Target is not (ActionTarget.Self or ActionTarget.Tile) && r.Target is not null;
            yield return head + (aimed
                ? s.Format("log.action_at", ("actor", r.Actor.Name), ("action", action.Name), ("target", r.Target!.Name))
                : s.Format("log.action", ("actor", r.Actor.Name), ("action", action.Name)));
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
        var s = data.Text;
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
                    parts.Add(s.Format(a.Roll.Success ? "log.hit" : "log.miss", ("chance", Pct(a.Chance))));
                    if (level == LogLevel.Full)
                        full.Add(s.Format(a.Roll.Success ? "log.roll_hit" : "log.roll_miss", ("roll", F(a.Roll.Value, 3)), ("chance", F(a.Chance, 3))));
                    break;
                case Damaged d:
                    var kinds = new List<string>();
                    if (action is not null && DamageTags(data, action.Tags) is { Length: > 0 } type) kinds.Add(type);
                    if (crit > 0) kinds.Add(s[crit == 2 ? "log.kind_brutal" : "log.kind_crit"]);
                    parts.Add(kinds.Count > 0
                        ? s.Format("log.damage_kinds", ("amount", d.Breakdown.Final), ("kinds", string.Join(s["log.list_separator"], kinds)))
                        : s.Format("log.damage", ("amount", d.Breakdown.Final)));
                    if (d.Taken.Absorbed > 0) parts.Add(s.Format("log.absorbs", ("amount", d.Taken.Absorbed)));
                    parts.Add(s.Format("log.hp", ("target", d.Target.Name), ("before", d.HealthBefore), ("after", d.HealthBefore - d.Taken.ToHealth)));
                    if (level == LogLevel.Full) full.Add(Breakdown(s, d.Breakdown));
                    break;
                case CritRolled c when level == LogLevel.Full:
                    full.Add(s.Format("log.crit_roll", ("c_rate", F(c.CRate, 3)), ("chance", Pct(c.Chance)), ("roll", F(c.Crit.Value, 3)),
                                 ("result", s[c.Crit.Success ? "log.crit_yes" : "log.crit_no"]))
                             + (c.Brutal is { } b
                                 ? s.Format("log.brutal_roll", ("roll", F(b.Value, 3)), ("result", s[b.Success ? "log.brutal_yes" : "log.brutal_no"]))
                                 : ""));
                    break;
                case ProcRolled p:
                    if (p.Roll.Success)
                        rest.Add((p.Scale > 1.0001
                                     ? s.Format("log.proc_scaled", ("owner", p.Owner.Name), ("proc", p.Proc.Name), ("chance", Pct(p.Chance)), ("scale", F(p.Scale, 2)))
                                     : s.Format("log.proc", ("owner", p.Owner.Name), ("proc", p.Proc.Name), ("chance", Pct(p.Chance))))
                                 + (level == LogLevel.Full ? s.Format("log.proc_roll", ("roll", F(p.Roll.Value, 3))) : ""));
                    else if (level == LogLevel.Full)
                        rest.Add(s.Format("log.proc_fails", ("owner", p.Owner.Name), ("proc", p.Proc.Name), ("chance", Pct(p.Chance)), ("roll", F(p.Roll.Value, 3))));
                    break;
                case ProcDamaged pd:
                    var procKinds = DamageTags(data, pd.Proc.Tags);
                    (string, object?)[] args =
                    [
                        ("proc", pd.Proc.Name), ("amount", pd.Breakdown.Final), ("kinds", procKinds), ("target", pd.Target.Name),
                        ("before", pd.HealthBefore), ("after", pd.HealthBefore - pd.Taken.ToHealth),
                    ];
                    rest.Add(s.Format(procKinds.Length > 0 ? "log.proc_damage_kinds" : "log.proc_damage", args));
                    if (level == LogLevel.Full) rest.Add(Breakdown(s, pd.Breakdown));
                    break;
                default:
                    if (Describe(battle.Grid, s, o) is string text) rest.Add(text);
                    break;
            }
        }
        if (parts.Count > 0) yield return string.Join(s["log.separator"], parts);
        foreach (var f in full) yield return f;
        foreach (var x in rest) yield return x;
    }

    static string? Describe(BattleGrid grid, Strings s, Outcome o) => o switch
    {
        Healed h when h.Amount > 0 => s.Format("log.healed", ("source", h.Source ?? s["log.regen"]), ("target", h.Target.Name), ("amount", h.Amount),
            ("before", h.HealthBefore), ("after", h.HealthBefore + h.Amount)),
        Healed => null,
        Shielded x => s.Format("log.shielded", ("source", x.Source), ("target", x.Target.Name), ("amount", x.Amount)),
        BuffApplied b => s.Format(b.Refreshed ? "log.buff_refreshed" : "log.buff_applied", ("buff", BuffText(s, b)), ("target", b.Target.Name)),
        BuffExpired e => s.Format("log.buff_expired", ("buff", e.Buff.Def.Name), ("target", e.Target.Name)),
        PeriodicDamaged p => s.Format("log.periodic", ("buff", p.Buff.Def.Name), ("amount", p.Taken.Absorbed + p.Taken.ToHealth), ("target", p.Target.Name),
            ("before", p.HealthBefore), ("after", p.HealthBefore - p.Taken.ToHealth)),
        DelayedDamaged p => s.Format("log.delayed", ("buff", p.Buff.Def.Name), ("amount", p.Taken.Absorbed + p.Taken.ToHealth), ("target", p.Target.Name),
            ("before", p.HealthBefore), ("after", p.HealthBefore - p.Taken.ToHealth)),
        StaggerIgnored x => s.Format("log.stagger_ignored", ("target", x.Target.Name)),
        Staggered x => s.Format(x.Broke ? "log.staggered_broke" : "log.staggered", ("target", x.Target.Name), ("amount", x.Amount), ("bar", x.Bar)),
        Interrupted i => s.Format("log.interrupted", ("unit", i.Unit.Name)),
        Fizzled { Reason: FizzleReason.TargetFell } f => s.Format("log.fizzle_target_fell", ("target", f.Target?.Name)),
        Fizzled f => s.Format("log.fizzle_caster_fell", ("caster", f.Caster.Name), ("action", f.Action.Name)),
        Died d => s.Format("log.died", ("unit", d.Unit.Name)),
        Moved m when m.Why == "collapse" => s.Format("log.collapse", ("unit", m.Unit.Name), ("from", Pos(grid, s, m.Unit, m.From)), ("to", Pos(grid, s, m.Unit, m.To))),
        Moved m when m.Why == "Move" => s.Format("log.moved", ("unit", m.Unit.Name), ("from", Pos(grid, s, m.Unit, m.From)), ("to", Pos(grid, s, m.Unit, m.To))),
        Moved m => s.Format("log.moved_by", ("unit", m.Unit.Name), ("from", Pos(grid, s, m.Unit, m.From)), ("to", Pos(grid, s, m.Unit, m.To)), ("why", m.Why)),
        Waited w => s.Format("log.waited", ("unit", w.Unit.Name)),
        TurnLost l => s.Format("log.turn_lost_" + l.Reason, ("unit", l.Unit.Name)),
        _ => null,
    };

    /// <summary>A buff with what it does: "War Cry (+10 power, 3 turns, ×2)".</summary>
    public static string BuffText(Strings s, BuffApplied b)
    {
        var d = b.Buff.Def;
        var bits = new List<string>();
        if (d.PeriodicDamage > 0)
            bits.Add(s.Format("log.buff_dot", ("amount", Math.Max(1, (int)Math.Round(d.PeriodicDamage * b.Stacks * b.Buff.DotFactor, MidpointRounding.AwayFromZero)))));
        if (d.PeriodicHeal > 0) bits.Add(s.Format("log.buff_regen", ("amount", d.PeriodicHeal * b.Stacks)));
        if (d.Cc != CcKind.None) bits.Add(CcName(s, d.Cc));
        foreach (var x in d.Stats)
        {
            var value = (x.Value >= 0 ? "+" : "") + F(x.Value * b.Stacks, 2);
            bits.Add(x.Tag is null
                ? s.Format("log.buff_stat", ("value", value), ("stat", x.Stat))
                : s.Format("log.buff_stat_tagged", ("value", value), ("tag", x.Tag), ("stat", x.Stat)));
        }
        if (b.Shield > 0) bits.Add(s.Format("log.buff_shield", ("amount", b.Shield)));
        if (d.Duration == DurationKind.Turns) bits.Add(s.Format("log.buff_turns", ("turns", b.Remaining)));
        if (d.Duration == DurationKind.UntilNextTurn) bits.Add(s["log.buff_until_next_turn"]);
        if (b.Stacks > 1) bits.Add(s.Format("log.buff_stacks", ("stacks", b.Stacks)));
        return s.Format("log.buff", ("name", d.Name), ("details", string.Join(s["log.list_separator"], bits)));
    }

    /// <summary>A crowd-control kind's name: "Stun".</summary>
    public static string CcName(Strings s, CcKind cc) => s["cc." + JsonField.SnakeCase(cc.ToString())];

    static string Breakdown(Strings s, DamageBreakdown d) => s.Format("log.breakdown",
        ("base", F(d.Base, 2)), ("power_factor", F(d.PowerFactor, 3)), ("power", F(d.Power, 1)),
        ("mult_factor", F(d.MultiplierFactor, 3)), ("mult", F(d.Multiplier, 2)),
        ("resist_factor", F(d.ResistFactor, 3)), ("resist", F(d.Resist, 3)), ("pen", F(d.Penetrate, 3)),
        ("all_damage_factor", F(d.AllDamageFactor, 3)), ("all_damage", F(d.AllDamage, 2)),
        ("all_resist_factor", F(d.AllResistFactor, 3)), ("all_resist", F(d.AllResist, 3)),
        ("crit", d.CritTiers > 0
            ? s.Format("log.breakdown_crit", ("crit_factor", F(d.CritFactor, 3)), ("tiers", d.CritTiers), ("c_mult", F(d.CMult, 2)),
                ("crit_resist", F(d.CritResist, 3)), ("crit_pen", F(d.CritPenetrate, 3)))
            : ""),
        ("raw", F(d.Raw, 3)), ("final", d.Final));

    /// <summary>The damage-relevant tags (damage types and elements) in tag order: "Arcane, Fire".</summary>
    public static string DamageTags(GameData data, IEnumerable<string> tags) =>
        string.Join(data.Text["log.list_separator"], tags.Select(t => data.Tags[t]).Where(t => t.Group is TagGroup.DamageType or TagGroup.Element).Select(t => t.Name));

    /// <summary>Front-relative row (depth) and column (lane), so the log reads the same in any layout; marked when
    /// the unit stands in the other side's area.</summary>
    static string Pos(BattleGrid grid, Strings s, Unit u, Tile t)
    {
        var (depth, lane) = grid.Relative(t);
        return s.Format(grid.SideOf(t) != u.Side ? "log.pos_opposing" : "log.pos", ("row", depth), ("col", lane));
    }

    public static string T(long tick) => (tick / (double)TurnClock.TicksPerTurn).ToString("0.00", Inv);
    public static string Pct(double p) => (p * 100).ToString("0", Inv) + "%";
    static string F(double v, int decimals) => v.ToString("0." + new string('#', decimals), Inv);
}
