namespace EternalDungeon.Core.Tests;

static class TestPaths
{
    public static string RepoRoot { get; } = Find();

    public static string DataDir => Path.Combine(RepoRoot, "data");

    static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "EternalDungeon.sln")))
                return dir.FullName;
        throw new InvalidOperationException("Can't find the repo root (EternalDungeon.sln).");
    }
}
