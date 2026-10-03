namespace EternalDungeon.Core.Data;

/// <summary>
/// Reads and validates every data file. Any problem throws a <see cref="DataException"/> naming the
/// file and field; a successful load means the content is internally consistent.
/// </summary>
public static class DataLoader
{
    public const string TagsFile = "tags.json";
    public const string StatsFile = "stats.json";

    public static GameData LoadDirectory(string directory) => Load(DataSource.FromDirectory(directory));

    public static GameData Load(DataSource source)
    {
        var tags = ReadList(source, TagsFile, ReadTag);
        var stats = ReadList(source, StatsFile, ReadStat);
        return new GameData(tags, stats);
    }

    /// <summary>Reads a file whose root is an array of entries with unique <c>id</c>s.</summary>
    static List<T> ReadList<T>(DataSource source, string file, Func<JsonField, T> read)
    {
        var text = source.Read(file) ?? throw new DataException(file, "", "file is missing");
        var seen = new HashSet<string>();
        var list = new List<T>();
        foreach (var item in JsonField.Parse(file, text).Items())
        {
            var id = item["id"];
            if (!seen.Add(id.Id()))
                throw id.Error($"duplicate id \"{id.String()}\"");
            list.Add(read(item));
        }
        return list;
    }

    static TagDef ReadTag(JsonField f)
    {
        f.OnlyFields("id", "name", "group");
        return new TagDef(f["id"].Id(), f["name"].String(), f["group"].Enum<TagGroup>());
    }

    static StatDef ReadStat(JsonField f)
    {
        f.OnlyFields("id", "name", "group", "combine", "integer", "hidden");
        var stat = new StatDef(
            f["id"].Id(),
            f["name"].String(),
            f["group"].Enum<StatGroup>(),
            f["combine"].Enum<CombineMode>(),
            f.Optional("integer")?.Bool() ?? false,
            f.Optional("hidden")?.Bool() ?? false);
        if (stat.Integer && stat.Combine != CombineMode.Add)
            throw f["integer"].Error("only stats that combine by add can be integers");
        return stat;
    }
}
