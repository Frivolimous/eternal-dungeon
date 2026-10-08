using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Tests;

// The Google Sheets sync: a push plans edits, a fake sheet applies them as tools/sheets-sync.gs does, a pull reads
// the tabs back through the TSV import.
public class SheetSyncTests
{
    static DataTables RepoTables() => DataTables.FromJson(DataSource.FromDirectory(TestPaths.DataDir));

    static string RepoStrings() => File.ReadAllText(Path.Combine(TestPaths.DataDir, Strings.FileName)).Replace("\r\n", "\n");

    static Dictionary<string, string> RepoFiles() =>
        Directory.GetFiles(TestPaths.DataDir, "*.json").ToDictionary(f => Path.GetFileName(f), f => File.ReadAllText(f).Replace("\r\n", "\n"))!;

    /// <summary>A sheet in memory. It applies edits the way the Apps Script does and reads tabs the way
    /// getDataRange().getValues() does: a rectangle up to the last cell with something in it.</summary>
    sealed class FakeSheet
    {
        public readonly List<(string Name, List<List<object?>> Grid)> Tabs = [("Sheet1", [])];

        public List<List<object?>> this[string name] => Tabs.Single(t => t.Name == name).Grid;

        public List<SheetTab> Read() => [.. Tabs.Select(t => new SheetTab(t.Name, Values(t.Grid)))];

        static List<IReadOnlyList<object?>> Values(List<List<object?>> grid)
        {
            static bool Empty(object? v) => v is null or "";
            var rows = grid.FindLastIndex(r => r.Any(v => !Empty(v))) + 1;
            var cols = rows == 0 ? 0 : grid.Take(rows).Max(r => r.FindLastIndex(v => !Empty(v)) + 1);
            if (rows == 0) return [new object?[] { "" }];
            return [.. grid.Take(rows).Select(r => (IReadOnlyList<object?>)Enumerable.Range(0, cols).Select(c => c < r.Count ? r[c] ?? "" : "").ToList())];
        }

        public void Apply(IEnumerable<SheetOp> ops)
        {
            foreach (var op in ops)
            {
                if (op.Op == "addTab") { Tabs.Add((op.Tab, [])); continue; }
                var grid = this[op.Tab];
                void Rows(int n) { while (grid.Count < n) grid.Add([]); }
                switch (op.Op)
                {
                    case "insertRowsAfter":
                        Rows(op.Row);
                        grid.InsertRange(op.Row, Enumerable.Range(0, op.Count).Select(_ => new List<object?>()));
                        break;
                    case "deleteRows":
                        grid.RemoveRange(op.Row - 1, Math.Min(op.Count, grid.Count - (op.Row - 1)));
                        break;
                    case "insertColumnsAfter":
                        foreach (var r in grid)
                        {
                            while (r.Count < op.Col) r.Add(null);
                            r.InsertRange(op.Col, Enumerable.Repeat<object?>(null, op.Count));
                        }
                        break;
                    case "deleteColumns":
                        foreach (var r in grid.Where(r => r.Count >= op.Col))
                            r.RemoveRange(op.Col - 1, Math.Min(op.Count, r.Count - (op.Col - 1)));
                        break;
                    case "write":
                        Rows(op.Row - 1 + op.Values!.Length);
                        for (var i = 0; i < op.Values.Length; i++)
                        {
                            var row = grid[op.Row - 1 + i];
                            for (var j = 0; j < op.Values[i].Length; j++)
                            {
                                while (row.Count < op.Col + j) row.Add(null);
                                // Sheets stores "'text" as the text; "" is an empty cell.
                                row[op.Col - 1 + j] = op.Values[i][j] switch { "" => null, string s when s.StartsWith('\'') => s[1..], var v => v };
                            }
                        }
                        break;
                    default:
                        throw new InvalidOperationException(op.Op);
                }
            }
        }
    }

    static FakeSheet Pushed()
    {
        var sheet = new FakeSheet();
        sheet.Apply(SheetSync.Plan(sheet.Read(), RepoTables(), RepoStrings()));
        return sheet;
    }

    /// <summary>The repo's tables with one TSV edited, unvalidated (a push doesn't need valid data to plan).</summary>
    static DataTables Edited(string table, Func<string, string> edit)
    {
        var tsv = DataExchange.ExportTsv(RepoTables());
        tsv[table + ".tsv"] = edit(tsv[table + ".tsv"]);
        return DataTables.Read(s => TableFormat.ReadTsv(s, tsv[s.TsvFile]), s => s.TsvFile);
    }

    static int Col(List<List<object?>> grid, string header) => grid[0].FindIndex(v => Equals(v, header));

    static List<object?> RowOf(List<List<object?>> grid, string id) => grid.Single(r => r.Count > 0 && Equals(r[Col(grid, "id")], id));

    [Fact]
    public void Push_to_a_blank_sheet_then_pull_reproduces_the_data_byte_for_byte()
    {
        var sheet = Pushed();
        Assert.Equal(["Sheet1", .. SheetSync.TabNames], sheet.Tabs.Select(t => t.Name));
        Assert.Empty(sheet["Sheet1"]);

        var update = SheetSync.Pull(sheet.Read(), RepoTables());
        Assert.Empty(update.Changes);
        var files = RepoFiles();
        foreach (var (name, text) in update.JsonFiles)
            Assert.Equal(files[name], text);
        Assert.Equal(RepoStrings(), SheetSync.StringsCsv(sheet.Read()));
    }

    [Fact]
    public void Numbers_and_booleans_go_to_the_sheet_as_values_and_text_as_text()
    {
        var units = Pushed()["units"];
        var grunt = RowOf(units, "goblin_grunt");
        Assert.IsType<double>(grunt[Col(units, "health")]);
        Assert.IsType<string>(grunt[Col(units, "name")]);
        Assert.Contains(Pushed()["stats"].Skip(1), r => r.Contains(true));

        var procs = Pushed()["procs"];                                        // free text that is a number: a number
        var flaming = RowOf(procs, "flaming");
        Assert.Equal("damage", flaming[Col(procs, "key_1")]);
        Assert.IsType<double>(flaming[Col(procs, "value_1")]);
    }

    [Fact]
    public void Pushing_again_changes_nothing()
    {
        var sheet = Pushed();
        Assert.Empty(SheetSync.Plan(sheet.Read(), RepoTables(), RepoStrings()));
    }

    [Fact]
    public void Cells_already_holding_the_value_are_left_alone()
    {
        // 95.0 for 95, "1" in a column whose default is 1, " 0.5" with a space: all the same value, so formulas
        // that produce them survive a push.
        var sheet = Pushed();
        var procs = sheet["procs"];
        foreach (var row in procs.Skip(1))
        {
            while (row.Count <= Col(procs, "chance")) row.Add(null);
            row[Col(procs, "chance")] ??= 1.0;
        }
        var units = sheet["units"];
        var cell = RowOf(units, "goblin_grunt");
        cell[Col(units, "health")] = $" {cell[Col(units, "health")]}";
        Assert.Empty(SheetSync.Plan(sheet.Read(), RepoTables(), RepoStrings()));
    }

    [Fact]
    public void Push_keeps_helper_columns_and_the_sheets_row_order()
    {
        var sheet = Pushed();
        var units = sheet["units"];
        // The user sorts the tab and adds two helper columns: one in the middle, one at the end.
        units.Reverse(1, units.Count - 1);
        var middle = Col(units, "health");
        foreach (var r in units) r.Insert(middle, r == units[0] ? "_mid" : "mid " + r[0]);
        var end = units.Max(r => r.Count);
        foreach (var r in units)
        {
            while (r.Count < end) r.Add(null);
            r.Add(r == units[0] ? "_check" : "=" + r[0]);
        }
        var order = units.Skip(1).Select(r => (string)r[0]!).ToList();

        // Change one value, add a unit after goblin_grunt, remove the last unit of the data.
        var last = RepoTables()["units"].Rows[^1].Str("id");
        var tables = Edited("units", tsv =>
        {
            var lines = tsv.TrimEnd('\n').Split('\n').ToList();
            var grunt = lines.FindIndex(l => l.StartsWith("goblin_grunt\t"));
            var health = Array.IndexOf(lines[0].Split('\t'), "health");
            var cells = lines[grunt].Split('\t');
            cells[health] = "123";
            lines[grunt] = string.Join('\t', cells);
            lines.Insert(grunt + 1, "goblin_twin" + lines[grunt][lines[grunt].IndexOf('\t')..]);
            lines.RemoveAll(l => l.StartsWith(last + "\t"));
            return string.Join('\n', lines) + "\n";
        });
        var ops = SheetSync.Plan(sheet.Read(), tables, RepoStrings());
        sheet.Apply(ops);

        Assert.Single(ops, o => o.Op == "deleteRows");
        Assert.Equal(123.0, RowOf(units, "goblin_grunt")[Col(units, "health")]);
        var gruntRow = units.IndexOf(RowOf(units, "goblin_grunt")) + 1;
        Assert.Single(ops, o => o.Op == "write" && o.Row == gruntRow);   // the one changed cell, nothing else
        static object? At(List<object?> row, int col) => col < row.Count ? row[col] : null;
        foreach (var r in units.Skip(1))
        {
            var isNew = Equals(r[0], "goblin_twin");   // a new row starts with empty helper cells
            Assert.Equal(isNew ? null : "=" + r[0], At(r, Col(units, "_check")));
            Assert.Equal(isNew ? null : "mid " + r[0], At(r, Col(units, "_mid")));
        }
        var expected = order.Where(id => id != last).ToList();
        expected.Insert(expected.IndexOf("goblin_grunt") + 1, "goblin_twin");
        Assert.Equal(expected, units.Skip(1).Select(r => (string)r[0]!));
    }

    [Fact]
    public void Push_adds_a_missing_column_before_the_helper_columns_and_drops_unknown_ones()
    {
        var sheet = Pushed();
        var tags = sheet["tags"];
        var note = Col(tags, "note");
        foreach (var r in tags) { while (r.Count <= note) r.Add(null); r.RemoveAt(note); }
        foreach (var r in tags) r.Add(r == tags[0] ? "_calc" : "x");
        foreach (var r in tags) r.Add(r == tags[0] ? "old_column" : "y");

        sheet.Apply(SheetSync.Plan(sheet.Read(), RepoTables(), RepoStrings()));
        Assert.Equal(["id", "name", "group", "implies", "note", "_calc"], tags[0].Take(6));
        Assert.DoesNotContain("old_column", tags[0]);
        Assert.Empty(SheetSync.Pull(sheet.Read(), RepoTables()).Changes);
    }

    [Fact]
    public void Strings_sync_too()
    {
        var sheet = Pushed();
        var strings = sheet["strings"];
        var victory = strings.Single(r => Equals(r[0], "log.victory"));
        victory[1] = "We won, \"barely\".";
        strings.RemoveAt(strings.FindIndex(r => Equals(r[0], "log.defeat")));

        var csv = SheetSync.StringsCsv(sheet.Read())!;
        Assert.Contains("log.victory,\"We won, \"\"barely\"\".\"\n", csv);
        Assert.Equal(["- strings: log.defeat removed", "strings log.victory: en \"Victory: the party wins.\" → \"We won, \"barely\".\""],
            SheetSync.StringChanges(RepoStrings(), csv));

        // Pushing the repo's text puts the row back and the text back.
        sheet.Apply(SheetSync.Plan(sheet.Read(), RepoTables(), RepoStrings()));
        Assert.Equal(RepoStrings(), SheetSync.StringsCsv(sheet.Read()));
    }

    [Fact]
    public void Edits_lists_what_the_sheet_changed()
    {
        var sheet = Pushed();
        var units = sheet["units"];
        RowOf(units, "goblin_grunt")[Col(units, "health")] = 95.0;
        sheet["strings"].Single(r => Equals(r[0], "log.victory"))[1] = "Yay";
        sheet.Tabs.RemoveAll(t => t.Name == "tags");   // a missing tab counts as unchanged

        var health = RepoTables()["units"].Rows.Single(r => r.Str("id") == "goblin_grunt").Num("health");
        Assert.Equal([$"Goblin Grunt: health {health} → 95", "strings log.victory: en \"Victory: the party wins.\" → \"Yay\""],
            SheetSync.Edits(sheet.Read(), RepoTables(), RepoStrings()));
        Assert.Equal([$"Goblin Grunt: health 95 → {health}", "strings log.victory: en \"Yay\" → \"Victory: the party wins.\""],
            SheetSync.PushChanges(sheet.Read(), RepoTables(), RepoStrings()));
    }

    [Fact]
    public void Pull_errors_name_the_tab_row_and_column()
    {
        var sheet = Pushed();
        var units = sheet["units"];
        var row = units.IndexOf(RowOf(units, "goblin_grunt"));
        units[row][Col(units, "name")] = "Goblin\nGrunt";
        var e = Assert.Throws<DataException>(() => SheetSync.Pull(sheet.Read(), RepoTables()));
        Assert.Equal($"units tab row {row + 1}, name: text can't contain tabs or line breaks", e.Message);

        units[row][Col(units, "name")] = "Goblin Grunt";
        units[row][Col(units, "health")] = "lots";
        Assert.Contains("units.tsv", Assert.Throws<DataException>(() => SheetSync.Pull(sheet.Read(), RepoTables())).Message);

        sheet.Tabs.RemoveAll(t => t.Name == "units");
        Assert.Contains("has no \"units\" tab", Assert.Throws<DataException>(() => SheetSync.Pull(sheet.Read(), RepoTables())).Message);
    }

    [Fact]
    public void Formula_rounding_noise_is_dropped()
    {
        Assert.Equal("0.3", SheetSync.Text(0.1 + 0.2));
        Assert.Equal("95", SheetSync.Text(95.0));
        Assert.Equal("TRUE", SheetSync.Text(true));
    }

    [Fact]
    public void Strings_csv_writes_back_byte_for_byte()
    {
        var csv = RepoStrings();
        Assert.Equal(csv, Strings.WriteCsv(Strings.ReadCsv(csv)));
    }
}
