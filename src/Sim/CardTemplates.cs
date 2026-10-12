using System.IO.Compression;
using EternalDungeon.Core.Assets;
using EternalDungeon.Core.Data;

// sim art-requests also writes docs/art-templates/: one template per card frame, at the frame's exact size, with each
// slot the game draws into (from CardLayout) in a flat colour. The artist paints the frame over the grey.
static class CardTemplates
{
    public static List<string> Write(string root)
    {
        var folder = Path.Combine(root, CardLayout.TemplateFolder);
        Directory.CreateDirectory(folder);
        var written = new List<string>();
        foreach (var size in Enum.GetValues<UnitSize>())
        {
            var layout = CardLayout.For(size);
            var (w, h) = (layout.Width * 2, layout.Height * 2);
            var pixels = new uint[w * h];
            void Fill(Box b, uint rgb)
            {
                for (var y = b.Y; y < b.Bottom; y++)
                for (var x = b.X; x < b.Right; x++)
                    pixels[y * w + x] = rgb;
            }
            Fill(new Box(0, 0, w, h), CardLayout.TemplateLegend[1].Rgb);
            Fill(new Box(CardLayout.HighlightWidth * 2, CardLayout.HighlightWidth * 2, w - CardLayout.HighlightWidth * 4, h - CardLayout.HighlightWidth * 4), CardLayout.TemplateLegend[0].Rgb);
            Fill(layout.ActMeter.Scaled(2), CardLayout.TemplateLegend[2].Rgb);
            Fill(layout.Portrait.Scaled(2), CardLayout.TemplateLegend[3].Rgb);
            Fill(layout.NamePlate.Scaled(2), CardLayout.TemplateLegend[4].Rgb);
            Fill(layout.Mana.Scaled(2), CardLayout.TemplateLegend[5].Rgb);
            Fill(layout.Health.Scaled(2), CardLayout.TemplateLegend[6].Rgb);
            var name = $"card_frame_{ArtCatalog.SizeName(size)}_template.png";
            File.WriteAllBytes(Path.Combine(folder, name), Png(w, h, pixels));
            written.Add(name);
        }
        return written;
    }

    /// <summary>An opaque RGB PNG.</summary>
    static byte[] Png(int w, int h, uint[] pixels)
    {
        var raw = new byte[h * (1 + w * 3)];
        for (int y = 0, i = 0; y < h; y++)
        {
            raw[i++] = 0; // no filter
            for (var x = 0; x < w; x++)
            {
                var p = pixels[y * w + x];
                raw[i++] = (byte)(p >> 16);
                raw[i++] = (byte)(p >> 8);
                raw[i++] = (byte)p;
            }
        }
        using var deflated = new MemoryStream();
        using (var z = new ZLibStream(deflated, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(raw);

        using var png = new MemoryStream();
        png.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BigEndian(header, 0, (uint)w);
        BigEndian(header, 4, (uint)h);
        header[8] = 8; // bit depth
        header[9] = 2; // RGB
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", deflated.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        BigEndian(len, 0, (uint)data.Length);
        s.Write(len);
        var body = new byte[4 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(type).CopyTo(body, 0);
        data.CopyTo(body, 4);
        s.Write(body);
        var crc = new byte[4];
        BigEndian(crc, 0, Crc32(body));
        s.Write(crc);
    }

    static void BigEndian(byte[] b, int at, uint v)
    {
        b[at] = (byte)(v >> 24);
        b[at + 1] = (byte)(v >> 16);
        b[at + 2] = (byte)(v >> 8);
        b[at + 3] = (byte)v;
    }

    static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }
}
