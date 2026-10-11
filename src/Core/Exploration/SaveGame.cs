using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Exploration;

/// <summary>
/// One action a run took (<see cref="DungeonRun.Commands"/>): what (<see cref="Do"/>) and its arguments. A finished
/// battle is one command holding every hero decision made in it.
/// </summary>
public sealed record RunCommand(
    string Do,
    string? Node = null,
    string? Choice = null,
    string? Hero = null,
    string? Item = null,
    string? Skill = null,
    InteractableKind? Interactable = null,
    int? Amount = null,
    IReadOnlyList<Choice>? Battle = null);

/// <summary>A save can't be restored: it doesn't parse, or it's out of step with the data (made with other data).</summary>
public sealed class SaveException(string message) : Exception(message);

/// <summary>
/// The one save slot (Anchor: Dungeons › Saving; M3A brief §9). Runs are deterministic, so a save is the dungeon, its
/// seed and every action taken, plus the hero decisions of a battle in progress (the replay format). Restoring replays
/// them: the same random generator position, the same enemy intents, the same Event block, the same battle turn. A save
/// also records the data's fingerprint; one made with other data can be told apart and a restart offered.
/// </summary>
public sealed record SaveGame(
    int Version,
    string Game,
    string Data,
    string Dungeon,
    ulong Seed,
    IReadOnlyList<RunCommand> Commands,
    IReadOnlyList<Choice>? Battle,
    bool AutoBattle = false,
    int Seconds = 0)
{
    // Seconds: time played, for the end screen (it plays no part in restoring).
    public const int CurrentVersion = 1;
    public const string FileName = "save.json";

    /// <summary>A save of <paramref name="run"/> as it stands, including the decisions of a battle in progress.</summary>
    public static SaveGame Of(DungeonRun run) =>
        new(CurrentVersion, GameInfo.Version, run.Data.Fingerprint, run.Dungeon.Id, run.Seed, [.. run.Commands],
            run.Battle is { } b ? [.. b.Session.Choices] : null, run.Battle?.Session.AutoBattle ?? false);

    /// <summary>The save was made with different data (or a different game version's data).</summary>
    public bool DataChanged(GameData data) => Data != data.Fingerprint;

    /// <summary>
    /// Replays the save into a new run. A battle in progress comes back at the same hero decision. Throws
    /// <see cref="SaveException"/> when the save doesn't fit the data.
    /// </summary>
    public DungeonRun Restore(GameData data)
    {
        if (Version > CurrentVersion) throw new SaveException($"The save is from a newer version (save format {Version})");
        if (!data.Dungeons.ContainsKey(Dungeon)) throw new SaveException($"Unknown dungeon \"{Dungeon}\"");
        var run = new DungeonRun(data, Dungeon, Seed);
        for (var i = 0; i < Commands.Count; i++)
        {
            try
            {
                Apply(run, Commands[i]);
            }
            catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or ArgumentException)
            {
                throw new SaveException($"The save is out of step with the data at action {i + 1} ({Commands[i].Do}): {e.Message}");
            }
        }
        if (Battle is not null)
        {
            if (run.Battle is not { } fight) throw new SaveException("The save has a battle in progress, but the run isn't in one");
            fight.Session.Script(Battle);
            try { fight.Session.Advance(); }
            catch (InvalidOperationException e) { throw new SaveException($"The saved battle is out of step: {e.Message}"); }
            fight.Session.AutoBattle = AutoBattle;
        }
        return run;
    }

    /// <summary>Applies one saved action to a run (restoring, and tests that continue a restored run).</summary>
    internal static void Apply(DungeonRun run, RunCommand c)
    {
        string Need(string? value, string what) => value ?? throw new SaveException($"\"{c.Do}\" needs a {what}");
        switch (c.Do)
        {
            case "start": run.Start(); break;
            case "debug_explore": run.DebugExplore(Need(c.Node, "node")); break;
            case "debug_gold": run.DebugSetGold(c.Amount ?? 0); break;
            case "debug_xp": run.DebugXp(c.Amount ?? 0); break;
            case "debug_restore": run.DebugRestore(); break;
            case "debug_reveal": run.DebugReveal(); break;
            case "explore": run.Explore(Need(c.Node, "node")); break;
            case "resume": run.Resume(Need(c.Node, "node")); break;
            case "camp": run.Camp(); break;
            case "use": run.UseInteractable(Need(c.Node, "node"), c.Interactable ?? throw new SaveException("\"use\" needs an interactable")); break;
            case "buy": run.Buy(Need(c.Node, "node"), Need(c.Item, "item")); break;
            case "use_item": run.UseItem(Need(c.Hero, "hero"), Need(c.Item, "item")); break;
            case "to_belt": run.MoveToBelt(Need(c.Hero, "hero"), Need(c.Item, "item")); break;
            case "to_pack": run.MoveToPack(Need(c.Hero, "hero"), Need(c.Item, "item")); break;
            case "spend": run.SpendPoint(Need(c.Hero, "hero"), Need(c.Skill, "skill")); break;
            case "choose": run.Choose(Need(c.Choice, "choice")); break;
            case "continue": run.Continue(); break;
            case "battle":
                var session = run.Battle?.Session ?? throw new InvalidOperationException("no battle to finish");
                session.Script(c.Battle ?? []);
                session.Advance();
                if (!session.Over) throw new InvalidOperationException("the battle didn't end where it did before");
                run.FinishBattle();
                break;
            default:
                throw new SaveException($"Unknown action \"{c.Do}\"");
        }
    }

    // ---- JSON: one action per line, so a save reads and diffs easily ----

    static readonly JsonSerializerOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    static string S(string value) => JsonSerializer.Serialize(value, Options);

    public string ToJson()
    {
        var sb = new StringBuilder();
        sb.Append($"{{ \"version\": {Version}, \"game\": {S(Game)}, \"data\": {S(Data)}, \"dungeon\": {S(Dungeon)}, \"seed\": {Seed},");
        sb.Append(AutoBattle ? " \"auto_battle\": true," : "");
        sb.Append(Seconds > 0 ? $" \"seconds\": {Seconds}," : "");
        sb.Append("\n\"commands\": [");
        for (var i = 0; i < Commands.Count; i++)
            sb.Append(i == 0 ? "\n  " : ",\n  ").Append(CommandJson(Commands[i]));
        sb.Append(Commands.Count > 0 ? "\n]" : "]");
        if (Battle is not null)
        {
            sb.Append(",\n\"battle\": [");
            for (var i = 0; i < Battle.Count; i++)
                sb.Append(i == 0 ? "\n  " : ",\n  ").Append(Replay.ChoiceJson(Battle[i]));
            sb.Append(Battle.Count > 0 ? "\n]" : "]");
        }
        return sb.Append(" }\n").ToString();
    }

    static string CommandJson(RunCommand c)
    {
        var fields = new List<string> { $"\"do\": {S(c.Do)}" };
        if (c.Node is not null) fields.Add($"\"node\": {S(c.Node)}");
        if (c.Choice is not null) fields.Add($"\"choice\": {S(c.Choice)}");
        if (c.Hero is not null) fields.Add($"\"hero\": {S(c.Hero)}");
        if (c.Item is not null) fields.Add($"\"item\": {S(c.Item)}");
        if (c.Skill is not null) fields.Add($"\"skill\": {S(c.Skill)}");
        if (c.Interactable is { } k) fields.Add($"\"interactable\": {S(JsonField.SnakeCase(k.ToString()))}");
        if (c.Amount is { } a) fields.Add($"\"amount\": {a}");
        if (c.Battle is not null) fields.Add($"\"battle\": [{string.Join(", ", c.Battle.Select(Replay.ChoiceJson))}]");
        return "{ " + string.Join(", ", fields) + " }";
    }

    public static SaveGame Parse(string text, string file = FileName)
    {
        try
        {
            var root = JsonField.Parse(file, text);
            root.OnlyFields("version", "game", "data", "dungeon", "seed", "auto_battle", "seconds", "commands", "battle");
            if (!root["seed"].Element.TryGetUInt64(out var seed)) throw root["seed"].Error("expected a whole number");
            var commands = root["commands"].Items().Select(c =>
            {
                c.OnlyFields("do", "node", "choice", "hero", "item", "skill", "interactable", "amount", "battle");
                return new RunCommand(c["do"].String(), c.Optional("node")?.String(), c.Optional("choice")?.String(),
                    c.Optional("hero")?.String(), c.Optional("item")?.String(), c.Optional("skill")?.String(),
                    c.Optional("interactable")?.Enum<InteractableKind>(), c.Optional("amount")?.Int(),
                    c.Optional("battle")?.Items().Select(Replay.ParseChoice).ToList());
            }).ToList();
            return new SaveGame(root["version"].Int(), root["game"].String(), root["data"].String(), root["dungeon"].String(),
                seed, commands, root.Optional("battle")?.Items().Select(Replay.ParseChoice).ToList(),
                root.Optional("auto_battle")?.Bool() ?? false, root.Optional("seconds")?.Int() ?? 0);
        }
        catch (DataException e)
        {
            throw new SaveException($"The save can't be read: {e.Message}");
        }
    }
}
