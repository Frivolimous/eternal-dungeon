using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

/// <summary>
/// A battle as a small file: the encounter, the seed and every hero decision. Battles are deterministic, so playing
/// the choices back reproduces the battle exactly, in the game or in <c>sim replay</c>.
/// </summary>
public sealed record Replay(string Encounter, ulong Seed, IReadOnlyList<Choice> Choices)
{
    public const string Extension = ".replay.json";

    /// <summary>Plays the choices back. The session stops where they run out: at the end of the battle, or with a
    /// hero waiting for a decision (a replay saved mid-battle).</summary>
    public BattleSession Play(GameData data)
    {
        if (!data.Encounters.TryGetValue(Encounter, out var encounter))
            throw new DataException("replay", "encounter", $"unknown encounter \"{Encounter}\"");
        var session = new BattleSession(data, encounter, Seed, Choices);
        session.Advance();
        return session;
    }

    static readonly JsonSerializerOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>One choice per line, so a replay reads (and diffs) easily.</summary>
    public string ToJson()
    {
        var sb = new StringBuilder();
        sb.Append($"{{ \"encounter\": {JsonSerializer.Serialize(Encounter, Options)}, \"seed\": {Seed}, \"choices\": [");
        for (var i = 0; i < Choices.Count; i++)
        {
            var c = Choices[i];
            var fields = new List<string> { $"\"unit\": {JsonSerializer.Serialize(c.Unit, Options)}", $"\"action\": {JsonSerializer.Serialize(c.Action, Options)}" };
            if (c.Target is not null) fields.Add($"\"target\": {JsonSerializer.Serialize(c.Target, Options)}");
            if (c.Tile is { } t) fields.Add($"\"tile\": [{t.Area}, {t.Row}, {t.Col}]");
            if (c.Auto) fields.Add("\"auto\": true");
            sb.Append(i == 0 ? "\n" : ",\n").Append("  { ").Append(string.Join(", ", fields)).Append(" }");
        }
        return sb.Append(Choices.Count > 0 ? "\n] }\n" : "] }\n").ToString();
    }

    public static Replay Parse(string text, string file = "replay")
    {
        var root = JsonField.Parse(file, text);
        root.OnlyFields("encounter", "seed", "choices");
        var seed = root["seed"];
        if (!seed.Element.TryGetUInt64(out var s)) throw seed.Error("expected a whole number");
        var choices = new List<Choice>();
        foreach (var c in root["choices"].Items())
        {
            c.OnlyFields("unit", "action", "target", "tile", "auto");
            Tile? tile = null;
            if (c.Optional("tile") is { } t)
            {
                var xyz = t.Items().Select(x => x.Int()).ToArray();
                if (xyz.Length != 3) throw t.Error("expected [area, row, col]");
                tile = new Tile(xyz[0], xyz[1], xyz[2]);
            }
            choices.Add(new Choice(c["unit"].String(), c["action"].String(), c.Optional("target")?.String(), tile,
                c.Optional("auto")?.Bool() ?? false));
        }
        return new Replay(root["encounter"].Id(), s, choices);
    }
}
