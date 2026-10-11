using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Exploration;
using Godot;
using static EternalDungeon.Game.Text;

namespace EternalDungeon.Game.RunUi;

/// <summary>
/// The footer of the exploration screen (Anchor: Exploration › Map screen): Camp (its charges on the button) with the Hero
/// Panel under it, then each hero (its card as in battle, a red dot while it has skill points; Stamina; up to 4 belt
/// slots), and Options at the right. Pressing a hero's card opens the Hero Panel on it; pressing a belt item usable
/// outside combat uses it.
/// </summary>
public partial class PartyBar : HBoxContainer
{
    readonly RunScreen screen;
    public Button Camp { get; private set; } = null!;
    public Button HeroPanel { get; private set; } = null!;
    public Button Options { get; private set; } = null!;

    public PartyBar(RunScreen screen)
    {
        this.screen = screen;
        AddThemeConstantOverride("separation", 14);
    }

    public void Refresh()
    {
        foreach (var c in GetChildren()) c.QueueFree();
        var run = screen.Run;
        var exploring = run.State == RunState.Exploring && run.Started;

        var left = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        left.AddThemeConstantOverride("separation", 6);
        Camp = Ui.Button(F("ui.camp_count", ("count", run.CampCharges)), screen.ConfirmCamp);
        Camp.Icon = screen.Main.Art.Get("icon_camp");
        Camp.ExpandIcon = true;
        Camp.AddThemeConstantOverride("icon_max_width", Ui.Px(28));
        Camp.TooltipText = T("ui.camp_hint");
        Camp.Disabled = !exploring || run.CampCharges <= 0;
        Camp.CustomMinimumSize = new Vector2(96, 52);
        left.AddChild(Camp);
        HeroPanel = Ui.Button(T("ui.hero_panel"), () => screen.OpenHeroPanel(run.Heroes.FirstOrDefault(h => h.SkillPoints > 0 && !h.Dead)));
        HeroPanel.Disabled = run.State != RunState.Exploring;
        HeroPanel.CustomMinimumSize = new Vector2(96, 52);
        left.AddChild(HeroPanel);
        AddChild(left);

        foreach (var hero in run.Heroes)
        {
            var block = new HBoxContainer();
            block.AddThemeConstantOverride("separation", 6);
            var card = new HeroCard(screen, hero);
            card.Pressed += () => screen.OpenHeroPanel(hero);
            block.AddChild(card);
            var side = new VBoxContainer();
            side.AddThemeConstantOverride("separation", 4);
            side.AddChild(new StaminaView(hero));
            var slots = new GridContainer { Columns = 2 };
            slots.AddThemeConstantOverride("h_separation", 4);
            slots.AddThemeConstantOverride("v_separation", 4);
            for (var i = 0; i < RunScreen.ActionSlots; i++)
                slots.AddChild(new BeltSlotButton(screen, hero, i));
            side.AddChild(slots);
            block.AddChild(side);
            AddChild(block);
        }

        AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        Options = Ui.Button(T("ui.options"), screen.OpenOptions);
        Options.CustomMinimumSize = new Vector2(96, 52);
        Options.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        AddChild(Options);
    }
}

/// <summary>A hero's card, drawn like its card in battle: portrait, name, Mana and Health. A red dot shows unspent skill
/// points.</summary>
public partial class HeroCard : Button
{
    public static readonly Vector2 Box = new(104, 128);
    readonly RunScreen screen;
    readonly Hero hero;

    public HeroCard(RunScreen screen, Hero hero)
    {
        this.screen = screen;
        this.hero = hero;
        CustomMinimumSize = Box;
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
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.15f, 0.2f, 0.3f));
        var p = new Rect2(4, 4, Size.X - 8, Size.X - 8);
        var state = hero.Dead || hero.Status == HeroStatus.Unconscious ? "knocked_out"
            : hero.Health * 3 < hero.MaxHealth ? "low_hp" : Core.Assets.ArtCatalog.DefaultState;
        var grey = hero.Dead ? new Color(0.4f, 0.4f, 0.4f) : Colors.White;
        if (screen.Main.Art.Portrait(hero.Unit.Id, state) is { } tex)
            DrawTextureRect(tex, p, false, grey);
        else
        {
            DrawRect(p, Ui.Party.Darkened(0.55f) * grey);
            DrawRect(new Rect2(p.Position, new Vector2(p.Size.X, p.Size.Y * 0.5f)), new Color(Ui.Party.Darkened(0.35f), 0.6f));
            DrawString(font, new Vector2(p.Position.X, p.Position.Y + p.Size.Y * 0.5f + 12), hero.Name[..1],
                HorizontalAlignment.Center, p.Size.X, 34, new Color(Ui.Ink, 0.8f));
        }
        var banner = new Rect2(p.Position.X, p.End.Y - 18, p.Size.X, 18);
        DrawRect(banner, new Color(0, 0, 0, 0.65f));
        DrawString(font, banner.Position + new Vector2(3, 13), hero.Name, HorizontalAlignment.Center, banner.Size.X - 6, 11, Ui.Ink);
        // The level at the portrait's top right.
        var level = F("ui.level_short", ("level", hero.Level));
        var lw = font.GetStringSize(level, HorizontalAlignment.Left, -1, 11).X + 8;
        DrawRect(new Rect2(p.End.X - lw, p.Position.Y, lw, 16), new Color(0, 0, 0, 0.65f));
        DrawString(font, new Vector2(p.End.X - lw, p.Position.Y + 12), level, HorizontalAlignment.Center, lw, 11, Ui.Ink);

        // Mana (thin, only for heroes with Mana), then Health with numbers, as on the battle card.
        var y = p.End.Y + 2;
        if (hero.MaxMana > 0)
        {
            DrawRect(new Rect2(p.Position.X, y, p.Size.X, 4), new Color(0, 0, 0, 0.5f));
            DrawRect(new Rect2(p.Position.X, y, p.Size.X * hero.Mana / hero.MaxMana, 4), Ui.Mana);
        }
        y += 6;
        var hp = new Rect2(p.Position.X, y, p.Size.X, 14);
        DrawRect(hp, new Color(0, 0, 0, 0.6f));
        var share = (float)hero.Health / Math.Max(1, hero.MaxHealth);
        DrawRect(new Rect2(hp.Position, new Vector2(hp.Size.X * share, hp.Size.Y)), share < 1f / 3 ? Ui.HealthLow : Ui.Health);
        DrawString(font, hp.Position + new Vector2(0, 11), F("ui.hp", ("hp", hero.Health), ("max", hero.MaxHealth)),
            HorizontalAlignment.Center, hp.Size.X, 11, Colors.White);

        DrawRect(new Rect2(Vector2.One, Size - Vector2.One * 2), IsHovered() ? Ui.Gold : new Color(Ui.Party, 0.7f), false, 2);
        // Unspent skill points: a red dot with the count.
        if (hero.SkillPoints > 0 && !hero.Dead)
        {
            DrawCircle(new Vector2(10, 10), 9, Ui.Enemy);
            DrawString(font, new Vector2(1, 15), hero.SkillPoints.ToString(), HorizontalAlignment.Center, 18, 12, Colors.White);
        }
    }
}

/// <summary>A hero's Stamina: a pip and the value; red with the status (Exhausted…) once it costs the hero.</summary>
public partial class StaminaView : Control
{
    readonly Hero hero;

    public StaminaView(Hero hero)
    {
        this.hero = hero;
        CustomMinimumSize = new Vector2(84, 22);
        TooltipText = hero.Status == HeroStatus.Ready ? F("ui.stamina", ("stamina", hero.Stamina), ("max", hero.MaxStamina))
            : F("ui.stamina_status", ("stamina", hero.Stamina), ("status", RunLog.StatusName(EternalDungeon.Game.Text.Strings, hero.Status)));
        MouseFilter = MouseFilterEnum.Pass;
    }

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        var ready = hero.Status == HeroStatus.Ready;
        DrawCircle(new Vector2(9, 11), 8, ready ? Ui.Act : Ui.Enemy);
        var text = F("ui.hp", ("hp", hero.Stamina), ("max", hero.MaxStamina));
        if (!ready && !hero.Dead) text += " " + RunLog.StatusName(EternalDungeon.Game.Text.Strings, hero.Status);
        DrawString(font, new Vector2(22, 16), text, HorizontalAlignment.Left, Size.X - 22, Ui.Px(12), ready ? Ui.Ink : Ui.Enemy);
    }
}

/// <summary>
/// One of a hero's exploration action slots. For now (M3A) the slots show the belt: an item and its charges; pressing one
/// usable outside combat uses it. (M3B: the first 4 exploration spells or items equipped on the belt.) Slots past the
/// hero's belt size are drawn shut.
/// </summary>
public partial class BeltSlotButton : Button
{
    public static readonly Vector2 Box = new(38, 38);
    readonly RunScreen screen;
    readonly Hero hero;
    readonly int index;

    BeltSlot? Slot => index < hero.Belt.Count ? hero.Belt[index] : null;

    bool CanUse => Slot is { Item.OutsideCombat: true } && screen.Run.State == RunState.Exploring && hero.Available;

    public BeltSlotButton(RunScreen screen, Hero hero, int index)
    {
        this.screen = screen;
        this.hero = hero;
        this.index = index;
        CustomMinimumSize = Box;
        AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        FocusMode = CanUse ? FocusModeEnum.All : FocusModeEnum.None;
        foreach (var style in new[] { "normal", "hover", "pressed", "disabled" })
            AddThemeStyleboxOverride(style, new StyleBoxEmpty());
        AddThemeStyleboxOverride("focus", Ui.Focus());
        TooltipText = Slot is { } s
            ? F(CanUse ? "ui.slot_use" : s.Item.OutsideCombat ? "ui.slot_cant_use" : "ui.slot_battle", ("item", s.Item.Name), ("charges", s.Charges))
            : index < hero.BeltSlots ? T("ui.slot_empty") : T("ui.slot_shut");
        Pressed += () =>
        {
            if (CanUse && Slot is { } slot) screen.Act(() => screen.Run.UseItem(hero.Id, slot.Item.Id));
        };
    }

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        var r = new Rect2(Vector2.Zero, Size);
        if (Slot is not { } slot)
        {
            var open = index < hero.BeltSlots;
            DrawRect(r, open ? new Color(0.25f, 0.24f, 0.28f) : new Color(0.14f, 0.13f, 0.16f));
            DrawRect(r, new Color(Ui.PanelBorder, open ? 1 : 0.4f), false, 1);
            return;
        }
        DrawRect(r, new Color(0.62f, 0.5f, 0.12f));
        if (screen.Main.Art.Get(Core.Assets.ArtCatalog.ActionIconId(slot.Item.Action)) is { } tex)
            DrawTextureRect(tex, r.Grow(-3), false);
        else
            DrawString(font, new Vector2(0, 24), slot.Item.Name[..1], HorizontalAlignment.Center, Size.X, 18, Ui.Ink);
        DrawString(font, new Vector2(0, Size.Y - 3), slot.Charges.ToString(), HorizontalAlignment.Right, Size.X - 3, 12, Colors.White);
        DrawRect(r, IsHovered() && CanUse ? Ui.Gold : new Color(0, 0, 0, 0.6f), false, IsHovered() && CanUse ? 2 : 1);
    }
}
