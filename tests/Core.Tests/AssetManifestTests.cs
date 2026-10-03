using EternalDungeon.Core.Assets;
using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Tests;

public class AssetManifestTests
{
    [Fact]
    public void Repo_manifest_is_valid_and_its_files_match()
    {
        var dir = Path.Combine(TestPaths.RepoRoot, "game", "assets");
        var manifest = AssetManifest.Load(dir);
        Assert.Empty(manifest.CheckFiles(dir));
    }

    [Fact]
    public void Lists_only_flagged_assets()
    {
        var manifest = AssetManifest.Parse("""
            { "assets": [
              { "id": "warrior", "path": "heroes/warrior.png", "width": 128, "height": 128, "aiPlaceholder": true },
              { "id": "logo", "path": "ui/logo.png", "width": 512, "height": 128, "aiPlaceholder": false }
            ] }
            """);
        Assert.Equal(["warrior"], manifest.AiPlaceholders.Select(e => e.Id));
    }

    [Fact]
    public void The_flag_is_required()
    {
        var e = Assert.Throws<DataException>(() => AssetManifest.Parse("""
            { "assets": [ { "id": "warrior", "path": "heroes/warrior.png", "width": 128, "height": 128 } ] }
            """));
        Assert.Equal("manifest.json", e.File);
        Assert.Equal("assets[0].aiPlaceholder", e.Field);
    }

    [Fact]
    public void Reports_missing_files_and_wrong_sizes()
    {
        var dir = Directory.CreateTempSubdirectory("ed-assets-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "box.svg"), """<svg xmlns="http://www.w3.org/2000/svg" width="64" height="32"></svg>""");
            var manifest = AssetManifest.Parse("""
                { "assets": [
                  { "id": "box", "path": "box.svg", "width": 64, "height": 64, "aiPlaceholder": true },
                  { "id": "gone", "path": "gone.png", "width": 8, "height": 8, "aiPlaceholder": true }
                ] }
                """);
            var problems = manifest.CheckFiles(dir);
            Assert.Equal(2, problems.Count);
            Assert.Contains("64×64", problems[0]);
            Assert.Contains("not found", problems[1]);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
