using EternalDungeon.Core.Stats;

namespace EternalDungeon.Core.Data;

/// <summary>
/// Content that loads but is almost certainly a design mistake. Unlike a <see cref="DataException"/> it doesn't
/// stop the game: <c>sim data</c> prints these and a test keeps the repo's data free of them.
/// </summary>
public static class DataWarnings
{
    public static IReadOnlyList<string> Check(GameData data)
    {
        var warnings = new List<string>();
        foreach (var unit in data.UnitList)
            foreach (var (compound, value) in unit.Compounds)
                if (value > StatBlock.CompoundWarning)
                    warnings.Add($"{DataLoader.UnitsFile} {unit.Id}: {data.Compounds[compound].Name} {value} is above {StatBlock.CompoundWarning} (compound stats are capped at {StatBlock.MaxCompoundPoints})");
        return warnings;
    }
}
