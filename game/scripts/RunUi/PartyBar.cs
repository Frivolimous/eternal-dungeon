using EternalDungeon.Core.Exploration;
using Godot;
using static EternalDungeon.Game.Text;

namespace EternalDungeon.Game.RunUi;

/// <summary>
/// The party along the bottom (M3A brief §7): each hero's portrait, level, Health, Mana, Stamina and status (in words,
/// so it reads without colour), and unspent skill points. Pressing a hero opens the Hero Panel on it.
/// </summary>
public partial class PartyBar : HBoxContainer
{
    readonly RunScreen screen;

    public PartyBar(RunScreen screen)
    {
        this.screen = screen;
        AddThemeConstantOverride("separation", 10);
    }

    public void Refresh()
    {
        foreach (var c in GetChildren()) c.QueueFree();
        foreach (var hero in screen.Run.Heroes)
        {
            var card = new HeroCard(screen, hero) { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            card.Pressed += () => screen.OpenHeroPanel(hero);
            AddChild(card);
        }
    }
}

public partial class HeroCard : Button
{
    readonly RunScreen screen;
    readonly Hero hero;

    public HeroCard(RunScreen screen, Hero hero)
    {
        this.screen = screen;
        this.hero = hero;
        CustomMinimumSize = new Vector2(250, 104);
        FocusMode = FocusModeEnum.All;
        AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        foreach (var style in new[] { "normal", "hover", "pressed", "disabled" })
            AddThemeStyleboxOverride(style, new StyleBoxEmpty());
        AddThemeStyleboxOverride("focus", Ui.Focus());
        TooltipText = F("ui.hero_card_hint", ("hero", hero.Name));
    }

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        var r = new Rect2(Vector2.Zero, Size);
        DrawRect(r, new Color(Ui.Panel, 0.95f));
        DrawRect(r, IsHovered() ? Ui.Gold : Ui.PanelBorder, false, 2);
        var portrait = new Rect2(6, 6, 92, 92);
        var state = hero.Dead ? "knocked_out" : hero.Health * 3 < hero.MaxHealth ? "low_hp" : Core.Assets.ArtCatalog.DefaultState;
        if (screen.Main.Art.Portrait(hero.Unit.Id, state) is { } tex)
            DrawTextureRect(tex, portrait, false, hero.Dead ? new Color(0.4f, 0.4f, 0.4f) : Colors.White);
        else
        {
            DrawRect(portrait, Ui.Party.Darkened(0.45f));
            DrawString(font, portrait.Position + new Vector2(0, 58), hero.Name[..1], HorizontalAlignment.Center, portrait.Size.X, Ui.Px(36), Ui.Ink);
        }
        var x = portrait.End.X + 8;
        var w = Size.X - x - 8;
        DrawString(font, new Vector2(x, 20), F("ui.hero_title", ("hero", hero.Name), ("level", hero.Level)), HorizontalAlignment.Left, w, Ui.Px(14), Ui.Ink);
        if (hero.SkillPoints > 0 && !hero.Dead)
            DrawString(font, new Vector2(x, 20), F("ui.points_short", ("points", hero.SkillPoints)), HorizontalAlignment.Right, w, Ui.Px(12), Ui.Gold);
        Bar(new Rect2(x, 28, w, 12), hero.Health, hero.MaxHealth, hero.Health * 3 < hero.MaxHealth ? Ui.HealthLow : Ui.Health,
            F("ui.hp", ("hp", hero.Health), ("max", hero.MaxHealth)), font);
        if (hero.MaxMana > 0)
            Bar(new Rect2(x, 44, w, 12), hero.Mana, hero.MaxMana, Ui.Mana, F("ui.hp", ("hp", hero.Mana), ("max", hero.MaxMana)), font);
        // Stamina as pips: filled up to the current value (0 to max); below 0 the status says how far.
        var pips = hero.MaxStamina;
        for (var i = 0; i < pips; i++)
        {
            var pip = new Rect2(x + i * 16, 62, 12, 12);
            DrawRect(pip, i < hero.Stamina ? Ui.Act : new Color(0, 0, 0, 0.5f));
            DrawRect(pip, Ui.Act.Darkened(0.3f), false, 1);
        }
        var status = hero.Status == HeroStatus.Ready ? F("ui.stamina", ("stamina", hero.Stamina), ("max", hero.MaxStamina))
            : F("ui.stamina_status", ("stamina", hero.Stamina), ("status", RunLog.StatusName(EternalDungeon.Game.Text.Strings, hero.Status)));
        DrawString(font, new Vector2(x + pips * 16 + 4, 73), status, HorizontalAlignment.Left, w - pips * 16 - 4, Ui.Px(11),
            hero.Status is HeroStatus.Ready ? Ui.Dim : Ui.Enemy);
        var belt = string.Join(" · ", hero.Belt.Select(s => $"{s.Item.Name} ×{s.Charges}"));
        DrawString(font, new Vector2(x, 94), belt.Length > 0 ? belt : T("ui.belt_empty"), HorizontalAlignment.Left, w, Ui.Px(10), Ui.Dim);
    }

    void Bar(Rect2 r, int value, int max, Color color, string text, Font font)
    {
        DrawRect(r, new Color(0, 0, 0, 0.55f));
        if (max > 0) DrawRect(new Rect2(r.Position, new Vector2(r.Size.X * Math.Clamp(value / (float)max, 0, 1), r.Size.Y)), color);
        DrawString(font, r.Position + new Vector2(0, r.Size.Y - 2), text, HorizontalAlignment.Center, r.Size.X, Ui.Px(10), Ui.Ink);
    }
}
