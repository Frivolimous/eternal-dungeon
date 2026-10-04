using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

/// <summary>One thing that happened while resolving an action or a clock event, in order.</summary>
public abstract record Outcome;

/// <summary>A success roll against a target. On a miss nothing else lands on that target.</summary>
public sealed record Attempt(Unit Target, double Chance, Roll Roll) : Outcome;

public sealed record Damaged(Unit Target, DamageBreakdown Breakdown, DamageTaken Taken, int HealthBefore) : Outcome;

public sealed record PeriodicDamaged(Unit Target, Buff Buff, DamageTaken Taken, int HealthBefore) : Outcome;

public sealed record Healed(Unit Target, string Source, int Amount, int HealthBefore) : Outcome;

public sealed record Shielded(Unit Target, string Source, int Amount) : Outcome;

public sealed record BuffApplied(Unit Target, Buff Buff, bool Refreshed) : Outcome;

public sealed record BuffExpired(Unit Target, Buff Buff) : Outcome;

public sealed record Triggered(Unit Owner, Buff Buff, TriggerDef Trigger) : Outcome;

public sealed record CastStarted(int CastTime, long ReadyAt) : Outcome;

/// <summary>A cast completed after its target fell or was otherwise gone, so it had no effect.</summary>
public sealed record Fizzled(string Reason) : Outcome;

public sealed record Died(Unit Unit) : Outcome;

/// <summary>
/// Everything one action did, the Anchor's IActionResult: the action's own hit and damage, then every effect it
/// queued (including triggered ones) in order, and finally the buffs those effects created, which apply only
/// after every effect has resolved.
/// </summary>
public sealed class ActionResult(long tick, Unit actor, ActionDef? action, Unit? target)
{
    public long Tick { get; } = tick;
    public Unit Actor { get; } = actor;
    /// <summary>Null for results that come from the clock (turn start, buff ticks) rather than an action.</summary>
    public ActionDef? Action { get; } = action;
    public Unit? Target { get; } = target;
    public List<Outcome> Outcomes { get; } = [];

    public double Time => Tick / (double)TurnClock.TicksPerTurn;

    public void Add(Outcome outcome) => Outcomes.Add(outcome);

    public IEnumerable<T> Of<T>() where T : Outcome => Outcomes.OfType<T>();
}
