using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

public static class EncounterSetup
{
    /// <summary>
    /// A battle for <paramref name="encounter"/>: the party and enemies placed as the data says. Unit ids are the
    /// unit's id plus a number (<c>goblin_grunt#2</c>); names get the number only when a unit appears more than
    /// once on its side ("Goblin Grunt #2"). An encounter without party rows (an Event's fight) gets the preset heroes
    /// at full Health, with their starting belts.
    /// </summary>
    /// <param name="grid">An empty board to use instead of the default one (placements are front-relative, so they
    /// work on any board).</param>
    public static Battle Build(GameData data, EncounterDef encounter, ulong seed, BattleGrid? grid = null)
    {
        grid ??= new BattleGrid(DataLoader.AreaCols, DataLoader.AreaRows);
        var presetParty = encounter.Party.Count == 0;
        var party = presetParty ? data.Heroes.Select(h => new Placement(h.Unit, h.Row, h.Col)).ToList() : encounter.Party;
        var units = Place(data, Side.Party, party, grid);
        if (presetParty)
            foreach (var (hero, unit) in data.Heroes.Zip(units))
                foreach (var item in hero.Belt.Select(i => data.Items[i]))
                    unit.Belt.Add(new BeltSlot(item, item.Uses));
        units.AddRange(Place(data, Side.Enemy, encounter.Enemies, grid));
        return new Battle(data, units, seed, grid);
    }

    /// <summary>Creates and places one side's units, numbered as <see cref="Build"/> describes.</summary>
    public static List<Unit> Place(GameData data, Side side, IReadOnlyList<Placement> placements, BattleGrid grid)
    {
        var units = new List<Unit>();
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
        return units;
    }
}
