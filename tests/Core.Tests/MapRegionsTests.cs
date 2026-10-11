using System.Numerics;
using EternalDungeon.Core.Exploration;
using static EternalDungeon.Core.Tests.TestData;

namespace EternalDungeon.Core.Tests;

/// <summary>The Regions the map screen draws from the Nodes' positions (Anchor: Exploration › Map screen).</summary>
public class MapRegionsTests
{
    [Fact]
    public void Every_link_between_nodes_crosses_a_shared_border()
    {
        var missing = new List<string>();
        foreach (var map in Repo.Dungeons.Values.SelectMany(d => d.Maps))
        {
            var layout = MapRegions.Of(map);
            foreach (var node in map.Nodes)
                foreach (var to in node.Links.Where(l => string.CompareOrdinal(node.Id, l) < 0 && map.Nodes.Any(n => n.Id == l)))
                    if (layout.Border(node.Id, to) is null) missing.Add($"{map.Id}: {node.Id} – {to}");
        }
        Assert.True(missing.Count == 0, "Linked Nodes whose Regions don't touch (move them closer):\n" + string.Join("\n", missing));
    }

    [Fact]
    public void Each_region_holds_its_own_node_and_the_regions_fill_the_map()
    {
        foreach (var map in Repo.Dungeons.Values.SelectMany(d => d.Maps))
        {
            var layout = MapRegions.Of(map);
            var area = 0f;
            foreach (var node in map.Nodes)
            {
                var cell = layout.Cells[node.Id];
                area += Area(cell);
                Assert.Equal(node, MapRegions.At(map, Centroid(cell)));
            }
            Assert.Equal(MapRegions.Width * MapRegions.Height, area, 1);
        }
    }

    static float Area(IReadOnlyList<Vector2> p) =>
        Math.Abs(Enumerable.Range(0, p.Count).Sum(i => p[i].X * p[(i + 1) % p.Count].Y - p[(i + 1) % p.Count].X * p[i].Y)) / 2;

    static Vector2 Centroid(IReadOnlyList<Vector2> p) => p.Aggregate(Vector2.Zero, (a, b) => a + b) / p.Count;
}
