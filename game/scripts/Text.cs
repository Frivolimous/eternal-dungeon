using EternalDungeon.Core.Data;

namespace EternalDungeon.Game;

/// <summary>
/// Every word on screen comes from data/strings.csv through here (or through Godot's Tr, which reads the same table).
/// <c>Text.T("ui.start")</c>; <c>Text.F("ui.seed_value", ("seed", 42))</c>.
/// </summary>
public static class Text
{
    static Strings strings = Strings.Parse("keys,en\n");

    public static void Use(Strings s) => strings = s;

    public static string T(string key) => strings[key];

    public static string F(string key, params (string Name, object? Value)[] args) => strings.Format(key, args);

    public static Strings Strings => strings;

    /// <summary>A Core reason code ("rooted", "out_of_reach") in words.</summary>
    public static string Reason(string code) => strings["reason." + code];
}
