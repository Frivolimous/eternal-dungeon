using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Tests;

public class DataLoaderTests
{
    const string ValidTags = """[{ "id": "fire", "name": "Fire", "group": "element" }]""";
    const string ValidStats = """[{ "id": "power", "name": "Power", "group": "attack", "combine": "add" }]""";

    static GameData Load(string tags = ValidTags, string stats = ValidStats) =>
        DataLoader.Load(DataSource.FromFiles(new Dictionary<string, string>
        {
            [DataLoader.TagsFile] = tags,
            [DataLoader.StatsFile] = stats,
        }));

    static DataException LoadFails(string tags = ValidTags, string stats = ValidStats) =>
        Assert.Throws<DataException>(() => Load(tags, stats));

    [Fact]
    public void Repo_data_loads()
    {
        var data = DataLoader.LoadDirectory(TestPaths.DataDir);

        Assert.Equal(28, data.TagList.Count);
        Assert.Equal(TagGroup.Element, data.Tags["fire"].Group);
        Assert.Equal(CombineMode.Dim, data.Stats["hit"].Combine);
        Assert.Equal(CombineMode.Add, data.Stats["multiplier"].Combine);
        Assert.True(data.Stats["speed"].Integer);
        Assert.True(data.Stats["vulnerability"].Hidden);
        Assert.False(data.Stats["health"].TagKeyed);
        Assert.True(data.Stats["avoid"].TagKeyed);
    }

    [Fact]
    public void Reads_snake_case_enums()
    {
        var data = Load(tags: """[{ "id": "arcane", "name": "Arcane", "group": "damage_type" }]""");
        Assert.Equal(TagGroup.DamageType, data.Tags["arcane"].Group);
    }

    [Fact]
    public void Missing_file_names_the_file()
    {
        var e = Assert.Throws<DataException>(() => DataLoader.Load(DataSource.FromFiles(
            new Dictionary<string, string> { [DataLoader.TagsFile] = ValidTags })));
        Assert.Equal("stats.json", e.File);
        Assert.Contains("missing", e.Message);
    }

    [Fact]
    public void Bad_enum_names_file_field_and_allowed_values()
    {
        var e = LoadFails(stats: """
            [
              { "id": "power", "name": "Power", "group": "attack", "combine": "add" },
              { "id": "hit", "name": "Hit", "group": "attack", "combine": "sum" }
            ]
            """);
        Assert.Equal("stats.json", e.File);
        Assert.Equal("[1].combine", e.Field);
        Assert.Equal("stats.json [1].combine: expected one of add, dim, mult, got \"sum\"", e.Message);
    }

    [Fact]
    public void Missing_field_is_named()
    {
        var e = LoadFails(tags: """[{ "id": "fire", "name": "Fire" }]""");
        Assert.Equal("tags.json", e.File);
        Assert.Equal("[0].group", e.Field);
    }

    [Fact]
    public void Unknown_field_is_named()
    {
        var e = LoadFails(tags: """[{ "id": "fire", "name": "Fire", "group": "element", "colour": "red" }]""");
        Assert.Equal("[0].colour", e.Field);
        Assert.Contains("unknown field", e.Message);
    }

    [Fact]
    public void Wrong_type_is_named()
    {
        var e = LoadFails(stats: """[{ "id": "speed", "name": "Speed", "group": "character", "combine": "add", "integer": "yes" }]""");
        Assert.Equal("[0].integer", e.Field);
        Assert.Contains("true or false", e.Message);
    }

    [Fact]
    public void Duplicate_and_malformed_ids_are_rejected()
    {
        var dup = LoadFails(tags: """
            [{ "id": "fire", "name": "Fire", "group": "element" }, { "id": "fire", "name": "Fire", "group": "element" }]
            """);
        Assert.Equal("[1].id", dup.Field);
        Assert.Contains("duplicate", dup.Message);

        var bad = LoadFails(tags: """[{ "id": "Fire Bolt", "name": "Fire", "group": "element" }]""");
        Assert.Equal("[0].id", bad.Field);
    }

    [Fact]
    public void Integer_stats_must_combine_by_add()
    {
        var e = LoadFails(stats: """[{ "id": "hit", "name": "Hit", "group": "attack", "combine": "dim", "integer": true }]""");
        Assert.Equal("[0].integer", e.Field);
    }

    [Fact]
    public void Broken_json_names_the_file_and_line()
    {
        var e = LoadFails(tags: "[\n  { \"id\": \"fire\" \n");
        Assert.Equal("tags.json", e.File);
        Assert.StartsWith("line ", e.Field);
    }
}
