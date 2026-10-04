namespace EternalDungeon.Core.Data;

/// <summary>The JSON files an import or format would write, and what changed in them.</summary>
public sealed record DataUpdate(IReadOnlyDictionary<string, string> JsonFiles, IReadOnlyList<string> Changes);

/// <summary>
/// Moves data between the JSON files (the source of truth) and TSVs (one per table, for spreadsheets). Import runs the
/// full validation first, so invalid data never reaches the JSON files.
/// </summary>
public static class DataExchange
{
    /// <summary>One TSV per table, by file name.</summary>
    public static Dictionary<string, string> ExportTsv(DataTables tables) =>
        tables.All.ToDictionary(t => t.Schema.TsvFile, TableFormat.WriteTsv);

    /// <summary>Reads the TSVs in <paramref name="source"/>, validates them, and returns the JSON files they make
    /// with a change summary against <paramref name="current"/>. Throws <see cref="DataException"/> if anything is
    /// invalid. Files that aren't tables are ignored.</summary>
    public static DataUpdate ImportTsv(DataSource source, DataTables current)
    {
        var imported = DataTables.Read(s => source.Read(s.TsvFile) is { } text ? TableFormat.ReadTsv(s, text) : null, s => s.TsvFile);
        DataLoader.Build(imported);
        return new DataUpdate(JsonFiles(imported), Changes(current, imported));
    }

    /// <summary>The canonical JSON files for <paramref name="tables"/>: child rows grouped under their parent, in
    /// order.</summary>
    public static Dictionary<string, string> JsonFiles(DataTables tables) =>
        tables.All.ToDictionary(t => t.Schema.JsonFile, t => TableFormat.WriteJson(Canonical(t, tables)));

    static Table Canonical(Table table, DataTables tables)
    {
        if (table.Schema.Parent is not { } parent) return table;
        var position = tables[parent].Rows.Select((r, i) => (r.Str("id"), i)).ToDictionary(x => x.Item1, x => x.i);
        var rows = table.Rows.OrderBy(r => position.GetValueOrDefault(r.Str(table.Schema.ParentColumn!), int.MaxValue));
        if (table.Schema.Find("side") is not null) rows = rows.ThenBy(r => r.Str("side") == "party" ? 0 : 1);
        if (table.Schema.Ordered) rows = rows.ThenBy(r => r.Int("order"));
        return new Table(table.Schema, table.File, [.. rows]);
    }

    /// <summary>Readable lines for every added, removed and changed row: "Goblin Grunt: health 85 → 95".</summary>
    public static List<string> Changes(DataTables before, DataTables after)
    {
        var lines = new List<string>();
        foreach (var name in Schemas.Names)
        {
            var old = before[name].Rows.ToDictionary(r => r.KeyText);
            var now = after[name].Rows.ToDictionary(r => r.KeyText);
            string Label(Row r) => r.Schema.Find("name") is not null ? $"{r.Str("name")}" : $"{name} {r.KeyText}";

            foreach (var (key, row) in now.Where(kv => !old.ContainsKey(kv.Key)))
                lines.Add($"+ {name}: {Label(row)} added");
            foreach (var (key, row) in old.Where(kv => !now.ContainsKey(kv.Key)))
                lines.Add($"- {name}: {Label(row)} removed");
            foreach (var (key, row) in now.Where(kv => old.ContainsKey(kv.Key)))
            {
                var was = old[key];
                var columns = after[name].Schema.Columns.Select(c => c.Name)
                    .Union(before[name].Schema.Columns.Select(c => c.Name));
                foreach (var column in columns)
                {
                    var a = Show(was, column);
                    var b = Show(row, column);
                    if (a != b) lines.Add($"{Label(row)}: {column} {a} → {b}");
                }
            }
        }
        return lines;
    }

    static string Show(Row r, string column) =>
        r.Schema.Find(column) is { } c && r.Values.GetValueOrDefault(column) is { } v ? TableFormat.Cell(c, v) : "(empty)";
}
