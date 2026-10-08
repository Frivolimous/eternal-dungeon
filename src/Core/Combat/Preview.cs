using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

/// <summary>A proc that could trigger from an action, with its chance.</summary>
/// <summary>A proc an action could set off, with its chance. <see cref="FromAction"/>: one of the action's own procs.</summary>
public sealed record ProcChance(ProcDef Proc, Unit Owner, double Chance, bool FromAction = false);

/// <summary>
/// What an action would do to one target, for the hover preview (M2 brief §6). Damage is static, so the three
/// damage numbers are exact; procs aren't in them (they're listed with their chances).
/// </summary>
/// <param name="HitChance">Chance the action succeeds (1 for actions that can't miss).</param>
/// <param name="CritChance">Chance a hit crits; a crit then goes Brutal at <see cref="BrutalChance"/>.</param>
public sealed record TargetPreview(
    Unit Target,
    double HitChance,
    int? Damage,
    int? CritDamage,
    int? BrutalDamage,
    double CritChance,
    double BrutalChance,
    IReadOnlyList<ProcChance> Procs);

/// <summary>When a unit's turns would come: when its cast completes (if the action has a cast time) and when its
/// next turn starts, in sub-ticks. An estimate at the unit's current Speed: buffs ending can move it.</summary>
public sealed record TurnEstimate(long? CastCompletes, long? NextTurn);

/// <summary>An upcoming turn on the timeline.</summary>
public sealed record UpcomingTurn(Unit Unit, long Tick);

/// <summary>
/// Previews for the battle screen. Everything here is pure: no rolls, no state changes, so showing a preview can
/// never change how a battle plays out (a test checks it).
/// </summary>
public static class Preview
{
    public static TargetPreview Target(Battle battle, Unit actor, ActionDef action, Unit target)
    {
        var hit = action.Target == ActionTarget.Enemy ? Resolution.SuccessChance(actor, action, target) : 1;
        int? normal = null, crit = null, brutal = null;
        double c = 0;
        if (action.DealsDamage)
        {
            normal = Resolution.Damage(actor, action, target).Final;
            crit = Resolution.Damage(actor, action, target, 1).Final;
            brutal = Resolution.Damage(actor, action, target, 2).Final;
            c = Resolution.CritChance(Resolution.CRate(actor, action, target));
        }
        return new TargetPreview(target, hit, normal, crit, brutal, c, c, Procs(battle, actor, action, target));
    }

    static readonly ProcTrigger[] ActorTriggers = [ProcTrigger.Hit, ProcTrigger.Crit, ProcTrigger.Brutal, ProcTrigger.Miss, ProcTrigger.ActionComplete];
    static readonly ProcTrigger[] TargetTriggers = [ProcTrigger.Struck, ProcTrigger.Damaged, ProcTrigger.Avoided];

    /// <summary>The procs this action could set off, the actor's and the target's, merged per proc as the battle
    /// would roll them.</summary>
    public static List<ProcChance> Procs(Battle battle, Unit actor, ActionDef action, Unit target)
    {
        var list = new List<ProcChance>();
        void From(Unit owner, Unit other, ProcTrigger[] triggers, ActionDef? own)
        {
            foreach (var group in battle.ProcsOf(owner, own)
                         .Where(p => triggers.Contains(p.Def.Trigger))
                         .Where(p => p.Def.TriggerTags.Count == 0 || action.Tags.Any(p.Def.TriggerTags.Contains))
                         .GroupBy(p => p.Def.Id))
            {
                var def = group.First().Def;
                var on = def.Target == ProcTarget.Self ? owner : other;
                var chances = group.Select(_ => Battle.CopyChance(def, owner, on)).ToList();
                var chance = def.Duplicates switch
                {
                    Duplicates.Merge => Battle.MergeCopies(chances.Select(x => (x, 1.0))).Chance,
                    Duplicates.Unique => chances.Max(),
                    _ => 1 - chances.Aggregate(1.0, (miss, x) => miss * (1 - x)),
                };
                list.Add(new ProcChance(def, owner, chance, group.Any(p => p.FromAction)));
            }
        }
        From(actor, target, ActorTriggers, action);
        if (action.Target == ActionTarget.Enemy) From(target, actor, TargetTriggers, null);
        return list;
    }

    /// <summary>Where <paramref name="unit"/>'s next turn would land after using <paramref name="action"/> now.</summary>
    public static TurnEstimate NextTurn(Battle battle, Unit unit, ActionDef action)
    {
        var now = battle.Clock.Tick;
        var act = unit.ActTicks - (long)action.ApCost * TurnClock.TicksPerTurn;
        var start = now + action.CastTime;                        // the meter is frozen while casting
        long? cast = action.CastTime > 0 ? start : null;
        return new TurnEstimate(cast, unit.Speed > 0 ? start + TicksToFill(act, unit.Speed) : null);
    }

    static long TicksToFill(long act, int speed) =>
        act >= TurnClock.TurnThreshold ? 0 : (TurnClock.TurnThreshold - act + speed - 1) / speed;

    /// <summary>
    /// The next <paramref name="count"/> turns, in order, assuming each unit keeps its current Speed and spends
    /// 100 AP a turn (an estimate for the timeline). Casting units come back when their cast completes; units that
    /// can't act (dead, Speed 0) are left out. Ties go to the higher Act, then Speed, then list order, as in the clock.
    /// </summary>
    public static List<UpcomingTurn> Timeline(Battle battle, int count)
    {
        var meters = battle.Units
            .Where(u => u.Alive && u.Speed > 0)
            .Select(u => (Unit: u, Act: u.ActTicks, From: u.Casting?.CompletesAt ?? battle.Clock.Tick))
            .ToList();
        var turns = new List<UpcomingTurn>();
        while (turns.Count < count && meters.Count > 0)
        {
            var best = -1;
            long bestTick = 0, bestAct = 0;
            for (var i = 0; i < meters.Count; i++)
            {
                var (u, act, from) = meters[i];
                var dt = TicksToFill(act, u.Speed);
                var tick = from + dt;
                var actThen = act + dt * u.Speed;
                if (best < 0 || tick < bestTick || (tick == bestTick && (actThen > bestAct ||
                        (actThen == bestAct && u.Speed > meters[best].Unit.Speed))))
                    (best, bestTick, bestAct) = (i, tick, actThen);
            }
            var m = meters[best];
            turns.Add(new UpcomingTurn(m.Unit, bestTick));
            meters[best] = (m.Unit, bestAct - 100L * TurnClock.TicksPerTurn, bestTick);
        }
        return turns;
    }
}
