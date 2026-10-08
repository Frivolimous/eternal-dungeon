using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

/// <summary>What a unit will do with its turn: an action and its target unit or tile.</summary>
public sealed record Decision(ActionDef Action, Unit? Target, Tile? Tile);

/// <summary>
/// Picks actions and targets from a unit's AI profile (Anchor: Combat › Enemy targeting). Rules are tried in
/// order; the first whose action is usable, whose conditions hold and that has a valid target wins. Enemy
/// targets score w × Threat + (1 − w) × Vulnerability. Heroes use the same rules in the simulator, since there
/// is no player input yet. With no rule usable, a unit uses its default actions: it Attacks if it has a valid
/// target, otherwise it Moves toward the front if it can, otherwise it Defends. A feared unit only Moves away from the front, or Defends.
/// </summary>
public static class UnitAi
{
    public static Decision? Decide(Battle battle, Unit unit)
    {
        if (unit.Afraid)
            return Default(battle, unit, DefaultRole.Move, battle.Grid.RetreatOptions(unit)) ?? Default(battle, unit, DefaultRole.Defend);

        var profile = battle.Data.AiProfiles[unit.Def.Ai];
        foreach (var rule in profile.Rules)
        {
            var action = battle.Data.Actions[rule.Action];
            if (unit.CantUse(action) is not null) continue;
            if (rule.SelfHealthBelow is double self && unit.Health >= self * unit.MaxHealth) continue;
            if (rule.MissingBuff is string buff && unit.Buffs.Any(b => b.Def.Id == buff)) continue;
            if (rule.NotIntruding && battle.Grid.Intruding(unit)) continue;
            if (rule.NotTwiceInARow && unit.LastActionId == rule.Action) continue;

            if (Choose(battle, unit, action, rule, profile.ThreatWeight) is { } decision)
                return decision;
        }
        if (DefaultAttack(battle, unit, profile.ThreatWeight) is { } attack) return attack;
        var forward = battle.Grid.ForwardOptions(unit);
        return Default(battle, unit, DefaultRole.Move, forward)
            ?? Default(battle, unit, DefaultRole.Move, SidestepOptions(battle, unit))
            ?? Default(battle, unit, DefaultRole.Defend);
    }

    /// <summary>
    /// Neighbouring tiles in the unit's row from which one of its melee actions would have a valid target: melee only
    /// reaches straight ahead or diagonally, so a unit with nobody in reach steps sideways toward someone. Each tile is
    /// tried by moving the unit there and back (no rolls, nothing else changes).
    /// </summary>
    static List<Tile> SidestepOptions(Battle battle, Unit unit)
    {
        var grid = battle.Grid;
        if (grid.AnchorOf(unit) is not { } from) return [];
        var melee = battle.Data.ActionsOf(unit.Def).Select(id => battle.Data.Actions[id])
            .Where(a => a.Target == ActionTarget.Enemy && a.Range is ActionRange.Melee or ActionRange.Reach && unit.CantUse(a) is null)
            .ToList();
        var tiles = new List<Tile>();
        foreach (var tile in grid.MoveOptions(unit).Where(t => grid.Depth(t) == grid.Depth(from)))
        {
            grid.MoveTo(unit, tile);
            if (melee.Any(a => ValidTargets(battle, unit, a).Count > 0)) tiles.Add(tile);
            grid.MoveTo(unit, from);
        }
        return tiles;
    }

    /// <summary>The default Attack at the best-scoring valid target, if the unit can use it and has one.</summary>
    static Decision? DefaultAttack(Battle battle, Unit unit, double w)
    {
        if (battle.Data.DefaultFor(unit.Def, DefaultRole.Attack) is not { } id) return null;
        var action = battle.Data.Actions[id];
        if (unit.CantUse(action) is not null) return null;
        return Choose(battle, unit, action, new AiRule(action.Id), w);
    }

    static Decision? Choose(Battle battle, Unit unit, ActionDef action, AiRule rule, double w)
    {
        switch (action.Target)
        {
            case ActionTarget.Self:
                return new Decision(action, unit, null);

            case ActionTarget.Tile:
                var tiles = battle.Grid.TileOptions(unit, action)
                    .Where(t => !rule.ToEnemyArea || battle.Grid.InEnemyArea(unit, t))
                    .ToList();
                return tiles.Count > 0 ? new Decision(action, null, tiles[0]) : null;

            case ActionTarget.Ally:
                var allies = ValidTargets(battle, unit, action)
                    .Where(a => rule.AllyHealthBelow is not double share || a.Health < share * a.MaxHealth)
                    .OrderBy(a => (double)a.Health / a.MaxHealth)
                    .ToList();
                return allies.Count > 0 ? new Decision(action, allies[0], null) : null;

            default:
                var enemies = ValidTargets(battle, unit, action)
                    .Where(e => rule.TargetMissingBuff is not string missing || e.Buffs.All(b => b.Def.Id != missing))
                    .Where(e => !rule.TargetCasting || e.Casting is not null)
                    .ToList();
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

    /// <summary>One of the unit's default actions (defaults.json, or its own replacement), if it can use it: a Move to
    /// the first of <paramref name="tiles"/>, or Defend.</summary>
    static Decision? Default(Battle battle, Unit unit, DefaultRole role, IEnumerable<Tile>? tiles = null)
    {
        if (battle.Data.DefaultFor(unit.Def, role) is not { } id) return null;
        var action = battle.Data.Actions[id];
        if (unit.CantUse(action) is not null) return null;
        if (action.Target != ActionTarget.Tile) return new Decision(action, unit, null);
        var tile = (tiles ?? []).Cast<Tile?>().FirstOrDefault();
        return tile is { } to ? new Decision(action, null, to) : null;
    }
}
