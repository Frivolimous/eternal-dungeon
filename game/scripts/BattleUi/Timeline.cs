using EternalDungeon.Core.Assets;
using EternalDungeon.Core.Combat;
using Godot;
using Side = EternalDungeon.Core.Combat.Side;

namespace EternalDungeon.Game.BattleUi;

/// <summary>
/// The turn order down the left edge (M2 brief §4, §6): upcoming turns as small portraits, sliding into their new order
/// after each action. While an action is hovered, a ghost marker shows where the hero's next turn would land (and
/// where its cast completes). Each enemy's next chip shows its intent: the planned action and its target.
/// </summary>
public partial class Timeline : Control
{
    const int Count = 10;
    const float ChipHeight = 56, Top = 30;

    readonly BattleScreen screen;
    readonly Dictionary<string, Chip> chips = [];

    public Timeline(BattleScreen screen)
    {
        this.screen = screen;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Ready()
    {
        var bg = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        bg.AddThemeStyleboxOverride("panel", Ui.Box(new Color(Ui.Panel, 0.85f), Ui.PanelBorder, 0, 0));
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);
        var title = Ui.Label(Text.T("ui.turn_order"), 12, Ui.Dim);
        title.Position = new Vector2(8, 6);
        AddChild(title);
        Refresh(null);
    }

    /// <summary>Rebuilds the order; <paramref name="ghost"/> adds the hovered action's markers.</summary>
    public void Refresh(TurnEstimate? ghost)
    {
        if (!IsInsideTree()) return;
        var battle = screen.Session.Battle;
        var entries = Preview.Timeline(battle, Count)
            .Select(t => (Key: "", t.Unit, t.Tick, Ghost: (string?)null)).ToList();
        if (ghost is not null && screen.Session.Awaiting is { } hero)
        {
            // The ghost replaces the hero's next predicted turn (its current turn stays first).
            var next = entries.FindIndex(1, e => e.Unit == hero);
            if (next > 0) entries.RemoveAt(next);
            if (ghost.CastCompletes is { } cast) entries.Add(("", hero, cast, Text.T("ui.ghost_cast")));
            if (ghost.NextTurn is { } turn) entries.Add(("", hero, turn, Text.T("ui.ghost_next")));
            var first = entries[0];
            entries = [first, .. entries.Skip(1).OrderBy(e => e.Tick).ThenBy(e => e.Ghost is null ? 0 : 1)];
            entries = [.. entries.Take(Count)];
        }

        // Key each chip by unit and occurrence, so a unit's chips slide rather than jump.
        var seen = new Dictionary<string, int>();
        var keep = new HashSet<string>();
        for (var i = 0; i < entries.Count; i++)
        {
            var (_, unit, tick, ghostLabel) = entries[i];
            var baseKey = (ghostLabel is null ? "" : "ghost:") + unit.Id;
            var n = seen[baseKey] = seen.GetValueOrDefault(baseKey) + 1;
            var key = $"{baseKey}#{n}";
            keep.Add(key);
            if (!chips.TryGetValue(key, out var chip))
            {
                chip = new Chip(screen) { Position = new Vector2(6, Top + i * ChipHeight + 30), Modulate = new Color(1, 1, 1, 0) };
                chips[key] = chip;
                AddChild(chip);
            }
            // An enemy's plan is for its next turn: only its first chip shows it.
            var intent = ghostLabel is null && n == 1 && unit.Alive ? unit.Intent : null;
            chip.Set(unit, tick, ghostLabel, i == 0 && ghostLabel is null && screen.Session.Awaiting is not null, intent);
            var t = chip.CreateTween().SetParallel();
            t.TweenProperty(chip, "position", new Vector2(6, Top + i * ChipHeight), 0.25f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            t.TweenProperty(chip, "modulate:a", ghostLabel is null ? 1f : 0.75f, 0.2f);
        }
        foreach (var key in chips.Keys.Where(k => !keep.Contains(k)).ToList())
        {
            chips[key].QueueFree();
            chips.Remove(key);
        }
    }

    /// <summary>
    /// A small tag on <paramref name="unit"/>'s next chip (a stagger's "−20 Act"). It's the chip's child, so it rides
    /// along as the chip slides back to its later place on the next refresh, then fades.
    /// </summary>
    public void Flash(Unit unit, string text)
    {
        if (!chips.TryGetValue(unit.Id + "#1", out var chip)) return;
        var label = Ui.Label(text, 11, Ui.Stagger);
        label.AddThemeConstantOverride("outline_size", 4);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.Position = new Vector2(42, chip.Size.Y - 17);              // bottom left: the intent sits bottom right
        label.MouseFilter = MouseFilterEnum.Ignore;
        chip.AddChild(label);
        var seconds = Math.Max(0.8f, screen.Main.Settings.ActionSeconds * 3f);
        var t = label.CreateTween();
        t.TweenInterval(seconds * 0.6f);
        t.TweenProperty(label, "modulate:a", 0f, seconds * 0.4f);
        t.TweenCallback(Callable.From(label.QueueFree));
    }

    /// <summary>A trigger changed <paramref name="unit"/>'s plan (a taunt, Stealth, its target falling): its intent
    /// flashes gold on its next chip.</summary>
    public void FlashIntent(Unit unit, float seconds)
    {
        if (chips.TryGetValue(unit.Id + "#1", out var chip)) chip.FlashIntent(seconds);
    }

    /// <summary>One upcoming turn: a small portrait, the unit's name and when, and for an enemy's next turn its plan.</summary>
    partial class Chip(BattleScreen screen) : Control
    {
        const int Icon = 18;

        Unit? unit;
        long tick;
        string? ghost;
        bool now;
        Intent? intent;
        float flash;                                                     // 0–1 while the intent flashes

        public void Set(Unit u, long t, string? ghostLabel, bool current, Intent? plan)
        {
            unit = u;
            tick = t;
            ghost = ghostLabel;
            now = current;
            intent = plan;
            Size = new Vector2(140, ChipHeight - 4);
            MouseFilter = MouseFilterEnum.Ignore;
            QueueRedraw();
        }

        public void FlashIntent(float seconds)
        {
            var t = CreateTween();
            t.TweenMethod(Callable.From<float>(v =>
            {
                flash = v;
                QueueRedraw();
            }), 1f, 0f, Math.Max(0.6f, seconds));
        }

        public override void _Draw()
        {
            if (unit is null) return;
            var font = GetThemeDefaultFont();
            var side = Ui.Side(unit.Side);
            var box = new Rect2(Vector2.Zero, Size);
            DrawRect(box, ghost is null ? new Color(side.Darkened(0.7f), 0.95f) : new Color(0, 0, 0, 0.3f));
            DrawRect(box, ghost is not null ? Ui.Gold : now ? Ui.Gold : new Color(side, 0.8f), false, ghost is not null || now ? 2 : 1);
            var portrait = new Rect2(4, 4, 34, 34);
            DrawFace(font, unit, portrait, 14);
            var x = portrait.End.X + 4;
            var w = Size.X - x - 2;
            var name = unit.Name.Length > 17 ? unit.Name[..16] + "…" : unit.Name;
            DrawString(font, new Vector2(x, 16), ghost ?? name, HorizontalAlignment.Left, w, 10, ghost is null ? Ui.Ink : Ui.Gold);
            DrawString(font, new Vector2(x, 30), Text.F("ui.at_time", ("time", Core.Combat.CombatLog.T(tick))), HorizontalAlignment.Left, w, 10, Ui.Dim);
            if (now) DrawString(font, new Vector2(x, 44), Text.T("ui.now"), HorizontalAlignment.Left, w, 10, Ui.Gold);
            if (intent is not null) DrawIntent(font, intent);
        }

        /// <summary>
        /// The enemy's plan, bottom right (Anchor: Combat › Enemy targeting): the planned action's icon (its initial
        /// until icons exist), then a small portrait of its target ("↑" for a Move, nothing for a self action). "?"
        /// while Confused, "…" when it plans to wait. Gold while it flashes.
        /// </summary>
        void DrawIntent(Font font, Intent plan)
        {
            var d = plan.Decision;
            var hasTarget = d is { Tile: not null } || (d?.Target is { } t0 && t0 != unit);
            var width = plan.Unknown || d is null || !hasTarget ? Icon + 4 : Icon * 2 + 6;
            var box = new Rect2(Size.X - width - 3, Size.Y - Icon - 7, width, Icon + 4);
            DrawRect(box, new Color(0, 0, 0, 0.6f));
            DrawRect(box, flash > 0 ? Ui.Gold.Lerp(Ui.Enemy, 1 - flash) : new Color(Ui.Enemy, 0.8f), false, flash > 0 ? 3 : 1);
            var slot = new Rect2(box.Position + new Vector2(2, 2), new Vector2(Icon, Icon));
            if (plan.Unknown || d is null)
            {
                DrawString(font, slot.Position + new Vector2(0, 14), plan.Unknown ? "?" : "…", HorizontalAlignment.Center, slot.Size.X, 14, Ui.Ink);
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
            var second = new Rect2(slot.Position + new Vector2(Icon + 2, 0), slot.Size);
            if (d.Tile is not null)
            {
                DrawString(font, second.Position + new Vector2(0, 14), "↑", HorizontalAlignment.Center, second.Size.X, 14, Ui.Ink);
                return;
            }
            DrawFace(font, d.Target!, second, 9);
            DrawRect(second, new Color(Ui.Side(d.Target!.Side), 0.9f), false, 1);
        }

        /// <summary>A unit's face: the top square of its portrait (a tall one shows its face), or its initials.</summary>
        void DrawFace(Font font, Unit who, Rect2 rect, int initialsSize)
        {
            if (screen.Main.Art.Portrait(who.Def.Id) is { } tex)
            {
                var square = Math.Min(tex.GetWidth(), tex.GetHeight());
                DrawTextureRectRegion(tex, rect, new Rect2(0, 0, square, square));
                return;
            }
            DrawRect(rect, Ui.Side(who.Side).Darkened(0.45f));
            var initials = string.Concat(who.Def.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w[0]));
            DrawString(font, rect.Position + new Vector2(0, rect.Size.Y * 0.62f), initials, HorizontalAlignment.Center, rect.Size.X, initialsSize, Ui.Ink);
        }
    }
}
