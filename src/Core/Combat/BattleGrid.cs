using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

/// <summary>A tile in one side's area. Row 0 is the front row (facing the other side).</summary>
public readonly record struct Tile(Side Area, int Row, int Col)
{
    public override string ToString() => $"{Area} r{Row}c{Col}";
}

/// <summary>
/// The battlefield (Anchor: Combat › Battlefield): each side has its own area, <see cref="Cols"/> wide and
/// <see cref="Rows"/> deep (3×2 by default), with at most 6 units per side. A unit's footprint follows its
/// size: Small 1 tile, Tall 2 tiles in a column (front and back), Large a 2×2 block. Dead units leave their
/// tiles. When an area's front row has no living unit, the area collapses forward.
/// <para>Range (placeholders from the brief): Melee from the front row to the enemy front row; Reach from the
/// front row to the enemy's first two rows; Any reaches every tile. A unit standing in the other side's area
/// (Sneak) can melee anyone there and be meleed by anyone there (placeholder).</para>
/// </summary>
public sealed class BattleGrid(int cols = 3, int rows = 2)
{
    public const int MaxUnitsPerSide = 6;

    public int Cols { get; } = cols;
    public int Rows { get; } = rows;

    readonly Dictionary<Unit, Tile> anchors = [];

    public IReadOnlyDictionary<Unit, Tile> Anchors => anchors;

    public Tile? AnchorOf(Unit unit) => anchors.TryGetValue(unit, out var t) ? t : null;

    public static IEnumerable<Tile> Footprint(UnitSize size, Tile anchor) => size switch
    {
        UnitSize.Small => [anchor],
        UnitSize.Tall => [anchor, anchor with { Row = anchor.Row + 1 }],
        UnitSize.Large =>
        [
            anchor, anchor with { Col = anchor.Col + 1 },
            anchor with { Row = anchor.Row + 1 }, new Tile(anchor.Area, anchor.Row + 1, anchor.Col + 1),
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };

    public IEnumerable<Tile> Footprint(Unit unit) =>
        anchors.TryGetValue(unit, out var a) ? Footprint(unit.Def.Size, a) : [];

    public bool Inside(Tile t) => t.Row >= 0 && t.Row < Rows && t.Col >= 0 && t.Col < Cols;

    /// <summary>The living unit on <paramref name="tile"/>, if any.</summary>
    public Unit? At(Tile tile) =>
        anchors.Keys.FirstOrDefault(u => u.Alive && Footprint(u).Contains(tile));

    /// <summary>Whether <paramref name="unit"/>'s footprint fits at <paramref name="anchor"/>, ignoring the unit itself.</summary>
    public bool Fits(Unit unit, Tile anchor) =>
        Footprint(unit.Def.Size, anchor).All(t => Inside(t) && (At(t) is null || At(t) == unit));

    public void Place(Unit unit, Tile anchor)
    {
        if (!Fits(unit, anchor))
            throw new InvalidOperationException($"{unit.Name} doesn't fit at {anchor}");
        if (!anchors.ContainsKey(unit) && anchors.Keys.Count(u => u.Side == unit.Side) >= MaxUnitsPerSide)
            throw new InvalidOperationException($"At most {MaxUnitsPerSide} units per side");
        anchors[unit] = anchor;
    }

    /// <summary>Places each unit in its own side's area, front row first, larger units first.</summary>
    public static BattleGrid AutoPlace(IEnumerable<Unit> units, int cols = 3, int rows = 2)
    {
        var grid = new BattleGrid(cols, rows);
        foreach (var unit in units.OrderByDescending(u => u.Def.Size))
        {
            var spot = Enumerable.Range(0, rows)
                .SelectMany(r => Enumerable.Range(0, cols).Select(c => new Tile(unit.Side, r, c)))
                .FirstOrDefault(t => grid.Fits(unit, t), new Tile(unit.Side, -1, -1));
            grid.Place(unit, spot);
        }
        return grid;
    }

    /// <summary>The unit is in the other side's area (it snuck in).</summary>
    public bool Intruding(Unit unit) => AnchorOf(unit) is { } a && a.Area != unit.Side;

    /// <summary>Any of the unit's tiles is within the first <paramref name="rows"/> rows of its area.</summary>
    public bool InRows(Unit unit, int rows) => Footprint(unit).Any(t => t.Row < rows);

    public bool InFrontRow(Unit unit) => InRows(unit, 1);

    /// <summary>Why <paramref name="actor"/> can't aim <paramref name="action"/> at <paramref name="target"/>, or null.</summary>
    public string? CantTarget(Unit actor, ActionDef action, Unit target)
    {
        if (!target.Alive) return "target is down";
        switch (action.Target)
        {
            case ActionTarget.Self:
                return target == actor ? null : "only targets self";
            case ActionTarget.Ally:
                if (target.Side != actor.Side) return "not an ally";
                break;
            case ActionTarget.Enemy:
                if (target.Side == actor.Side) return "not an enemy";
                break;
            case ActionTarget.Tile:
                return "targets a tile";
        }
        if (action.Range is not (ActionRange.Melee or ActionRange.Reach) || action.Target != ActionTarget.Enemy)
            return null;

        // Close combat inside one area: an intruder and the side it stands among.
        if (AnchorOf(actor) is { } a && AnchorOf(target) is { } b && a.Area == b.Area)
            return null;
        if (Intruding(target)) return "out of reach";
        if (!InFrontRow(actor)) return "not in the front row";
        var reach = action.Range == ActionRange.Reach ? 2 : 1;
        return InRows(target, reach) ? null : "out of reach";
    }

    /// <summary>Where <paramref name="unit"/> could step with Move: an empty tile next to it in the area it
    /// stands in (Small units only).</summary>
    public IEnumerable<Tile> MoveOptions(Unit unit)
    {
        if (unit.Def.Size != UnitSize.Small || AnchorOf(unit) is not { } at) return [];
        Tile[] next = [at with { Row = at.Row - 1 }, at with { Row = at.Row + 1 }, at with { Col = at.Col - 1 }, at with { Col = at.Col + 1 }];
        return next.Where(t => Inside(t) && At(t) is null);
    }

    /// <summary>Where <paramref name="unit"/> could Sneak to: any empty tile in the other side's area.</summary>
    public IEnumerable<Tile> SneakOptions(Unit unit)
    {
        if (unit.Def.Size != UnitSize.Small) return [];
        var area = unit.Side == Side.Party ? Side.Enemy : Side.Party;
        return Enumerable.Range(0, Rows)
            .SelectMany(r => Enumerable.Range(0, Cols).Select(c => new Tile(area, r, c)))
            .Where(t => At(t) is null);
    }

    /// <summary>Moves a unit, without range rules (Move, Sneak and Push/Pull check those).</summary>
    public void MoveTo(Unit unit, Tile anchor)
    {
        if (!Fits(unit, anchor))
            throw new InvalidOperationException($"{unit.Name} can't move to {anchor}");
        anchors[unit] = anchor;
    }

    /// <summary>
    /// Pushes (one row back) or pulls (one row forward) a unit within its area, if the tiles are free.
    /// Returns the new anchor, or null when it couldn't move.
    /// </summary>
    public Tile? Shove(Unit unit, Displace how)
    {
        if (AnchorOf(unit) is not { } at || how == Displace.None) return null;
        var to = at with { Row = at.Row + (how == Displace.Push ? 1 : -1) };
        if (!Fits(unit, to)) return null;
        anchors[unit] = to;
        return to;
    }

    /// <summary>
    /// While <paramref name="area"/>'s front row has no living unit and there's someone behind, everyone in it
    /// steps one row forward. Returns the units that moved.
    /// </summary>
    public List<Unit> Collapse(Side area)
    {
        var moved = new List<Unit>();
        while (true)
        {
            var inArea = anchors.Where(kv => kv.Key.Alive && kv.Value.Area == area).Select(kv => kv.Key).ToList();
            if (inArea.Count == 0 || inArea.Any(u => Footprint(u).Any(t => t.Row == 0))) return moved;
            foreach (var u in inArea)
            {
                anchors[u] = anchors[u] with { Row = anchors[u].Row - 1 };
                if (!moved.Contains(u)) moved.Add(u);
            }
        }
    }
}
