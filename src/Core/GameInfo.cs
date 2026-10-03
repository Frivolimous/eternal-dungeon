using System.Reflection;

namespace EternalDungeon.Core;

public static class GameInfo
{
    public const string Title = "Eternal Dungeon";

    /// <summary>The version from Directory.Build.props, without any build metadata.</summary>
    public static string Version { get; } =
        typeof(GameInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0] ?? "0.0.0";
}
