using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

/// <summary>A tile in one area, in that area's own grid coordinates (<see cref="BattleGrid.Relative"/> gives the
/// front-relative ones).</summary>
public readonly record struct Tile(int Area, int Row, int Col)
{
    public override string ToString() => $"a{Area} r{Row}c{Col}";
}

/// <summary>An edge of an area's grid: the first or last row, or the first or last column.</summary>
public enum Edge { RowStart, RowEnd, ColStart, ColEnd }

/// <summary>A block of tiles belonging to one side, <see cref="Cols"/> × <see cref="Rows"/> in its own coordinates.</summary>
public sealed record BattleArea(int Id, Side Side, int Cols, int Rows);

/// <summary>Two opposing areas facing each other: <see cref="EdgeA"/> of area A meets <see cref="EdgeB"/> of area B.</summary>
public sealed record Front(int AreaA, Edge EdgeA, int AreaB, Edge EdgeB)
{
    public bool Touches(int area) => AreaA == area || AreaB == area;

    /// <summary>The edge of <paramref name="area"/> on this front.</summary>
    public Edge EdgeOf(int area) => area == AreaA ? EdgeA : EdgeB;

    public int Other(int area) => area == AreaA ? AreaB : AreaA;
}

/// <summary>
/// The battlefield (Anchor: Combat › Battlefield): areas joined by fronts. Core knows only this logic; where an area
/// sits on the table and which way it faces is presentation. Every rule is front-relative: a tile's depth is its
/// distance from its area's front edge (depth 0 is the front row), and its lane runs along that edge.
/// <para>The default board is two 3×2 areas, party and enemy, with their row-0 edges facing. A unit's footprint
/// follows its size: Small 1 tile, Tall 2 tiles (front and back), Large a 2×2 block, anchored at the tile nearest the
/// front with the lowest lane. Dead units leave their tiles. When an area's front row has no living unit, the area
/// collapses forward.</para>
/// <para>Range: Melee from depth 0 across a front to the other area's depth 0, in the same lane or the next one over
/// (straight ahead or diagonal, Jeremy 2026-10-08); Reach also its depth 1, with the same lanes; Any reaches every
/// tile. A unit standing in the other side's area (the Rogue's Move) melees and is meleed by units on adjacent or
/// diagonal tiles there.</para>
/// <para>M2 supports one front per area. With several (M3: Ambushed, Surrounding) range already checks every front
/// between the two areas; footprints, Push/Pull and collapse use an area's first front, and collapse is skipped for
/// areas with more than one.</para>
/// </summary>
public sealed class BattleGrid
{
    public const int MaxUnitsPerSide = 6;

    /// <summary>Area ids on the default board.</summary>
    public const int PartyArea = 0, EnemyArea = 1;

    readonly List<BattleArea> areas;
    public IReadOnlyList<BattleArea> Areas => areas;
    public IReadOnlyList<Front> Fronts { get; }

    readonly Dictionary<Unit, Tile> anchors = [];

    /// <summary>The default board: party and enemy areas of <paramref name="cols"/> × <paramref name="rows"/>,
    /// row 0 facing row 0.</summary>
    public BattleGrid(int cols = 3, int rows = 2)
        : this([new BattleArea(PartyArea, Side.Party, cols, rows), new BattleArea(EnemyArea, Side.Enemy, cols, rows)],
               [new Front(PartyArea, Edge.RowStart, EnemyArea, Edge.RowStart)])
    {
    }

    public BattleGrid(IReadOnlyList<BattleArea> areas, IReadOnlyList<Front> fronts)
    {
        for (var i = 0; i < areas.Count; i++)
            if (areas[i].Id != i) throw new ArgumentException("area ids must be 0, 1, 2… in order");
        foreach (var f in fronts)
        {
            if (f.AreaA >= areas.Count || f.AreaB >= areas.Count) throw new ArgumentException($"front {f} names a missing area");
            if (areas[f.AreaA].Side == areas[f.AreaB].Side) throw new ArgumentException($"front {f} joins two areas of one side");
        }
        if (areas.Any(a => !fronts.Any(f => f.Touches(a.Id)))) throw new ArgumentException("every area needs a front");
        this.areas = [.. areas];
        Fronts = fronts;
    }

    public IReadOnlyDictionary<Unit, Tile> Anchors => anchors;

    public Tile? AnchorOf(Unit unit) => anchors.TryGetValue(unit, out var t) ? t : null;

    public Side SideOf(Tile t) => Areas[t.Area].Side;

    /// <summary>The first area of <paramref name="side"/> (the only one in M2).</summary>
    public BattleArea HomeOf(Side side) => Areas.First(a => a.Side == side);

    public IEnumerable<Front> FrontsOf(int area) => Fronts.Where(f => f.Touches(area));

    /// <summary>The edge an area faces with: the edge of its first front.</summary>
    public Edge FacingOf(int area) => FrontsOf(area).First().EdgeOf(area);

    // ---- Front-relative geometry ----

    /// <summary>Distance of <paramref name="t"/> from <paramref name="edge"/> of its area: 0 on the edge.</summary>
    public int Depth(Tile t, Edge edge)
    {
        var a = Areas[t.Area];
        return edge switch
        {
            Edge.RowStart => t.Row,
            Edge.RowEnd => a.Rows - 1 - t.Row,
            Edge.ColStart => t.Col,
            _ => a.Cols - 1 - t.Col,
        };
    }

    /// <summary>Distance from the nearest front of the tile's area.</summary>
    public int Depth(Tile t) => FrontsOf(t.Area).Min(f => Depth(t, f.EdgeOf(t.Area)));

    /// <summary>One step away from <paramref name="edge"/> (back), and one step along it (the next lane).</summary>
    static ((int R, int C) Back, (int R, int C) Side) Steps(Edge edge) => edge switch
    {
        Edge.RowStart => ((1, 0), (0, 1)),
        Edge.RowEnd => ((-1, 0), (0, 1)),
        Edge.ColStart => ((0, 1), (1, 0)),
        _ => ((0, -1), (1, 0)),
    };

    static Tile Step(Tile t, (int R, int C) d, int n = 1) => t with { Row = t.Row + d.R * n, Col = t.Col + d.C * n };

    /// <summary>A tile's front-relative position in its area: depth from the facing edge, and lane along it.</summary>
    public (int Depth, int Lane) Relative(Tile t)
    {
        var edge = FacingOf(t.Area);
        var lane = edge is Edge.RowStart or Edge.RowEnd ? t.Col : t.Row;
        return (Depth(t, edge), lane);
    }

    /// <summary>The tile at a front-relative position: the inverse of <see cref="Relative"/>.</summary>
    public Tile TileAt(int area, int depth, int lane)
    {
        var a = Areas[area];
        return FacingOf(area) switch
        {
            Edge.RowStart => new Tile(area, depth, lane),
            Edge.RowEnd => new Tile(area, a.Rows - 1 - depth, lane),
            Edge.ColStart => new Tile(area, lane, depth),
            _ => new Tile(area, lane, a.Cols - 1 - depth),
        };
    }

    // ---- Footprints and placement ----

    /// <summary>The tiles a unit of <paramref name="size"/> anchored at <paramref name="anchor"/> covers.</summary>
    public IEnumerable<Tile> Footprint(UnitSize size, Tile anchor)
    {
        var (back, side) = Steps(FacingOf(anchor.Area));
        return size switch
        {
            UnitSize.Small => [anchor],
            UnitSize.Tall => [anchor, Step(anchor, back)],
            UnitSize.Large => [anchor, Step(anchor, side), Step(anchor, back), Step(Step(anchor, back), side)],
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };
    }

    public IEnumerable<Tile> Footprint(Unit unit) =>
        anchors.TryGetValue(unit, out var a) ? Footprint(unit.Def.Size, a) : [];

    public bool Inside(Tile t) =>
        t.Area >= 0 && t.Area < Areas.Count && t.Row >= 0 && t.Row < Areas[t.Area].Rows && t.Col >= 0 && t.Col < Areas[t.Area].Cols;

    /// <summary>The living unit on <paramref name="tile"/>, if any.</summary>
    public Unit? At(Tile tile) =>
        anchors.Keys.FirstOrDefault(u => u.Alive && Footprint(u).Contains(tile));

    /// <summary>Whether <paramref name="unit"/>'s footprint fits at <paramref name="anchor"/>, ignoring the unit itself.</summary>
    public bool Fits(Unit unit, Tile anchor) =>
        Inside(anchor) && Footprint(unit.Def.Size, anchor).All(t => Inside(t) && (At(t) is null || At(t) == unit));

    public void Place(Unit unit, Tile anchor)
    {
        if (!Fits(unit, anchor))
            throw new InvalidOperationException($"{unit.Name} doesn't fit at {anchor}");
        if (!anchors.ContainsKey(unit) && anchors.Keys.Count(u => u.Side == unit.Side) >= MaxUnitsPerSide)
            throw new InvalidOperationException($"At most {MaxUnitsPerSide} units per side");
        anchors[unit] = anchor;
    }

    /// <summary>Places each unit in its side's home area, front row first, larger units first.</summary>
    public static BattleGrid AutoPlace(IEnumerable<Unit> units, int cols = 3, int rows = 2)
    {
        var grid = new BattleGrid(cols, rows);
        foreach (var unit in units.OrderByDescending(u => u.Def.Size))
        {
            var area = grid.HomeOf(unit.Side);
            var spot = grid.InOrder(grid.TilesOf(area.Id))
                .Cast<Tile?>()
                .FirstOrDefault(t => grid.Fits(unit, t!.Value))
                ?? throw new InvalidOperationException($"No room for {unit.Name}");
            grid.Place(unit, spot);
        }
        return grid;
    }

    public IEnumerable<Tile> TilesOf(int area) =>
        Enumerable.Range(0, Areas[area].Rows).SelectMany(r => Enumerable.Range(0, Areas[area].Cols).Select(c => new Tile(area, r, c)));

    // ---- Rules ----

    /// <summary>The unit is in the other side's area (it snuck in).</summary>
    public bool Intruding(Unit unit) => AnchorOf(unit) is { } a && SideOf(a) != unit.Side;

    /// <summary>The unit's nearest tile to <paramref name="edge"/> of the area it stands in.</summary>
    int DepthOf(Unit unit, Edge edge) => Footprint(unit).Min(t => Depth(t, edge));

    /// <summary>Any of the unit's tiles is in the front row of its area (on any of its fronts).</summary>
    public bool InFrontRow(Unit unit) =>
        AnchorOf(unit) is { } a && FrontsOf(a.Area).Any(f => DepthOf(unit, f.EdgeOf(a.Area)) == 0);

    /// <summary>Why <paramref name="actor"/> can't aim <paramref name="action"/> at <paramref name="target"/>, or null.</summary>
    public string? CantTarget(Unit actor, ActionDef action, Unit target)
    {
        if (!target.Alive) return "target_down";
        switch (action.Target)
        {
            case ActionTarget.Self:
                return target == actor ? null : "only_targets_self";
            case ActionTarget.Ally:
                if (target.Side != actor.Side) return "not_an_ally";
                break;
            case ActionTarget.Enemy:
                if (target.Side == actor.Side) return "not_an_enemy";
                break;
            case ActionTarget.Tile:
                return "targets_a_tile";
        }
        if (action.Target != ActionTarget.Enemy) return null;
        return CantReach(actor, action, target);
    }

    /// <summary>
    /// Whether <paramref name="action"/>'s range reaches <paramref name="target"/>, whichever side it's on: a Confused
    /// unit can hit its allies. Melee and Reach follow the reach rules below; everything else reaches every tile.
    /// </summary>
    public string? CantReach(Unit actor, ActionDef action, Unit target)
    {
        if (!target.Alive) return "target_down";
        if (action.Range is not (ActionRange.Melee or ActionRange.Reach)) return null;
        if (AnchorOf(actor) is not { } a || AnchorOf(target) is not { } b) return "out_of_reach";

        var reach = action.Range == ActionRange.Reach ? 2 : 1;

        // Close combat inside one area (an intruder and the side it stands among): adjacent or diagonal tiles only
        // (within 2 for Reach).
        if (a.Area == b.Area)
            return Footprint(actor).Any(x => Footprint(target).Any(y => Math.Max(Math.Abs(x.Row - y.Row), Math.Abs(x.Col - y.Col)) <= reach))
                ? null
                : "out_of_reach";
        if (Intruding(target) || SideOf(a) == SideOf(b)) return "out_of_reach";
        if (!InFrontRow(actor)) return "not_in_front_row";

        // Across a front: from the front row, the target's first row (two for Reach), in the same lane or the next
        // one over: straight ahead or diagonal.
        var across = Fronts.Where(f => f.Touches(a.Area) && f.Other(a.Area) == b.Area);
        return across.Any(f => ReachesAcross(actor, target, f.EdgeOf(a.Area), f.EdgeOf(b.Area), reach))
            ? null
            : "out_of_reach";
    }

    /// <summary>A tile's lane along <paramref name="edge"/>: its column for a row edge, its row for a column edge.</summary>
    static int Lane(Tile t, Edge edge) => edge is Edge.RowStart or Edge.RowEnd ? t.Col : t.Row;

    /// <summary>Whether one of <paramref name="actor"/>'s front-row tiles is at most one lane from one of
    /// <paramref name="target"/>'s tiles within <paramref name="reach"/> rows of its front. Lanes face each other across
    /// a front: lane 0 faces lane 0.</summary>
    bool ReachesAcross(Unit actor, Unit target, Edge mine, Edge theirs, int reach)
    {
        var from = Footprint(actor).Where(t => Depth(t, mine) == 0).Select(t => Lane(t, mine)).ToList();
        return Footprint(target).Where(t => Depth(t, theirs) < reach)
            .Any(t => from.Any(lane => Math.Abs(lane - Lane(t, theirs)) <= 1));
    }

    /// <summary>Where <paramref name="unit"/> could go with Move: any empty tile in the area it stands in (Small units
    /// only; Jeremy, 2026-10-08: Move isn't limited to neighbouring tiles).</summary>
    public IEnumerable<Tile> MoveOptions(Unit unit)
    {
        if (unit.Def.Size != UnitSize.Small || AnchorOf(unit) is not { } at) return [];
        return InOrder(TilesOf(at.Area).Where(t => At(t) is null));
    }

    /// <summary>Tiles in front-relative order (area, depth, lane), so choices don't depend on the layout.</summary>
    IEnumerable<Tile> InOrder(IEnumerable<Tile> tiles) =>
        tiles.OrderBy(t => t.Area).ThenBy(t => Relative(t).Depth).ThenBy(t => Relative(t).Lane);

    /// <summary>Move options nearer the front.</summary>
    public IEnumerable<Tile> ForwardOptions(Unit unit) =>
        AnchorOf(unit) is { } at ? MoveOptions(unit).Where(t => Depth(t) < Depth(at)) : [];

    /// <summary>Where a feared unit may Move: an empty tile further from the front.</summary>
    public IEnumerable<Tile> RetreatOptions(Unit unit) =>
        AnchorOf(unit) is { } at ? MoveOptions(unit).Where(t => Depth(t) > Depth(at)) : [];

    /// <summary>Any empty tile in an area of the other side (Small units only): where the Rogue's Move can go.</summary>
    public IEnumerable<Tile> EnemyAreaOptions(Unit unit)
    {
        if (unit.Def.Size != UnitSize.Small) return [];
        return InOrder(Areas.Where(a => a.Side != unit.Side).SelectMany(a => TilesOf(a.Id)).Where(t => At(t) is null));
    }

    /// <summary>Any empty tile in the unit's own side's areas, while it stands in the enemy area (Small units only):
    /// how a Rogue that went in comes back.</summary>
    public IEnumerable<Tile> HomeAreaOptions(Unit unit)
    {
        if (unit.Def.Size != UnitSize.Small || !Intruding(unit)) return [];
        return InOrder(Areas.Where(a => a.Side == unit.Side).SelectMany(a => TilesOf(a.Id)).Where(t => At(t) is null));
    }

    /// <summary>Where a tile action takes <paramref name="unit"/>: Move's neighbours, plus the other side's empty
    /// tiles for a Move that can enter the enemy area, plus its own side's empty tiles while it stands in the enemy
    /// area. Feared units are limited further by the caller.</summary>
    public IEnumerable<Tile> TileOptions(Unit unit, ActionDef action) => action.MoveTo switch
    {
        Data.MoveTo.Own => MoveOptions(unit).Concat(HomeAreaOptions(unit)).Distinct(),
        Data.MoveTo.OwnOrEnemy => MoveOptions(unit).Concat(EnemyAreaOptions(unit)).Concat(HomeAreaOptions(unit)).Distinct(),
        _ => [],
    };

    /// <summary>The tile is in an area of the other side from <paramref name="unit"/>.</summary>
    public bool InEnemyArea(Unit unit, Tile tile) => SideOf(tile) != unit.Side;

    /// <summary>Moves a unit, without range rules (Move and Push/Pull check those).</summary>
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
        var (back, _) = Steps(FacingOf(at.Area));
        var to = Step(at, back, how == Displace.Push ? 1 : -1);
        if (!Fits(unit, to)) return null;
        anchors[unit] = to;
        return to;
    }

    /// <summary>
    /// The forward collapse (Jeremy, 2026-10-08). When none of <paramref name="area"/>'s own units stands in its front
    /// row, its own units step forward until one does. Units of the other side standing in it (a Rogue that moved in)
    /// don't count and don't step with them: each keeps its tile if it's still free, else goes to the front-most free
    /// tile there (nearest its lane), else to the front-most free tile of its own side's area, else a new back row is
    /// added to that area for it. Areas with more than one front don't collapse (M3).
    /// </summary>
    /// <returns>Every unit that moved, with where from, where to, and why ("collapse" or "displaced"), and the areas
    /// that grew a row.</returns>
    public (List<(Unit Unit, Tile From, Tile To, string Why)> Moves, List<int> Grown) Collapse(int area)
    {
        var moves = new List<(Unit, Tile, Tile, string)>();
        var grown = new List<int>();
        if (FrontsOf(area).Count() != 1) return (moves, grown);
        var side = Areas[area].Side;
        var edge = FacingOf(area);
        var (back, _) = Steps(edge);
        var here = anchors.Where(kv => kv.Key.Alive && kv.Value.Area == area).Select(kv => kv.Key).ToList();
        var own = here.Where(u => u.Side == side).ToList();
        if (own.Count == 0 || own.Any(u => DepthOf(u, edge) == 0)) return (moves, grown);

        var from = here.ToDictionary(u => u, u => anchors[u]);
        var guests = here.Where(u => u.Side != side).OrderBy(u => Relative(from[u]).Depth).ThenBy(u => Relative(from[u]).Lane).ToList();
        foreach (var g in guests) anchors.Remove(g);
        while (!own.Any(u => DepthOf(u, edge) == 0))
            foreach (var u in own)
                anchors[u] = Step(anchors[u], back, -1);
        foreach (var u in own)
            moves.Add((u, from[u], anchors[u], "collapse"));

        foreach (var guest in guests)
        {
            var was = from[guest];
            var lane = Relative(was).Lane;
            Tile? spot = Fits(guest, was) ? was : FrontMost(guest, area, lane);
            if (spot is null)
            {
                var home = HomeOf(guest.Side).Id;
                spot = FrontMost(guest, home, lane);
                if (spot is null)
                {
                    AddBackRow(home);
                    grown.Add(home);
                    spot = FrontMost(guest, home, lane);
                }
            }
            anchors[guest] = spot!.Value;
            if (spot.Value != was) moves.Add((guest, was, spot.Value, "displaced"));
        }
        return (moves, grown);
    }

    /// <summary>The free tile nearest <paramref name="area"/>'s front where <paramref name="unit"/> fits, nearest
    /// <paramref name="lane"/> first; null when there's none.</summary>
    Tile? FrontMost(Unit unit, int area, int lane) =>
        TilesOf(area)
            .OrderBy(t => Relative(t).Depth).ThenBy(t => Math.Abs(Relative(t).Lane - lane)).ThenBy(t => Relative(t).Lane)
            .Cast<Tile?>()
            .FirstOrDefault(t => Fits(unit, t!.Value));

    /// <summary>Adds a row at the back of <paramref name="area"/> (the row furthest from its front). Where the back is
    /// row or column 0, everything in the area shifts by one so its front-relative position is unchanged.</summary>
    public void AddBackRow(int area)
    {
        var a = Areas[area];
        var edge = FacingOf(area);
        areas[area] = edge is Edge.RowStart or Edge.RowEnd ? a with { Rows = a.Rows + 1 } : a with { Cols = a.Cols + 1 };
        if (edge is Edge.RowEnd or Edge.ColEnd)
            foreach (var (u, t) in anchors.Where(kv => kv.Value.Area == area).ToList())
                anchors[u] = edge == Edge.RowEnd ? t with { Row = t.Row + 1 } : t with { Col = t.Col + 1 };
    }
}
