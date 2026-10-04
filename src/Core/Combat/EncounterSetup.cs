using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

public static class EncounterSetup
{
    /// <summary>
    /// A battle for <paramref name="encounter"/>: the party and enemies placed as the data says. Unit ids are the
    /// unit's id plus a number (<c>goblin_grunt#2</c>); names get the number only when a unit appears more than
    /// once on its side ("Goblin Grunt #2").
    /// </summary>
    /// <param name="grid">An empty board to use instead of the default one (placements are front-relative, so they
    /// work on any board).</param>
    public static Battle Build(GameData data, EncounterDef encounter, ulong seed, BattleGrid? grid = null)
    {
        grid ??= new BattleGrid(DataLoader.AreaCols, DataLoader.AreaRows);
        var units = new List<Unit>();
        foreach (var (side, placements) in new[] { (Side.Party, encounter.Party), (Side.Enemy, encounter.Enemies) })
        {
            var counts = placements.GroupBy(p => p.Unit).ToDictionary(g => g.Key, g => g.Count());
            var seen = new Dictionary<string, int>();
            foreach (var p in placements)
            {
                var n = seen[p.Unit] = seen.GetValueOrDefault(p.Unit) + 1;
                var def = data.Units[p.Unit];
                var unit = new Unit($"{p.Unit}#{n}", def, side, data);
                if (counts[p.Unit] > 1) unit.Name = $"{def.Name} #{n}";
                grid.Place(unit, grid.TileAt(grid.HomeOf(side).Id, p.Row, p.Col));
                units.Add(unit);
            }
        }
        return new Battle(data, units, seed, grid);
    }
}
