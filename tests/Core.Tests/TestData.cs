using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Tests;

static class TestData
{
    /// <summary>The repo's data/ folder, loaded once.</summary>
    public static GameData Repo { get; } = DataLoader.LoadDirectory(TestPaths.DataDir);
}
