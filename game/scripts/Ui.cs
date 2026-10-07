using Godot;

namespace EternalDungeon.Game;

/// <summary>Placeholder look: colours, the theme, and small builders for panels and buttons.</summary>
public static class Ui
{
    public static readonly Color Background = new(0.118f, 0.106f, 0.149f);
    public static readonly Color Panel = new(0.16f, 0.145f, 0.2f, 0.94f);
    public static readonly Color PanelBorder = new(0.36f, 0.32f, 0.42f);
    public static readonly Color Ink = new(0.93f, 0.9f, 0.84f);
    public static readonly Color Dim = new(0.65f, 0.62f, 0.7f);
    public static readonly Color Gold = new(0.95f, 0.78f, 0.35f);
    public static readonly Color Party = new(0.35f, 0.62f, 0.95f);
    public static readonly Color Enemy = new(0.92f, 0.38f, 0.33f);
    public static readonly Color Health = new(0.36f, 0.78f, 0.38f);
    public static readonly Color HealthLow = new(0.9f, 0.3f, 0.25f);
    public static readonly Color Shield = new(0.6f, 0.8f, 0.95f);
    public static readonly Color Mana = new(0.4f, 0.55f, 1f);
    public static readonly Color Act = new(0.95f, 0.78f, 0.35f);
    public static readonly Color Stagger = new(0.95f, 0.55f, 0.2f);
    public static readonly Color Valid = new(0.45f, 0.95f, 0.55f);

    /// <summary>Design font size at text scale 1.</summary>
    public const int FontSize = 16;

    /// <summary>The current text scale (Settings.TextScale), applied to explicit font sizes too.</summary>
    public static float Scale { get; private set; } = 1;

    /// <summary>A font size at the current text scale.</summary>
    public static int Px(int size) => Mathf.RoundToInt(size * Scale);

    public static Theme MakeTheme(float scale)
    {
        Scale = scale;
        var theme = new Theme { DefaultFontSize = Mathf.RoundToInt(FontSize * scale) };
        theme.SetColor("font_color", "Label", Ink);
        theme.SetColor("font_color", "Button", Ink);
        theme.SetColor("font_disabled_color", "Button", new Color(Dim, 0.6f));
        theme.SetStylebox("normal", "Button", Box(new Color(0.22f, 0.2f, 0.28f), PanelBorder));
        theme.SetStylebox("hover", "Button", Box(new Color(0.3f, 0.27f, 0.37f), Gold));
        theme.SetStylebox("pressed", "Button", Box(new Color(0.36f, 0.32f, 0.44f), Gold));
        theme.SetStylebox("disabled", "Button", Box(new Color(0.15f, 0.14f, 0.18f), new Color(PanelBorder, 0.5f)));
        theme.SetStylebox("focus", "Button", Focus());
        theme.SetStylebox("panel", "PanelContainer", Box(Panel, PanelBorder));
        theme.SetStylebox("normal", "LineEdit", Box(new Color(0.1f, 0.09f, 0.13f), PanelBorder));
        theme.SetStylebox("focus", "LineEdit", Focus());
        theme.SetStylebox("focus", "ItemList", Focus());
        theme.SetStylebox("panel", "ItemList", Box(new Color(0.1f, 0.09f, 0.13f), PanelBorder));
        return theme;
    }

    public static StyleBoxFlat Box(Color fill, Color border, int width = 2, int radius = 6) => new()
    {
        BgColor = fill,
        BorderColor = border,
        BorderWidthLeft = width, BorderWidthRight = width, BorderWidthTop = width, BorderWidthBottom = width,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6,
    };

    /// <summary>A bright outline, so keyboard and controller focus is always visible.</summary>
    public static StyleBoxFlat Focus()
    {
        var b = Box(Colors.Transparent, Gold, 3);
        b.DrawCenter = false;
        b.ExpandMarginLeft = b.ExpandMarginRight = b.ExpandMarginTop = b.ExpandMarginBottom = 2;
        return b;
    }

    public static Label Label(string text, int size = 0, Color? color = null)
    {
        var l = new Label { Text = text, AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled };
        if (size > 0) l.AddThemeFontSizeOverride("font_size", Px(size));
        if (color is { } c) l.AddThemeColorOverride("font_color", c);
        return l;
    }

    public static Button Button(string text, Action pressed)
    {
        var b = new Button { Text = text, AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled, FocusMode = Control.FocusModeEnum.All };
        b.Pressed += pressed;
        return b;
    }

    public static Color Side(Core.Combat.Side side) => side == Core.Combat.Side.Party ? Party : Enemy;
}
