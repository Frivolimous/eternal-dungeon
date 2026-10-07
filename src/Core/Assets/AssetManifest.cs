using System.Buffers.Binary;
using System.Text.RegularExpressions;
using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Assets;

/// <summary>One image the game loads, at a fixed path and size so final art drops in without code changes.</summary>
public sealed record AssetEntry(string Id, string Path, int Width, int Height, bool AiPlaceholder, string? Note);

/// <summary>A candidate art style: a folder of images with the ids and sizes in <see cref="ArtCatalog"/>.</summary>
public sealed record ArtStyle(string Id, string Name, bool AiPlaceholder, string? Note);

/// <summary>
/// game/assets/manifest.json: every fixed image (with its size and whether it's an AI placeholder), and the art
/// styles, each a folder under assets/styles/ (flagged as a whole). Anchor: Art pipeline. No flagged asset may remain
/// at release.
/// </summary>
public sealed partial class AssetManifest(IReadOnlyList<AssetEntry> entries, IReadOnlyList<ArtStyle>? styles = null)
{
    public const string FileName = "manifest.json";
    public const string StylesFolder = "styles";

    public IReadOnlyList<AssetEntry> Entries { get; } = entries;
    public IReadOnlyList<ArtStyle> Styles { get; } = styles ?? [];

    public IEnumerable<AssetEntry> AiPlaceholders => Entries.Where(e => e.AiPlaceholder);

    /// <summary>
    /// Problems with the style folders: folders not in the manifest (or the reverse), files that aren't in the art
    /// catalog, and wrong sizes. Missing images aren't problems (they fall back to placeholders); <paramref name="missing"/>
    /// counts them per style.
    /// </summary>
    public IReadOnlyList<string> CheckStyles(string assetsDirectory, IReadOnlyList<ArtRequest> catalog, Dictionary<string, int>? missing = null)
    {
        var problems = new List<string>();
        var root = System.IO.Path.Combine(assetsDirectory, StylesFolder);
        var folders = Directory.Exists(root) ? Directory.GetDirectories(root).Select(System.IO.Path.GetFileName).ToHashSet() : [];
        foreach (var folder in folders.Where(f => Styles.All(s => s.Id != f)))
            problems.Add($"styles/{folder}: not listed in {FileName}");
        var byId = catalog.ToDictionary(r => r.Id);
        foreach (var style in Styles)
        {
            var dir = System.IO.Path.Combine(root, style.Id);
            if (!Directory.Exists(dir))
            {
                problems.Add($"style {style.Id}: folder styles/{style.Id} not found");
                continue;
            }
            var present = new HashSet<string>();
            foreach (var file in Directory.GetFiles(dir).Where(f => !f.EndsWith(".import")))
            {
                var name = System.IO.Path.GetFileName(file);
                var id = System.IO.Path.GetFileNameWithoutExtension(file);
                if (!name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || !byId.TryGetValue(id, out var request))
                {
                    problems.Add($"styles/{style.Id}/{name}: not in the art catalog (see docs/art-requests.md)");
                    continue;
                }
                present.Add(id);
                if (ImageSize(file) is var (w, h) && (w != request.Width || h != request.Height))
                    problems.Add($"styles/{style.Id}/{name}: should be {request.Width}×{request.Height}, file is {w}×{h}");
            }
            if (missing is not null) missing[style.Id] = catalog.Count(r => !r.Optional && !present.Contains(r.Id));
        }
        return problems;
    }

    public static AssetManifest Parse(string text)
    {
        var root = JsonField.Parse(FileName, text);
        root.OnlyFields("assets", "styles");
        var seen = new HashSet<string>();
        var entries = new List<AssetEntry>();
        foreach (var f in root["assets"].Items())
        {
            f.OnlyFields("id", "path", "width", "height", "aiPlaceholder", "note");
            var entry = new AssetEntry(
                f["id"].Id(),
                f["path"].String(),
                f["width"].Int(),
                f["height"].Int(),
                f["aiPlaceholder"].Bool(),
                f.Optional("note")?.String());
            if (!seen.Add(entry.Id))
                throw f["id"].Error($"duplicate id \"{entry.Id}\"");
            if (entry.Width <= 0 || entry.Height <= 0)
                throw f["width"].Error("width and height must be positive");
            entries.Add(entry);
        }
        var styles = new List<ArtStyle>();
        foreach (var f in root.Optional("styles")?.Items() ?? [])
        {
            f.OnlyFields("id", "name", "aiPlaceholder", "note");
            var style = new ArtStyle(f["id"].Id(), f["name"].String(), f["aiPlaceholder"].Bool(), f.Optional("note")?.String());
            if (styles.Any(s => s.Id == style.Id)) throw f["id"].Error($"duplicate style \"{style.Id}\"");
            styles.Add(style);
        }
        return new AssetManifest(entries, styles);
    }

    public static AssetManifest Load(string assetsDirectory) =>
        Parse(File.ReadAllText(System.IO.Path.Combine(assetsDirectory, FileName)));

    /// <summary>Problems with the files on disk: missing files, and PNG/SVG sizes that don't match the manifest.</summary>
    public IReadOnlyList<string> CheckFiles(string assetsDirectory)
    {
        var problems = new List<string>();
        foreach (var e in Entries)
        {
            var path = System.IO.Path.Combine(assetsDirectory, e.Path);
            if (!File.Exists(path))
            {
                problems.Add($"{e.Id}: file not found ({e.Path})");
                continue;
            }
            var size = ImageSize(path);
            if (size is var (w, h) && (w != e.Width || h != e.Height))
                problems.Add($"{e.Id}: manifest says {e.Width}×{e.Height}, file is {w}×{h} ({e.Path})");
        }
        return problems;
    }

    /// <summary>Width and height of a PNG or SVG, or null for formats this check doesn't read.</summary>
    static (int, int)? ImageSize(string path)
    {
        if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            // PNG: 8-byte signature, then the IHDR chunk with big-endian width and height at bytes 16–23.
            Span<byte> header = stackalloc byte[24];
            using var stream = File.OpenRead(path);
            if (stream.Read(header) < 24) return null;
            return (BinaryPrimitives.ReadInt32BigEndian(header[16..]), BinaryPrimitives.ReadInt32BigEndian(header[20..]));
        }
        if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            var match = SvgSize().Match(File.ReadAllText(path));
            if (match.Success)
                return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
        }
        return null;
    }

    [GeneratedRegex("<svg[^>]*?\\swidth=\"(\\d+)\"[^>]*?\\sheight=\"(\\d+)\"")]
    private static partial Regex SvgSize();
}
