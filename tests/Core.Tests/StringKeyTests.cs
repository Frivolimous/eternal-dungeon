using System.Text.RegularExpressions;
using static EternalDungeon.Core.Tests.TestData;

namespace EternalDungeon.Core.Tests;

/// <summary>The game's screens ask strings.csv for their text by key; every key they spell out must be there (a sheet pull
/// that drops keys would otherwise show "[ui.node_unexplored]" on screen).</summary>
public partial class StringKeyTests
{
    [GeneratedRegex("""\b(?:T|F|Text\.T|Text\.F)\("([a-z_]+\.[a-z0-9_.]+)"[,)]""")]
    private static partial Regex KeyUse();

    [Fact]
    public void Every_text_key_the_game_screens_use_is_in_the_strings_table()
    {
        var scripts = Path.Combine(TestPaths.RepoRoot, "game", "scripts");
        var missing = Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories)
            .SelectMany(f => File.ReadLines(f).Where(l => !l.TrimStart().StartsWith("//"))       // not examples in comments
                .SelectMany(l => KeyUse().Matches(l)).Select(m => (File: Path.GetFileName(f), Key: m.Groups[1].Value)))
            .Where(u => !Repo.Text.Has(u.Key))
            .Select(u => $"{u.File}: {u.Key}")
            .Distinct()
            .ToList();
        Assert.True(missing.Count == 0, "Keys missing from data/strings.csv:\n" + string.Join("\n", missing));
    }
}
