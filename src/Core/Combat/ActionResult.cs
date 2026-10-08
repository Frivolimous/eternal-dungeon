using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

/// <summary>One thing that happened while resolving an action or a clock event, in order.</summary>
public abstract record Outcome;

/// <summary>A success roll against a target. On a miss nothing else lands on that target.</summary>
public sealed record Attempt(Unit Target, double Chance, Roll Roll) : Outcome;

/// <summary>The crit rolls on a hit: a crit at <see cref="Chance"/>, then a Brutal crit at the same chance.
/// <see cref="Tiers"/>: 0 none, 1 crit, 2 Brutal.</summary>
public sealed record CritRolled(Unit Target, double CRate, double Chance, Roll Crit, Roll? Brutal) : Outcome
{
    public int Tiers => !Crit.Success ? 0 : Brutal is { Success: true } ? 2 : 1;
}

public sealed record Damaged(Unit Target, DamageBreakdown Breakdown, DamageTaken Taken, int HealthBefore) : Outcome;

public sealed record PeriodicDamaged(Unit Target, Buff Buff, DamageTaken Taken, int HealthBefore) : Outcome;

/// <summary>Healing. <see cref="Source"/> is the name of the effect, buff or proc that healed.</summary>
public sealed record Healed(Unit Target, string Source, int Amount, int HealthBefore) : Outcome;

public sealed record Shielded(Unit Target, string Source, int Amount) : Outcome;

/// <summary>A buff landed. Stacks, turns left and Shield are as they were at that moment.</summary>
public sealed record BuffApplied(Unit Target, Buff Buff, bool Refreshed, int Stacks, int Remaining, int Shield) : Outcome;

public sealed record BuffExpired(Unit Target, Buff Buff) : Outcome;

/// <summary>A proc's roll. <see cref="Scale"/> multiplies its amounts (only merged copies raise it).</summary>
/// <summary>A proc rolled. <see cref="FromAction"/>: it's one of the action's own procs (Shield Bash's Daze).</summary>
public sealed record ProcRolled(Unit Owner, ProcDef Proc, Unit Target, double Chance, Roll Roll, double Scale, bool FromAction = false) : Outcome;

/// <summary>Damage from a proc: through the damage formula with the proc's tags, but not an action.</summary>
public sealed record ProcDamaged(Unit Owner, Unit Target, ProcDef Proc, DamageBreakdown Breakdown, DamageTaken Taken, int HealthBefore) : Outcome;

public sealed record CastStarted(int CastTime, long ReadyAt) : Outcome;

/// <summary>A cast completed after its target fell or was otherwise gone, so it had no effect.</summary>
public enum FizzleReason { TargetFell, CasterFell }

/// <summary>A cast that came to nothing: its target or its caster fell first.</summary>
public sealed record Fizzled(FizzleReason Reason, Unit Caster, ActionDef Action, Unit? Target) : Outcome;

public sealed record Died(Unit Unit) : Outcome;

/// <summary>A unit changed tiles: by Move (the Rogue's can enter the enemy area), pushed or pulled, or the area collapsing forward.</summary>
public sealed record Moved(Unit Unit, Tile From, Tile To, string Why) : Outcome;

/// <summary>An area gained a back row (a unit forced out of the other side's area had nowhere else to go).</summary>
public sealed record AreaGrew(Side Side, int Area) : Outcome;

/// <summary>Stagger: the target lost <see cref="Amount"/> Act (its next turn comes that much later); Force Deval
/// resisted <see cref="Resisted"/> of it.</summary>
public sealed record Staggered(Unit Target, int Amount, int Resisted) : Outcome;

/// <summary>An enemy planned its next turn, or changed its plan (<see cref="Triggered"/>: because of something a unit
/// did, so the intent flashes).</summary>
public sealed record IntentSet(Unit Unit, Intent Intent, IntentReason Why) : Outcome
{
    public bool Triggered => Why is not (IntentReason.BattleStart or IntentReason.TurnEnd);
}

/// <summary>A threat effect (a taunt) raised <see cref="Target"/>'s Threat score.</summary>
public sealed record ThreatAdded(Unit Target, ProcDef Proc, double Amount) : Outcome;

/// <summary>A cast cancelled by an interrupt or a stun: the spell fizzles.</summary>
public sealed record Interrupted(Unit Unit, Cast Cast) : Outcome;

/// <summary>Damage a buff dealt as it ended (delayed damage).</summary>
public sealed record DelayedDamaged(Unit Target, Buff Buff, DamageTaken Taken, int HealthBefore) : Outcome;

/// <summary>A unit had nothing it could do and let its turn pass.</summary>
public sealed record Waited(Unit Unit) : Outcome;

/// <summary>A unit lost its turn to Sleep or Fear.</summary>
public sealed record TurnLost(Unit Unit, string Reason) : Outcome;

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
