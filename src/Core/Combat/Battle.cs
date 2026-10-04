using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

/// <summary>
/// One battle: its units, turn clock and seeded RNG, and the rules that change state. Decisions (which
/// action, which target) come from outside; this class only applies them. Every change is reported as an
/// <see cref="ActionResult"/>, appended to <see cref="Results"/> in order.
/// </summary>
public sealed class Battle
{
    /// <summary>Guard against triggers that keep triggering each other.</summary>
    public const int MaxQueuedEffects = 100;

    public GameData Data { get; }
    public IReadOnlyList<Unit> Units { get; }
    public TurnClock Clock { get; }
    public BattleGrid Grid { get; }
    public Rng Rng { get; }
    public List<ActionResult> Results { get; } = [];

    /// <param name="grid">Where everyone stands; by default each side fills its own area front row first.</param>
    public Battle(GameData data, IReadOnlyList<Unit> units, ulong seed, BattleGrid? grid = null)
    {
        if (units.Select(u => u.Id).Distinct().Count() != units.Count)
            throw new ArgumentException("Unit ids must be unique in a battle");
        Data = data;
        Units = units;
        Clock = new TurnClock(units);
        Grid = grid ?? BattleGrid.AutoPlace(units);
        if (units.Any(u => Grid.AnchorOf(u) is null))
            throw new ArgumentException("Every unit needs a place on the grid");
        Rng = new Rng(seed);
    }

    public Side? Winner => BattleRules.Winner(Units);

    public Unit Unit(string id) => Units.First(u => u.Id == id);

    // ---- Clock events ----

    /// <summary>
    /// The start of <paramref name="unit"/>'s turn: buffs that last until its next turn end, then its
    /// turn-start triggers fire.
    /// </summary>
    public ActionResult StartTurn(Unit unit)
    {
        var r = new ActionResult(Clock.Tick, unit, null, null);
        foreach (var buff in unit.Buffs.Where(b => b.Def.Duration == DurationKind.UntilNextTurn).ToList())
            Expire(unit, buff, r);
        var queue = new Queue<Pending>();
        QueueTriggers(unit, TriggerOn.TurnStart, other: null, queue, r);
        Process(queue, r);
        return Record(r);
    }

    /// <summary>Stagger drained from every bar each buff-clock turn (placeholder).</summary>
    public const int StaggerDrain = 10;

    /// <summary>
    /// <paramref name="unit"/> loses its turn to Sleep or Fear: it spends a full turn's AP and does nothing.
    /// </summary>
    public ActionResult SkipTurn(Unit unit)
    {
        var r = new ActionResult(Clock.Tick, unit, null, null);
        TurnClock.Spend(unit, 100);
        r.Add(new TurnLost(unit, unit.Has(CcKind.Sleep) ? "asleep" : "afraid"));
        return Record(r);
    }

    /// <summary><paramref name="unit"/> has nothing it can do: it lets the turn pass (100 AP).</summary>
    public ActionResult Wait(Unit unit)
    {
        var r = new ActionResult(Clock.Tick, unit, null, null);
        TurnClock.Spend(unit, 100);
        r.Add(new Waited(unit));
        return Record(r);
    }

    /// <summary>One buff-clock turn: stagger bars drain, Health and Mana regenerate, periodic damage and healing land, then buffs count
    /// down and expire.</summary>
    public ActionResult BuffTick()
    {
        var r = new ActionResult(Clock.Tick, Units[0], null, null);
        foreach (var unit in Units.Where(u => u.Alive))
        {
            unit.DrainStagger(StaggerDrain);
            var regen = (int)Math.Round(unit.Stats.Get("h_regen"), MidpointRounding.AwayFromZero);
            if (regen > 0 && unit.Health < unit.MaxHealth)
            {
                var before = unit.Health;
                r.Add(new Healed(unit, "Regen", unit.Heal(regen), before));
            }
            unit.RestoreMana((int)Math.Round(unit.Stats.Get("m_regen"), MidpointRounding.AwayFromZero));
            foreach (var buff in unit.Buffs.ToList())
            {
                if (buff.Def.PeriodicDamage > 0 && unit.Alive)
                {
                    var before = unit.Health;
                    var taken = unit.TakeDamage(buff.Def.PeriodicDamage * buff.Stacks);
                    AddThreat(buff.CasterId, taken.Absorbed + taken.ToHealth);
                    r.Add(new PeriodicDamaged(unit, buff, taken, before));
                    if (taken.Killed) r.Add(new Died(unit));
                }
                if (buff.Def.PeriodicHeal > 0 && unit.Alive)
                {
                    var before = unit.Health;
                    r.Add(new Healed(unit, buff.Def.Name, unit.Heal(buff.Def.PeriodicHeal * buff.Stacks), before));
                }
                if (buff.Def.Duration == DurationKind.Turns && --buff.Remaining <= 0)
                    Expire(unit, buff, r);
            }
        }
        CollapseAreas(r);
        return Record(r);
    }

    // ---- Actions ----

    /// <summary>
    /// <paramref name="actor"/> uses <paramref name="action"/>: pays Mana and AP, then either starts casting
    /// (the effect comes later, from <see cref="CompleteCast"/>) or resolves at once.
    /// </summary>
    public ActionResult Act(Unit actor, ActionDef action, Unit? target)
    {
        if (!actor.Alive) throw new InvalidOperationException($"{actor.Name} is dead");
        if (actor.CantUse(action) is string why)
            throw new InvalidOperationException($"{actor.Name} can't use {action.Name}: {why}");
        if (action.Target == ActionTarget.Tile)
            throw new InvalidOperationException($"{action.Name} targets a tile: use ActAt");
        var aimed = action.Target == ActionTarget.Self ? actor : target
            ?? throw new InvalidOperationException($"{action.Name} needs a target");
        if (Grid.CantTarget(actor, action, aimed) is string bad)
            throw new InvalidOperationException($"{actor.Name} can't aim {action.Name} at {aimed.Name}: {bad}");
        target = aimed;
        actor.SpendMana(action.ManaCost);
        TurnClock.Spend(actor, action.ApCost);

        if (action.CastTime > 0)
        {
            Clock.BeginCast(actor, action.Id, target?.Id, action.CastTime);
            var r = new ActionResult(Clock.Tick, actor, action, target);
            r.Add(new CastStarted(action.CastTime, actor.Casting!.CompletesAt));
            return Record(r);
        }
        return Record(Resolve(actor, action, target));
    }

    /// <summary>A tile-targeted action: Move steps to an empty tile next to the unit in the area it stands in;
    /// Sneak goes to any empty tile in the other side's area. Then the action's effects apply.</summary>
    public ActionResult ActAt(Unit actor, ActionDef action, Tile tile)
    {
        if (!actor.Alive) throw new InvalidOperationException($"{actor.Name} is dead");
        if (action.Target != ActionTarget.Tile)
            throw new InvalidOperationException($"{action.Name} doesn't target a tile");
        if (actor.CantUse(action) is string why)
            throw new InvalidOperationException($"{actor.Name} can't use {action.Name}: {why}");
        var options = action.MoveTo == MoveTo.Enemy ? Grid.SneakOptions(actor) : Grid.MoveOptions(actor);
        if (!options.Contains(tile))
            throw new InvalidOperationException($"{actor.Name} can't {action.Name} to {tile}");

        actor.SpendMana(action.ManaCost);
        TurnClock.Spend(actor, action.ApCost);
        var r = new ActionResult(Clock.Tick, actor, action, null);
        var from = Grid.AnchorOf(actor)!.Value;
        Grid.MoveTo(actor, tile);
        r.Add(new Moved(actor, from, tile, action.Name));

        var queue = new Queue<Pending>();
        foreach (var e in action.Effects)
            queue.Enqueue(new Pending(Data.Effects[e.Effect], actor, action.Id, actor, action.Tags));
        Process(queue, r);
        CollapseAreas(r);
        return Record(r);
    }

    public ActionResult CompleteCast(CastComplete done)
    {
        var action = Data.Actions[done.Cast.ActionId];
        var target = done.Cast.TargetId is string id ? Unit(id) : null;
        if (target is { Alive: false })
        {
            var r = new ActionResult(Clock.Tick, done.Unit, action, target);
            r.Add(new Fizzled($"{target.Name} has fallen"));
            return Record(r);
        }
        return Record(Resolve(done.Unit, action, target));
    }

    /// <summary>
    /// The effect queue: the action's own hit and damage, then each queued effect in order (instant ones apply
    /// as they come; triggers they set off join the queue), then every buff created along the way.
    /// </summary>
    ActionResult Resolve(Unit actor, ActionDef action, Unit? target)
    {
        var r = new ActionResult(Clock.Tick, actor, action, target);
        var queue = new Queue<Pending>();
        var landed = true;

        if (action.Target == ActionTarget.Enemy && target is not null)
        {
            var chance = Resolution.SuccessChance(actor, action, target);
            var roll = Rng.Roll(chance);
            r.Add(new Attempt(target, chance, roll));
            landed = roll.Success;
            if (landed && action.DealsDamage)
            {
                var breakdown = Resolution.Damage(actor, action, target);
                var before = target.Health;
                var taken = target.TakeDamage(breakdown.Final);
                actor.ThreatEarned += taken.Absorbed + taken.ToHealth;
                r.Add(new Damaged(target, breakdown, taken, before));
                if (taken.Killed) r.Add(new Died(target));
            }
            if (landed)
            {
                // Any hit wakes a sleeper.
                foreach (var sleep in target.Buffs.Where(b => b.Def.Cc == CcKind.Sleep).ToList())
                    Expire(target, sleep, r, natural: false);
            }
            if (landed && target.Alive)
                QueueTriggers(target, TriggerOn.HitTaken, other: actor, queue, r);
        }

        if (landed)
            foreach (var e in action.Effects)
            {
                var on = e.On == EffectAim.Self ? actor : target ?? actor;
                queue.Enqueue(new Pending(Data.Effects[e.Effect], actor, action.Id, on, action.Tags));
            }

        QueueTriggers(actor, TriggerOn.ActionComplete, other: target, queue, r);
        Process(queue, r);
        CollapseAreas(r);
        return r;
    }

    /// <summary>An effect waiting in the queue: what, from whom (caster + action = the buff source), onto whom.</summary>
    sealed record Pending(EffectDef Def, Unit Caster, string ActionId, Unit Target, IReadOnlyList<string> Tags);

    void Process(Queue<Pending> queue, ActionResult r)
    {
        var buffs = new List<Pending>();
        var processed = 0;
        while (queue.TryDequeue(out var p))
        {
            if (++processed > MaxQueuedEffects)
                throw new InvalidOperationException($"More than {MaxQueuedEffects} effects queued: a trigger loop?");
            if (!p.Target.Alive) continue;
            if (p.Def.IsBuff)
                buffs.Add(p);
            else
                ApplyInstant(p, r);
        }
        foreach (var p in buffs)
            if (p.Target.Alive)
                ApplyBuff(p, r);
    }

    /// <summary>Placeholder: an instant heal scales with the caster's Power for the action's tags, like damage.</summary>
    void ApplyInstant(Pending p, ActionResult r)
    {
        if (p.Def.Heal > 0)
        {
            var power = p.Caster.Stats.Get("power", p.Tags);
            var amount = (int)Math.Round(p.Def.Heal * (1 + power / 100), MidpointRounding.AwayFromZero);
            var before = p.Target.Health;
            var healed = p.Target.Heal(Math.Max(0, amount));
            p.Caster.ThreatEarned += healed;
            r.Add(new Healed(p.Target, p.Def.Name, healed, before));
        }
        if (p.Def.ShieldMaxHealth > 0)
        {
            var amount = ShieldAmount(p.Def, p.Target);
            p.Target.AddShield(amount);
            r.Add(new Shielded(p.Target, p.Def.Name, amount));
        }
        if (p.Def.Stagger > 0)
        {
            var broke = p.Target.TakeStagger(p.Def.Stagger);
            r.Add(new Staggered(p.Target, p.Def.Stagger, p.Target.Stagger, broke));
            if (broke) Interrupt(p.Target, r);
        }
        if (p.Def.Displace != Displace.None && Grid.AnchorOf(p.Target) is { } from && Grid.Shove(p.Target, p.Def.Displace) is { } to)
            r.Add(new Moved(p.Target, from, to, p.Def.Name));
    }

    void AddThreat(string unitId, double amount)
    {
        if (Units.FirstOrDefault(u => u.Id == unitId) is { } u) u.ThreatEarned += amount;
    }

    /// <summary>Collapses any area whose front row emptied (deaths, moves, pushes).</summary>
    void CollapseAreas(ActionResult r)
    {
        foreach (var area in new[] { Side.Party, Side.Enemy })
        {
            var before = Units.Where(u => u.Alive).ToDictionary(u => u, u => Grid.AnchorOf(u));
            foreach (var u in Grid.Collapse(area))
                r.Add(new Moved(u, before[u]!.Value, Grid.AnchorOf(u)!.Value, "collapse"));
        }
    }

    void Interrupt(Unit unit, ActionResult r)
    {
        if (TurnClock.Interrupt(unit) is { } cast)
            r.Add(new Interrupted(unit, cast));
    }

    void ApplyBuff(Pending p, ActionResult r)
    {
        var unit = p.Target;
        var key = Buff.Key(p.Def.Id, p.ActionId, p.Caster.Id);
        var buff = unit.Buffs.FirstOrDefault(b => b.SourceKey == key);
        var refreshed = buff is not null;

        if (buff is null)
        {
            buff = new Buff(p.Def, p.Caster.Id, p.ActionId);
            unit.Buffs.Add(buff);
            AddStacks(unit, buff, 1);
        }
        else
        {
            // Same source: a stacking buff gains a stack (up to its limit); either way the timer resets.
            if (p.Def.Stacking && buff.Stacks < p.Def.MaxStacks)
            {
                buff.Stacks++;
                AddStacks(unit, buff, 1);
            }
            buff.Remaining = p.Def.Turns;
            unit.RemoveShield(buff.ShieldGranted);
            buff.ShieldGranted = 0;
        }

        if (p.Def.ShieldMaxHealth > 0)
        {
            buff.ShieldGranted = ShieldAmount(p.Def, unit);
            unit.AddShield(buff.ShieldGranted);
        }
        r.Add(new BuffApplied(unit, buff, refreshed, buff.Stacks, buff.Remaining, buff.ShieldGranted));
        if (p.Def.Cc == CcKind.Stun) Interrupt(unit, r);
    }

    static void AddStacks(Unit unit, Buff buff, int stacks)
    {
        for (var i = 0; i < stacks; i++)
            foreach (var s in buff.Def.Stats)
                unit.Stats.Add(buff.SourceKey, s.Stat, s.Value, s.Tag);
    }

    /// <summary>Ends a buff. When it runs its course (<paramref name="natural"/>), its delayed damage lands.</summary>
    static void Expire(Unit unit, Buff buff, ActionResult r, bool natural = true)
    {
        unit.Stats.RemoveSource(buff.SourceKey);
        unit.RemoveShield(buff.ShieldGranted);
        unit.Buffs.Remove(buff);
        r.Add(new BuffExpired(unit, buff));
        if (natural && buff.Def.DelayedDamage > 0 && unit.Alive)
        {
            var before = unit.Health;
            var taken = unit.TakeDamage(buff.Def.DelayedDamage * buff.Stacks);
            r.Add(new DelayedDamaged(unit, buff, taken, before));
            if (taken.Killed) r.Add(new Died(unit));
        }
    }

    static int ShieldAmount(EffectDef def, Unit unit) =>
        (int)Math.Round(def.ShieldMaxHealth * unit.MaxHealth, MidpointRounding.AwayFromZero);

    /// <summary>Queues the effects of <paramref name="owner"/>'s triggers for <paramref name="on"/> whose state
    /// check passes.</summary>
    void QueueTriggers(Unit owner, TriggerOn on, Unit? other, Queue<Pending> queue, ActionResult r)
    {
        foreach (var buff in owner.Buffs.ToList())
            foreach (var t in buff.Def.Triggers.Where(t => t.On == on))
            {
                if (t.HealthBelow is double share && owner.Health >= share * owner.MaxHealth) continue;
                var target = t.Target == TriggerTarget.Self ? owner : other;
                if (target is null) continue;
                r.Add(new Triggered(owner, buff, t));
                queue.Enqueue(new Pending(Data.Effects[t.Effect], owner, $"trigger:{buff.Def.Id}", target, []));
            }
    }

    ActionResult Record(ActionResult r)
    {
        Results.Add(r);
        return r;
    }
}
