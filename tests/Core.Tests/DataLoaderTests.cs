using EternalDungeon.Core.Data;
using static EternalDungeon.Core.Tests.TestData;

namespace EternalDungeon.Core.Tests;

public class DataLoaderTests
{
    const string Fire = """[{ "id": "fire", "name": "Fire", "group": "element" }]""";
    const string HealthAndPower = """
        [{ "id": "health", "name": "Health", "group": "character", "combine": "add" },
         { "id": "power", "name": "Power", "group": "attack", "combine": "add" }]
        """;
    const string Magic = """[{ "id": "magic", "name": "Magic" }]""";
    const string MagicRows = """[{ "compound": "magic", "tag": "fire", "stat": "power", "coef": 1 }]""";
    const string Poke = """[{ "id": "poke", "name": "Poke", "tags": ["fire"], "target": "enemy", "range": "melee", "ap_cost": 100, "base_damage": 5 }]""";
    const string Basic = """[{ "id": "basic", "name": "Basic", "threat_weight": 0.5 }]""";
    const string BasicRules = """[{ "profile": "basic", "order": 1, "action": "poke" }]""";

    /// <summary>A small valid set of tables plus <paramref name="extra"/> (which replaces tables of the same name).</summary>
    static (string, string)[] Base(params (string Table, string Json)[] extra)
    {
        var tables = new Dictionary<string, string>
        {
            ["tags"] = Fire, ["stats"] = HealthAndPower, ["compound_stats"] = Magic, ["compound_stat_rows"] = MagicRows,
            ["actions"] = Poke, ["ai_profiles"] = Basic, ["ai_rules"] = BasicRules,
        };
        foreach (var (t, j) in extra) tables[t] = j;
        return [.. tables.Select(kv => (kv.Key, kv.Value))];
    }

    static DataException Fails(params (string Table, string Json)[] extra) => TablesFail(Base(extra));

    static DataException UnitFails(string unit, string tagStats = "[]") =>
        Fails(("units", $"[{unit}]"), ("unit_tag_stats", tagStats));

    [Fact]
    public void Units_load_with_stat_columns_tag_stats_and_compounds()
    {
        var data = LoadTables(Base(
            ("units", """[{ "id": "troll", "name": "Troll", "size": 1.5, "health": 200, "magic": 5, "actions": ["poke"], "ai": "basic" }]"""),
            ("unit_tag_stats", """[{ "unit": "troll", "stat": "power", "tag": "fire", "value": 10 }]""")));
        var troll = data.Units["troll"];
        Assert.Equal(UnitSize.Tall, troll.Size);
        Assert.Contains(new StatValue("health", null, 200), troll.Stats);
        Assert.Contains(new StatValue("power", "fire", 10), troll.Stats);
        Assert.Equal(5, troll.Compounds["magic"]);
    }

    [Fact]
    public void Unit_errors_name_the_file_row_and_column()
    {
        const string ok = """ "actions": ["poke"], "ai": "basic" """;
        Assert.Equal("[0].size", UnitFails($$"""{ "id": "u", "name": "U", "size": 3, "health": 1, {{ok}} }""").Field);
        Assert.Equal("[0].luck", UnitFails($$"""{ "id": "u", "name": "U", "size": 1, "health": 1, "luck": 7, {{ok}} }""").Field);
        Assert.Equal("[0].health", UnitFails($$"""{ "id": "u", "name": "U", "size": 1, "power": 1, {{ok}} }""").Field);
        Assert.Equal("[1].actions", UnitFails($$"""{ "id": "u", "name": "U", "size": 1, "health": 1, {{ok}} }, { "id": "v", "name": "V", "size": 1, "health": 1, "actions": ["poke", "fly"], "ai": "basic" }""").Field);

        var tag = UnitFails($$"""{ "id": "u", "name": "U", "size": 1, "health": 1, {{ok}} }""", """[{ "unit": "u", "stat": "health", "tag": "fire", "value": 5 }]""");
        Assert.Equal(("unit_tag_stats.json", "[0].stat"), (tag.File, tag.Field));
        var orphan = UnitFails($$"""{ "id": "u", "name": "U", "size": 1, "health": 1, {{ok}} }""", """[{ "unit": "ghost", "stat": "power", "tag": "fire", "value": 5 }]""");
        Assert.Equal("[0].unit", orphan.Field);
        Assert.Contains("units.json", orphan.Message);
    }

    [Fact]
    public void Repo_data_loads()
    {
        var data = DataLoader.LoadDirectory(TestPaths.DataDir);

        Assert.Equal(30, data.TagList.Count);
        Assert.Equal(TagGroup.Element, data.Tags["fire"].Group);
        Assert.Equal(CombineMode.Dim, data.Stats["hit"].Combine);
        Assert.Equal(CombineMode.Add, data.Stats["multiplier"].Combine);
        Assert.True(data.Stats["speed"].Integer);
        Assert.True(data.Stats["vulnerability"].Hidden);
        Assert.False(data.Stats["health"].TagKeyed);
        Assert.True(data.Stats["avoid"].TagKeyed);
        Assert.Equal(11, data.CompoundList.Count);
        Assert.Contains(new CompoundRow("heavy", "power", 0.5), data.Compounds["strength"].Rows);
    }

    [Fact]
    public void Compound_rows_must_reference_real_tag_keyed_stats()
    {
        var tag = Fails(("compound_stat_rows", """[{ "compound": "magic", "tag": "lava", "stat": "power", "coef": 1 }]"""));
        Assert.Equal(("compound_stat_rows.json", "[0].tag"), (tag.File, tag.Field));

        var stat = Fails(("compound_stat_rows", """[{ "compound": "magic", "tag": "fire", "stat": "health", "coef": 1 }]"""));
        Assert.Equal("[0].stat", stat.Field);

        var mult = Fails(
            ("stats", """[{ "id": "health", "name": "Health", "group": "character", "combine": "add" }, { "id": "power", "name": "Power", "group": "attack", "combine": "mult" }]"""),
            ("actions", "[]"), ("ai_profiles", "[]"), ("ai_rules", "[]"));
        Assert.Contains("mult", mult.Message);

        Assert.Contains("at least one row", Fails(("compound_stat_rows", "[]")).Message);
    }

    [Fact]
    public void Implied_tags_must_exist_and_imply_nothing_themselves()
    {
        static DataException TagsFail(string tags) => Fails(("tags", tags));

        var unknown = TagsFail("""[{ "id": "fire", "name": "Fire", "group": "element", "implies": ["elemental"] }]""");
        Assert.Equal(("tags.json", "[0].implies"), (unknown.File, unknown.Field));
        Assert.Contains("elemental", unknown.Message);

        Assert.Contains("itself", TagsFail("""[{ "id": "fire", "name": "Fire", "group": "element", "implies": ["fire"] }]""").Message);

        var chain = TagsFail("""
            [{ "id": "fire", "name": "Fire", "group": "element", "implies": ["elemental"] },
             { "id": "elemental", "name": "Elemental", "group": "family", "implies": ["magic"] },
             { "id": "magic", "name": "Magic", "group": "family" }]
            """);
        Assert.Equal("[0].implies", chain.Field);
    }

    [Fact]
    public void Actions_and_procs_carry_their_implied_tags_but_trigger_filters_dont()
    {
        var data = LoadTables(Base(
            ("tags", """
                [{ "id": "fire", "name": "Fire", "group": "element", "implies": ["elemental"] },
                 { "id": "elemental", "name": "Elemental", "group": "family" }]
                """),
            ("procs", """[{ "id": "p", "name": "P", "trigger": "struck", "trigger_tags": ["fire"], "tags": ["fire"], "target": "other", "damage": 1 }]""")));
        Assert.Equal(["fire", "elemental"], data.Actions["poke"].Tags);
        Assert.Equal(["fire", "elemental"], data.Procs["p"].Tags);
        Assert.Equal(["fire"], data.Procs["p"].TriggerTags);
    }

    static DataException ActionFails(string action) => Fails(
        ("tags", """
            [{ "id": "fire", "name": "Fire", "group": "element" },
             { "id": "melee", "name": "Melee", "group": "delivery" },
             { "id": "heavy", "name": "Heavy", "group": "style" }]
            """),
        ("actions", $"[{action}]"), ("ai_profiles", "[]"), ("ai_rules", "[]"));

    [Fact]
    public void Action_errors_name_the_column()
    {
        Assert.Equal("[0].ap_cost", ActionFails("""{ "id": "a", "name": "A", "target": "self", "ap_cost": 75 }""").Field);
        Assert.Equal("[0].tags", ActionFails("""{ "id": "a", "name": "A", "tags": ["heavy"], "target": "enemy", "range": "any", "ap_cost": 100 }""").Field);
        Assert.Equal("[0].range", ActionFails("""{ "id": "a", "name": "A", "tags": ["fire"], "target": "enemy", "ap_cost": 100 }""").Field);
        Assert.Contains("lava", ActionFails("""{ "id": "a", "name": "A", "tags": ["fire", "lava"], "target": "self", "ap_cost": 100 }""").Message);
        Assert.Equal("[0].base_damage", ActionFails("""{ "id": "a", "name": "A", "target": "self", "ap_cost": 100, "base_damage": 5 }""").Field);
    }

    [Fact]
    public void Reads_snake_case_enums()
    {
        var data = LoadTables(("tags", """[{ "id": "arcane", "name": "Arcane", "group": "damage_type" }]"""));
        Assert.Equal(TagGroup.DamageType, data.Tags["arcane"].Group);
    }

    [Fact]
    public void Missing_file_names_the_file()
    {
        var e = Assert.Throws<DataException>(() => DataLoader.Load(DataSource.FromFiles(
            new Dictionary<string, string> { ["tags.json"] = Fire })));
        Assert.Equal("stats.json", e.File);
        Assert.Contains("missing", e.Message);
    }

    [Fact]
    public void Bad_enum_names_file_field_and_allowed_values()
    {
        var e = Fails(("stats", """
            [
              { "id": "health", "name": "Health", "group": "character", "combine": "add" },
              { "id": "power", "name": "Power", "group": "attack", "combine": "sum" }
            ]
            """));
        Assert.Equal("stats.json [1].combine: expected one of add, dim, mult, got \"sum\"", e.Message);
    }

    [Fact]
    public void Missing_unknown_and_wrong_type_fields_are_named()
    {
        var missing = TablesFail(("tags", """[{ "id": "fire", "name": "Fire" }]"""));
        Assert.Equal(("tags.json", "[0].group"), (missing.File, missing.Field));

        var unknown = TablesFail(("tags", """[{ "id": "fire", "name": "Fire", "group": "element", "colour": "red" }]"""));
        Assert.Equal("[0].colour", unknown.Field);
        Assert.Contains("unknown field", unknown.Message);

        var type = TablesFail(("stats", """[{ "id": "speed", "name": "Speed", "group": "character", "combine": "add", "integer": "yes" }]"""));
        Assert.Equal("[0].integer", type.Field);
        Assert.Contains("true or false", type.Message);
    }

    [Fact]
    public void Duplicate_and_malformed_ids_are_rejected()
    {
        var dup = TablesFail(("tags", """[{ "id": "fire", "name": "Fire", "group": "element" }, { "id": "fire", "name": "Fire", "group": "element" }]"""));
        Assert.Equal("[1].id", dup.Field);
        Assert.Contains("duplicate", dup.Message);

        var bad = TablesFail(("tags", """[{ "id": "Fire Bolt", "name": "Fire", "group": "element" }]"""));
        Assert.Equal("[0].id", bad.Field);

        var order = Fails(("ai_rules", """[{ "profile": "basic", "order": 1, "action": "poke" }, { "profile": "basic", "order": 1, "action": "poke" }]"""));
        Assert.Equal("[1].profile", order.Field);
        Assert.Contains("duplicate row", order.Message);
    }

    [Fact]
    public void Integer_stats_must_combine_by_add()
    {
        var e = TablesFail(("stats", """[{ "id": "hit", "name": "Hit", "group": "attack", "combine": "dim", "integer": true }]"""));
        Assert.Equal("[0].integer", e.Field);
    }

    [Fact]
    public void Comments_and_broken_json_name_the_file_and_line()
    {
        var broken = TablesFail(("tags", "[\n  { \"id\": \"fire\" \n"));
        Assert.Equal("tags.json", broken.File);
        Assert.StartsWith("line ", broken.Field);

        var comment = TablesFail(("tags", "// notes go in the note column\n[]"));
        Assert.Equal("tags.json", comment.File);
    }

    [Fact]
    public void Strings_read_godots_csv_format()
    {
        var s = Strings.Parse("keys,en\nlog.hp,\"{target} HP {before} → {after}\"\nui.quote,\"say \"\"hi\"\", then go\"\nui.plain,Start\n");
        Assert.Equal("Grunt HP 5 → 0", s.Format("log.hp", ("target", "Grunt"), ("before", 5), ("after", 0)));
        Assert.Equal("say \"hi\", then go", s["ui.quote"]);
        Assert.Equal("Start", s["ui.plain"]);
        Assert.Equal("[ui.nothing]", s["ui.nothing"]);
        Assert.Contains("duplicate", Assert.Throws<DataException>(() => Strings.Parse("keys,en\na,1\na,2\n")).Message);
        Assert.True(Repo.Text.Has("log.victory"));
    }

    [Fact]
    public void Ordered_child_rows_follow_their_order_column()
    {
        var data = LoadTables(Base(
            ("tags", """[{ "id": "fire", "name": "Fire", "group": "element" }, { "id": "buff", "name": "Buff", "group": "function" }]"""),
            ("actions", """[{ "id": "poke", "name": "Poke", "tags": ["fire"], "target": "enemy", "range": "melee", "ap_cost": 100, "base_damage": 5 }, { "id": "rest", "name": "Rest", "target": "self", "ap_cost": 100 }]"""),
            ("ai_rules", """[{ "profile": "basic", "order": 2, "action": "poke" }, { "profile": "basic", "order": 1, "action": "rest" }]""")));
        Assert.Equal(["rest", "poke"], data.AiProfiles["basic"].Rules.Select(r => r.Action));
    }
}
