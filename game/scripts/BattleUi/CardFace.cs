using EternalDungeon.Core.Assets;
using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using Godot;
using Side = EternalDungeon.Core.Combat.Side;

namespace EternalDungeon.Game.BattleUi;

/// <summary>How a card is highlighted on the board.</summary>
public enum CardMark { None, Active, Valid, Hovered }

/// <summary>
/// The front of a unit's card, drawn into its own viewport (twice its size, for sharpness) and shown on the 3D card
/// (M2 brief §3): portrait (or a generated placeholder), name, HP bar with numbers and Shield, the Act meter along
/// the left edge (a cast's progress while casting), the Mana gauge and status icons. All text
/// comes from the engine; images hold none.
/// </summary>
public partial class CardFace : Control
{
    public Unit Unit { get; }
    readonly BattleScreen screen;
    readonly bool rotatePortrait;
    public CardMark Mark { get; set; }

    /// <summary>A moment's portrait state set by the screen while it animates: "attacking", "hurt" or
    /// "knocked_out". It wins over the lasting states (casting, low HP).</summary>
    public string? Moment { get; set; }

    public CardFace(BattleScreen screen, Unit unit, Vector2 size, bool rotatePortrait)
    {
        this.screen = screen;
        Unit = unit;
        this.rotatePortrait = rotatePortrait;
        Size = size;
        CustomMinimumSize = size;
    }

    /// <summary>Portrait rectangle: 10 px from the left (the Act meter), 4 px from the top, full width otherwise.</summary>
    Rect2 PortraitRect
    {
        get
        {
            var (w, h) = ArtCatalog.PortraitDisplay(Unit.Def.Size);
            if (rotatePortrait) (w, h) = (h, w);
            var x = 10 + (Size.X - 14 - w) / 2;
            return new Rect2(x, 4, w, h);
        }
    }

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        var side = Ui.Side(Unit.Side);
        var bg = Unit.Side == Side.Party ? new Color(0.15f, 0.2f, 0.3f) : new Color(0.3f, 0.15f, 0.15f);
        DrawRect(new Rect2(Vector2.Zero, Size), bg);

        // Portrait, or a generated placeholder: a gradient in the side's colour and the unit's initials.
        var p = PortraitRect;
        var texture = screen.Main.Art.Portrait(Unit.Def.Id, PortraitState());
        if (texture is not null)
        {
            if (rotatePortrait)
            {
                DrawSetTransform(p.Position + new Vector2(p.Size.X, 0), Mathf.Pi / 2, Vector2.One);
                DrawTextureRect(texture, new Rect2(0, 0, p.Size.Y, p.Size.X), false);
                DrawSetTransform(Vector2.Zero, 0, Vector2.One);
            }
            else
                DrawTextureRect(texture, p, false);
        }
        else
        {
            DrawRect(p, side.Darkened(0.55f));
            DrawRect(new Rect2(p.Position, new Vector2(p.Size.X, p.Size.Y * 0.5f)), new Color(side.Darkened(0.35f), 0.6f));
            var initials = string.Concat(Unit.Def.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w[0]));
            var big = Mathf.RoundToInt(Mathf.Min(p.Size.X, p.Size.Y) * 0.36f);
            DrawString(font, new Vector2(p.Position.X, p.Position.Y + p.Size.Y * 0.5f + big * 0.35f), initials,
                HorizontalAlignment.Center, p.Size.X, big, new Color(Ui.Ink, 0.8f));
        }

        // Name banner along the bottom of the portrait.
        var banner = new Rect2(p.Position.X, p.End.Y - 18, p.Size.X, 18);
        DrawRect(banner, new Color(0, 0, 0, 0.65f));
        DrawString(font, banner.Position + new Vector2(3, 13), Unit.Name, HorizontalAlignment.Center, banner.Size.X - 6, 11, Ui.Ink);

        // Status icons along the top of the portrait.
        var x = p.Position.X + 2;
        foreach (var status in Statuses(Unit, screen.Session.Battle))
        {
            StatusIcon.Draw(this, status, new Rect2(x, p.Position.Y + 2, ArtCatalog.StatusIconDisplay, ArtCatalog.StatusIconDisplay), screen.Main.Art);
            x += ArtCatalog.StatusIconDisplay + 2;
        }

        if (Unit.Alive && Unit.Intent is { } intent) DrawIntent(font, p, intent);

        // Act meter (or cast progress) up the left edge.
        var meter = new Rect2(3, 4, 5, Size.Y - 8);
        DrawRect(meter, new Color(0, 0, 0, 0.5f));
        var tick = screen.Session.Battle.Clock.Tick;
        var (fill, color) = Unit.Casting is { } cast
            ? ((float)(tick - cast.StartedAt) / Math.Max(1, cast.CompletesAt - cast.StartedAt), Ui.Mana)
            : ((float)(Unit.Act / 100.0), Ui.Act);
        fill = Mathf.Clamp(fill, 0, 1);
        DrawRect(new Rect2(meter.Position.X, meter.End.Y - meter.Size.Y * fill, meter.Size.X, meter.Size.Y * fill), color);

        // The Mana gauge (units with Mana only; the slot stays empty otherwise, so HP bars line up), then the HP bar
        // with numbers and Shield.
        var barX = p.Position.X;
        var barW = p.Size.X;
        var y = p.End.Y + 2;
        const int thin = 4;
        if (Unit.MaxMana > 0)
        {
            DrawRect(new Rect2(barX, y, barW, thin), new Color(0, 0, 0, 0.5f));
            DrawRect(new Rect2(barX, y, barW * Unit.Mana / Unit.MaxMana, thin), Ui.Mana);
        }
        y += thin + 2;
        var hp = new Rect2(barX, y, barW, 14);
        DrawRect(hp, new Color(0, 0, 0, 0.6f));
        var share = (float)Unit.Health / Math.Max(1, Unit.MaxHealth);
        DrawRect(new Rect2(hp.Position, new Vector2(hp.Size.X * share, hp.Size.Y)), share < 1f / 3 ? Ui.HealthLow : Ui.Health);
        if (Unit.Shield > 0)
        {
            var sw = Mathf.Min(hp.Size.X, hp.Size.X * Unit.Shield / Math.Max(1, Unit.MaxHealth));
            DrawRect(new Rect2(hp.End.X - sw, hp.Position.Y, sw, 4), Ui.Shield);
        }
        var hpText = Unit.Shield > 0 ? Text.F("ui.hp_shield", ("hp", Unit.Health), ("max", Unit.MaxHealth), ("shield", Unit.Shield))
                                     : Text.F("ui.hp", ("hp", Unit.Health), ("max", Unit.MaxHealth));
        DrawString(font, hp.Position + new Vector2(0, 11), hpText, HorizontalAlignment.Center, hp.Size.X, 11, Colors.White);

        // Highlight: the active hero glows gold, valid targets green, the hovered target white.
        var mark = Mark switch
        {
            CardMark.Active => Ui.Gold,
            CardMark.Valid => Ui.Valid,
            CardMark.Hovered => Colors.White,
            _ => new Color(side, 0.7f),
        };
        DrawRect(new Rect2(Vector2.One, Size - Vector2.One * 2), mark, false, Mark == CardMark.None ? 2 : 4);
        if (screen.Main.Art.Get($"card_frame_{ArtCatalog.SizeName(Unit.Def.Size)}") is { } frame && !rotatePortrait)
            DrawTextureRect(frame, new Rect2(Vector2.Zero, Size), false);
    }

    const int IntentIcon = 18;

    /// <summary>0–1 while the intent flashes after a trigger changed the plan.</summary>
    float intentFlash;

    /// <summary>The plan changed because of something a unit did (a taunt, Stealth, a target falling): flash it.</summary>
    public void FlashIntent(float seconds)
    {
        var t = CreateTween();
        t.TweenMethod(Callable.From<float>(v =>
        {
            intentFlash = v;
            QueueRedraw();
        }), 1f, 0f, Math.Max(0.6f, seconds));
    }

    /// <summary>
    /// An enemy's plan in the top-right corner of its portrait (Anchor: Combat › Enemy targeting): the planned
    /// action's icon, then a small portrait of its target ("↑" for a Move, nothing for a self action). "?" while
    /// Confused, "…" when it plans to wait.
    /// </summary>
    void DrawIntent(Font font, Rect2 p, Intent intent)
    {
        var d = intent.Decision;
        var hasTarget = d is { Tile: not null } || (d?.Target is { } t0 && t0 != Unit);
        var width = intent.Unknown || d is null || !hasTarget ? IntentIcon + 4 : IntentIcon * 2 + 6;
        var box = new Rect2(p.End.X - width - 2, p.Position.Y + 2, width, IntentIcon + 4);
        DrawRect(box, new Color(0, 0, 0, 0.75f));
        DrawRect(box, intentFlash > 0 ? Ui.Gold.Lerp(Ui.Enemy, 1 - intentFlash) : new Color(Ui.Enemy, 0.8f), false, intentFlash > 0 ? 3 : 1);
        var slot = new Rect2(box.Position + new Vector2(2, 2), new Vector2(IntentIcon, IntentIcon));
        if (intent.Unknown || d is null)
        {
            DrawString(font, slot.Position + new Vector2(0, 14), intent.Unknown ? "?" : "…", HorizontalAlignment.Center, slot.Size.X, 14, Ui.Ink);
            return;
        }
        if (screen.Main.Art.Get(ArtCatalog.ActionIconId(d.Action.Id)) is { } icon)
            DrawTextureRect(icon, slot, false);
        else
        {
            DrawRect(slot, Ui.Enemy.Darkened(0.4f));
            DrawString(font, slot.Position + new Vector2(0, 14), d.Action.Name[..1], HorizontalAlignment.Center, slot.Size.X, 13, Ui.Ink);
        }
        if (!hasTarget) return;
        var second = new Rect2(slot.Position + new Vector2(IntentIcon + 2, 0), slot.Size);
        if (d.Tile is not null)
        {
            DrawString(font, second.Position + new Vector2(0, 14), "↑", HorizontalAlignment.Center, second.Size.X, 14, Ui.Ink);
            return;
        }
        var target = d.Target!;
        if (screen.Main.Art.Portrait(target.Def.Id) is { } tex)
        {
            var square = Math.Min(tex.GetWidth(), tex.GetHeight());         // a tall portrait shows its face
            DrawTextureRectRegion(tex, second, new Rect2(0, 0, square, square));
        }
        else
        {
            DrawRect(second, Ui.Side(target.Side).Darkened(0.45f));
            var initials = string.Concat(target.Def.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w[0]));
            DrawString(font, second.Position + new Vector2(0, 13), initials, HorizontalAlignment.Center, second.Size.X, 10, Ui.Ink);
        }
        DrawRect(second, new Color(Ui.Side(target.Side), 0.9f), false, 1);
    }

    string PortraitState()
    {
        if (Moment is { } moment) return moment;
        if (!Unit.Alive) return "knocked_out";
        if (Unit.Casting is not null) return "casting";
        return Unit.Health < Unit.MaxHealth / 3.0 ? "low_hp" : ArtCatalog.DefaultState;
    }

    /// <summary>The status icons to show: crowd control by kind, damage over time, then other buffs and debuffs.</summary>
    public static List<string> Statuses(Unit unit, Battle battle)
    {
        var list = new List<string>();
        foreach (var b in unit.Buffs)
        {
            string status;
            if (b.Def.Cc != CcKind.None) status = JsonField.SnakeCase(b.Def.Cc.ToString());
            else if (b.Def.PeriodicDamage > 0) status = "dot";
            else status = battle.Units.FirstOrDefault(u => u.Id == b.CasterId)?.Side == unit.Side ? "buff" : "debuff";
            if (!list.Contains(status)) list.Add(status);
        }
        return list;
    }
}
