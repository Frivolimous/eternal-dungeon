using System.Text;

namespace EternalDungeon.Core.Data;

/// <summary>
/// Every piece of text the game shows, by key (M2 brief §8), from data/strings.csv in Godot's CSV translation format:
/// a <c>keys</c> column, then one column per language (<c>en</c>). The combat log formats its lines from these, and the
/// game registers the same table with Godot's translation system, so the simulator and the game print identical text.
/// Placeholders are named: <c>{actor} → {action}</c>. A missing key shows as <c>[key]</c>, so it's easy to spot.
/// Content names (units, actions, effects) stay in the data tables.
/// </summary>
public sealed class Strings
{
    public const string FileName = "strings.csv";
    public const string DefaultLanguage = "en";

    readonly Dictionary<string, string> texts;

    public string Language { get; }

    Strings(Dictionary<string, string> texts, string language)
    {
        this.texts = texts;
        Language = language;
    }

    public IReadOnlyDictionary<string, string> All => texts;

    public bool Has(string key) => texts.ContainsKey(key);

    /// <summary>The text for <paramref name="key"/>, or <c>[key]</c> when it's missing.</summary>
    public string this[string key] => texts.TryGetValue(key, out var t) ? t : $"[{key}]";

    /// <summary>The text for <paramref name="key"/> with each <c>{name}</c> replaced by its value.</summary>
    public string Format(string key, params (string Name, object? Value)[] args)
    {
        var text = this[key];
        foreach (var (name, value) in args)
            text = text.Replace("{" + name + "}", Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
        return text;
    }

    public static Strings Load(DataSource source, string language = DefaultLanguage) =>
        Parse(source.Read(FileName) ?? throw new DataException(FileName, "", "file is missing"), language);

    /// <summary>Reads Godot's CSV translation format: commas between fields, quotes around fields that hold commas,
    /// quotes (doubled) or line breaks.</summary>
    public static Strings Parse(string csv, string language = DefaultLanguage)
    {
        var rows = Rows(csv).ToList();
        if (rows.Count == 0 || rows[0].Count == 0 || rows[0][0] != "keys")
            throw new DataException(FileName, "line 1", "the first column must be \"keys\"");
        var col = rows[0].IndexOf(language);
        if (col < 1) throw new DataException(FileName, "line 1", $"no \"{language}\" column");
        var texts = new Dictionary<string, string>();
        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(c => c.Length == 0)) continue;
            var key = row[0];
            if (key.Length == 0) throw new DataException(FileName, $"row {i + 1}", "the key is empty");
            if (!texts.TryAdd(key, col < row.Count ? row[col] : ""))
                throw new DataException(FileName, $"row {i + 1}", $"duplicate key \"{key}\"");
        }
        return new Strings(texts, language);
    }

    static IEnumerable<List<string>> Rows(string csv)
    {
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < csv.Length && csv[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                row.Add(field.ToString());
                field.Clear();
                yield return row;
                row = [];
            }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            yield return row;
        }
    }
}
