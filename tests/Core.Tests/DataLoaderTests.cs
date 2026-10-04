using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Tests;

public class DataLoaderTests
{
    const string ValidTags = """[{ "id": "fire", "name": "Fire", "group": "element" }]""";
    const string ValidStats = """[{ "id": "power", "name": "Power", "group": "attack", "combine": "add" }]""";

    const string ValidCompounds = """[{ "id": "magic", "name": "Magic", "rows": [{ "tag": "fire", "stat": "power", "coef": 1 }] }]""";

    const string HealthAndPower = """
        [{ "id": "health", "name": "Health", "group": "character", "combine": "add" },
         { "id": "power", "name": "Power", "group": "attack", "combine": "add" }]
        """;
    const string ValidUnits = "[]";
    const string ValidAis = """[{ "id": "basic", "name": "Basic", "threatWeight": 0.5, "rules": [{ "action": "poke" }] }]""";
    const string ValidActions = """[{ "id": "poke", "name": "Poke", "tags": ["fire"], "target": "enemy", "range": "melee", "apCost": 100, "baseDamage": 5 }]""";

    static GameData Load(string tags = ValidTags, string stats = ValidStats, string compounds = ValidCompounds, string units = ValidUnits, string actions = ValidActions, string effects = "[]", string ais = ValidAis) =>
        DataLoader.Load(DataSource.FromFiles(new Dictionary<string, string>
        {
            [DataLoader.TagsFile] = tags,
            [DataLoader.StatsFile] = stats,
            [DataLoader.CompoundStatsFile] = compounds,
            [DataLoader.UnitsFile] = units,
            [DataLoader.ActionsFile] = actions,
            [DataLoader.EffectsFile] = effects,
            [DataLoader.AiProfilesFile] = ais,
            [DataLoader.EncountersFile] = "[]",
            [DataLoader.DefaultsFile] = """{ "unitStats": [] }""",
        }));

    static DataException LoadFails(string tags = ValidTags, string stats = ValidStats, string compounds = ValidCompounds, string units = ValidUnits, string actions = ValidActions, string effects = "[]", string ais = ValidAis) =>
        Assert.Throws<DataException>(() => Load(tags, stats, compounds, units, actions, effects, ais));

    static DataException UnitFails(string unitJson) =>
        LoadFails(stats: HealthAndPower, units: $"[{unitJson}]");

    [Fact]
    public void Units_load_with_stats_tag_stats_and_compounds()
    {
        var data = Load(stats: HealthAndPower, units: """
            [{ "id": "troll", "name": "Troll", "size": 1.5,
               "stats": { "health": 200 },
               "tagStats": [{ "tag": "fire", "stat": "power", "value": 10 }],
               "compounds": { "magic": 5 }, "actions": ["poke"], "ai": "basic" }]
            """);
        var troll = data.Units["troll"];
        Assert.Equal(UnitSize.Tall, troll.Size);
        Assert.Contains(new StatValue("health", null, 200), troll.Stats);
        Assert.Contains(new StatValue("power", "fire", 10), troll.Stats);
        Assert.Equal(5, troll.Compounds["magic"]);
    }

    [Fact]
    public void Unit_errors_name_the_field()
    {
        Assert.Equal("[0].size", UnitFails("""{ "id": "u", "name": "U", "size": 3, "stats": { "health": 1 } }""").Field);
        Assert.Equal("[0].stats.luck", UnitFails("""{ "id": "u", "name": "U", "size": 1, "stats": { "health": 1, "luck": 7 } }""").Field);
        Assert.Equal("[0].compounds.charm", UnitFails("""{ "id": "u", "name": "U", "size": 1, "stats": { "health": 1 }, "compounds": { "charm": 3 } }""").Field);
        Assert.Equal("[0].tagStats[0].stat", UnitFails("""{ "id": "u", "name": "U", "size": 1, "stats": { "health": 1 }, "tagStats": [{ "tag": "fire", "stat": "health", "value": 5 }] }""").Field);
        Assert.Equal("[0].stats", UnitFails("""{ "id": "u", "name": "U", "size": 1, "stats": { "power": 1 }, "actions": ["poke"], "ai": "basic" }""").Field);
    }

    [Fact]
    public void Repo_data_loads()
    {
        var data = DataLoader.LoadDirectory(TestPaths.DataDir);

        Assert.Equal(29, data.TagList.Count);
        Assert.Equal(TagGroup.Element, data.Tags["fire"].Group);
        Assert.Equal(CombineMode.Dim, data.Stats["hit"].Combine);
        Assert.Equal(CombineMode.Add, data.Stats["multiplier"].Combine);
        Assert.True(data.Stats["speed"].Integer);
        Assert.True(data.Stats["vulnerability"].Hidden);
        Assert.False(data.Stats["health"].TagKeyed);
        Assert.True(data.Stats["avoid"].TagKeyed);
        Assert.Equal(12, data.CompoundList.Count);
        Assert.Contains(new CompoundRow("heavy", "power", 0.5), data.Compounds["strength"].Rows);
    }

    [Fact]
    public void Compound_rows_must_reference_real_tag_keyed_stats()
    {
        var tag = LoadFails(compounds: """[{ "id": "magic", "name": "Magic", "rows": [{ "tag": "lava", "stat": "power", "coef": 1 }] }]""");
        Assert.Equal("compound_stats.json", tag.File);
        Assert.Equal("[0].rows[0].tag", tag.Field);

        var stat = LoadFails(
            stats: """[{ "id": "power", "name": "Power", "group": "attack", "combine": "add" }, { "id": "health", "name": "Health", "group": "character", "combine": "add" }]""",
            compounds: """[{ "id": "vigor", "name": "Vigor", "rows": [{ "tag": "fire", "stat": "health", "coef": 1 }] }]""");
        Assert.Equal("[0].rows[0].stat", stat.Field);

        var mult = LoadFails(
            stats: """[{ "id": "echo", "name": "Echo", "group": "attack", "combine": "mult" }]""",
            compounds: """[{ "id": "magic", "name": "Magic", "rows": [{ "tag": "fire", "stat": "echo", "coef": 1 }] }]""");
        Assert.Contains("mult", mult.Message);
    }

    static DataException ActionFails(string actionJson) =>
        LoadFails(tags: """
            [{ "id": "fire", "name": "Fire", "group": "element" },
             { "id": "melee", "name": "Melee", "group": "delivery" },
             { "id": "heavy", "name": "Heavy", "group": "style" }]
            """, compounds: "[]", actions: $"[{actionJson}]", ais: "[]");

    [Fact]
    public void Action_errors_name_the_field()
    {
        Assert.Equal("[0].apCost", ActionFails("""{ "id": "a", "name": "A", "tags": [], "target": "self", "apCost": 75 }""").Field);
        Assert.Equal("[0].tags", ActionFails("""{ "id": "a", "name": "A", "tags": ["heavy"], "target": "enemy", "range": "any", "apCost": 100 }""").Field);
        Assert.Equal("[0].range", ActionFails("""{ "id": "a", "name": "A", "tags": ["fire"], "target": "enemy", "apCost": 100 }""").Field);
        Assert.Equal("[0].tags[1]", ActionFails("""{ "id": "a", "name": "A", "tags": ["fire", "lava"], "target": "self", "apCost": 100 }""").Field);
        Assert.Equal("[0].baseDamage", ActionFails("""{ "id": "a", "name": "A", "tags": [], "target": "self", "apCost": 100, "baseDamage": 5 }""").Field);
    }

    [Fact]
    public void Units_must_reference_real_actions()
    {
        var e = UnitFails("""{ "id": "u", "name": "U", "size": 1, "stats": { "health": 1 }, "actions": ["poke", "fly"], "ai": "basic" }""");
        Assert.Equal("[0].actions[1]", e.Field);
        Assert.Contains("actions.json", e.Message);
    }

    [Fact]
    public void Reads_snake_case_enums()
    {
        var data = Load(tags: """[{ "id": "arcane", "name": "Arcane", "group": "damage_type" }]""", compounds: "[]", actions: "[]", ais: "[]");
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
