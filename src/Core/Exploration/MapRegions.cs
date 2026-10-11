using System.Numerics;
using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Exploration;

/// <summary>A border between two Regions: <see cref="A"/> and <see cref="B"/> (ordinal order) and its two ends.</summary>
public sealed record RegionBorder(string A, string B, Vector2 From, Vector2 To)
{
    public Vector2 Middle => (From + To) / 2;
}

/// <summary>The Regions of an Outdoor Map: each Node's area (the points nearer it than any other Node) and the borders
/// between them, in layout space (<see cref="MapRegions.Width"/> × <see cref="MapRegions.Height"/>).</summary>
public sealed record MapLayout(IReadOnlyDictionary<string, IReadOnlyList<Vector2>> Cells, IReadOnlyList<RegionBorder> Borders)
{
    public RegionBorder? Border(string a, string b) =>
        Borders.FirstOrDefault(x => (x.A == a && x.B == b) || (x.A == b && x.B == a));
}

/// <summary>
/// Divides a Map into Regions from its Nodes' positions (Anchor: Exploration › Map screen): each Node owns the part of
/// the Map nearest to it, so the areas need no drawing by hand. Pure geometry for the map screen, kept in Core so a test
/// can check that every link between Nodes crosses a shared border.
/// </summary>
public static class MapRegions
{
    /// <summary>Layout space: a Node's x (0–100) doubled, and its y. The screen stretches it to the map's area.</summary>
    public const float Width = 200, Height = 100;

    public static Vector2 Site(NodeDef n) => new((float)n.X * Width / 100, (float)n.Y * Height / 100);

    /// <summary>The node nearest to a point in layout space: the Region that holds it.</summary>
    public static NodeDef At(MapDef map, Vector2 point) =>
        map.Nodes.MinBy(n => Vector2.DistanceSquared(Site(n), point))!;

    public static MapLayout Of(MapDef map)
    {
        var cells = new Dictionary<string, IReadOnlyList<Vector2>>();
        var borders = new List<RegionBorder>();
        foreach (var node in map.Nodes)
        {
            var (points, labels) = Cell(map, node);
            cells[node.Id] = points;
            for (var k = 0; k < points.Count; k++)
            {
                // Each border is taken once, from the cell of the Node that sorts first.
                if (labels[k] is not { } other || string.CompareOrdinal(node.Id, other) > 0) continue;
                var (from, to) = (points[k], points[(k + 1) % points.Count]);
                if (Vector2.Distance(from, to) > 1e-3f) borders.Add(new RegionBorder(node.Id, other, from, to));
            }
        }
        return new MapLayout(cells, borders);
    }

    /// <summary>A Node's cell: the Map's rectangle cut by the half-way line to every other Node. Each edge is labelled
    /// with the Node on its other side (null along the Map's edge).</summary>
    static (List<Vector2> Points, List<string?> Labels) Cell(MapDef map, NodeDef node)
    {
        var points = new List<Vector2> { new(0, 0), new(Width, 0), new(Width, Height), new(0, Height) };
        var labels = new List<string?> { null, null, null, null };
        var site = Site(node);
        foreach (var other in map.Nodes)
        {
            if (other == node) continue;
            var o = Site(other);
            var normal = o - site;
            var middle = (o + site) / 2;
            bool Inside(Vector2 p) => Vector2.Dot(p - middle, normal) <= 0;
            Vector2 Cross(Vector2 a, Vector2 b)
            {
                var da = Vector2.Dot(a - middle, normal);
                var db = Vector2.Dot(b - middle, normal);
                return a + (b - a) * (da / (da - db));
            }
            var nextPoints = new List<Vector2>();
            var nextLabels = new List<string?>();
            for (var k = 0; k < points.Count; k++)
            {
                var (a, b, label) = (points[k], points[(k + 1) % points.Count], labels[k]);
                var (ina, inb) = (Inside(a), Inside(b));
                if (ina)
                {
                    nextPoints.Add(a);
                    nextLabels.Add(label);
                    if (!inb)
                    {
                        nextPoints.Add(Cross(a, b));             // leaving: the new edge runs along the half-way line
                        nextLabels.Add(other.Id);
                    }
                }
                else if (inb)
                {
                    nextPoints.Add(Cross(a, b));                 // entering: the rest of this edge
                    nextLabels.Add(label);
                }
            }
            (points, labels) = (nextPoints, nextLabels);
        }
        return (points, labels);
    }
}
