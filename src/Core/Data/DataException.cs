namespace EternalDungeon.Core.Data;

/// <summary>
/// Invalid content in a data file. The message always names the file, and the field when there is one,
/// e.g. <c>stats.json [4].combine: expected one of add, dim, mult, got "sum"</c>.
/// </summary>
public sealed class DataException(string file, string field, string problem)
    : Exception(field.Length == 0 ? $"{file}: {problem}" : $"{file} {field}: {problem}")
{
    public string File { get; } = file;
    public string Field { get; } = field;
    public string Problem { get; } = problem;
}
