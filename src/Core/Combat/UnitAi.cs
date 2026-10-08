using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

/// <summary>What a unit will do with its turn: an action and its target unit or tile.</summary>
public sealed record Decision(ActionDef Action, Unit? Target, Tile? Tile);

/// <summary>An enemy's committed next turn, shown on the turn order (Anchor: Combat › Enemy targeting). A null
/// <see cref="Decision"/> means it will wait. <see cref="Unknown"/>: it's Confused and will act at random ("?").</summary>
public sealed record Intent(Decision? Decision, bool Unknown = false);

/// <summary>Why an enemy made or changed its plan. The first two are routine; the rest are triggers the player
/// caused (and the intent flashes).</summary>
public enum IntentReason { BattleStart, TurnEnd, TargetFell, TargetHid, Drawn, Blocked, Feared, Confused }

/// <summary>
/// Picks actions and targets from a unit's AI profile (Anchor: Combat › Enemy targeting). Rules are tried in
/// order; the first whose action is usable, whose conditions hold and that has a valid target wins. Enemy
/// targets score w × scaled threat + (1 − w) × Vulnerability. Heroes use the same rules in the simulator, since there
/// is no player input yet. With no rule usable, a unit uses its default actions: it Attacks if it has a valid
/// target, otherwise it Moves toward the front if it can, otherwise it Defends. A feared unit only Moves away from the front, or Defends.
/// </summary>
public static class UnitAi
{
    public static Decision? Decide(Battle battle, Unit unit)
    {
        if (unit.Has(CcKind.Confusion))
            return Confused(battle, unit);
        if (unit.Afraid)
            return Default(battle, unit, DefaultRole.Move, battle.Grid.RetreatOptions(unit)) ?? Default(battle, unit, DefaultRole.Defend);

        var profile = battle.Data.AiProfiles[unit.Def.Ai];
        foreach (var rule in profile.Rules)
        {
            var action = battle.Data.Actions[rule.Action];
            if (unit.CantUse(action) is not null) continue;
            if (rule.SelfHealthBelow is double self && unit.Health >= self * unit.MaxHealth) continue;
            if (rule.MissingBuff is string buff && unit.Buffs.Any(b => b.Def.Id == buff)) continue;
            // On a self action, "ally below" means some other ally is (a taunt to protect them).
            if (action.Target == ActionTarget.Self && rule.AllyHealthBelow is double share
                && !battle.Units.Any(a => a != unit && a.Alive && a.Side == unit.Side && a.Health < share * a.MaxHealth)) continue;
            if (rule.NotIntruding && battle.Grid.Intruding(unit)) continue;
            if (rule.NotTwiceInARow && unit.LastActionId == rule.Action) continue;

            if (Choose(battle, unit, action, rule, profile.ThreatWeight) is { } decision)
                return decision;
        }
        if (DefaultAttack(battle, unit, profile.ThreatWeight) is { } attack) return attack;
        // Step forward, preferring a tile from which its melee reaches someone; else sidestep toward a target.
        var forward = battle.Grid.ForwardOptions(unit).ToList();
        return Default(battle, unit, DefaultRole.Move, [.. Reaching(battle, unit, forward), .. forward])
            ?? Default(battle, unit, DefaultRole.Move, Reaching(battle, unit, SameRow(battle, unit)))
            ?? Default(battle, unit, DefaultRole.Defend);
    }

    static IEnumerable<Tile> SameRow(Battle battle, Unit unit) =>
        battle.Grid.AnchorOf(unit) is { } from ? battle.Grid.MoveOptions(unit).Where(t => battle.Grid.Depth(t) == battle.Grid.Depth(from)) : [];

    /// <summary>
    /// The <paramref name="tiles"/> from which one of the unit's melee actions would have a valid target (melee only
    /// reaches straight ahead or diagonally). Each tile is tried by moving the unit there and back (no rolls, nothing
    /// else changes).
    /// </summary>
    static List<Tile> Reaching(Battle battle, Unit unit, IEnumerable<Tile> tiles)
    {
        var grid = battle.Grid;
        if (grid.AnchorOf(unit) is not { } from) return [];
        var melee = battle.Data.ActionsOf(unit.Def).Select(id => battle.Data.Actions[id])
            .Where(a => a.Target == ActionTarget.Enemy && a.Range is ActionRange.Melee or ActionRange.Reach && unit.CantUse(a) is null)
            .ToList();
        var reaching = new List<Tile>();
        foreach (var tile in tiles.ToList())
        {
            grid.MoveTo(unit, tile);
            if (melee.Any(a => ValidTargets(battle, unit, a).Count > 0)) reaching.Add(tile);
            grid.MoveTo(unit, from);
        }
        return reaching;
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
                return new Decision(action, PickTarget(enemies, w, battle.Rng), null);
        }
    }

    /// <summary>
    /// A Confused unit's turn (Jeremy, 2026-10-08): no choice, for heroes too. A random usable action, then a random
    /// target among every unit it could reach, allies included (or a random tile, or itself). Both picks roll the
    /// battle's RNG.
    /// </summary>
    public static Decision? Confused(Battle battle, Unit unit)
    {
        var options = new List<(ActionDef Action, List<Unit> Targets, List<Tile> Tiles)>();
        foreach (var id in battle.Data.ActionsOf(unit.Def))
        {
            var action = battle.Data.Actions[id];
            if (unit.CantUse(action) is not null) continue;
            switch (action.Target)
            {
                case ActionTarget.Self:
                    options.Add((action, [unit], []));
                    break;
                case ActionTarget.Tile:
                    var tiles = unit.Afraid ? battle.Grid.RetreatOptions(unit).ToList() : battle.Grid.TileOptions(unit, action).ToList();
                    if (tiles.Count > 0) options.Add((action, [], tiles));
                    break;
                default:
                    var anyone = battle.Units.Where(u => u != unit && battle.Grid.CantReach(unit, action, u) is null).ToList();
                    if (anyone.Count > 0) options.Add((action, anyone, []));
                    break;
            }
        }
        if (options.Count == 0) return null;
        var (pick, targets, tileOptions) = options[battle.Rng.NextInt(options.Count)];
        return tileOptions.Count > 0
            ? new Decision(pick, null, tileOptions[battle.Rng.NextInt(tileOptions.Count)])
            : new Decision(pick, targets[battle.Rng.NextInt(targets.Count)], null);
    }

    public static List<Unit> ValidTargets(Battle battle, Unit unit, ActionDef action) =>
        battle.Units.Where(u => battle.Grid.CantTarget(unit, action, u) is null).ToList();

    internal const double Tolerance = 1e-12;

    /// <summary>
    /// Each candidate's score, w × scaled threat + (1 − w) × Vulnerability (Anchor: Combat › Enemy targeting).
    /// Effective threat (Threat score × Threatening) is divided by the highest among the candidates, so it's 0–1 like
    /// Vulnerability and doesn't change with party size. When the highest is 0 (no threat yet, or everyone in
    /// Stealth), scaled threat is 0 for all.
    /// </summary>
    public static List<(Unit Unit, double Score)> Scores(IReadOnlyList<Unit> candidates, double w)
    {
        var max = candidates.Max(c => c.EffectiveThreat);
        return [.. candidates.Select(c => (c, w * (max > 0 ? c.EffectiveThreat / max : 0) + (1 - w) * c.Vulnerability))];
    }

    /// <summary>The highest <see cref="Scores"/>. Ties are broken by a pick from the battle's seeded RNG (never a
    /// global one); it's only rolled when there is a tie.</summary>
    public static Unit PickTarget(IReadOnlyList<Unit> candidates, double w, Rng rng)
    {
        var scored = Scores(candidates, w);
        var best = scored.Max(s => s.Score);
        var tied = scored.Where(s => s.Score >= best - Tolerance).Select(s => s.Unit).ToList();
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
