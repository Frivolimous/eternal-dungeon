using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Assets;

/// <summary>A rectangle in whole pixels.</summary>
public readonly record struct Box(int X, int Y, int W, int H)
{
    public int Right => X + W;
    public int Bottom => Y + H;
    public Box Scaled(int k) => new(X * k, Y * k, W * k, H * k);
}

/// <summary>
/// Where the game draws each part of a unit card's front, in on-screen pixels (the art is twice these). The card
/// frame image is painted around these slots and left transparent over them; <c>sim art-requests</c> writes a
/// template of each frame from this, so the art and the code read the same numbers.
/// </summary>
public sealed record CardLayout(int Width, int Height, Box ActMeter, Box Portrait, Box NamePlate, Box Mana, Box Health)
{
    public const int NamePlateHeight = 18, ManaHeight = 4, HealthHeight = 14, Gap = 2;

    /// <summary>A highlight's width along the card's edge, drawn over the frame's outer border.</summary>
    public const int HighlightWidth = 3;

    /// <summary>The Act meter up the left edge, the portrait beside it (centred in the rest), the name plate over the
    /// portrait's bottom, then the Mana and HP bars below it.</summary>
    public static CardLayout Of(int width, int height, int portraitW, int portraitH)
    {
        var portrait = new Box(10 + (width - 14 - portraitW) / 2, 4, portraitW, portraitH);
        var mana = new Box(portrait.X, portrait.Bottom + Gap, portrait.W, ManaHeight);
        var health = new Box(portrait.X, mana.Bottom + Gap, portrait.W, HealthHeight);
        return new CardLayout(width, height,
            ActMeter: new Box(3, 4, 5, height - 8),
            Portrait: portrait,
            NamePlate: new Box(portrait.X, portrait.Bottom - NamePlateHeight, portrait.W, NamePlateHeight),
            Mana: mana,
            Health: health);
    }

    /// <summary>Where <c>sim art-requests</c> writes the card frame templates.</summary>
    public const string TemplateFolder = "docs/art-templates";

    /// <summary>The colours, in drawing order, with what each means (the legend in docs/art-requests.md; sim art-requests paints them).</summary>
    public static readonly (string Name, uint Rgb, string Means)[] TemplateLegend =
    [
        ("grey", 0x808080, "the frame: paint here"),
        ("pink", 0xE060A0, "the outer edge: the frame's border goes here; a highlight (active, valid target, hovered) is drawn over it"),
        ("orange", 0xE8A33C, "the Act meter, drawn by the game: keep it see-through (a light tint is fine, the fill must still read)"),
        ("white", 0xFFFFFF, "the portrait: transparent"),
        ("purple", 0x3A2F5A, "the name plate, drawn by the game (solid, the name on it): transparent"),
        ("blue", 0x4C7BFF, "the Mana bar (empty for units without Mana): transparent"),
        ("green", 0x5CC75F, "the HP bar: transparent"),
    ];

    public static CardLayout For(UnitSize size)
    {
        var (w, h) = ArtCatalog.CardDisplay(size);
        var (pw, ph) = ArtCatalog.PortraitDisplay(size);
        return Of(w, h, pw, ph);
    }
}
