namespace EternalDungeon.Core.Data;

/// <summary>What a column holds. Every value is a single cell, so any table maps to a spreadsheet tab.</summary>
public enum ColumnKind
{
    /// <summary>A lowercase snake_case id.</summary>
    Id,
    /// <summary>Free text on one line.</summary>
    Text,
    Int,
    Number,
    Bool,
    /// <summary>One of an enum's values, in snake_case.</summary>
    Enum,
    /// <summary>A list of ids in one cell: a JSON array, or "a, b, c" in a TSV.</summary>
    IdList,
}

/// <summary>
/// One column of a data table. An empty cell (or a field left out of the JSON) means <see cref="Default"/>; a
/// <see cref="Required"/> column must always have a value.
/// </summary>
public sealed record Column(string Name, ColumnKind Kind, bool Required = false, object? Default = null, Type? EnumType = null)
{
    public static Column Id(string name, bool required = true) => new(name, ColumnKind.Id, required);
    public static Column Text(string name, bool required = false) => new(name, ColumnKind.Text, required);
    public static Column Int(string name, bool required = false, int? @default = null) => new(name, ColumnKind.Int, required, @default);
    public static Column Number(string name, bool required = false, double? @default = null) => new(name, ColumnKind.Number, required, @default);
    public static Column Bool(string name) => new(name, ColumnKind.Bool, false, false);
    public static Column List(string name) => new(name, ColumnKind.IdList);

    public static Column Enum<T>(string name, bool required = false, T? @default = null) where T : struct, System.Enum =>
        new(name, ColumnKind.Enum, required, @default is { } d ? JsonField.SnakeCase(d.ToString()) : null, typeof(T));

    /// <summary>The snake_case names an Enum column accepts.</summary>
    public IEnumerable<string> EnumNames => System.Enum.GetNames(EnumType!).Select(JsonField.SnakeCase);
}

/// <summary>
/// A flat table: its file name (without extension), its columns in order, and the columns that identify a row.
/// A child table names its <see cref="Parent"/> table and the column that points at the parent's id.
/// </summary>
public sealed record TableSchema(string Name, IReadOnlyList<Column> Columns, IReadOnlyList<string> Key,
    string? Parent = null, string? ParentColumn = null)
{
    public string JsonFile => Name + ".json";
    public string TsvFile => Name + ".tsv";

    public Column? Find(string name) => Columns.FirstOrDefault(c => c.Name == name);

    /// <summary>Child rows are kept grouped under their parent, in <c>order</c> when the table has that column.</summary>
    public bool Ordered => Find("order") is not null;
}

/// <summary>One row: its values by column (null where the cell is empty), and where it came from, for errors.</summary>
public sealed class Row(TableSchema schema, string file, int index, bool tsv, Dictionary<string, object?> values)
{
    public TableSchema Schema { get; } = schema;
    public int Index { get; } = index;
    public IReadOnlyDictionary<string, object?> Values => values;

    /// <summary>The row's place in its file: <c>[3]</c> in JSON, <c>row 5</c> (the sheet's row, after the header)
    /// in a TSV.</summary>
    public string Where => tsv ? $"row {Index + 2}" : $"[{Index}]";

    public DataException Error(string problem) => new(file, Where, problem);

    public DataException Error(string column, string problem) => new(file, tsv ? $"{Where}, {column}" : $"{Where}.{column}", problem);

    /// <summary>The cell's value, or the column's default when it's empty.</summary>
    public object? this[string column] => values.GetValueOrDefault(column) ?? Schema.Find(column)!.Default;

    /// <summary>Whether the cell has a value (not empty, not the default).</summary>
    public bool Has(string column) => values.GetValueOrDefault(column) is not null;

    public string Str(string column) => (string)this[column]!;
    public string? OptStr(string column) => (string?)this[column];
    public int Int(string column) => (int)(this[column] ?? 0);
    public int? OptInt(string column) => (int?)this[column];
    public double Num(string column) => (double)(this[column] ?? 0.0);
    public double? OptNum(string column) => (double?)this[column];
    public bool Bool(string column) => (bool)(this[column] ?? false);
    public IReadOnlyList<string> List(string column) => (string[]?)this[column] ?? [];

    public T Enum<T>(string column) where T : struct, System.Enum =>
        OptEnum<T>(column) ?? throw Error(column, "is required");

    public T? OptEnum<T>(string column) where T : struct, System.Enum =>
        this[column] is string s ? System.Enum.GetValues<T>().First(v => JsonField.SnakeCase(v.ToString()) == s) : null;

    /// <summary>The values that identify this row, joined: "warrior", or "warrior/power/fire".</summary>
    public string KeyText => string.Join("/", Schema.Key.Select(k => TableFormat.Cell(Schema.Find(k)!, this[k])));
}

/// <summary>A table's rows as read from one file.</summary>
public sealed class Table(TableSchema schema, string file, IReadOnlyList<Row> rows)
{
    public TableSchema Schema { get; } = schema;
    public string File { get; } = file;
    public IReadOnlyList<Row> Rows { get; } = rows;
}
