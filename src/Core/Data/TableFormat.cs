using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EternalDungeon.Core.Data;

/// <summary>
/// Reads and writes tables as canonical JSON (the source of truth) and as TSV (for spreadsheets). The JSON writer is
/// the only way data files are written: one row per line, columns in schema order, empty and default values left
/// out, numbers in their shortest exact form. So export then import reproduces a file byte for byte.
/// <para>TSV conventions: a header row of column names; numbers with a <c>.</c> decimal point; booleans
/// <c>TRUE</c>/<c>FALSE</c>; enums by name; id lists as <c>a, b, c</c>; an empty cell means the default. On
/// import, columns starting with <c>_</c> are ignored, so calculations can sit beside the data.</para>
/// </summary>
public static class TableFormat
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static readonly JsonSerializerOptions StringOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // ---- JSON ----

    public static Table ReadJson(TableSchema schema, string text, string? file = null)
    {
        file ??= schema.JsonFile;
        var rows = new List<Row>();
        var i = 0;
        foreach (var item in JsonField.Parse(file, text).Items())
        {
            item.OnlyFields([.. schema.Columns.Select(c => c.Name)]);
            var values = new Dictionary<string, object?>();
            foreach (var column in schema.Columns)
            {
                if (item.Optional(column.Name) is not { } f)
                {
                    if (column.Required) throw new DataException(file, $"[{i}].{column.Name}", "is required");
                    continue;
                }
                values[column.Name] = Normalize(column, column.Kind switch
                {
                    ColumnKind.Id => f.Id(),
                    ColumnKind.Text => Text(f.String(), m => f.Error(m)),
                    ColumnKind.Int => f.Int(),
                    ColumnKind.Number => f.Number(),
                    ColumnKind.Bool => f.Bool(),
                    ColumnKind.Enum => EnumName(column, f.String(), m => f.Error(m)),
                    _ => f.Items().Select(x => x.Id()).ToArray(),
                });
            }
            rows.Add(new Row(schema, file, i++, tsv: false, values));
        }
        return Checked(new Table(schema, file, rows));
    }

    public static string WriteJson(Table table)
    {
        if (table.Rows.Count == 0) return "[]\n";
        var sb = new StringBuilder("[\n");
        for (var i = 0; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            var fields = table.Schema.Columns
                .Where(c => row.Values.GetValueOrDefault(c.Name) is not null)
                .Select(c => $"{Quote(c.Name)}: {JsonValue(c, row.Values[c.Name]!)}");
            sb.Append("  { ").Append(string.Join(", ", fields)).Append(i < table.Rows.Count - 1 ? " },\n" : " }\n");
        }
        return sb.Append("]\n").ToString();
    }

    static string JsonValue(Column c, object value) => c.Kind switch
    {
        ColumnKind.Int => ((int)value).ToString(Inv),
        ColumnKind.Number => Number((double)value),
        ColumnKind.Bool => (bool)value ? "true" : "false",
        ColumnKind.IdList => "[" + string.Join(", ", ((string[])value).Select(Quote)) + "]",
        _ => Quote((string)value),
    };

    static string Quote(string s) => JsonSerializer.Serialize(s, StringOptions);

    // ---- TSV ----

    public static string WriteTsv(Table table)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join('\t', table.Schema.Columns.Select(c => c.Name))).Append('\n');
        foreach (var row in table.Rows)
            sb.Append(string.Join('\t', table.Schema.Columns.Select(c => Cell(c, row.Values.GetValueOrDefault(c.Name))))).Append('\n');
        return sb.ToString();
    }

    /// <summary>A value as a TSV cell (or as it reads in a change summary).</summary>
    public static string Cell(Column c, object? value) => value switch
    {
        null => "",
        bool b => b ? "TRUE" : "FALSE",
        double d => Number(d),
        int n => n.ToString(Inv),
        string[] list => string.Join(", ", list),
        _ => (string)value,
    };

    public static Table ReadTsv(TableSchema schema, string text, string? file = null)
    {
        file ??= schema.TsvFile;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var header = lines[0].Split('\t');
        var columns = new Column?[header.Length];
        for (var i = 0; i < header.Length; i++)
        {
            var name = header[i].Trim();
            if (name.StartsWith('_')) continue;
            columns[i] = schema.Find(name) ?? throw new DataException(file, $"row 1, {name}",
                $"unknown column (allowed: {string.Join(", ", schema.Columns.Select(c => c.Name))}; start a column with _ to keep notes)");
        }
        if (schema.Columns.FirstOrDefault(c => c.Required && !columns.Contains(c)) is { } missing)
            throw new DataException(file, "row 1", $"the required column \"{missing.Name}\" is missing");

        var rows = new List<Row>();
        for (var line = 1; line < lines.Length; line++)
        {
            var cells = lines[line].Split('\t');
            if (cells.All(c => c.Trim().Length == 0)) continue;
            var values = new Dictionary<string, object?>();
            var index = line - 1;
            DataException Fail(string column, string problem) => new(file, $"row {index + 2}, {column}", problem);
            for (var i = 0; i < columns.Length; i++)
            {
                if (columns[i] is not { } column) continue;
                var cell = i < cells.Length ? cells[i].Trim() : "";
                if (cell.Length == 0)
                {
                    if (column.Required) throw Fail(column.Name, "is required");
                    continue;
                }
                values[column.Name] = Normalize(column, ParseCell(column, cell, m => Fail(column.Name, m)));
            }
            rows.Add(new Row(schema, file, index, tsv: true, values));
        }
        return Checked(new Table(schema, file, rows));
    }

    static object ParseCell(Column c, string cell, Func<string, DataException> fail)
    {
        switch (c.Kind)
        {
            case ColumnKind.Id:
                return JsonField.IsId(cell) ? cell : throw fail($"\"{cell}\" is not a valid id ({JsonField.IdRule})");
            case ColumnKind.Text:
                return cell;
            case ColumnKind.Int:
                return int.TryParse(cell, NumberStyles.AllowLeadingSign, Inv, out var n) ? n : throw fail($"expected a whole number, got \"{cell}\"");
            case ColumnKind.Number:
                if (double.TryParse(cell, NumberStyles.Float, Inv, out var d)) return d;
                throw fail(cell.Contains(',') ? $"expected a number with a . decimal point, got \"{cell}\"" : $"expected a number, got \"{cell}\"");
            case ColumnKind.Bool:
                if (cell.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return true;
                if (cell.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return false;
                throw fail($"expected TRUE or FALSE, got \"{cell}\"");
            case ColumnKind.Enum:
                return EnumName(c, cell, fail);
            default:
                var ids = cell.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
                if (ids.FirstOrDefault(s => !JsonField.IsId(s)) is { } bad)
                    throw fail($"\"{bad}\" is not a valid id ({JsonField.IdRule})");
                return ids;
        }
    }

    // ---- Shared ----

    /// <summary>Empty text, empty lists and values equal to the column's default become empty, so every file
    /// has one canonical form.</summary>
    static object? Normalize(Column c, object value) => value switch
    {
        string s when s.Length == 0 => null,
        string[] { Length: 0 } => null,
        _ when Equals(value, c.Default) => null,
        _ => value,
    };

    static string Text(string s, Func<string, DataException> fail) =>
        s.Contains('\t') || s.Contains('\n') || s.Contains('\r') ? throw fail("text can't contain tabs or line breaks") : s;

    static string EnumName(Column c, string text, Func<string, DataException> fail) =>
        c.EnumNames.Contains(text) ? text : throw fail($"expected one of {string.Join(", ", c.EnumNames)}, got \"{text}\"");

    static string Number(double d) => d.ToString("R", Inv);

    /// <summary>Fails on two rows with the same key, naming the later one.</summary>
    static Table Checked(Table table)
    {
        var seen = new HashSet<string>();
        foreach (var row in table.Rows)
            if (!seen.Add(row.KeyText))
            {
                var first = table.Schema.Key[0];
                throw row.Error(first, table.Schema.Key.Count == 1
                    ? $"duplicate {first} \"{row.KeyText}\""
                    : $"duplicate row ({string.Join(", ", table.Schema.Key)}: {row.KeyText})");
            }
        return table;
    }
}
