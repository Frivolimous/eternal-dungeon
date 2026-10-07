using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

/// <summary>
/// One battle: its units, turn clock and seeded RNG, and the rules that change state. Decisions (which
/// action, which target) come from outside; this class only applies them. Every change is reported as an
/// <see cref="ActionResult"/>, appended to <see cref="Results"/> in order.
/// </summary>
public sealed partial class Battle
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
        FireProcs(ProcTrigger.TurnStart, new ProcEvent(unit, null, null, 0, queue, r));
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
        r.Add(new TurnLost(unit, "asleep"));
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

    /// <summary>One buff-clock turn: stagger bars drain, periodic damage and healing land, then buffs count down and
    /// expire. There is no passive Health or Mana regeneration.</summary>
    public ActionResult BuffTick()
    {
        var r = new ActionResult(Clock.Tick, Units[0], null, null);
        foreach (var unit in Units.Where(u => u.Alive))
        {
            unit.DrainStagger(StaggerDrain);
            foreach (var buff in unit.Buffs.ToList())
            {
                if (buff.Def.PeriodicDamage > 0 && unit.Alive)
                {
                    var before = unit.Health;
                    var taken = unit.TakeDamage(DotTick(buff));
                    AddThreat(buff.CasterId, taken.Absorbed + taken.ToHealth);
                    r.Add(new PeriodicDamaged(unit, buff, taken, before));
                    if (taken.Killed) AddDeath(unit, r);
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
        actor.LastActionId = action.Id;

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
        if (actor.Afraid)
            options = Grid.RetreatOptions(actor);
        if (!options.Contains(tile))
            throw new InvalidOperationException($"{actor.Name} can't {action.Name} to {tile}");

        actor.SpendMana(action.ManaCost);
        TurnClock.Spend(actor, action.ApCost);
        actor.LastActionId = action.Id;
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
            r.Add(new Fizzled(FizzleReason.TargetFell, done.Unit, action, target));
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
            if (!landed)
            {
                FireProcs(ProcTrigger.Miss, new ProcEvent(actor, target, action, 0, queue, r));
                FireProcs(ProcTrigger.Avoided, new ProcEvent(target, actor, action, 0, queue, r));
            }
            else
            {
                // Before-damage procs can change this hit; their this-hit stats go once the damage is dealt.
                FireProcs(ProcTrigger.Hit, new ProcEvent(actor, target, action, 0, queue, r), ProcPhase.BeforeDamage);
                var tiers = 0;
                var dealt = 0;
                if (action.DealsDamage && target.Alive)
                {
                    tiers = RollCrit(actor, action, target, r);
                    var breakdown = Resolution.Damage(actor, action, target, tiers);
                    var before = target.Health;
                    var taken = target.TakeDamage(breakdown.Final);
                    dealt = taken.Absorbed + taken.ToHealth;
                    actor.ThreatEarned += dealt;
                    r.Add(new Damaged(target, breakdown, taken, before));
                    if (taken.Killed) AddDeath(target, r);
                }
                actor.Stats.RemoveSource(ThisHitSource);

                // Any hit wakes a sleeper.
                foreach (var sleep in target.Buffs.Where(b => b.Def.Cc == CcKind.Sleep).ToList())
                    Expire(target, sleep, r, natural: false);

                var hit = new ProcEvent(actor, target, action, dealt, queue, r);
                FireProcs(ProcTrigger.Hit, hit);
                if (tiers >= 1) FireProcs(ProcTrigger.Crit, hit);
                if (tiers >= 2) FireProcs(ProcTrigger.Brutal, hit);
                var struck = new ProcEvent(target, actor, action, dealt, queue, r);
                FireProcs(ProcTrigger.Struck, struck);
                if (dealt > 0) FireProcs(ProcTrigger.Damaged, struck);
            }
        }

        if (landed)
            foreach (var e in action.Effects)
            {
                var on = e.On == EffectAim.Self ? actor : target ?? actor;
                queue.Enqueue(new Pending(Data.Effects[e.Effect], actor, action.Id, on, action.Tags));
            }

        FireProcs(ProcTrigger.ActionComplete, new ProcEvent(actor, target, action, 0, queue, r));
        Process(queue, r);
        CollapseAreas(r);
        return r;
    }

    /// <summary>Crit on a successful hit (Anchor: Combat › Formulas): roll at the chance from C.Rate; on a crit,
    /// roll again at the same chance for Brutal. No roll when the chance is 0. Returns the tiers (0–2).</summary>
    int RollCrit(Unit actor, ActionDef action, Unit target, ActionResult r)
    {
        var cRate = Resolution.CRate(actor, action, target);
        var chance = Resolution.CritChance(cRate);
        if (chance <= 0) return 0;
        var crit = Rng.Roll(chance);
        var outcome = new CritRolled(target, cRate, chance, crit, crit.Success ? Rng.Roll(chance) : null);
        r.Add(outcome);
        return outcome.Tiers;
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
            if (p.Target.StaggerBroken)
                r.Add(new StaggerIgnored(p.Target));
            else
            {
                var broke = p.Target.TakeStagger(p.Def.Stagger);
                r.Add(new Staggered(p.Target, p.Def.Stagger, p.Target.Stagger, broke));
                if (broke) Interrupt(p.Target, r);
            }
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
        foreach (var area in Grid.Areas.Select(a => a.Id))
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

        if (p.Def.PeriodicDamage > 0)
        {
            // Damage over time scales with the caster, locked in now (Anchor: Combat › Crowd control).
            var a = p.Caster.Stats;
            buff.DotFactor = (1 + a.Get("power", p.Tags) / 100) * (1 + a.Get("multiplier", p.Tags));
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
    void Expire(Unit unit, Buff buff, ActionResult r, bool natural = true)
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
            if (taken.Killed) AddDeath(unit, r);
        }
    }

    /// <summary>One damage-over-time tick: the base amount × stacks × the caster's locked-in factors.</summary>
    static int DotTick(Buff buff)
    {
        var raw = buff.Def.PeriodicDamage * buff.Stacks * buff.DotFactor;
        return raw <= 0 ? 0 : Math.Max(1, (int)Math.Round(raw, MidpointRounding.AwayFromZero));
    }

    static int ShieldAmount(EffectDef def, Unit unit) =>
        (int)Math.Round(def.ShieldMaxHealth * unit.MaxHealth, MidpointRounding.AwayFromZero);

    /// <summary>Reports a death; a cast the unit was in the middle of fizzles (Anchor: Combat › Turn order).</summary>
    void AddDeath(Unit unit, ActionResult r)
    {
        r.Add(new Died(unit));
        if (unit.CastLostOnDeath is { } cast)
        {
            r.Add(new Fizzled(FizzleReason.CasterFell, unit, Data.Actions[cast.ActionId], null));
            unit.CastLostOnDeath = null;
        }
    }

    ActionResult Record(ActionResult r)
    {
        Results.Add(r);
        return r;
    }
}
