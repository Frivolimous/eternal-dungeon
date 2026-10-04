using EternalDungeon.Core.Stats;

namespace EternalDungeon.Core.Data;

/// <summary>
/// Reads and validates every data file. Any problem throws a <see cref="DataException"/> naming the
/// file and field; a successful load means the content is internally consistent.
/// </summary>
public static class DataLoader
{
    public const string TagsFile = "tags.json";
    public const string StatsFile = "stats.json";
    public const string CompoundStatsFile = "compound_stats.json";
    public const string UnitsFile = "units.json";

    public static GameData LoadDirectory(string directory) => Load(DataSource.FromDirectory(directory));

    public static GameData Load(DataSource source)
    {
        var tags = ReadList(source, TagsFile, ReadTag).ToDictionary(t => t.Id);
        var stats = ReadList(source, StatsFile, ReadStat).ToDictionary(s => s.Id);
        var compounds = ReadList(source, CompoundStatsFile, f => ReadCompound(f, tags, stats));
        if (compounds.FirstOrDefault(c => stats.ContainsKey(c.Id)) is { } clash)
            throw new DataException(CompoundStatsFile, "", $"\"{clash.Id}\" is both a stat and a compound stat");
        var compoundIds = compounds.Select(c => c.Id).ToHashSet();
        var units = ReadList(source, UnitsFile, f => ReadUnit(f, tags, stats, compoundIds));
        return new GameData([.. tags.Values], [.. stats.Values], compounds, units);
    }

    static UnitDef ReadUnit(JsonField f, Dictionary<string, TagDef> tags, Dictionary<string, StatDef> stats, HashSet<string> compounds)
    {
        f.OnlyFields("id", "name", "size", "stats", "tagStats", "compounds");

        var size = f["size"];
        var unitSize = size.Number() switch
        {
            1 => UnitSize.Small,
            1.5 => UnitSize.Tall,
            2 => UnitSize.Large,
            var n => throw size.Error($"expected 1, 1.5 or 2, got {n}"),
        };

        var values = new List<StatValue>();
        foreach (var prop in f["stats"].Properties())
        {
            if (!stats.TryGetValue(prop.Name, out var def))
                throw prop.Value.Error($"unknown stat \"{prop.Name}\" (not in {StatsFile})");
            values.Add(new StatValue(def.Id, null, ReadStatValue(prop.Value, def)));
        }
        foreach (var t in f.Optional("tagStats")?.Items() ?? [])
        {
            t.OnlyFields("tag", "stat", "value");
            var tag = t["tag"].Id();
            if (!tags.ContainsKey(tag))
                throw t["tag"].Error($"unknown tag \"{tag}\" (not in {TagsFile})");
            var stat = t["stat"].Id();
            if (!stats.TryGetValue(stat, out var def))
                throw t["stat"].Error($"unknown stat \"{stat}\" (not in {StatsFile})");
            if (!def.TagKeyed)
                throw t["stat"].Error($"{def.Name} is a character stat and can't be keyed to a tag");
            values.Add(new StatValue(stat, tag, ReadStatValue(t["value"], def)));
        }

        var compoundValues = new Dictionary<string, double>();
        foreach (var prop in f.Optional("compounds")?.Properties() ?? [])
        {
            if (!compounds.Contains(prop.Name))
                throw prop.Value.Error($"unknown compound stat \"{prop.Name}\" (not in {CompoundStatsFile})");
            compoundValues[prop.Name] = prop.Value.Number();
        }

        var unit = new UnitDef(f["id"].Id(), f["name"].String(), unitSize, values, compoundValues);
        if (!values.Any(v => v.Stat == "health" && v.Value > 0))
            throw f["stats"].Error("a unit needs health above 0");
        return unit;
    }

    static double ReadStatValue(JsonField f, StatDef def)
    {
        var value = f.Number();
        try { Combine.Validate(def, value); }
        catch (ArgumentException e) { throw f.Error(e.Message); }
        return value;
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

    static CompoundStatDef ReadCompound(JsonField f, Dictionary<string, TagDef> tags, Dictionary<string, StatDef> stats)
    {
        f.OnlyFields("id", "name", "rows");
        var rows = new List<CompoundRow>();
        foreach (var r in f["rows"].Items())
        {
            r.OnlyFields("tag", "stat", "coef");
            var tag = r["tag"].Id();
            if (!tags.ContainsKey(tag))
                throw r["tag"].Error($"unknown tag \"{tag}\" (not in {TagsFile})");
            var stat = r["stat"].Id();
            if (!stats.TryGetValue(stat, out var def))
                throw r["stat"].Error($"unknown stat \"{stat}\" (not in {StatsFile})");
            if (!def.TagKeyed)
                throw r["stat"].Error($"{def.Name} is a character stat and can't be keyed to a tag");
            if (def.Combine == CombineMode.Mult)
                throw r["stat"].Error($"{def.Name} combines by mult; compound stats can only feed add and dim stats");
            rows.Add(new CompoundRow(tag, stat, r["coef"].Number()));
        }
        if (rows.Count == 0)
            throw f["rows"].Error("needs at least one row");
        return new CompoundStatDef(f["id"].Id(), f["name"].String(), rows);
    }
}
