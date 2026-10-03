using System.Buffers.Binary;
using System.Text.RegularExpressions;
using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Assets;

/// <summary>One image the game loads, at a fixed path and size so final art drops in without code changes.</summary>
public sealed record AssetEntry(string Id, string Path, int Width, int Height, bool AiPlaceholder, string? Note);

/// <summary>
/// game/assets/manifest.json: every image, with its size and whether it's an AI placeholder.
/// Anchor: Art pipeline. No flagged asset may remain at release.
/// </summary>
public sealed partial class AssetManifest(IReadOnlyList<AssetEntry> entries)
{
    public const string FileName = "manifest.json";

    public IReadOnlyList<AssetEntry> Entries { get; } = entries;

    public IEnumerable<AssetEntry> AiPlaceholders => Entries.Where(e => e.AiPlaceholder);

    public static AssetManifest Parse(string text)
    {
        var root = JsonField.Parse(FileName, text);
        root.OnlyFields("assets");
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
        return new AssetManifest(entries);
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
