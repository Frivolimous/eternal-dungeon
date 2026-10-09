using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

// Committed enemy intents (Anchor: Combat › Enemy targeting; Jeremy, 2026-10-08). Enemies plan their next turn at the
// start of the battle and at the end of each of their turns, and the player sees the plan. A plan changes only when a
// unit does something to it, never because a buff wore off or ordinary damage and healing moved the scores:
//   - its target falls or flees, or something makes the plan impossible (a move, push or collapse, Silence, Root): re-plan;
//   - its target has its Threatening lowered (enters Stealth): re-plan;
//   - another unit has its Threatening raised, or a threat effect (taunt) lands on it: re-pick between it and the
//     current target, weighted by their scores;
//   - Fear is applied: re-plan (losing it doesn't); Confusion is applied: the plan becomes "?".
public sealed partial class Battle
{
    /// <summary>The unit whose turn is in progress. Its own plan is made when the turn ends, so triggers skip it.</summary>
    Unit? turnUnit;

    /// <summary>Turns where an enemy's plan had become impossible without a trigger catching it. Each is a missed
    /// trigger: the turn-start check decides afresh, and tests keep this at 0.</summary>
    public int StaleIntents { get; private set; }

    /// <summary>Units that commit to their next turn ahead of time: enemies (heroes choose on their turn, or by AI on
    /// auto-battle).</summary>
    public static bool HasIntents(Unit unit) => unit.Side == Side.Enemy;

    Intent NewIntent(Unit unit) =>
        unit.Has(CcKind.Confusion) ? new Intent(null, Unknown: true) : new Intent(UnitAi.Decide(this, unit));

    void SetIntent(Unit unit, IntentReason why, ActionResult r)
    {
        unit.Intent = NewIntent(unit);
        r.Add(new IntentSet(unit, unit.Intent, why));
    }

    /// <summary>Every enemy's first plan, made once the fight has started (after fight-start procs).</summary>
    void PlanEnemies(ActionResult r)
    {
        foreach (var unit in Units.Where(u => u.Alive && HasIntents(u)))
            SetIntent(unit, IntentReason.BattleStart, r);
    }

    /// <summary>
    /// What an enemy does with its turn: its plan, unless it's Confused (it acts at random) or the plan has become
    /// impossible without a trigger noticing (then it decides afresh, counted in <see cref="StaleIntents"/>).
    /// </summary>
    public Decision? TakeIntent(Unit unit)
    {
        if (unit.Has(CcKind.Confusion)) return UnitAi.Confused(this, unit);
        if (unit.Intent is { Unknown: false, Decision: { } planned })
        {
            if (IntentProblem(unit) is null) return planned;
            StaleIntents++;
        }
        return UnitAi.Decide(this, unit);
    }

    /// <summary>A unit's turn is over: an enemy plans its next one (shown with this turn's result).</summary>
    public void EndTurn(Unit unit)
    {
        turnUnit = null;
        if (!unit.Alive || !HasIntents(unit) || Winner is not null || Results.Count == 0) return;
        SetIntent(unit, IntentReason.TurnEnd, Results[^1]);
    }

    /// <summary>Why <paramref name="unit"/>'s plan can't be carried out now, or null.</summary>
    string? IntentProblem(Unit unit)
    {
        if (unit.Intent is not { Unknown: false, Decision: { } d }) return null;
        if (unit.CantUse(d.Action) is string why) return why;
        if (d.Tile is { } tile)
            return (unit.Afraid ? Grid.RetreatOptions(unit) : Grid.TileOptions(unit, d.Action)).Contains(tile) ? null : "nowhere_to_move";
        if (d.Action.Target == ActionTarget.Self) return null;
        if (d.Target is not { Alive: true } target) return "target_down";
        return Grid.CantTarget(unit, d.Action, target);
    }

    /// <summary>
    /// Checks every enemy's plan against what just happened in <paramref name="r"/> (the triggers above) and adds an
    /// <see cref="IntentSet"/> for each plan that changes. Plans change in unit order, so tie-break rolls keep a fixed
    /// order.
    /// </summary>
    void ReviewIntents(ActionResult r)
    {
        var planners = Units.Where(u => u.Alive && HasIntents(u) && u.Intent is not null && u != turnUnit).ToList();
        var replan = new Dictionary<Unit, IntentReason>();
        var drawn = new List<Unit>();
        var check = false;

        void Replan(Unit unit, IntentReason why)
        {
            if (planners.Contains(unit)) replan.TryAdd(unit, why);
        }
        IEnumerable<Unit> Aiming(Unit target) => planners.Where(p => p != target && p.Intent!.Decision?.Target == target);

        foreach (var o in r.Outcomes.ToList())
        {
            switch (o)
            {
                case Died d:
                    d.Unit.Intent = null;
                    foreach (var p in Aiming(d.Unit)) Replan(p, IntentReason.TargetFell);
                    break;
                case Fled f:
                    f.Unit.Intent = null;
                    foreach (var p in Aiming(f.Unit)) Replan(p, IntentReason.TargetFled);
                    break;
                case Moved:
                    check = true;
                    break;
                case BuffApplied b:
                    check = true;
                    if (b.Buff.Def.Cc == CcKind.Fear) Replan(b.Target, IntentReason.Feared);
                    if (b.Buff.Def.Cc == CcKind.Confusion) Replan(b.Target, IntentReason.Confused);
                    var change = ThreateningChange(b);
                    if (change < 0)
                        foreach (var p in Aiming(b.Target)) Replan(p, IntentReason.TargetHid);
                    else if (change > 0)
                        drawn.Add(b.Target);
                    break;
                case ThreatAdded t:
                    drawn.Add(t.Target);
                    break;
            }
        }
        if (planners.Count == 0) return;

        foreach (var p in planners)
        {
            IntentReason? why = replan.TryGetValue(p, out var reason) ? reason
                : check && IntentProblem(p) is not null ? IntentReason.Blocked
                : null;
            if (why is { } w)
            {
                SetIntent(p, w, r);
                continue;
            }
            foreach (var unit in drawn.Distinct())
                Draw(p, unit, r);
        }
    }

    /// <summary>How a buff just applied changes its holder's Threatening: its stat entries, unless it was only
    /// refreshed (a non-stacking buff applied again changes nothing).</summary>
    static double ThreateningChange(BuffApplied b) =>
        b.Refreshed && !b.Buff.Def.Stacking ? 0 : b.Buff.Def.Stats.Where(s => s.Stat == "threatening" && s.Tag is null).Sum(s => s.Value);

    /// <summary>
    /// Trigger 2: <paramref name="unit"/> just became more threatening. <paramref name="enemy"/> weighs it only against
    /// its current target, on the usual scores among everyone its planned action can reach, and re-picks between the
    /// two with the same weighted roll as any target pick (decided 2026-10-08): the higher the taunter now scores, the
    /// likelier the switch.
    /// </summary>
    void Draw(Unit enemy, Unit unit, ActionResult r)
    {
        if (enemy.Intent is not { Unknown: false, Decision: { Target: { } current } d } || current == unit || unit.Side == enemy.Side) return;
        if (d.Action.Target != ActionTarget.Enemy) return;
        var candidates = UnitAi.ValidTargets(this, enemy, d.Action);
        if (!candidates.Contains(unit) || !candidates.Contains(current)) return;
        var scores = UnitAi.Scores(candidates, Data.AiProfiles[enemy.Def.Ai].ThreatWeight).ToDictionary(s => s.Unit, s => s.Score);
        var (stay, go) = (UnitAi.Weight(scores[current]), UnitAi.Weight(scores[unit]));
        if (Rng.NextDouble() * (stay + go) >= go) return;
        enemy.Intent = new Intent(d with { Target = unit });
        r.Add(new IntentSet(enemy, enemy.Intent, IntentReason.Drawn));
    }
}
