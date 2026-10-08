using System.Globalization;

namespace EternalDungeon.Core.Data;

/// <summary>One tab of the Google Sheet as the sync reads it: rows of cells, header first. A cell is a string, a
/// double, a bool or null (empty).</summary>
public sealed record SheetTab(string Name, IReadOnlyList<IReadOnlyList<object?>> Cells);

/// <summary>
/// One edit a push asks the sheet to make. Edits apply in order and count rows and columns from 1: <c>addTab</c>,
/// <c>insertRowsAfter</c> (Row, Count), <c>deleteRows</c> (Row, Count), <c>insertColumnsAfter</c> (Col, Count),
/// <c>deleteColumns</c> (Col, Count) and <c>write</c> (Values, from Row and Col). Text is sent with a leading
/// <c>'</c>, so Sheets keeps it as text instead of reading "1/2" as a date.
/// </summary>
public sealed record SheetOp(string Op, string Tab, int Row = 0, int Col = 0, int Count = 0, object?[][]? Values = null);

/// <summary>
/// The Google Sheets sync: one tab per data table, named like the table, plus a <c>strings</c> tab for strings.csv.
/// tools/sheets-sync.gs is the sheet's side. Pull turns the tabs into TSVs and runs the normal TSV import, so it
/// validates the same way. Push plans the fewest edits that make the tabs match the data: rows are matched by key
/// and keep their place in the sheet, cells that already hold the right value (a formula's result included) are
/// left alone, and columns starting with <c>_</c> or with no header keep everything, formulas included.
/// </summary>
public static class SheetSync
{
    public const string StringsTab = "strings";

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>The tabs the sync owns, in the order a push creates them.</summary>
    public static IEnumerable<string> TabNames => Schemas.Names.Append(StringsTab);

    /// <summary>A cell as TSV text. Numbers are rounded to 15 significant digits, so a formula's 0.30000000000000004
    /// reads as 0.3.</summary>
    public static string Text(object? cell) => cell switch
    {
        null => "",
        bool b => b ? "TRUE" : "FALSE",
        double d => double.Parse(d.ToString("G15", Inv), Inv).ToString("R", Inv),
        _ => Convert.ToString(cell, Inv) ?? "",
    };

    static bool IsDataColumn(string header) => header.Length > 0 && !header.StartsWith('_');

    static string Header(SheetTab tab, int col) => tab.Cells.Count > 0 && col < tab.Cells[0].Count ? Text(tab.Cells[0][col]).Trim() : "";

    static string At(IReadOnlyList<object?> row, int col) => col < row.Count ? Text(row[col]) : "";

    /// <summary>Whether no tab the sync owns has anything in it yet.</summary>
    public static bool IsBlank(IReadOnlyList<SheetTab> sheet) =>
        sheet.Where(t => TabNames.Contains(t.Name)).All(t => t.Cells.All(r => r.All(c => Text(c).Trim().Length == 0)));

    // ---- Pull ----

    /// <summary>A tab's data columns as a TSV (columns starting with _ or with no header are left out).</summary>
    public static string ToTsv(SheetTab tab)
    {
        var width = tab.Cells.Count == 0 ? 0 : tab.Cells.Max(r => r.Count);
        var cols = Enumerable.Range(0, width).Where(c => IsDataColumn(Header(tab, c))).ToList();
        var lines = new List<string>();
        for (var r = 0; r < tab.Cells.Count; r++)
            lines.Add(string.Join('\t', cols.Select(c =>
            {
                var text = r == 0 ? Header(tab, c) : At(tab.Cells[r], c);
                return text.IndexOfAny(['\t', '\n', '\r']) < 0 ? text
                    : throw new DataException($"{tab.Name} tab", $"row {r + 1}, {Header(tab, c)}", "text can't contain tabs or line breaks");
            })));
        return string.Join('\n', lines) + "\n";
    }

    /// <summary>The tables in the sheet, validated, with the JSON files they make and what changed against
    /// <paramref name="current"/>. A missing tab is an error, unless <paramref name="fallback"/> gives the table to
    /// use instead.</summary>
    public static DataUpdate Pull(IReadOnlyList<SheetTab> sheet, DataTables current, DataTables? fallback = null)
    {
        var files = new Dictionary<string, string>();
        var exported = fallback is null ? null : DataExchange.ExportTsv(fallback);
        foreach (var name in Schemas.Names)
            if (sheet.FirstOrDefault(t => t.Name == name) is { } tab) files[name + ".tsv"] = ToTsv(tab);
            else if (exported is not null) files[name + ".tsv"] = exported[name + ".tsv"];
            else throw new DataException("the sheet", "", $"has no \"{name}\" tab (push-sheets creates the tabs)");
        return DataExchange.ImportTsv(DataSource.FromFiles(files), current);
    }

    /// <summary>The strings tab as strings.csv, validated.</summary>
    public static string StringsCsv(SheetTab tab)
    {
        var width = tab.Cells.Count == 0 ? 0 : tab.Cells.Max(r => r.Count);
        var cols = Enumerable.Range(0, width).Where(c => IsDataColumn(Header(tab, c))).ToList();
        var rows = tab.Cells.Select((row, r) => (IReadOnlyList<string>)cols.Select(c => r == 0 ? Header(tab, c) : At(row, c)).ToList())
            .Where((row, r) => r == 0 || row.Any(f => f.Length > 0));
        var csv = Strings.WriteCsv(rows);
        try { Strings.Parse(csv); }
        catch (DataException e) { throw new DataException($"{StringsTab} tab", e.Field, e.Problem); }
        return csv;
    }

    /// <summary>The strings tab as strings.csv, or <paramref name="fallback"/> when the sheet has no strings tab
    /// (or null without a fallback).</summary>
    public static string? StringsCsv(IReadOnlyList<SheetTab> sheet, string? fallback = null) =>
        sheet.FirstOrDefault(t => t.Name == StringsTab) is { } tab ? StringsCsv(tab) : fallback;

    /// <summary>Readable lines for every added, removed and changed text: "log.victory: en "A" → "B"".</summary>
    public static List<string> StringChanges(string before, string after)
    {
        static (List<string> Header, Dictionary<string, List<string>> Rows) Read(string csv)
        {
            var rows = Strings.ReadCsv(csv).ToList();
            var keyed = new Dictionary<string, List<string>>();
            foreach (var row in rows.Skip(1).Where(r => r.Any(f => f.Length > 0)))
                keyed.TryAdd(row[0], row);
            return (rows.FirstOrDefault() ?? [], keyed);
        }
        var (oldHeader, old) = Read(before);
        var (newHeader, now) = Read(after);
        var lines = new List<string>();
        foreach (var language in newHeader.Skip(1).Except(oldHeader))
            lines.Add($"+ strings: language {language} added");
        foreach (var language in oldHeader.Skip(1).Except(newHeader))
            lines.Add($"- strings: language {language} removed");
        foreach (var key in now.Keys.Where(k => !old.ContainsKey(k)))
            lines.Add($"+ strings: {key} added");
        foreach (var key in old.Keys.Where(k => !now.ContainsKey(k)))
            lines.Add($"- strings: {key} removed");
        foreach (var (key, row) in now.Where(kv => old.ContainsKey(kv.Key)))
            foreach (var language in newHeader.Skip(1).Intersect(oldHeader))
            {
                string Get(List<string> header, List<string> r) => header.IndexOf(language) is var i && i < r.Count ? r[i] : "";
                var a = Get(oldHeader, old[key]);
                var b = Get(newHeader, row);
                if (a != b) lines.Add($"strings {key}: {language} \"{a}\" → \"{b}\"");
            }
        return lines;
    }

    /// <summary>What the sheet changes against the data in <paramref name="tables"/> and
    /// <paramref name="strings"/>: the lines a pull would print. A tab the sheet doesn't have counts as unchanged.
    /// Throws <see cref="DataException"/> when the sheet's data is invalid.</summary>
    public static List<string> Edits(IReadOnlyList<SheetTab> sheet, DataTables tables, string strings) =>
        [.. Pull(sheet, tables, fallback: tables).Changes, .. StringChanges(strings, StringsCsv(sheet, strings)!)];

    /// <summary>What a push of <paramref name="tables"/> and <paramref name="strings"/> changes on the sheet: the
    /// other direction from <see cref="Edits"/>. Throws <see cref="DataException"/> when the sheet's data is
    /// invalid.</summary>
    public static List<string> PushChanges(IReadOnlyList<SheetTab> sheet, DataTables tables, string strings)
    {
        var onSheet = DataTables.FromJson(DataSource.FromFiles(Pull(sheet, tables, fallback: tables).JsonFiles));
        return [.. DataExchange.Changes(onSheet, tables), .. StringChanges(StringsCsv(sheet, strings)!, strings)];
    }

    // ---- Push ----

    /// <summary>What one tab should hold: its header, rows of typed cells (double, bool, string or null) and the
    /// columns that identify a row.</summary>
    sealed record Target(string Name, IReadOnlyList<string> Header, IReadOnlyList<object?[]> Rows, IReadOnlyList<string> Key,
        Func<string, Column?> Column);

    /// <summary>The edits that make the sheet's tabs match <paramref name="tables"/> and the strings.csv text
    /// <paramref name="strings"/>. Tabs the sync doesn't own are never touched.</summary>
    public static List<SheetOp> Plan(IReadOnlyList<SheetTab> sheet, DataTables tables, string strings)
    {
        var ops = new List<SheetOp>();
        foreach (var table in tables.All)
            PlanTab(ops, sheet.FirstOrDefault(t => t.Name == table.Schema.Name), TableTarget(table));
        PlanTab(ops, sheet.FirstOrDefault(t => t.Name == StringsTab), StringsTarget(strings));
        return ops;
    }

    static Target TableTarget(Table table)
    {
        var columns = table.Schema.Columns;
        var rows = table.Rows.Select(row => columns.Select(c => Typed(c, row.Values.GetValueOrDefault(c.Name))).ToArray()).ToList();
        return new Target(table.Schema.Name, [.. columns.Select(c => c.Name)], rows, table.Schema.Key, table.Schema.Find);
    }

    static object? Typed(Column c, object? value) => value switch
    {
        null => null,
        int n => (double)n,
        double or bool => value,
        // Free text that is a number (a proc's value_1 "5") goes as a number, so formulas can use it.
        string s when c.Kind == ColumnKind.Text && double.TryParse(s, NumberStyles.Float, Inv, out var d) && Text(d) == s => d,
        _ => TableFormat.Cell(c, value),
    };

    static Target StringsTarget(string csv)
    {
        var rows = Strings.ReadCsv(csv).Where(r => r.Any(f => f.Length > 0)).ToList();
        var header = rows[0];
        return new Target(StringsTab, header, [.. rows.Skip(1).Select(r => header.Select((_, i) => (object?)(i < r.Count ? r[i] : "")).ToArray())],
            [header[0]], _ => null);
    }

    static object? Send(object? value) => value is string s && s.Length > 0 ? "'" + s : value ?? "";

    static SheetOp Write(string tab, int row, int col, object?[][] values) =>
        new("write", tab, row, col, Values: [.. values.Select(r => r.Select(Send).ToArray())]);

    /// <summary>Whether the sheet's cell already holds the value: the same text, or the same once both are in
    /// canonical form for the column.</summary>
    static bool Same(Column? column, object? have, object? want)
    {
        var a = Text(have);
        var b = Text(want);
        return a == b || column is not null && TableFormat.Canonical(column, a) is { } x && x == TableFormat.Canonical(column, b);
    }

    static void PlanTab(List<SheetOp> ops, SheetTab? tab, Target target)
    {
        var name = target.Name;
        if (tab is null) ops.Add(new("addTab", name));
        var grid = (tab?.Cells ?? []).Select(r => r.ToList()).ToList();
        if (grid.All(r => r.All(c => Text(c).Trim().Length == 0)))
        {
            ops.Add(Write(name, 1, 1, [[.. target.Header], .. target.Rows]));
            return;
        }

        var width = grid.Max(r => r.Count);
        foreach (var r in grid)
            while (r.Count < width) r.Add(null);
        string HeaderAt(int c) => Text(grid[0][c]).Trim();

        // Columns: drop data columns the table doesn't have (and repeats), add missing ones after the last data column.
        var seen = new HashSet<string>();
        var drop = Enumerable.Range(0, width).Where(c => IsDataColumn(HeaderAt(c)) && (!target.Header.Contains(HeaderAt(c)) || !seen.Add(HeaderAt(c)))).ToList();
        foreach (var c in Enumerable.Reverse(drop))
        {
            ops.Add(new("deleteColumns", name, Col: c + 1, Count: 1));
            foreach (var r in grid) r.RemoveAt(c);
        }
        width -= drop.Count;
        var pos = new Dictionary<string, int>();
        for (var c = 0; c < width; c++)
            if (IsDataColumn(HeaderAt(c))) pos[HeaderAt(c)] = c;
        var missing = target.Header.Where(h => !pos.ContainsKey(h)).ToList();
        if (missing.Count > 0)
        {
            var after = pos.Count > 0 ? pos.Values.Max() + 1 : width;
            if (after < width)
            {
                ops.Add(new("insertColumnsAfter", name, Col: after, Count: missing.Count));
                foreach (var r in grid) r.InsertRange(after, Enumerable.Repeat<object?>(null, missing.Count));
            }
            else
                foreach (var r in grid) r.AddRange(Enumerable.Repeat<object?>(null, after + missing.Count - r.Count));
            for (var i = 0; i < missing.Count; i++) pos[missing[i]] = after + i;
        }

        // Rows: match by key, delete the sheet's rows the table no longer has, insert new rows after the row before them.
        bool IsDataRow(List<object?> row) => pos.Values.Any(c => Text(row[c]).Trim().Length > 0);
        string Canon(string column, string text) =>
            target.Column(column) is { } c ? TableFormat.Canonical(c, text) ?? text.Trim() : text;
        string KeyOf(Func<string, string> cell) => string.Join("\t", target.Key.Select(k => Canon(k, cell(k))));

        var byKey = new Dictionary<string, List<object?>>();
        foreach (var row in grid.Skip(1).Where(IsDataRow))
            byKey.TryAdd(KeyOf(k => Text(row[pos[k]])), row);
        var header = target.Header.ToList();
        var placed = target.Rows.Select(row => byKey.Remove(KeyOf(k => Text(row[header.IndexOf(k)])), out var r) ? r : null).ToList();
        var kept = placed.OfType<List<object?>>().ToHashSet();

        for (var r = grid.Count - 1; r >= 1; r--)
        {
            if (!IsDataRow(grid[r]) || kept.Contains(grid[r])) continue;
            var count = 1;
            while (r - 1 >= 1 && IsDataRow(grid[r - 1]) && !kept.Contains(grid[r - 1])) { r--; count++; }
            ops.Add(new("deleteRows", name, Row: r + 1, Count: count));
            grid.RemoveRange(r, count);
        }

        List<object?>? anchor = null;
        for (var i = 0; i < placed.Count; i++)
        {
            if (placed[i] is { } row) { anchor = row; continue; }
            var at = anchor is null ? Math.Max(1, grid.FindIndex(1, IsDataRow)) : grid.IndexOf(anchor) + 1;
            if (at < grid.Count) ops.Add(new("insertRowsAfter", name, Row: at, Count: 1));
            var blank = Enumerable.Repeat<object?>(null, width + missing.Count).ToList();
            grid.Insert(at, blank);
            placed[i] = anchor = blank;
        }

        // Cells: write only what differs, one write per run of neighbouring cells.
        void WriteRow(int r, List<object?> row, IReadOnlyList<object?> values)
        {
            var changed = header.Select((h, i) => (Col: pos[h], Value: values[i], Column: target.Column(h)))
                .Where(x => !Same(x.Column, row[x.Col], x.Value)).OrderBy(x => x.Col).ToList();
            for (var i = 0; i < changed.Count;)
            {
                var run = 1;
                while (i + run < changed.Count && changed[i + run].Col == changed[i].Col + run) run++;
                ops.Add(Write(name, r + 1, changed[i].Col + 1, [[.. changed.Skip(i).Take(run).Select(x => x.Value)]]));
                i += run;
            }
        }
        WriteRow(0, grid[0], header.Cast<object?>().ToList());
        for (var i = 0; i < placed.Count; i++)
            WriteRow(grid.IndexOf(placed[i]!), placed[i]!, target.Rows[i]);
    }
}
