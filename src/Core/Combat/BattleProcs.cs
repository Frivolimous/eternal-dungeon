using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

/// <summary>One copy of a proc on a unit and where it comes from (the unit itself, a buff, or the action being
/// used: <see cref="FromAction"/>).</summary>
public readonly record struct ProcCopy(ProcDef Def, string Source, bool FromAction = false);

// Procs (Anchor: Combat › Procs). Nothing a proc causes triggers further procs: procs fire only from the
// action and clock events in Battle.cs, never from Process or from another proc.
public sealed partial class Battle
{
    /// <summary>The triggers that are the owner's own action: an action's procs fire only on these, for its user.</summary>
    public static readonly ProcTrigger[] OwnActionTriggers =
        [ProcTrigger.Hit, ProcTrigger.Miss, ProcTrigger.Crit, ProcTrigger.Brutal, ProcTrigger.ActionComplete];

    /// <summary>Every proc copy <paramref name="unit"/> has: its own, those its buffs grant, then (when it's using
    /// <paramref name="action"/>) the action's procs.</summary>
    public IEnumerable<ProcCopy> ProcsOf(Unit unit, ActionDef? action = null)
    {
        foreach (var id in unit.Def.Procs ?? [])
            yield return new ProcCopy(Data.Procs[id], "unit");
        foreach (var buff in unit.Buffs)
            foreach (var id in buff.Def.Procs)
                yield return new ProcCopy(Data.Procs[id], buff.SourceKey);
        foreach (var id in action?.Procs ?? [])
            yield return new ProcCopy(Data.Procs[id], action!.Id, FromAction: true);
    }

    /// <summary>
    /// The Merge rule for copies of one proc: one roll at 1 − Π(1 − chance) (at least one copy fires), with the
    /// amount Σ(chance × amount) ÷ that chance, so the expected amount is unchanged. Flaming 100% × 15 +
    /// 100% × 20 = 100% × 35; two 20% copies = 36%.
    /// </summary>
    public static (double Chance, double Amount) MergeCopies(IEnumerable<(double Chance, double Amount)> copies)
    {
        var miss = 1.0;
        var expected = 0.0;
        foreach (var (chance, amount) in copies)
        {
            miss *= 1 - chance;
            expected += chance * amount;
        }
        var merged = 1 - miss;
        return (merged, merged > 0 ? expected / merged : 0);
    }

    /// <summary>
    /// One copy's chance: Base × (1 + owner's Rate) ÷ (1 + target's Deval), all over the proc's tags; Deval only
    /// when the proc lands on someone else and isn't marked ignore_deval. It stops at 100%: excess Rate is lost and
    /// never scales amounts. Amounts grow only when several copies of the same proc merge.
    /// </summary>
    public static double CopyChance(ProcDef def, Unit owner, Unit target)
    {
        var rate = owner.Stats.Get("rate", def.Tags);
        var deval = target == owner || def.IgnoreDeval ? 0 : target.Stats.Get("deval", def.Tags);
        return Math.Min(1, Resolution.ProcChance(def.Chance, rate, deval));
    }

    static readonly string[] Force = ["force"];

    /// <summary>
    /// The share of a stagger <paramref name="target"/> takes: 1 − its Force Deval (untagged Deval counts too),
    /// between 0 and 1. For stagger, Deval is a straight resist, unlike the divisor it is for proc chances (Anchor:
    /// Combat › Crowd control).
    /// </summary>
    public static double StaggerTaken(Unit target) => Math.Clamp(1 - target.Stats.Get("deval", Force), 0, 1);

    /// <summary>What a proc reacts to: who owns it, the other unit in the event, the action involved, and how much
    /// damage the hit dealt (for lifesteal).</summary>
    sealed record ProcEvent(Unit Owner, Unit? Other, ActionDef? Action, int HitDamage, Queue<Pending> Queue, ActionResult Result);

    /// <summary>Rolls and applies <paramref name="e"/>.Owner's procs for <paramref name="trigger"/>.</summary>
    void FireProcs(ProcTrigger trigger, ProcEvent e, ProcPhase phase = ProcPhase.AfterHit)
    {
        var owner = e.Owner;
        if (!owner.Alive) return;
        var action = Array.IndexOf(OwnActionTriggers, trigger) >= 0 ? e.Action : null;
        var groups = ProcsOf(owner, action)
            .Where(c => c.Def.Trigger == trigger && c.Def.Phase == phase)
            .Where(c => c.Def.TriggerTags.Count == 0 || (e.Action?.Tags.Any(c.Def.TriggerTags.Contains) ?? false))
            .Where(c => c.Def.OwnerHealthBelow is not double share || owner.Health < share * owner.MaxHealth)
            .GroupBy(c => c.Def.Id)
            .ToList();

        foreach (var group in groups)
        {
            var def = group.First().Def;
            var fromAction = group.Any(c => c.FromAction);
            var target = def.Target == ProcTarget.Self ? owner : e.Other;
            if (target is null || !target.Alive) continue;
            var copies = group.Select(_ => CopyChance(def, owner, target)).ToList();
            switch (def.Duplicates)
            {
                case Duplicates.Merge:
                    // Every copy of one proc has the same amounts, so each weighs 1 and the merged scale is the
                    // expected number of copies firing ÷ the merged chance.
                    var (chance, scale) = MergeCopies(copies.Select(c => (c, 1.0)));
                    RollProc(def, owner, target, chance, scale, fromAction, e);
                    break;
                case Duplicates.Separate:
                    foreach (var c in copies)
                        RollProc(def, owner, target, c, 1, fromAction, e);
                    break;
                case Duplicates.Unique:
                    RollProc(def, owner, target, copies.Max(), 1, fromAction, e);
                    break;
            }
        }
    }

    void RollProc(ProcDef def, Unit owner, Unit target, double chance, double scale, bool fromAction, ProcEvent e)
    {
        // A certain proc (100%) needs no roll, so it never draws from the battle RNG.
        var roll = chance >= 1 ? new Roll(chance, 0, true) : Rng.Roll(chance);
        e.Result.Add(new ProcRolled(owner, def, target, chance, roll, scale, fromAction));
        if (roll.Success)
            ApplyProc(def, owner, target, scale, fromAction, e);
    }

    /// <summary>
    /// The results, amounts scaled by <paramref name="scale"/>. Damage, heals, Shield, stagger, interrupts and pushes land at
    /// once; a buff joins the queue and applies once the action or event is done. A buff from an action's own
    /// proc has that action as its source (action + caster), any other proc's buff the proc.
    /// </summary>
    void ApplyProc(ProcDef def, Unit owner, Unit target, double scale, bool fromAction, ProcEvent e)
    {
        var r = e.Result;
        if (def.Damage > 0)
        {
            var breakdown = Resolution.ProcDamage(owner, def, target, def.Damage * scale);
            var before = target.Health;
            var taken = target.TakeDamage(breakdown.Final);
            owner.ThreatEarned += taken.Absorbed + taken.ToHealth;
            r.Add(new ProcDamaged(owner, target, def, breakdown, taken, before));
            if (taken.Killed) AddDeath(target, r);
        }
        if (def.Heal > 0)
            HealFromProc(def, owner, target, def.Heal * scale * (1 + owner.Stats.Get("power", def.Tags) / 100), r);
        if (def.Lifesteal > 0)
            HealFromProc(def, owner, target, def.Lifesteal * scale * e.HitDamage, r);
        if (def.Shield > 0 && target.Alive)
        {
            var amount = (int)Math.Round(def.Shield * scale, MidpointRounding.AwayFromZero);
            target.AddShield(amount);
            r.Add(new Shielded(target, def.Name, amount));
        }
        if (def.Stagger > 0 && target.Alive)
        {
            var full = (int)Math.Round(def.Stagger * scale, MidpointRounding.AwayFromZero);
            var amount = (int)Math.Round(full * StaggerTaken(target), MidpointRounding.AwayFromZero);
            target.ActTicks -= amount * TurnClock.TicksPerTurn;
            r.Add(new Staggered(target, amount, full - amount));
        }
        if (def.Interrupt && target.Alive)
            Interrupt(target, r);
        if (def.Displace != Displace.None && target.Alive && Grid.AnchorOf(target) is { } from && Grid.Shove(target, def.Displace) is { } to)
            r.Add(new Moved(target, from, to, def.Name));
        if (def.Buff is string buff)
            e.Queue.Enqueue(new Pending(Data.Buffs[buff], owner, fromAction ? e.Action!.Id : $"proc:{def.Id}", target, def.Tags));
    }

    static void HealFromProc(ProcDef def, Unit owner, Unit target, double raw, ActionResult r)
    {
        var amount = (int)Math.Round(Math.Max(0, raw), MidpointRounding.AwayFromZero);
        if (amount <= 0 || !target.Alive) return;
        var before = target.Health;
        var healed = target.Heal(amount);
        owner.ThreatEarned += healed;
        r.Add(new Healed(target, def.Name, healed, before));
    }

    // ---- Fight start ----

    bool started;

    /// <summary>The fight begins: every unit's fight-start procs fire (once per battle).</summary>
    public ActionResult Start()
    {
        if (started) throw new InvalidOperationException("The battle has already started");
        started = true;
        var r = new ActionResult(Clock.Tick, Units[0], null, null);
        var queue = new Queue<Pending>();
        foreach (var unit in Units)
            FireProcs(ProcTrigger.FightStart, new ProcEvent(unit, null, null, 0, queue, r));
        Process(queue, r);
        return Record(r);
    }
}
