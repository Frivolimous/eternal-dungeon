using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using Godot;

namespace EternalDungeon.Game;

/// <summary>
/// Screenshot mode (M2 brief §9): load an encounter and seed (or a replay), play a number of actions at instant speed
/// (heroes on auto-battle unless a replay drives them), save a PNG of the screen and quit. Command line, after
/// Godot's own arguments and a <c>--</c>:
/// <code>--screenshot out.png --encounter goblin_patrol [--seed 42] [--actions 6] [--layout side_on] [--style ink] [--replay f.replay.json] [--select-hero] [--keys 1,Tab,Enter] [--save-replay f] [--save-log f]</code>
/// <c>--select-hero</c> stops at the next hero turn and shows an action's target preview; <c>--keys</c> instead presses
/// those keys at that turn (to check keyboard play) and shoots once the screen is waiting again.
/// It needs a real window: Godot's headless mode doesn't render.
/// </summary>
public sealed record ScreenshotJob(string Path, string Encounter, ulong Seed, int Actions, BoardLayout? Layout, string? Style,
    Replay? Replay, bool StopAtHero, string[] Keys, string? SaveReplay = null, string? SaveLog = null)
{
    public static ScreenshotJob? FromCommandLine(string[] args)
    {
        var values = new Dictionary<string, string>();
        for (var i = 0; i < args.Length; i++)
            if (args[i].StartsWith("--"))
                values[args[i][2..]] = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "";
        if (!values.TryGetValue("screenshot", out var path)) return null;

        Replay? replay = values.TryGetValue("replay", out var r) ? Replay.Parse(System.IO.File.ReadAllText(r), r) : null;
        var encounter = replay?.Encounter ?? values.GetValueOrDefault("encounter", "goblin_patrol");
        var seed = replay?.Seed ?? (ulong.TryParse(values.GetValueOrDefault("seed"), out var s) ? s : 1);
        var actions = int.TryParse(values.GetValueOrDefault("actions"), out var n) ? n : 0;
        BoardLayout? layout = values.GetValueOrDefault("layout") switch
        {
            "side_on" => BoardLayout.SideOn,
            "vertical" => BoardLayout.Vertical,
            _ => null,
        };
        return new ScreenshotJob(System.IO.Path.GetFullPath(path), encounter, seed, actions, layout, values.GetValueOrDefault("style"), replay,
            values.ContainsKey("select-hero") || values.ContainsKey("keys"),
            values.TryGetValue("keys", out var k) ? k.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [],
            values.GetValueOrDefault("save-replay"), values.GetValueOrDefault("save-log"));
    }
}
