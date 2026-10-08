using EternalDungeon.Core.Data;
using Godot;

namespace EternalDungeon.Game;

public enum BattleSpeed { Slow, Normal, Fast, Instant }

/// <summary>Player and debug settings for this run (saving them comes with the rest of saving, M3).</summary>
public sealed class Settings
{
    public BattleSpeed Speed { get; set; } = BattleSpeed.Normal;

    /// <summary>Heroes act on the simulator's scripted AI.</summary>
    public bool AutoBattle { get; set; }

    /// <summary>Text size: 1 is the design size at 1280×800.</summary>
    public float TextScale { get; set; } = 1;

    public static readonly float[] TextScales = [0.85f, 1f, 1.15f, 1.3f];

    /// <summary>The candidate art style: a folder under assets/styles/ (M2 brief §9).</summary>
    public string Style { get; set; } = "";

    /// <summary>Seconds one enemy action takes to show (brief: about 0.5 s at 1×; 0.5× added 2026-10-08).</summary>
    public float ActionSeconds => Speed switch
    {
        BattleSpeed.Slow => 1f,
        BattleSpeed.Normal => 0.5f,
        BattleSpeed.Fast => 0.25f,
        _ => 0f,
    };

    public static string SpeedKey(BattleSpeed s) => "ui.speed_" + JsonField.SnakeCase(s.ToString());

    public BattleSpeed NextSpeed() => Speed = (BattleSpeed)(((int)Speed + 1) % Enum.GetValues<BattleSpeed>().Length);

    /// <summary>Where saved replays go.</summary>
    public static string ReplayFolder => ProjectSettings.GlobalizePath("user://replays");
}
