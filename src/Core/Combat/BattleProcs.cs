using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

/// <summary>One copy of a proc on a unit and where it comes from (the unit itself, or a buff).</summary>
public readonly record struct ProcCopy(ProcDef Def, string Source);

// Procs (Anchor: Combat › Procs). Nothing a proc causes triggers further procs: procs fire only from the
// action and clock events in Battle.cs, never from Process or from another proc.
public sealed partial class Battle
{
    /// <summary>Source key for this-hit stats from before-damage procs; removed once the hit's damage is dealt.</summary>
    public const string ThisHitSource = "this_hit";

    /// <summary>Every proc copy <paramref name="unit"/> has: its own, then those its buffs grant.</summary>
    public IEnumerable<ProcCopy> ProcsOf(Unit unit)
    {
        foreach (var id in unit.Def.Procs ?? [])
            yield return new ProcCopy(Data.Procs[id], "unit");
        foreach (var buff in unit.Buffs)
            foreach (var id in buff.Def.Procs)
                yield return new ProcCopy(Data.Procs[id], buff.SourceKey);
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
    /// when the proc lands on someone else. Above 100% the chance stops at 1 and the excess becomes a scale on
    /// the proc's amounts (lost if it has none).
    /// </summary>
    public static (double Chance, double Scale) CopyChance(ProcDef def, Unit owner, Unit target)
    {
        var rate = owner.Stats.Get("rate", def.Tags);
        var deval = target == owner ? 0 : target.Stats.Get("deval", def.Tags);
        var chance = Resolution.ProcChance(def.Chance, rate, deval);
        return chance > 1 ? (1, def.HasAmounts ? chance : 1) : (chance, 1);
    }

    /// <summary>What a proc reacts to: who owns it, the other unit in the event, the action involved, and how much
    /// damage the hit dealt (for lifesteal).</summary>
    sealed record ProcEvent(Unit Owner, Unit? Other, ActionDef? Action, int HitDamage, Queue<Pending> Queue, ActionResult Result);

    /// <summary>Rolls and applies <paramref name="e"/>.Owner's procs for <paramref name="trigger"/>.</summary>
    void FireProcs(ProcTrigger trigger, ProcEvent e, ProcPhase phase = ProcPhase.AfterHit)
    {
        var owner = e.Owner;
        if (!owner.Alive) return;
        var groups = ProcsOf(owner)
            .Where(c => c.Def.Trigger == trigger && c.Def.Phase == phase)
            .Where(c => c.Def.TriggerTags.Count == 0 || (e.Action?.Tags.Any(c.Def.TriggerTags.Contains) ?? false))
            .Where(c => c.Def.OwnerHealthBelow is not double share || owner.Health < share * owner.MaxHealth)
            .GroupBy(c => c.Def.Id)
            .ToList();

        foreach (var group in groups)
        {
            var def = group.First().Def;
            var target = def.Target == ProcTarget.Self ? owner : e.Other;
            if (target is null || !target.Alive) continue;
            var copies = group.Select(_ => CopyChance(def, owner, target)).ToList();
            switch (def.Duplicates)
            {
                case Duplicates.Merge:
                    var (chance, scale) = MergeCopies(copies);
                    RollProc(def, owner, target, chance, scale, e);
                    break;
                case Duplicates.Separate:
                    foreach (var (c, s) in copies)
                        RollProc(def, owner, target, c, s, e);
                    break;
                case Duplicates.Unique:
                    var best = copies.MaxBy(c => c.Chance * c.Scale);
                    RollProc(def, owner, target, best.Chance, best.Scale, e);
                    break;
            }
        }
    }

    void RollProc(ProcDef def, Unit owner, Unit target, double chance, double scale, ProcEvent e)
    {
        var roll = Rng.Roll(chance);
        e.Result.Add(new ProcRolled(owner, def, target, chance, roll, scale));
        if (roll.Success)
            ApplyProc(def, owner, target, scale, e);
    }

    /// <summary>
    /// The building blocks, amounts scaled by <paramref name="scale"/>. Damage, heals and Shield land at once;
    /// an effect joins the action's queue (so a buff applies after every effect, like any other).
    /// </summary>
    void ApplyProc(ProcDef def, Unit owner, Unit target, double scale, ProcEvent e)
    {
        var r = e.Result;
        foreach (var s in def.HitStats)
            owner.Stats.Add(ThisHitSource, s.Stat, s.Value * scale, s.Tag);

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
        if (def.Effect is string effect)
            e.Queue.Enqueue(new Pending(Data.Effects[effect], owner, $"proc:{def.Id}", target, def.Tags));
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
