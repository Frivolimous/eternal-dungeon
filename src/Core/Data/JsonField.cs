using System.Text.Json;
using System.Text.RegularExpressions;

namespace EternalDungeon.Core.Data;

/// <summary>
/// A JSON value plus where it came from (file and field path), so every read can fail with a
/// <see cref="DataException"/> that points at the exact field. Reads are strict: wrong types,
/// missing required fields and unknown fields are all errors.
/// </summary>
public readonly partial struct JsonField
{
    public JsonElement Element { get; }
    public string File { get; }
    public string Path { get; }

    JsonField(JsonElement element, string file, string path)
    {
        Element = element;
        File = file;
        Path = path;
    }

    /// <summary>Parses strict JSON: no comments and no trailing commas (data files are written by one canonical
    /// writer, which would drop them).</summary>
    public static JsonField Parse(string file, string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return new JsonField(doc.RootElement.Clone(), file, "");
        }
        catch (JsonException e)
        {
            var where = e.LineNumber is long line ? $"line {line + 1}" : "";
            throw new DataException(file, where, $"not valid JSON ({e.Message})");
        }
    }

    public DataException Error(string problem) => new(File, Path, problem);

    string Child(string name) => Path.Length == 0 ? name : $"{Path}.{name}";

    // ---- Objects ----

    void ExpectKind(JsonValueKind kind, string what)
    {
        if (Element.ValueKind != kind)
            throw Error($"expected {what}, got {Describe(Element)}");
    }

    /// <summary>A required property of this object.</summary>
    public JsonField this[string name]
    {
        get
        {
            ExpectKind(JsonValueKind.Object, "an object");
            if (!Element.TryGetProperty(name, out var value))
                throw new DataException(File, Child(name), "is required");
            return new JsonField(value, File, Child(name));
        }
    }

    /// <summary>An optional property of this object, or null when absent (or JSON null).</summary>
    public JsonField? Optional(string name)
    {
        ExpectKind(JsonValueKind.Object, "an object");
        if (!Element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        return new JsonField(value, File, Child(name));
    }

    /// <summary>Fails on any property not in <paramref name="allowed"/>, so typos don't pass silently.</summary>
    public void OnlyFields(params string[] allowed)
    {
        ExpectKind(JsonValueKind.Object, "an object");
        foreach (var prop in Element.EnumerateObject())
            if (Array.IndexOf(allowed, prop.Name) < 0)
                throw new DataException(File, Child(prop.Name),
                    $"unknown field (allowed: {string.Join(", ", allowed)})");
    }

    /// <summary>Every property of this object, for maps such as <c>"stats": { "health": 100 }</c>.</summary>
    public IEnumerable<(string Name, JsonField Value)> Properties()
    {
        ExpectKind(JsonValueKind.Object, "an object");
        foreach (var prop in Element.EnumerateObject())
            yield return (prop.Name, new JsonField(prop.Value, File, Child(prop.Name)));
    }

    // ---- Arrays ----

    public IEnumerable<JsonField> Items()
    {
        ExpectKind(JsonValueKind.Array, "an array");
        var i = 0;
        foreach (var item in Element.EnumerateArray())
            yield return new JsonField(item, File, $"{Path}[{i++}]");
    }

    // ---- Scalars ----

    public string String()
    {
        ExpectKind(JsonValueKind.String, "a string");
        return Element.GetString()!;
    }

    public double Number()
    {
        ExpectKind(JsonValueKind.Number, "a number");
        return Element.GetDouble();
    }

    public int Int()
    {
        ExpectKind(JsonValueKind.Number, "a whole number");
        if (!Element.TryGetInt32(out var value))
            throw Error($"expected a whole number, got {Element.GetRawText()}");
        return value;
    }

    public bool Bool()
    {
        if (Element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw Error($"expected true or false, got {Describe(Element)}");
        return Element.GetBoolean();
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex IdPattern();

    /// <summary>Whether <paramref name="text"/> is a valid id: lowercase snake_case, starting with a letter.</summary>
    public static bool IsId(string text) => IdPattern().IsMatch(text);

    public const string IdRule = "lowercase letters, digits and underscores, starting with a letter";

    /// <summary>A lowercase snake_case id, such as <c>power_attack</c>.</summary>
    public string Id()
    {
        var id = String();
        if (!IdPattern().IsMatch(id))
            throw Error($"\"{id}\" is not a valid id ({IdRule})");
        return id;
    }

    /// <summary>An enum written in snake_case: <c>"damage_type"</c> reads as <c>DamageType</c>.</summary>
    public T Enum<T>() where T : struct, System.Enum
    {
        var text = String();
        foreach (var value in System.Enum.GetValues<T>())
            if (SnakeCase(value.ToString()) == text)
                return value;
        var allowed = string.Join(", ", System.Enum.GetValues<T>().Select(v => SnakeCase(v.ToString())));
        throw Error($"expected one of {allowed}, got \"{text}\"");
    }

    public static string SnakeCase(string pascal) =>
        string.Concat(pascal.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

    static string Describe(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => "an object",
        JsonValueKind.Array => "an array",
        JsonValueKind.String => $"\"{e.GetString()}\"",
        JsonValueKind.Null => "null",
        _ => e.GetRawText(),
    };
}
