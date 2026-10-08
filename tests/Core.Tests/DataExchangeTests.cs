using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Tests;

// The data as flat tables: JSON is the source of truth, TSV is for spreadsheets (M2 brief §2).
public class DataExchangeTests
{
    static DataTables RepoTables() => DataTables.FromJson(DataSource.FromDirectory(TestPaths.DataDir));

    static Dictionary<string, string> RepoFiles() =>
        Directory.GetFiles(TestPaths.DataDir, "*.json").ToDictionary(Path.GetFileName, f => File.ReadAllText(f).Replace("\r\n", "\n"))!;

    [Fact]
    public void The_data_files_are_in_canonical_form()
    {
        var files = RepoFiles();
        Assert.Equal(Schemas.Names.Select(n => n + ".json").Order(), files.Keys.Order());
        foreach (var (name, text) in DataExchange.JsonFiles(RepoTables()))
            Assert.True(files[name] == text, $"{name} isn't canonical: run sim format-data");
    }

    [Fact]
    public void Export_then_import_reproduces_the_data_files_byte_for_byte()
    {
        var tables = RepoTables();
        var tsv = DataExchange.ExportTsv(tables);
        var update = DataExchange.ImportTsv(DataSource.FromFiles(tsv), tables);

        Assert.Empty(update.Changes);
        var files = RepoFiles();
        foreach (var (name, text) in update.JsonFiles)
            Assert.Equal(files[name], text);
    }

    static Dictionary<string, string> EditedTsv(string table, Func<string, string> edit)
    {
        var tsv = DataExchange.ExportTsv(RepoTables());
        tsv[table + ".tsv"] = edit(tsv[table + ".tsv"]);
        return tsv;
    }

    static string SetCell(string tsv, string id, string column, string value)
    {
        var lines = tsv.Split('\n');
        var col = Array.IndexOf(lines[0].Split('\t'), column);
        for (var i = 1; i < lines.Length; i++)
        {
            var cells = lines[i].Split('\t');
            if (cells[0] != id) continue;
            cells[col] = value;
            lines[i] = string.Join('\t', cells);
        }
        return string.Join('\n', lines);
    }

    [Fact]
    public void Import_summarizes_changes()
    {
        var tsv = EditedTsv("units", t => SetCell(t, "goblin_grunt", "health", "95"));
        var update = DataExchange.ImportTsv(DataSource.FromFiles(tsv), RepoTables());

        var health = RepoTables()["units"].Rows.Single(r => r.Str("id") == "goblin_grunt").Num("health");
        Assert.Equal([$"Goblin Grunt: health {health} → 95"], update.Changes);
        Assert.Contains("\"health\": 95,", update.JsonFiles["units.json"]);
    }

    [Fact]
    public void Invalid_imports_name_the_sheet_row_and_column()
    {
        var bad = EditedTsv("units", t => SetCell(t, "goblin_grunt", "health", "9,5"));
        var e = Assert.Throws<DataException>(() => DataExchange.ImportTsv(DataSource.FromFiles(bad), RepoTables()));
        Assert.Equal("units.tsv", e.File);
        Assert.Matches(@"^row \d+, health$", e.Field);
        Assert.Contains(". decimal point", e.Message);

        // A unit's own Health adds to the default stats' Health, so cancelling it out leaves 0.
        var defaultHealth = RepoTables()["default_stats"].Rows.Where(r => r.Str("stat") == "health").Sum(r => r.Num("value"));
        var rules = EditedTsv("units", t => SetCell(t, "goblin_grunt", "health", (-defaultHealth).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var invalid = Assert.Throws<DataException>(() => DataExchange.ImportTsv(DataSource.FromFiles(rules), RepoTables()));
        Assert.Contains("health above 0", invalid.Message);
    }

    [Fact]
    public void Underscore_columns_and_unknown_files_are_ignored()
    {
        var tsv = EditedTsv("units", t => string.Join('\n', t.Split('\n').Select((l, i) =>
            l.Length == 0 ? l : l + "\t" + (i == 0 ? "_ehp" : "=B2*2"))));
        tsv["notes.tsv"] = "anything\tgoes\n";
        Assert.Empty(DataExchange.ImportTsv(DataSource.FromFiles(tsv), RepoTables()).Changes);

        var unknown = EditedTsv("units", t => t.Replace("\tnote\n", "\tnotes\n"));
        Assert.Contains("unknown column", Assert.Throws<DataException>(() => DataExchange.ImportTsv(DataSource.FromFiles(unknown), RepoTables())).Message);
    }

    [Fact]
    public void Sorting_a_child_sheet_doesnt_change_the_json()
    {
        var tsv = EditedTsv("encounter_units", t =>
        {
            var lines = t.TrimEnd('\n').Split('\n');
            return string.Join('\n', [lines[0], .. lines.Skip(1).Reverse()]) + "\n";
        });
        var update = DataExchange.ImportTsv(DataSource.FromFiles(tsv), RepoTables());
        Assert.Empty(update.Changes);
        Assert.Equal(RepoFiles()["encounter_units.json"], update.JsonFiles["encounter_units.json"]);
    }
}
