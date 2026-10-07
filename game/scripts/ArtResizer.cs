using EternalDungeon.Core.Assets;
using EternalDungeon.Core.Data;
using Godot;

namespace EternalDungeon.Game;

/// <summary>
/// Turns master art (any size, e.g. 1024×1024) into a style folder at the exact sizes in <see cref="ArtCatalog"/>:
/// each image is cropped from the centre to the right shape, resized with Lanczos, and saved as PNG in
/// <c>assets/styles/&lt;style&gt;/</c>. Files are matched by name: <c>portrait_warrior.png</c> → <c>portrait_warrior</c>.
/// Run headless: <c>godot --headless --path game -- --resize-art &lt;masters folder&gt; &lt;style&gt;</c>.
/// </summary>
public static class ArtResizer
{
    static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".webp"];

    public static void Run(GameData data, string source, string style)
    {
        if (!JsonField.IsId(style))
        {
            GD.PrintErr($"The style name \"{style}\" must be {JsonField.IdRule}.");
            return;
        }
        source = System.IO.Path.GetFullPath(source);
        if (!System.IO.Directory.Exists(source))
        {
            GD.PrintErr($"No folder {source}");
            return;
        }
        var catalog = ArtCatalog.All(data).ToDictionary(r => r.Id);
        var target = ProjectSettings.GlobalizePath($"{Art.StylesFolder}/{style}");
        System.IO.Directory.CreateDirectory(target);

        var written = new HashSet<string>();
        foreach (var file in System.IO.Directory.GetFiles(source).Order())
        {
            var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
            if (!Extensions.Contains(ext)) continue;
            var id = System.IO.Path.GetFileNameWithoutExtension(file);
            if (!catalog.TryGetValue(id, out var request))
            {
                GD.Print($"  skip   {System.IO.Path.GetFileName(file)}: not an id in docs/art-requests.md");
                continue;
            }
            var image = Image.LoadFromFile(file);
            if (image is null || image.IsEmpty())
            {
                GD.PrintErr($"  error  {System.IO.Path.GetFileName(file)}: can't read the image");
                continue;
            }
            var note = Fit(image, request.Width, request.Height);
            image.SavePng(System.IO.Path.Combine(target, id + ".png"));
            written.Add(id);
            GD.Print($"  wrote  {id}.png {request.Width}×{request.Height}{note}");
        }

        var missing = catalog.Values.Where(r => !r.Optional && !written.Contains(r.Id)
                                                && !System.IO.File.Exists(System.IO.Path.Combine(target, r.Id + ".png"))).ToList();
        GD.Print($"Style \"{style}\": {written.Count} image(s) written to {target}");
        foreach (var r in missing)
            GD.Print($"  still needed: {r.Id} ({r.Description})");
        GD.Print($"If it's new, list the style in game/assets/manifest.json under \"styles\" (aiPlaceholder: true for AI art), then run sim assets.");
    }

    /// <summary>Crops <paramref name="image"/> from the centre to the target's shape, then resizes it. Returns a note
    /// on what was done (cropping, or enlarging a small source).</summary>
    static string Fit(Image image, int width, int height)
    {
        var notes = new List<string>();
        var w = image.GetWidth();
        var h = image.GetHeight();
        var targetAspect = (double)width / height;
        var aspect = (double)w / h;
        if (Math.Abs(aspect - targetAspect) > 0.01)
        {
            var (cw, ch) = aspect > targetAspect ? ((int)Math.Round(h * targetAspect), h) : (w, (int)Math.Round(w / targetAspect));
            var cropped = image.GetRegion(new Rect2I((w - cw) / 2, (h - ch) / 2, cw, ch));
            image.CopyFrom(cropped);
            notes.Add($"cropped {w}×{h} to {cw}×{ch} from the centre");
            (w, h) = (cw, ch);
        }
        if (w < width || h < height) notes.Add($"enlarged from {w}×{h}: the master is smaller than the target");
        image.Resize(width, height, Image.Interpolation.Lanczos);
        return notes.Count == 0 ? "" : $" ({string.Join("; ", notes)})";
    }
}
