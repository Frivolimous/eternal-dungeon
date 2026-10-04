using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

/// <summary>What a unit will do with its turn: an action and its target unit or tile.</summary>
public sealed record Decision(ActionDef Action, Unit? Target, Tile? Tile);

/// <summary>
/// Picks actions and targets from a unit's AI profile (Anchor: Combat › Enemy targeting). Rules are tried in
/// order; the first whose action is usable, whose conditions hold and that has a valid target wins. Enemy
/// targets score w × Threat + (1 − w) × Vulnerability. Heroes use the same rules in the simulator, since there
/// is no player input yet.
/// </summary>
public static class UnitAi
{
    public static Decision? Decide(Battle battle, Unit unit)
    {
        var profile = battle.Data.AiProfiles[unit.Def.Ai];
        foreach (var rule in profile.Rules)
        {
            var action = battle.Data.Actions[rule.Action];
            if (unit.CantUse(action) is not null) continue;
            if (rule.SelfHealthBelow is double self && unit.Health >= self * unit.MaxHealth) continue;
            if (rule.MissingBuff is string buff && unit.Buffs.Any(b => b.Def.Id == buff)) continue;
            if (rule.NotIntruding && battle.Grid.Intruding(unit)) continue;

            if (Choose(battle, unit, action, rule, profile.ThreatWeight) is { } decision)
                return decision;
        }
        return StepForward(battle, unit);
    }

    static Decision? Choose(Battle battle, Unit unit, ActionDef action, AiRule rule, double w)
    {
        switch (action.Target)
        {
            case ActionTarget.Self:
                return new Decision(action, unit, null);

            case ActionTarget.Tile:
                var tiles = (action.MoveTo == MoveTo.Enemy ? battle.Grid.SneakOptions(unit) : battle.Grid.MoveOptions(unit)).ToList();
                return tiles.Count > 0 ? new Decision(action, null, tiles[0]) : null;

            case ActionTarget.Ally:
                var allies = ValidTargets(battle, unit, action)
                    .Where(a => rule.AllyHealthBelow is not double share || a.Health < share * a.MaxHealth)
                    .OrderBy(a => (double)a.Health / a.MaxHealth)
                    .ToList();
                return allies.Count > 0 ? new Decision(action, allies[0], null) : null;

            default:
                var enemies = ValidTargets(battle, unit, action);
                if (enemies.Count == 0) return null;
                // Confusion (placeholder): any valid target, at random.
                var pick = unit.Has(CcKind.Confusion)
                    ? enemies[battle.Rng.NextInt(enemies.Count)]
                    : PickTarget(enemies, w, battle.Rng);
                return new Decision(action, pick, null);
        }
    }

    public static List<Unit> ValidTargets(Battle battle, Unit unit, ActionDef action) =>
        battle.Units.Where(u => battle.Grid.CantTarget(unit, action, u) is null).ToList();

    /// <summary>
    /// The highest score w × Threat + (1 − w) × Vulnerability. Ties are broken by a pick from the battle's seeded
    /// RNG (never a global one); it's only rolled when there is a tie. Threat is earned in damage and healing, so it's
    /// scaled against the highest Threat among the candidates to put it on the same 0–1 footing as
    /// Vulnerability (placeholder).
    /// </summary>
    public static Unit PickTarget(IReadOnlyList<Unit> candidates, double w, Rng rng)
    {
        const double tolerance = 1e-12;
        var maxThreat = candidates.Max(c => Math.Max(0, c.Threat));
        var scored = candidates
            .Select(c => (Unit: c, Score: w * (maxThreat > 0 ? Math.Max(0, c.Threat) / maxThreat : 0) + (1 - w) * c.Vulnerability))
            .ToList();
        var best = scored.Max(s => s.Score);
        var tied = scored.Where(s => s.Score >= best - tolerance).Select(s => s.Unit).ToList();
        return tied.Count == 1 ? tied[0] : tied[rng.NextInt(tied.Count)];
    }

    /// <summary>Nothing to do from here: a unit that can Move steps toward the front row.</summary>
    static Decision? StepForward(Battle battle, Unit unit)
    {
        var move = unit.Def.Actions.Select(id => battle.Data.Actions[id])
            .FirstOrDefault(a => a.MoveTo == MoveTo.Own && unit.CantUse(a) is null);
        if (move is null || battle.Grid.AnchorOf(unit) is not { } at) return null;
        var forward = battle.Grid.MoveOptions(unit).Where(t => t.Row < at.Row).ToList();
        return forward.Count > 0 ? new Decision(move, null, forward[0]) : null;
    }
}
