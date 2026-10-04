namespace EternalDungeon.Core.Battle;

/// <summary>A spell in progress: it takes effect at <see cref="CompletesAt"/> unless interrupted.</summary>
public sealed record Cast(string ActionId, string? TargetId, long StartedAt, long CompletesAt);

public abstract record ClockEvent(long Tick)
{
    /// <summary>Battle time in base-speed turns, as the combat log prints it.</summary>
    public double Time => Tick / (double)TurnClock.TicksPerTurn;
}

/// <summary>A unit reached Act 100: it's that unit's turn.</summary>
public sealed record TurnReady(long Tick, Unit Unit) : ClockEvent(Tick);

/// <summary>A unit's cast timer finished: the spell takes effect now.</summary>
public sealed record CastComplete(long Tick, Unit Unit, Cast Cast) : ClockEvent(Tick);

/// <summary>One turn of the buff clock, which runs at base Speed 100: buffs, Regen and the stagger bar count these.</summary>
public sealed record BuffTick(long Tick, int Turn) : ClockEvent(Tick);

/// <summary>
/// The turn order (Anchor: Combat › Turn order). Time moves in sub-ticks of 1/100 of a base turn. Each sub-tick
/// every living unit gains its Speed in hundredths of Act; at Act 100 it's that unit's turn, and the action's
/// AP cost is subtracted, so Act can stay above 100 (extra turns) or drop below 0 (delays). Nothing happens
/// in real time: <see cref="Next"/> jumps straight to the next event.
/// <para>Events at the same sub-tick come in this order: buff tick, cast completions, then turns. Turn ties go
/// to the higher Act, then the higher Speed, then the unit listed first. A unit that is casting gains Act but
/// takes no turn until its cast completes or is interrupted.</para>
/// </summary>
public sealed class TurnClock(IReadOnlyList<Unit> units)
{
    public const int TicksPerTurn = 100;
    /// <summary>Act 100 in hundredths.</summary>
    public const long TurnThreshold = 100L * TicksPerTurn;

    long nextBuffTick = TicksPerTurn;

    public long Tick { get; private set; }
    public double Time => Tick / (double)TicksPerTurn;

    public ClockEvent Next()
    {
        while (true)
        {
            if (Tick == nextBuffTick)
            {
                nextBuffTick += TicksPerTurn;
                return new BuffTick(Tick, (int)(Tick / TicksPerTurn));
            }
            if (units.FirstOrDefault(u => u.Alive && u.Casting?.CompletesAt == Tick) is { } caster)
            {
                var cast = caster.Casting!;
                caster.Casting = null;
                return new CastComplete(Tick, caster, cast);
            }
            if (ReadyUnit() is { } ready)
                return new TurnReady(Tick, ready);

            Advance(TicksToNextEvent());
        }
    }

    /// <summary>Subtracts an action's AP cost from the unit's meter.</summary>
    public static void Spend(Unit unit, int apCost) => unit.ActTicks -= (long)apCost * TicksPerTurn;

    /// <summary>Starts a cast that completes <paramref name="castTime"/> sub-ticks from now.</summary>
    public void BeginCast(Unit unit, string actionId, string? targetId, int castTime)
    {
        if (castTime <= 0) throw new ArgumentOutOfRangeException(nameof(castTime));
        unit.Casting = new Cast(actionId, targetId, Tick, Tick + castTime);
    }

    /// <summary>Cancels the unit's cast; returns the cast that was interrupted, if any.</summary>
    public static Cast? Interrupt(Unit unit)
    {
        var cast = unit.Casting;
        unit.Casting = null;
        return cast;
    }

    Unit? ReadyUnit()
    {
        Unit? best = null;
        foreach (var u in units)
        {
            if (!u.Alive || u.Casting is not null || u.ActTicks < TurnThreshold) continue;
            if (best is null || u.ActTicks > best.ActTicks || (u.ActTicks == best.ActTicks && u.Speed > best.Speed))
                best = u;
        }
        return best;
    }

    long TicksToNextEvent()
    {
        var dt = nextBuffTick - Tick;
        foreach (var u in units)
        {
            if (!u.Alive) continue;
            if (u.Casting is { } cast)
                dt = Math.Min(dt, cast.CompletesAt - Tick);
            else if (u.Speed > 0)
                dt = Math.Min(dt, CeilDiv(TurnThreshold - u.ActTicks, u.Speed));
        }
        return Math.Max(1, dt);
    }

    void Advance(long dt)
    {
        Tick += dt;
        foreach (var u in units)
            if (u.Alive)
                u.ActTicks += u.Speed * dt;
    }

    static long CeilDiv(long a, long b) => (a + b - 1) / b;
}
