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
    public void Art_requests_doc_is_up_to_date()
    {
        var doc = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "docs", "art-requests.md")).Replace("\r\n", "\n");
        Assert.True(doc == ArtCatalog.Markdown(TestData.Repo).Replace("\r\n", "\n"), "run sim art-requests");
    }

    [Fact]
    public void The_art_catalog_covers_every_unit_and_sizes_art_at_twice_its_display()
    {
        var all = ArtCatalog.All(TestData.Repo);
        foreach (var unit in TestData.Repo.UnitList)
        {
            var portrait = all.Single(r => r.Id == ArtCatalog.PortraitId(unit.Id));
            var (w, h) = ArtCatalog.PortraitDisplay(unit.Size);
            Assert.Equal((w * 2, h * 2), (portrait.Width, portrait.Height));
            Assert.False(portrait.Optional);
        }
        Assert.Equal(all.Count, all.Select(r => r.Id).Distinct().Count());
        Assert.All(all.Where(r => r.Group != "Portraits"), r => Assert.True(r.Optional));
    }

    [Fact]
    public void Styles_are_listed_in_the_manifest_and_checked_against_the_catalog()
    {
        var root = Path.Combine(Path.GetTempPath(), "ed-styles-" + Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, "styles", "ink");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "portrait_nobody.png"), "");
            var manifest = AssetManifest.Parse("""{ "assets": [], "styles": [{ "id": "ink", "name": "Ink", "aiPlaceholder": true }] }""");
            var missing = new Dictionary<string, int>();
            var problems = manifest.CheckStyles(root, ArtCatalog.All(TestData.Repo), missing);
            Assert.Contains(problems, p => p.Contains("portrait_nobody.png") && p.Contains("not in the art catalog"));
            Assert.Equal(TestData.Repo.UnitList.Count, missing["ink"]);

            var unlisted = AssetManifest.Parse("""{ "assets": [] }""").CheckStyles(root, ArtCatalog.All(TestData.Repo));
            Assert.Contains(unlisted, p => p.Contains("not listed"));
        }
        finally { Directory.Delete(root, true); }
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
