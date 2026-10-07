using EternalDungeon.Core.Assets;
using Godot;

namespace EternalDungeon.Game;

/// <summary>
/// Loads styled art by id: <c>assets/styles/{style}/{id}.png</c> (M2 brief §9). Every style has the same ids at the same
/// sizes (the list is <see cref="ArtCatalog"/>, written out by <c>sim art-requests</c>). Missing art returns null and
/// the caller draws a generated placeholder, so the game runs with whatever art exists.
/// </summary>
public sealed class Art(Settings settings)
{
    readonly Dictionary<string, Texture2D?> cache = [];

    public const string StylesFolder = "res://assets/styles";

    /// <summary>The style folders present, by name.</summary>
    public static List<string> Styles()
    {
        var dir = DirAccess.Open(StylesFolder);
        return dir is null ? [] : [.. dir.GetDirectories().Order()];
    }

    /// <summary>The image for <paramref name="id"/> in the current style, or null.</summary>
    public Texture2D? Get(string id)
    {
        var key = settings.Style + "/" + id;
        if (cache.TryGetValue(key, out var hit)) return hit;
        return cache[key] = settings.Style.Length == 0 ? null : Load($"{StylesFolder}/{settings.Style}/{id}.png");
    }

    /// <summary>A unit's portrait for <paramref name="state"/>, falling back to its default portrait.</summary>
    public Texture2D? Portrait(string unitId, string state = ArtCatalog.DefaultState) =>
        (state == ArtCatalog.DefaultState ? null : Get(ArtCatalog.PortraitId(unitId, state))) ?? Get(ArtCatalog.PortraitId(unitId));

    static Texture2D? Load(string path)
    {
        // Imported art (opened once in the editor) loads as a resource; fresh files load straight from disk.
        if (ResourceLoader.Exists(path)) return ResourceLoader.Load<Texture2D>(path);
        var file = ProjectSettings.GlobalizePath(path);
        if (!System.IO.File.Exists(file)) return null;
        var image = Image.LoadFromFile(file);
        return image is null ? null : ImageTexture.CreateFromImage(image);
    }

    public void Clear() => cache.Clear();
}
